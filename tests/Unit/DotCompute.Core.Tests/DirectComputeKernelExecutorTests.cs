// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using DotCompute.Core.Types;
using DotCompute.Tests.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using CompiledKernel = DotCompute.Tests.Common.CompiledKernel;
using KernelArgument = DotCompute.Tests.Common.KernelArgument;
using KernelConfiguration = DotCompute.Tests.Common.KernelConfiguration;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Comprehensive tests for DirectCompute kernel executor that can run on CI/CD without GPU hardware.
/// Uses mocks and dependency injection to simulate DirectX 11 compute shader behavior.
/// </summary>
public class DirectComputeKernelExecutorTests : IDisposable
{
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<ILogger<DirectComputeKernelExecutor>> _mockLogger;
    private readonly DirectComputeKernelExecutor _executor;

    public DirectComputeKernelExecutorTests()
    {
        _mockAccelerator = CreateMockDirectComputeAccelerator();
        _mockLogger = new Mock<ILogger<DirectComputeKernelExecutor>>();
        _executor = new DirectComputeKernelExecutor(_mockAccelerator.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DirectComputeKernelExecutor(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DirectComputeKernelExecutor(_mockAccelerator.Object, null!));
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
        // On platforms without DirectCompute support, this should still work as a stub
        Assert.NotNull(result.Handle);
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
        Assert.NotNull(result.Handle);
        Assert.True(result.Handle.IsCompleted);
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
        Assert.Contains("DirectCompute", handle.KernelName);
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
        Assert.Equal(handle, result.Handle);
        Assert.True(handle.IsCompleted);
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
        // DirectCompute typically uses higher thread counts
        Assert.Contains(config.LocalWorkSize![0], 256 }); // new[] { 64;
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
        // DirectCompute should prefer larger thread groups for efficiency
        Assert.True(config.LocalWorkSize![0] >= 64);
    }

    [Fact]
    public async Task ExecuteAsync_WithComputeShaderKernel_ShouldHandleDirectXFeatures()
    {
        // Arrange
        var kernel = CreateComputeShaderKernel();
        var arguments = CreateStructuredBufferArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Handle);
        // Should handle DirectX-specific features gracefully
    }

    [Fact]
    public async Task ExecuteAsync_WithGroupSharedMemory_ShouldHandleSharedMemory()
    {
        // Arrange
        var kernel = CreateKernelWithGroupSharedMemory();
        var arguments = CreateValidKernelArguments();
        var config = CreateExecutionConfigWithSharedMemory();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Handle);
        // Should handle group shared memory allocation
    }

    [Fact]
    public async Task ExecuteAsync_WithUnorderedAccessViews_ShouldHandleUAVs()
    {
        // Arrange
        var kernel = CreateKernelWithUAVs();
        var arguments = CreateUAVArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Handle);
        // Should handle UAV creation and binding
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
        Assert.NotNull(result.Bottleneck);
        Assert.NotNull(result.OptimizationSuggestions);
        Assert.NotEmpty(result.PercentileTimingsMs);
        
        // DirectCompute-specific suggestions should be included
        Assert.Contains(result.OptimizationSuggestions, 
            s => s.Contains("shared memory") || s.Contains("thread group") || s.Contains("coalescing"));
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
    public async Task ExecuteAsync_ConcurrentExecutions_ShouldHandleMultipleDispatches()
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
            Assert.NotNull(result.Handle);
        });
    }

    [Fact]
    public async Task ExecuteAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var executor = new DirectComputeKernelExecutor(_mockAccelerator.Object, _mockLogger.Object);
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
    [InlineData(16, 16, 1)]
    [InlineData(8, 8, 8)]
    [InlineData(32, 32, 1)]
    public async Task ExecuteAsync_WithDifferentThreadGroupDimensions_ShouldExecuteCorrectly(int dimX, int dimY, int dimZ)
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { dimX * 16, dimY * 16, dimZ * 4 },
            LocalWorkSize = new[] { dimX, dimY, dimZ },
            CaptureTimings = true
        };

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Handle);
    }

    [Fact]
    public async Task ExecuteAsync_OnNonWindowsPlatform_ShouldHandleGracefully()
    {
        // Arrange
        var kernel = CreateValidCompiledKernel();
        var arguments = CreateValidKernelArguments();
        var config = CreateValidExecutionConfig();

        // Act
        var result = await _executor.ExecuteAsync(kernel, arguments.ToCoreKernelArguments(), config);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Handle);
        // On non-Windows platforms or when DirectCompute is not available,
        // the executor should still work as a stub implementation
    }

    [Fact]
    public void Constructor_ShouldLogInitializationStatus()
    {
        // Act - Constructor already called in setup
        
        // Assert - Verify appropriate logging occurred
        _mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DirectCompute")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #region Helper Methods

    private Mock<IAccelerator> CreateMockDirectComputeAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "directcompute_device_0",
            Name = "Mock DirectCompute Device",
            DeviceType = "DirectCompute",
            Vendor = "Mock GPU Vendor",
            DriverVersion = "DirectX 11.4",
            TotalMemory = 6L * 1024 * 1024 * 1024, // 6GB VRAM
            AvailableMemory = 5L * 1024 * 1024 * 1024,
            LocalMemorySize = 32 * 1024, // 32KB group shared memory
            MaxSharedMemoryPerBlock = 32 * 1024,
            MaxMemoryAllocationSize = 1L * 1024 * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 2048,
            MaxThreadsPerBlock = 1024
        });

        mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()
            .Returns(ValueTask.CompletedTask);

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
            Language = DotCompute.Tests.Common.KernelLanguage.HLSL, // DirectCompute uses HLSL
            Metadata = new Dictionary<string, string>
            {
                ["shader_model"] = "5.0",
                ["thread_group_size"] = "256,1,1",
                ["shared_memory_size"] = "0",
                ["num_threads"] = "256"
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

    private CompiledKernel CreateComputeShaderKernel()
    {
        var kernel = CreateValidCompiledKernel("compute_shader_kernel");
        kernel.Metadata["uses_structured_buffers"] = "true";
        kernel.Metadata["uses_uavs"] = "true";
        kernel.Metadata["shader_profile"] = "cs_5_0";
        return kernel;
    }

    private CompiledKernel CreateKernelWithGroupSharedMemory()
    {
        var kernel = CreateValidCompiledKernel("group_shared_kernel");
        kernel.SharedMemorySize = 4096; // 4KB group shared memory
        kernel.Metadata["shared_memory_size"] = "4096";
        kernel.Metadata["uses_group_shared"] = "true";
        kernel.Configuration!.SharedMemorySize = 4096;
        return kernel;
    }

    private CompiledKernel CreateKernelWithUAVs()
    {
        var kernel = CreateValidCompiledKernel("uav_kernel");
        kernel.Metadata["uses_uavs"] = "true";
        kernel.Metadata["uav_count"] = "4";
        kernel.Metadata["buffer_types"] = "structured,raw";
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
                SizeInBytes = 1024 * sizeof(float),
                ArgumentType = KernelArgumentType.Buffer
            },
            new KernelArgument
            {
                Name = "output",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float),
                ArgumentType = KernelArgumentType.Buffer
            },
            new KernelArgument
            {
                Name = "size",
                Value = 1024,
                Type = typeof(int),
                IsDeviceMemory = false,
                SizeInBytes = sizeof(int),
                ArgumentType = KernelArgumentType.Scalar
            }
        };
    }

    private KernelArgument[] CreateStructuredBufferArguments()
    {
        return new[]
        {
            new KernelArgument
            {
                Name = "structured_buffer",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float),
                ArgumentType = KernelArgumentType.StructuredBuffer
            },
            new KernelArgument
            {
                Name = "output_buffer",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float),
                ArgumentType = KernelArgumentType.UnorderedAccessView
            }
        };
    }

    private KernelArgument[] CreateUAVArguments()
    {
        return new[]
        {
            new KernelArgument
            {
                Name = "input_uav",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float),
                ArgumentType = KernelArgumentType.UnorderedAccessView
            },
            new KernelArgument
            {
                Name = "output_uav",
                Value = new float[1024],
                Type = typeof(float[]),
                IsDeviceMemory = true,
                SizeInBytes = 1024 * sizeof(float),
                ArgumentType = KernelArgumentType.UnorderedAccessView
            },
            new KernelArgument
            {
                Name = "counter_uav",
                Value = new int[1],
                Type = typeof(int[]),
                IsDeviceMemory = true,
                SizeInBytes = sizeof(int),
                ArgumentType = KernelArgumentType.UnorderedAccessView
            }
        };
    }

    private KernelExecutionConfig CreateValidExecutionConfig()
    {
        return new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { 1024 },
            LocalWorkSize = new[] { 256 }, // DirectCompute thread group size
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
            DynamicSharedMemorySize = 4096, // 4KB group shared memory
            Flags = KernelExecutionFlags.PreferSharedMemory
        };
    }

    #endregion

    public void Dispose()
    {
        _executor?.Dispose();
        GC.SuppressFinalize(this);
    }
}
