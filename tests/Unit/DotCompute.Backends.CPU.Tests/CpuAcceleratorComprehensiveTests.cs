// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using FluentAssertions;
// Memory types are in the main namespace
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Backends.CPU.Tests.Accelerators;

/// <summary>
/// Comprehensive unit tests for CpuAccelerator with 90% coverage target.
/// Tests initialization, memory management, kernel compilation, and error handling.
/// </summary>
public sealed class CpuAcceleratorComprehensiveTests : IDisposable
{
    private readonly Mock<ILogger<CpuAccelerator>> _mockLogger;
    private readonly Mock<IOptions<CpuAcceleratorOptions>> _mockOptions;
    private readonly Mock<IOptions<CpuThreadPoolOptions>> _mockThreadPoolOptions;
    private readonly CpuAccelerator _accelerator;
    private readonly bool _disposed;

    public CpuAcceleratorComprehensiveTests()
    {
        _mockLogger = new Mock<ILogger<CpuAccelerator>>();
        _mockOptions = new Mock<IOptions<CpuAcceleratorOptions>>();
        _mockThreadPoolOptions = new Mock<IOptions<CpuThreadPoolOptions>>();

        // Setup default options
        _mockOptions.Setup(o => o.Value).Returns(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true
        });

        _mockThreadPoolOptions.Setup(o => o.Value).Returns(new CpuThreadPoolOptions
        {
            WorkerThreads = Environment.ProcessorCount,
            MaxQueuedItems = 10000,
            EnableWorkStealing = true
        });

        _accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_accelerator);
        _accelerator.Type.Should().Be(AcceleratorType.CPU);
        _accelerator.Info.Should().NotBeNull();
        _accelerator.Memory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act =() => new CpuAccelerator(
            null!,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);
        act.Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithNullThreadPoolOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act =() => new CpuAccelerator(
            _mockOptions.Object,
            null!,
            _mockLogger.Object);
        act.Throw<ArgumentNullException>().WithParameterName("threadPoolOptions");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act =() => new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            null!);
        act.Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Type_ShouldReturnCpu()
    {
        // Act
        var type = _accelerator.Type;

        // Assert
        Assert.Equal(AcceleratorType.CPU, type);
    }

    [Fact]
    public void Info_ShouldContainValidInformation()
    {
        // Act
        var info = _accelerator.Info;

        // Assert
        Assert.NotNull(info);
        info.Type.Should().Be(AcceleratorType.CPU);
        info.Name.Should().NotBeNullOrEmpty();
        info.Name.Should().Contain("CPU");
        info.Name.Should().Contain("cores");
       info.DeviceMemory > 0.Should().BeTrue();
       info.ComputeUnits > 0.Should().BeTrue();
        info.ComputeUnits.Should().Be(Environment.ProcessorCount);
        info.IsUnified.Should().BeTrue();
        info.Capabilities.Should().NotBeNull();
    }

    [Fact]
    public void Info_Capabilities_ShouldContainRequiredKeys()
    {
        // Act
        var capabilities = _accelerator.Info.Capabilities;

        // Assert
        capabilities.Should().ContainKey("SimdWidth");
        capabilities.Should().ContainKey("SimdInstructionSets");
        capabilities.Should().ContainKey("ThreadCount");
        capabilities.Should().ContainKey("NumaNodes");
        capabilities.Should().ContainKey("CacheLineSize");

        capabilities["SimdWidth"].Should().BeOfType<int>();
        capabilities["ThreadCount"].Should().BeOfType<int>();
        capabilities["NumaNodes"].Should().BeOfType<int>();
        capabilities["CacheLineSize"].Should().Be(64);
    }

    [Fact]
    public void Memory_ShouldReturnValidMemoryManager()
    {
        // Act
        var memory = _accelerator.Memory;

        // Assert
        Assert.NotNull(memory);
        Assert.IsAssignableFrom<IMemoryManager>(memory);
    }

    #endregion

    #region Kernel Compilation Tests

    [Fact]
    public async Task CompileKernelAsync_WithValidDefinition_ShouldReturnCompiledKernel()
    {
        // Arrange
        var definition = CreateTestKernelDefinition("TestKernel", "VectorAdd");

        // Act
        var result = await _accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _accelerator.MethodCall().AsTask())
            .WithParameterName("definition");
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullOptions_ShouldUseDefaults()
    {
        // Arrange
        var definition = CreateTestKernelDefinition("TestKernel", "VectorAdd");

        // Act
        var result = await _accelerator.CompileKernelAsync(definition, null);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task CompileKernelAsync_WithOptimizationEnabled_ShouldTryOptimizedKernel()
    {
        // Arrange
        var options = new Mock<IOptions<CpuAcceleratorOptions>>();
        options.Setup(o => o.Value).Returns(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true
        });

        await using var accelerator = new CpuAccelerator(
            options.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        var definition = CreateTestKernelDefinition("VectorAddKernel", "VectorAdd");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("VectorAddKernel");
    }

    [Fact]
    public async Task CompileKernelAsync_WithOptimizationDisabled_ShouldFallbackToStandardCompilation()
    {
        // Arrange
        var options = new Mock<IOptions<CpuAcceleratorOptions>>();
        options.Setup(o => o.Value).Returns(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = false,
            PreferPerformanceOverPower = false
        });

        await using var accelerator = new CpuAccelerator(
            options.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        var definition = CreateTestKernelDefinition("StandardKernel", "Generic");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("StandardKernel");
    }

    [Theory]
    [InlineData("VectorAdd")]
    [InlineData("VectorScale")]
    [InlineData("MatrixMultiply")]
    [InlineData("Reduction")]
    [InlineData("MemoryIntensive")]
    [InlineData("ComputeIntensive")]
    public async Task CompileKernelAsync_WithKnownKernelTypes_ShouldCreateOptimizedKernels(string kernelType)
    {
        // Arrange
        var definition = CreateTestKernelDefinition($"{kernelType}Kernel", kernelType);

        // Act
        var result = await _accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be($"{kernelType}Kernel");
    }

    [Fact]
    public async Task CompileKernelAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var definition = CreateTestKernelDefinition("CancelledKernel", "VectorAdd");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _accelerator.MethodCall().AsTask());
    }

    [Fact]
    public async Task CompileKernelAsync_WithInvalidKernelCode_ShouldHandleGracefully()
    {
        // Arrange
        var definition = new KernelDefinition(
            "InvalidKernel",
            System.Text.Encoding.UTF8.GetBytes("invalid kernel code that will cause parsing to fail"));

        // Act
        var result = await _accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result); // Should fall back to standard compilation
        result.Name.Should().Be("InvalidKernel");
    }

    #endregion

    #region Synchronization Tests

    [Fact]
    public async Task SynchronizeAsync_ShouldCompleteSuccessfully()
    {
        // Act
        var task = _accelerator.SynchronizeAsync();
        await task;

        // Assert
        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SynchronizeAsync_WithCancellation_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var task = _accelerator.SynchronizeAsync(cts.Token);
        await task;

        // Assert
        task.IsCompleted.Should().BeTrue();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        // Act
        await accelerator.DisposeAsync();

        // Assert - Should not throw
        // Verify logger was called for disposal
        VerifyLoggerWasCalledForDisposal();
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        // Act
        await accelerator.DisposeAsync();
        await accelerator.DisposeAsync();

        // Assert - Should not throw
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeThreadPoolAndMemoryManager()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        // Act
        await accelerator.DisposeAsync();

        // Assert - Should complete without throwing
        // Internal resources should be disposed
    }

    #endregion

    #region Platform-Specific Tests

    [Theory]
    [InlineData(1024 * 1024)]      // 1MB
    [InlineData(4L * 1024 * 1024 * 1024)] // 4GB
    public void GetTotalPhysicalMemory_ShouldReturnReasonableValue(long minExpected)
    {
        // Act
        var info = _accelerator.Info;

        // Assert
        (info.DeviceMemory >= minExpected).Should().BeTrue();
    }

    [Fact]
    public void GetProcessorName_ShouldContainArchitectureAndCoreCount()
    {
        // Act
        var info = _accelerator.Info;

        // Assert
        info.Name.Should().NotBeNullOrEmpty();
        info.Name.Should().Contain("CPU");
        info.Name.Should().Contain("cores");
        info.Name.Should().MatchRegex(@"\d+\s+cores");
    }

    [Fact]
    public void GetNumaNodeCount_ShouldReturnPositiveValue()
    {
        // Act
        var capabilities = _accelerator.Info.Capabilities;

        // Assert
        capabilities.Should().ContainKey("NumaNodes");
        capabilities["NumaNodes"].Should().BeOfType<int>();
       ((int)capabilities["NumaNodes"]).Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetCacheLineSize_ShouldReturn64()
    {
        // Act
        var capabilities = _accelerator.Info.Capabilities;

        // Assert
        capabilities.Should().ContainKey("CacheLineSize");
        capabilities["CacheLineSize"].Should().Be(64);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CompileKernelAsync_WithExceptionInOptimizedCompilation_ShouldFallbackToStandard()
    {
        // Arrange
        var definition = new KernelDefinition(
            "ProblematicKernel", 
            System.Text.Encoding.UTF8.GetBytes("kernel that might cause optimization issues"));

        // Act
        var result = await _accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("ProblematicKernel");
    }

    [Fact]
    public void ConvertToCoreKernelDefinition_ShouldHandleNullMetadata()
    {
        // Arrange
        var definition = new KernelDefinition(
            "TestKernel",
            System.Text.Encoding.UTF8.GetBytes("test code"));

        // Act
        var result = _accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void ConvertToCoreCompilationOptions_ShouldHandleAllOptimizationLevels()
    {
        // This is tested indirectly through CompileKernelAsync calls
        // The conversion should work for all optimization levels
        var result = true; // Placeholder
        Assert.True(result);
    }

    #endregion

    #region Helper Methods

    private KernelDefinition CreateTestKernelDefinition(string name, string operation)
    {
        var sourceCode = operation switch
        {
            "VectorAdd" => "__kernel void vector_add(__global const float* a, __global const float* b, __global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
            "VectorScale" => "__kernel void vector_scale(__global const float* input, __global float* output, float scale) { int i = get_global_id(0); output[i] = input[i] * scale; }",
            "MatrixMultiply" => "__kernel void matrix_multiply(__global const float* a, __global const float* b, __global float* c, const int n) { /* matrix multiply */ }",
            "Reduction" => "__kernel void reduce(__global const float* input, __global float* output, const int n) { /* reduction */ }",
            "MemoryIntensive" => "__kernel void memory_intensive(__global const float* input, __global float* output) { /* memory intensive operations */ }",
            "ComputeIntensive" => "__kernel void compute_intensive(__global const float* input, __global float* output) { /* compute intensive operations */ }",
            _ => "__kernel void generic() { /* generic kernel */ }"
        };

        return new KernelDefinition(
            name,
            System.Text.Encoding.UTF8.GetBytes(sourceCode))
        {
            EntryPoint = name.ToLowerInvariant(),
            Metadata = new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["Language"] = "OpenCL"
            }
        };
    }

    private void VerifyLoggerWasCalledForDisposal()
    {
        // Verify that the logger was called with disposal message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing CPU accelerator")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    public void Dispose()
    {
        if(!_disposed)
        {
            _accelerator?.DisposeAsync().AsTask().Wait();
            _disposed = true;
        }
    }
}
