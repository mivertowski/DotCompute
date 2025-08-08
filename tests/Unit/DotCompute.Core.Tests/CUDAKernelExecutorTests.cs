// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using DotCompute.Core.Types;
using DotCompute.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using CompiledKernel = DotCompute.SharedTestUtilities.CompiledKernel;
using KernelArgument = DotCompute.SharedTestUtilities.KernelArgument;
using KernelConfiguration = DotCompute.SharedTestUtilities.KernelConfiguration;
using KernelLanguage = DotCompute.SharedTestUtilities.KernelLanguage;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive tests for CUDA kernel executor that can run on CI/CD without GPU hardware.
/// Uses mocks and dependency injection to simulate CUDA runtime behavior.
/// </summary>
public class CUDAKernelExecutorTests : IDisposable
{
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<ILogger<CUDAKernelExecutor>> _mockLogger;
    private readonly CUDAKernelExecutor _executor;

    public CUDAKernelExecutorTests()
    {
        _mockAccelerator = CreateMockCudaAccelerator();
        _mockLogger = new Mock<ILogger<CUDAKernelExecutor>>();
        _executor = new CUDAKernelExecutor(_mockAccelerator.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CUDAKernelExecutor(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CUDAKernelExecutor(_mockAccelerator.Object, null!));
    }

    [Fact]
    public void Constructor_WithNonCudaAccelerator_ShouldThrowArgumentException()
    {
        // Arrange
        var nonCudaAccelerator = CreateMockAcceleratorOfType("OpenCL");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            new CUDAKernelExecutor(nonCudaAccelerator.Object, _mockLogger.Object));
        Assert.Contains("Expected CUDA accelerator but received OpenCL", exception.Message);
    }

    [Fact]
    public void Accelerator_ShouldReturnProvidedAccelerator()
    {
        // Act
        var result = _executor.Accelerator;

        // Assert
        Assert.Equal(_mockAccelerator.Object, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullKernel_ShouldThrowArgumentNullException()
    {
        // Arrange
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => 
        {
            await _executor.ExecuteAsync(default(DotCompute.Abstractions.CompiledKernel), arguments.ToCoreKernelArguments(), config);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNullArguments_ShouldThrowArgumentNullException()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var config = CreateValidExecutionConfig();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _executor.ExecuteAsync(kernel, null!, config).AsTask());
    }

    [Fact]
    public async Task ExecuteAsync_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), null!).AsTask());
    }

    [Fact]
    public async Task ExecuteAsync_WithValidParameters_ShouldExecuteSuccessfully()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Handle);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAndWaitAsync_WithValidParameters_ShouldExecuteAndWait()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAndWaitAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Handle);
        Assert.True(result.Handle.IsCompleted);
        Assert.NotNull(result.Timings);
    }

    [Fact]
    public void EnqueueExecution_WithValidParameters_ShouldReturnHandle()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var handle = _executor.EnqueueExecution(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(handle);
        Assert.NotEqual(Guid.Empty, handle.Id);
        Assert.NotNull(handle.KernelName);
        Assert.True(handle.SubmittedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task WaitForCompletionAsync_WithValidHandle_ShouldComplete()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();
        var handle = _executor.EnqueueExecution(kernel, arguments.ToCoreKernelArguments(), config);

        // Act
        var result = await _executor.WaitForCompletionAsync(handle);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(handle, result.Handle);
        Assert.True(handle.IsCompleted);
        Assert.NotNull(handle.CompletedAt);
    }

    [Fact]
    public async Task WaitForCompletionAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();
        var handle = _executor.EnqueueExecution(kernel, arguments.ToCoreKernelArguments(), config);
        
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await _executor.WaitForCompletionAsync(handle, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage);
    }

    [Fact]
    public void GetOptimalExecutionConfig_WithValidKernel_ShouldReturnOptimalConfig()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var problemSize = new[] { 1024, 1024 };

        // Act
        var config = _executor.GetOptimalExecutionConfig(kernel, problemSize);

        // Assert
        Assert.NotNull(config);
        Assert.NotEmpty(config.GlobalWorkSize!);
        Assert.NotEmpty(config.LocalWorkSize!);
        Assert.True(config.GlobalWorkSize[0] >= problemSize[0]);
        Assert.True(config.LocalWorkSize[0] > 0);
        Assert.True(config.CaptureTimings);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    public void GetOptimalExecutionConfig_WithDifferentProblemSizes_ShouldAdaptConfiguration(int size)
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var problemSize = new[] { size };

        // Act
        var config = _executor.GetOptimalExecutionConfig(kernel, problemSize);

        // Assert
        Assert.NotNull(config);
        Assert.True(config.GlobalWorkSize![0] >= size);
        
        // Should use reasonable block sizes for CUDA (typically 256 or 512)
        Assert.Contains(config.LocalWorkSize![0], new[] { 256, 512 });
    }

    [Fact]
    public async Task ProfileAsync_WithValidParameters_ShouldReturnProfilingResults()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();
        var iterations = 10;

        // Act
        var result = await _executor.ProfileAsync(kernel, arguments.ToCoreKernelArguments(), config, iterations);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(iterations, result.Iterations);
        Assert.True(result.AverageTimeMs >= 0);
        Assert.True(result.MinTimeMs >= 0);
        Assert.True(result.MaxTimeMs >= result.MinTimeMs);
        Assert.True(result.AchievedOccupancy >= 0);
        Assert.True(result.MemoryThroughputGBps >= 0);
        Assert.True(result.ComputeThroughputGFLOPS >= 0);
        Assert.NotNull(result.Bottleneck);
        Assert.NotNull(result.OptimizationSuggestions);
        Assert.NotEmpty(result.PercentileTimingsMs);
    }

    [Fact]
    public async Task ProfileAsync_WithZeroIterations_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _executor.ProfileAsync(kernel, arguments.ToCoreKernelArguments(), config, 0).AsTask());
    }

    [Fact]
    public async Task ProfileAsync_WithNegativeIterations_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _executor.ProfileAsync(kernel, arguments.ToCoreKernelArguments(), config, -5).AsTask());
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeSharedMemory_ShouldHandleResourceAllocation()
    {
        // Arrange
        var kernel = CreateKernelWithSharedMemory();
        var arguments = CreateValidKernelArguments();
        var config = CreateExecutionConfigWithSharedMemory();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        // Should handle shared memory allocation gracefully
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentExecutions_ShouldHandleMultipleStreams()
    {
        // Arrange
        var kernel1 = CreateValidCompiledKernel("kernel1");
        var kernel2 = CreateValidCompiledKernel("kernel2");
        var kernel3 = CreateValidCompiledKernel("kernel3");
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act - Execute multiple kernels concurrently
        var task1 = _executor.ExecuteAsync(kernel1, arguments.ToCoreKernelArguments(), config);
        var task2 = _executor.ExecuteAsync(kernel2, arguments.ToCoreKernelArguments(), config);
        var task3 = _executor.ExecuteAsync(kernel3, arguments.ToCoreKernelArguments(), config);

        var results = await Task.WhenAll(task1.AsTask(), task2.AsTask(), task3.AsTask());

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.True(result.Success);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeKernel_ShouldHandleResourceLimits()
    {
        // Arrange
        var kernel = CreateLargeCompiledKernel();
        var arguments = CreateLargeKernelArguments();
        var config = CreateLargeExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        // Should either succeed or fail gracefully with resource constraints
        if (!result.Success)
        {
            Assert.NotNull(result.ErrorMessage);
            Assert.NotEmpty(result.ErrorMessage);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var executor = new CUDAKernelExecutor(_mockAccelerator.Object, _mockLogger.Object);
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        executor.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config).AsTask());
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(256, 256, 1)]
    [InlineData(16, 16, 4)]
    [InlineData(32, 32, 1)]
    public async Task ExecuteAsync_WithDifferentGridDimensions_ShouldExecuteCorrectly(int dimX, int dimY, int dimZ)
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { dimX * 256, dimY * 16, dimZ * 4 },
            LocalWorkSize = new[] { 256, 16, 4 },
            CaptureTimings = true
        };

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    #region Helper Methods

    private Mock<IAccelerator> CreateMockCudaAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "cuda_device_0",
            Name = "Mock CUDA Device",
            DeviceType = AcceleratorType.CUDA.ToString(),
            Vendor = "NVIDIA Corporation",
            DriverVersion = "12.0",
            TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
            AvailableMemory = 7L * 1024 * 1024 * 1024,
            LocalMemorySize = 48 * 1024, // 48KB shared memory
            MaxSharedMemoryPerBlock = 48 * 1024,
            MaxMemoryAllocationSize = 2L * 1024 * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 80,
            MaxThreadsPerBlock = 1024
        });

        mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return mock;
    }

    private Mock<IAccelerator> CreateMockAcceleratorOfType(string deviceType)
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "device_0",
            Name = "Mock Device",
            DeviceType = deviceType,
            Vendor = "Mock Vendor",
            DriverVersion = "1.0",
            TotalMemory = 1L * 1024 * 1024 * 1024,
            AvailableMemory = 800L * 1024 * 1024,
            LocalMemorySize = 16 * 1024,
            MaxSharedMemoryPerBlock = 16 * 1024,
            MaxMemoryAllocationSize = 512L * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 16,
            MaxThreadsPerBlock = 256
        });

        return mock;
    }

    private CompiledKernel CreateValidCompiledKernel(string name = "test_kernel")
    {
        return new CompiledKernel
        {
            Id = Guid.NewGuid(),
            KernelId = Guid.NewGuid(),
            Name = name,
            EntryPoint = name,
            NativeHandle = new IntPtr(0x12345678), // Mock handle
            IsCompiled = true,
            Language = KernelLanguage.CUDA,
            Metadata = new Dictionary<string, string>
            {
                ["compute_capability"] = "75",
                ["registers_per_thread"] = "32",
                ["shared_memory_bytes"] = "0",
                ["max_threads_per_block"] = "1024"
            },
            Configuration = new KernelConfiguration
            {
                BlockDimensions = new Dimensions3D(256, 1, 1),
                SharedMemorySize = 0
            },
            SharedMemorySize = 0,
            CompilationTimestamp = DateTimeOffset.UtcNow
        };
    }

    private CompiledKernel CreateKernelWithSharedMemory()
    {
        var kernel = CreateValidCompiledKernel("shared_memory_kernel");
        kernel.SharedMemorySize = 4096; // 4KB shared memory
        kernel.Metadata["shared_memory_bytes"] = "4096";
        kernel.Configuration.SharedMemorySize = 4096;
        return kernel;
    }

    private CompiledKernel CreateLargeCompiledKernel()
    {
        var kernel = CreateValidCompiledKernel("large_kernel");
        kernel.SharedMemorySize = 32768; // 32KB shared memory
        kernel.Metadata["registers_per_thread"] = "128"; // High register usage
        kernel.Configuration.BlockDimensions = new Dimensions3D(1024, 1, 1);
        return kernel;
    }

    private KernelArgument[] CreateValidKernelArguments()
    {
        return new[]
        {
            new KernelArgument
            {
                Name = "input",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float)
            },
            new KernelArgument
            {
                Name = "output",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float)
            },
            new KernelArgument
            {
                Name = "size",
                Value = 1024,
                Type = typeof(int),
                IsDeviceMemory = false,
                SizeInBytes = sizeof(int)
            }
        };
    }

    private KernelArgument[] CreateLargeKernelArguments()
    {
        return new[]
        {
            new KernelArgument
            {
                Name = "input",
                Value = new float[1024 * 1024], // 1M elements
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * 1024 * sizeof(float)
            },
            new KernelArgument
            {
                Name = "output",
                Value = new float[1024 * 1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * 1024 * sizeof(float)
            },
            new KernelArgument
            {
                Name = "size",
                Value = 1024 * 1024,
                Type = typeof(int),
                IsDeviceMemory = false,
                SizeInBytes = sizeof(int)
            }
        };
    }

    private KernelExecutionConfig CreateValidExecutionConfig()
    {
        return new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 },
            LocalWorkSize = new[] { 256 },
            CaptureTimings = true,
            DynamicSharedMemorySize = 0,
            Flags = KernelExecutionFlags.None
        };
    }

    private KernelExecutionConfig CreateExecutionConfigWithSharedMemory()
    {
        return new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 },
            LocalWorkSize = new[] { 256 },
            CaptureTimings = true,
            DynamicSharedMemorySize = 2048, // 2KB dynamic shared memory
            Flags = KernelExecutionFlags.PreferSharedMemory
        };
    }

    private KernelExecutionConfig CreateLargeExecutionConfig()
    {
        return new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 * 1024 }, // 1M threads
            LocalWorkSize = new[] { 512 },
            CaptureTimings = true,
            DynamicSharedMemorySize = 16384, // 16KB dynamic shared memory
            Flags = KernelExecutionFlags.PreferSharedMemory | KernelExecutionFlags.OptimizeForThroughput
        };
    }

    #endregion

    public void Dispose()
    {
        _executor?.Dispose();
        GC.SuppressFinalize(this);
    }
}