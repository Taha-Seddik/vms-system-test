using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class FfmpegRecordingKeyframeGenerator(
    RecordingStoragePathResolver paths,
    IOptions<RecordingOptions> options) : IRecordingKeyframeGenerator
{
    private const int MaxErrorLength = 800;

    public async Task<IReadOnlyList<GeneratedRecordingKeyframe>> GenerateAsync(
        Guid recordingId,
        string recordingPath,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(recordingPath) || durationSeconds <= 0)
        {
            return [];
        }

        var interval = options.Value.KeyframeIntervalSeconds;
        var timestamps = new List<int> { 0 };
        for (var timestamp = interval; timestamp < durationSeconds; timestamp += interval)
        {
            timestamps.Add(timestamp);
        }

        var generated = new List<GeneratedRecordingKeyframe>(timestamps.Count);
        foreach (var timestamp in timestamps)
        {
            var fileName = $"{timestamp:D6}.jpg";
            var outputPath = paths.GetKeyframePath(recordingId, fileName);
            using var process = new Process
            {
                StartInfo = CreateStartInfo(recordingPath, outputPath, timestamp)
            };
            process.Start();
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var error = await errorTask;
            if (process.ExitCode != 0
                || !File.Exists(outputPath)
                || new FileInfo(outputPath).Length == 0)
            {
                var safeError = string.IsNullOrWhiteSpace(error)
                    ? "FFmpeg did not create a keyframe image."
                    : error.Trim();
                throw new InvalidOperationException(
                    safeError.Length > MaxErrorLength
                        ? safeError[..MaxErrorLength]
                        : safeError);
            }

            generated.Add(new GeneratedRecordingKeyframe(timestamp, fileName));
        }

        return generated;
    }

    private ProcessStartInfo CreateStartInfo(
        string inputPath,
        string outputPath,
        int timestampSeconds)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.Value.FfmpegExecutable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "-hide_banner",
                     "-loglevel", "error",
                     "-i", inputPath,
                     "-ss", timestampSeconds.ToString(CultureInfo.InvariantCulture),
                     "-frames:v", "1",
                     "-vf", "scale=320:-2",
                     "-q:v", "3",
                     "-y",
                     outputPath
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
