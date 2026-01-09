// Sample 5: Distributed Actors with Ring Kernels
// Demonstrates: Ring kernel system, actor model, persistent GPU computation
// Complexity: Advanced

using DotCompute;
using DotCompute.Abstractions;
using DotCompute.RingKernels;
using MemoryPack;

namespace DotCompute.Samples.DistributedActors;

/// <summary>
/// Distributed actor system using DotCompute Ring Kernels.
/// Demonstrates persistent GPU computation with message passing.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("DotCompute Distributed Actors Sample");
        Console.WriteLine("=====================================\n");

        // Create accelerator
        await using var accelerator = await AcceleratorFactory.CreateAsync();
        Console.WriteLine($"Using: {accelerator.Name}\n");

        // Create ring kernel runtime
        await using var runtime = await RingKernelRuntime.CreateAsync(accelerator, new RingKernelConfig
        {
            MaxMessageSize = 4096,
            QueueCapacity = 1024,
            ExecutionMode = ExecutionMode.EventDriven // WSL2 compatible
        });

        // Demo 1: Simple ping-pong actor
        Console.WriteLine("=== Demo 1: Ping-Pong Actor ===\n");
        await DemoPingPong(runtime);

        // Demo 2: Parallel worker pool
        Console.WriteLine("\n=== Demo 2: Worker Pool ===\n");
        await DemoWorkerPool(runtime);

        // Demo 3: Data pipeline actors
        Console.WriteLine("\n=== Demo 3: Data Pipeline ===\n");
        await DemoDataPipeline(runtime);

        // Demo 4: Aggregation pattern
        Console.WriteLine("\n=== Demo 4: Map-Reduce Pattern ===\n");
        await DemoMapReduce(runtime);

        Console.WriteLine("\nAll demos completed successfully!");
    }

    /// <summary>
    /// Demo 1: Simple request-response pattern with ping-pong messages.
    /// </summary>
    static async Task DemoPingPong(RingKernelRuntime runtime)
    {
        // Create ping-pong actor
        var actor = await runtime.SpawnAsync<PingPongActor>("ping-pong");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 100;

        for (int i = 0; i < iterations; i++)
        {
            var request = new PingMessage { SequenceNumber = i, Timestamp = DateTime.UtcNow.Ticks };
            var response = await actor.AskAsync<PingMessage, PongMessage>(request);

            if (i < 5 || i == iterations - 1)
            {
                Console.WriteLine($"  Ping #{i} -> Pong (latency: {(DateTime.UtcNow.Ticks - request.Timestamp) / 10}μs)");
            }
            else if (i == 5)
            {
                Console.WriteLine("  ...");
            }
        }

        sw.Stop();
        Console.WriteLine($"  Completed {iterations} round-trips in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Average latency: {sw.Elapsed.TotalMicroseconds / iterations:F1}μs");

        await actor.StopAsync();
    }

    /// <summary>
    /// Demo 2: Parallel worker pool processing tasks concurrently.
    /// </summary>
    static async Task DemoWorkerPool(RingKernelRuntime runtime)
    {
        const int numWorkers = 4;
        const int numTasks = 50;

        // Spawn worker actors
        var workers = new List<ActorRef<ComputeWorkerActor>>();
        for (int i = 0; i < numWorkers; i++)
        {
            workers.Add(await runtime.SpawnAsync<ComputeWorkerActor>($"worker-{i}"));
        }

        Console.WriteLine($"  Spawned {numWorkers} worker actors");

        // Generate work items
        var tasks = Enumerable.Range(0, numTasks)
            .Select(i => new ComputeTask
            {
                TaskId = i,
                VectorSize = 10000,
                Operation = ComputeOperation.VectorAdd
            })
            .ToList();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Distribute tasks round-robin
        var pendingResults = new List<Task<ComputeResult>>();
        for (int i = 0; i < tasks.Count; i++)
        {
            var worker = workers[i % numWorkers];
            pendingResults.Add(worker.AskAsync<ComputeTask, ComputeResult>(tasks[i]));
        }

        // Wait for all results
        var results = await Task.WhenAll(pendingResults);
        sw.Stop();

        float totalOps = results.Sum(r => r.OperationsPerformed);
        Console.WriteLine($"  Processed {numTasks} tasks across {numWorkers} workers");
        Console.WriteLine($"  Total operations: {totalOps:N0}");
        Console.WriteLine($"  Throughput: {totalOps / sw.Elapsed.TotalSeconds / 1e6:F2}M ops/sec");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms");

        // Cleanup
        foreach (var worker in workers)
        {
            await worker.StopAsync();
        }
    }

    /// <summary>
    /// Demo 3: Multi-stage data pipeline with GPU acceleration.
    /// </summary>
    static async Task DemoDataPipeline(RingKernelRuntime runtime)
    {
        // Create pipeline stages
        var parser = await runtime.SpawnAsync<DataParserActor>("parser");
        var transformer = await runtime.SpawnAsync<DataTransformerActor>("transformer");
        var aggregator = await runtime.SpawnAsync<DataAggregatorActor>("aggregator");

        Console.WriteLine("  Created 3-stage pipeline: Parser -> Transformer -> Aggregator");

        const int batchCount = 20;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int batch = 0; batch < batchCount; batch++)
        {
            // Stage 1: Parse raw data
            var rawData = new RawDataBatch
            {
                BatchId = batch,
                Data = Enumerable.Range(0, 1000).Select(i => (float)(i + batch * 1000)).ToArray()
            };

            var parsed = await parser.AskAsync<RawDataBatch, ParsedDataBatch>(rawData);

            // Stage 2: Transform
            var transformed = await transformer.AskAsync<ParsedDataBatch, TransformedDataBatch>(parsed);

            // Stage 3: Aggregate
            var result = await aggregator.AskAsync<TransformedDataBatch, AggregatedResult>(transformed);

            if (batch < 3 || batch == batchCount - 1)
            {
                Console.WriteLine($"  Batch {batch}: sum={result.Sum:F2}, mean={result.Mean:F2}, std={result.StdDev:F2}");
            }
            else if (batch == 3)
            {
                Console.WriteLine("  ...");
            }
        }

        sw.Stop();
        Console.WriteLine($"  Processed {batchCount} batches in {sw.ElapsedMilliseconds}ms");

        await parser.StopAsync();
        await transformer.StopAsync();
        await aggregator.StopAsync();
    }

    /// <summary>
    /// Demo 4: Map-reduce pattern for distributed aggregation.
    /// </summary>
    static async Task DemoMapReduce(RingKernelRuntime runtime)
    {
        const int numMappers = 4;
        const int dataSize = 100_000;

        // Generate input data
        var random = new Random(42);
        var inputData = Enumerable.Range(0, dataSize).Select(_ => (float)random.NextDouble()).ToArray();

        // Spawn mapper actors
        var mappers = new List<ActorRef<MapperActor>>();
        for (int i = 0; i < numMappers; i++)
        {
            mappers.Add(await runtime.SpawnAsync<MapperActor>($"mapper-{i}"));
        }

        // Spawn reducer actor
        var reducer = await runtime.SpawnAsync<ReducerActor>("reducer");

        Console.WriteLine($"  Spawned {numMappers} mappers and 1 reducer");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Partition data and distribute to mappers
        int chunkSize = dataSize / numMappers;
        var mapTasks = new List<Task<MapResult>>();

        for (int i = 0; i < numMappers; i++)
        {
            int start = i * chunkSize;
            int end = (i == numMappers - 1) ? dataSize : start + chunkSize;

            var chunk = new MapInput
            {
                ChunkId = i,
                Data = inputData[start..end]
            };

            mapTasks.Add(mappers[i].AskAsync<MapInput, MapResult>(chunk));
        }

        // Wait for map results
        var mapResults = await Task.WhenAll(mapTasks);
        Console.WriteLine($"  Map phase complete: {mapResults.Length} partial results");

        // Send to reducer
        var reduceInput = new ReduceInput
        {
            PartialResults = mapResults.ToArray()
        };

        var finalResult = await reducer.AskAsync<ReduceInput, FinalResult>(reduceInput);

        sw.Stop();

        Console.WriteLine($"  Final Results:");
        Console.WriteLine($"    Count: {finalResult.Count:N0}");
        Console.WriteLine($"    Sum: {finalResult.Sum:F4}");
        Console.WriteLine($"    Mean: {finalResult.Mean:F6}");
        Console.WriteLine($"    Min: {finalResult.Min:F6}");
        Console.WriteLine($"    Max: {finalResult.Max:F6}");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms");

        // Cleanup
        foreach (var mapper in mappers)
        {
            await mapper.StopAsync();
        }
        await reducer.StopAsync();
    }
}

#region Message Types

[MemoryPackable]
public partial record PingMessage : IRingKernelMessage
{
    public int SequenceNumber { get; init; }
    public long Timestamp { get; init; }
}

[MemoryPackable]
public partial record PongMessage : IRingKernelMessage
{
    public int SequenceNumber { get; init; }
    public long ResponseTimestamp { get; init; }
}

[MemoryPackable]
public partial record ComputeTask : IRingKernelMessage
{
    public int TaskId { get; init; }
    public int VectorSize { get; init; }
    public ComputeOperation Operation { get; init; }
}

public enum ComputeOperation
{
    VectorAdd,
    VectorMultiply,
    DotProduct,
    Normalize
}

[MemoryPackable]
public partial record ComputeResult : IRingKernelMessage
{
    public int TaskId { get; init; }
    public float Result { get; init; }
    public long OperationsPerformed { get; init; }
}

[MemoryPackable]
public partial record RawDataBatch : IRingKernelMessage
{
    public int BatchId { get; init; }
    public float[] Data { get; init; } = Array.Empty<float>();
}

[MemoryPackable]
public partial record ParsedDataBatch : IRingKernelMessage
{
    public int BatchId { get; init; }
    public float[] Values { get; init; } = Array.Empty<float>();
    public int ValidCount { get; init; }
}

[MemoryPackable]
public partial record TransformedDataBatch : IRingKernelMessage
{
    public int BatchId { get; init; }
    public float[] TransformedValues { get; init; } = Array.Empty<float>();
}

[MemoryPackable]
public partial record AggregatedResult : IRingKernelMessage
{
    public int BatchId { get; init; }
    public float Sum { get; init; }
    public float Mean { get; init; }
    public float StdDev { get; init; }
}

[MemoryPackable]
public partial record MapInput : IRingKernelMessage
{
    public int ChunkId { get; init; }
    public float[] Data { get; init; } = Array.Empty<float>();
}

[MemoryPackable]
public partial record MapResult : IRingKernelMessage
{
    public int ChunkId { get; init; }
    public int Count { get; init; }
    public float Sum { get; init; }
    public float Min { get; init; }
    public float Max { get; init; }
}

[MemoryPackable]
public partial record ReduceInput : IRingKernelMessage
{
    public MapResult[] PartialResults { get; init; } = Array.Empty<MapResult>();
}

[MemoryPackable]
public partial record FinalResult : IRingKernelMessage
{
    public int Count { get; init; }
    public float Sum { get; init; }
    public float Mean { get; init; }
    public float Min { get; init; }
    public float Max { get; init; }
}

#endregion

#region Actor Implementations

/// <summary>
/// Simple echo actor for ping-pong testing.
/// </summary>
[RingKernelActor]
public partial class PingPongActor : ActorBase
{
    public override async Task<TResponse> HandleAsync<TRequest, TResponse>(TRequest message)
    {
        if (message is PingMessage ping)
        {
            var pong = new PongMessage
            {
                SequenceNumber = ping.SequenceNumber,
                ResponseTimestamp = DateTime.UtcNow.Ticks
            };
            return (TResponse)(object)pong;
        }
        throw new InvalidOperationException($"Unknown message type: {typeof(TRequest)}");
    }
}

/// <summary>
/// Worker actor that performs GPU-accelerated compute tasks.
/// </summary>
[RingKernelActor]
public partial class ComputeWorkerActor : ActorBase
{
    public override async Task<TResponse> HandleAsync<TRequest, TResponse>(TRequest message)
    {
        if (message is ComputeTask task)
        {
            // Simulate GPU compute operation
            float result = 0;
            long ops = 0;

            switch (task.Operation)
            {
                case ComputeOperation.VectorAdd:
                    result = task.VectorSize * 2.0f; // Sum of 1+1 for each element
                    ops = task.VectorSize;
                    break;
                case ComputeOperation.VectorMultiply:
                    result = task.VectorSize * 1.0f;
                    ops = task.VectorSize;
                    break;
                case ComputeOperation.DotProduct:
                    result = task.VectorSize * 1.0f;
                    ops = task.VectorSize * 2; // Multiply + add
                    break;
                case ComputeOperation.Normalize:
                    result = 1.0f;
                    ops = task.VectorSize * 3; // Square + sum + divide
                    break;
            }

            return (TResponse)(object)new ComputeResult
            {
                TaskId = task.TaskId,
                Result = result,
                OperationsPerformed = ops
            };
        }
        throw new InvalidOperationException($"Unknown message type: {typeof(TRequest)}");
    }
}

/// <summary>
/// Data parser actor for pipeline stage 1.
/// </summary>
[RingKernelActor]
public partial class DataParserActor : ActorBase
{
    public override async Task<TResponse> HandleAsync<TRequest, TResponse>(TRequest message)
    {
        if (message is RawDataBatch raw)
        {
            // Parse and validate data
            var valid = raw.Data.Where(x => !float.IsNaN(x) && !float.IsInfinity(x)).ToArray();

            return (TResponse)(object)new ParsedDataBatch
            {
                BatchId = raw.BatchId,
                Values = valid,
                ValidCount = valid.Length
            };
        }
        throw new InvalidOperationException($"Unknown message type: {typeof(TRequest)}");
    }
}

/// <summary>
/// Data transformer actor for pipeline stage 2.
/// </summary>
[RingKernelActor]
public partial class DataTransformerActor : ActorBase
{
    public override async Task<TResponse> HandleAsync<TRequest, TResponse>(TRequest message)
    {
        if (message is ParsedDataBatch parsed)
        {
            // Apply transformation (normalize to [0, 1])
            float min = parsed.Values.Min();
            float max = parsed.Values.Max();
            float range = max - min;

            var transformed = range > 0
                ? parsed.Values.Select(v => (v - min) / range).ToArray()
                : parsed.Values;

            return (TResponse)(object)new TransformedDataBatch
            {
                BatchId = parsed.BatchId,
                TransformedValues = transformed
            };
        }
        throw new InvalidOperationException($"Unknown message type: {typeof(TRequest)}");
    }
}

/// <summary>
/// Data aggregator actor for pipeline stage 3.
/// </summary>
[RingKernelActor]
public partial class DataAggregatorActor : ActorBase
{
    public override async Task<TResponse> HandleAsync<TRequest, TResponse>(TRequest message)
    {
        if (message is TransformedDataBatch transformed)
        {
            float sum = transformed.TransformedValues.Sum();
            float mean = sum / transformed.TransformedValues.Length;
            float variance = transformed.TransformedValues.Average(v => (v - mean) * (v - mean));
            float stdDev = MathF.Sqrt(variance);

            return (TResponse)(object)new AggregatedResult
            {
                BatchId = transformed.BatchId,
                Sum = sum,
                Mean = mean,
                StdDev = stdDev
            };
        }
        throw new InvalidOperationException($"Unknown message type: {typeof(TRequest)}");
    }
}

/// <summary>
/// Mapper actor for map-reduce pattern.
/// </summary>
[RingKernelActor]
public partial class MapperActor : ActorBase
{
    public override async Task<TResponse> HandleAsync<TRequest, TResponse>(TRequest message)
    {
        if (message is MapInput input)
        {
            return (TResponse)(object)new MapResult
            {
                ChunkId = input.ChunkId,
                Count = input.Data.Length,
                Sum = input.Data.Sum(),
                Min = input.Data.Min(),
                Max = input.Data.Max()
            };
        }
        throw new InvalidOperationException($"Unknown message type: {typeof(TRequest)}");
    }
}

/// <summary>
/// Reducer actor for map-reduce pattern.
/// </summary>
[RingKernelActor]
public partial class ReducerActor : ActorBase
{
    public override async Task<TResponse> HandleAsync<TRequest, TResponse>(TRequest message)
    {
        if (message is ReduceInput input)
        {
            int totalCount = input.PartialResults.Sum(r => r.Count);
            float totalSum = input.PartialResults.Sum(r => r.Sum);
            float globalMin = input.PartialResults.Min(r => r.Min);
            float globalMax = input.PartialResults.Max(r => r.Max);

            return (TResponse)(object)new FinalResult
            {
                Count = totalCount,
                Sum = totalSum,
                Mean = totalSum / totalCount,
                Min = globalMin,
                Max = globalMax
            };
        }
        throw new InvalidOperationException($"Unknown message type: {typeof(TRequest)}");
    }
}

#endregion
