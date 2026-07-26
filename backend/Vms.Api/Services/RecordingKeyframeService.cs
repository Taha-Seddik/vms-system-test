using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class RecordingKeyframeService(
    IServiceScopeFactory scopeFactory,
    IRecordingKeyframeGenerator generator,
    RecordingStoragePathResolver paths,
    ILogger<RecordingKeyframeService> logger)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IReadOnlyList<RecordingKeyframeResponse>> EnsureAsync(
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(recordingId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await GetExistingAsync(recordingId, cancellationToken);
            if (existing.Count > 0)
            {
                return existing;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            var recording = await database.Recordings
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == recordingId,
                    cancellationToken);
            if (recording is null
                || recording.State != RecordingState.Completed
                || recording.DurationSeconds is null)
            {
                return [];
            }

            var recordingPath = paths.GetRecordingPath(recording.FileName);
            var generated = await generator.GenerateAsync(
                recording.Id,
                recordingPath,
                recording.DurationSeconds.Value,
                cancellationToken);
            if (generated.Count == 0)
            {
                return [];
            }

            foreach (var item in generated)
            {
                database.RecordingKeyframes.Add(new RecordingKeyframe
                {
                    Id = Guid.NewGuid(),
                    RecordingId = recording.Id,
                    TimestampSeconds = item.TimestampSeconds,
                    FileName = item.FileName
                });
            }

            await database.SaveChangesAsync(cancellationToken);
            return await GetExistingAsync(recordingId, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Keyframes could not be generated for recording {RecordingId}.",
                recordingId);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<RecordingKeyframeResponse>> GetExistingAsync(
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        return await database.RecordingKeyframes
            .AsNoTracking()
            .Where(item => item.RecordingId == recordingId)
            .OrderBy(item => item.TimestampSeconds)
            .Select(item => new RecordingKeyframeResponse(
                item.Id,
                item.TimestampSeconds))
            .ToArrayAsync(cancellationToken);
    }
}
