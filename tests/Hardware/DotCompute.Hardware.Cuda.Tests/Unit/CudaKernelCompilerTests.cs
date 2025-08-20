// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

using DotCompute.Abstractions.Kernels;
namespace DotCompute.Hardware.Cuda.Tests.Unit;


/// <summary>
/// Unit tests for CUDA kernel compilation functionality
/// </summary>
[Collection("CUDA Hardware Tests")]
public sealed class CudaKernelCompilerTests : IDisposable
{
    private readonly ILogger<CudaKernelCompilerTests> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];
    private readonly List<ICompiledKernel> _compiledKernels = [];

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, Exception, Exception?> LogKernelDisposeError =
        LoggerMessage.Define<Exception>(
            LogLevel.Warning,
            new EventId(1, nameof(LogKernelDisposeError)),
            "Error disposing compiled kernel: {Exception}");

    private static readonly Action<ILogger, Exception, Exception?> LogAcceleratorDisposeError =
        LoggerMessage.Define<Exception>(
            LogLevel.Warning,
            new EventId(2, nameof(LogAcceleratorDisposeError)),
            "Error disposing CUDA accelerator: {Exception}");

    public CudaKernelCompilerTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = _loggerFactory.CreateLogger<CudaKernelCompilerTests>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithSimpleKernel_ShouldSucceed()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSimpleVectorAddKernel();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("vector_add");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithOptimizationOptions_ShouldRespectSettings()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSimpleVectorAddKernel();
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = false
        };

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("vector_add");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithDebugInfo_ShouldIncludeDebugSymbols()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSimpleVectorAddKernel();
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.None,
            EnableDebugInfo = true
        };

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("vector_add");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Maximum)]
    public async Task CudaKernelCompiler_CompileAsync_WithDifferentOptimizationLevels_ShouldSucceed(OptimizationLevel level)
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSimpleVectorAddKernel();
        var options = new CompilationOptions { OptimizationLevel = level };

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithInvalidSyntax_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateInvalidKernel();

        // Act & Assert
        var compileAction = async () => await accelerator.CompileKernelAsync(kernelDefinition);
        _ = await compileAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to compile CUDA kernel*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithMathFunctions_ShouldIncludeHeaders()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateMathKernel();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("math_kernel");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithSharedMemory_ShouldCompileSuccessfully()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSharedMemoryKernel();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("shared_memory_kernel");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithCustomEntryPoint_ShouldRespectName()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateCustomEntryPointKernel();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("custom_kernel");
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_PerformanceTest_ShouldCompleteQuickly()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSimpleVectorAddKernel();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _compiledKernels.Add(compiledKernel);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(compiledKernel);
        _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            "Kernel compilation should complete within 10 seconds");

        _output.WriteLine($"Kernel compilation took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_CachingTest_SecondCompileShouldBeFaster()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSimpleVectorAddKernel();

        // Act - First compilation
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        var compiledKernel1 = await accelerator.CompileKernelAsync(kernelDefinition);
        stopwatch1.Stop();
        _compiledKernels.Add(compiledKernel1);

        // Act - Second compilation(should use cache)
        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();
        var compiledKernel2 = await accelerator.CompileKernelAsync(kernelDefinition);
        stopwatch2.Stop();
        _compiledKernels.Add(compiledKernel2);

        // Assert
        Assert.NotNull(compiledKernel1);
        Assert.NotNull(compiledKernel2);
        _ = (stopwatch2.ElapsedMilliseconds < stopwatch1.ElapsedMilliseconds + 100).Should().BeTrue(
            "Cached compilation should be faster or similar to first compilation");

        _output.WriteLine($"First compilation: {stopwatch1.ElapsedMilliseconds}ms, Second: {stopwatch2.ElapsedMilliseconds}ms");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithComplexKernel_ShouldHandleComplexity()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateComplexMatrixMultiplyKernel();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
        _ = compiledKernel.Name.Should().Be("matrix_multiply");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithAdditionalFlags_ShouldAcceptCustomOptions()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateSimpleVectorAddKernel();
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            AdditionalFlags = ["-lineinfo", "--ptxas-options=-v"]
        };

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
        _compiledKernels.Add(compiledKernel);

        // Assert
        Assert.NotNull(compiledKernel);
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithNullDefinition_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();

        // Act & Assert
        var compileAction = async () => await accelerator.CompileKernelAsync(null!);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await compileAction());
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_WithEmptyKernelName_ShouldHandleGracefully()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = new KernelDefinition
        {
            Name = "", // Empty name
            Code = GetSimpleKernelSource(),
            EntryPoint = "vector_add"
        };

        // Act & Assert
        var compileAction = async () => await accelerator.CompileKernelAsync(kernelDefinition);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await compileAction());
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaKernelCompiler_CompileAsync_ConcurrentCompilation_ShouldHandleParallelRequests()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinitions = Enumerable.Range(0, 5)
            .Select(i => CreateUniqueKernel($"test_kernel_{i}"))
            .ToArray();

        // Act
        var compileTasks = kernelDefinitions
            .Select(kernel => accelerator.CompileKernelAsync(kernel))
            .ToArray();

        var compiledKernels = await Task.WhenAll(compileTasks.Select(t => t.AsTask()));
        _compiledKernels.AddRange(compiledKernels);

        // Assert
        Assert.Equal(5, compiledKernels.Length);
        _ = compiledKernels.Should().AllSatisfy(k => k.Should().NotBeNull());
    }

    // Helper Methods
    private CudaAccelerator CreateAccelerator()
    {
        var accelerator = new CudaAccelerator(0, null);
        _accelerators.Add(accelerator);
        return accelerator;
    }

    private static KernelDefinition CreateSimpleVectorAddKernel()
    {
        return new KernelDefinition
        {
            Name = "vector_add",
            Code = GetSimpleKernelSource(),
            EntryPoint = "vector_add"
        };
    }

    private static KernelDefinition CreateInvalidKernel()
    {
        return new KernelDefinition
        {
            Name = "invalid_kernel",
            Code = "invalid cuda syntax {{{ this will not compile",
            EntryPoint = "invalid_kernel"
        };
    }

    private static KernelDefinition CreateMathKernel()
    {
        const string mathKernelSource = @"
__global__ void math_kernel(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        output[idx] = sinf(input[idx]) + cosf(input[idx]) + expf(input[idx]);
    }
}";
        return new KernelDefinition
        {
            Name = "math_kernel",
            Code = mathKernelSource,
            EntryPoint = "math_kernel"
        };
    }

    private static KernelDefinition CreateSharedMemoryKernel()
    {
        const string sharedMemoryKernelSource = @"
__global__ void shared_memory_kernel(float* input, float* output, int n)
{
    __shared__ float shared_data[256];
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int tid = threadIdx.x;
    
    if(idx < n) {
        shared_data[tid] = input[idx];
    }
    
    __syncthreads();
    
    if(idx < n) {
        output[idx] = shared_data[tid] * 2.0f;
    }
}";
        return new KernelDefinition
        {
            Name = "shared_memory_kernel",
            Code = sharedMemoryKernelSource,
            EntryPoint = "shared_memory_kernel"
        };
    }

    private static KernelDefinition CreateCustomEntryPointKernel()
    {
        return new KernelDefinition
        {
            Name = "custom_kernel",
            Code = GetSimpleKernelSource().Replace("vector_add", "custom_entry_point", StringComparison.Ordinal),
            EntryPoint = "custom_entry_point"
        };
    }

    private static KernelDefinition CreateComplexMatrixMultiplyKernel()
    {
        const string matrixMultiplySource = @"
__global__ void matrix_multiply(float* A, float* B, float* C, int n)
{
    __shared__ float As[16][16];
    __shared__ float Bs[16][16];
    
    int bx = blockIdx.x, by = blockIdx.y;
    int tx = threadIdx.x, ty = threadIdx.y;
    
    int Row = by * 16 + ty;
    int Col = bx * 16 + tx;
    
    float Cvalue = 0.0f;
    
    for(int m = 0; m <n + 16 - 1) / 16; ++m) {
        if((Row < n) &&(m * 16 + tx < n))
            As[ty][tx] = A[Row * n + m * 16 + tx];
        else
            As[ty][tx] = 0.0f;
            
        if((Col < n) &&(m * 16 + ty < n))
            Bs[ty][tx] = B[(m * 16 + ty) * n + Col];
        else
            Bs[ty][tx] = 0.0f;
            
        __syncthreads();
        
        for(int k = 0; k < 16; ++k)
            Cvalue += As[ty][k] * Bs[k][tx];
            
        __syncthreads();
    }
    
    if((Row < n) &&(Col < n))
        C[Row * n + Col] = Cvalue;
}";
        return new KernelDefinition
        {
            Name = "matrix_multiply",
            Code = matrixMultiplySource,
            EntryPoint = "matrix_multiply"
        };
    }

    private static KernelDefinition CreateUniqueKernel(string name)
    {
        var source = GetSimpleKernelSource().Replace("vector_add", name, StringComparison.Ordinal);
        return new KernelDefinition
        {
            Name = name,
            Code = source,
            EntryPoint = name
        };
    }

    private static string GetSimpleKernelSource()
    {
        return @"
__global__ void vector_add(float* a, float* b, float* c, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}";
    }

    private static bool IsCudaAvailable()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNvrtcAvailable() => CudaKernelCompiler.IsNvrtcAvailable();

    public void Dispose()
    {
        foreach (var kernel in _compiledKernels)
        {
            try
            {
                (kernel as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                LogKernelDisposeError(_logger, ex, null);
            }
        }
        _compiledKernels.Clear();

        foreach (var accelerator in _accelerators)
        {
            try
            {
                accelerator?.Dispose();
            }
            catch (Exception ex)
            {
                LogAcceleratorDisposeError(_logger, ex, null);
            }
        }
        _accelerators.Clear();
        _loggerFactory?.Dispose();
    }
}
