using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spangle.Net.Transport.SRT;

// ReSharper disable once CheckNamespace
namespace Spangle.Interop.Native;

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static unsafe partial class LibSRT
{
    public const int SRT_ERROR        = -1;
    public const int SRT_INVALID_SOCK = -1;

    public const int AF_INET = 2;

    /// <summary>
    /// Returns last srt error as C# string.
    /// </summary>
    /// <remarks>
    /// Call this method immediately on the same thread where the error occurred.
    /// Otherwise, you will not get the expected value.
    /// See <a href="https://github.com/Haivision/srt/blob/master/docs/API/API-functions.md#diagnostics-1">the official srt document</a>.
    /// </remarks>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetLastErrorStr()
    {
        return new string((sbyte*)srt_getlasterror_str());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DoesNotReturn]
    public static void ThrowWithErrorStr()
    {
        throw new SRTException(GetLastErrorStr());
    }

    public static sockaddr* WriteSockaddrIn(IPEndPoint ep, byte* writeBuffer, int length)
    {
        Debug.Assert(length == 16);
        var bufferSpan = new Span<byte>(writeBuffer, length);
        ref var sin = ref MemoryMarshal.AsRef<sockaddr_in>(bufferSpan);
        sin.sin_family = AF_INET;
        var dataSpan = bufferSpan[2..];
        // 2 bytes port
        BinaryPrimitives.WriteUInt16BigEndian(dataSpan.Slice(0, 2), (ushort)ep.Port);
        // 4 bytes addr
#pragma warning disable CS0618
        BinaryPrimitives.WriteUInt32BigEndian(dataSpan.Slice(2, 4), (uint)ep.Address.Address);
#pragma warning restore CS0618
        // 8 bytes zero
        sin.sin_zero = 0L;

        return (sockaddr*)writeBuffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct sockaddr_in
    {
        public       ushort sin_family;
        public fixed byte   sin_port[2];
        public fixed byte   sin_addr[4];
        public       ulong  sin_zero;

        public readonly ushort Port
        {
            get
            {
                fixed (byte* p = sin_port)
                {
                    return BinaryPrimitives.ReadUInt16BigEndian(new Span<byte>(p, 2));
                }
            }
        }

        public readonly uint Address
        {
            get
            {
                fixed (byte* p = sin_addr)
                {
                    return BinaryPrimitives.ReadUInt16BigEndian(new Span<byte>(p, 4));
                }
            }
        }
    }

    public enum SRT_SOCKSTATUS
    {
        SRTS_INIT       = 1,
        SRTS_OPENED     = 2,
        SRTS_LISTENING  = 3,
        SRTS_CONNECTING = 4,
        SRTS_CONNECTED  = 5,
        SRTS_BROKEN     = 6,
        SRTS_CLOSING    = 7,
        SRTS_CLOSED     = 8,
        SRTS_NONEXIST   = 9,
    }

    public enum SRT_SOCKOPT
    {
        SRTO_MSS                 = 0,
        SRTO_SNDSYN              = 1,
        SRTO_RCVSYN              = 2,
        SRTO_ISN                 = 3,
        SRTO_FC                  = 4,
        SRTO_SNDBUF              = 5,
        SRTO_RCVBUF              = 6,
        SRTO_LINGER              = 7,
        SRTO_UDP_SNDBUF          = 8,
        SRTO_UDP_RCVBUF          = 9,
        SRTO_RENDEZVOUS          = 12,
        SRTO_SNDTIMEO            = 13,
        SRTO_RCVTIMEO            = 14,
        SRTO_REUSEADDR           = 15,
        SRTO_MAXBW               = 16,
        SRTO_STATE               = 17,
        SRTO_EVENT               = 18,
        SRTO_SNDDATA             = 19,
        SRTO_RCVDATA             = 20,
        SRTO_SENDER              = 21,
        SRTO_TSBPDMODE           = 22,
        SRTO_LATENCY             = 23,
        SRTO_INPUTBW             = 24,
        SRTO_OHEADBW             = 25,
        SRTO_PASSPHRASE          = 26,
        SRTO_PBKEYLEN            = 27,
        SRTO_KMSTATE             = 28,
        SRTO_IPTTL               = 29,
        SRTO_IPTOS               = 30,
        SRTO_TLPKTDROP           = 31,
        SRTO_SNDDROPDELAY        = 32,
        SRTO_NAKREPORT           = 33,
        SRTO_VERSION             = 34,
        SRTO_PEERVERSION         = 35,
        SRTO_CONNTIMEO           = 36,
        SRTO_DRIFTTRACER         = 37,
        SRTO_MININPUTBW          = 38,
        SRTO_SNDKMSTATE          = 40,
        SRTO_RCVKMSTATE          = 41,
        SRTO_LOSSMAXTTL          = 42,
        SRTO_RCVLATENCY          = 43,
        SRTO_PEERLATENCY         = 44,
        SRTO_MINVERSION          = 45,
        SRTO_STREAMID            = 46,
        SRTO_CONGESTION          = 47,
        SRTO_MESSAGEAPI          = 48,
        SRTO_PAYLOADSIZE         = 49,
        SRTO_TRANSTYPE           = 50,
        SRTO_KMREFRESHRATE       = 51,
        SRTO_KMPREANNOUNCE       = 52,
        SRTO_ENFORCEDENCRYPTION  = 53,
        SRTO_IPV6ONLY            = 54,
        SRTO_PEERIDLETIMEO       = 55,
        SRTO_BINDTODEVICE        = 56,
        SRTO_GROUPCONNECT        = 57,
        SRTO_GROUPMINSTABLETIMEO = 58,
        SRTO_GROUPTYPE           = 59,
        SRTO_PACKETFILTER        = 60,
        SRTO_RETRANSMITALGO      = 61,
        SRTO_E_SIZE              = 62,
    }

    // same values with `<sys/epoll.h>`.
    [Flags]
    public enum SRT_EPOLL_OPT
    {
        SRT_EPOLL_OPT_NONE = 0,
        SRT_EPOLL_IN       = 1,
        SRT_EPOLL_OUT      = 4,
        SRT_EPOLL_ERR      = 8,
        SRT_EPOLL_CONNECT  = 4,
        SRT_EPOLL_ACCEPT   = 1,
        SRT_EPOLL_UPDATE   = 16,
        SRT_EPOLL_ET       = -2147483648,
    }
}
