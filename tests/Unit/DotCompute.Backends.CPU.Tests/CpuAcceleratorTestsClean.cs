// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CPU.Tests.Clean;

/// <summary>
/// Clean unit tests for CpuAccelerator focusing only on public API and working components.
/// </summary>
public class CpuAcceleratorTestsClean : IDisposable
{
    private readonly Mock<ILogger<CpuAccelerator>> _mockLogger;
    private readonly Mock<IOptions<CpuAcceleratorOptions>> _mockOptions;
    private readonly Mock<IOptions<CpuThreadPoolOptions>> _mockThreadPoolOptions;
    private bool _disposed;

    public CpuAcceleratorTestsClean()
    {
        _mockLogger = new Mock<ILogger<CpuAccelerator>>();
        _mockOptions = new Mock<IOptions<CpuAcceleratorOptions>>();
        _mockThreadPoolOptions = new Mock<IOptions<CpuThreadPoolOptions>>();

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
    public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Act
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Type.Should().Be(AcceleratorType.CPU);
        accelerator.Memory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new CpuAccelerator(null!, _mockThreadPoolOptions.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Type_ShouldReturnCPU()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act & Assert
        accelerator.Type.Should().Be(AcceleratorType.CPU);
    }

    [Fact]
    public void Memory_ShouldReturnNonNull()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act & Assert
        accelerator.Memory.Should().NotBeNull();
    }

    #endregion

    #region CompileKernelAsync Tests

    [Fact]
    public async Task CompileKernelAsync_WithValidKernel_ShouldReturnCompiledKernel()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
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
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act & Assert
        await accelerator.Invoking(a => a.CompileKernelAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CompileKernelAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var definition = CreateTestKernelDefinition("CancelledKernel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await accelerator.Invoking(a => a.CompileKernelAsync(definition, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region SynchronizeAsync Tests

    [Fact]
    public async Task SynchronizeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act & Assert
        await accelerator.Invoking(a => a.SynchronizeAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task SynchronizeAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await accelerator.Invoking(a => a.SynchronizeAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Memory Management Tests

    [Fact]
    public async Task Memory_CreateBuffer_ShouldReturnValidBuffer()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act
        using var buffer = await accelerator.Memory.CreateBufferAsync<float>(1024, MemoryLocation.Host, MemoryAccess.ReadWrite);

        // Assert
        buffer.Should().NotBeNull();
        buffer.ElementCount.Should().Be(1024);
        buffer.Location.Should().Be(MemoryLocation.Host);
        buffer.Access.Should().Be(MemoryAccess.ReadWrite);
    }

    [Fact]
    public async Task Memory_CreateBufferFromData_ShouldReturnValidBuffer()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };

        // Act
        using var buffer = await accelerator.Memory.CreateBufferAsync(data, MemoryLocation.Host, MemoryAccess.ReadOnly);

        // Assert
        buffer.Should().NotBeNull();
        buffer.ElementCount.Should().Be(4);
        buffer.Location.Should().Be(MemoryLocation.Host);
        buffer.Access.Should().Be(MemoryAccess.ReadOnly);
    }

    [Fact]
    public void Memory_GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        using var accelerator = new CpuAccelerator(_mockOptions.Object, _mockThreadPoolOptions.Object, _mockLogger.Object);

        // Act
        var stats = accelerator.Memory.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalAllocatedBytes.Should().BeGreaterOrEqualTo(0);
        stats.AllocationCount.Should().BeGreaterOrEqualTo(0);
        stats.DeallocationsCount.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region Helper Methods

    private KernelDefinition CreateTestKernelDefinition(string name)
    {
        var code = @"
__kernel void vectorAdd(__global const float* a, __global const float* b, __global float* c) {
    int id = get_global_id(0);
    c[id] = a[id] + b[id];
}";
        return new KernelDefinition(name, System.Text.Encoding.UTF8.GetBytes(code));
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

/// <summary>
/// Tests for CpuAcceleratorOptions configuration and validation.
/// </summary>
public class CpuAcceleratorOptionsTests
{
    [Fact]
    public void DefaultOptions_ShouldHaveExpectedDefaults()
    {
        // Act
        var options = new CpuAcceleratorOptions();

        // Assert
        options.EnableAutoVectorization.Should().BeTrue();
        options.EnableNumaAwareAllocation.Should().BeTrue();
        options.MaxMemoryAllocation.Should().Be(2L * 1024 * 1024 * 1024); // 2GB
        options.EnableProfiling.Should().BeTrue();
        options.EnableKernelCaching.Should().BeTrue();
        options.MaxCachedKernels.Should().Be(1000);
        options.MemoryAlignment.Should().Be(64);
    }

    [Fact]
    public void Validate_WithValidOptions_ShouldReturnNoErrors()
    {
        // Arrange
        var options = new CpuAcceleratorOptions
        {
            MaxWorkGroupSize = 512,
            MaxMemoryAllocation = 1024 * 1024,
            MinVectorizationWorkSize = 128,
            TargetVectorWidth = 256
        };

        // Act
        var errors = options.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1, "MaxWorkGroupSize must be positive when specified")]
    [InlineData(0, "MaxWorkGroupSize must be positive when specified")]
    public void Validate_WithInvalidMaxWorkGroupSize_ShouldReturnError(int maxWorkGroupSize, string expectedError)
    {
        // Arrange
        var options = new CpuAcceleratorOptions
        {
            MaxWorkGroupSize = maxWorkGroupSize
        };

        // Act
        var errors = options.Validate();

        // Assert
        errors.Should().Contain(expectedError);
    }

    [Fact]
    public void GetEffectiveWorkGroupSize_WithoutMaxSet_ShouldReturnDefaultCalculation()
    {
        // Arrange
        var options = new CpuAcceleratorOptions();

        // Act
        var workGroupSize = options.GetEffectiveWorkGroupSize();

        // Assert
        workGroupSize.Should().BePositive();
        workGroupSize.Should().BeLessOrEqualTo(1024);
    }

    [Fact]
    public void GetEffectiveWorkGroupSize_WithMaxSet_ShouldRespectMaximum()
    {
        // Arrange
        var options = new CpuAcceleratorOptions
        {
            MaxWorkGroupSize = 256
        };

        // Act
        var workGroupSize = options.GetEffectiveWorkGroupSize();

        // Assert
        workGroupSize.Should().BeLessOrEqualTo(256);
        workGroupSize.Should().BePositive();
    }

    [Theory]
    [InlineData("AVX2", true)]
    [InlineData("SSE4", true)]
    [InlineData("DISABLED", false)]
    public void ShouldUseInstructionSet_WithPreferences_ShouldRespectSettings(string instructionSet, bool expected)
    {
        // Arrange
        var options = new CpuAcceleratorOptions();
        if (!expected)
        {
            options.DisabledInstructionSets.Add(instructionSet);
        }
        else
        {
            options.PreferredInstructionSets.Add(instructionSet);
        }

        // Act
        var shouldUse = options.ShouldUseInstructionSet(instructionSet);

        // Assert
        shouldUse.Should().Be(expected);
    }

    [Fact]
    public void ConfigureForMaxPerformance_ShouldSetOptimalSettings()
    {
        // Arrange
        var options = new CpuAcceleratorOptions();

        // Act
        options.ConfigureForMaxPerformance();

        // Assert
        options.EnableAutoVectorization.Should().BeTrue();
        options.EnableNumaAwareAllocation.Should().BeTrue();
        options.EnableLoopUnrolling.Should().BeTrue();
        options.EnableMemoryPrefetching.Should().BeTrue();
        options.PreferPerformanceOverPower.Should().BeTrue();
        options.ComputeThreadPriority.Should().Be(ThreadPriority.AboveNormal);
        options.UseHugePages.Should().BeTrue();
        options.EnableHardwareCounters.Should().BeTrue();
        options.MemoryAlignment.Should().Be(64);
    }

    [Fact]
    public void ConfigureForMinMemory_ShouldSetMemoryOptimizedSettings()
    {
        // Arrange
        var options = new CpuAcceleratorOptions();

        // Act
        options.ConfigureForMinMemory();

        // Assert
        options.EnableKernelCaching.Should().BeFalse();
        options.MaxCachedKernels.Should().Be(10);
        options.EnableProfiling.Should().BeFalse();
        options.UseHugePages.Should().BeFalse();
        options.EnableMemoryPrefetching.Should().BeFalse();
        options.MemoryPrefetchDistance.Should().Be(1);
    }
}