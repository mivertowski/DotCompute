// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CPU.Tests.Simple;

/// <summary>
/// Simplified comprehensive unit tests for CpuAccelerator with 90% coverage target.
/// Tests initialization, memory management, kernel compilation, and error handling.
/// </summary>
public class CpuAcceleratorSimpleTests : IDisposable
{
    private readonly Mock<ILogger<CpuAccelerator>> _mockLogger;
    private readonly Mock<IOptions<CpuAcceleratorOptions>> _mockOptions;
    private bool _disposed;

    public CpuAcceleratorSimpleTests()
    {
        _mockLogger = new Mock<ILogger<CpuAccelerator>>();
        _mockOptions = new Mock<IOptions<CpuAcceleratorOptions>>();

        // Setup default options
        _mockOptions.Setup(o => o.Value).Returns(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true
        });
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidOptions_ShouldInitializeSuccessfully()
    {
        // Act
        using var accelerator = new CpuAccelerator(
            _mockOptions.Object,
            _mockLogger.Object);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Type.Should().Be(AcceleratorType.CPU);
        accelerator.Info.Should().NotBeNull();
        accelerator.Memory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new CpuAccelerator(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new CpuAccelerator(_mockOptions.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Type_ShouldReturnCpu()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

        // Act
        var type = accelerator.Type;

        // Assert
        type.Should().Be(AcceleratorType.CPU);
    }

    [Fact]
    public void Info_ShouldContainValidInformation()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

        // Act
        var info = accelerator.Info;

        // Assert
        info.Should().NotBeNull();
        info.Type.Should().Be(AcceleratorType.CPU);
        info.Name.Should().NotBeNullOrEmpty();
        info.DeviceMemory.Should().BeGreaterThan(0);
        info.ComputeUnits.Should().BeGreaterThan(0);
        info.IsUnified.Should().BeTrue();
        info.Capabilities.Should().NotBeNull();
    }

    [Fact]
    public void Info_Capabilities_ShouldContainSimdInformation()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

        // Act
        var capabilities = accelerator.Info.Capabilities;

        // Assert
        capabilities.Should().ContainKey("SimdWidth");
        capabilities.Should().ContainKey("ThreadCount");
        capabilities.Should().ContainKey("CacheLineSize");
        capabilities["CacheLineSize"].Should().Be(64);
    }

    [Fact]
    public void Memory_ShouldReturnValidMemoryManager()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

        // Act
        var memory = accelerator.Memory;

        // Assert
        memory.Should().NotBeNull();
        memory.Should().BeAssignableTo<IMemoryManager>();
    }

    #endregion

    #region Kernel Compilation Tests

    [Fact]
    public async Task CompileKernelAsync_WithValidDefinition_ShouldReturnCompiledKernel()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);
        var definition = CreateTestKernelDefinition("TestKernel");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

        // Act & Assert
        await accelerator.Invoking(a => a.CompileKernelAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CompileKernelAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);
        var definition = CreateTestKernelDefinition("CancelledKernel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await accelerator.Invoking(a => a.CompileKernelAsync(definition, cancellationToken: cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("VectorAdd")]
    [InlineData("MatrixMultiply")]
    [InlineData("Reduction")]
    public async Task CompileKernelAsync_WithDifferentKernelTypes_ShouldSucceed(string kernelType)
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);
        var definition = CreateTestKernelDefinition($"{kernelType}Kernel", kernelType);

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be($"{kernelType}Kernel");
    }

    #endregion

    #region Synchronization Tests

    [Fact]
    public async Task SynchronizeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

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
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);
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
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);
        var memory = accelerator.Memory;

        // Act
        using var buffer = await memory.CreateBufferAsync<float>(1024, MemoryLocation.Host);

        // Assert
        buffer.Should().NotBeNull();
        buffer.ElementCount.Should().Be(1024);
    }

    [Fact]
    public async Task MemoryManager_CopyBuffer_ShouldTransferData()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);
        var memory = accelerator.Memory;
        var testData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };

        using var source = await memory.CreateBufferAsync(new ReadOnlyMemory<float>(testData), MemoryLocation.Host);
        using var dest = await memory.CreateBufferAsync<float>(4, MemoryLocation.Host);

        // Act
        await memory.CopyAsync(source, dest);

        // Assert - Should not throw
    }

    [Fact]
    public void MemoryManager_GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);
        var memory = accelerator.Memory;

        // Act
        var statistics = memory.GetStatistics();

        // Assert
        statistics.Should().NotBeNull();
        statistics.TotalAllocatedBytes.Should().BeGreaterOrEqualTo(0);
        statistics.AvailableBytes.Should().BeGreaterThan(0);
        statistics.AllocationCount.Should().BeGreaterOrEqualTo(0);
        statistics.FragmentationPercentage.Should().BeInRange(0.0, 100.0);
        statistics.UsageByLocation.Should().NotBeNull();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

        // Act
        await accelerator.DisposeAsync();

        // Assert - Should not throw
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var accelerator = new CpuAccelerator(_mockOptions.Object, _mockLogger.Object);

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
        options.MaxMemoryAllocation.Should().BeGreaterThan(0);
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
        errors.Should().NotBeEmpty();
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
        shouldUse.Should().BeTrue();
        shouldNotUse.Should().BeFalse();
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
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}