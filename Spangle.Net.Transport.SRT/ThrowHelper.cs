using System.Runtime.CompilerServices;
using Spangle.Interop.Native;

namespace Spangle.Net.Transport.SRT;

public static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ThrowIfError(this int handle)
    {
        if (handle < 0)
        {
            LibSRT.ThrowWithErrorStr();
        }

        return handle;
    }
}
