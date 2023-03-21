using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
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

    public static string GetLastErrorStr()
    {
        return new string((sbyte*)srt_getlasterror_str());
    }

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

    public static class SRT_SOCKSTATUS
    {
        public const int SRTS_INIT       = 1;
        public const int SRTS_OPENED     = 2;
        public const int SRTS_LISTENING  = 3;
        public const int SRTS_CONNECTING = 4;
        public const int SRTS_CONNECTED  = 5;
        public const int SRTS_BROKEN     = 6;
        public const int SRTS_CLOSING    = 7;
        public const int SRTS_CLOSED     = 8;
        public const int SRTS_NONEXIST   = 9;
    }

    public static class SRT_SOCKOPT
    {
        public const int SRTO_MSS                 = 0;
        public const int SRTO_SNDSYN              = 1;
        public const int SRTO_RCVSYN              = 2;
        public const int SRTO_ISN                 = 3;
        public const int SRTO_FC                  = 4;
        public const int SRTO_SNDBUF              = 5;
        public const int SRTO_RCVBUF              = 6;
        public const int SRTO_LINGER              = 7;
        public const int SRTO_UDP_SNDBUF          = 8;
        public const int SRTO_UDP_RCVBUF          = 9;
        public const int SRTO_RENDEZVOUS          = 12;
        public const int SRTO_SNDTIMEO            = 13;
        public const int SRTO_RCVTIMEO            = 14;
        public const int SRTO_REUSEADDR           = 15;
        public const int SRTO_MAXBW               = 16;
        public const int SRTO_STATE               = 17;
        public const int SRTO_EVENT               = 18;
        public const int SRTO_SNDDATA             = 19;
        public const int SRTO_RCVDATA             = 20;
        public const int SRTO_SENDER              = 21;
        public const int SRTO_TSBPDMODE           = 22;
        public const int SRTO_LATENCY             = 23;
        public const int SRTO_INPUTBW             = 24;
        public const int SRTO_OHEADBW             = 25;
        public const int SRTO_PASSPHRASE          = 26;
        public const int SRTO_PBKEYLEN            = 27;
        public const int SRTO_KMSTATE             = 28;
        public const int SRTO_IPTTL               = 29;
        public const int SRTO_IPTOS               = 30;
        public const int SRTO_TLPKTDROP           = 31;
        public const int SRTO_SNDDROPDELAY        = 32;
        public const int SRTO_NAKREPORT           = 33;
        public const int SRTO_VERSION             = 34;
        public const int SRTO_PEERVERSION         = 35;
        public const int SRTO_CONNTIMEO           = 36;
        public const int SRTO_DRIFTTRACER         = 37;
        public const int SRTO_MININPUTBW          = 38;
        public const int SRTO_SNDKMSTATE          = 40;
        public const int SRTO_RCVKMSTATE          = 41;
        public const int SRTO_LOSSMAXTTL          = 42;
        public const int SRTO_RCVLATENCY          = 43;
        public const int SRTO_PEERLATENCY         = 44;
        public const int SRTO_MINVERSION          = 45;
        public const int SRTO_STREAMID            = 46;
        public const int SRTO_CONGESTION          = 47;
        public const int SRTO_MESSAGEAPI          = 48;
        public const int SRTO_PAYLOADSIZE         = 49;
        public const int SRTO_TRANSTYPE           = 50;
        public const int SRTO_KMREFRESHRATE       = 51;
        public const int SRTO_KMPREANNOUNCE       = 52;
        public const int SRTO_ENFORCEDENCRYPTION  = 53;
        public const int SRTO_IPV6ONLY            = 54;
        public const int SRTO_PEERIDLETIMEO       = 55;
        public const int SRTO_BINDTODEVICE        = 56;
        public const int SRTO_GROUPCONNECT        = 57;
        public const int SRTO_GROUPMINSTABLETIMEO = 58;
        public const int SRTO_GROUPTYPE           = 59;
        public const int SRTO_PACKETFILTER        = 60;
        public const int SRTO_RETRANSMITALGO      = 61;
        public const int SRTO_E_SIZE              = 62;
    }

    // same values with `<sys/epoll.h>`.
    public static class SRT_EPOLL_OPT
    {
        public const int SRT_EPOLL_OPT_NONE = 0;
        public const int SRT_EPOLL_IN       = 1;
        public const int SRT_EPOLL_OUT      = 4;
        public const int SRT_EPOLL_ERR      = 8;
        public const int SRT_EPOLL_CONNECT  = 4;
        public const int SRT_EPOLL_ACCEPT   = 1;
        public const int SRT_EPOLL_UPDATE   = 16;
        public const int SRT_EPOLL_ET       = -2147483648;
    }
}
