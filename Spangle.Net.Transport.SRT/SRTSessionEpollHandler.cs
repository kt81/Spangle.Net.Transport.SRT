using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Spangle.Interop.Native;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

/// <summary>
/// Control multiple connections until the capacity.
/// The capacity is not promised strictly.
/// </summary>
[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
internal sealed class SRTSessionEpollHandler : IDisposable
{
    private readonly int               _capacity;
    private readonly ILogger           _logger;
    private readonly CancellationToken _token;
    private readonly SRTSOCKET         _sessionEpollId;

    private readonly ConcurrentDictionary<SRTSOCKET, SRTClient> _sessionHandles;

    private bool _isActive;

    public bool IsFull => _sessionHandles.Count >= _capacity;

    internal SRTSessionEpollHandler(int capacity, ILogger logger, CancellationToken token)
    {
        _logger = logger;
        _token = token;
        _capacity = capacity;
        int c = capacity * 2;
        _sessionHandles = new ConcurrentDictionary<int, SRTClient>(c, c);
        _sessionEpollId = srt_epoll_create();
    }

    internal unsafe bool TryAddToControl(SRTClient client)
    {
        if (IsFull)
        {
            return false;
        }

        SRTSOCKET peerHandle = client.PeerHandle;
        var events = (int)(SRT_EPOLL_OPT.SRT_EPOLL_IN | SRT_EPOLL_OPT.SRT_EPOLL_ERR);
        int result = srt_epoll_add_usock(_sessionEpollId, peerHandle, &events);
        if (result < 0)
        {
            _logger.LogWarning("TryAdd: {}", GetLastErrorStr());
            return false;
        }

        _sessionHandles[peerHandle] = client;

        return true;
    }

    public Task Start()
    {
        lock (this)
        {
            if (!_isActive)
            {
                return Task.Run(BeginRead, _token);
            }
        }

        throw new InvalidOperationException("Already activated");
    }

    /// <summary>
    /// Loop for same epoll group.
    /// </summary>
    /// <remarks>
    /// This method must not throw an exception, but instead log and close the connection.
    /// </remarks>
    private unsafe void BeginRead()
    {
        if (_isActive)
        {
            throw new InvalidOperationException("Already started.");
        }

        _isActive = true;

        const int timeout = 100;
        int maxSize = _capacity * 2;
        var handles = stackalloc SRT_EPOLL_EVENT_STR[maxSize];
        while (true)
        {
            if (_token.IsCancellationRequested) break;

            if (_sessionHandles.IsEmpty)
            {
                Thread.Sleep(100);
                continue;
            }

            int numHandles = srt_epoll_uwait(_sessionEpollId, handles, maxSize, timeout);
            Debug.Assert(numHandles <= maxSize);
            if (numHandles <= 0)
            {
                continue;
            }

            for (var i = 0; i < numHandles; i++)
            {
                ref SRT_EPOLL_EVENT_STR handle = ref handles[i];
                SRTSOCKET s = handle.fd;
                var events = (SRT_EPOLL_OPT)handle.events;
                _logger.LogTrace("Incoming epoll event ({}): {}", s, events);
                SRT_SOCKSTATUS status = (SRT_SOCKSTATUS)srt_getsockstate(s);

                if (status is SRT_SOCKSTATUS.SRTS_BROKEN
                    or SRT_SOCKSTATUS.SRTS_NONEXIST
                    or SRT_SOCKSTATUS.SRTS_CLOSED)
                {
                    if (_sessionHandles.TryGetValue(s, out var cl))
                    {
                        srt_epoll_remove_usock(_sessionEpollId, s).LogIfError(_logger);
                        srt_close(s).LogIfError(_logger);
                        cl.MarkCompleted();
                        ((IDictionary)_sessionHandles).Remove(s);
                        _logger.LogDebug("Source disconnected: {}", status);
                    }

                    continue;
                }

                _sessionHandles[s].InternalPipe.ReadFromSRT();
            }
        }

        Dispose();
    }

    private void ReleaseUnmanagedResources()
    {
        srt_epoll_release(_sessionEpollId).LogIfError(_logger);
    }

    public void Dispose()
    {
        _sessionHandles.Clear();
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~SRTSessionEpollHandler() => ReleaseUnmanagedResources();
}
