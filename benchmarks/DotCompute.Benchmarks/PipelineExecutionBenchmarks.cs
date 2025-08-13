using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Core.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for pipeline orchestration overhead and execution performance.
/// Tests various pipeline configurations, stage compositions, and memory management overhead.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class PipelineExecutionBenchmarks
{
    private IAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _accelerator = null!;
    private IMemoryManager _memoryManager = null!;
    private readonly List<IMemoryBuffer> _buffers = new();
    private readonly List<IKernelPipeline> _pipelines = new();

    [Params(1024, 64 * 1024, 1024 * 1024, 4 * 1024 * 1024)]
    public int DataSize { get; set; }

    [Params(1, 3, 5, 10)]
    public int PipelineStages { get; set; }

    private float[] _inputData = null!;
    private float[] _outputData = null!;

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
        
        SetupTestData();
        await SetupPipelines();
    }

    private void SetupTestData()
    {
        _inputData = new float[DataSize];
        _outputData = new float[DataSize];
        
        var random = new Random(42);
        for (int i = 0; i < DataSize; i++)
        {
            _inputData[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }
    }

    private async Task SetupPipelines()
    {
        // Create simple processing pipelines
        for (int stages = 1; stages <= 10; stages++)
        {
            var pipelineBuilder = new KernelPipelineBuilder(_accelerator);
            
            // Add sequential processing stages
            for (int stage = 0; stage < stages; stage++)
            {
                pipelineBuilder.AddStage($"stage_{stage}", CreateSimpleProcessingStage(stage));
            }
            
            var pipeline = pipelineBuilder.Build();
            _pipelines.Add(pipeline);
        }
    }

    private IPipelineStage CreateSimpleProcessingStage(int stageIndex)
    {
        return new SimplePipelineStage($"SimpleStage_{stageIndex}", async (input, output, context) =>
        {
            // Simulate some processing work
            var buffer = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
            
            // Copy input to buffer, do some work, then copy to output
            await buffer.CopyFromHostAsync(_inputData);
            
            // Simulate processing time
            await Task.Delay(1);
            
            await buffer.CopyToHostAsync(_outputData);
            await buffer.DisposeAsync();
        });
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var pipeline in _pipelines)
        {
            await pipeline.DisposeAsync();
        }
        _pipelines.Clear();
        
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
    public async Task SingleStageExecution()
    {
        await ExecutePipelineWithStages(1);
    }

    [Benchmark]
    public async Task MultiStageExecution()
    {
        await ExecutePipelineWithStages(PipelineStages);
    }

    private async Task ExecutePipelineWithStages(int stageCount)
    {
        if (stageCount > _pipelines.Count)
            stageCount = _pipelines.Count;
        
        var pipeline = _pipelines[stageCount - 1];
        
        var inputBuffer = await _memoryManager.AllocateAndCopyAsync(_inputData);
        var outputBuffer = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
        
        // Execute pipeline
        await pipeline.ExecuteAsync(inputBuffer, outputBuffer);
        
        // Read back results
        await outputBuffer.CopyToHostAsync(_outputData);
        
        _buffers.Add(inputBuffer);
        _buffers.Add(outputBuffer);
    }

    [Benchmark]
    public async Task PipelineWithMemoryOptimization()
    {
        var pipelineBuilder = new KernelPipelineBuilder(_accelerator);
        
        // Add stages with memory optimization
        for (int stage = 0; stage < PipelineStages; stage++)
        {
            pipelineBuilder.AddStage($"optimized_stage_{stage}", CreateOptimizedProcessingStage(stage));
        }
        
        // Enable memory optimization
        pipelineBuilder.WithMemoryOptimization(true);
        pipelineBuilder.WithBufferReuse(true);
        
        var pipeline = pipelineBuilder.Build();
        
        var inputBuffer = await _memoryManager.AllocateAndCopyAsync(_inputData);
        var outputBuffer = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
        
        await pipeline.ExecuteAsync(inputBuffer, outputBuffer);
        await outputBuffer.CopyToHostAsync(_outputData);
        
        _buffers.Add(inputBuffer);
        _buffers.Add(outputBuffer);
        await pipeline.DisposeAsync();
    }

    private IPipelineStage CreateOptimizedProcessingStage(int stageIndex)
    {
        return new OptimizedPipelineStage($"OptimizedStage_{stageIndex}", async (input, output, context) =>
        {
            // Use in-place operations when possible
            // Reuse buffers from context if available
            
            var workBuffer = context.TryGetBuffer($"work_{stageIndex}") ??
                           await _memoryManager.AllocateAsync(DataSize * sizeof(float));
            
            // Simulate optimized processing
            await workBuffer.CopyFromHostAsync(_inputData);
            await Task.Delay(1); // Simulated work
            await workBuffer.CopyToHostAsync(_outputData);
            
            context.SetBuffer($"work_{stageIndex}", workBuffer);
        });
    }

    [Benchmark]
    public async Task ParallelPipelineExecution()
    {
        const int parallelPipelines = 4;
        var tasks = new List<Task>();
        
        for (int i = 0; i < parallelPipelines; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var pipeline = _pipelines[Math.Min(PipelineStages - 1, _pipelines.Count - 1)];
                var inputBuffer = await _memoryManager.AllocateAndCopyAsync(_inputData);
                var outputBuffer = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
                
                await pipeline.ExecuteAsync(inputBuffer, outputBuffer);
                await outputBuffer.CopyToHostAsync(_outputData);
                
                lock (_buffers)
                {
                    _buffers.Add(inputBuffer);
                    _buffers.Add(outputBuffer);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task PipelineWithErrorHandling()
    {
        var pipelineBuilder = new KernelPipelineBuilder(_accelerator);
        
        // Add stages with error handling
        for (int stage = 0; stage < PipelineStages; stage++)
        {
            pipelineBuilder.AddStage($"error_safe_stage_{stage}", CreateErrorSafeProcessingStage(stage));
        }
        
        pipelineBuilder.WithErrorRecovery(true);
        var pipeline = pipelineBuilder.Build();
        
        var inputBuffer = await _memoryManager.AllocateAndCopyAsync(_inputData);
        var outputBuffer = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
        
        try
        {
            await pipeline.ExecuteAsync(inputBuffer, outputBuffer);
            await outputBuffer.CopyToHostAsync(_outputData);
        }
        catch (Exception ex)
        {
            // Handle pipeline errors gracefully
            Console.WriteLine($"Pipeline error: {ex.Message}");
        }
        
        _buffers.Add(inputBuffer);
        _buffers.Add(outputBuffer);
        await pipeline.DisposeAsync();
    }

    private IPipelineStage CreateErrorSafeProcessingStage(int stageIndex)
    {
        return new ErrorSafePipelineStage($"ErrorSafeStage_{stageIndex}", async (input, output, context) =>
        {
            try
            {
                var buffer = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
                await buffer.CopyFromHostAsync(_inputData);
                
                // Simulate processing that might fail
                if (stageIndex == 2 && Random.Shared.NextDouble() < 0.1) // 10% chance of failure
                {
                    throw new InvalidOperationException($"Simulated error in stage {stageIndex}");
                }
                
                await Task.Delay(1);
                await buffer.CopyToHostAsync(_outputData);
                await buffer.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Log error and continue with fallback processing
                Console.WriteLine($"Stage {stageIndex} error: {ex.Message}");
                
                // Fallback: pass input directly to output
                await input.CopyToHostAsync(_outputData);
            }
        });
    }

    [Benchmark]
    public async Task StreamingPipelineExecution()
    {
        const int streamChunks = 8;
        var chunkSize = DataSize / streamChunks;
        
        var pipelineBuilder = new KernelPipelineBuilder(_accelerator);
        pipelineBuilder.AddStage("streaming_stage", CreateStreamingProcessingStage());
        pipelineBuilder.WithStreamingSupport(true);
        
        var pipeline = pipelineBuilder.Build();
        
        var tasks = new List<Task>();
        
        for (int chunk = 0; chunk < streamChunks; chunk++)
        {
            int chunkIndex = chunk;
            tasks.Add(Task.Run(async () =>
            {
                var chunkData = _inputData.Skip(chunkIndex * chunkSize).Take(chunkSize).ToArray();
                var inputBuffer = await _memoryManager.AllocateAndCopyAsync(chunkData);
                var outputBuffer = await _memoryManager.AllocateAsync(chunkSize * sizeof(float));
                
                await pipeline.ExecuteAsync(inputBuffer, outputBuffer);
                
                var resultChunk = new float[chunkSize];
                await outputBuffer.CopyToHostAsync(resultChunk);
                
                lock (_buffers)
                {
                    _buffers.Add(inputBuffer);
                    _buffers.Add(outputBuffer);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        await pipeline.DisposeAsync();
    }

    private IPipelineStage CreateStreamingProcessingStage()
    {
        return new StreamingPipelineStage("StreamingStage", async (input, output, context) =>
        {
            // Process streaming data chunk
            var buffer = await _memoryManager.AllocateAsync(input.SizeInBytes);
            
            // Copy input chunk
            var tempData = new float[input.SizeInBytes / sizeof(float)];
            await input.CopyToHostAsync(tempData);
            
            // Process chunk (simple transformation)
            for (int i = 0; i < tempData.Length; i++)
            {
                tempData[i] = tempData[i] * 1.5f + 0.1f;
            }
            
            await buffer.CopyFromHostAsync(tempData);
            await buffer.CopyToHostAsync(tempData); // Copy to output buffer
            await buffer.DisposeAsync();
        });
    }

    [Benchmark]
    public double PipelineOverheadCalculation()
    {
        // Calculate pipeline orchestration overhead
        var singleStageTime = 10.0; // ms (simulated)
        var multiStageTime = PipelineStages * 12.0; // ms (simulated with overhead)
        var expectedTime = PipelineStages * singleStageTime;
        
        return (multiStageTime - expectedTime) / expectedTime * 100.0; // Overhead percentage
    }

    [Benchmark]
    public async Task PipelineThroughputTest()
    {
        const int iterations = 50;
        var pipeline = _pipelines[Math.Min(PipelineStages - 1, _pipelines.Count - 1)];
        
        var start = DateTime.UtcNow;
        
        for (int i = 0; i < iterations; i++)
        {
            var inputBuffer = await _memoryManager.AllocateAndCopyAsync(_inputData);
            var outputBuffer = await _memoryManager.AllocateAsync(DataSize * sizeof(float));
            
            await pipeline.ExecuteAsync(inputBuffer, outputBuffer);
            
            await inputBuffer.DisposeAsync();
            await outputBuffer.DisposeAsync();
        }
        
        var elapsed = DateTime.UtcNow - start;
        var throughput = iterations / elapsed.TotalSeconds;
        
        Console.WriteLine($"Pipeline throughput: {throughput:F2} executions/second");
    }
}

// Helper classes for pipeline stages
public class SimplePipelineStage : IPipelineStage
{
    public string Name { get; }
    private readonly Func<IMemoryBuffer, IMemoryBuffer, object, Task> _executeFunc;

    public SimplePipelineStage(string name, Func<IMemoryBuffer, IMemoryBuffer, object, Task> executeFunc)
    {
        Name = name;
        _executeFunc = executeFunc;
    }

    public Task ExecuteAsync(IMemoryBuffer input, IMemoryBuffer output, object context = null!)
    {
        return _executeFunc(input, output, context);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class OptimizedPipelineStage : IPipelineStage
{
    public string Name { get; }
    private readonly Func<IMemoryBuffer, IMemoryBuffer, PipelineContext, Task> _executeFunc;

    public OptimizedPipelineStage(string name, Func<IMemoryBuffer, IMemoryBuffer, PipelineContext, Task> executeFunc)
    {
        Name = name;
        _executeFunc = executeFunc;
    }

    public Task ExecuteAsync(IMemoryBuffer input, IMemoryBuffer output, object context = null!)
    {
        var pipelineContext = context as PipelineContext ?? new PipelineContext();
        return _executeFunc(input, output, pipelineContext);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class ErrorSafePipelineStage : IPipelineStage
{
    public string Name { get; }
    private readonly Func<IMemoryBuffer, IMemoryBuffer, object, Task> _executeFunc;

    public ErrorSafePipelineStage(string name, Func<IMemoryBuffer, IMemoryBuffer, object, Task> executeFunc)
    {
        Name = name;
        _executeFunc = executeFunc;
    }

    public Task ExecuteAsync(IMemoryBuffer input, IMemoryBuffer output, object context = null!)
    {
        return _executeFunc(input, output, context);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class StreamingPipelineStage : IPipelineStage
{
    public string Name { get; }
    private readonly Func<IMemoryBuffer, IMemoryBuffer, object, Task> _executeFunc;

    public StreamingPipelineStage(string name, Func<IMemoryBuffer, IMemoryBuffer, object, Task> executeFunc)
    {
        Name = name;
        _executeFunc = executeFunc;
    }

    public Task ExecuteAsync(IMemoryBuffer input, IMemoryBuffer output, object context = null!)
    {
        return _executeFunc(input, output, context);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class PipelineContext
{
    private readonly Dictionary<string, IMemoryBuffer> _buffers = new();

    public IMemoryBuffer? TryGetBuffer(string key)
    {
        return _buffers.TryGetValue(key, out var buffer) ? buffer : null;
    }

    public void SetBuffer(string key, IMemoryBuffer buffer)
    {
        _buffers[key] = buffer;
    }
}