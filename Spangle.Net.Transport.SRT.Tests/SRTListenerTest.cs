using System.Buffers;
using System.Diagnostics;
using System.Net;
using Xunit.Abstractions;

namespace Spangle.Net.Transport.SRT.Tests;

public class SRTListenerTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public SRTListenerTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestListen()
    {
        var ep = IPEndPoint.Parse("0.0.0.0:9999");
        var listener = new SRTListener(ep);
        listener.Start();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(6000);
        var ct = cts.Token;

        var tServer = Task.Run(async () =>
        {
            var client = await listener.AcceptSRTClientAsync(ct);
            _testOutputHelper.WriteLine("Accept connection!!");
            for (var i = 0; i < 100; i++)
            {
                var res = await client.Pipe.Input.ReadAsync(ct).ConfigureAwait(false);
                res.Buffer.ToArray().DumpHex(_testOutputHelper.WriteLine);
                client.Pipe.Input.AdvanceTo(res.Buffer.End);
                await Task.Delay(10, ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);

        await Task.Delay(100, ct);

        ProcessStartInfo psInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = @"-f lavfi -re -i testsrc2=duration=3:rate=30 " +
                            @"-f lavfi -re -i sine=frequency=1000:duration=3:sample_rate=44100 " +
                            @"-pix_fmt yuv420p -c:v libx264 -b:v 1000k -g 30 -keyint_min 30 -profile:v baseline -preset veryfast " +
                            @"-f mpegts srt://localhost:9999",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        var proc = Process.Start(psInfo);
        _testOutputHelper.WriteLine("ffmpeg started");
        try
        {
            await tServer;
        }
        finally
        {
            if (proc!.HasExited)
            {
                _testOutputHelper.WriteLine("STDOUT: {0}", await proc.StandardOutput.ReadToEndAsync(ct));
                _testOutputHelper.WriteLine("STDERR: {0}", await proc.StandardError.ReadToEndAsync(ct));
            }
        }
    }
}
