using Microsoft.Extensions.Options;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class RecordingStoragePathResolver(
    IOptions<RecordingStorageOptions> options)
{
    private readonly string _root = Path.GetFullPath(options.Value.Path);

    public string GetRecordingPath(string fileName) =>
        ResolveContainedPath(_root, fileName, ".mp4");

    public string GetKeyframeDirectory(Guid recordingId)
    {
        var directory = Path.Combine(_root, "keyframes", recordingId.ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public string GetKeyframePath(Guid recordingId, string fileName) =>
        ResolveContainedPath(
            GetKeyframeDirectory(recordingId),
            fileName,
            ".jpg");

    private static string ResolveContainedPath(
        string directory,
        string fileName,
        string expectedExtension)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(
                fileName,
                Path.GetFileName(fileName),
                StringComparison.Ordinal)
            || !string.Equals(
                Path.GetExtension(fileName),
                expectedExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Stored media file name is invalid.");
        }

        var fullDirectory = Path.GetFullPath(directory);
        var fullPath = Path.GetFullPath(Path.Combine(fullDirectory, fileName));
        var boundary = fullDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullDirectory
            : fullDirectory + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(boundary, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Stored media path is outside the recording directory.");
        }

        return fullPath;
    }
}
