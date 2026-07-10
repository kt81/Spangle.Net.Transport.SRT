using System.Text;

namespace Spangle.Net.Transport.SRT;

/// <summary>
/// Options applied to the listening socket before it starts listening.
/// Accepted connections inherit them.
/// </summary>
public sealed class SRTListenerOptions
{
    private readonly string? _passphrase;

    /// <summary>
    /// Pre-shared passphrase for SRT encryption (SRTO_PASSPHRASE).
    /// libsrt derives AES key material from it via PBKDF2; senders must present
    /// the same passphrase or their handshake is rejected. 10 to 79 bytes
    /// (UTF-8), or null to accept unencrypted connections only.
    /// </summary>
    public string? Passphrase
    {
        get => _passphrase;
        init
        {
            if (value is not null)
            {
                int byteCount = Encoding.UTF8.GetByteCount(value);
                if (byteCount is < 10 or > 79)
                {
                    throw new ArgumentOutOfRangeException(nameof(Passphrase), byteCount,
                        "The SRT passphrase must be 10 to 79 bytes long (UTF-8).");
                }
            }
            _passphrase = value;
        }
    }
}
