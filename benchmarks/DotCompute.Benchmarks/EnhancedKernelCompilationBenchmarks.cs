using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Benchmarks;


/// <summary>
/// Enhanced benchmarks for kernel compilation performance.
/// Tests cold vs cached compilation, different optimization levels, and kernel complexity.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class EnhancedKernelCompilationBenchmarks : IDisposable
{
    private DefaultAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _accelerator = null!;
    private readonly Dictionary<string, KernelDefinition> _kernelDefinitions = [];
    private readonly Dictionary<string, ICompiledKernel> _compiledKernels = [];

    [Params("Simple", "Complex", "VectorAdd", "MatrixMultiply", "Reduction")]
    public string KernelType { get; set; } = "Simple";

    [Params(OptimizationLevel.Debug, OptimizationLevel.Default, OptimizationLevel.Release, OptimizationLevel.Maximum)]
    public OptimizationLevel OptimizationLevel { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);

        var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
        _acceleratorManager.RegisterProvider(cpuProvider);
        await _acceleratorManager.InitializeAsync();

        _accelerator = _acceleratorManager.Default;

        SetupKernelDefinitions();
    }

    private void SetupKernelDefinitions()
    {
        // Simple kernel
        var simpleKernelSource = @"
            void simple_kernel(global float* input, global float* output, int size) {
                int id = get_global_id(0);
                if (id < size) {
                    output[id] = input[id] * 2.0f;
                }
            }";

        var simpleKernelSource_obj = new TextKernelSource(simpleKernelSource, "simple_kernel", KernelLanguage.OpenCL, "simple_kernel");
        _kernelDefinitions["Simple"] = new KernelDefinition("simple_kernel", simpleKernelSource_obj, new CompilationOptions());

        // Complex kernel with multiple operations
        var complexKernelSource = @"
            void complex_kernel(global float* input, global float* output, int size) {
                int id = get_global_id(0);
                if (id < size) {
                    float value = input[id];
                    
                    // Multiple mathematical operations
                    value = sin(value) * cos(value);
                    value = exp(value) - log(value + 1.0f);
                    value = sqrt(fabs(value));
                    
                    // Conditional logic
                    if (value > 1.0f) {
                        value = value * value;
                    } else {
                        value = 1.0f / (value + 0.001f);
                    }
                    
                    output[id] = value;
                }
            }";

        var complexKernelSource_obj = new TextKernelSource(complexKernelSource, "complex_kernel", KernelLanguage.OpenCL, "complex_kernel");
        _kernelDefinitions["Complex"] = new KernelDefinition("complex_kernel", complexKernelSource_obj, new CompilationOptions());

        // Vector addition kernel
        var vectorAddSource = @"
            void vector_add(global const float* a, global const float* b, global float* result, int size) {
                int id = get_global_id(0);
                if (id < size) {
                    result[id] = a[id] + b[id];
                }
            }";

        var vectorAddSource_obj = new TextKernelSource(vectorAddSource, "vector_add", KernelLanguage.OpenCL, "vector_add");
        _kernelDefinitions["VectorAdd"] = new KernelDefinition("vector_add", vectorAddSource_obj, new CompilationOptions());

        // Matrix multiplication kernel
        var matrixMultiplySource = @"
            void matrix_multiply(global const float* A, global const float* B, global float* C, 
                               int M, int N, int K) {
                int row = get_global_id(0);
                int col = get_global_id(1);
                
                if (row < M && col < N) {
                    float sum = 0.0f;
                    for (int k = 0; k < K; k++) {
                        sum += A[row * K + k] * B[k * N + col];
                    }
                    C[row * N + col] = sum;
                }
            }";

        var matrixMultiplySource_obj = new TextKernelSource(matrixMultiplySource, "matrix_multiply", KernelLanguage.OpenCL, "matrix_multiply");
        _kernelDefinitions["MatrixMultiply"] = new KernelDefinition("matrix_multiply", matrixMultiplySource_obj, new CompilationOptions());

        // Reduction kernel
        var reductionSource = @"
            void reduction_sum(global const float* input, global float* output, 
                              local float* scratch, int size) {
                int global_id = get_global_id(0);
                int local_id = get_local_id(0);
                int local_size = get_local_size(0);
                
                // Load data into local memory
                scratch[local_id] = (global_id < size) ? input[global_id] : 0.0f;
                barrier(CLK_LOCAL_MEM_FENCE);
                
                // Reduction in local memory
                for (int stride = local_size / 2; stride > 0; stride /= 2) {
                    if (local_id < stride) {
                        scratch[local_id] += scratch[local_id + stride];
                    }
                    barrier(CLK_LOCAL_MEM_FENCE);
                }
                
                // Write result
                if (local_id == 0) {
                    output[get_group_id(0)] = scratch[0];
                }
            }";

        var reductionSource_obj = new TextKernelSource(reductionSource, "reduction_sum", KernelLanguage.OpenCL, "reduction_sum");
        _kernelDefinitions["Reduction"] = new KernelDefinition("reduction_sum", reductionSource_obj, new CompilationOptions());
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var kernel in _compiledKernels.Values)
        {
            await kernel.DisposeAsync();
        }
        _compiledKernels.Clear();
        if (_acceleratorManager != null)
        {
            await _acceleratorManager.DisposeAsync();
        }
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        foreach (var kernel in _compiledKernels.Values)
        {
            await kernel.DisposeAsync();
        }
        _compiledKernels.Clear();
    }

    [Benchmark(Baseline = true)]
    public async Task ColdKernelCompilation()
    {
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel
        };

        var kernel = await _accelerator.CompileKernelAsync(
            _kernelDefinitions[KernelType], options);

        _compiledKernels[$"{KernelType}_cold"] = kernel;
    }

    [Benchmark]
    public async Task CachedKernelCompilation()
    {
        // First compilation (should be cached)
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel
        };

        var kernel1 = await _accelerator.CompileKernelAsync(
            _kernelDefinitions[KernelType], options);
        await kernel1.DisposeAsync();

        // Second compilation (should hit cache)
        var kernel2 = await _accelerator.CompileKernelAsync(
            _kernelDefinitions[KernelType], options);

        _compiledKernels[$"{KernelType}_cached"] = kernel2;
    }

    [Benchmark]
    public async Task ParallelKernelCompilation()
    {
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel
        };

        var tasks = new List<Task<ICompiledKernel>>();

        // Compile multiple kernels in parallel
        foreach (var kvp in _kernelDefinitions.Take(3))
        {
            tasks.Add(_accelerator.CompileKernelAsync(kvp.Value, options).AsTask());
        }

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < results.Length; i++)
        {
            _compiledKernels[$"parallel_{i}"] = results[i];
        }
    }

    [Benchmark]
    public async Task OptimizationLevelComparison()
    {
        var optimizationLevels = new[]
        {
        OptimizationLevel.Debug,
        OptimizationLevel.Default,
        OptimizationLevel.Release
    };

        var kernels = new List<ICompiledKernel>();

        foreach (var level in optimizationLevels)
        {
            var options = new CompilationOptions { OptimizationLevel = level };
            var kernel = await _accelerator.CompileKernelAsync(
                _kernelDefinitions[KernelType], options);
            kernels.Add(kernel);
        }

        for (var i = 0; i < kernels.Count; i++)
        {
            _compiledKernels[$"opt_{i}"] = kernels[i];
        }
    }

    [Benchmark]
    public async Task CompilationWithDefines()
    {
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel,
            Defines = new Dictionary<string, string>
            {
                ["BLOCK_SIZE"] = "256",
                ["USE_FAST_MATH"] = "1",
                ["PRECISION"] = "float"
            }
        };

        var kernel = await _accelerator.CompileKernelAsync(
            _kernelDefinitions[KernelType], options);

        _compiledKernels[$"{KernelType}_with_defines"] = kernel;
    }

    [Benchmark]
    public async Task CompilationWithAdditionalFlags()
    {
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel,
            AdditionalFlags = ["-cl-fast-relaxed-math", "-cl-mad-enable"],
            FastMath = true,
            UnrollLoops = true
        };

        var kernel = await _accelerator.CompileKernelAsync(
            _kernelDefinitions[KernelType], options);

        _compiledKernels[$"{KernelType}_with_flags"] = kernel;
    }

    [Benchmark]
    public static double CompilationThroughput()
    {
        // Calculate kernels compiled per second (simulated)
        var compilationTime = 0.5; // Simulated 0.5 seconds
        var kernelsCompiled = 1;

        return kernelsCompiled / compilationTime;
    }

    public void Dispose()
    {
        try
        {
            Cleanup().GetAwaiter().GetResult();
            if (_accelerator != null)
            {
                var task = _accelerator.DisposeAsync();
                if (task.IsCompleted)
                {
                    task.GetAwaiter().GetResult();
                }
                else
                {
                    task.AsTask().Wait();
                }
            }
        }
        catch
        {
            // Ignore disposal errors
        }
        GC.SuppressFinalize(this);
    }
}
