using System.Diagnostics;
using Microsoft.Extensions.Options;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class FfmpegRecordingProcessRunner(
    IOptions<RecordingOptions> options) : IRecordingProcessRunner
{
    public IRecordingProcessHandle Start(RecordingProcessRequest request)
    {
        var process = new Process
        {
            StartInfo = CreateStartInfo(request)
        };

        process.Start();
        return new FfmpegRecordingProcessHandle(process);
    }

    private ProcessStartInfo CreateStartInfo(RecordingProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.Value.FfmpegExecutable,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        AddArguments(
            startInfo,
            "-hide_banner",
            "-loglevel",
            "error",
            "-rtsp_transport",
            "tcp",
            "-i",
            request.SourceUrl,
            "-map",
            "0:v:0",
            "-c:v",
            "copy",
            "-an");

        if (request.MaximumDuration.HasValue)
        {
            AddArguments(
                startInfo,
                "-t",
                request.MaximumDuration.Value.TotalSeconds.ToString(
                    "0.###",
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        AddArguments(
            startInfo,
            "-movflags",
            "+faststart",
            "-y",
            request.OutputPath);
        return startInfo;
    }

    private static void AddArguments(
        ProcessStartInfo startInfo,
        params string[] arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private sealed class FfmpegRecordingProcessHandle(
        Process process) : IRecordingProcessHandle
    {
        private readonly Task<string> _errorOutput =
            process.StandardError.ReadToEndAsync();

        public Task<int> Completion { get; } = WaitForExitAsync(process);

        public Task<string> GetErrorAsync() => _errorOutput;

        public async Task StopAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                await process.StandardInput.WriteLineAsync("q");
                await process.StandardInput.FlushAsync(cancellationToken);
                await Completion.WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await Completion;
            }
            catch (InvalidOperationException)
            {
                // FFmpeg exited between the state check and the stop request.
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await Completion;
            process.Dispose();
        }

        private static async Task<int> WaitForExitAsync(Process process)
        {
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }
}
