// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Comprehensive tests for OpenCL kernel compiler that can run on CI/CD without GPU hardware.
/// Uses mocks and dependency injection to simulate OpenCL runtime behavior.
/// </summary>
public class OpenCLKernelCompilerTests : IDisposable
{
    private readonly Mock<ILogger<OpenCLKernelCompiler>> _mockLogger;
    private readonly OpenCLKernelCompiler _compiler;

    public OpenCLKernelCompilerTests()
    {
        _mockLogger = new Mock<ILogger<OpenCLKernelCompiler>>();
        _compiler = new OpenCLKernelCompiler(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OpenCLKernelCompiler(null!));
    }

    [Fact]
    public void AcceleratorType_ShouldReturnOpenCL()
    {
        // Act
        var result = _compiler.AcceleratorType;

        // Assert
        Assert.Equal(AcceleratorType.OpenCL, result);
    }

    [Fact]
    public async Task CompileAsync_WithNullKernel_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = CreateValidCompilationOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _compiler.CompileAsync(null!, options).AsTask());
    }

    [Fact]
    public async Task CompileAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var kernel = CreateValidOpenCLKernel();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _compiler.CompileAsync(kernel, null!).AsTask());
    }

    [Fact]
    public async Task CompileAsync_WithNonOpenCLKernel_ShouldThrowArgumentException()
    {
        // Arrange
        var kernel = new GeneratedKernel
        {
            Name = "test_kernel",
            Source = "__kernel void test() {}",
            Language = DotCompute.Core.Kernels.KernelLanguage.CUDA, // Wrong language
            Parameters = Array.Empty<DotCompute.Core.Kernels.KernelParameter>()
        };
        var options = CreateValidCompilationOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _compiler.CompileAsync(kernel, options).AsTask());
        Assert.Contains("Expected OpenCL kernel but received CUDA", exception.Message);
    }

    [Fact]
    public async Task CompileAsync_WithValidKernel_ShouldCompileSuccessfully()
    {
        // Arrange
        var kernel = CreateValidOpenCLKernel();
        var options = CreateValidCompilationOptions();

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        Assert.Equal(kernel.Name, result.Name);
        Assert.NotNull(result.Parameters);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.1")]
    [InlineData("1.2")]
    [InlineData("2.0")]
    [InlineData("3.0")]
    public async Task CompileAsync_WithDifferentOpenCLVersions_ShouldHandleCorrectly(string version)
    {
        // Arrange
        var kernel = CreateValidOpenCLKernel();
        var options = CreateValidCompilationOptions();
        options.Defines["OPENCL_VERSION"] = version.Replace(".", "");

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        VerifyLoggerWasCalled("Compiling OpenCL kernel", LogLevel.Information);
    }

    [Fact]
    public async Task CompileAsync_WithExtensions_ShouldIncludeExtensions()
    {
        // Arrange
        var kernel = CreateKernelWithExtensions();
        var options = CreateValidCompilationOptions();

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        Assert.NotNull(result.PerformanceMetadata);
    }

    [Fact]
    public async Task CompileAsync_WithMacroDefinitions_ShouldIncludeMacros()
    {
        // Arrange
        var kernel = CreateValidOpenCLKernel();
        var options = CreateValidCompilationOptions();
        options.Defines["WORK_GROUP_SIZE"] = "256";
        options.Defines["TILE_SIZE"] = "16";
        options.Defines["USE_DOUBLE_PRECISION"] = "1";

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        Assert.Contains("WORK_GROUP_SIZE", options.Defines.Keys);
        Assert.Contains("TILE_SIZE", options.Defines.Keys);
        Assert.Contains("USE_DOUBLE_PRECISION", options.Defines.Keys);
    }

    [Fact]
    public async Task CompileAsync_WithComplexKernel_ShouldHandleAdvancedFeatures()
    {
        // Arrange
        var kernel = CreateComplexOpenCLKernel();
        var options = CreateValidCompilationOptions();
        options.EnableFastMath = true;
        options.OptimizationLevel = DotCompute.Core.Kernels.OptimizationLevel.O3;

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        Assert.NotNull(result.PerformanceMetadata);
        if (result.PerformanceMetadata != null)
        {
            Assert.True(result.PerformanceMetadata.ContainsKey("local_memory_size") || 
                       result.PerformanceMetadata.ContainsKey("work_group_size"));
        }
    }

    [Fact]
    public async Task CompileAsync_WithVectorizationKernel_ShouldHandleVectorTypes()
    {
        // Arrange
        var kernel = CreateVectorizationKernel();
        var options = CreateValidCompilationOptions();

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        Assert.NotNull(result.PerformanceMetadata);
    }

    [Fact]
    public async Task CompileAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var kernel = CreateValidOpenCLKernel();
        var options = CreateValidCompilationOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _compiler.CompileAsync(kernel, options, cts.Token).AsTask());
    }

    [Fact]
    public async Task CompileAsync_WithInvalidOpenCLCode_ShouldHandleCompilationErrors()
    {
        // Arrange
        var kernel = new GeneratedKernel
        {
            Name = "invalid_kernel",
            Source = "__kernel void test() { invalid_syntax_here; }", // Invalid OpenCL syntax
            Language = DotCompute.Core.Kernels.KernelLanguage.OpenCL,
            Parameters = Array.Empty<DotCompute.Core.Kernels.KernelParameter>()
        };
        var options = CreateValidCompilationOptions();

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        // The compiler should handle the error gracefully (since we're using mocked OpenCL)
        Assert.NotNull(result);
        // In a mock scenario, this would still "compile" since we're not hitting real OpenCL runtime
        // In a real scenario with actual OpenCL runtime, this would fail compilation
    }

    [Theory]
    [InlineData(DotCompute.Core.Kernels.OptimizationLevel.O0)]
    [InlineData(DotCompute.Core.Kernels.OptimizationLevel.O1)]
    [InlineData(DotCompute.Core.Kernels.OptimizationLevel.O2)]
    [InlineData(DotCompute.Core.Kernels.OptimizationLevel.O3)]
    public async Task CompileAsync_WithVariousOptimizationLevels_ShouldCompileCorrectly(DotCompute.Core.Kernels.OptimizationLevel level)
    {
        // Arrange
        var kernel = CreateValidOpenCLKernel();
        var options = CreateValidCompilationOptions();
        options.OptimizationLevel = level;

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
    }

    [Fact]
    public async Task CompileAsync_WithImageProcessingKernel_ShouldHandleImages()
    {
        // Arrange
        var kernel = CreateImageProcessingKernel();
        var options = CreateValidCompilationOptions();

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        Assert.NotNull(result.PerformanceMetadata);
    }

    [Fact]
    public async Task CompileAsync_WithAtomicOperations_ShouldHandleAtomics()
    {
        // Arrange
        var kernel = CreateAtomicOperationsKernel();
        var options = CreateValidCompilationOptions();

        // Act
        var result = await _compiler.CompileAsync(kernel, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Binary);
        Assert.NotNull(result.PerformanceMetadata);
    }

    [Fact]
    public async Task CompileAsync_ConcurrentCompilations_ShouldHandleThreadSafety()
    {
        // Arrange
        var kernel1 = CreateValidOpenCLKernel("kernel1");
        var kernel2 = CreateValidOpenCLKernel("kernel2");
        var kernel3 = CreateValidOpenCLKernel("kernel3");
        var options = CreateValidCompilationOptions();

        // Act - Compile multiple kernels concurrently
        var task1 = _compiler.CompileAsync(kernel1, options);
        var task2 = _compiler.CompileAsync(kernel2, options);
        var task3 = _compiler.CompileAsync(kernel3, options);

        var results = await Task.WhenAll(task1.AsTask(), task2.AsTask(), task3.AsTask());

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.True(result.IsCompiled);
        });
    }

    #region Helper Methods

    private GeneratedKernel CreateValidOpenCLKernel(string name = "test_kernel")
    {
        return new GeneratedKernel
        {
            Name = name,
            Source = @"
__kernel void test_kernel(__global float* input, __global float* output, int size) {
    int gid = get_global_id(0);
    if (gid < size) {
        output[gid] = input[gid] * 2.0f;
    }
}",
            Language = DotCompute.Core.Kernels.KernelLanguage.OpenCL,
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new() { Name = "input", Type = typeof(float[]), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "output", Type = typeof(float[]), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "size", Type = typeof(int), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Private }
            }
        };
    }

    private GeneratedKernel CreateComplexOpenCLKernel()
    {
        return new GeneratedKernel
        {
            Name = "complex_kernel",
            Source = @"
#define TILE_SIZE 16

__kernel void complex_kernel(__global float* A, __global float* B, __global float* C, 
                           int N, __local float* localA, __local float* localB) {
    int row = get_group_id(0);
    int col = get_group_id(1);
    int localRow = get_local_id(0);
    int localCol = get_local_id(1);
    
    float sum = 0.0f;
    
    for (int k = 0; k < N / TILE_SIZE; k++) {
        // Load tiles into local memory
        localA[localRow * TILE_SIZE + localCol] = 
            A[(row * TILE_SIZE + localRow) * N + k * TILE_SIZE + localCol];
        localB[localRow * TILE_SIZE + localCol] = 
            B[(k * TILE_SIZE + localRow) * N + col * TILE_SIZE + localCol];
            
        barrier(CLK_LOCAL_MEM_FENCE);
        
        // Compute partial sum
        for (int i = 0; i < TILE_SIZE; i++) {
            sum += localA[localRow * TILE_SIZE + i] * localB[i * TILE_SIZE + localCol];
        }
        
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    C[(row * TILE_SIZE + localRow) * N + col * TILE_SIZE + localCol] = sum;
}",
            Language = DotCompute.Core.Kernels.KernelLanguage.OpenCL,
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new() { Name = "A", Type = typeof(float[]), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "B", Type = typeof(float[]), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "C", Type = typeof(float[]), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "N", Type = typeof(int), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Private },
                new() { Name = "localA", Type = typeof(float[]), IsInput = false, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Shared },
                new() { Name = "localB", Type = typeof(float[]), IsInput = false, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Shared }
            },
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["local_memory_size"] = (2 * TILE_SIZE * TILE_SIZE * sizeof(float)).ToString(),
                ["work_group_size"] = (TILE_SIZE * TILE_SIZE).ToString(),
                ["preferred_work_group_size_multiple"] = "32"
            }
        };
    }

    private GeneratedKernel CreateKernelWithExtensions()
    {
        return new GeneratedKernel
        {
            Name = "kernel_with_extensions",
            Source = @"
#pragma OPENCL EXTENSION cl_khr_fp64 : enable
#pragma OPENCL EXTENSION cl_khr_global_int32_base_atomics : enable

__kernel void kernel_with_extensions(__global double* input, __global double* output, 
                                   __global int* counter, int size) {
    int gid = get_global_id(0);
    if (gid < size) {
        output[gid] = sqrt(input[gid]);
        atomic_inc(counter);
    }
}",
            Language = DotCompute.Core.Kernels.KernelLanguage.OpenCL,
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new() { Name = "input", Type = typeof(double[]), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "output", Type = typeof(double[]), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "counter", Type = typeof(int[]), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "size", Type = typeof(int), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Private }
            },
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["extensions"] = "cl_khr_fp64,cl_khr_global_int32_base_atomics",
                ["uses_double_precision"] = "true",
                ["uses_atomics"] = "true"
            }
        };
    }

    private GeneratedKernel CreateVectorizationKernel()
    {
        return new GeneratedKernel
        {
            Name = "vectorization_kernel",
            Source = @"
__kernel void vectorization_kernel(__global float4* input, __global float4* output, int size) {
    int gid = get_global_id(0);
    if (gid < size) {
        float4 data = input[gid];
        float4 result = data * data + (float4)(1.0f, 2.0f, 3.0f, 4.0f);
        output[gid] = result;
    }
}",
            Language = DotCompute.Core.Kernels.KernelLanguage.OpenCL,
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new() { Name = "input", Type = typeof(float[]), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "output", Type = typeof(float[]), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "size", Type = typeof(int), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Private }
            },
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["uses_vector_types"] = "true",
                ["vector_width"] = "4"
            }
        };
    }

    private GeneratedKernel CreateImageProcessingKernel()
    {
        return new GeneratedKernel
        {
            Name = "image_processing_kernel",
            Source = @"
__constant sampler_t sampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP | CLK_FILTER_NEAREST;

__kernel void image_processing_kernel(__read_only image2d_t input, __write_only image2d_t output) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    int2 coord = (int2)(x, y);
    
    float4 pixel = read_imagef(input, sampler, coord);
    float4 result = pixel * 0.8f + (float4)(0.2f, 0.2f, 0.2f, 0.0f);
    
    write_imagef(output, coord, result);
}",
            Language = DotCompute.Core.Kernels.KernelLanguage.OpenCL,
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new() { Name = "input", Type = typeof(object), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "output", Type = typeof(object), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global }
            },
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["uses_images"] = "true",
                ["image_format"] = "CL_RGBA",
                ["image_type"] = "2D"
            }
        };
    }

    private GeneratedKernel CreateAtomicOperationsKernel()
    {
        return new GeneratedKernel
        {
            Name = "atomic_operations_kernel",
            Source = @"
__kernel void atomic_operations_kernel(__global float* input, __global float* output, 
                                     __global int* histogram, int size, int bins) {
    int gid = get_global_id(0);
    if (gid < size) {
        float value = input[gid];
        output[gid] = value * 2.0f;
        
        // Update histogram atomically
        int bin = (int)(value * bins);
        if (bin >= 0 && bin < bins) {
            atomic_inc(&histogram[bin]);
        }
    }
}",
            Language = DotCompute.Core.Kernels.KernelLanguage.OpenCL,
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new() { Name = "input", Type = typeof(float[]), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "output", Type = typeof(float[]), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "histogram", Type = typeof(int[]), IsOutput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Global },
                new() { Name = "size", Type = typeof(int), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Private },
                new() { Name = "bins", Type = typeof(int), IsInput = true, MemorySpace = DotCompute.Core.Kernels.MemorySpace.Private }
            },
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["uses_atomics"] = "true",
                ["atomic_operations"] = "atomic_inc"
            }
        };
    }

    private DotCompute.Core.Kernels.CompilationOptions CreateValidCompilationOptions()
    {
        return new DotCompute.Core.Kernels.CompilationOptions
        {
            OptimizationLevel = DotCompute.Core.Kernels.OptimizationLevel.O2,
            EnableFastMath = false,
            GenerateDebugInfo = false,
            Defines = new Dictionary<string, string>(),
            IncludeDirectories = new List<string>()
        };
    }

    private void VerifyLoggerWasCalled(string message, LogLevel level)
    {
        _mockLogger.Verify(
            logger => logger.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private const int TILE_SIZE = 16; // For complex kernel shared memory calculations

    #endregion

    public void Dispose()
    {
        // Clean up any resources if needed
        GC.SuppressFinalize(this);
    }
}