using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using Microsoft.Extensions.Logging;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
public class SRTClient : IDisposable
{
    private bool _disposed;

    internal readonly SRTPipe InternalPipe;
    private readonly  ILogger _logger;

    public EndPoint RemoteEndPoint { get; }
    public SRTSOCKET PeerHandle { get; }
    public bool IsCompleted { get; private set; }

    public IDuplexPipe Pipe => InternalPipe;

    internal SRTClient(SRTSOCKET peerHandle, EndPoint remoteEndPoint, ILogger logger)
    {
        InternalPipe = new SRTPipe(peerHandle);
        PeerHandle = peerHandle;
        _logger = logger;
        RemoteEndPoint = remoteEndPoint;
    }

    internal void MarkCompleted()
    {
        IsCompleted = true;
    }

    private void ReleaseUnmanagedResources()
    {
        srt_close(PeerHandle).LogIfError(_logger);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        ReleaseUnmanagedResources();
        if (disposing)
        {
            InternalPipe.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~SRTClient() => Dispose(false);
}
