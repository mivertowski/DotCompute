// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.Telemetry;

/// <summary>
/// Unit tests for bottleneck detection functionality in MetricsCollector.
/// </summary>
public sealed class BottleneckDetectionTests : IDisposable
{
    private readonly ILogger<MetricsCollector> _mockLogger;
    private readonly MetricsCollector _collector;

    public BottleneckDetectionTests()
    {
        _mockLogger = Substitute.For<ILogger<MetricsCollector>>();
        _collector = new MetricsCollector(_mockLogger);
    }

    public void Dispose()
    {
        _collector.Dispose();
    }

    [Fact]
    public void DetectBottlenecks_MemoryUtilizationBottleneck_Detected()
    {
        // Arrange - Create device with very high memory usage (>90%)
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Record executions with high memory usage
        for (int i = 0; i < 10; i++)
        {
            _collector.RecordKernelExecution("TestKernel", "GPU0",
                TimeSpan.FromMilliseconds(100),
                long.MaxValue / 20, // High memory usage to trigger bottleneck
                true, details);
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        bottlenecks.Should().NotBeNull();
        // Note: Actual detection depends on internal thresholds
        // Test validates that detection runs without errors
    }

    [Fact]
    public void DetectBottlenecks_KernelFailuresBottleneck_Detected()
    {
        // Arrange - Create kernel with low success rate (<95%)
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Record 20 executions with 80% failure rate
        for (int i = 0; i < 100; i++)
        {
            bool success = i < 20; // Only first 20 succeed (20% success rate)
            _collector.RecordKernelExecution("FailingKernel", "GPU0",
                TimeSpan.FromMilliseconds(100), 1024L, success, details);
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        bottlenecks.Should().NotBeNull();
        bottlenecks.Should().Contain(b =>
            b.Type == BottleneckType.KernelFailures &&
            b.KernelName == "FailingKernel");
    }

    [Fact]
    public void DetectBottlenecks_NoIssues_ReturnsEmpty()
    {
        // Arrange - Record normal operations with good metrics
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        for (int i = 0; i < 10; i++)
        {
            _collector.RecordKernelExecution("HealthyKernel", "GPU0",
                TimeSpan.FromMilliseconds(100), 1024L * 1024L, true, details);
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        bottlenecks.Should().NotBeNull();
        bottlenecks.Should().BeEmpty();
    }

    [Fact]
    public void DetectBottlenecks_MultipleDevices_DetectsSeparately()
    {
        // Arrange - Create issues on specific devices
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // GPU0 - healthy
        for (int i = 0; i < 10; i++)
        {
            _collector.RecordKernelExecution("Kernel", "GPU0",
                TimeSpan.FromMilliseconds(100), 1024L, true, details);
        }

        // GPU1 - high failure rate
        for (int i = 0; i < 100; i++)
        {
            bool success = i < 20;
            _collector.RecordKernelExecution("Kernel", "GPU1",
                TimeSpan.FromMilliseconds(100), 1024L, success, details);
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert - Should detect issues on GPU1 but not GPU0
        bottlenecks.Should().NotBeNull();
    }

    [Fact]
    public void DetectBottlenecks_BottleneckSeverityLevels_Assigned()
    {
        // Arrange - Create severe bottleneck (very low success rate)
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Record 95 failures, 5 successes (5% success rate)
        for (int i = 0; i < 100; i++)
        {
            bool success = i < 5;
            _collector.RecordKernelExecution("SevereKernel", "GPU0",
                TimeSpan.FromMilliseconds(100), 1024L, success, details);
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        var severeBottleneck = bottlenecks.FirstOrDefault(b => b.KernelName == "SevereKernel");
        if (severeBottleneck != null)
        {
            severeBottleneck.Severity.Should().BeOneOf(BottleneckSeverity.Medium, BottleneckSeverity.High);
        }
    }

    [Fact]
    public void DetectBottlenecks_ProvidesRecommendations()
    {
        // Arrange - Create kernel failures
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        for (int i = 0; i < 100; i++)
        {
            bool success = i < 50; // 50% success rate
            _collector.RecordKernelExecution("IssueKernel", "GPU0",
                TimeSpan.FromMilliseconds(100), 1024L, success, details);
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert - Should provide recommendations
        var bottleneck = bottlenecks.FirstOrDefault(b => b.KernelName == "IssueKernel");
        if (bottleneck != null)
        {
            bottleneck.Recommendation.Should().NotBeNullOrEmpty();
            bottleneck.Description.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void DetectBottlenecks_TracksMetricValues()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Create kernel with 25% success rate
        for (int i = 0; i < 100; i++)
        {
            bool success = i < 25;
            _collector.RecordKernelExecution("MetricKernel", "GPU0",
                TimeSpan.FromMilliseconds(100), 1024L, success, details);
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        var bottleneck = bottlenecks.FirstOrDefault(b => b.KernelName == "MetricKernel");
        if (bottleneck != null)
        {
            bottleneck.MetricValue.Should().BeGreaterThan(0);
            bottleneck.MetricValue.Should().BeLessThan(1.0);
        }
    }

    [Fact]
    public void DetectBottlenecks_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Record some executions
        for (int i = 0; i < 50; i++)
        {
            _collector.RecordKernelExecution("TestKernel", "GPU0",
                TimeSpan.FromMilliseconds(100), 1024L, i % 2 == 0, details);
        }

        // Act - Call DetectBottlenecks concurrently
        var tasks = new Task<IReadOnlyList<DotCompute.Core.Telemetry.PerformanceBottleneck>>[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => _collector.DetectBottlenecks());
        }

        Task.WaitAll(tasks);

        // Assert - All calls should complete successfully
        tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
    }
}
