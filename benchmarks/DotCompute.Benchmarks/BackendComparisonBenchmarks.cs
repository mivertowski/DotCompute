using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for comparing CPU vs simulated GPU backend performance.
/// Tests relative performance, overhead differences, and scalability characteristics.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class BackendComparisonBenchmarks
{
    private IAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _cpuAccelerator = null!;
    private IAccelerator? _gpuAccelerator;
    private readonly List<IMemoryBuffer> _buffers = new();

    [Params("CPU", "GPU", "Both")]
    public string BackendType { get; set; } = "CPU";

    [Params(1024, 16384, 65536, 262144, 1048576)]
    public int DataSize { get; set; }

    [Params("VectorAdd", "MatrixMultiply", "Reduction", "Convolution")]
    public string WorkloadType { get; set; } = "VectorAdd";

    private float[] _inputA = null!;
    private float[] _inputB = null!;
    private float[] _outputCpu = null!;
    private float[] _outputGpu = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);
        
        // Setup CPU backend
        var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
        _acceleratorManager.RegisterProvider(cpuProvider);
        
        // Setup simulated GPU backend (using CPU with different characteristics)
        var simulatedGpuProvider = new CpuAcceleratorProvider(
            new NullLogger<CpuAcceleratorProvider>(),
            "SimulatedGPU"
        );
        _acceleratorManager.RegisterProvider(simulatedGpuProvider);
        
        await _acceleratorManager.InitializeAsync();
        
        var accelerators = _acceleratorManager.GetAccelerators().ToList();
        _cpuAccelerator = accelerators.First(a => a.Info.Name.Contains("CPU"));
        _gpuAccelerator = accelerators.FirstOrDefault(a => a.Info.Name.Contains("SimulatedGPU"));
        
        SetupTestData();
    }

    private void SetupTestData()
    {
        _inputA = new float[DataSize];
        _inputB = new float[DataSize];
        _outputCpu = new float[DataSize];
        _outputGpu = new float[DataSize];

        var random = new Random(42);
        for (int i = 0; i < DataSize; i++)
        {
            _inputA[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            _inputB[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
                await buffer.DisposeAsync();
        }
        _buffers.Clear();
        
        await _acceleratorManager.DisposeAsync();
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
                await buffer.DisposeAsync();
        }
        _buffers.Clear();
    }

    [Benchmark(Baseline = true)]
    public async Task CpuBackendExecution()
    {
        await ExecuteWorkloadOnBackend(_cpuAccelerator, _outputCpu);
    }

    [Benchmark]
    public async Task GpuBackendExecution()
    {
        if (_gpuAccelerator != null)
        {
            await ExecuteWorkloadOnBackend(_gpuAccelerator, _outputGpu);
        }
        else
        {
            await ExecuteWorkloadOnBackend(_cpuAccelerator, _outputGpu);
        }
    }

    [Benchmark]
    public async Task BackendComparison()
    {
        // Execute same workload on both backends and compare
        await ExecuteWorkloadType(BackendType);
    }

    private async Task ExecuteWorkloadType(string backendType)
    {
        switch (backendType)
        {
            case "CPU":
                await ExecuteWorkloadOnBackend(_cpuAccelerator, _outputCpu);
                break;
            case "GPU":
                if (_gpuAccelerator != null)
                    await ExecuteWorkloadOnBackend(_gpuAccelerator, _outputGpu);
                break;
            case "Both":
                var cpuTask = ExecuteWorkloadOnBackend(_cpuAccelerator, _outputCpu);
                var gpuTask = _gpuAccelerator != null 
                    ? ExecuteWorkloadOnBackend(_gpuAccelerator, _outputGpu)
                    : Task.CompletedTask;
                await Task.WhenAll(cpuTask, gpuTask);
                break;
        }
    }

    private async Task ExecuteWorkloadOnBackend(IAccelerator accelerator, float[] output)
    {
        switch (WorkloadType)
        {
            case "VectorAdd":
                await ExecuteVectorAddition(accelerator, output);
                break;
            case "MatrixMultiply":
                await ExecuteMatrixMultiplication(accelerator, output);
                break;
            case "Reduction":
                await ExecuteReduction(accelerator, output);
                break;
            case "Convolution":
                await ExecuteConvolution(accelerator, output);
                break;
        }
    }

    private async Task ExecuteVectorAddition(IAccelerator accelerator, float[] output)
    {
        var bufferA = await accelerator.Memory.AllocateAndCopyAsync(_inputA);
        var bufferB = await accelerator.Memory.AllocateAndCopyAsync(_inputB);
        var bufferResult = await accelerator.Memory.AllocateAsync(DataSize * sizeof(float));
        
        // Simulate vector addition kernel execution
        await SimulateKernelExecution(accelerator, "VectorAdd", new[] { bufferA, bufferB, bufferResult });
        
        // Copy result back
        await bufferResult.CopyToHostAsync<float>(output);
        
        _buffers.AddRange(new[] { bufferA, bufferB, bufferResult });
    }

    private async Task ExecuteMatrixMultiplication(IAccelerator accelerator, float[] output)
    {
        int matrixSize = (int)Math.Sqrt(DataSize);
        if (matrixSize * matrixSize != DataSize)
        {
            matrixSize = (int)Math.Sqrt(Math.Min(DataSize, 65536)); // Reasonable default
        }
        
        var actualSize = matrixSize * matrixSize;
        var inputA = _inputA.Take(actualSize).ToArray();
        var inputB = _inputB.Take(actualSize).ToArray();
        
        var bufferA = await accelerator.Memory.AllocateAndCopyAsync(inputA);
        var bufferB = await accelerator.Memory.AllocateAndCopyAsync(inputB);
        var bufferResult = await accelerator.Memory.AllocateAsync(actualSize * sizeof(float));
        
        // Simulate matrix multiplication - more compute-intensive for GPU
        var executionTime = accelerator.Info.Name.Contains("SimulatedGPU") ? 10 : 50; // GPU should be faster
        await SimulateKernelExecution(accelerator, "MatrixMultiply", new[] { bufferA, bufferB, bufferResult }, executionTime);
        
        var result = new float[actualSize];
        await bufferResult.CopyToHostAsync<float>(result);
        
        // Copy to output (pad if necessary)
        Array.Copy(result, 0, output, 0, Math.Min(result.Length, output.Length));
        
        _buffers.AddRange(new[] { bufferA, bufferB, bufferResult });
    }

    private async Task ExecuteReduction(IAccelerator accelerator, float[] output)
    {
        var bufferInput = await accelerator.Memory.AllocateAndCopyAsync(_inputA);
        var bufferResult = await accelerator.Memory.AllocateAsync(sizeof(float));
        
        // Reduction is typically more efficient on GPU due to parallel tree reduction
        var executionTime = accelerator.Info.Name.Contains("SimulatedGPU") ? 5 : 20;
        await SimulateKernelExecution(accelerator, "Reduction", new[] { bufferInput, bufferResult }, executionTime);
        
        var result = new float[1];
        await bufferResult.CopyToHostAsync<float>(result);
        output[0] = result[0];
        
        _buffers.AddRange(new[] { bufferInput, bufferResult });
    }

    private async Task ExecuteConvolution(IAccelerator accelerator, float[] output)
    {
        const int filterSize = 5;
        var filter = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };
        
        var bufferInput = await accelerator.Memory.AllocateAndCopyAsync(_inputA);
        var bufferFilter = await accelerator.Memory.AllocateAndCopyAsync(filter);
        var outputSize = DataSize - filterSize + 1;
        var bufferResult = await accelerator.Memory.AllocateAsync(outputSize * sizeof(float));
        
        // Convolution benefits from GPU parallel processing
        var executionTime = accelerator.Info.Name.Contains("SimulatedGPU") ? 8 : 25;
        await SimulateKernelExecution(accelerator, "Convolution", new[] { bufferInput, bufferFilter, bufferResult }, executionTime);
        
        var result = new float[outputSize];
        await bufferResult.CopyToHostAsync<float>(result);
        
        // Copy to output
        Array.Copy(result, 0, output, 0, Math.Min(result.Length, output.Length));
        
        _buffers.AddRange(new[] { bufferInput, bufferFilter, bufferResult });
    }

    private async Task SimulateKernelExecution(IAccelerator accelerator, string kernelType, IMemoryBuffer[] buffers, int baseExecutionTime = 10)
    {
        // Simulate different execution characteristics for different backends
        var executionTime = baseExecutionTime;
        
        if (accelerator.Info.Name.Contains("SimulatedGPU"))
        {
            // GPU characteristics: higher setup overhead, better scaling
            var setupOverhead = 5; // ms
            var scalingFactor = Math.Log10(DataSize) / 10.0; // Better scaling with data size
            executionTime = (int)(setupOverhead + baseExecutionTime * scalingFactor);
        }
        else
        {
            // CPU characteristics: lower setup overhead, linear scaling
            var scalingFactor = (double)DataSize / 1024.0 / 1024.0; // Linear with data size
            executionTime = (int)(baseExecutionTime * Math.Max(0.1, scalingFactor));
        }
        
        // Simulate memory bandwidth limitations
        var memoryTransferTime = CalculateMemoryTransferTime(buffers, accelerator);
        
        // Simulate actual execution
        await Task.Delay(Math.Max(1, executionTime + memoryTransferTime));
        
        // Simulate synchronization overhead
        if (accelerator.Info.Name.Contains("SimulatedGPU"))
        {
            await Task.Delay(1); // GPU sync overhead
        }
    }

    private int CalculateMemoryTransferTime(IMemoryBuffer[] buffers, IAccelerator accelerator)
    {
        var totalBytes = buffers.Sum(b => b.SizeInBytes);
        
        // Different memory bandwidth for different backends
        var bandwidth = accelerator.Info.Name.Contains("SimulatedGPU") 
            ? 500.0 * 1024 * 1024 * 1024 // 500 GB/s for simulated GPU
            : 50.0 * 1024 * 1024 * 1024;  // 50 GB/s for CPU
        
        var transferTime = (totalBytes / bandwidth) * 1000; // Convert to milliseconds
        return Math.Max(1, (int)transferTime);
    }

    [Benchmark]
    public async Task MemoryBandwidthComparison()
    {
        const int largeDataSize = 64 * 1024 * 1024; // 64MB
        var largeData = new float[largeDataSize];
        
        // CPU memory transfer
        var cpuBuffer = await _cpuAccelerator.Memory.AllocateAndCopyAsync(largeData);
        var cpuResult = new float[largeDataSize];
        await cpuBuffer.CopyToHostAsync(cpuResult);
        _buffers.Add(cpuBuffer);
        
        // GPU memory transfer (if available)
        if (_gpuAccelerator != null)
        {
            var gpuBuffer = await _gpuAccelerator.Memory.AllocateAndCopyAsync(largeData);
            var gpuResult = new float[largeDataSize];
            await gpuBuffer.CopyToHostAsync(gpuResult);
            _buffers.Add(gpuBuffer);
        }
    }

    [Benchmark]
    public async Task KernelCompilationComparison()
    {
        // Compare kernel compilation time between backends
        var kernelSource = @"
            void test_kernel(global const float* input, global float* output, int size) {
                int id = get_global_id(0);
                if (id < size) {
                    output[id] = input[id] * 2.0f + 1.0f;
                }
            }";
        
        var kernelDef = new KernelDefinition
        {
            Name = "test_kernel",
            Code = System.Text.Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "test_kernel"
        };
        
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Release
        };
        
        // Compile on CPU
        var cpuKernel = await _cpuAccelerator.CompileKernelAsync(kernelDef, options);
        
        // Compile on GPU (if available)
        ICompiledKernel? gpuKernel = null;
        if (_gpuAccelerator != null)
        {
            gpuKernel = await _gpuAccelerator.CompileKernelAsync(kernelDef, options);
        }
        
        // Execute kernels
        var bufferA = await _cpuAccelerator.Memory.AllocateAndCopyAsync(_inputA);
        var bufferResult = await _cpuAccelerator.Memory.AllocateAsync(DataSize * sizeof(float));
        
        var args = new KernelArguments(bufferA, bufferResult, DataSize);
        await cpuKernel.ExecuteAsync(args);
        
        _buffers.AddRange(new[] { bufferA, bufferResult });
        
        await cpuKernel.DisposeAsync();
        if (gpuKernel != null)
            await gpuKernel.DisposeAsync();
    }

    [Benchmark]
    public double PerformanceRatio()
    {
        // Calculate relative performance ratio
        var cpuTime = SimulateCpuExecutionTime(WorkloadType, DataSize);
        var gpuTime = SimulateGpuExecutionTime(WorkloadType, DataSize);
        
        return cpuTime / gpuTime; // Ratio > 1 means GPU is faster
    }

    private double SimulateCpuExecutionTime(string workload, int dataSize)
    {
        // Simulate CPU execution time based on workload characteristics
        return workload switch
        {
            "VectorAdd" => dataSize * 0.001, // Linear with data size
            "MatrixMultiply" => Math.Pow(Math.Sqrt(dataSize), 3) * 0.00001, // O(n^3)
            "Reduction" => dataSize * 0.002, // Linear but with more overhead
            "Convolution" => dataSize * 0.005, // More compute-intensive
            _ => dataSize * 0.001
        };
    }

    private double SimulateGpuExecutionTime(string workload, int dataSize)
    {
        // Simulate GPU execution time (generally better scaling)
        var baseTime = workload switch
        {
            "VectorAdd" => dataSize * 0.0002, // 5x faster than CPU
            "MatrixMultiply" => Math.Pow(Math.Sqrt(dataSize), 3) * 0.000001, // 10x faster than CPU
            "Reduction" => dataSize * 0.0001, // 20x faster due to parallel reduction
            "Convolution" => dataSize * 0.0005, // 10x faster than CPU
            _ => dataSize * 0.0002
        };
        
        // Add GPU setup overhead
        var setupOverhead = 5.0; // milliseconds
        return baseTime + setupOverhead;
    }

    [Benchmark]
    public async Task PowerEfficiencySimulation()
    {
        // Simulate power consumption comparison
        var workTime = 100; // ms
        
        // CPU: Lower peak power, longer execution
        var cpuPower = 65; // watts
        var cpuTime = workTime * 2;
        var cpuEnergy = cpuPower * cpuTime / 1000.0; // joules
        
        // GPU: Higher peak power, shorter execution  
        var gpuPower = 250; // watts
        var gpuTime = workTime * 0.5;
        var gpuEnergy = gpuPower * gpuTime / 1000.0; // joules
        
        await Task.Delay(Math.Max((int)cpuTime, (int)gpuTime));
        
        // Store energy efficiency metric
        _outputCpu[0] = (float)cpuEnergy;
        _outputGpu[0] = (float)gpuEnergy;
    }

    [Benchmark]
    public async Task ConcurrentExecutionComparison()
    {
        // Test how well each backend handles concurrent execution
        const int concurrentTasks = 4;
        var tasks = new List<Task>();
        
        for (int i = 0; i < concurrentTasks; i++)
        {
            tasks.Add(ExecuteWorkloadOnBackend(_cpuAccelerator, _outputCpu));
            
            if (_gpuAccelerator != null)
            {
                tasks.Add(ExecuteWorkloadOnBackend(_gpuAccelerator, _outputGpu));
            }
        }
        
        await Task.WhenAll(tasks);
    }
}