using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
internal sealed class SRTPipe : IDuplexPipe, IDisposable
{
    // Must cover the largest SRT message (SRTO_PAYLOADSIZE; 1456 by default,
    // larger with jumbo MTUs), which srt_recvmsg delivers as one unit.
    private const int BufferSize = 9400;

    private readonly Pipe _receivePipe;
    private readonly Pipe _sendPipe;

    private readonly SRTSOCKET _peerHandle;
    private readonly ILogger   _logger;

    // Send-side staging for multi-segment sequences only; the receive side
    // reads straight into the pipe's own buffer.
    private readonly IntPtr _pWriteBuffer;

    private readonly CancellationToken _cancellationToken;

    public PipeReader Input => _receivePipe.Reader;
    public PipeWriter Output => _sendPipe.Writer;

    private volatile bool _disposed;

    public SRTPipe(SRTSOCKET peerHandle, ILogger logger, CancellationToken cancellationToken = default)
    {
        _peerHandle = peerHandle;
        _logger = logger;
        _pWriteBuffer = Marshal.AllocCoTaskMem(BufferSize);
        _cancellationToken = cancellationToken;

        _receivePipe = new Pipe(new PipeOptions(useSynchronizationContext: false));
        _sendPipe = new Pipe(new PipeOptions(useSynchronizationContext: false));

        _ = RunSendRelayAsync();
    }

    /// <summary>
    /// Receives one SRT message directly into the receive pipe (no staging copy).
    /// Returns the flush of that write so the epoll handler can apply backpressure:
    /// while the flush is incomplete, the socket must not be polled for input again.
    /// </summary>
    internal unsafe ValueTask<FlushResult> ReadFromSRT()
    {
        if (_disposed)
        {
            return new ValueTask<FlushResult>(new FlushResult(false, true));
        }

        PipeWriter writer = _receivePipe.Writer;
        Span<byte> span = writer.GetSpan(BufferSize);
        int size;
        fixed (byte* p = span)
        {
            size = srt_recvmsg(_peerHandle, p, span.Length);
        }

        if (size == SRT_ERROR)
        {
            writer.Complete(new SRTException(GetLastErrorStr()));
            return new ValueTask<FlushResult>(new FlushResult(false, true));
        }
        if (size == 0)
        {
            // orderly shutdown
            writer.Complete();
            return new ValueTask<FlushResult>(new FlushResult(false, true));
        }

        writer.Advance(size);
        return writer.FlushAsync(_cancellationToken);
    }

    /// <summary>
    /// Completes the receive side so downstream readers observe the disconnect
    /// even when it was detected by socket state rather than a failed recv.
    /// </summary>
    internal void CompleteReceive(Exception? exception = null)
    {
        _receivePipe.Writer.Complete(exception);
    }

    private async Task RunSendRelayAsync()
    {
        PipeReader reader = _sendPipe.Reader;
        try
        {
            while (!_disposed)
            {
                ReadResult result = await reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
                if (result.IsCanceled)
                {
                    break;
                }

                // Drain everything that was read; IsCompleted is honored only after
                // the final bytes have been relayed.
                ReadOnlySequence<byte> buff = result.Buffer;
                while (buff.Length > 0)
                {
                    ReadOnlySequence<byte> chunk = buff.Length > BufferSize ? buff.Slice(0, BufferSize) : buff;
                    if (SendChunk(chunk) == SRT_ERROR)
                    {
                        throw new SRTException(GetLastErrorStr());
                    }
                    buff = buff.Slice(chunk.End);
                }
                reader.AdvanceTo(result.Buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            await reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception e) when (_disposed)
        {
            // disposal races surface as pipe/socket errors; nothing left to relay
            _logger.LogTrace("Send relay stopped after dispose: {}", e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SRT send relay failed");
            await reader.CompleteAsync(e).ConfigureAwait(false);
        }
    }

    private unsafe int SendChunk(in ReadOnlySequence<byte> chunk)
    {
        if (chunk.IsSingleSegment)
        {
            fixed (byte* p = chunk.FirstSpan)
            {
                return srt_send(_peerHandle, p, (int)chunk.Length);
            }
        }

        chunk.CopyTo(new Span<byte>(_pWriteBuffer.ToPointer(), BufferSize));
        return srt_send(_peerHandle, (byte*)_pWriteBuffer.ToPointer(), (int)chunk.Length);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _receivePipe.Reader.Complete();
        _receivePipe.Writer.Complete();

        _sendPipe.Reader.Complete();
        _sendPipe.Writer.Complete();

        // The write buffer is freed by the finalizer only: the send relay task may
        // still be inside SendChunk on another thread, and it keeps this instance
        // reachable, so the finalizer cannot run before the relay has ended.
    }

    ~SRTPipe() => Marshal.FreeCoTaskMem(_pWriteBuffer);
}
