using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
public class KernelCompilationBenchmarks
{
    private IComputeEngine _computeEngine = null!;
    private IAcceleratorManager _acceleratorManager = null!;
    
    private const string SimpleKernel = @"
        __kernel void simple_add(__global float* a, __global float* b, __global float* c) {
            int i = get_global_id(0);
            c[i] = a[i] + b[i];
        }";
    
    private const string ComplexKernel = @"
        __kernel void matrix_multiply(__global float* A, __global float* B, __global float* C, int N) {
            int row = get_global_id(0);
            int col = get_global_id(1);
            
            float sum = 0.0f;
            for (int k = 0; k < N; k++) {
                sum += A[row * N + k] * B[k * N + col];
            }
            C[row * N + col] = sum;
        }";
    
    private const string VeryComplexKernel = @"
        __kernel void complex_computation(
            __global float* input1, __global float* input2, 
            __global float* input3, __global float* output,
            float alpha, float beta, int size) {
            
            int idx = get_global_id(0);
            if (idx >= size) return;
            
            float val1 = input1[idx];
            float val2 = input2[idx];
            float val3 = input3[idx];
            
            // Complex computation
            float result = 0.0f;
            for (int i = 0; i < 10; i++) {
                result += (val1 * alpha + val2 * beta) * sin(val3 * i);
                result = sqrt(fabs(result)) + cos(val1 * val2);
                result = fmax(result, val3) - fmin(val1, val2);
            }
            
            output[idx] = result;
        }";

    [Params("Simple", "Complex", "VeryComplex")]
    public string KernelComplexity { get; set; } = "Simple";

    private string GetKernelSource() => KernelComplexity switch
    {
        "Simple" => SimpleKernel,
        "Complex" => ComplexKernel,
        "VeryComplex" => VeryComplexKernel,
        _ => SimpleKernel
    };

    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);
        
        var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
        _acceleratorManager.RegisterProvider(cpuProvider);
        await _acceleratorManager.InitializeAsync();
        
        _computeEngine = new DefaultComputeEngine(_acceleratorManager, new NullLogger<DefaultComputeEngine>());
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _computeEngine.DisposeAsync();
        await _acceleratorManager.DisposeAsync();
    }

    [Benchmark]
    public async Task<ICompiledKernel> CompileKernel()
    {
        var kernel = await _computeEngine.CompileKernelAsync(
            GetKernelSource(),
            "kernel_" + KernelComplexity.ToLower());
        
        await kernel.DisposeAsync();
        return kernel;
    }

    [Benchmark]
    public async Task<ICompiledKernel> CompileWithOptimization()
    {
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true
        };
        
        var kernel = await _computeEngine.CompileKernelAsync(
            GetKernelSource(),
            "kernel_optimized_" + KernelComplexity.ToLower(),
            options);
        
        await kernel.DisposeAsync();
        return kernel;
    }

    [Benchmark]
    public async Task CompileMultipleKernels()
    {
        const int kernelCount = 5;
        var kernels = new ICompiledKernel[kernelCount];
        
        for (int i = 0; i < kernelCount; i++)
        {
            kernels[i] = await _computeEngine.CompileKernelAsync(
                GetKernelSource(),
                $"kernel_{i}");
        }
        
        foreach (var kernel in kernels)
        {
            await kernel.DisposeAsync();
        }
    }
}