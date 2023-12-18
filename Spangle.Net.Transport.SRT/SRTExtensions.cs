using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Spangle.Interop.Native;

namespace Spangle.Net.Transport.SRT;

public static class SRTExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ThrowIfError(this int handle)
    {
        if (handle < 0)
        {
            LibSRT.ThrowWithErrorStr();
        }

        return handle;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void LogIfError(this int handle, ILogger logger, LogLevel logLevel = LogLevel.Warning)
    {
        if (handle < 0)
        {
            logger.Log(logLevel, "{}", LibSRT.GetLastErrorStr());
        }
    }

    internal static void DumpHex(this byte[] data, Action<string> output) => DumpHex(new ReadOnlySpan<byte>(data), output);
    internal static void DumpHex(this ReadOnlySpan<byte> data, Action<string> output)
    {
        const int tokenLen = 8;
        const int lineTokensLen = 8;
        while (data.Length > 0)
        {
            var lineTokens = new string[lineTokensLen];
            for (var i = 0; i < lineTokensLen && data.Length > 0; i++)
            {
                int len = Math.Min(tokenLen, data.Length);
                var token = data[..len];
                lineTokens[i] = string.Join(' ', token.ToArray().Select(x => $"{x:X02}"));
                data = data[len..];
            }
            output.Invoke(string.Join("  ", lineTokens));
        }
    }

}
