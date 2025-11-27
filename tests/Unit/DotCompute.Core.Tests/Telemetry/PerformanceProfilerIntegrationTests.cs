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
/// Integration tests for PerformanceProfiler demonstrating end-to-end profiling scenarios.
/// </summary>
public sealed class PerformanceProfilerIntegrationTests : IDisposable
{
    private readonly ILogger<PerformanceProfiler> _mockLogger;
    private readonly IOptions<PerformanceProfilerOptions> _options;
    private readonly PerformanceProfiler _profiler;

    public PerformanceProfilerIntegrationTests()
    {
        _mockLogger = Substitute.For<ILogger<PerformanceProfiler>>();
        _options = Options.Create(new PerformanceProfilerOptions
        {
            EnableContinuousProfiling = false,
            MaxConcurrentProfiles = 10,
            AllowOrphanedRecords = true
        });
        _profiler = new PerformanceProfiler(_mockLogger, _options);
    }

    public void Dispose() => _profiler.Dispose();

    [Fact]
    public async Task CompleteProfilingWorkflow_Success()
    {
        // Arrange
        var correlationId = "integration-test-1";

        // Act - Create profile
        var profile = await _profiler.CreateProfileAsync(correlationId);
        _ = profile.Status.Should().Be(ProfileStatus.Active);

        // Record kernel executions
        for (var i = 0; i < 10; i++)
        {
            var metrics = new KernelExecutionMetrics
            {
                StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-100),
                EndTime = DateTimeOffset.UtcNow,
                ExecutionTime = TimeSpan.FromMilliseconds(100 + i * 10),
                ThroughputOpsPerSecond = 10000 - i * 100,
                OccupancyPercentage = 75.0 - i,
                CacheHitRate = 0.85 - i * 0.01,
                MemoryBandwidthGBPerSecond = 50.0
            };

            _profiler.RecordKernelExecution(correlationId, $"Kernel{i}", "GPU0", metrics);
        }

        // Record memory operations
        for (var i = 0; i < 5; i++)
        {
            var memMetrics = new MemoryOperationMetrics
            {
                StartTime = DateTimeOffset.UtcNow,
                Duration = TimeSpan.FromMilliseconds(10),
                BytesTransferred = 1024 * 1024 * (i + 1),
                BandwidthGBPerSecond = 100.0 + i * 10,
                AccessPattern = i % 2 == 0 ? "Sequential" : "Random",
                CoalescingEfficiency = i % 2 == 0 ? 0.9 : 0.6
            };

            _profiler.RecordMemoryOperation(correlationId, $"Transfer{i}", "GPU0", memMetrics);
        }

        // Finish profiling
        var finalProfile = await _profiler.FinishProfilingAsync(correlationId);

        // Assert
        _ = finalProfile.Status.Should().Be(ProfileStatus.Completed);
        _ = finalProfile.TotalKernelExecutions.Should().Be(10);
        _ = finalProfile.TotalMemoryOperations.Should().Be(5);
        _ = finalProfile.Analysis.Should().NotBeNull();
        _ = finalProfile.Analysis!.TotalExecutionTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MultiDeviceProfilingScenario_TracksDevicesSeparately()
    {
        // Arrange
        var correlationId = "multi-device-test";
        _ = await _profiler.CreateProfileAsync(correlationId);

        // Act - Record executions on different devices
        var devices = new[] { "GPU0", "GPU1", "CPU" };
        foreach (var device in devices)
        {
            for (var i = 0; i < 5; i++)
            {
                var metrics = new KernelExecutionMetrics
                {
                    StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-100),
                    EndTime = DateTimeOffset.UtcNow,
                    ExecutionTime = TimeSpan.FromMilliseconds(100),
                    ThroughputOpsPerSecond = 10000,
                    OccupancyPercentage = 75.0
                };

                _profiler.RecordKernelExecution(correlationId, $"Kernel_{device}_{i}", device, metrics);
            }
        }

        // Finish profiling
        var profile = await _profiler.FinishProfilingAsync(correlationId);

        // Assert
        _ = profile.DevicesInvolved.Should().Be(3);
        _ = profile.TotalKernelExecutions.Should().Be(15);
        _ = profile.DeviceMetrics.Should().HaveCount(3);
        _ = profile.DeviceMetrics.Should().ContainKeys("GPU0", "GPU1", "CPU");
    }

    [Fact]
    public async Task BottleneckIdentificationScenario_DetectsIssues()
    {
        // Arrange
        var correlationId = "bottleneck-test";
        _ = await _profiler.CreateProfileAsync(correlationId);

        // Act - Record mix of fast and slow kernels so bottlenecks can be identified
        // Fast kernels (baseline)
        for (var i = 0; i < 8; i++)
        {
            var fastMetrics = new KernelExecutionMetrics
            {
                StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-50),
                EndTime = DateTimeOffset.UtcNow,
                ExecutionTime = TimeSpan.FromMilliseconds(50), // Fast execution
                ThroughputOpsPerSecond = 10000,
                OccupancyPercentage = 25.0, // Low occupancy for recommendations
                CacheHitRate = 0.3,
                WarpEfficiency = 0.4,
                MemoryCoalescingEfficiency = 0.5
            };
            _profiler.RecordKernelExecution(correlationId, "FastKernel", "GPU0", fastMetrics);
        }

        // Slow kernels (bottlenecks) - more than 2x the average of fast kernels
        for (var i = 0; i < 2; i++)
        {
            var slowMetrics = new KernelExecutionMetrics
            {
                StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-500),
                EndTime = DateTimeOffset.UtcNow,
                ExecutionTime = TimeSpan.FromMilliseconds(500), // 10x slower than fast kernels
                ThroughputOpsPerSecond = 1000,
                OccupancyPercentage = 25.0,
                CacheHitRate = 0.3,
                WarpEfficiency = 0.4,
                MemoryCoalescingEfficiency = 0.5
            };
            _profiler.RecordKernelExecution(correlationId, "SlowKernel", "GPU0", slowMetrics);
        }

        // Finish profiling
        var profile = await _profiler.FinishProfilingAsync(correlationId);

        // Assert - Should identify bottlenecks (slow kernels are >2x average)
        _ = profile.Analysis.Should().NotBeNull();
        _ = profile.Analysis!.IdentifiedBottlenecks.Should().NotBeEmpty();
        _ = profile.Analysis.OptimizationRecommendations.Should().NotBeEmpty();
    }

    [Fact]
    public void KernelPerformanceTrendAnalysis_DetectsDegradation()
    {
        // Arrange
        var correlationId = "trend-test";
        _profiler.CreateProfileAsync(correlationId).Wait();

        // Act - Simulate performance degradation over time
        // StartTime must increase with i so that later executions (higher i, worse performance)
        // appear later in time when sorted by StartTime
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        for (var i = 0; i < 20; i++)
        {
            var metrics = new KernelExecutionMetrics
            {
                StartTime = baseTime.AddMilliseconds(i * 100), // Forward in time
                EndTime = baseTime.AddMilliseconds(i * 100 + 100 + i * 10),
                ExecutionTime = TimeSpan.FromMilliseconds(100 + i * 10), // Increasing execution time
                ThroughputOpsPerSecond = 10000 - i * 100, // Decreasing throughput
                OccupancyPercentage = 75.0
            };

            _profiler.RecordKernelExecution(correlationId, "DegradingKernel", "GPU0", metrics);
        }

        // Analyze kernel performance
        var analysis = _profiler.AnalyzeKernelPerformance("DegradingKernel");

        // Assert
        _ = analysis.Status.Should().Be(AnalysisStatus.Success);
        _ = analysis.PerformanceTrend.Should().Be(PerformanceTrend.Degrading);
        _ = analysis.MinExecutionTime.Should().BeLessThan(analysis.MaxExecutionTime);
    }

    [Fact]
    public void MemoryAccessPatternOptimization_IdentifiesIssues()
    {
        // Arrange
        var correlationId = "memory-pattern-test";
        _profiler.CreateProfileAsync(correlationId).Wait();

        // Act - Record various memory access patterns
        var sequentialMetrics = new MemoryOperationMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(5),
            BytesTransferred = 1024 * 1024 * 100,
            BandwidthGBPerSecond = 150.0,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.95,
            CacheHitRate = 0.9
        };

        var randomMetrics = new MemoryOperationMetrics
        {
            StartTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(20),
            BytesTransferred = 1024 * 1024 * 100,
            BandwidthGBPerSecond = 30.0,
            AccessPattern = "Random",
            CoalescingEfficiency = 0.4,
            CacheHitRate = 0.5
        };

        // Record 10 sequential, 10 random
        for (var i = 0; i < 10; i++)
        {
            _profiler.RecordMemoryOperation(correlationId, $"SeqOp{i}", "GPU0", sequentialMetrics);
            _profiler.RecordMemoryOperation(correlationId, $"RandOp{i}", "GPU0", randomMetrics);
        }

        // Analyze memory patterns
        var analysis = _profiler.AnalyzeMemoryAccessPatterns();

        // Assert
        _ = analysis.Status.Should().Be(AnalysisStatus.Success);
        _ = analysis.AccessPatternDistribution.Should().ContainKey("Sequential");
        _ = analysis.AccessPatternDistribution.Should().ContainKey("Random");
        _ = analysis.AccessPatternDistribution["Sequential"].Should().Be(10);
        _ = analysis.AccessPatternDistribution["Random"].Should().Be(10);

        // Should recommend optimization due to poor random access performance
        _ = analysis.OptimizationRecommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConcurrentProfilingScenario_HandlesMultipleProfiles()
    {
        // Arrange & Act - Create and finish 5 concurrent profiles
        var tasks = new List<Task>();

        for (var i = 0; i < 5; i++)
        {
            var correlationId = $"concurrent-{i}";
            var task = Task.Run(async () =>
            {
                _ = await _profiler.CreateProfileAsync(correlationId);

                // Record some executions
                for (var j = 0; j < 10; j++)
                {
                    var metrics = new KernelExecutionMetrics
                    {
                        StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-100),
                        EndTime = DateTimeOffset.UtcNow,
                        ExecutionTime = TimeSpan.FromMilliseconds(100),
                        ThroughputOpsPerSecond = 10000
                    };

                    _profiler.RecordKernelExecution(correlationId, $"Kernel{j}", "GPU0", metrics);
                }

                return await _profiler.FinishProfilingAsync(correlationId);
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert - All profiles should complete successfully
        var completedTasks = tasks.Cast<Task<DotCompute.Core.Telemetry.Profiles.PerformanceProfile>>().ToList();
        _ = completedTasks.Should().OnlyContain(t => t.Result.Status == ProfileStatus.Completed);
    }

    [Fact]
    public void SystemPerformanceMonitoring_CapturesRealTimeMetrics()
    {
        // Arrange
        var correlationId = "system-monitoring";
        _profiler.CreateProfileAsync(correlationId).Wait();

        // Act - Record activity
        for (var i = 0; i < 50; i++)
        {
            var metrics = new KernelExecutionMetrics
            {
                StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-10),
                EndTime = DateTimeOffset.UtcNow,
                ExecutionTime = TimeSpan.FromMilliseconds(10),
                ThroughputOpsPerSecond = 10000
            };

            _profiler.RecordKernelExecution(correlationId, "MonitoredKernel", "GPU0", metrics);
        }

        // Get system snapshot
        var snapshot = _profiler.GetSystemPerformanceSnapshot();

        // Assert
        _ = snapshot.Should().NotBeNull();
        _ = snapshot.ActiveProfiles.Should().Be(1);
        _ = snapshot.MemoryUsage.Should().BeGreaterThan(0);
        _ = snapshot.ThreadCount.Should().BeGreaterThan(0);
        _ = snapshot.Gen0Collections.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task LongRunningProfileWithAutoStop_CompletesAutomatically()
    {
        // Arrange
        var correlationId = "auto-stop-test";
        var profileOptions = new ProfileOptions
        {
            AutoStopAfter = TimeSpan.FromMilliseconds(200)
        };

        // Act
        var profile = await _profiler.CreateProfileAsync(correlationId, profileOptions);

        // Assert - Should auto-complete after specified duration
        _ = profile.Status.Should().Be(ProfileStatus.Completed);
        _ = profile.TotalDuration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public void PerformanceOptimizationWorkflow_ProvidesActionableInsights()
    {
        // Arrange
        var correlationId = "optimization-workflow";
        _profiler.CreateProfileAsync(correlationId).Wait();

        // Act - Record various performance characteristics
        var goodMetrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-50),
            EndTime = DateTimeOffset.UtcNow,
            ExecutionTime = TimeSpan.FromMilliseconds(50),
            ThroughputOpsPerSecond = 20000,
            OccupancyPercentage = 85.0,
            CacheHitRate = 0.95,
            WarpEfficiency = 0.9,
            MemoryCoalescingEfficiency = 0.95
        };

        var poorMetrics = new KernelExecutionMetrics
        {
            StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-200),
            EndTime = DateTimeOffset.UtcNow,
            ExecutionTime = TimeSpan.FromMilliseconds(200),
            ThroughputOpsPerSecond = 5000,
            OccupancyPercentage = 30.0,
            CacheHitRate = 0.5,
            WarpEfficiency = 0.4,
            MemoryCoalescingEfficiency = 0.5
        };

        // Record both good and poor kernels
        for (var i = 0; i < 5; i++)
        {
            _profiler.RecordKernelExecution(correlationId, "OptimizedKernel", "GPU0", goodMetrics);
            _profiler.RecordKernelExecution(correlationId, "UnoptimizedKernel", "GPU0", poorMetrics);
        }

        // Analyze both kernels
        var goodAnalysis = _profiler.AnalyzeKernelPerformance("OptimizedKernel");
        var poorAnalysis = _profiler.AnalyzeKernelPerformance("UnoptimizedKernel");

        // Assert
        _ = goodAnalysis.OptimizationRecommendations.Should().BeEmpty(); // Good kernel shouldn't need optimization
        _ = poorAnalysis.OptimizationRecommendations.Should().NotBeEmpty(); // Poor kernel should have recommendations
        _ = poorAnalysis.AverageOccupancy.Should().BeLessThan(goodAnalysis.AverageOccupancy);
    }
}
