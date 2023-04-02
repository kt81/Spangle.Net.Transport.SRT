using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Spangle.Interop.Native;
using Xunit.Abstractions;

namespace Spangle.Net.Transport.SRT.Tests;

public class SRTListenerTest : IDisposable
{
    private static ITestOutputHelper? s_testOutputHelper;
    private readonly ILogger           _logger;

    public SRTListenerTest(ITestOutputHelper testOutputHelper)
    {
        s_testOutputHelper = testOutputHelper;
        _logger = new XunitOutputLogger<SRTListener>();
        unsafe
        {
            const string name = nameof(SRTListenerTest);
            LibSRT.srt_setloghandler(Marshal.StringToCoTaskMemAnsi(name).ToPointer(), &LogHandler);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void LogHandler(void* opaque, int level, byte* file, int line, byte* area, byte* message)
    {
        string? name = Marshal.PtrToStringAnsi(new IntPtr(opaque));
        string? areaStr = Marshal.PtrToStringAnsi(new IntPtr(area));
        string? msgStr = Marshal.PtrToStringAnsi(new IntPtr(message))?.TrimEnd();
        s_testOutputHelper!.WriteLine("srtNativeLog: [{0}]({1}){2}: {3}", name, level, areaStr, msgStr);
    }

    [Fact]
    public async Task TestListen()
    {
        var ep = IPEndPoint.Parse("0.0.0.0:9999");
        var listener = new SRTListener(ep, _logger);
        listener.Start();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(60000);
        var ct = cts.Token;

        var tServer = Task.Run(async () =>
        {
            for (var h = 0; h < 10; h++)
            {
                if (ct.IsCancellationRequested) break;
                var client = await listener.AcceptSRTClientAsync(ct);
                s_testOutputHelper!.WriteLine("Accept connection!! {0}", client.PeerHandle);
                Task.Run(async () =>
                {
                    while (true)
                    {
                        if (ct.IsCancellationRequested) break;
                        var res = await client.Pipe.Input.ReadAsync(ct).ConfigureAwait(false);
                        Assert.NotEmpty(res.Buffer.ToArray()); // .DumpHex(s_testOutputHelper.WriteLine);
                        client.Pipe.Input.AdvanceTo(res.Buffer.End);
                    }
                }, ct).SafeFireAndForget(e => Assert.Fail(e.Message));
            }

            await Task.Delay(100, ct);
        }, ct).ConfigureAwait(false);

        await Task.Delay(200, ct);

        ProcessStartInfo psInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = @"-f lavfi -re -i testsrc2=duration=3:rate=30 " +
                        @"-f lavfi -re -i sine=frequency=1000:duration=3:sample_rate=44100 " +
                        @"-pix_fmt yuv420p -c:v libx264 -b:v 1000k -g 30 -keyint_min 30 -profile:v baseline -preset veryfast " +
                        @"-f mpegts srt://localhost:9999?pkt_size=940",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var procs = new List<Process?>();
        for (var i = 0; i < 10; i++)
        {
            procs.Add(Process.Start(psInfo));
            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        try
        {
            await tServer;
        }
        finally
        {
            cts.Cancel();
            cts.Dispose();
            foreach (var proc in procs)
            {
                if (proc is null)
                {
                    continue;
                }
                proc.StandardInput.WriteLine("\x3");
                proc.StandardInput.Close();
                s_testOutputHelper!.WriteLine("Proc {0} has exited? : {1}", proc.Id, proc.HasExited);
                proc.Dispose();
            }
        }
        // if (proc!.HasExited)
        // {
        //     s_testOutputHelper.WriteLine("STDOUT: {0}", await proc.StandardOutput.ReadToEndAsync(ct));
        //     s_testOutputHelper.WriteLine("STDERR: {0}", await proc.StandardError.ReadToEndAsync(ct));
        // }
    }

    private class XunitOutputLogger<T> : ILogger<T>
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            s_testOutputHelper!.WriteLine(formatter.Invoke(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    public void Dispose()
    {
        LibSRT.srt_cleanup();
    }
}
