using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Text;
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

    /// <summary>
    /// The stream id the sender presented (SRTO_STREAMID; e.g.
    /// <c>srt://host:port?streamid=...</c>). The SRT counterpart of an RTMP
    /// stream key: route and authorize publishes with it. Empty when the
    /// sender did not set one.
    /// </summary>
    public string StreamId { get; }

    public IDuplexPipe Pipe => InternalPipe;

    internal SRTClient(SRTSOCKET peerHandle, EndPoint remoteEndPoint, ILogger logger)
    {
        InternalPipe = new SRTPipe(peerHandle, logger);
        PeerHandle = peerHandle;
        _logger = logger;
        RemoteEndPoint = remoteEndPoint;
        StreamId = ReadStreamId(peerHandle);
    }

    private static unsafe string ReadStreamId(SRTSOCKET peerHandle)
    {
        // SRT limits stream ids to 512 bytes
        Span<byte> buff = stackalloc byte[512];
        int len = buff.Length;
        fixed (byte* p = buff)
        {
            if (srt_getsockflag(peerHandle, (int)SRT_SOCKOPT.SRTO_STREAMID, p, &len) == SRT_ERROR)
            {
                // diagnostics must not fail the accept
                return string.Empty;
            }
        }

        return len <= 0 ? string.Empty : Encoding.UTF8.GetString(buff[..len]);
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
