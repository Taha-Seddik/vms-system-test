using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vms.Api.Data;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Tests;

public sealed class VmsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"vms-tests-{Guid.NewGuid()}";
    private readonly string _recordingPath = Path.Combine(
        Path.GetTempPath(),
        $"vms-recording-tests-{Guid.NewGuid():N}");

    public string RecordingPath => _recordingPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VmsDatabase"] =
                    "Host=unused;Database=vms_tests;Username=unused;Password=unused",
                ["RecordingStorage:Path"] = _recordingPath,
                ["Recording:ContinuousSegmentSeconds"] = "3",
                ["Recording:EventDurationSeconds"] = "3",
                ["Recording:MinimumCaptureSeconds"] = "0"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<VmsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VmsDbContext>>();
            services.RemoveAll<VmsDbContext>();
            services.RemoveAll<ICameraProbe>();
            services.RemoveAll<IStorageMetricsProvider>();
            services.RemoveAll<IRecordingProcessRunner>();
            services.RemoveAll<IRecordingMediaInspector>();
            services.RemoveAll<IRecordingKeyframeGenerator>();
            services.AddDbContext<VmsDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddSingleton<FakeCameraProbe>();
            services.AddSingleton<ICameraProbe>(
                provider => provider.GetRequiredService<FakeCameraProbe>());
            services.AddSingleton<FakeStorageMetricsProvider>();
            services.AddSingleton<IStorageMetricsProvider>(
                provider => provider.GetRequiredService<FakeStorageMetricsProvider>());
            services.AddSingleton<FakeRecordingProcessRunner>();
            services.AddSingleton<IRecordingProcessRunner>(
                provider => provider.GetRequiredService<FakeRecordingProcessRunner>());
            services.AddSingleton<IRecordingMediaInspector, FakeRecordingMediaInspector>();
            services.AddSingleton<FakeRecordingKeyframeGenerator>();
            services.AddSingleton<IRecordingKeyframeGenerator>(
                provider => provider.GetRequiredService<FakeRecordingKeyframeGenerator>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing
            && Directory.Exists(_recordingPath)
            && Path.GetFullPath(_recordingPath).StartsWith(
                Path.GetTempPath(),
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(_recordingPath, recursive: true);
        }
    }
}

public sealed class FakeCameraProbe : ICameraProbe
{
    public CameraProbeResult NextResult { get; set; } = Online();

    public Task<CameraProbeResult> ProbeAsync(
        string rtspUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        Task.FromResult(NextResult);

    public static CameraProbeResult Online() =>
        new(
            true,
            TimeSpan.FromMilliseconds(12),
            "h264",
            640,
            360,
            10,
            null);

    public static CameraProbeResult Offline(string error = "Connection refused.") =>
        new(
            false,
            TimeSpan.FromMilliseconds(18),
            null,
            null,
            null,
            null,
            error);
}

public sealed class FakeStorageMetricsProvider : IStorageMetricsProvider
{
    public StorageHealthResponse NextResponse { get; set; } = Healthy();

    public Task<StorageHealthResponse> GetAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(NextResponse);

    public static StorageHealthResponse Healthy() =>
        new(
            "/test/recordings",
            StorageHealthStatus.Healthy,
            1_000_000,
            600_000,
            400_000,
            25_000,
            40,
            null);
}

public sealed class FakeRecordingProcessRunner : IRecordingProcessRunner
{
    public bool FailNextStart { get; set; }

    public IRecordingProcessHandle Start(RecordingProcessRequest request)
    {
        if (FailNextStart)
        {
            FailNextStart = false;
            throw new InvalidOperationException("Simulated FFmpeg startup failure.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
        File.WriteAllBytes(request.OutputPath, [0, 0, 0, 24, 102, 116, 121, 112]);
        return new FakeRecordingProcessHandle(request.MaximumDuration.HasValue);
    }

    private sealed class FakeRecordingProcessHandle : IRecordingProcessHandle
    {
        private readonly TaskCompletionSource<int> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> Completion => _completion.Task;

        public Task<string> GetErrorAsync() => Task.FromResult(string.Empty);

        public Task StopAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _completion.TrySetResult(0);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task CompleteAutomaticallyAsync()
        {
            await Task.Delay(75);
            _completion.TrySetResult(0);
        }

        public FakeRecordingProcessHandle(bool completesAutomatically)
        {
            if (completesAutomatically)
            {
                _ = CompleteAutomaticallyAsync();
            }
        }
    }
}

public sealed class FakeRecordingMediaInspector : IRecordingMediaInspector
{
    public Task<RecordedMediaInfo?> InspectAsync(
        string filePath,
        CancellationToken cancellationToken) =>
        Task.FromResult<RecordedMediaInfo?>(
            File.Exists(filePath)
                ? new RecordedMediaInfo(3, new FileInfo(filePath).Length)
                : null);
}

public sealed class FakeRecordingKeyframeGenerator(
    RecordingStoragePathResolver paths) : IRecordingKeyframeGenerator
{
    public Task<IReadOnlyList<GeneratedRecordingKeyframe>> GenerateAsync(
        Guid recordingId,
        string recordingPath,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        var generated = new List<GeneratedRecordingKeyframe>();
        for (var timestamp = 0; timestamp < durationSeconds; timestamp += 30)
        {
            var fileName = $"{timestamp:D6}.jpg";
            var outputPath = paths.GetKeyframePath(recordingId, fileName);
            File.WriteAllBytes(outputPath, [0xff, 0xd8, 0xff, 0xd9]);
            generated.Add(new GeneratedRecordingKeyframe(timestamp, fileName));
        }

        return Task.FromResult<IReadOnlyList<GeneratedRecordingKeyframe>>(
            generated);
    }
}
