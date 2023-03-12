using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
internal sealed class SRTPipe : IDuplexPipe, IDisposable
{
    private const int BufferSize = 4096;

    private readonly Pipe _receivePipe;
    private readonly Pipe _sendPipe;

    private readonly SRTSOCKET    _peerHandle;

    private readonly IntPtr _pReadBuffer;
    private readonly IntPtr _pWriteBuffer;

    private readonly CancellationToken _cancellationToken;

    public PipeReader Input => _receivePipe.Reader;
    public PipeWriter Output => _sendPipe.Writer;

    private bool _disposed;

    public SRTPipe(SRTSOCKET peerHandle, CancellationToken cancellationToken = default)
    {
        _peerHandle = peerHandle;
        _pReadBuffer = Marshal.AllocCoTaskMem(BufferSize);
        _pWriteBuffer = Marshal.AllocCoTaskMem(BufferSize);
        _cancellationToken = cancellationToken;

        var receivePipeOptions = new PipeOptions(useSynchronizationContext: false);
        var sendPipeOptions = new PipeOptions(useSynchronizationContext: false);

        _receivePipe = new Pipe(receivePipeOptions);
        _sendPipe = new Pipe(sendPipeOptions);

        // ReSharper disable AsyncVoidLambda
        receivePipeOptions.ReaderScheduler.Schedule(
            async obj => await (obj as SRTPipe)!.ReadFromSRT().ConfigureAwait(false), this);
        sendPipeOptions.WriterScheduler.Schedule(
            async obj => await (obj as SRTPipe)!.WriteToSRT().ConfigureAwait(false), this);
        // ReSharper restore AsyncVoidLambda
    }

    public void Reset()
    {
        _receivePipe.Reset();
        _sendPipe.Reset();
    }

    private async ValueTask ReadFromSRT()
    {
        var writer = _receivePipe.Writer;
        while (!_disposed)
        {
            int size;
            unsafe
            {
                size = srt_recvmsg(_peerHandle, (byte*)_pReadBuffer.ToPointer(), BufferSize);
            }

            if (size == SRT_ERROR)
            {
                await writer.CompleteAsync(new SRTException(GetLastErrorStr())).ConfigureAwait(false);
                return;
            }

            unsafe
            {
                fixed (void* p = writer.GetSpan(size))
                {
                    Buffer.MemoryCopy(_pReadBuffer.ToPointer(), p, size, size);
                }
            }
            writer.Advance(size);
            var result = await writer.FlushAsync(_cancellationToken).ConfigureAwait(false);
            if (result.IsCanceled || result.IsCompleted)
            {
                return;
            }
        }
    }

    private async ValueTask WriteToSRT()
    {
        var reader = _sendPipe.Reader;
        while (!_disposed)
        {
            var readResult = await reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
            if (readResult.IsCanceled || readResult.IsCompleted)
            {
                return;
            }

            var buff = readResult.Buffer;
            if (buff.Length > BufferSize)
            {
                buff = buff.Slice(buff.Start, BufferSize);
            }

            int writeResult;
            unsafe
            {
                buff.CopyTo(new Span<byte>(_pWriteBuffer.ToPointer(), BufferSize));
                reader.AdvanceTo(buff.End);
                writeResult = srt_send(_peerHandle, (byte*)_pWriteBuffer.ToPointer(), (int)buff.Length);
            }

            if (writeResult == SRT_ERROR)
            {
                await reader.CompleteAsync(new SRTException(GetLastErrorStr())).ConfigureAwait(false);
                return;
            }
        }
    }

    private void ReleaseUnmanagedResources()
    {
        Marshal.FreeCoTaskMem(_pReadBuffer);
        Marshal.FreeCoTaskMem(_pWriteBuffer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        ReleaseUnmanagedResources();

        _receivePipe.Reader.Complete();
        _receivePipe.Writer.Complete();

        _sendPipe.Reader.Complete();
        _sendPipe.Writer.Complete();

        GC.SuppressFinalize(this);
        _disposed = true;
    }

    ~SRTPipe() => ReleaseUnmanagedResources();
}
