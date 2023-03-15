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

        var tReceiver = Task.Run(async () =>
        {
            var client = await listener.AcceptSRTClientAsync();
            var res = await client.Pipe.Input.ReadAsync();
            res.Buffer.ToArray().DumpHex(Console.Out.WriteLine);
        });

        var proc = Process.Start("/usr/bin/ffmpeg", "-i -re -f lavfi -i testsrc2 -f ts srt://localhost:9999?mode=client");
        await tReceiver;
    }
}
