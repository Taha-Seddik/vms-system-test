using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vms.Api.Models;
using Vms.Api.Utils;

namespace Vms.Api.Services;

public sealed class FfprobeCameraProbe(
    IOptions<CameraMonitoringOptions> options,
    ILogger<FfprobeCameraProbe> logger) : ICameraProbe
{
    private const int MaxErrorLength = 900;

    public async Task<CameraProbeResult> ProbeAsync(
        string rtspUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = CreateStartInfo(options.Value.FfprobeExecutable, rtspUrl)
        };

        try
        {
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeoutSource.CancelAfter(timeout);

            await process.WaitForExitAsync(timeoutSource.Token);
            var output = await standardOutput;
            var error = await standardError;
            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                return Failure(
                    stopwatch.Elapsed,
                    string.IsNullOrWhiteSpace(error)
                        ? $"ffprobe exited with code {process.ExitCode}."
                        : error,
                    rtspUrl);
            }

            return ParseSuccess(output, stopwatch.Elapsed, rtspUrl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            stopwatch.Stop();
            return Failure(
                stopwatch.Elapsed,
                $"Connection probe timed out after {timeout.TotalSeconds:0.#} seconds.",
                rtspUrl);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException)
        {
            TryKill(process);
            stopwatch.Stop();
            logger.LogWarning(
                exception,
                "Camera probe failed for {RtspUrl}",
                RtspUrlUtilities.RedactCredentials(rtspUrl));
            return Failure(stopwatch.Elapsed, exception.Message, rtspUrl);
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        string rtspUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-rtsp_transport");
        startInfo.ArgumentList.Add("tcp");
        startInfo.ArgumentList.Add("-analyzeduration");
        startInfo.ArgumentList.Add("1000000");
        startInfo.ArgumentList.Add("-probesize");
        startInfo.ArgumentList.Add("1000000");
        startInfo.ArgumentList.Add("-select_streams");
        startInfo.ArgumentList.Add("v:0");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add(
            "stream=codec_name,width,height,avg_frame_rate,r_frame_rate");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(rtspUrl);
        return startInfo;
    }

    private static CameraProbeResult ParseSuccess(
        string output,
        TimeSpan elapsed,
        string rtspUrl)
    {
        try
        {
            using var document = JsonDocument.Parse(output);
            var streams = document.RootElement.GetProperty("streams");
            if (streams.GetArrayLength() == 0)
            {
                return Failure(elapsed, "The source has no video stream.", rtspUrl);
            }

            var stream = streams[0];
            var codec = ReadString(stream, "codec_name");
            var width = ReadInt(stream, "width");
            var height = ReadInt(stream, "height");
            var framesPerSecond =
                ParseFrameRate(ReadString(stream, "avg_frame_rate"))
                ?? ParseFrameRate(ReadString(stream, "r_frame_rate"));

            return new CameraProbeResult(
                true,
                elapsed,
                codec,
                width,
                height,
                framesPerSecond,
                null);
        }
        catch (JsonException)
        {
            return Failure(
                elapsed,
                "ffprobe returned an unreadable stream response.",
                rtspUrl);
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/', 2);
        if (!double.TryParse(
                parts[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var numerator))
        {
            return null;
        }

        if (parts.Length == 1)
        {
            return numerator;
        }

        return double.TryParse(
                parts[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var denominator)
            && denominator != 0
                ? Math.Round(numerator / denominator, 2)
                : null;
    }

    private static CameraProbeResult Failure(
        TimeSpan elapsed,
        string error,
        string rtspUrl)
    {
        var safeError = RtspUrlUtilities
            .RedactCredentials(error.Replace(rtspUrl, "[camera source]"));
        if (safeError.Length > MaxErrorLength)
        {
            safeError = safeError[..MaxErrorLength];
        }

        return new CameraProbeResult(
            false,
            elapsed,
            null,
            null,
            null,
            null,
            safeError.Trim());
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process ended between the state check and the kill request.
        }
    }
}
