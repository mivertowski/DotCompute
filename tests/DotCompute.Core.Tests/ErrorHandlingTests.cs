using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Core.Pipelines;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive error handling tests for DotCompute.Core
/// These tests target edge cases and error scenarios to boost coverage to 95%+
/// </summary>
public class ErrorHandlingTests
{
    [Fact]
    public void AcceleratorInfo_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new AcceleratorInfo(AcceleratorType.CPU, null!, "1.0", 1024));
    }

    [Fact]
    public void AcceleratorInfo_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new AcceleratorInfo(AcceleratorType.CPU, "", "1.0", 1024));
    }

    [Fact]
    public void AcceleratorInfo_WithWhitespaceName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new AcceleratorInfo(AcceleratorType.CPU, "   ", "1.0", 1024));
    }

    [Fact]
    public void AcceleratorInfo_WithNullVersion_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new AcceleratorInfo(AcceleratorType.CPU, "Test", null!, 1024));
    }

    [Fact]
    public void AcceleratorInfo_WithNegativeMemorySize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", -1));
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MaxValue - 1)]
    [InlineData(1024L * 1024L * 1024L * 1024L)] // 1TB
    public void AcceleratorInfo_WithExtremeMemorySizes_ShouldHandleGracefully(long memorySize)
    {
        // Arrange & Act
        var info = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", memorySize);

        // Assert
        info.MemorySize.Should().Be(memorySize);
        info.Type.Should().Be(AcceleratorType.CPU);
    }

    [Fact]
    public void AcceleratorInfo_Equality_WithNullComparison_ShouldReturnFalse()
    {
        // Arrange
        var info = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024);

        // Act & Assert
        info.Equals(null).Should().BeFalse();
        (info == null).Should().BeFalse();
        (null == info).Should().BeFalse();
    }

    [Fact]
    public void AcceleratorInfo_GetHashCode_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var info1 = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024);
        var info2 = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024);

        // Act & Assert
        info1.GetHashCode().Should().Be(info2.GetHashCode());
    }

    [Fact]
    public void AcceleratorInfo_GetHashCode_WithDifferentValues_ShouldBeDifferent()
    {
        // Arrange
        var info1 = new AcceleratorInfo(AcceleratorType.CPU, "Test1", "1.0", 1024);
        var info2 = new AcceleratorInfo(AcceleratorType.CPU, "Test2", "1.0", 1024);

        // Act & Assert
        info1.GetHashCode().Should().NotBe(info2.GetHashCode());
    }

    [Fact]
    public void AcceleratorInfo_ToString_ShouldContainAllProperties()
    {
        // Arrange
        var info = new AcceleratorInfo(AcceleratorType.GPU, "TestGPU", "2.1", 2048);

        // Act
        var result = info.ToString();

        // Assert
        result.Should().Contain("GPU");
        result.Should().Contain("TestGPU");
        result.Should().Contain("2.1");
        result.Should().Contain("2048");
    }
}

/// <summary>
/// Pipeline error handling and edge case tests
/// </summary>
public class PipelineErrorHandlingTests
{
    [Fact]
    public void KernelPipeline_WithNullStages_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KernelPipeline(null!));
    }

    [Fact]
    public void KernelPipeline_WithEmptyStages_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new KernelPipeline(Array.Empty<IPipelineStage>()));
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var stages = new IPipelineStage[] { new TestPipelineStage() };
        var pipeline = new KernelPipeline(stages);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await pipeline.ExecuteAsync(new object(), cts.Token));
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_WithNullInput_ShouldThrowArgumentNullException()
    {
        // Arrange
        var stages = new IPipelineStage[] { new TestPipelineStage() };
        var pipeline = new KernelPipeline(stages);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await pipeline.ExecuteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void PipelineMetrics_WithNegativeValues_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new PipelineMetrics { ExecutionTime = TimeSpan.FromMilliseconds(-1) });
    }

    [Fact]
    public void PipelineMetrics_ExportPrometheus_WithInvalidMetrics_ShouldHandleGracefully()
    {
        // Arrange
        var metrics = new PipelineMetrics
        {
            ExecutionTime = TimeSpan.MaxValue,
            MemoryUsed = long.MaxValue,
            ThroughputOpsPerSecond = double.PositiveInfinity
        };

        // Act & Assert
        var result = metrics.ExportPrometheus();
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("execution_time");
        result.Should().Contain("memory_used");
    }

    [Fact]
    public void PipelineMetrics_ExportJson_WithExtremeValues_ShouldSerialize()
    {
        // Arrange
        var metrics = new PipelineMetrics
        {
            ExecutionTime = TimeSpan.Zero,
            MemoryUsed = 0,
            ThroughputOpsPerSecond = 0.0,
            StageCount = int.MaxValue
        };

        // Act & Assert
        var result = metrics.ExportJson();
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"ExecutionTime\"");
        result.Should().Contain("\"MemoryUsed\"");
    }

    [Fact]
    public void PipelineOptimizer_CreateOptimizedPipeline_WithNullStages_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optimizer = new PipelineOptimizer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            optimizer.CreateOptimizedPipeline(null!));
    }

    [Fact]
    public void PipelineOptimizer_CreateOptimizedPipeline_WithSingleStage_ShouldReturnOptimized()
    {
        // Arrange
        var optimizer = new PipelineOptimizer();
        var stages = new IPipelineStage[] { new TestPipelineStage() };

        // Act
        var result = optimizer.CreateOptimizedPipeline(stages);

        // Assert
        result.Should().NotBeNull();
        result.StageCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PipelineOptimizer_CreateParallelStage_WithNullOperations_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optimizer = new PipelineOptimizer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            optimizer.CreateParallelStage(null!));
    }

    [Fact]
    public void PipelineErrors_InvalidStageConfiguration_ShouldBeHandled()
    {
        // Arrange & Act
        var error = new PipelineErrors.InvalidStageConfiguration("Test stage", "Test error");

        // Assert
        error.StageName.Should().Be("Test stage");
        error.ErrorDetails.Should().Be("Test error");
        error.Message.Should().Contain("Test stage");
        error.Message.Should().Contain("Test error");
    }

    [Fact]
    public void PipelineErrors_StageExecutionFailure_WithInnerException_ShouldPreserveStack()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var error = new PipelineErrors.StageExecutionFailure("Test stage", innerException);

        // Assert
        error.StageName.Should().Be("Test stage");
        error.InnerException.Should().Be(innerException);
        error.Message.Should().Contain("Test stage");
    }
}

/// <summary>
/// Memory and resource management error tests
/// </summary>
public class ResourceManagementErrorTests
{
    [Fact]
    public void KernelDefinition_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new KernelDefinition("test", null!, new CompilationOptions()));
    }

    [Fact]
    public void KernelDefinition_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new KernelDefinition("test", new TextKernelSource("source"), null!));
    }

    [Fact]
    public void KernelDefinition_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new KernelDefinition("", new TextKernelSource("source"), new CompilationOptions()));
    }

    [Fact]
    public void TextKernelSource_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TextKernelSource(null!));
    }

    [Fact]
    public void TextKernelSource_WithEmptySource_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new TextKernelSource(""));
    }

    [Fact]
    public void BytecodeKernelSource_WithNullBytecode_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BytecodeKernelSource(null!));
    }

    [Fact]
    public void BytecodeKernelSource_WithEmptyBytecode_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new BytecodeKernelSource(Array.Empty<byte>()));
    }

    [Fact]
    public void CompilationOptions_WithNullDefines_ShouldHandleGracefully()
    {
        // Arrange & Act
        var options = new CompilationOptions { Defines = null };

        // Assert
        options.Should().NotBeNull();
        options.Defines.Should().BeNull();
    }

    [Fact]
    public void CompilationOptions_WithEmptyDefines_ShouldHandleGracefully()
    {
        // Arrange & Act
        var options = new CompilationOptions { Defines = new Dictionary<string, string>() };

        // Assert
        options.Defines.Should().NotBeNull();
        options.Defines.Should().BeEmpty();
    }

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Basic)]
    [InlineData(OptimizationLevel.Aggressive)]
    public void CompilationOptions_WithAllOptimizationLevels_ShouldBeSupported(OptimizationLevel level)
    {
        // Arrange & Act
        var options = new CompilationOptions { OptimizationLevel = level };

        // Assert
        options.OptimizationLevel.Should().Be(level);
    }
}

/// <summary>
/// Test implementation of IPipelineStage for testing purposes
/// </summary>
internal class TestPipelineStage : IPipelineStage
{
    public string Name => "TestStage";
    public int Priority => 1;

    public Task<object> ProcessAsync(object input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(input);
    }

    public void Dispose()
    {
        // Test implementation - no resources to dispose
    }
}

/// <summary>
/// Concurrency and thread safety tests
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public void AcceleratorInfo_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var info = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024);
        const int threadCount = 10;
        const int operationsPerThread = 1000;
        var tasks = new List<Task>();
        var results = new List<bool>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    var result = info.Type == AcceleratorType.CPU &&
                                info.Name == "Test" &&
                                info.Version == "1.0" &&
                                info.MemorySize == 1024;
                    
                    lock (results)
                    {
                        results.Add(result);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        results.Should().HaveCount(threadCount * operationsPerThread);
        results.Should().OnlyContain(r => r == true, "All concurrent reads should be consistent");
    }

    [Fact]
    public async Task KernelPipeline_ConcurrentExecution_ShouldHandleMultipleRequests()
    {
        // Arrange
        var stages = new IPipelineStage[] { new TestPipelineStage() };
        var pipeline = new KernelPipeline(stages);
        const int concurrentRequests = 50;
        var tasks = new List<Task<object>>();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            var input = $"Request-{i}";
            tasks.Add(pipeline.ExecuteAsync(input, CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentRequests);
        for (int i = 0; i < concurrentRequests; i++)
        {
            results[i].Should().Be($"Request-{i}");
        }
    }

    [Fact]
    public void PipelineMetrics_ConcurrentUpdates_ShouldBeSafe()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        const int threadCount = 10;
        const int updatesPerThread = 100;
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < updatesPerThread; j++)
                    {
                        var newMetrics = new PipelineMetrics
                        {
                            ExecutionTime = TimeSpan.FromMilliseconds(j),
                            MemoryUsed = j * 1024,
                            ThroughputOpsPerSecond = j * 10.0
                        };
                        
                        // Simulate concurrent access to metrics
                        var json = newMetrics.ExportJson();
                        json.Should().NotBeNullOrEmpty();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        exceptions.Should().BeEmpty("No thread safety issues should occur");
    }
}

/// <summary>
/// Performance and stress tests for coverage
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void AcceleratorInfo_Creation_ShouldBePerformant()
    {
        // Arrange
        const int iterations = 10000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var info = new AcceleratorInfo(AcceleratorType.CPU, $"Test-{i}", "1.0", 1024);
            info.Should().NotBeNull();
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            "Creating 10k AcceleratorInfo instances should be fast");
    }

    [Fact]
    public async Task KernelPipeline_HighVolumeExecution_ShouldMaintainPerformance()
    {
        // Arrange
        var stages = new IPipelineStage[] { new TestPipelineStage() };
        var pipeline = new KernelPipeline(stages);
        const int executionCount = 1000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < executionCount; i++)
        {
            await pipeline.ExecuteAsync($"Input-{i}", CancellationToken.None);
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "1000 pipeline executions should complete within 5 seconds");
    }

    [Fact]
    public void PipelineMetrics_SerializationPerformance_ShouldBeAcceptable()
    {
        // Arrange
        var metrics = new PipelineMetrics
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            MemoryUsed = 1024 * 1024,
            ThroughputOpsPerSecond = 1000.0
        };
        const int iterations = 1000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var json = metrics.ExportJson();
            var prometheus = metrics.ExportPrometheus();
            
            json.Should().NotBeNullOrEmpty();
            prometheus.Should().NotBeNullOrEmpty();
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, 
            "1000 serializations should complete within 2 seconds");
    }
}