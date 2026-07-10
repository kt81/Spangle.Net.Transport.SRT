Spangle.Net.Transport.SRT
=========================

An SRT (Secure Reliable Transport) listener and duplex-pipe transport for .NET,
built on `System.IO.Pipelines`. Part of the [Spangle](https://github.com/kt81/Spangle)
streaming media server family.

The NuGet package is fully self-contained: the native `libsrt` (statically linked
with mbedTLS) ships inside for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`
and `osx-arm64`. No system packages, no vcpkg, nothing to install.

Features
--------

- **Truly asynchronous accept** — a dedicated thread drives `srt_epoll` and feeds a
  bounded channel; `await AcceptSRTClientAsync()` never blocks a thread pool thread
- **Backpressure-aware receive** — when the consumer falls behind, the socket is
  masked out of the epoll set until the pipe drains; memory stays bounded per
  connection while libsrt buffers within its own receive window
- **Zero staging copies** — `srt_recvmsg` writes directly into the pipe's buffer
- **`IDuplexPipe` surface** — consume SRT streams with the same `PipeReader` code
  you would use for any other transport
- **Encryption-capable native** — libsrt is built with the mbedTLS crypto backend
  (AES + PBKDF2 passphrase key derivation); surfacing the passphrase options on
  the managed API is on the roadmap

Getting Started
---------------

```shell
dotnet add package Spangle.Net.Transport.SRT
```

```cs
using System.Net;
using Spangle.Net.Transport.SRT;

var listener = new SRTListener(IPEndPoint.Parse("0.0.0.0:9998"));
listener.Start();

while (true)
{
    SRTClient client = await listener.AcceptSRTClientAsync(ct);
    _ = ConsumeAsync(client, ct);
}

static async Task ConsumeAsync(SRTClient client, CancellationToken ct)
{
    using (client)
    {
        while (true)
        {
            var result = await client.Pipe.Input.ReadAsync(ct);
            // e.g. feed result.Buffer (MPEG-TS packets) to a demuxer
            client.Pipe.Input.AdvanceTo(result.Buffer.End);
            if (result.IsCompleted) break;
        }
    }
}
```

Push a test stream with an SRT-capable ffmpeg:

```shell
ffmpeg -re -f lavfi -i testsrc2 -c:v libx264 -f mpegts srt://127.0.0.1:9998
```

Status
------

Pre-1.0: the API surface is small and still evolving (listener/receive first;
options such as passphrases and client-side connect are not exposed yet).

Native pieces
-------------

| Component | Version | License |
|---|---|---|
| [libsrt](https://github.com/Haivision/srt) | pinned tag (see release notes) | MPL-2.0 |
| [Mbed TLS](https://github.com/Mbed-TLS/mbedtls) | 3.6 LTS | Apache-2.0 |

Both are statically linked, unmodified, into a single `srt_interop` library per
RID and rebuilt from source by CI on native runners for every target. See
[THIRD-PARTY-NOTICES.txt](./THIRD-PARTY-NOTICES.txt) for the details required by
their licenses.

License
-------

This library is under the MIT License. See [LICENSE](./LICENSE).
The bundled native components remain under their own licenses as described above.
