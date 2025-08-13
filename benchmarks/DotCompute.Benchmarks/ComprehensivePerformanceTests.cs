using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace DotCompute.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
[RPlotExporter]
[HtmlExporter]
public class ComprehensivePerformanceTests
{
    private IAcceleratorManager _acceleratorManager = null!;
    private IComputeEngine _computeEngine = null!;
    private IMemoryManager _memoryManager = null!;
    
    [Params(1000, 10000, 100000)]
    public int WorkloadSize { get; set; }
    
    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);
        
        var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
        _acceleratorManager.RegisterProvider(cpuProvider);
        await _acceleratorManager.InitializeAsync();
        
        _computeEngine = new DefaultComputeEngine(_acceleratorManager, new NullLogger<DefaultComputeEngine>());
        _memoryManager = _acceleratorManager.Default.Memory;
    }
    
    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _computeEngine.DisposeAsync();
        await _acceleratorManager.DisposeAsync();
    }
    
    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public async Task<float> CompleteWorkflow_VectorOperation()
    {
        // Generate data
        var a = new float[WorkloadSize];
        var b = new float[WorkloadSize];
        for (int i = 0; i < WorkloadSize; i++)
        {
            a[i] = i;
            b[i] = i * 2;
        }
        
        // Allocate buffers
        var bufferA = await _memoryManager.AllocateAndCopyAsync<float>(a);
        var bufferB = await _memoryManager.AllocateAndCopyAsync<float>(b);
        var bufferResult = await _memoryManager.AllocateAsync(WorkloadSize * sizeof(float));
        
        // Compile kernel
        var kernel = await _computeEngine.CompileKernelAsync(@"
            __kernel void vector_add(__global float* a, __global float* b, __global float* result) {
                int i = get_global_id(0);
                result[i] = a[i] + b[i];
            }", "vector_add");
        
        // Execute
        await _computeEngine.ExecuteAsync(
            kernel,
            new object[] { bufferA, bufferB, bufferResult },
            ComputeBackendType.CPU);
        
        // Read back result
        var result = new float[WorkloadSize];
        await bufferResult.CopyToHostAsync<float>(result);
        
        // Cleanup
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferResult.DisposeAsync();
        await kernel.DisposeAsync();
        
        return result[WorkloadSize / 2]; // Return middle element as verification
    }
    
    [Benchmark]
    [BenchmarkCategory("Scalability")]
    public async Task ParallelKernelExecutions()
    {
        const int parallelCount = 4;
        var tasks = new Task[parallelCount];
        
        for (int i = 0; i < parallelCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                var data = new float[WorkloadSize / parallelCount];
                var buffer = await _memoryManager.AllocateAndCopyAsync<float>(data);
                
                // Simulate work
                await Task.Delay(1);
                
                var result = new float[data.Length];
                await buffer.CopyToHostAsync<float>(result);
                await buffer.DisposeAsync();
            });
        }
        
        await Task.WhenAll(tasks);
    }
    
    [Benchmark]
    [BenchmarkCategory("Memory")]
    public async Task MemoryStressTest()
    {
        const int iterations = 100;
        var buffers = new List<IMemoryBuffer>();
        
        // Allocation phase
        for (int i = 0; i < iterations; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(1024);
            buffers.Add(buffer);
        }
        
        // Deallocation phase
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }
    
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public async Task<double> MeasureThroughput()
    {
        var sw = Stopwatch.StartNew();
        const int operations = 1000;
        var bytesProcessed = 0L;
        
        for (int i = 0; i < operations; i++)
        {
            var size = Random.Shared.Next(1024, 10240);
            var data = new byte[size];
            
            var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(data);
            bytesProcessed += size;
            await buffer.DisposeAsync();
        }
        
        sw.Stop();
        
        // Return throughput in MB/s
        return (bytesProcessed / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
    }
    
    [Benchmark]
    [BenchmarkCategory("Latency")]
    public async Task<double> MeasureLatency()
    {
        const int samples = 100;
        var latencies = new List<double>(samples);
        
        for (int i = 0; i < samples; i++)
        {
            var sw = Stopwatch.StartNew();
            
            var buffer = await _memoryManager.AllocateAsync(1024);
            await buffer.DisposeAsync();
            
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMicroseconds);
        }
        
        latencies.Sort();
        return latencies[latencies.Count / 2]; // Return median latency in microseconds
    }
}
