using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;
using Vms.Api.Utils;

namespace Vms.Api.Services;

public sealed class RecordingCoordinator(
    IServiceScopeFactory scopeFactory,
    IRecordingProcessRunner processRunner,
    IRecordingMediaInspector mediaInspector,
    IOptions<RecordingOptions> recordingOptions,
    IOptions<RecordingStorageOptions> storageOptions,
    DashboardUpdatePublisher dashboardUpdates,
    TimeProvider timeProvider,
    ILogger<RecordingCoordinator> logger) : IHostedService
{
    private const int MaxFailureLength = 900;
    private readonly ConcurrentDictionary<string, ActiveRecordingSession>
        _activeSessions = new(StringComparer.Ordinal);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var interrupted = await database.Recordings
            .Where(item => item.State == RecordingState.Recording)
            .ToListAsync(cancellationToken);
        var interruptedCameraIds = interrupted
            .Select(item => item.CameraId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var now = timeProvider.GetUtcNow();

        foreach (var recording in interrupted)
        {
            recording.State = RecordingState.Failed;
            recording.EndedAt = now;
            recording.DurationSeconds =
                Math.Max(0, (now - recording.StartedAt).TotalSeconds);
            recording.FailureReason =
                "Recording was interrupted by an API restart.";
        }

        if (interruptedCameraIds.Length > 0)
        {
            var cameras = await database.Cameras
                .Where(camera => interruptedCameraIds.Contains(camera.Id))
                .ToListAsync(cancellationToken);
            foreach (var camera in cameras)
            {
                camera.RecordingStatus = CameraRecordingStatus.NotRecording;
                camera.UpdatedAt = now;
                database.SystemEvents.Add(CreateEvent(
                    SystemEventType.RecordingFailure,
                    camera.Id,
                    EventSeverity.Critical,
                    $"Recording for {camera.Name} was interrupted by an API restart.",
                    EventStatus.Open,
                    null,
                    now));
            }

            await database.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var sessions = _activeSessions.Values.ToArray();
        await Task.WhenAll(sessions.Select(session =>
            StopSessionAsync(session, cancellationToken)));
    }

    public async Task<RecordingMutationResult> StartRecordingAsync(
        string cameraId,
        RecordingMode mode,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var camera = await GetCameraAsync(cameraId, cancellationToken);
        if (camera is null)
        {
            return RecordingMutationResult.NotFound(
                $"Camera '{cameraId}' was not found.");
        }

        if (!camera.IsEnabled
            || camera.ConnectionStatus != CameraConnectionStatus.Online)
        {
            return RecordingMutationResult.Invalid(
                "Only enabled, online cameras can be recorded.");
        }

        var session = new ActiveRecordingSession(
            camera.Id,
            camera.Name,
            camera.RtspUrl,
            mode,
            userId);
        if (!_activeSessions.TryAdd(camera.Id, session))
        {
            return RecordingMutationResult.Conflict(
                $"{camera.Name} already has an active recording.");
        }

        try
        {
            var recording = await StartSegmentAsync(
                session,
                isFirstSegment: true,
                cancellationToken);
            session.RunTask = MonitorSessionAsync(session);
            return RecordingMutationResult.Success(recording);
        }
        catch (Exception exception)
        {
            _activeSessions.TryRemove(camera.Id, out _);
            var error = SafeFailure(exception.Message, camera.RtspUrl);
            await FinishSessionAsync(session, failed: true, error, cancellationToken);
            logger.LogWarning(
                exception,
                "Recording could not start for camera {CameraId}.",
                camera.Id);
            return RecordingMutationResult.Invalid(error);
        }
    }

    public async Task<RecordingMutationResult> StopRecordingAsync(
        string cameraId,
        CancellationToken cancellationToken)
    {
        if (!_activeSessions.TryGetValue(cameraId, out var session))
        {
            return RecordingMutationResult.Conflict(
                $"Camera '{cameraId}' has no active recording.");
        }

        await StopSessionAsync(session, cancellationToken);
        var recording = await GetRecordingAsync(
            session.LastRecordingId,
            cancellationToken);
        return recording is null
            ? RecordingMutationResult.Invalid(
                "The recording stopped but its metadata could not be loaded.")
            : RecordingMutationResult.Success(recording);
    }

    private async Task StopSessionAsync(
        ActiveRecordingSession session,
        CancellationToken cancellationToken)
    {
        session.StopRequested = true;
        IRecordingProcessHandle? handle;
        lock (session.SyncRoot)
        {
            handle = session.Process;
        }

        if (handle is not null)
        {
            var minimumCaptureTime = TimeSpan.FromSeconds(
                recordingOptions.Value.MinimumCaptureSeconds);
            var elapsed = timeProvider.GetUtcNow() - session.CurrentStartedAt;
            var remaining = minimumCaptureTime - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.WhenAny(
                    handle.Completion,
                    Task.Delay(remaining, cancellationToken));
            }

            await handle.StopAsync(
                TimeSpan.FromSeconds(recordingOptions.Value.StopTimeoutSeconds),
                cancellationToken);
        }

        if (session.RunTask is not null)
        {
            await session.RunTask.WaitAsync(cancellationToken);
        }
    }

    private async Task<RecordingResponse> StartSegmentAsync(
        ActiveRecordingSession session,
        bool isFirstSegment,
        CancellationToken cancellationToken)
    {
        var recordingId = Guid.NewGuid();
        var fileName = $"{recordingId:N}.mp4";
        var fullPath = Path.Combine(
            Path.GetFullPath(storageOptions.Value.Path),
            fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var now = timeProvider.GetUtcNow();
        Guid? motionEventId = null;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            if (isFirstSegment && session.Mode == RecordingMode.Event)
            {
                motionEventId = Guid.NewGuid();
                database.SystemEvents.Add(new SystemEvent
                {
                    Id = motionEventId.Value,
                    Type = SystemEventType.MotionDetected,
                    Timestamp = now,
                    CameraId = session.CameraId,
                    UserId = session.UserId,
                    Severity = EventSeverity.Warning,
                    Description =
                        $"Simulated motion detected on {session.CameraName}.",
                    Status = EventStatus.Open
                });
            }

            var recording = new Recording
            {
                Id = recordingId,
                CameraId = session.CameraId,
                Mode = session.Mode,
                State = RecordingState.Recording,
                FileName = fileName,
                StartedAt = now,
                StartedByUserId = session.UserId,
                TriggerEventId = motionEventId
            };
            database.Recordings.Add(recording);

            if (isFirstSegment)
            {
                var camera = await database.Cameras.SingleAsync(
                    item => item.Id == session.CameraId,
                    cancellationToken);
                camera.RecordingStatus = CameraRecordingStatus.Recording;
                camera.UpdatedAt = now;
                database.SystemEvents.Add(CreateEvent(
                    SystemEventType.RecordingStarted,
                    session.CameraId,
                    EventSeverity.Information,
                    $"{session.Mode} recording started for {session.CameraName}.",
                    EventStatus.Closed,
                    session.UserId,
                    now));
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        session.LastRecordingId = recordingId;
        session.CurrentStartedAt = now;
        session.CurrentFilePath = fullPath;

        try
        {
            var handle = processRunner.Start(new RecordingProcessRequest(
                session.RtspUrl,
                fullPath,
                GetMaximumDuration(session.Mode)));
            lock (session.SyncRoot)
            {
                session.Process = handle;
            }
        }
        catch (Exception exception)
        {
            var error = SafeFailure(exception.Message, session.RtspUrl);
            await FinalizeRecordingAsync(
                recordingId,
                completed: false,
                error,
                media: null,
                cancellationToken);
            throw new InvalidOperationException(error, exception);
        }

        if (isFirstSegment)
        {
            await dashboardUpdates.PublishAsync(
                "recording-started",
                cancellationToken);
        }

        return (await GetRecordingAsync(recordingId, cancellationToken))!;
    }

    private async Task MonitorSessionAsync(ActiveRecordingSession session)
    {
        var failed = false;
        string? failure = null;

        try
        {
            while (true)
            {
                IRecordingProcessHandle handle;
                lock (session.SyncRoot)
                {
                    handle = session.Process!;
                }

                var exitCode = await handle.Completion;
                var errorOutput = await handle.GetErrorAsync();
                await handle.DisposeAsync();
                lock (session.SyncRoot)
                {
                    if (ReferenceEquals(session.Process, handle))
                    {
                        session.Process = null;
                    }
                }

                var media = await mediaInspector.InspectAsync(
                    session.CurrentFilePath,
                    CancellationToken.None);
                var hasMedia = media is not null;
                var expectedExit = session.StopRequested
                    || session.Mode != RecordingMode.Manual;
                var completed = exitCode == 0 && hasMedia && expectedExit;
                var discarded = false;

                if (session.Mode == RecordingMode.Continuous
                    && session.StopRequested
                    && !hasMedia)
                {
                    await DiscardRecordingAsync(
                        session.LastRecordingId,
                        session.CurrentFilePath,
                        CancellationToken.None);
                    completed = true;
                    discarded = true;
                }

                failure = completed
                    ? null
                    : SafeFailure(
                        string.IsNullOrWhiteSpace(errorOutput)
                            ? "FFmpeg ended without producing a playable file."
                            : errorOutput,
                        session.RtspUrl);

                if (!discarded)
                {
                    await FinalizeRecordingAsync(
                        session.LastRecordingId,
                        completed,
                        failure,
                        media,
                        CancellationToken.None);
                }

                if (session.Mode == RecordingMode.Continuous
                    && !session.StopRequested
                    && completed)
                {
                    try
                    {
                        await StartSegmentAsync(
                            session,
                            isFirstSegment: false,
                            CancellationToken.None);
                        continue;
                    }
                    catch (Exception exception)
                    {
                        failure = SafeFailure(
                            exception.Message,
                            session.RtspUrl);
                    }
                }

                failed = !completed;
                break;
            }
        }
        catch (Exception exception)
        {
            failed = true;
            failure = SafeFailure(exception.Message, session.RtspUrl);
            logger.LogError(
                exception,
                "Recording monitor failed for camera {CameraId}.",
                session.CameraId);
        }
        finally
        {
            await FinishSessionAsync(
                session,
                failed,
                failure,
                CancellationToken.None);
            _activeSessions.TryRemove(session.CameraId, out _);
        }
    }

    private async Task FinalizeRecordingAsync(
        Guid recordingId,
        bool completed,
        string? failure,
        RecordedMediaInfo? media,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var recording = await database.Recordings.SingleAsync(
            item => item.Id == recordingId,
            cancellationToken);
        var endedAt = timeProvider.GetUtcNow();
        var fullPath = Path.Combine(
            Path.GetFullPath(storageOptions.Value.Path),
            recording.FileName);

        recording.State = completed
            ? RecordingState.Completed
            : RecordingState.Failed;
        recording.EndedAt = endedAt;
        recording.DurationSeconds = media?.DurationSeconds
            ?? Math.Round(
                Math.Max(0, (endedAt - recording.StartedAt).TotalSeconds),
                2);
        recording.FileSizeBytes = media?.FileSizeBytes
            ?? (File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0);
        recording.FailureReason = completed ? null : failure;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task DiscardRecordingAsync(
        Guid recordingId,
        string fullPath,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var recording = await database.Recordings.SingleAsync(
            item => item.Id == recordingId,
            cancellationToken);
        database.Recordings.Remove(recording);
        await database.SaveChangesAsync(cancellationToken);

        if (File.Exists(fullPath)
            && Path.GetFullPath(fullPath).StartsWith(
                Path.GetFullPath(storageOptions.Value.Path),
                StringComparison.Ordinal))
        {
            File.Delete(fullPath);
        }
    }

    private async Task FinishSessionAsync(
        ActiveRecordingSession session,
        bool failed,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var camera = await database.Cameras.SingleOrDefaultAsync(
            item => item.Id == session.CameraId,
            cancellationToken);
        if (camera is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        camera.RecordingStatus = CameraRecordingStatus.NotRecording;
        camera.UpdatedAt = now;
        database.SystemEvents.Add(CreateEvent(
            failed
                ? SystemEventType.RecordingFailure
                : SystemEventType.RecordingStopped,
            camera.Id,
            failed ? EventSeverity.Critical : EventSeverity.Information,
            failed
                ? $"{session.Mode} recording failed for {camera.Name}: {error}"
                : $"{session.Mode} recording stopped for {camera.Name}.",
            failed ? EventStatus.Open : EventStatus.Closed,
            session.UserId,
            now));
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync(
            failed ? "recording-failed" : "recording-stopped",
            cancellationToken);
    }

    private async Task<CameraStartInfo?> GetCameraAsync(
        string cameraId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        return await database.Cameras
            .AsNoTracking()
            .Where(camera => camera.Id == cameraId)
            .Select(camera => new CameraStartInfo(
                camera.Id,
                camera.Name,
                camera.RtspUrl,
                camera.IsEnabled,
                camera.ConnectionStatus))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<RecordingResponse?> GetRecordingAsync(
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var recording = await database.Recordings
            .AsNoTracking()
            .Include(item => item.Camera)
            .Where(item => item.Id == recordingId)
            .SingleOrDefaultAsync(cancellationToken);
        return recording is null
            ? null
            : RecordingService.ToResponse(recording);
    }

    private TimeSpan? GetMaximumDuration(RecordingMode mode) =>
        mode switch
        {
            RecordingMode.Continuous => TimeSpan.FromSeconds(
                recordingOptions.Value.ContinuousSegmentSeconds),
            RecordingMode.Event => TimeSpan.FromSeconds(
                recordingOptions.Value.EventDurationSeconds),
            _ => null
        };

    private static SystemEvent CreateEvent(
        SystemEventType type,
        string cameraId,
        EventSeverity severity,
        string description,
        EventStatus status,
        Guid? userId,
        DateTimeOffset timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Timestamp = timestamp,
            CameraId = cameraId,
            UserId = userId,
            Severity = severity,
            Description = description,
            Status = status
        };

    private static string SafeFailure(string error, string rtspUrl)
    {
        var safe = RtspUrlUtilities.RedactCredentials(
            error.Replace(rtspUrl, "[camera source]"));
        return safe.Length > MaxFailureLength
            ? safe[..MaxFailureLength]
            : safe.Trim();
    }

    private sealed record CameraStartInfo(
        string Id,
        string Name,
        string RtspUrl,
        bool IsEnabled,
        CameraConnectionStatus ConnectionStatus);

    private sealed class ActiveRecordingSession(
        string cameraId,
        string cameraName,
        string rtspUrl,
        RecordingMode mode,
        Guid userId)
    {
        public object SyncRoot { get; } = new();

        public string CameraId { get; } = cameraId;

        public string CameraName { get; } = cameraName;

        public string RtspUrl { get; } = rtspUrl;

        public RecordingMode Mode { get; } = mode;

        public Guid UserId { get; } = userId;

        public bool StopRequested { get; set; }

        public Guid LastRecordingId { get; set; }

        public DateTimeOffset CurrentStartedAt { get; set; }

        public string CurrentFilePath { get; set; } = string.Empty;

        public IRecordingProcessHandle? Process { get; set; }

        public Task? RunTask { get; set; }
    }
}

public sealed record RecordingMutationResult(
    RecordingMutationError ErrorType,
    string? Error,
    RecordingResponse? Recording)
{
    public static RecordingMutationResult Success(RecordingResponse recording) =>
        new(RecordingMutationError.None, null, recording);

    public static RecordingMutationResult NotFound(string error) =>
        new(RecordingMutationError.NotFound, error, null);

    public static RecordingMutationResult Invalid(string error) =>
        new(RecordingMutationError.Invalid, error, null);

    public static RecordingMutationResult Conflict(string error) =>
        new(RecordingMutationError.Conflict, error, null);
}

public enum RecordingMutationError
{
    None,
    NotFound,
    Invalid,
    Conflict
}
