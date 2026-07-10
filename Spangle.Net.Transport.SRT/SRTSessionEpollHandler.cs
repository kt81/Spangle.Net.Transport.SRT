using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;
using Spangle.Interop.Native;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

/// <summary>
/// Controls multiple connections up to the capacity on one dedicated polling thread.
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
        _sessionHandles = new ConcurrentDictionary<SRTSOCKET, SRTClient>(c, c);
        _sessionEpollId = srt_epoll_create();
    }

    /// <summary>
    /// Registers the client with this handler.
    /// Returns false only when the handler is full; a failing epoll registration throws.
    /// </summary>
    internal unsafe bool TryAddToControl(SRTClient client)
    {
        if (IsFull)
        {
            return false;
        }

        SRTSOCKET peerHandle = client.PeerHandle;
        var events = (int)(SRT_EPOLL_OPT.SRT_EPOLL_IN | SRT_EPOLL_OPT.SRT_EPOLL_ERR);
        srt_epoll_add_usock(_sessionEpollId, peerHandle, &events).ThrowIfError();

        _sessionHandles[peerHandle] = client;

        return true;
    }

    public void Start()
    {
        lock (this)
        {
            if (_isActive)
            {
                throw new InvalidOperationException("Already activated");
            }
            _isActive = true;

            // The poll loop blocks in srt_epoll_uwait for its whole lifetime,
            // so it gets its own thread instead of starving the thread pool.
            var thread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = $"srt-epoll-{_sessionEpollId}",
            };
            thread.Start();
        }
    }

    /// <summary>
    /// Poll loop for one epoll group.
    /// Must not throw; failures are logged and the connection is closed instead.
    /// </summary>
    private unsafe void ReadLoop()
    {
        try
        {
            const int timeout = 100;
            int maxSize = _capacity * 2;
            var handles = stackalloc SRT_EPOLL_EVENT_STR[maxSize];
            while (!_token.IsCancellationRequested)
            {
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
                    _logger.LogTrace("Incoming epoll event ({}): {}", s, (SRT_EPOLL_OPT)handle.events);

                    if (!_sessionHandles.TryGetValue(s, out SRTClient? client))
                    {
                        continue;
                    }

                    var status = (SRT_SOCKSTATUS)srt_getsockstate(s);
                    if (status is SRT_SOCKSTATUS.SRTS_BROKEN
                        or SRT_SOCKSTATUS.SRTS_NONEXIST
                        or SRT_SOCKSTATUS.SRTS_CLOSED)
                    {
                        CloseAndRemove(s, client, $"source disconnected: {status}");
                        continue;
                    }

                    ValueTask<FlushResult> flush = client.InternalPipe.ReadFromSRT();
                    if (flush.IsCompletedSuccessfully)
                    {
                        if (flush.Result.IsCompleted || flush.Result.IsCanceled)
                        {
                            CloseAndRemove(s, client, "downstream completed");
                        }
                        continue;
                    }

                    // Backpressure: the downstream has not consumed enough yet.
                    // Stop polling this socket for input; libsrt keeps buffering
                    // within its own receive window in the meantime.
                    PauseReading(s);
                    ResumeWhenFlushed(s, client, flush);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SRT session epoll loop crashed");
        }
        finally
        {
            Dispose();
        }
    }

    private void ResumeWhenFlushed(SRTSOCKET s, SRTClient client, ValueTask<FlushResult> flush)
    {
        _ = Impl();
        return;

        async Task Impl()
        {
            try
            {
                FlushResult result = await flush.ConfigureAwait(false);
                if (result.IsCompleted || result.IsCanceled)
                {
                    CloseAndRemove(s, client, "downstream completed");
                    return;
                }
                ResumeReading(s);
            }
            catch (Exception e)
            {
                _logger.LogDebug("Flush failed on {}: {}", s, e.Message);
                CloseAndRemove(s, client, "flush failed");
            }
        }
    }

    // srt_epoll_update_usock is safe to call from any thread; the epoll
    // container is guarded inside libsrt.
    private unsafe void PauseReading(SRTSOCKET s)
    {
        var events = (int)SRT_EPOLL_OPT.SRT_EPOLL_ERR;
        srt_epoll_update_usock(_sessionEpollId, s, &events).LogIfError(_logger);
    }

    private unsafe void ResumeReading(SRTSOCKET s)
    {
        var events = (int)(SRT_EPOLL_OPT.SRT_EPOLL_IN | SRT_EPOLL_OPT.SRT_EPOLL_ERR);
        srt_epoll_update_usock(_sessionEpollId, s, &events).LogIfError(_logger);
    }

    private void CloseAndRemove(SRTSOCKET s, SRTClient client, string reason)
    {
        if (!_sessionHandles.TryRemove(s, out _))
        {
            return; // already handled by a concurrent path
        }

        srt_epoll_remove_usock(_sessionEpollId, s).LogIfError(_logger);
        srt_close(s).LogIfError(_logger);
        client.InternalPipe.CompleteReceive();
        client.MarkCompleted();
        _logger.LogDebug("Connection removed ({}): {}", s, reason);
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
