using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AsyncAwaitBestPractices;
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

    // ReSharper disable once UnusedMember.Local
    private static readonly StaticFinalizeHandle s_finalizeHandle = StaticFinalizeHandle.Instance;

    private static readonly int s_socketAddressSize = Marshal.SizeOf<sockaddr>();

    private readonly IPEndPoint      _serverSocketEP;
    private readonly SRTSOCKET       _listenHandle;
    private readonly SRTSOCKET       _listenEpollId;
    private readonly SRTSessionEpollProxy _sessionEpollProxy;
    private readonly ILogger         _logger;

    private bool _active;
    private bool _disposed;

    static SRTListener()
    {
        if (srt_startup() == SRT_ERROR)
        {
            ThrowWithErrorStr();
        }
    }

    public SRTListener(IPEndPoint localEP, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(localEP);
        _serverSocketEP = localEP;
        _logger = logger ?? new NullLogger<SRTListener>();

        _listenHandle = srt_create_socket().ThrowIfError();
        _listenEpollId = srt_epoll_create().ThrowIfError();
        // TODO ct
        _sessionEpollProxy = new SRTSessionEpollProxy(DefaultHandleCapacity, _logger, default);
        _sessionEpollProxy.Start().SafeFireAndForget(e => _logger.LogError("Fatal: {}", e));
    }

    public unsafe void Start(int backlog = (int)SocketOptionName.MaxConnections)
    {
        if (_active)
        {
            return;
        }

        var falsy = 0;
        srt_setsockopt(_listenHandle, 0, (int)SRT_SOCKOPT.SRTO_RCVSYN, &falsy, sizeof(int)).ThrowIfError();

        // The C example says the following, but I see no reason why it should be this size.
        // https://github.com/Haivision/srt/blob/39822840c506d72cef5a742d28f32ea28e144345/examples/recvlive.cpp#L66-L71
        // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        // {
        //     var mss = 1052;
        //     srt_setsockopt(_listenHandle, 0, (int)SRT_SOCKOPT.SRTO_MSS, &mss, sizeof(int)).ThrowIfError();
        // }

        IntPtr pSockAddrIn = Marshal.AllocCoTaskMem(s_socketAddressSize);
        try
        {
            var sin = WriteSockaddrIn(_serverSocketEP, (byte*)pSockAddrIn.ToPointer(), s_socketAddressSize);
            srt_bind(_listenHandle, sin, s_socketAddressSize).ThrowIfError();
            srt_listen(_listenHandle, backlog).ThrowIfError();
        }
        finally
        {
            Marshal.FreeCoTaskMem(pSockAddrIn);
        }

        _logger.LogInformation("{} started to listen on {}:{}", nameof(SRTListener), _serverSocketEP.Address.ToString(),
            _serverSocketEP.Port);

        // add to epoll the listening handle
        var events = (int)(SRT_EPOLL_OPT.SRT_EPOLL_IN | SRT_EPOLL_OPT.SRT_EPOLL_ERR);
        srt_epoll_add_usock(_listenEpollId, _listenHandle, &events).ThrowIfError();

        _active = true;
    }

    public ValueTask<SRTClient> AcceptSRTClientAsync(CancellationToken ct = default)
    {
        return WaitAndWrap(AcceptAsync(ct));

        async ValueTask<SRTClient> WaitAndWrap(ValueTask<(SRTSOCKET, IPEndPoint)> task)
        {
            (int peerHandle, IPEndPoint ep) = await task.ConfigureAwait(false);
            var client =  new SRTClient(peerHandle, ep, _logger);
            if (!_sessionEpollProxy.TryAddToControl(client))
            {
                // TODO create new instance if fail
            }
            return client;
        }
    }

    /// <summary>
    /// Accept connection with epoll feature (not a system call, libsrt's own implementation).
    /// Once the connection is accepted, the socket handle is returned immediately and is not included in this epoll instance.
    /// This method uses srt_epoll_*, but the purpose is only for cancellation.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public unsafe ValueTask<(SRTSOCKET, IPEndPoint)> AcceptAsync(CancellationToken ct = default)
    {
        if (!_active)
        {
            throw new InvalidOperationException("SRTListener has not Start()-ed.");
        }

        // Do not change this over 1. Otherwise the return value must be AsyncEnumerable or possible to drop some connections.
        const int numClientPerAcceptIsOne = 1;
        int* acceptHandles = stackalloc SRTSOCKET[numClientPerAcceptIsOne];
        SRTSOCKET phHandles = 0;
        var sysPhHandles = 0UL;
        var zeroLen = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int size = numClientPerAcceptIsOne;

            int numHandles = srt_epoll_wait(_listenEpollId, acceptHandles, &size, &phHandles, &zeroLen,
                AcceptTimeout, &sysPhHandles, &zeroLen, &sysPhHandles, &zeroLen);
            Debug.Assert(numHandles <= numClientPerAcceptIsOne);
            if (numHandles < numClientPerAcceptIsOne)
            {
                continue;
            }

            SRTSOCKET s = acceptHandles[0];
            SRT_SOCKSTATUS status = (SRT_SOCKSTATUS)srt_getsockstate(s);

            if (status is SRT_SOCKSTATUS.SRTS_BROKEN
                or SRT_SOCKSTATUS.SRTS_NONEXIST
                or SRT_SOCKSTATUS.SRTS_CLOSED)
            {
                srt_close(s).ThrowIfError();
                throw new SRTException("Source disconnected: " + status);
            }

            Debug.Assert(status == SRT_SOCKSTATUS.SRTS_LISTENING);

            IntPtr pPeerAddr = IntPtr.Zero;
            IntPtr pPeerAddrSize = IntPtr.Zero;
            try
            {
                pPeerAddr = Marshal.AllocCoTaskMem(s_socketAddressSize);
                pPeerAddrSize = Marshal.AllocCoTaskMem(Marshal.SizeOf<int>());
                SRTSOCKET peerHandle = srt_accept(_listenHandle, (sockaddr*)pPeerAddr.ToPointer(),
                    (int*)pPeerAddrSize.ToPointer());
                if (peerHandle == SRT_INVALID_SOCK)
                {
                    ThrowWithErrorStr();
                }

                var ep = ConvertToEndPoint(pPeerAddr);

                return ValueTask.FromResult((peerHandle, ep));
            }
            finally
            {
                if (pPeerAddr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pPeerAddr);
                }

                if (pPeerAddrSize != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pPeerAddrSize);
                }
            }
        }
    }

    private static unsafe IPEndPoint ConvertToEndPoint(IntPtr pPeerAddr)
    {
        ref readonly sockaddr_in addr =
            ref MemoryMarshal.AsRef<sockaddr_in>(new ReadOnlySpan<byte>(pPeerAddr.ToPointer(), s_socketAddressSize));
        return new IPEndPoint(addr.Address, addr.Port);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

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
