// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using KernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;

namespace DotCompute.Abstractions.Tests.Interfaces.Kernels;

/// <summary>
/// Comprehensive tests for IKernelExecutor interface and related types.
/// </summary>
public class IKernelExecutorTests
{
    #region Interface Contract Tests

    [Fact]
    public void IKernelExecutor_ShouldHaveAcceleratorProperty()
    {
        // Arrange
        var executor = Substitute.For<IKernelExecutor>();
        var accelerator = Substitute.For<IAccelerator>();
        executor.Accelerator.Returns(accelerator);

        // Act
        var result = executor.Accelerator;

        // Assert
        result.Should().Be(accelerator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidKernel_ShouldReturnResult()
    {
        // Arrange
        var executor = Substitute.For<IKernelExecutor>();
        var kernel = Substitute.For<CompiledKernel>();
        var arguments = Array.Empty<KernelArgument>();
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 }
        };
        var expectedResult = new KernelExecutionResult
        {
            Success = true,
            Handle = new KernelExecutionHandle
            {
                Id = Guid.NewGuid(),
                KernelName = "test_kernel",
                SubmittedAt = DateTimeOffset.UtcNow
            }
        };
        executor.ExecuteAsync(kernel, arguments, config, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await executor.ExecuteAsync(kernel, arguments, config);

        // Assert
        result.Should().Be(expectedResult);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAndWaitAsync_ShouldWaitForCompletion()
    {
        // Arrange
        var executor = Substitute.For<IKernelExecutor>();
        var kernel = Substitute.For<CompiledKernel>();
        var arguments = Array.Empty<KernelArgument>();
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 }
        };
        var handle = new KernelExecutionHandle
        {
            Id = Guid.NewGuid(),
            KernelName = "test_kernel",
            SubmittedAt = DateTimeOffset.UtcNow,
            IsCompleted = true,
            CompletedAt = DateTimeOffset.UtcNow
        };
        var expectedResult = new KernelExecutionResult
        {
            Success = true,
            Handle = handle
        };
        executor.ExecuteAndWaitAsync(kernel, arguments, config, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await executor.ExecuteAndWaitAsync(kernel, arguments, config);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Handle.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void EnqueueExecution_ShouldReturnHandle()
    {
        // Arrange
        var executor = Substitute.For<IKernelExecutor>();
        var kernel = Substitute.For<CompiledKernel>();
        var arguments = Array.Empty<KernelArgument>();
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 }
        };
        var expectedHandle = new KernelExecutionHandle
        {
            Id = Guid.NewGuid(),
            KernelName = "test_kernel",
            SubmittedAt = DateTimeOffset.UtcNow
        };
        executor.EnqueueExecution(kernel, arguments, config).Returns(expectedHandle);

        // Act
        var handle = executor.EnqueueExecution(kernel, arguments, config);

        // Assert
        handle.Should().NotBeNull();
        handle.Id.Should().NotBe(Guid.Empty);
        handle.KernelName.Should().Be("test_kernel");
    }

    [Fact]
    public async Task WaitForCompletionAsync_ShouldReturnResult()
    {
        // Arrange
        var executor = Substitute.For<IKernelExecutor>();
        var handle = new KernelExecutionHandle
        {
            Id = Guid.NewGuid(),
            KernelName = "test_kernel",
            SubmittedAt = DateTimeOffset.UtcNow
        };
        var expectedResult = new KernelExecutionResult
        {
            Success = true,
            Handle = handle
        };
        executor.WaitForCompletionAsync(handle, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await executor.WaitForCompletionAsync(handle);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void GetOptimalExecutionConfig_ShouldReturnConfiguration()
    {
        // Arrange
        var executor = Substitute.For<IKernelExecutor>();
        var kernel = Substitute.For<CompiledKernel>();
        var problemSize = new[] { 1024, 1024 };
        var expectedConfig = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024, 1024 },
            LocalWorkSize = new[] { 16, 16 }
        };
        executor.GetOptimalExecutionConfig(kernel, problemSize).Returns(expectedConfig);

        // Act
        var config = executor.GetOptimalExecutionConfig(kernel, problemSize);

        // Assert
        config.Should().NotBeNull();
        config.GlobalWorkSize.Should().BeEquivalentTo(problemSize);
    }

    [Fact]
    public async Task ProfileAsync_ShouldReturnProfilingResults()
    {
        // Arrange
        var executor = Substitute.For<IKernelExecutor>();
        var kernel = Substitute.For<CompiledKernel>();
        var arguments = Array.Empty<KernelArgument>();
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 }
        };
        var expectedResult = new KernelProfilingResult
        {
            Iterations = 100,
            AverageTimeMs = 1.5,
            MinTimeMs = 1.0,
            MaxTimeMs = 2.0,
            StdDevMs = 0.2,
            MedianTimeMs = 1.4
        };
        executor.ProfileAsync(kernel, arguments, config, 100, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await executor.ProfileAsync(kernel, arguments, config, 100);

        // Assert
        result.Should().NotBeNull();
        result.Iterations.Should().Be(100);
        result.AverageTimeMs.Should().BeGreaterThan(0);
    }

    #endregion

    #region KernelArgument Tests

    [Fact]
    public void KernelArgument_ShouldInitializeWithRequiredProperties()
    {
        // Arrange & Act
        var argument = new KernelArgument
        {
            Name = "input",
            Value = 42,
            Type = typeof(int)
        };

        // Assert
        argument.Name.Should().Be("input");
        argument.Value.Should().Be(42);
        argument.Type.Should().Be(typeof(int));
    }

    [Fact]
    public void KernelArgument_DeviceMemory_ShouldHaveMemoryBuffer()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();

        // Act
        var argument = new KernelArgument
        {
            Name = "buffer",
            Value = buffer,
            Type = typeof(IUnifiedMemoryBuffer),
            IsDeviceMemory = true,
            MemoryBuffer = buffer,
            SizeInBytes = 1024
        };

        // Assert
        argument.IsDeviceMemory.Should().BeTrue();
        argument.MemoryBuffer.Should().Be(buffer);
        argument.SizeInBytes.Should().Be(1024);
    }

    [Fact]
    public void KernelArgument_OutputParameter_ShouldBeFlagged()
    {
        // Arrange & Act
        var argument = new KernelArgument
        {
            Name = "output",
            Value = new int[100],
            Type = typeof(int[]),
            IsOutput = true
        };

        // Assert
        argument.IsOutput.Should().BeTrue();
    }

    [Fact]
    public void KernelArgument_Size_ShouldMapToSizeInBytes()
    {
        // Arrange & Act
        var argument = new KernelArgument
        {
            Name = "data",
            Value = new byte[512],
            Type = typeof(byte[]),
            Size = 512
        };

        // Assert
        argument.SizeInBytes.Should().Be(512);
        argument.Size.Should().Be(512);
    }

    #endregion

    #region KernelExecutionConfig Tests

    [Fact]
    public void KernelExecutionConfig_ShouldInitializeWithGlobalWorkSize()
    {
        // Arrange & Act
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024, 1024 }
        };

        // Assert
        config.GlobalWorkSize.Should().BeEquivalentTo(new[] { 1024, 1024 });
        config.LocalWorkSize.Should().BeNull();
    }

    [Fact]
    public void KernelExecutionConfig_WithLocalWorkSize_ShouldInitialize()
    {
        // Arrange & Act
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024, 1024 },
            LocalWorkSize = new[] { 16, 16 }
        };

        // Assert
        config.LocalWorkSize.Should().BeEquivalentTo(new[] { 16, 16 });
    }

    [Fact]
    public void KernelExecutionConfig_WithSharedMemory_ShouldSet()
    {
        // Arrange & Act
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 },
            DynamicSharedMemorySize = 4096
        };

        // Assert
        config.DynamicSharedMemorySize.Should().Be(4096);
    }

    [Fact]
    public void KernelExecutionConfig_WithFlags_ShouldSet()
    {
        // Arrange & Act
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 },
            Flags = KernelExecutionFlags.PreferSharedMemory | KernelExecutionFlags.HighPriority
        };

        // Assert
        config.Flags.Should().HaveFlag(KernelExecutionFlags.PreferSharedMemory);
        config.Flags.Should().HaveFlag(KernelExecutionFlags.HighPriority);
    }

    [Fact]
    public void KernelExecutionConfig_WithCaptureTimings_ShouldSet()
    {
        // Arrange & Act
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 },
            CaptureTimings = true
        };

        // Assert
        config.CaptureTimings.Should().BeTrue();
    }

    #endregion

    #region KernelExecutionHandle Tests

    [Fact]
    public void KernelExecutionHandle_ShouldInitializeWithRequiredProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;

        // Act
        var handle = new KernelExecutionHandle
        {
            Id = id,
            KernelName = "vector_add",
            SubmittedAt = submittedAt
        };

        // Assert
        handle.Id.Should().Be(id);
        handle.KernelName.Should().Be("vector_add");
        handle.SubmittedAt.Should().Be(submittedAt);
        handle.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void KernelExecutionHandle_WhenCompleted_ShouldHaveCompletionTime()
    {
        // Arrange
        var handle = new KernelExecutionHandle
        {
            Id = Guid.NewGuid(),
            KernelName = "test",
            SubmittedAt = DateTimeOffset.UtcNow
        };

        // Act
        handle.IsCompleted = true;
        handle.CompletedAt = DateTimeOffset.UtcNow;

        // Assert
        handle.IsCompleted.Should().BeTrue();
        handle.CompletedAt.Should().NotBeNull();
        handle.CompletedAt.Should().BeOnOrAfter(handle.SubmittedAt);
    }

    #endregion

    #region KernelExecutionResult Tests

    [Fact]
    public void KernelExecutionResult_CreateSuccess_ShouldReturnSuccessResult()
    {
        // Arrange
        var handle = new KernelExecutionHandle
        {
            Id = Guid.NewGuid(),
            KernelName = "test",
            SubmittedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = KernelExecutionResult.CreateSuccess(handle);

        // Assert
        result.Success.Should().BeTrue();
        result.Handle.Should().Be(handle);
        result.ErrorMessage.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void KernelExecutionResult_CreateFailure_ShouldReturnFailureResult()
    {
        // Arrange
        var handle = new KernelExecutionHandle
        {
            Id = Guid.NewGuid(),
            KernelName = "test",
            SubmittedAt = DateTimeOffset.UtcNow
        };
        var error = new InvalidOperationException("Test error");

        // Act
        var result = KernelExecutionResult.CreateFailure(handle, error);

        // Assert
        result.Success.Should().BeFalse();
        result.Handle.Should().Be(handle);
        result.Error.Should().Be(error);
        result.ErrorMessage.Should().Be("Test error");
    }

    [Fact]
    public void KernelExecutionResult_ExecutionId_ShouldMatchHandleId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handle = new KernelExecutionHandle
        {
            Id = id,
            KernelName = "test",
            SubmittedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = new KernelExecutionResult
        {
            Success = true,
            Handle = handle
        };

        // Assert
        result.ExecutionId.Should().Be(id);
    }

    [Fact]
    public void KernelExecutionResult_WithTimings_ShouldContainPerformanceData()
    {
        // Arrange
        var handle = new KernelExecutionHandle
        {
            Id = Guid.NewGuid(),
            KernelName = "test",
            SubmittedAt = DateTimeOffset.UtcNow
        };
        var timings = new KernelExecutionTimings
        {
            KernelTimeMs = 1.5,
            TotalTimeMs = 2.0
        };

        // Act
        var result = new KernelExecutionResult
        {
            Success = true,
            Handle = handle,
            Timings = timings
        };

        // Assert
        result.Timings.Should().NotBeNull();
        result.Timings!.KernelTimeMs.Should().Be(1.5);
        result.Timings.MemoryTransferTimeMs.Should().Be(0.5);
    }

    #endregion

    #region KernelExecutionTimings Tests

    [Fact]
    public void KernelExecutionTimings_MemoryTransferTime_ShouldCalculateCorrectly()
    {
        // Arrange & Act
        var timings = new KernelExecutionTimings
        {
            KernelTimeMs = 5.0,
            TotalTimeMs = 8.0
        };

        // Assert
        timings.MemoryTransferTimeMs.Should().Be(3.0);
    }

    [Fact]
    public void KernelExecutionTimings_WithBandwidth_ShouldSet()
    {
        // Arrange & Act
        var timings = new KernelExecutionTimings
        {
            KernelTimeMs = 1.0,
            TotalTimeMs = 2.0,
            EffectiveMemoryBandwidthGBps = 150.5
        };

        // Assert
        timings.EffectiveMemoryBandwidthGBps.Should().Be(150.5);
    }

    [Fact]
    public void KernelExecutionTimings_WithThroughput_ShouldSet()
    {
        // Arrange & Act
        var timings = new KernelExecutionTimings
        {
            KernelTimeMs = 1.0,
            TotalTimeMs = 2.0,
            EffectiveComputeThroughputGFLOPS = 500.0
        };

        // Assert
        timings.EffectiveComputeThroughputGFLOPS.Should().Be(500.0);
    }

    #endregion

    #region KernelProfilingResult Tests

    [Fact]
    public void KernelProfilingResult_ShouldContainStatistics()
    {
        // Arrange & Act
        var result = new KernelProfilingResult
        {
            Iterations = 100,
            AverageTimeMs = 1.5,
            MinTimeMs = 1.0,
            MaxTimeMs = 2.5,
            StdDevMs = 0.3,
            MedianTimeMs = 1.4
        };

        // Assert
        result.Iterations.Should().Be(100);
        result.AverageTimeMs.Should().Be(1.5);
        result.MinTimeMs.Should().BeLessThan(result.AverageTimeMs);
        result.MaxTimeMs.Should().BeGreaterThan(result.AverageTimeMs);
    }

    [Fact]
    public void KernelProfilingResult_WithPercentiles_ShouldContainData()
    {
        // Arrange
        var percentiles = new Dictionary<int, double>
        {
            [50] = 1.4,
            [95] = 2.0,
            [99] = 2.3
        };

        // Act
        var result = new KernelProfilingResult
        {
            Iterations = 100,
            AverageTimeMs = 1.5,
            MinTimeMs = 1.0,
            MaxTimeMs = 2.5,
            StdDevMs = 0.3,
            MedianTimeMs = 1.4,
            PercentileTimingsMs = percentiles
        };

        // Assert
        result.PercentileTimingsMs.Should().ContainKey(50);
        result.PercentileTimingsMs.Should().ContainKey(95);
        result.PercentileTimingsMs.Should().ContainKey(99);
    }

    #endregion

    #region BottleneckAnalysis Tests

    [Fact]
    public void BottleneckAnalysis_ShouldIdentifyBottleneck()
    {
        // Arrange & Act
        var analysis = new BottleneckAnalysis
        {
            Type = BottleneckType.Memory,
            Severity = 0.8,
            Details = "Memory bandwidth saturated"
        };

        // Assert
        analysis.Type.Should().Be(BottleneckType.Memory);
        analysis.Severity.Should().Be(0.8);
        analysis.Details.Should().Contain("Memory");
    }

    [Fact]
    public void BottleneckAnalysis_WithResourceUtilization_ShouldContainMetrics()
    {
        // Arrange
        var utilization = new Dictionary<string, double>
        {
            ["Memory"] = 95.0,
            ["Compute"] = 45.0,
            ["L1Cache"] = 80.0
        };

        // Act
        var analysis = new BottleneckAnalysis
        {
            Type = BottleneckType.Memory,
            Severity = 0.95,
            Details = "High memory pressure",
            ResourceUtilization = utilization
        };

        // Assert
        analysis.ResourceUtilization.Should().ContainKey("Memory");
        analysis.ResourceUtilization["Memory"].Should().Be(95.0);
    }

    #endregion
}
