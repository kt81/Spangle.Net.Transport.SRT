using System.Runtime.CompilerServices;
using Spangle.Interop.Native;

namespace Spangle.Net.Transport.SRT;

public static class ThrowHelper
{
    public static void ThrowIfError(int handle)
    {
        if (handle < 0)
        {
            LibSRT.ThrowWithErrorStr();
        }
    }

    public static void ThrowIfNull(object? argument, [CallerArgumentExpression("argument")]string? paramName = default)
    {
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

}
