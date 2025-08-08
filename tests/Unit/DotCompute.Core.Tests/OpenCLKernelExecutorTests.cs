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
using CompiledKernel = DotCompute.Tests.Common.CompiledKernel;
using KernelArgument = DotCompute.Tests.Common.KernelArgument;
using KernelConfiguration = DotCompute.Tests.Common.KernelConfiguration;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive tests for OpenCL kernel executor that can run on CI/CD without GPU hardware.
/// Uses mocks and dependency injection to simulate OpenCL runtime behavior.
/// </summary>
public class OpenCLKernelExecutorTests : IDisposable
{
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<ILogger<OpenCLKernelExecutor>> _mockLogger;
    private readonly OpenCLKernelExecutor _executor;

    public OpenCLKernelExecutorTests()
    {
        _mockAccelerator = CreateMockOpenCLAccelerator();
        _mockLogger = new Mock<ILogger<OpenCLKernelExecutor>>();
        _executor = new OpenCLKernelExecutor(_mockAccelerator.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OpenCLKernelExecutor(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OpenCLKernelExecutor(_mockAccelerator.Object, null!));
    }

    [Fact]
    public void Constructor_WithNonOpenCLAccelerator_ShouldThrowArgumentException()
    {
        // Arrange
        var nonOpenCLAccelerator = CreateMockAcceleratorOfType("CUDA");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            new OpenCLKernelExecutor(nonOpenCLAccelerator.Object, _mockLogger.Object));
        Assert.Contains("Expected OpenCL accelerator but received CUDA", exception.Message);
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
            () => _executor.ExecuteAsync(kernel, arguments: null!, config).AsTask());
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
        Assert.True(config.GlobalWorkSize![0] >= problemSize[0]);
        Assert.True(config.LocalWorkSize![0] > 0);
        Assert.True(config.CaptureTimings);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void GetOptimalExecutionConfig_WithDifferentWorkGroupSizes_ShouldAdaptConfiguration(int workGroupSize)
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        kernel.Metadata["preferred_work_group_size"] = workGroupSize.ToString();
        var problemSize = new[] { workGroupSize * 16 };

        // Act
        var config = _executor.GetOptimalExecutionConfig(kernel, problemSize);

        // Assert
        Assert.NotNull(config);
        Assert.True(config.GlobalWorkSize![0] >= problemSize[0]);
        // Local work size should be reasonable for OpenCL (typically power of 2)
        Assert.True(config.LocalWorkSize![0] <= workGroupSize);
        Assert.True((config.LocalWorkSize[0] & (config.LocalWorkSize[0] - 1)) == 0 || config.LocalWorkSize[0] == 1);
    }

    [Fact]
    public async Task ExecuteAsync_WithLocalMemoryKernel_ShouldHandleLocalMemory()
    {
        // Arrange
        var kernel = CreateKernelWithLocalMemory();
        var arguments = CreateValidKernelArguments();
        var config = CreateExecutionConfigWithLocalMemory();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        // Should handle local memory allocation gracefully
    }

    [Fact]
    public async Task ExecuteAsync_WithImageKernel_ShouldHandleImageArguments()
    {
        // Arrange
        var kernel = CreateImageProcessingKernel();
        var arguments = CreateImageKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_WithVectorKernel_ShouldHandleVectorizedExecution()
    {
        // Arrange
        var kernel = CreateVectorizedKernel();
        var arguments = CreateVectorKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
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
    public async Task ExecuteAsync_ConcurrentExecutions_ShouldHandleMultipleCommandQueues()
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
    public async Task ExecuteAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var executor = new OpenCLKernelExecutor(_mockAccelerator.Object, _mockLogger.Object);
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        executor.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config).AsTask());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ExecuteAsync_WithMultiDimensionalExecution_ShouldExecuteCorrectly(int dimensions)
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateMultiDimensionalConfig(dimensions);

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(dimensions, config.GlobalWorkSize.Length);
    }

    [Fact]
    public async Task ExecuteAsync_WithAtomicOperations_ShouldHandleAtomics()
    {
        // Arrange
        var kernel = CreateAtomicKernel();
        var arguments = CreateAtomicKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    #region Helper Methods

    private Mock<IAccelerator> CreateMockOpenCLAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "opencl_device_0",
            Name = "Mock OpenCL Device",
            DeviceType = AcceleratorType.OpenCL.ToString(),
            Vendor = "Mock Vendor",
            DriverVersion = "3.0",
            TotalMemory = 4L * 1024 * 1024 * 1024, // 4GB
            AvailableMemory = 3L * 1024 * 1024 * 1024,
            LocalMemorySize = 32 * 1024, // 32KB local memory
            MaxSharedMemoryPerBlock = 32 * 1024,
            MaxMemoryAllocationSize = 1L * 1024 * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 32,
            MaxThreadsPerBlock = 256
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
            Language = DotCompute.Tests.Common.KernelLanguage.OpenCL,
            Metadata = new Dictionary<string, string>
            {
                ["preferred_work_group_size"] = "256",
                ["local_memory_size"] = "0",
                ["work_group_size"] = "256,1,1",
                ["uses_local_memory"] = "false"
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

    private CompiledKernel CreateKernelWithLocalMemory()
    {
        var kernel = CreateValidCompiledKernel("local_memory_kernel");
        kernel.Metadata["uses_local_memory"] = "true";
        kernel.Metadata["local_memory_size"] = "2048";
        kernel.Configuration!.SharedMemorySize = 2048;
        return kernel;
    }

    private CompiledKernel CreateImageProcessingKernel()
    {
        var kernel = CreateValidCompiledKernel("image_kernel");
        kernel.Metadata["uses_images"] = "true";
        kernel.Metadata["image_format"] = "CL_RGBA";
        return kernel;
    }

    private CompiledKernel CreateVectorizedKernel()
    {
        var kernel = CreateValidCompiledKernel("vector_kernel");
        kernel.Metadata["uses_vector_types"] = "true";
        kernel.Metadata["vector_width"] = "4";
        return kernel;
    }

    private CompiledKernel CreateAtomicKernel()
    {
        var kernel = CreateValidCompiledKernel("atomic_kernel");
        kernel.Metadata["uses_atomics"] = "true";
        kernel.Metadata["atomic_operations"] = "atomic_inc,atomic_add";
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

    private KernelArgument[] CreateImageKernelArguments()
    {
        return new[]
        {
            new KernelArgument
            {
                Name = "input_image",
                Value = new byte[512 * 512 * 4], // RGBA image
                Type = typeof(byte[]),
                IsDeviceMemory = true,
                SizeInBytes = 512 * 512 * 4,
                ArgumentType = KernelArgumentType.Image2D
            },
            new KernelArgument
            {
                Name = "output_image",
                Value = new byte[512 * 512 * 4],
                Type = typeof(byte[]),
                IsDeviceMemory = true,
                SizeInBytes = 512 * 512 * 4,
                ArgumentType = KernelArgumentType.Image2D
            }
        };
    }

    private KernelArgument[] CreateVectorKernelArguments()
    {
        return new[]
        {
            new KernelArgument
            {
                Name = "input_vectors",
                Value = new float[1024 * 4], // float4 vectors
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * 4 * sizeof(float)
            },
            new KernelArgument
            {
                Name = "output_vectors",
                Value = new float[1024 * 4],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * 4 * sizeof(float)
            }
        };
    }

    private KernelArgument[] CreateAtomicKernelArguments()
    {
        return new[]
        {
            new KernelArgument
            {
                Name = "data",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float)
            },
            new KernelArgument
            {
                Name = "counter",
                Value = new int[1],
                Type = typeof(int[]),
                IsDeviceMemory = true,
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

    private KernelExecutionConfig CreateExecutionConfigWithLocalMemory()
    {
        return new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 },
            LocalWorkSize = new[] { 256 },
            CaptureTimings = true,
            DynamicSharedMemorySize = 2048, // 2KB local memory
            Flags = KernelExecutionFlags.PreferSharedMemory
        };
    }

    private KernelExecutionConfig CreateMultiDimensionalConfig(int dimensions)
    {
        switch (dimensions)
        {
            case 1:
                return new KernelExecutionConfig
                {
                    GlobalWorkSize = new[] { 1024 },
                    LocalWorkSize = new[] { 256 },
                    CaptureTimings = true
                };
            case 2:
                return new KernelExecutionConfig
                {
                    GlobalWorkSize = new[] { 1024, 1024 },
                    LocalWorkSize = new[] { 16, 16 },
                    CaptureTimings = true
                };
            case 3:
                return new KernelExecutionConfig
                {
                    GlobalWorkSize = new[] { 256, 256, 4 },
                    LocalWorkSize = new[] { 8, 8, 4 },
                    CaptureTimings = true
                };
            default:
                throw new ArgumentException($"Unsupported dimensions: {dimensions}");
        }
    }

    #endregion

    public void Dispose()
    {
        _executor?.Dispose();
        GC.SuppressFinalize(this);
    }
}