using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using AsyncAwaitBestPractices;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
internal sealed class SRTPipe : IDuplexPipe, IDisposable
{
    private const int BufferSize = 9400;

    private readonly Pipe _receivePipe;
    private readonly Pipe _sendPipe;

    private readonly SRTSOCKET _peerHandle;

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

        sendPipeOptions.WriterScheduler.Schedule(SendRelay, this);
    }

    public void Reset()
    {
        _receivePipe.Reset();
        _sendPipe.Reset();
    }

    internal unsafe void ReadFromSRT()
    {
        var writer = _receivePipe.Writer;
        if (_disposed)
        {
            return;
        }

        int size = srt_recvmsg(_peerHandle, (byte*)_pReadBuffer.ToPointer(), BufferSize);

        if (size == SRT_ERROR)
        {
            writer.CompleteAsync(new SRTException(GetLastErrorStr())).SafeFireAndForget();
            return;
        }

        fixed (void* p = writer.GetSpan(size))
        {
            Buffer.MemoryCopy(_pReadBuffer.ToPointer(), p, size, size);
        }

        writer.Advance(size);
        WrapAsync(writer.FlushAsync(_cancellationToken)).SafeFireAndForget();
    }

    private static async ValueTask WrapAsync<T>(ValueTask<T> task)
    {
        await task.ConfigureAwait(false);
    }

    private static async void SendRelay(object? s)
    {
        var self = (SRTPipe)s!;
        if (self._disposed)
        {
            return;
        }

        while (true)
        {
            if (self._cancellationToken.IsCancellationRequested || self._disposed)
            {
                break;
            }

            await WriteToSRT(self).ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteToSRT(SRTPipe self)
    {
        var reader = self._sendPipe.Reader;
        ReadResult readResult;

        try
        {
            readResult = await reader.ReadAsync(self._cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            if (self._disposed) return;
            throw;
        }

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
            buff.CopyTo(new Span<byte>(self._pWriteBuffer.ToPointer(), BufferSize));
            reader.AdvanceTo(buff.End);
            writeResult = srt_send(self._peerHandle, (byte*)self._pWriteBuffer.ToPointer(), (int)buff.Length);
        }

        if (writeResult == SRT_ERROR)
        {
            await reader.CompleteAsync(new SRTException(GetLastErrorStr())).ConfigureAwait(false);
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
