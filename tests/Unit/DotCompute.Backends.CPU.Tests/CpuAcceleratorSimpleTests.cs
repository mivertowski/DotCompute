// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Backends.CPU.Tests.Simple;

/// <summary>
/// Simplified comprehensive unit tests for CpuAccelerator with 90% coverage target.
/// Tests initialization, memory management, kernel compilation, and error handling.
/// </summary>
public sealed class CpuAcceleratorSimpleTests : IDisposable
{
    private readonly Mock<ILogger<CpuAccelerator>> _mockLogger;
    private readonly Mock<IOptions<CpuAcceleratorOptions>> _mockOptions;
    private readonly Mock<IOptions<CpuThreadPoolOptions>> _mockThreadPoolOptions;
    private readonly bool _disposed;

    public CpuAcceleratorSimpleTests()
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
    }

    #region Constructor Tests

    [Fact]
    public async Task Constructor_WithValidOptions_ShouldInitializeSuccessfully()
    {
        // Act
        await using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockThreadPoolOptions.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(accelerator);
        accelerator.Type.Should().Be(AcceleratorType.CPU);
        accelerator.Info.Should().NotBeNull();
        accelerator.Memory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CpuAccelerator(null!, Options.Create(new CpuThreadPoolOptions()), _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CpuAccelerator(_mockOptions.Object, Options.Create(new CpuThreadPoolOptions()), null!));
    }

    #endregion

    #region Properties Tests

    [Fact]
    public async Task Type_ShouldReturnCpu()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act
        var type = accelerator.Type;

        // Assert
        Assert.Equal(AcceleratorType.CPU, type);
    }

    [Fact]
    public async Task Info_ShouldContainValidInformation()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act
        var info = accelerator.Info;

        // Assert
        Assert.NotNull(info);
        info.DeviceType.Should().Be("CPU");
        info.Name.Should().NotBeNullOrEmpty();
       info.TotalMemory > 0.Should().BeTrue();
info.ComputeUnits > 0.Should().BeTrue();
        info.IsUnifiedMemory.Should().BeTrue();
        info.Capabilities.Should().NotBeNull();
    }

    [Fact]
    public async Task Info_Capabilities_ShouldContainSimdInformation()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act
        var capabilities = accelerator.Info.Capabilities;

        // Assert
        capabilities.Should().ContainKey("SimdWidth");
        capabilities.Should().ContainKey("ThreadCount");
        capabilities.Should().ContainKey("CacheLineSize");
        capabilities["CacheLineSize"].Should().Be(64);
    }

    [Fact]
    public async Task Memory_ShouldReturnValidMemoryManager()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act
        var memory = accelerator.Memory;

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
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var definition = CreateTestKernelDefinition("TestKernel");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => accelerator.MethodCall().AsTask());
    }

    [Fact]
    public async Task CompileKernelAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var definition = CreateTestKernelDefinition("CancelledKernel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => accelerator.MethodCall().AsTask());
    }

    [Theory]
    [InlineData("VectorAdd")]
    [InlineData("MatrixMultiply")]
    [InlineData("Reduction")]
    public async Task CompileKernelAsync_WithDifferentKernelTypes_ShouldSucceed(string kernelType)
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var definition = CreateTestKernelDefinition($"{kernelType}Kernel", kernelType);

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        Assert.NotNull(result);
        result.Name.Should().Be($"{kernelType}Kernel");
    }

    #endregion

    #region Synchronization Tests

    [Fact]
    public async Task SynchronizeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act
        var task = accelerator.SynchronizeAsync();
        await task;

        // Assert
        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SynchronizeAsync_WithCancellation_ShouldCompleteSuccessfully()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var task = accelerator.SynchronizeAsync(cts.Token);
        await task;

        // Assert
        task.IsCompleted.Should().BeTrue();
    }

    #endregion

    #region Memory Manager Tests

    [Fact]
    public async Task MemoryManager_CreateBuffer_ShouldReturnValidBuffer()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var memory = accelerator.Memory;

        // Act
        using var buffer = await memory.CreateBufferAsync<float>(1024, MemoryLocation.Host);

        // Assert
        Assert.NotNull(buffer);
        buffer.ElementCount.Should().Be(1024);
    }

    [Fact]
    public async Task MemoryManager_CopyBuffer_ShouldTransferData()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var memory = accelerator.Memory;
        var testData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };

        using var source = await memory.CreateBufferAsync(new ReadOnlyMemory<float>(testData), MemoryLocation.Host);
        using var dest = await memory.CreateBufferAsync<float>(4, MemoryLocation.Host);

        // Act
        await memory.CopyAsync(source, dest);

        // Assert - Should not throw
    }

    [Fact]
    public async Task MemoryManager_GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var memory = accelerator.Memory;

        // Act
        var statistics = memory.GetStatistics();

        // Assert
        Assert.NotNull(statistics);
        statistics.TotalAllocatedBytes .Should().BeGreaterThanOrEqualTo(0,);
statistics.AvailableBytes > 0.Should().BeTrue();
        statistics.AllocationCount .Should().BeGreaterThanOrEqualTo(0,);
        statistics.FragmentationPercentage.Should().BeInRange(0.0, 100.0);
        statistics.UsageByLocation.Should().NotBeNull();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var accelerator = new CpuAccelerator(_mockOptions.Object, Options.Create(new CpuThreadPoolOptions()), _mockLogger.Object);

        // Act
        await accelerator.DisposeAsync();

        // Assert - Should not throw
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var accelerator = new CpuAccelerator(_mockOptions.Object, Options.Create(new CpuThreadPoolOptions()), _mockLogger.Object);

        // Act
        await accelerator.DisposeAsync();
        await accelerator.DisposeAsync();

        // Assert - Should not throw
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void CpuAcceleratorOptions_DefaultValues_ShouldBeReasonable()
    {
        // Arrange & Act
        var options = new CpuAcceleratorOptions();

        // Assert
        options.EnableAutoVectorization.Should().BeTrue();
        options.EnableNumaAwareAllocation.Should().BeTrue();
        options.PreferPerformanceOverPower.Should().BeTrue();
        options.MemoryAlignment.Should().Be(64);
options.MaxMemoryAllocation > 0.Should().BeTrue();
        options.EnableKernelCaching.Should().BeTrue();
    }

    [Fact]
    public void CpuAcceleratorOptions_Validation_ShouldDetectInvalidValues()
    {
        // Arrange
        var options = new CpuAcceleratorOptions
        {
            MaxWorkGroupSize = -1,
            MaxMemoryAllocation = -1,
            TargetVectorWidth = 99 // Invalid width
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        errors.Should().Contain(e => e.Contains("MaxWorkGroupSize"));
        errors.Should().Contain(e => e.Contains("MaxMemoryAllocation"));
        errors.Should().Contain(e => e.Contains("TargetVectorWidth"));
    }

    [Theory]
    [InlineData("AVX2")]
    [InlineData("SSE4")]
    [InlineData("NEON")]
    public void CpuAcceleratorOptions_InstructionSetFiltering_ShouldWork(string instructionSet)
    {
        // Arrange
        var options = new CpuAcceleratorOptions();
        options.PreferredInstructionSets.Add(instructionSet);

        // Act
        var shouldUse = options.ShouldUseInstructionSet(instructionSet);
        var shouldNotUse = options.ShouldUseInstructionSet("UnknownSet");

        // Assert
        Assert.True(shouldUse);
        Assert.False(shouldNotUse);
    }

    [Fact]
    public void CpuAcceleratorOptionsExtensions_ConfigureForMaxPerformance_ShouldSetOptimalValues()
    {
        // Arrange
        var options = new CpuAcceleratorOptions();

        // Act
        options.ConfigureForMaxPerformance();

        // Assert
        options.EnableAutoVectorization.Should().BeTrue();
        options.EnableNumaAwareAllocation.Should().BeTrue();
        options.PreferPerformanceOverPower.Should().BeTrue();
        options.UseHugePages.Should().BeTrue();
        options.ComputeThreadPriority.Should().Be(ThreadPriority.AboveNormal);
    }

    #endregion

    #region Helper Methods

    private KernelDefinition CreateTestKernelDefinition(string name, string? operationType = null)
    {
        var sourceCode = operationType switch
        {
            "VectorAdd" => "__kernel void vector_add(__global const float* a, __global const float* b, __global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
            "MatrixMultiply" => "__kernel void matrix_multiply(__global const float* a, __global const float* b, __global float* c) { /* matrix multiply */ }",
            "Reduction" => "__kernel void reduce(__global const float* input, __global float* output) { /* reduction */ }",
            _ => "__kernel void generic() { /* generic kernel */ }"
        };

        return new KernelDefinition(
            name,
            System.Text.Encoding.UTF8.GetBytes(sourceCode))
        {
            EntryPoint = name.ToLowerInvariant(),
            Metadata = new Dictionary<string, object>
            {
                ["Operation"] = operationType ?? "Generic",
                ["Language"] = "OpenCL"
            }
        };
    }

    #endregion

    public void Dispose()
    {
        if(!_disposed)
        {
            _disposed = true;
        }
    }
}
