using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spangle.Interop.Native;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
public sealed class SRTListener : IDisposable
{
    private const int AcceptTimeout         = 100;
    private const int DefaultHandleCapacity = 10;
    private const int AcceptQueueCapacity   = 16;

    // ReSharper disable once UnusedMember.Local
    private static readonly StaticFinalizeHandle s_finalizeHandle = StaticFinalizeHandle.Instance;

    private static readonly int s_socketAddressSize = Marshal.SizeOf<sockaddr>();

    private readonly IPEndPoint _serverSocketEP;
    private readonly SRTSOCKET  _listenHandle;
    private readonly SRTSOCKET  _listenEpollId;
    private readonly ILogger    _logger;

    private readonly CancellationTokenSource _cts;

    // The accept thread produces accepted sockets; AcceptSRTClientAsync consumes
    // them truly asynchronously. The bounded capacity acts as the accept backlog.
    private readonly Channel<(SRTSOCKET Handle, IPEndPoint RemoteEndPoint)> _acceptQueue;

    private readonly IList<SRTSessionEpollHandler> _sessionEpollHandlers;
    private          SRTSessionEpollHandler        _sessionEpollHandler;

    private bool _active;
    private bool _disposed;

    static SRTListener()
    {
        if (srt_startup() == SRT_ERROR)
        {
            ThrowWithErrorStr();
        }
    }

    private readonly SRTListenerOptions? _options;

    public SRTListener(IPEndPoint localEP, ILogger? logger = null)
        : this(localEP, null, logger)
    {
    }

    public SRTListener(IPEndPoint localEP, SRTListenerOptions? options, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(localEP);
        _serverSocketEP = localEP;
        _options = options;
        _logger = logger ?? new NullLogger<SRTListener>();
        _cts = new CancellationTokenSource();
        _acceptQueue = Channel.CreateBounded<(SRTSOCKET, IPEndPoint)>(new BoundedChannelOptions(AcceptQueueCapacity)
        {
            SingleWriter = true,
        });

        _listenHandle = srt_create_socket().ThrowIfError();
        _listenEpollId = srt_epoll_create().ThrowIfError();
        _sessionEpollHandlers = new List<SRTSessionEpollHandler>();
        _sessionEpollHandler = AddNewHandler();
    }

    private SRTSessionEpollHandler AddNewHandler()
    {
        var handler = new SRTSessionEpollHandler(DefaultHandleCapacity, _logger, _cts.Token);
        handler.Start();
        _sessionEpollHandlers.Add(handler);
        return handler;
    }

    private SRTSessionEpollHandler FindOrAddHandler()
    {
        foreach (SRTSessionEpollHandler handler in _sessionEpollHandlers)
        {
            if (!handler.IsFull)
            {
                return handler;
            }
        }
        return AddNewHandler();
    }

    public unsafe void Start(int backlog = (int)SocketOptionName.MaxConnections)
    {
        if (_active)
        {
            return;
        }

        var falsy = 0;
        srt_setsockopt(_listenHandle, 0, (int)SRT_SOCKOPT.SRTO_RCVSYN, &falsy, sizeof(int)).ThrowIfError();

        if (_options?.Passphrase is { } passphrase)
        {
            // accepted sockets inherit the listener's passphrase
            byte[] passBytes = System.Text.Encoding.UTF8.GetBytes(passphrase);
            fixed (byte* pp = passBytes)
            {
                srt_setsockopt(_listenHandle, 0, (int)SRT_SOCKOPT.SRTO_PASSPHRASE, pp, passBytes.Length)
                    .ThrowIfError();
            }
        }

        var sockAddrIn = new sockaddr_in();
        var sin = WriteSockaddrIn(_serverSocketEP, &sockAddrIn, s_socketAddressSize);
        srt_bind(_listenHandle, sin, s_socketAddressSize).ThrowIfError();
        srt_listen(_listenHandle, backlog).ThrowIfError();

        _logger.LogInformation("{} started to listen on {}:{}", nameof(SRTListener), _serverSocketEP.Address.ToString(),
            _serverSocketEP.Port);

        // add to epoll the listening handle
        var events = (int)(SRT_EPOLL_OPT.SRT_EPOLL_IN | SRT_EPOLL_OPT.SRT_EPOLL_ERR);
        srt_epoll_add_usock(_listenEpollId, _listenHandle, &events).ThrowIfError();

        // The accept loop blocks in srt_epoll_uwait, so it lives on its own thread;
        // consumers only ever touch the channel.
        var thread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "srt-accept",
        };
        thread.Start();

        _active = true;
    }

    /// <summary>
    /// Accepts a new SRT connection.
    /// Truly asynchronous: awaiting this does not block any thread.
    /// </summary>
    public async ValueTask<SRTClient> AcceptSRTClientAsync(CancellationToken ct = default)
    {
        if (!_active)
        {
            throw new InvalidOperationException("SRTListener has not Start()-ed.");
        }

        (SRTSOCKET peerHandle, IPEndPoint ep) = await _acceptQueue.Reader.ReadAsync(ct).ConfigureAwait(false);
        var client = new SRTClient(peerHandle, ep, _logger);

        // A full handler is not an error: find or grow, then register.
        // (TryAddToControl throws on real epoll failures.)
        while (!_sessionEpollHandler.TryAddToControl(client))
        {
            _sessionEpollHandler = FindOrAddHandler();
        }

        return client;
    }

    /// <summary>
    /// Blocking accept loop on the dedicated thread. Uses srt_epoll only to make
    /// the wait cancellable; accepted sockets are handed to the channel.
    /// </summary>
    private unsafe void AcceptLoop()
    {
        ChannelWriter<(SRTSOCKET, IPEndPoint)> queue = _acceptQueue.Writer;
        try
        {
            // Do not change this over 1. Otherwise some connections may be dropped.
            const int numClientPerAcceptIsOne = 1;
            SRT_EPOLL_EVENT_STR* eventHandles = stackalloc SRT_EPOLL_EVENT_STR[numClientPerAcceptIsOne];

            while (!_cts.Token.IsCancellationRequested)
            {
                int numHandles = srt_epoll_uwait(_listenEpollId, eventHandles, numClientPerAcceptIsOne, AcceptTimeout);
                Debug.Assert(numHandles <= numClientPerAcceptIsOne);
                if (numHandles < numClientPerAcceptIsOne)
                {
                    if (numHandles == -1 && IsLastError(SRT_ERRNO.SRT_ETIMEOUT))
                    {
                        _logger.LogError("Error on accepting: {}", GetLastErrorStr());
                    }

                    continue;
                }

                SRTSOCKET s = eventHandles[0].fd;
                var status = (SRT_SOCKSTATUS)srt_getsockstate(s);
                if (status is SRT_SOCKSTATUS.SRTS_BROKEN
                    or SRT_SOCKSTATUS.SRTS_NONEXIST
                    or SRT_SOCKSTATUS.SRTS_CLOSED)
                {
                    // The only socket in this epoll is the listener itself: fatal.
                    srt_close(s).LogIfError(_logger);
                    throw new SRTException("Listener socket broken: " + status);
                }

                Debug.Assert(status == SRT_SOCKSTATUS.SRTS_LISTENING);

                var peerAddr = new sockaddr_in();
                int peerAddrSize = s_socketAddressSize;
                SRTSOCKET peerHandle = srt_accept(_listenHandle, (sockaddr*)&peerAddr, &peerAddrSize);
                if (peerHandle == SRT_INVALID_SOCK)
                {
                    _logger.LogError("srt_accept failed: {}", GetLastErrorStr());
                    continue;
                }

                var ep = new IPEndPoint(peerAddr.Address, peerAddr.Port);
                _logger.LogDebug("Accepted peer handle: {} ({})", peerHandle, ep);

                // Block (this thread only) while consumers are behind; the bounded
                // queue is the accept backlog.
                while (!queue.TryWrite((peerHandle, ep)))
                {
                    if (_cts.Token.IsCancellationRequested)
                    {
                        srt_close(peerHandle).LogIfError(_logger);
                        return;
                    }
                    Thread.Sleep(10);
                }
            }

            queue.TryComplete();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SRT accept loop stopped");
            queue.TryComplete(e);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _acceptQueue.Writer.TryComplete();

        if (_listenHandle >= 0)
        {
            srt_epoll_release(_listenEpollId).LogIfError(_logger);
            srt_close(_listenHandle).LogIfError(_logger);
            // DO NOT do srt_cleanup() here
        }

        GC.SuppressFinalize(this);
        _disposed = true;
    }

    ~SRTListener()
    {
        Dispose();
    }

    private sealed class StaticFinalizeHandle
    {
        public static StaticFinalizeHandle Instance { get; } = new();

        private StaticFinalizeHandle()
        {
        }

        ~StaticFinalizeHandle()
        {
            srt_cleanup();
        }
    }
}
