using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class FfprobeRecordingMediaInspector(
    IOptions<CameraMonitoringOptions> cameraOptions)
    : IRecordingMediaInspector
{
    public async Task<RecordedMediaInfo?> InspectAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = CreateStartInfo(filePath)
        };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            _ = await errorTask;
            if (process.ExitCode != 0)
            {
                return null;
            }

            using var document = JsonDocument.Parse(output);
            var streams = document.RootElement.GetProperty("streams");
            if (streams.GetArrayLength() == 0)
            {
                return null;
            }

            var format = document.RootElement.GetProperty("format");
            if (!format.TryGetProperty("duration", out var durationValue)
                || !double.TryParse(
                    durationValue.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var duration)
                || duration <= 0)
            {
                return null;
            }

            return new RecordedMediaInfo(
                Math.Round(duration, 2),
                new FileInfo(filePath).Length);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private ProcessStartInfo CreateStartInfo(string filePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = cameraOptions.Value.FfprobeExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "-v", "error",
                     "-select_streams", "v:0",
                     "-show_entries", "stream=codec_name",
                     "-show_entries", "format=duration",
                     "-of", "json",
                     filePath
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
