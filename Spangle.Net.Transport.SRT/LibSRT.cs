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

    public static sockaddr* SockaddrIn(IPEndPoint ep, byte* writeBuffer, int length)
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
        public ushort sin_family;
        public fixed byte sin_port[2];
        public fixed byte sin_addr[4];
        public ulong sin_zero;

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
}
