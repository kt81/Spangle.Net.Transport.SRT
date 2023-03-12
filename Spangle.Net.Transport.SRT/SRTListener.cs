using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Spangle.Interop.Native;
using static Spangle.Interop.Native.LibSRT;

namespace Spangle.Net.Transport.SRT;

[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
public sealed class SRTListener : IDisposable
{
    // [SuppressMessage("ReSharper", "InconsistentNaming")] private const int SRT_ERROR = -1;

    // ReSharper disable once UnusedMember.Local
    private static readonly StaticFinalizeHandle s_finalizeHandle = StaticFinalizeHandle.Instance;

    private static readonly int s_socketAddressSize = Marshal.SizeOf<sockaddr>();

    private readonly IPEndPoint _serverSocketEP;
    private readonly SRTSOCKET  _handle;

    private bool _active;
    private bool _disposed;

    private readonly CancellationTokenSource _tokenSource = new();

    static SRTListener()
    {
        if (srt_startup() == SRT_ERROR)
        {
            ThrowWithErrorStr();
        }
    }

    public SRTListener(IPEndPoint localEP)
    {
        ThrowHelper.ThrowIfNull(localEP);
        _serverSocketEP = localEP;
        _handle = srt_create_socket();
        if (_handle == SRT_ERROR)
        {
            ThrowWithErrorStr();
        }
    }

    public unsafe void Start(int backlog = (int)SocketOptionName.MaxConnections)
    {
        if (_active)
        {
            return;
        }

        IntPtr pSockAddrIn = Marshal.AllocCoTaskMem(s_socketAddressSize);
        try
        {
            var sin = SockaddrIn(_serverSocketEP, (byte*)pSockAddrIn.ToPointer(), s_socketAddressSize);
            int result = srt_bind(_handle, sin, s_socketAddressSize);
            ThrowHelper.ThrowIfError(result);
            result = srt_listen(_handle, backlog);
            ThrowHelper.ThrowIfError(result);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pSockAddrIn);
        }

        _active = true;
    }

    public ValueTask<SRTClient> AcceptSRTClientAsync()
    {
        if (!_active)
        {
            throw new InvalidOperationException("SRTListener has not Start-ed.");
        }

        IntPtr pPeerAddr = IntPtr.Zero;
        IntPtr pPeerAddrSize = IntPtr.Zero;
        try
        {
            pPeerAddr = Marshal.AllocCoTaskMem(s_socketAddressSize);
            pPeerAddrSize = Marshal.AllocCoTaskMem(Marshal.SizeOf<int>());
            SRTSOCKET peerHandle;
            unsafe
            {
                peerHandle = srt_accept(_handle, (sockaddr*)pPeerAddr.ToPointer(), (int*)pPeerAddrSize.ToPointer());
                if (peerHandle == SRT_INVALID_SOCK)
                {
                    ThrowWithErrorStr();
                }
            }

            var ep = ConvertToEndPoint(pPeerAddr);

            return ValueTask.FromResult(new SRTClient(peerHandle, ep, _tokenSource.Token));
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

        if (_handle >= 0)
        {
            srt_close(_handle);
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
            // Marshal.FreeCoTaskMem(s_pFalsy);
            // Marshal.FreeCoTaskMem(s_pEventsForAccept);
            srt_cleanup();
        }
    }
}
