using System.Buffers;
using System.Diagnostics;
using System.Net;

namespace Spangle.Net.Transport.SRT.Tests;

public class SRTListenerTest
{
    [Fact]
    public async Task Test1()
    {
        var ep = IPEndPoint.Parse("0.0.0.0:9999");
        var listener = new SRTListener(ep);
        listener.Start();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(2000);
        var ct = cts.Token;

        var tServer = Task.Run(async () =>
        {
            var client = listener.AcceptSRTClient();
            var res = await client.Pipe.Input.ReadAsync(ct);
            res.Buffer.ToArray().DumpHex(Console.Out.WriteLine);
        }, ct);

        var proc = Process.Start("/usr/bin/ffmpeg", "-i -re -t 5 -f lavfi -i testsrc2 -f ts srt://localhost:9999?mode=client");
        await tServer;
    }
}
