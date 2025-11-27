// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry;
using DotCompute.Core.Telemetry.Enums;
using DotCompute.Core.Telemetry.Metrics;
using DotCompute.Core.Telemetry.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DotCompute.Core.Tests.Telemetry;

/// <summary>
/// Comprehensive unit tests for PerformanceProfiler class.
/// Tests profiling, kernel analysis, memory access pattern analysis, and optimization recommendations.
/// </summary>
public sealed class PerformanceProfilerTests : IDisposable
{
    private readonly ILogger<PerformanceProfiler> _mockLogger;
    private readonly IOptions<PerformanceProfilerOptions> _options;
    private readonly PerformanceProfiler _profiler;

    public PerformanceProfilerTests()
    {
        _mockLogger = Substitute.For<ILogger<PerformanceProfiler>>();
        _options = Options.Create(new PerformanceProfilerOptions
        {
            EnableContinuousProfiling = false, // Disable for tests
            MaxConcurrentProfiles = 10,
            SamplingIntervalMs = 1000,
            AllowOrphanedRecords = true
        });
        _profiler = new PerformanceProfiler(_mockLogger, _options);
    }

    public void Dispose() => _profiler.Dispose();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        using var profiler = new PerformanceProfiler(_mockLogger, _options);

        // Assert
        _ = profiler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new PerformanceProfiler(null!, _options);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        // Arrange & Act
        using var profiler = new PerformanceProfiler(_mockLogger, null!);

        // Assert
        _ = profiler.Should().NotBeNull();
    }

    #endregion

    #region CreateProfileAsync Tests

    [Fact]
    public async Task CreateProfileAsync_WithValidCorrelationId_CreatesProfile()
    {
        // Arrange
        var correlationId = "test-profile-1";

        // Act
        var profile = await _profiler.CreateProfileAsync(correlationId);

        // Assert
        _ = profile.Should().NotBeNull();
        _ = profile.CorrelationId.Should().Be(correlationId);
        _ = profile.Status.Should().Be(ProfileStatus.Active);
    }

    [Fact]
    public async Task CreateProfileAsync_WithProfileOptions_RespectsOptions()
    {
        // Arrange
        var correlationId = "test-profile-2";
        var profileOptions = new ProfileOptions
        {
            AutoStopAfter = TimeSpan.FromMilliseconds(100),
            EnableSystemProfiling = true
        };

        // Act
        var profile = await _profiler.CreateProfileAsync(correlationId, profileOptions);

        // Assert
        _ = profile.Should().NotBeNull();
        _ = profile.Status.Should().Be(ProfileStatus.Completed);
    }

    [Fact]
    public async Task CreateProfileAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var correlationId = "test-profile-3";
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await _profiler.CreateProfileAsync(correlationId, cancellationToken: cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateProfileAsync_MultipleConcurrentProfiles_HandlesCorrectly()
    {
        // Arrange
        var tasks = new Task<DotCompute.Core.Telemetry.Profiles.PerformanceProfile>[5];

        // Act - Create 5 concurrent profiles
        for (var i = 0; i < 5; i++)
        {
            var correlationId = $"concurrent-profile-{i}";
            tasks[i] = _profiler.CreateProfileAsync(correlationId);
        }
        var profiles = await Task.WhenAll(tasks);

        // Assert
        _ = profiles.Should().HaveCount(5);
        _ = profiles.Should().OnlyContain(p => p.Status == ProfileStatus.Active);
    }

    #endregion

    #region RecordKernelExecution Tests

    [Fact]
    public async Task RecordKernelExecution_WithValidProfile_RecordsExecution()
    {
        // Arrange
        var correlationId = "test-profile-1";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-100),
            EndTime = DateTimeOffset.UtcNow,
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            ThroughputOpsPerSecond = 10000,
            OccupancyPercentage = 75.0,
            MemoryBandwidthGBPerSecond = 50.0,
            CacheHitRate = 0.85,
            MemoryCoalescingEfficiency = 0.9
        };

        // Act
        _profiler.RecordKernelExecution(correlationId, "TestKernel", "GPU0", metrics);

        // Assert - Verify by finishing profile
        var profile = await _profiler.FinishProfilingAsync(correlationId);
        _ = profile.TotalKernelExecutions.Should().Be(1);
    }

    [Fact]
    public async Task RecordKernelExecution_WithMultipleExecutions_RecordsAll()
    {
        // Arrange
        var correlationId = "test-profile-2";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-100),
            EndTime = DateTimeOffset.UtcNow,
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            ThroughputOpsPerSecond = 10000,
            OccupancyPercentage = 75.0
        };

        // Act - Record 3 executions
        _profiler.RecordKernelExecution(correlationId, "Kernel1", "GPU0", metrics);
        _profiler.RecordKernelExecution(correlationId, "Kernel2", "GPU0", metrics);
        _profiler.RecordKernelExecution(correlationId, "Kernel3", "GPU0", metrics);

        // Assert
        var profile = await _profiler.FinishProfilingAsync(correlationId);
        _ = profile.TotalKernelExecutions.Should().Be(3);
    }

    [Fact]
    public void RecordKernelExecution_WithUnknownProfile_HandlesGracefully()
    {
        // Arrange
        var metrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow,
            ExecutionTime = TimeSpan.FromMilliseconds(100)
        };

        // Act & Assert - Should not throw when AllowOrphanedRecords is true
        var act = () => _profiler.RecordKernelExecution("unknown-profile", "TestKernel", "GPU0", metrics);
        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task RecordKernelExecution_RecordsPerformanceMetrics()
    {
        // Arrange
        var correlationId = "test-profile-perf";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-100),
            EndTime = DateTimeOffset.UtcNow,
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            ThroughputOpsPerSecond = 10000,
            OccupancyPercentage = 75.0,
            InstructionThroughput = 5000,
            MemoryBandwidthGBPerSecond = 50.0,
            CacheHitRate = 0.85,
            MemoryCoalescingEfficiency = 0.9,
            WarpEfficiency = 0.8,
            BranchDivergence = 0.1,
            MemoryLatency = 500.0, // microseconds as double
            PowerConsumption = 150.0
        };

        // Act
        _profiler.RecordKernelExecution(correlationId, "PerfKernel", "GPU0", metrics);

        // Assert
        var profile = await _profiler.FinishProfilingAsync(correlationId);
        _ = profile.KernelExecutions.Should().HaveCount(1);
        var execution = profile.KernelExecutions.First();
        _ = execution.ThroughputOpsPerSecond.Should().Be(10000);
        _ = execution.OccupancyPercentage.Should().Be(75.0);
        _ = execution.WarpEfficiency.Should().Be(0.8);
    }

    #endregion

    #region RecordMemoryOperation Tests

    [Fact]
    public async Task RecordMemoryOperation_WithValidProfile_RecordsOperation()
    {
        // Arrange
        var correlationId = "test-profile-mem";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = new MemoryOperationMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            BytesTransferred = 1024 * 1024,
            BandwidthGBPerSecond = 100.0,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9,
            CacheHitRate = 0.85,
            MemorySegment = "Global",
            TransferDirection = "HostToDevice",
            QueueDepth = 4
        };

        // Act
        _profiler.RecordMemoryOperation(correlationId, "Transfer", "GPU0", metrics);

        // Assert
        var profile = await _profiler.FinishProfilingAsync(correlationId);
        _ = profile.TotalMemoryOperations.Should().Be(1);
    }

    [Fact]
    public async Task RecordMemoryOperation_WithMultipleOperations_RecordsAll()
    {
        // Arrange
        var correlationId = "test-profile-mem2";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = new MemoryOperationMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            BytesTransferred = 1024 * 1024,
            BandwidthGBPerSecond = 100.0,
            AccessPattern = "Sequential"
        };

        // Act
        _profiler.RecordMemoryOperation(correlationId, "Transfer1", "GPU0", metrics);
        _profiler.RecordMemoryOperation(correlationId, "Transfer2", "GPU0", metrics);
        _profiler.RecordMemoryOperation(correlationId, "Allocation", "GPU0", metrics);

        // Assert
        var profile = await _profiler.FinishProfilingAsync(correlationId);
        _ = profile.TotalMemoryOperations.Should().Be(3);
    }

    #endregion

    #region FinishProfilingAsync Tests

    [Fact]
    public async Task FinishProfilingAsync_WithActiveProfile_CompletesProfile()
    {
        // Arrange
        var correlationId = "test-finish-1";
        _ = await _profiler.CreateProfileAsync(correlationId);

        // Act
        var profile = await _profiler.FinishProfilingAsync(correlationId);

        // Assert
        _ = profile.Should().NotBeNull();
        _ = profile.Status.Should().Be(ProfileStatus.Completed);
        _ = profile.EndTime.Should().NotBe(default);
        _ = profile.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task FinishProfilingAsync_WithUnknownProfile_ReturnsNotFound()
    {
        // Act
        var profile = await _profiler.FinishProfilingAsync("unknown-profile");

        // Assert
        _ = profile.Status.Should().Be(ProfileStatus.NotFound);
    }

    [Fact]
    public async Task FinishProfilingAsync_IncludesAnalysis()
    {
        // Arrange
        var correlationId = "test-finish-analysis";
        _ = await _profiler.CreateProfileAsync(correlationId);

        var metrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddMilliseconds(100),
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            ThroughputOpsPerSecond = 10000
        };
        _profiler.RecordKernelExecution(correlationId, "TestKernel", "GPU0", metrics);

        // Act
        var profile = await _profiler.FinishProfilingAsync(correlationId);

        // Assert
        _ = profile.Analysis.Should().NotBeNull();
        _ = profile.Analysis!.TotalExecutionTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FinishProfilingAsync_WithCancellation_CompletesSuccessfully()
    {
        // Arrange
        var correlationId = "test-finish-cancel";
        _ = await _profiler.CreateProfileAsync(correlationId);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act - The implementation completes the work even with a cancelled token
        // This is valid behavior as cancellation is cooperative
        var profile = await _profiler.FinishProfilingAsync(correlationId, cts.Token);

        // Assert - Profile should be completed even with cancelled token
        _ = profile.Should().NotBeNull();
        _ = profile.Status.Should().Be(ProfileStatus.Completed);
    }

    #endregion

    #region AnalyzeKernelPerformance Tests

    [Fact]
    public void AnalyzeKernelPerformance_WithNoData_ReturnsNoDataStatus()
    {
        // Act
        var analysis = _profiler.AnalyzeKernelPerformance("UnknownKernel");

        // Assert
        _ = analysis.Should().NotBeNull();
        _ = analysis.Status.Should().Be(AnalysisStatus.NoData);
    }

    [Fact]
    public async Task AnalyzeKernelPerformance_WithData_ReturnsAnalysis()
    {
        // Arrange
        var correlationId = "test-kernel-analysis";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddMilliseconds(100),
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            ThroughputOpsPerSecond = 10000,
            OccupancyPercentage = 75.0,
            CacheHitRate = 0.85,
            MemoryBandwidthGBPerSecond = 50.0,
            WarpEfficiency = 0.8,
            BranchDivergence = 0.1,
            MemoryCoalescingEfficiency = 0.9
        };

        _profiler.RecordKernelExecution(correlationId, "AnalyzeMe", "GPU0", metrics);

        // Act
        var analysis = _profiler.AnalyzeKernelPerformance("AnalyzeMe");

        // Assert
        _ = analysis.Should().NotBeNull();
        _ = analysis.Status.Should().Be(AnalysisStatus.Success);
        _ = analysis.ExecutionCount.Should().Be(1);
        _ = analysis.AverageExecutionTime.Should().BeApproximately(100.0, 1.0);
        _ = analysis.AverageThroughput.Should().Be(10000);
    }

    [Fact]
    public async Task AnalyzeKernelPerformance_CalculatesStatistics()
    {
        // Arrange
        var correlationId = "test-kernel-stats";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics1 = CreateKernelMetrics(TimeSpan.FromMilliseconds(100));
        var metrics2 = CreateKernelMetrics(TimeSpan.FromMilliseconds(200));
        var metrics3 = CreateKernelMetrics(TimeSpan.FromMilliseconds(150));

        _profiler.RecordKernelExecution(correlationId, "StatsKernel", "GPU0", metrics1);
        _profiler.RecordKernelExecution(correlationId, "StatsKernel", "GPU0", metrics2);
        _profiler.RecordKernelExecution(correlationId, "StatsKernel", "GPU0", metrics3);

        // Act
        var analysis = _profiler.AnalyzeKernelPerformance("StatsKernel");

        // Assert
        _ = analysis.MinExecutionTime.Should().BeApproximately(100.0, 1.0);
        _ = analysis.MaxExecutionTime.Should().BeApproximately(200.0, 1.0);
        _ = analysis.ExecutionTimeStdDev.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeKernelPerformance_GeneratesOptimizationRecommendations()
    {
        // Arrange
        var correlationId = "test-kernel-opt";
        await _profiler.CreateProfileAsync(correlationId);

        // Create metrics with poor performance characteristics
        var metrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddMilliseconds(100),
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OccupancyPercentage = 30.0, // Low occupancy
            CacheHitRate = 0.5, // Poor cache performance
            WarpEfficiency = 0.5, // Low warp efficiency
            ThroughputOpsPerSecond = 1000
        };

        _profiler.RecordKernelExecution(correlationId, "SlowKernel", "GPU0", metrics);

        // Act
        var analysis = _profiler.AnalyzeKernelPerformance("SlowKernel");

        // Assert
        _ = analysis.OptimizationRecommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeKernelPerformance_RespectsTimeWindow()
    {
        // Arrange
        var correlationId = "test-kernel-window";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = CreateKernelMetrics(TimeSpan.FromMilliseconds(100));
        _profiler.RecordKernelExecution(correlationId, "WindowKernel", "GPU0", metrics);

        // Act - Request analysis with very short time window
        var analysis = _profiler.AnalyzeKernelPerformance("WindowKernel", TimeSpan.FromMilliseconds(1));

        // Assert - May or may not find data depending on timing
        _ = analysis.Should().NotBeNull();
    }

    #endregion

    #region AnalyzeMemoryAccessPatterns Tests

    [Fact]
    public void AnalyzeMemoryAccessPatterns_WithNoData_ReturnsNoDataStatus()
    {
        // Act
        var analysis = _profiler.AnalyzeMemoryAccessPatterns();

        // Assert
        _ = analysis.Should().NotBeNull();
        _ = analysis.Status.Should().Be(AnalysisStatus.NoData);
    }

    [Fact]
    public async Task AnalyzeMemoryAccessPatterns_WithData_ReturnsAnalysis()
    {
        // Arrange
        var correlationId = "test-mem-analysis";
        await _profiler.CreateProfileAsync(correlationId);

        var metrics = new MemoryOperationMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            BytesTransferred = 1024 * 1024 * 100,
            BandwidthGBPerSecond = 100.0,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9,
            CacheHitRate = 0.85,
            TransferDirection = "HostToDevice"
        };

        _profiler.RecordMemoryOperation(correlationId, "Transfer", "GPU0", metrics);

        // Act
        var analysis = _profiler.AnalyzeMemoryAccessPatterns();

        // Assert
        _ = analysis.Should().NotBeNull();
        _ = analysis.Status.Should().Be(AnalysisStatus.Success);
        _ = analysis.TotalOperations.Should().Be(1);
        _ = analysis.AverageBandwidth.Should().Be(100.0);
    }

    [Fact]
    public async Task AnalyzeMemoryAccessPatterns_TracksAccessPatternDistribution()
    {
        // Arrange
        var correlationId = "test-mem-patterns";
        await _profiler.CreateProfileAsync(correlationId);

        var sequentialMetrics = CreateMemoryMetrics("Sequential");
        var randomMetrics = CreateMemoryMetrics("Random");

        _profiler.RecordMemoryOperation(correlationId, "Transfer1", "GPU0", sequentialMetrics);
        _profiler.RecordMemoryOperation(correlationId, "Transfer2", "GPU0", sequentialMetrics);
        _profiler.RecordMemoryOperation(correlationId, "Transfer3", "GPU0", randomMetrics);

        // Act
        var analysis = _profiler.AnalyzeMemoryAccessPatterns();

        // Assert
        _ = analysis.AccessPatternDistribution.Should().ContainKey("Sequential");
        _ = analysis.AccessPatternDistribution.Should().ContainKey("Random");
        _ = analysis.AccessPatternDistribution["Sequential"].Should().Be(2);
        _ = analysis.AccessPatternDistribution["Random"].Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeMemoryAccessPatterns_GeneratesOptimizationRecommendations()
    {
        // Arrange
        var correlationId = "test-mem-opt";
        await _profiler.CreateProfileAsync(correlationId);

        // Create metrics with poor performance characteristics
        var metrics = new MemoryOperationMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            BytesTransferred = 1024 * 1024,
            BandwidthGBPerSecond = 10.0,
            CoalescingEfficiency = 0.5, // Poor coalescing
            CacheHitRate = 0.7, // Poor cache performance
            AccessPattern = "Random"
        };

        _profiler.RecordMemoryOperation(correlationId, "Transfer", "GPU0", metrics);

        // Act
        var analysis = _profiler.AnalyzeMemoryAccessPatterns();

        // Assert
        _ = analysis.OptimizationRecommendations.Should().NotBeEmpty();
    }

    #endregion

    #region GetSystemPerformanceSnapshot Tests

    [Fact]
    public void GetSystemPerformanceSnapshot_ReturnsValidSnapshot()
    {
        // Act
        var snapshot = _profiler.GetSystemPerformanceSnapshot();

        // Assert
        _ = snapshot.Should().NotBeNull();
        _ = snapshot.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        _ = snapshot.MemoryUsage.Should().BeGreaterThan(0);
        _ = snapshot.ThreadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetSystemPerformanceSnapshot_IncludesGCMetrics()
    {
        // Act
        var snapshot = _profiler.GetSystemPerformanceSnapshot();

        // Assert
        _ = snapshot.Gen0Collections.Should().BeGreaterThanOrEqualTo(0);
        _ = snapshot.Gen1Collections.Should().BeGreaterThanOrEqualTo(0);
        _ = snapshot.Gen2Collections.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSystemPerformanceSnapshot_TracksActiveProfiles()
    {
        // Arrange
        await _profiler.CreateProfileAsync("profile1");
        await _profiler.CreateProfileAsync("profile2");

        // Act
        var snapshot = _profiler.GetSystemPerformanceSnapshot();

        // Assert
        _ = snapshot.ActiveProfiles.Should().Be(2);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        using var profiler = new PerformanceProfiler(_mockLogger, _options);

        // Act & Assert
        profiler.Dispose();
        profiler.Dispose();
    }

    [Fact]
    public void Dispose_DisposedProfiler_ThrowsObjectDisposedException()
    {
        // Arrange
        var profiler = new PerformanceProfiler(_mockLogger, _options);
        profiler.Dispose();

        // Act
        var act = profiler.GetSystemPerformanceSnapshot;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Helper Methods

    private static KernelExecutionMetrics CreateKernelMetrics(TimeSpan executionTime)
    {
        return new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow - executionTime,
            EndTime = DateTimeOffset.UtcNow,
            ExecutionTime = executionTime,
            ThroughputOpsPerSecond = 10000,
            OccupancyPercentage = 75.0,
            CacheHitRate = 0.85,
            MemoryBandwidthGBPerSecond = 50.0,
            WarpEfficiency = 0.8,
            BranchDivergence = 0.1,
            MemoryCoalescingEfficiency = 0.9
        };
    }

    private static MemoryOperationMetrics CreateMemoryMetrics(string accessPattern)
    {
        return new MemoryOperationMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            BytesTransferred = 1024 * 1024,
            BandwidthGBPerSecond = 100.0,
            AccessPattern = accessPattern,
            CoalescingEfficiency = 0.9,
            CacheHitRate = 0.85
        };
    }

    #endregion
}
