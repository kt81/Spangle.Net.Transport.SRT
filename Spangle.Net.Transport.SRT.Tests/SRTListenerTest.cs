using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Spangle.Interop.Native;
using Xunit.Abstractions;

namespace Spangle.Net.Transport.SRT.Tests;

public sealed class SRTListenerTest : IDisposable
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
    public void TestNativeLibraryVersion()
    {
        uint v = LibSRT.srt_getversion();
        var version = new Version((int)(v >> 16) & 0xFF, (int)(v >> 8) & 0xFF, (int)v & 0xFF);
        s_testOutputHelper!.WriteLine($"libsrt {version}");
        Assert.True(version >= new Version(1, 5, 5), $"expected libsrt >= 1.5.5, got {version}");
    }

    [Fact]
    public async Task TestListen()
    {
        const int numConnections = 21;

        var ep = IPEndPoint.Parse("0.0.0.0:9999");
        var listener = new SRTListener(ep, _logger);
        listener.Start();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(10000);
        var ct = cts.Token;

        var tServer = Task.Run(async () =>
        {
            var receives = new List<Task>(numConnections);
            for (var h = 0; h < numConnections; h++)
            {
                if (ct.IsCancellationRequested) break;
                var client = await listener.AcceptSRTClientAsync(ct);
                receives.Add(ReceiveAny(client, ct));
            }

            // every accepted connection must actually deliver data
            await Task.WhenAll(receives);
        }, ct);

        await Task.Delay(200, ct);

        ProcessStartInfo psInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = "-f lavfi -re -i testsrc2=duration=3:rate=30 " +
                        "-f lavfi -re -i sine=frequency=1000:duration=3:sample_rate=44100 " +
                        "-pix_fmt yuv420p -c:v libx264 -b:v 1000k -g 30 -keyint_min 30 -profile:v baseline -preset veryfast " +
                        "-f mpegts srt://localhost:9999",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        var processes = new List<Process>();
        for (var i = 0; i < numConnections; i++)
        {
            processes.Add(Process.Start(psInfo) ?? throw new Exception("Could not create ffmpeg process."));
            await Task.Delay(10, ct);
        }

        try
        {
            await tServer;
        }
        finally
        {
            await cts.CancelAsync();
            cts.Dispose();
            foreach (var proc in processes)
            {
                proc.Kill();
                proc.Dispose();
            }
        }
    }

    /// <summary>
    /// A slow consumer must not blow up memory nor deadlock: the epoll handler
    /// masks EPOLL_IN while the pipe flush is pending and re-arms it after the
    /// consumer catches up. The read loop must still observe completion when the
    /// sender disconnects.
    /// </summary>
    [Fact]
    public async Task TestSlowConsumerBackpressure()
    {
        var ep = IPEndPoint.Parse("0.0.0.0:9998");
        using var listener = new SRTListener(ep, _logger);
        listener.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        ValueTask<SRTClient> acceptTask = listener.AcceptSRTClientAsync(ct);

        var psInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            // ~500 KB/s so that a 200 ms consumer stall accumulates ~100 KB, well past
            // the pipe's 64 KB pause threshold: the flush goes async and the epoll
            // handler must take the mask/re-arm path.
            Arguments = "-f lavfi -re -i testsrc2=duration=3:rate=30:size=1280x720 " +
                        "-pix_fmt yuv420p -c:v libx264 -b:v 4000k -minrate 3000k -bufsize 1000k -g 30 -preset veryfast " +
                        "-f mpegts srt://localhost:9998",
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psInfo) ?? throw new Exception("Could not create ffmpeg process.");

        try
        {
            using var client = await acceptTask;
            var reader = client.Pipe.Input;
            long total = 0;
            var reads = 0;
            while (true)
            {
                var result = await reader.ReadAsync(ct);
                total += result.Buffer.Length;
                reads++;
                reader.AdvanceTo(result.Buffer.End);
                if (result.IsCompleted)
                {
                    break;
                }

                // Force the pipe past its pause threshold repeatedly; SRT live mode
                // may drop late packets (TLPKTDROP), but the stream must keep
                // flowing and complete when the sender goes away.
                await Task.Delay(200, ct);
            }

            s_testOutputHelper!.WriteLine($"received {total} bytes in {reads} reads");
            Assert.True(total > 100_000, $"received only {total} bytes");
        }
        finally
        {
            if (!proc.HasExited)
            {
                proc.Kill();
            }
        }
    }

    private async Task ReceiveAny(SRTClient client, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        var res = await client.Pipe.Input.ReadAsync(ct).ConfigureAwait(false);
        Assert.NotEmpty(res.Buffer.ToArray()); // .DumpHex(s_testOutputHelper.WriteLine);
        client.Pipe.Input.AdvanceTo(res.Buffer.End);
        // read only first buffer
    }

    private class XunitOutputLogger<T> : ILogger<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly string s_name;

        static XunitOutputLogger()
        {
            s_name = typeof(T).Name;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            var sb = new StringBuilder();
            sb.Append('[').Append(logLevel.ToString()).Append("] [")
                .Append(s_name).Append("] ")
                .Append(formatter(state, exception));

            if (exception != null)
            {
                sb.Append('\n').Append(exception);
            }

            s_testOutputHelper!.WriteLine(sb.ToString());
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    public void Dispose()
    {
        LibSRT.srt_cleanup().LogIfError(_logger);
    }
}
