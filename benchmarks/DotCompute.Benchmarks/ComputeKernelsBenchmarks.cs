using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Benchmarks
{

/// <summary>
/// Comprehensive benchmarks for compute kernel execution performance.
/// Tests vector operations, matrix multiplication, reductions, and complex algorithms.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class ComputeKernelsBenchmarks : IDisposable
{
    private DefaultAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _accelerator = null!;
    private IMemoryManager _memoryManager = null!;
    private readonly Dictionary<string, ICompiledKernel> _kernels = [];
    private readonly List<IMemoryBuffer> _buffers = [];

    [Params(1024, 64 * 1024, 1024 * 1024, 16 * 1024 * 1024)]
    public int DataSize { get; set; }

    [Params("VectorAdd", "VectorMultiply", "DotProduct", "MatrixMultiply", "Reduction", "Convolution")]
    public string KernelType { get; set; } = "VectorAdd";

    private float[] _inputA = null!;
    private float[] _inputB = null!;
    private float[] _output = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);

        var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
        _acceleratorManager.RegisterProvider(cpuProvider);
        await _acceleratorManager.InitializeAsync();

        _accelerator = _acceleratorManager.Default;
        _memoryManager = _accelerator.Memory;

        await SetupTestData();
        await CompileKernels();
    }

    private async Task SetupTestData()
    {
        _inputA = new float[DataSize];
        _inputB = new float[DataSize];
        _output = new float[DataSize];

#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        var random = new Random(42); // Fixed seed for reproducibility
        for (var i = 0; i < DataSize; i++)
        {
            _inputA[i] = (float)(random.NextDouble() * 2.0 - 1.0); // [-1, 1]
            _inputB[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }
#pragma warning restore CA5394

        // Fix CS1998: Add minimal await to satisfy async method requirement
        await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task CompileKernels()
    {
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Release,
            FastMath = true
        };

        // Vector addition kernel
        var vectorAddSource = @"
            void vector_add(global const float* a, global const float* b, global float* result, int size) {
                int id = get_global_id(0);
                if (id < size) {
                    result[id] = a[id] + b[id];
                }
            }";

        var vectorAddSource_obj = new TextKernelSource(vectorAddSource, "vector_add", KernelLanguage.OpenCL, "vector_add");
        var vectorAddDef = new KernelDefinition("vector_add", vectorAddSource_obj, options);
        _kernels["VectorAdd"] = await _accelerator.CompileKernelAsync(vectorAddDef, options);

        // Vector multiplication kernel
        var vectorMultiplySource = @"
            void vector_multiply(global const float* a, global const float* b, global float* result, int size) {
                int id = get_global_id(0);
                if (id < size) {
                    result[id] = a[id] * b[id];
                }
            }";

        var vectorMultiplySource_obj = new TextKernelSource(vectorMultiplySource, "vector_multiply", KernelLanguage.OpenCL, "vector_multiply");
        var vectorMultiplyDef = new KernelDefinition("vector_multiply", vectorMultiplySource_obj, options);
        _kernels["VectorMultiply"] = await _accelerator.CompileKernelAsync(vectorMultiplyDef, options);

        // Dot product kernel with reduction
        var dotProductSource = @"
            void dot_product(global const float* a, global const float* b, global float* result, 
                            local float* scratch, int size) {
                int global_id = get_global_id(0);
                int local_id = get_local_id(0);
                int local_size = get_local_size(0);
                
                // Compute partial products
                scratch[local_id] = (global_id < size) ? a[global_id] * b[global_id] : 0.0f;
                barrier(CLK_LOCAL_MEM_FENCE);
                
                // Reduction
                for (int stride = local_size / 2; stride > 0; stride /= 2) {
                    if (local_id < stride) {
                        scratch[local_id] += scratch[local_id + stride];
                    }
                    barrier(CLK_LOCAL_MEM_FENCE);
                }
                
                if (local_id == 0) {
                    result[get_group_id(0)] = scratch[0];
                }
            }";

        var dotProductSource_obj = new TextKernelSource(dotProductSource, "dot_product", KernelLanguage.OpenCL, "dot_product");
        var dotProductDef = new KernelDefinition("dot_product", dotProductSource_obj, options);
        _kernels["DotProduct"] = await _accelerator.CompileKernelAsync(dotProductDef, options);

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
        var matrixMultiplyDef = new KernelDefinition("matrix_multiply", matrixMultiplySource_obj, options);
        _kernels["MatrixMultiply"] = await _accelerator.CompileKernelAsync(matrixMultiplyDef, options);

        // Reduction sum kernel
        var reductionSource = @"
            void reduction_sum(global const float* input, global float* output, 
                              local float* scratch, int size) {
                int global_id = get_global_id(0);
                int local_id = get_local_id(0);
                int local_size = get_local_size(0);
                
                scratch[local_id] = (global_id < size) ? input[global_id] : 0.0f;
                barrier(CLK_LOCAL_MEM_FENCE);
                
                for (int stride = local_size / 2; stride > 0; stride /= 2) {
                    if (local_id < stride) {
                        scratch[local_id] += scratch[local_id + stride];
                    }
                    barrier(CLK_LOCAL_MEM_FENCE);
                }
                
                if (local_id == 0) {
                    output[get_group_id(0)] = scratch[0];
                }
            }";

        var reductionSource_obj = new TextKernelSource(reductionSource, "reduction_sum", KernelLanguage.OpenCL, "reduction_sum");
        var reductionDef = new KernelDefinition("reduction_sum", reductionSource_obj, options);
        _kernels["Reduction"] = await _accelerator.CompileKernelAsync(reductionDef, options);

        // 1D Convolution kernel
        var convolutionSource = @"
            void convolution_1d(global const float* input, global const float* filter, 
                               global float* output, int input_size, int filter_size) {
                int id = get_global_id(0);
                
                if (id < input_size - filter_size + 1) {
                    float sum = 0.0f;
                    for (int i = 0; i < filter_size; i++) {
                        sum += input[id + i] * filter[i];
                    }
                    output[id] = sum;
                }
            }";

        var convolutionSource_obj = new TextKernelSource(convolutionSource, "convolution_1d", KernelLanguage.OpenCL, "convolution_1d");
        var convolutionDef = new KernelDefinition("convolution_1d", convolutionSource_obj, options);
        _kernels["Convolution"] = await _accelerator.CompileKernelAsync(convolutionDef, options);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var kernel in _kernels.Values)
        {
            await kernel.DisposeAsync();
        }
        _kernels.Clear();

        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
            {
                await buffer.DisposeAsync();
            }
        }
        _buffers.Clear();

        if (_acceleratorManager != null)
        {
            await _acceleratorManager.DisposeAsync();
        }
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
            {
                await buffer.DisposeAsync();
            }
        }
        _buffers.Clear();
    }

    [Benchmark(Baseline = true)]
    public async Task ExecuteKernel() => await ExecuteSpecificKernel(KernelType);

    private async Task ExecuteSpecificKernel(string kernelType)
    {
        switch (kernelType)
        {
            case "VectorAdd":
                await ExecuteVectorAdd();
                break;
            case "VectorMultiply":
                await ExecuteVectorMultiply();
                break;
            case "DotProduct":
                await ExecuteDotProduct();
                break;
            case "MatrixMultiply":
                await ExecuteMatrixMultiply();
                break;
            case "Reduction":
                await ExecuteReduction();
                break;
            case "Convolution":
                await ExecuteConvolution();
                break;
        }
    }

    private async Task ExecuteVectorAdd()
    {
        var bufferA = await _memoryManager.AllocateAndCopyAsync<float>(_inputA);
        var bufferB = await _memoryManager.AllocateAndCopyAsync<float>(_inputB);
        var bufferResult = await _memoryManager.AllocateAsync(DataSize * sizeof(float));

        var args = new KernelArguments(bufferA, bufferB, bufferResult, DataSize);
        await _kernels["VectorAdd"].ExecuteAsync(args);

        await bufferResult.CopyToHostAsync<float>(_output);

        _buffers.AddRange(new[] { bufferA, bufferB, bufferResult });
    }

    private async Task ExecuteVectorMultiply()
    {
        var bufferA = await _memoryManager.AllocateAndCopyAsync<float>(_inputA);
        var bufferB = await _memoryManager.AllocateAndCopyAsync<float>(_inputB);
        var bufferResult = await _memoryManager.AllocateAsync(DataSize * sizeof(float));

        var args = new KernelArguments(bufferA, bufferB, bufferResult, DataSize);
        await _kernels["VectorMultiply"].ExecuteAsync(args);

        await bufferResult.CopyToHostAsync<float>(_output);

        _buffers.AddRange(new[] { bufferA, bufferB, bufferResult });
    }

    private async Task ExecuteDotProduct()
    {
        var bufferA = await _memoryManager.AllocateAndCopyAsync<float>(_inputA);
        var bufferB = await _memoryManager.AllocateAndCopyAsync<float>(_inputB);

        const int workGroupSize = 256;
        var numWorkGroups = (DataSize + workGroupSize - 1) / workGroupSize;
        var bufferResult = await _memoryManager.AllocateAsync(numWorkGroups * sizeof(float));

        var args = new KernelArguments(bufferA, bufferB, bufferResult, IntPtr.Zero, DataSize);
        await _kernels["DotProduct"].ExecuteAsync(args);

        var partialResults = new float[numWorkGroups];
        await bufferResult.CopyToHostAsync<float>(partialResults);

        // Final reduction on CPU
        _output[0] = partialResults.Sum();

        _buffers.AddRange(new[] { bufferA, bufferB, bufferResult });
    }

    private async Task ExecuteMatrixMultiply()
    {
        var matrixSize = (int)Math.Sqrt(DataSize);
        if (matrixSize * matrixSize != DataSize)
        {
            matrixSize = 256; // Default size if not perfect square
        }

        var bufferA = await _memoryManager.AllocateAndCopyAsync<float>(_inputA.Take(matrixSize * matrixSize).ToArray());
        var bufferB = await _memoryManager.AllocateAndCopyAsync<float>(_inputB.Take(matrixSize * matrixSize).ToArray());
        var bufferResult = await _memoryManager.AllocateAsync(matrixSize * matrixSize * sizeof(float));

        var args = new KernelArguments(bufferA, bufferB, bufferResult, matrixSize, matrixSize, matrixSize);
        await _kernels["MatrixMultiply"].ExecuteAsync(args);

        var result = new float[matrixSize * matrixSize];
        await bufferResult.CopyToHostAsync<float>(result);

        _buffers.AddRange(new[] { bufferA, bufferB, bufferResult });
    }

    private async Task ExecuteReduction()
    {
        var bufferInput = await _memoryManager.AllocateAndCopyAsync<float>(_inputA);

        const int workGroupSize = 256;
        var numWorkGroups = (DataSize + workGroupSize - 1) / workGroupSize;
        var bufferResult = await _memoryManager.AllocateAsync(numWorkGroups * sizeof(float));

        var args = new KernelArguments(bufferInput, bufferResult, IntPtr.Zero, DataSize);
        await _kernels["Reduction"].ExecuteAsync(args);

        var partialResults = new float[numWorkGroups];
        await bufferResult.CopyToHostAsync<float>(partialResults);

        _output[0] = partialResults.Sum();

        _buffers.AddRange(new[] { bufferInput, bufferResult });
    }

    private async Task ExecuteConvolution()
    {
        const int filterSize = 5;
        var filter = new[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f }; // Simple average filter

        var bufferInput = await _memoryManager.AllocateAndCopyAsync<float>(_inputA);
        var bufferFilter = await _memoryManager.AllocateAndCopyAsync<float>(filter);
        var bufferResult = await _memoryManager.AllocateAsync((DataSize - filterSize + 1) * sizeof(float));

        var args = new KernelArguments(bufferInput, bufferFilter, bufferResult, DataSize, filterSize);
        await _kernels["Convolution"].ExecuteAsync(args);

        var result = new float[DataSize - filterSize + 1];
        await bufferResult.CopyToHostAsync<float>(result);

        _buffers.AddRange(new[] { bufferInput, bufferFilter, bufferResult });
    }

    [Benchmark]
    public async Task KernelThroughputTest()
    {
        const int iterations = 100;
        var start = DateTime.UtcNow;

        for (var i = 0; i < iterations; i++)
        {
            await ExecuteSpecificKernel("VectorAdd");
            // Clear buffers to prevent memory buildup
            foreach (var buffer in _buffers)
            {
                if (!buffer.IsDisposed)
                {
                    await buffer.DisposeAsync();
                }
            }
            _buffers.Clear();
        }

        var elapsed = DateTime.UtcNow - start;
        var throughput = iterations / elapsed.TotalSeconds;

        Console.WriteLine($"Kernel throughput: {throughput:F2} executions/second");
    }

    [Benchmark]
    public double CalculateGFLOPS()
    {
        // Calculate theoretical GFLOPS for the current kernel type
        var operations = KernelType switch
        {
            "VectorAdd" => DataSize, // 1 add per element
            "VectorMultiply" => DataSize, // 1 multiply per element
            "DotProduct" => DataSize * 2, // 1 multiply + 1 add per element
            "MatrixMultiply" => (long)Math.Sqrt(DataSize) * (long)Math.Sqrt(DataSize) * (long)Math.Sqrt(DataSize) * 2, // 2 ops per element
            "Reduction" => DataSize * 2, // Log reduction with 2 ops per level
            "Convolution" => DataSize * 5 * 2, // 5-point convolution with multiply-add
            _ => DataSize
        };

        var executionTime = 0.001; // Simulated 1ms execution time
        return (operations / executionTime) / 1e9; // GFLOPS
    }

    [Benchmark]
    public async Task MemoryBoundKernel()
    {
        // A kernel that is memory-bound rather than compute-bound
        var bufferA = await _memoryManager.AllocateAndCopyAsync<float>(_inputA);
        var bufferB = await _memoryManager.AllocateAndCopyAsync<float>(_inputB);
        var bufferC = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
        var bufferD = await _memoryManager.AllocateAsync(DataSize * sizeof(float));

        // Multiple memory operations with minimal compute
        await bufferA.CopyToHostAsync<float>(_output);
        await bufferC.CopyFromHostAsync<float>(_output);
        await bufferC.CopyToHostAsync<float>(_output);
        await bufferD.CopyFromHostAsync<float>(_output);

        _buffers.AddRange(new[] { bufferA, bufferB, bufferC, bufferD });
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
}}
