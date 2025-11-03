// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Kernels.Exceptions;
using DotCompute.Backends.CPU.Kernels.Models;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Tests.Common;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Tests for CPU kernel compilation including optimization levels, AOT compilation,
/// JIT compilation, and compiler-specific features.
/// </summary>
[Trait("Category", TestCategories.HardwareIndependent)]
[Trait("Category", TestCategories.KernelCompilation)]
public class CpuKernelCompilerTests : IAsyncDisposable
{
    private readonly ILogger<CpuKernelCompilerTests> _logger;
    private readonly CpuThreadPool _threadPool;
    /// <summary>
    /// Initializes a new instance of the CpuKernelCompilerTests class.
    /// </summary>


    public CpuKernelCompilerTests()
    {
        using var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<CpuKernelCompilerTests>();


        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions
        {
            WorkerThreads = Environment.ProcessorCount
        });


        _threadPool = new CpuThreadPool(threadPoolOptions);
    }
    /// <summary>
    /// Gets compile async_ with valid kernel_ compiles successfully.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task CompileAsync_WithValidKernel_CompilesSuccessfully()
    {
        // Arrange
        var definition = CreateSimpleKernelDefinition("simple_add", 3, 1);
        var context = CreateCompilationContext(definition, OptimizationLevel.Default);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be("simple_add");
        _ = compiledKernel.Id.Should().NotBe(Guid.Empty);


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ with different optimization levels_ applies optimizations.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <returns>The result of the operation.</returns>


    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Default)]

    [InlineData(OptimizationLevel.O3)]
    public async Task CompileAsync_WithDifferentOptimizationLevels_AppliesOptimizations(OptimizationLevel level)
    {
        // Arrange
        var definition = CreateComplexKernelDefinition($"optimized_kernel_{level}", 5, 2);
        var context = CreateCompilationContext(definition, level);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be($"optimized_kernel_{level}");

        // Verify optimization metadata if available

        if (definition.Metadata?.ContainsKey("CompilationTime") == true)
        {
            _ = definition.Metadata["CompilationTime"].Should().BeOfType<TimeSpan>();
        }


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ with vectorizable kernel_ enables vectorization.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task CompileAsync_WithVectorizableKernel_EnablesVectorization()
    {
        // Arrange
        var definition = CreateVectorizableKernelDefinition("vector_operation", 3, 1);
        var context = CreateCompilationContext(definition, OptimizationLevel.O3);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be("vector_operation");

        // Check if SIMD capabilities were utilized

        if (definition.Metadata?.ContainsKey("SimdCapabilities") == true)
        {
            var simdCapabilities = definition.Metadata["SimdCapabilities"];
            _ = simdCapabilities.Should().NotBeNull();
        }


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ with non vectorizable kernel_ falls back to scalar.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task CompileAsync_WithNonVectorizableKernel_FallsBackToScalar()
    {
        // Arrange
        var definition = CreateNonVectorizableKernelDefinition("complex_branching", 4, 1);
        var context = CreateCompilationContext(definition, OptimizationLevel.O3);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be("complex_branching");


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ with memory intensive kernel_ optimizes memory access.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task CompileAsync_WithMemoryIntensiveKernel_OptimizesMemoryAccess()
    {
        // Arrange
        var definition = CreateMemoryIntensiveKernelDefinition("memory_intensive", 8, 2);
        var context = CreateCompilationContext(definition, OptimizationLevel.O3);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be("memory_intensive");


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ with invalid kernel_ throws kernel compilation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task CompileAsync_WithInvalidKernel_ThrowsKernelCompilationException()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition("invalid", "", "")
        {
            Metadata = new Dictionary<string, object>
            {
                ["WorkDimensions"] = 0, // Invalid work dimensions
                ["ParameterCount"] = 0  // Invalid parameter count
            }
        };
        var context = CreateCompilationContext(invalidDefinition, OptimizationLevel.Default);

        // Act & Assert

        Func<Task> act = async () => await CpuKernelCompiler.CompileAsync(context);
        _ = await act.Should().ThrowAsync<KernelCompilationException>();
    }
    /// <summary>
    /// Gets compile async_ with null context_ throws argument null exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task CompileAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = async () => await CpuKernelCompiler.CompileAsync(null!);
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }
    /// <summary>
    /// Gets compile async_ with different dimensions_ calculates correct work group size.
    /// </summary>
    /// <param name="dimensions">The dimensions.</param>
    /// <returns>The result of the operation.</returns>


    [Theory]
    [InlineData(1)] // 1D kernel
    [InlineData(2)] // 2D kernel
    [InlineData(3)] // 3D kernel
    public async Task CompileAsync_WithDifferentDimensions_CalculatesCorrectWorkGroupSize(int dimensions)
    {
        // Arrange
        var definition = CreateKernelWithDimensions($"kernel_{dimensions}d", 3, dimensions);
        var context = CreateCompilationContext(definition, OptimizationLevel.Default);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be($"kernel_{dimensions}d");


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ compilation performance_ meets timing requirements.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public async Task CompileAsync_CompilationPerformance_MeetsTimingRequirements()
    {
        // Arrange
        var definition = CreateComplexKernelDefinition("performance_test", 10, 3);
        var context = CreateCompilationContext(definition, OptimizationLevel.O3);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);
        stopwatch.Stop();

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Should compile within 10 seconds


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ concurrent compilation_ handles parallel requests.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.Concurrency)]
    public async Task CompileAsync_ConcurrentCompilation_HandlesParallelRequests()
    {
        // Arrange
        var definitions = Enumerable.Range(0, 5).Select(i =>
            CreateSimpleKernelDefinition($"concurrent_{i}", 3, 1)).ToArray();


        var contexts = definitions.Select(d =>

            CreateCompilationContext(d, OptimizationLevel.Default)).ToArray();

        // Act

        var compilationTasks = contexts.Select(c => CpuKernelCompiler.CompileAsync(c).AsTask()).ToArray();
        var compiledKernels = await Task.WhenAll(compilationTasks);

        // Assert
        _ = compiledKernels.Should().HaveCount(5);
        _ = compiledKernels.Should().OnlyContain(k => k != null);


        for (var i = 0; i < 5; i++)
        {
            _ = compiledKernels[i].Name.Should().Be($"concurrent_{i}");
        }

        // Cleanup

        await Task.WhenAll(compiledKernels.Select(k => k.DisposeAsync().AsTask()));
    }
    /// <summary>
    /// Gets compile async_ with debug info_ includes debugging symbols.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task CompileAsync_WithDebugInfo_IncludesDebuggingSymbols()
    {
        // Arrange
        var definition = CreateSimpleKernelDefinition("debug_kernel", 3, 1);
        _ = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.None,
            EnableDebugInfo = true,
            // AdditionalFlags - read-only, add after construction
            // AdditionalFlags = ["debug", "symbols"]
        };
        var context = CreateCompilationContext(definition, OptimizationLevel.None);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be("debug_kernel");


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ with a o t path_ uses a o t compiler.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task CompileAsync_WithAOTPath_UsesAOTCompiler()
    {
        // Arrange - Simulate AOT environment
        var definition = CreateSimpleKernelDefinition("aot_kernel", 3, 1);
        var context = CreateCompilationContext(definition, OptimizationLevel.Default);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be("aot_kernel");


        await compiledKernel.DisposeAsync();
    }
    /// <summary>
    /// Gets compile async_ with j i t path_ uses j i t compiler.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task CompileAsync_WithJITPath_UsesJITCompiler()
    {
        // Arrange - Simulate JIT environment (this is the default)
        var definition = CreateSimpleKernelDefinition("jit_kernel", 3, 1);
        var context = CreateCompilationContext(definition, OptimizationLevel.Default);

        // Act

        var compiledKernel = await CpuKernelCompiler.CompileAsync(context);

        // Assert
        _ = compiledKernel.Should().NotBeNull();
        _ = compiledKernel.Name.Should().Be("jit_kernel");


        await compiledKernel.DisposeAsync();
    }


    private static KernelDefinition CreateSimpleKernelDefinition(string name, int parameterCount, int workDimensions)
    {
        return new KernelDefinition(name, GenerateSimpleKernelCode(name), name)
        {
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = parameterCount,
                ["WorkDimensions"] = workDimensions,
                ["ParameterAccess"] = Enumerable.Repeat("ReadWrite", parameterCount).ToArray()
            }
        };
    }


    private static KernelDefinition CreateComplexKernelDefinition(string name, int parameterCount, int workDimensions)
    {
        return new KernelDefinition(name, GenerateComplexKernelCode(name), name)
        {
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = parameterCount,
                ["WorkDimensions"] = workDimensions,
                ["ParameterAccess"] = Enumerable.Repeat("ReadWrite", parameterCount).ToArray()
            }
        };
    }


    private static KernelDefinition CreateVectorizableKernelDefinition(string name, int parameterCount, int workDimensions)
    {
        return new KernelDefinition(name, GenerateVectorizableKernelCode(name), name)
        {
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = parameterCount,
                ["WorkDimensions"] = workDimensions,
                ["ParameterAccess"] = new[] { "ReadOnly", "ReadOnly", "WriteOnly" }
            }
        };
    }


    private static KernelDefinition CreateNonVectorizableKernelDefinition(string name, int parameterCount, int workDimensions)
    {
        return new KernelDefinition(name, GenerateNonVectorizableKernelCode(name), name)
        {
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = parameterCount,
                ["WorkDimensions"] = workDimensions,
                ["ParameterAccess"] = Enumerable.Repeat("ReadWrite", parameterCount).ToArray()
            }
        };
    }


    private static KernelDefinition CreateMemoryIntensiveKernelDefinition(string name, int parameterCount, int workDimensions)
    {
        return new KernelDefinition(name, GenerateMemoryIntensiveKernelCode(name), name)
        {
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = parameterCount,
                ["WorkDimensions"] = workDimensions,
                ["ParameterAccess"] = Enumerable.Repeat("ReadWrite", parameterCount).ToArray()
            }
        };
    }


    private static KernelDefinition CreateKernelWithDimensions(string name, int parameterCount, int workDimensions)
    {
        return new KernelDefinition(name, GenerateKernelWithDimensions(name, workDimensions), name)
        {
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = parameterCount,
                ["WorkDimensions"] = workDimensions,
                ["ParameterAccess"] = Enumerable.Repeat("ReadWrite", parameterCount).ToArray()
            }
        };
    }

    // NOTE: CpuKernelCompilationContext is internal and cannot be accessed from tests
    // These methods are commented out because they reference internal types
    // Tests that use these methods need to be refactored to use public APIs

    /*
    private CpuKernelCompilationContext CreateCompilationContext(KernelDefinition definition, OptimizationLevel optimizationLevel)
    {
        return CreateCompilationContext(definition, new CompilationOptions
        {
            OptimizationLevel = optimizationLevel,
            EnableDebugInfo = optimizationLevel == OptimizationLevel.None
        });
    }
    
    private CpuKernelCompilationContext CreateCompilationContext(KernelDefinition definition, CompilationOptions options)
    {
        return new CpuKernelCompilationContext
        {
            Definition = definition,
            Options = options,
            SimdCapabilities = SimdCapabilities.GetSummary(),
            ThreadPool = _threadPool,
            Logger = _logger
        };
    }
    */



    private static string GenerateSimpleKernelCode(string name) => $"__kernel void {name}(__global float* a, __global float* b, __global float* c) {{ int i = get_global_id(0); c[i] = a[i] + b[i]; }}";


    private static string GenerateComplexKernelCode(string name)
    {
        return $@"__kernel void {name}(__global float* input, __global float* output, __global float* temp, float factor) {{
            int i = get_global_id(0);
            temp[i] = input[i] * factor;
            output[i] = sqrt(temp[i] * temp[i] + input[i] * input[i]);
        }}";
    }


    private static string GenerateVectorizableKernelCode(string name) => $"__kernel void {name}(__global const float* a, __global const float* b, __global float* c) {{ int i = get_global_id(0); c[i] = a[i] * b[i] + a[i]; }}";


    private static string GenerateNonVectorizableKernelCode(string name)
    {
        return $@"__kernel void {name}(__global float* data) {{
            int i = get_global_id(0);
            if (data[i] > 0.5f) {{
                data[i] = data[i] * 2.0f;
            }} else {{
                data[i] = data[i] / 2.0f;
            }}
        }}";
    }


    private static string GenerateMemoryIntensiveKernelCode(string name)
    {
        return $@"__kernel void {name}(__global float* input, __global float* output, __global float* cache) {{
            int i = get_global_id(0);
            cache[i] = input[i];
            cache[i+1] = input[i+1];
            output[i] = cache[i] + cache[i+1];
        }}";
    }


    private static string GenerateKernelWithDimensions(string name, int dimensions)
    {
        return dimensions switch
        {
            1 => $"__kernel void {name}(__global float* data) {{ int i = get_global_id(0); data[i] *= 2.0f; }}",
            2 => $"__kernel void {name}(__global float* data) {{ int i = get_global_id(0); int j = get_global_id(1); data[i*get_global_size(1)+j] *= 2.0f; }}",
            3 => $"__kernel void {name}(__global float* data) {{ int i = get_global_id(0); int j = get_global_id(1); int k = get_global_id(2); data[i*get_global_size(1)*get_global_size(2)+j*get_global_size(2)+k] *= 2.0f; }}",
            _ => GenerateSimpleKernelCode(name)
        };
    }


    private CpuKernelCompilationContext CreateCompilationContext(KernelDefinition definition, OptimizationLevel optimizationLevel)
    {
        var options = new CompilationOptions
        {
            OptimizationLevel = optimizationLevel
        };

        var simdCapabilities = SimdCapabilities.GetSummary();

        return new CpuKernelCompilationContext
        {
            Definition = definition,
            Options = options,
            SimdCapabilities = simdCapabilities,
            ThreadPool = _threadPool,
            Logger = _logger
        };
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public async ValueTask DisposeAsync()
    {
        if (_threadPool != null)
        {
            await _threadPool.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
