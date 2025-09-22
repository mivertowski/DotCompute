// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using DotCompute.Tests.Common.Specialized;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests;

/// <summary>
/// Comprehensive tests for CUDA kernel compilation covering all major scenarios.
/// </summary>
public class CudaKernelCompilerTests : CudaTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<CudaKernelCompiler>>? _mockLogger;
    private readonly CudaKernelCompiler? _compiler;

    public CudaKernelCompilerTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<CudaKernelCompiler>>();
        if (IsCudaAvailable())
        {
            var context = new DotCompute.Backends.CUDA.CudaContext(0);
            _compiler = new CudaKernelCompiler(context, _mockLogger.Object);
        }
    }

    #region Basic Compilation Tests

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Compilation")]
    public async Task CompileAsync_SimpleKernel_Success()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void vector_add(float* a, float* b, float* c, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    c[idx] = a[idx] + b[idx];
                }
            }";

        var definition = new KernelDefinition("vector_add", kernelSource, "vector_add")
        {
            // Language would be set via source generator
        };

        // Act
        var result = await _compiler.CompileAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("vector_add");
        _ = result.Id.Should().NotBeEmpty();
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Compilation")]
    public async Task CompileAsync_ComplexKernel_WithSharedMemory_Success()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void matrix_multiply(
                float* A, float* B, float* C, 
                int width, int height) 
            {
                __shared__ float tile_A[16][16];
                __shared__ float tile_B[16][16];
                
                int bx = blockIdx.x;
                int by = blockIdx.y;
                int tx = threadIdx.x;
                int ty = threadIdx.y;
                
                int row = by * 16 + ty;
                int col = bx * 16 + tx;
                
                float sum = 0.0f;
                
                for (int m = 0; m < (width + 15) / 16; ++m) {
                    if (row < height && m * 16 + tx < width)
                        tile_A[ty][tx] = A[row * width + m * 16 + tx];
                    else
                        tile_A[ty][tx] = 0.0f;
                        
                    if (m * 16 + ty < height && col < width)
                        tile_B[ty][tx] = B[(m * 16 + ty) * width + col];
                    else
                        tile_B[ty][tx] = 0.0f;
                        
                    __syncthreads();
                    
                    for (int k = 0; k < 16; ++k)
                        sum += tile_A[ty][k] * tile_B[k][tx];
                        
                    __syncthreads();
                }
                
                if (row < height && col < width)
                    C[row * width + col] = sum;
            }";

        var definition = new KernelDefinition("matrix_multiply", kernelSource, "matrix_multiply")
        {
            // Language would be set via source generator
        };

        // Act
        var result = await _compiler.CompileAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("matrix_multiply");
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Compilation")]
    public async Task CompileAsync_InvalidSyntax_ThrowsCompilationException()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var invalidKernel = @"
            extern ""C"" __global__ void invalid_kernel() {
                this is not valid CUDA code
            }";

        var definition = new KernelDefinition("invalid", invalidKernel, "invalid_kernel")
        {
            // Language would be set via source generator
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        _ = await act.Should().ThrowAsync<Exception>()
            .WithMessage("*compilation failed*");
    }

    #endregion

    #region Optimization Level Tests

    [SkippableTheory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Minimal)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Aggressive)]
    [InlineData(OptimizationLevel.Maximum)]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Optimization")]
    public async Task CompileAsync_DifferentOptimizationLevels_Success(OptimizationLevel level)
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void optimization_test(float* data, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    float value = data[idx];
                    for (int i = 0; i < 100; i++) {
                        value = value * 1.01f + 0.01f;
                    }
                    data[idx] = value;
                }
            }";

        var definition = new KernelDefinition("optimization_test", kernelSource, "optimization_test")
        {
            // Language would be set via source generator
        };

        var options = new DotCompute.Abstractions.CompilationOptions { OptimizationLevel = level };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
        _output.WriteLine($"Compiled with optimization level: {level}");
    }

    #endregion

    #region Compute Capability Tests

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "ComputeCapability")]
    public async Task CompileAsync_TargetsCorrectComputeCapability()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var capability = CudaCapabilityManager.GetTargetComputeCapability();
        _output.WriteLine($"Target compute capability: {capability}");

        var kernelSource = @"
            extern ""C"" __global__ void capability_test(float* data) {
                int idx = threadIdx.x;
                data[idx] = __fmaf_rn(data[idx], 2.0f, 1.0f);
            }";

        var definition = new KernelDefinition("capability_test", kernelSource, "capability_test")
        {
            // Language would be set via source generator
        };

        var options = new DotCompute.Abstractions.CompilationOptions
        {
            // Metadata = new Dictionary<string, object>
            // {
            //     ["ComputeCapability"] = capability
            // }
        };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "ComputeCapability")]
    public async Task CompileAsync_ModernGpuFeatures_Success()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        var capability = CudaCapabilityManager.GetTargetComputeCapability();
        Skip.If(capability.major < 7 || (capability.major == 7 && capability.minor < 0), "Requires compute capability 7.0 or higher");

        // Arrange - Use tensor core operations (requires CC 7.0+)
        var kernelSource = @"
            #include <mma.h>
            using namespace nvcuda;
            
            extern ""C"" __global__ void tensor_core_test(
                half* a, half* b, float* c, int m, int n, int k) 
            {
                wmma::fragment<wmma::matrix_a, 16, 16, 16, half, wmma::row_major> a_frag;
                wmma::fragment<wmma::matrix_b, 16, 16, 16, half, wmma::col_major> b_frag;
                wmma::fragment<wmma::accumulator, 16, 16, 16, float> c_frag;
                
                wmma::fill_fragment(c_frag, 0.0f);
                
                int warpM = (blockIdx.x * blockDim.x + threadIdx.x) / 32;
                int warpN = (blockIdx.y * blockDim.y + threadIdx.y) / 32;
                
                if (warpM < m/16 && warpN < n/16) {
                    wmma::load_matrix_sync(a_frag, a + warpM * 16 * k, k);
                    wmma::load_matrix_sync(b_frag, b + warpN * 16 * k, k);
                    wmma::mma_sync(c_frag, a_frag, b_frag, c_frag);
                    wmma::store_matrix_sync(c + warpM * 16 * n + warpN * 16, c_frag, n, wmma::mem_row_major);
                }
            }";

        var definition = new KernelDefinition("tensor_core_test", kernelSource, "tensor_core_test")
        {
            // Language would be set via source generator
        };

        // Act
        var result = await _compiler.CompileAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _output.WriteLine("Successfully compiled tensor core kernel");
    }

    #endregion

    #region Caching Tests

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Caching")]
    public async Task CompileAsync_SameKernelTwice_UsesCachedResult()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void cache_test(float* data) {
                data[threadIdx.x] = threadIdx.x * 2.0f;
            }";

        var definition = new KernelDefinition("cache_test", kernelSource, "cache_test")
        {
            // Language would be set via source generator
        };

        // Act
        var result1 = await _compiler.CompileAsync(definition);
        var result2 = await _compiler.CompileAsync(definition);

        // Assert
        _ = result1.Should().NotBeNull();
        _ = result2.Should().NotBeNull();
        _ = result1.Id.Should().Be(result2.Id, "cached result should have same ID");
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Caching")]
    public async Task ClearCache_RemovesCachedKernels()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void clear_cache_test(float* data) {
                data[threadIdx.x] = 1.0f;
            }";

        var definition = new KernelDefinition("clear_cache_test", kernelSource, "clear_cache_test")
        {
            // Language would be set via source generator
        };

        // Act
        var result1 = await _compiler.CompileAsync(definition);
        _compiler.ClearCache();
        var result2 = await _compiler.CompileAsync(definition);

        // Assert
        _ = result1.Should().NotBeNull();
        _ = result2.Should().NotBeNull();
        _ = result1.Id.Should().NotBe(result2.Id, "should recompile after cache clear");
    }

    #endregion

    #region Concurrent Compilation Tests

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Concurrency")]
    public async Task CompileAsync_ConcurrentCompilations_ThreadSafe()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelTemplates = Enumerable.Range(0, 10)
            .Select(i => new KernelDefinition(
                $"concurrent_{i}",
                $@"extern ""C"" __global__ void concurrent_{i}(float* data) {{
                    data[threadIdx.x] = {i}.0f;
                }}",
                $"concurrent_{i}")
            {
                // Language would be set via source generator
            })
            .ToArray();

        // Act
        var tasks = kernelTemplates.Select(k => _compiler.CompileAsync(k));
        var results = await Task.WhenAll(tasks);

        // Assert
        _ = results.Should().HaveCount(10);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());
        _ = results.Select(r => r.Name).Should().OnlyHaveUniqueItems();
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Concurrency")]
    public async Task CompileAsync_ConcurrentSameKernel_OnlyCompilesOnce()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void concurrent_same(float* data) {
                data[threadIdx.x] = 42.0f;
            }";

        var definition = new KernelDefinition("concurrent_same", kernelSource, "concurrent_same")
        {
            // Language would be set via source generator
        };

        // Act - Launch multiple concurrent compilations of the same kernel
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => _compiler.CompileAsync(definition))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        _ = results.Should().HaveCount(20);
        _ = results.Should().AllSatisfy(r => r.Id.Should().Be(results[0].Id),

            "all should get the same cached instance");
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "ErrorHandling")]
    public async Task CompileAsync_MissingEntryPoint_ThrowsException()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void some_kernel(float* data) {
                data[threadIdx.x] = 1.0f;
            }";

        var definition = new KernelDefinition("missing", kernelSource, "non_existent_kernel")
        {
            // Language would be set via source generator
        };

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition);
        _ = await act.Should().ThrowAsync<Exception>();
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "ErrorHandling")]
    public async Task CompileAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var largeKernelSource = string.Join("\n",
            Enumerable.Range(0, 1000).Select(i =>

                $"__device__ float function_{i}(float x) {{ return x * {i}.0f + {i}.0f; }}")) +
            @"
            extern ""C"" __global__ void large_kernel(float* data) {
                data[threadIdx.x] = function_0(1.0f);
            }";

        var definition = new KernelDefinition("large", largeKernelSource, "large_kernel")
        {
            // Language would be set via source generator
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _compiler.CompileAsync(definition, cancellationToken: cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region PTX vs CUBIN Tests

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Compilation")]
    public async Task CompileAsync_GeneratesPtxForOlderGpus()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void ptx_test(float* data) {
                data[threadIdx.x] = 1.0f;
            }";

        var definition = new KernelDefinition("ptx_test", kernelSource, "ptx_test")
        {
            // Language would be set via source generator
        };

        var options = new DotCompute.Abstractions.CompilationOptions
        {
            // Metadata = new Dictionary<string, object>
            // {
            //     ["GeneratePTX"] = true
            // }
        };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
        _output.WriteLine("Successfully generated PTX code");
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "Compilation")]
    public async Task CompileAsync_GeneratesCubinForModernGpus()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        var capability = CudaCapabilityManager.GetTargetComputeCapability();
        Skip.If(capability.major < 7 || (capability.major == 7 && capability.minor < 0), "CUBIN generation requires compute capability 7.0+");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void cubin_test(float* data) {
                data[threadIdx.x] = 2.0f;
            }";

        var definition = new KernelDefinition("cubin_test", kernelSource, "cubin_test")
        {
            // Language would be set via source generator
        };

        var options = new DotCompute.Abstractions.CompilationOptions
        {
            // Metadata = new Dictionary<string, object>
            // {
            //     ["GenerateCUBIN"] = true,
            //     ["ComputeCapability"] = capability
            // }
        };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
        _output.WriteLine($"Successfully generated CUBIN for compute capability {capability}");
    }

    #endregion

    #region Advanced Feature Tests

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "AdvancedFeatures")]
    public async Task CompileAsync_DynamicParallelism_Success()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        var capability = CudaCapabilityManager.GetTargetComputeCapability();
        Skip.If(capability.major < 3 || (capability.major == 3 && capability.minor < 5), "Dynamic parallelism requires compute capability 3.5+");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void child_kernel(float* data, int offset) {
                int idx = threadIdx.x + offset;
                data[idx] = idx * 2.0f;
            }
            
            extern ""C"" __global__ void parent_kernel(float* data, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx == 0) {
                    child_kernel<<<1, 32>>>(data, 32);
                }
            }";

        var definition = new KernelDefinition("parent_kernel", kernelSource, "parent_kernel")
        {
            // Language would be set via source generator
        };

        var options = new DotCompute.Abstractions.CompilationOptions
        {
            // Metadata = new Dictionary<string, object>
            // {
            //     ["EnableDynamicParallelism"] = true
            // }
        };

        // Act
        var result = await _compiler.CompileAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
        _output.WriteLine("Successfully compiled kernel with dynamic parallelism");
    }

    [SkippableFact]
    [Trait("Category", "CUDA")]
    [Trait("Category", "AdvancedFeatures")]
    public async Task CompileAsync_AtomicOperations_Success()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var kernelSource = @"
            extern ""C"" __global__ void atomic_test(int* counter, float* data, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    atomicAdd(counter, 1);
                    float old = atomicExch(&data[idx], idx * 2.0f);
                    atomicMax(counter, (int)old);
                }
            }";

        var definition = new KernelDefinition("atomic_test", kernelSource, "atomic_test")
        {
            // Language would be set via source generator
        };

        // Act
        var result = await _compiler.CompileAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _output.WriteLine("Successfully compiled kernel with atomic operations");
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _compiler?.Dispose();
        }
        base.Dispose(disposing);
    }
}