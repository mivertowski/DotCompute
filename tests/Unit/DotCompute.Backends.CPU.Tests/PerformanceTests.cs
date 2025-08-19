// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Performance;
using FluentAssertions;

namespace DotCompute.Backends.CPU
{

public sealed class KernelPerformanceMetricsTests
{
    [Fact]
    public void KernelPerformanceMetrics_WithValidData_ShouldCalculateCorrectly()
    {
        // Arrange & Act
        var metrics = new KernelPerformanceMetrics
        {
            KernelName = "TestKernel",
            ExecutionCount = 100,
            TotalExecutionTimeMs = 1000.0,
            AverageExecutionTimeMs = 10.0,
            VectorizationEnabled = true,
            VectorWidth = 256,
            InstructionSets = new HashSet<string> { "AVX2" },
            MinExecutionTimeMs = 5.0,
            MaxExecutionTimeMs = 20.0,
            ExecutionTimeStdDev = 2.5,
            Efficiency = 0.85,
            CacheHitRatio = 0.9,
            MemoryBandwidthUtilization = 0.3
        };

        // Assert
        metrics.KernelName.Should().Be("TestKernel");
        metrics.ThroughputOpsPerSec.Should().BeApproximately(100.0, 0.1); // 1000ms / 10ms = 100 ops/sec
        metrics.VectorizationEnabled.Should().BeTrue();
        metrics.InstructionSets.Should().Contain("AVX2");
    }

    [Fact]
    public void ThroughputOpsPerSec_WithZeroAverageTime_ShouldReturnZero()
    {
        // Arrange
        var metrics = new KernelPerformanceMetrics
        {
            KernelName = "Test",
            ExecutionCount = 0,
            TotalExecutionTimeMs = 0,
            AverageExecutionTimeMs = 0,
            VectorizationEnabled = false,
            VectorWidth = 0,
            InstructionSets = new HashSet<string>()
        };

        // Act & Assert
        metrics.ThroughputOpsPerSec.Should().Be(0);
    }
}

public sealed class CpuKernelProfilerTests : IDisposable
{
    private readonly CpuKernelProfiler _profiler;

    public CpuKernelProfilerTests()
    {
        _profiler = new CpuKernelProfiler(samplingIntervalMs: 50); // Faster sampling for tests
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_profiler);
    }

    [Fact]
    public void StartProfiling_ShouldReturnValidSession()
    {
        // Act
        using var session = _profiler.StartProfiling("TestKernel", true, 256);

        // Assert
        Assert.NotNull(session);
        session.KernelName.Should().Be("TestKernel");
        session.UseVectorization.Should().BeTrue();
        session.VectorWidth.Should().Be(256);
        session.SessionId.Should().Not.Be(Guid.Empty);
    }

    [Fact]
    public void GetMetrics_WithNoExecutions_ShouldReturnEmptyMetrics()
    {
        // Act
        var metrics = _profiler.GetMetrics("NonExistentKernel");

        // Assert
        Assert.NotNull(metrics);
        metrics.KernelName.Should().Be("NonExistentKernel");
        metrics.ExecutionCount.Should().Be(0);
        metrics.TotalExecutionTimeMs.Should().Be(0);
        metrics.AverageExecutionTimeMs.Should().Be(0);
    }

    [Fact]
    public async Task ProfilingSession_WhenDisposed_ShouldUpdateMetrics()
    {
        // Arrange
        const string kernelName = "ProfilingTestKernel";

        // Act
        using(var session = _profiler.StartProfiling(kernelName, true, 256))
        {
            // Simulate some work
            await Task.Delay(10);
        } // Session disposed here

        // Give profiler time to process
        await Task.Delay(100);

        // Assert
        var metrics = _profiler.GetMetrics(kernelName);
        metrics.ExecutionCount.Should().Be(1);
        (metrics.TotalExecutionTimeMs > 0).Should().BeTrue();
        metrics.VectorizationEnabled.Should().BeTrue();
        metrics.VectorWidth.Should().Be(256);
    }

    [Fact]
    public void GetAllMetrics_WithMultipleKernels_ShouldReturnAll()
    {
        // Arrange
        using(var session1 = _profiler.StartProfiling("Kernel1", false, 128))
        using(var session2 = _profiler.StartProfiling("Kernel2", true, 256))
        {
            // Sessions automatically dispose
        }

        // Act
        var allMetrics = _profiler.GetAllMetrics();

        // Assert
        allMetrics.Should().ContainKey("Kernel1");
        allMetrics.Should().ContainKey("Kernel2");
        allMetrics["Kernel1"].VectorizationEnabled.Should().BeFalse();
        allMetrics["Kernel2"].VectorizationEnabled.Should().BeTrue();
    }

    [Fact]
    public void Reset_ShouldClearAllMetrics()
    {
        // Arrange
        using(var session = _profiler.StartProfiling("TestKernel", false, 128))
        {
            // Session automatically disposes
        }

        // Act
        _profiler.Reset();

        // Assert
        var metrics = _profiler.GetMetrics("TestKernel");
        metrics.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public void StartProfiling_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _profiler.Dispose();

        // Act & Assert
        _profiler.Invoking(p => p.StartProfiling("Test", false, 128))
            .Should().Throw<ObjectDisposedException>();
    }

    public void Dispose()
    {
        _profiler?.Dispose();
    }
}

public sealed class ProfilingSessionTests
{
    [Fact]
    public void ProfilingSession_Properties_ShouldBeSetCorrectly()
    {
        // Arrange
        using var profiler = new CpuKernelProfiler();
        
        // Act
        using var session = profiler.StartProfiling("TestKernel", true, 512);

        // Assert
        session.SessionId.Should().Not.Be(Guid.Empty);
        session.KernelName.Should().Be("TestKernel");
        session.UseVectorization.Should().BeTrue();
        session.VectorWidth.Should().Be(512);
        (session.StartTime > 0).Should().BeTrue();
        session.StartCounters.Should().NotBeNull();
    }

    [Fact]
    public async Task ProfilingSession_LongRunning_ShouldMeasureCorrectTime()
    {
        // Arrange
        using var profiler = new CpuKernelProfiler();
        const int delayMs = 50;
        
        // Act
        using(var session = profiler.StartProfiling("TimingTest", false, 128))
        {
            await Task.Delay(delayMs);
        }

        // Allow processing time
        await Task.Delay(10);

        // Assert
        var metrics = profiler.GetMetrics("TimingTest");
        (metrics.TotalExecutionTimeMs >= delayMs * 0.8).Should().BeTrue(); // Allow some tolerance
        (metrics.TotalExecutionTimeMs <= delayMs * 2.0).Should().BeTrue(); // Upper bound for CI environments
    }
}

public sealed class PerformanceAnalyzerTests
{
    [Fact]
    public void AnalyzeKernel_WithGoodPerformance_ShouldReturnHighScore()
    {
        // Arrange
        var metrics = new KernelPerformanceMetrics
        {
            KernelName = "HighPerformanceKernel",
            ExecutionCount = 1000,
            TotalExecutionTimeMs = 100.0,
            AverageExecutionTimeMs = 0.1,
            VectorizationEnabled = true,
            VectorWidth = 256,
            InstructionSets = new HashSet<string> { "AVX2" },
            ExecutionTimeStdDev = 0.01, // Low variance
            Efficiency = 0.9, // High efficiency
            CacheHitRatio = 0.95, // High cache hit ratio
            MemoryBandwidthUtilization = 0.2 // Low memory pressure
        };

        // Act
        var analysis = PerformanceAnalyzer.AnalyzeKernel(metrics);

        // Assert
        analysis.PerformanceScore.Should().BeGreaterThan(80.0);
        analysis.Issues.Should().BeEmpty();
        analysis.Recommendations.Should().BeEmpty();
        analysis.Analysis.Should().Contain("TestKernel");
    }

    [Fact]
    public void AnalyzeKernel_WithPoorPerformance_ShouldIdentifyIssues()
    {
        // Arrange
        var metrics = new KernelPerformanceMetrics
        {
            KernelName = "PoorPerformanceKernel",
            ExecutionCount = 100,
            TotalExecutionTimeMs = 1000.0,
            AverageExecutionTimeMs = 10.0,
            VectorizationEnabled = false, // Not using vectorization
            VectorWidth = 0,
            InstructionSets = new HashSet<string>(),
            ExecutionTimeStdDev = 5.0, // High variance
            Efficiency = 0.3, // Low efficiency
            CacheHitRatio = 0.5, // Poor cache performance
            MemoryBandwidthUtilization = 0.9 // High memory pressure
        };

        // Act
        var analysis = PerformanceAnalyzer.AnalyzeKernel(metrics);

        // Assert
        analysis.PerformanceScore.Should().BeLessThan(60.0);
        analysis.Issues.Should().NotBeEmpty();
        analysis.Issues.Should().Contain(issue => issue.Contains("variance"));
        analysis.Issues.Should().Contain(issue => issue.Contains("cache"));
        analysis.Issues.Should().Contain(issue => issue.Contains("bandwidth"));
        analysis.Issues.Should().Contain(issue => issue.Contains("vectorization"));
        analysis.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public void AnalyzeKernel_WithVectorizedButInefficient_ShouldSuggestOptimizations()
    {
        // Arrange
        var metrics = new KernelPerformanceMetrics
        {
            KernelName = "InefficientVectorKernel",
            ExecutionCount = 500,
            TotalExecutionTimeMs = 500.0,
            AverageExecutionTimeMs = 1.0,
            VectorizationEnabled = true,
            VectorWidth = 512,
            InstructionSets = new HashSet<string> { "AVX512F" },
            ExecutionTimeStdDev = 0.1,
            Efficiency = 0.4, // Low efficiency despite vectorization
            CacheHitRatio = 0.85,
            MemoryBandwidthUtilization = 0.3
        };

        // Act
        var analysis = PerformanceAnalyzer.AnalyzeKernel(metrics);

        // Assert
        analysis.Issues.Should().Contain(issue => issue.Contains("vectorization efficiency"));
        analysis.Recommendations.Should().Contain(rec => rec.Contains("SIMD"));
    }

    [Theory]
    [InlineData(0.5, 0.8, 0.2, true)] // Low efficiency, good cache, low memory - efficiency issue
    [InlineData(0.9, 0.6, 0.2, true)] // Good efficiency, poor cache, low memory - cache issue
    [InlineData(0.9, 0.9, 0.9, true)] // Good efficiency, good cache, high memory - bandwidth issue
    public void AnalyzeKernel_WithSpecificIssues_ShouldIdentifyCorrectly(
        double efficiency, 
        double cacheHitRatio, 
        double memoryBandwidth, 
        bool vectorizationEnabled)
    {
        // Arrange
        var metrics = new KernelPerformanceMetrics
        {
            KernelName = "TestKernel",
            ExecutionCount = 100,
            TotalExecutionTimeMs = 100.0,
            AverageExecutionTimeMs = 1.0,
            VectorizationEnabled = vectorizationEnabled,
            VectorWidth = vectorizationEnabled ? 256 : 0,
            InstructionSets = vectorizationEnabled ? new HashSet<string> { "AVX2" } : new HashSet<string>(),
            ExecutionTimeStdDev = 0.05,
            Efficiency = efficiency,
            CacheHitRatio = cacheHitRatio,
            MemoryBandwidthUtilization = memoryBandwidth
        };

        // Act
        var analysis = PerformanceAnalyzer.AnalyzeKernel(metrics);

        // Assert
        if(efficiency < 0.7 && vectorizationEnabled)
        {
            analysis.Issues.Should().Contain(issue => issue.Contains("vectorization efficiency"));
        }
        
        if(cacheHitRatio < 0.8)
        {
            analysis.Issues.Should().Contain(issue => issue.Contains("cache"));
        }
        
        if(memoryBandwidth > 0.8)
        {
            analysis.Issues.Should().Contain(issue => issue.Contains("bandwidth"));
        }
    }

    [Fact]
    public void AnalyzeKernel_GeneratedAnalysis_ShouldContainKeyMetrics()
    {
        // Arrange
        var metrics = new KernelPerformanceMetrics
        {
            KernelName = "AnalysisTestKernel",
            ExecutionCount = 42,
            TotalExecutionTimeMs = 84.0,
            AverageExecutionTimeMs = 2.0,
            VectorizationEnabled = true,
            VectorWidth = 256,
            InstructionSets = new HashSet<string> { "AVX2", "FMA" },
            Efficiency = 0.75,
            CacheHitRatio = 0.88,
            MemoryBandwidthUtilization = 0.45
        };

        // Act
        var analysis = PerformanceAnalyzer.AnalyzeKernel(metrics);

        // Assert
        analysis.Analysis.Should().Contain("AnalysisTestKernel");
        analysis.Analysis.Should().Contain("42"); // Execution count
        analysis.Analysis.Should().Contain("2.00"); // Average time
        analysis.Analysis.Should().Contain("Enabled").And.Contain("256-bit"); // Vectorization
        analysis.Analysis.Should().Contain("AVX2"); // Instruction sets
        analysis.Analysis.Should().Contain("FMA");
    }
}

public sealed class PerformanceCounterManagerTests : IDisposable
{
    private readonly PerformanceCounterManager _manager;

    public PerformanceCounterManagerTests()
    {
        _manager = new PerformanceCounterManager();
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_manager);
    }

    [Fact]
    public void Sample_ShouldReturnValidSample()
    {
        // Act
        var sample = _manager.Sample();

        // Assert
        sample.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        sample.CpuUsagePercent.Should().BeGreaterThanOrEqualTo(0);
        sample.AvailableMemoryMB.Should().BeGreaterThanOrEqualTo(0);
        sample.CacheHitRatio.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
        sample.MemoryBandwidthUtilization.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void Sample_CalledMultipleTimes_ShouldReturnDifferentTimestamps()
    {
        // Act
        var sample1 = _manager.Sample();
        Thread.Sleep(10); // Small delay
        var sample2 = _manager.Sample();

        // Assert
        sample2.Timestamp.Should().BeAfter(sample1.Timestamp);
    }

    [Fact]
    public void Sample_AfterDispose_ShouldReturnEmptySample()
    {
        // Arrange
        _manager.Dispose();

        // Act
        var sample = _manager.Sample();

        // Assert
        sample.CpuUsagePercent.Should().Be(0);
        sample.AvailableMemoryMB.Should().Be(0);
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }
}

public sealed class NumaNodeMetricsTests
{
    [Fact]
    public void NumaNodeMetrics_WithValidData_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var metrics = new NumaNodeMetrics
        {
            NodeId = 1,
            ExecutionCount = 500,
            TotalExecutionTimeMs = 1000.0,
            MemoryAllocations = 50,
            MemoryUsageBytes = 1024 * 1024 * 10 // 10MB
        };

        // Assert
        metrics.NodeId.Should().Be(1);
        metrics.ExecutionCount.Should().Be(500);
        metrics.TotalExecutionTimeMs.Should().Be(1000.0);
        metrics.MemoryAllocations.Should().Be(50);
        metrics.MemoryUsageBytes.Should().Be(1024 * 1024 * 10);
    }
}
}
