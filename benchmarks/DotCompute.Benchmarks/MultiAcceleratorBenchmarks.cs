using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Benchmarks;


/// <summary>
/// Benchmarks for multi-accelerator scaling and coordination performance.
/// Tests workload distribution, memory management across devices, and synchronization overhead.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class MultiAcceleratorBenchmarks : IDisposable
{
    private DefaultAcceleratorManager _acceleratorManager = null!;
    private List<IAccelerator> _accelerators = null!;
    private readonly List<IMemoryBuffer> _buffers = [];

    [Params(1, 2, 4, 8)]
    public int AcceleratorCount { get; set; }

    [Params(1024 * 1024, 16 * 1024 * 1024, 64 * 1024 * 1024)]
    public int DataSize { get; set; }

    [Params("Independent", "Coordinated", "PipelinedParallel")]
    public string WorkloadType { get; set; } = "Independent";

    private float[] _inputData = null!;
    private float[][] _partitionedData = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);

        // Create multiple CPU accelerator providers to simulate multiple devices
        for (var i = 0; i < Math.Max(AcceleratorCount, 8); i++)
        {
            var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
            _acceleratorManager.RegisterProvider(cpuProvider);
        }

        await _acceleratorManager.InitializeAsync();
        _accelerators = [.. (await _acceleratorManager.GetAcceleratorsAsync()).Take(AcceleratorCount)];

        SetupTestData();
    }

    private void SetupTestData()
    {
        _inputData = new float[DataSize];
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        var random = new Random(42);
#pragma warning restore CA5394

        for (var i = 0; i < DataSize; i++)
        {
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
            _inputData[i] = (float)(random.NextDouble() * 2.0 - 1.0);
#pragma warning restore CA5394
        }

        // Partition data for multi-accelerator processing
        _partitionedData = new float[AcceleratorCount][];
        var partitionSize = DataSize / AcceleratorCount;

        for (var i = 0; i < AcceleratorCount; i++)
        {
            var startIndex = i * partitionSize;
            var endIndex = (i == AcceleratorCount - 1) ? DataSize : (i + 1) * partitionSize;
            var currentPartitionSize = endIndex - startIndex;

            _partitionedData[i] = new float[currentPartitionSize];
            Array.Copy(_inputData, startIndex, _partitionedData[i], 0, currentPartitionSize);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
            {
                await buffer.DisposeAsync();
            }
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
            {
                await buffer.DisposeAsync();
            }
        }
        _buffers.Clear();
    }

    [Benchmark(Baseline = true)]
    public async Task SingleAcceleratorExecution()
    {
        var accelerator = _accelerators[0];
        var buffer = await accelerator.Memory.AllocateAndCopyAsync<float>(_inputData);

        // Simulate processing work
        await SimulateProcessingWork(accelerator, buffer);

        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task MultiAcceleratorParallelExecution()
    {
        var tasks = new List<Task>();

        for (var i = 0; i < AcceleratorCount && i < _accelerators.Count; i++)
        {
            var acceleratorIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                var accelerator = _accelerators[acceleratorIndex];
                var partitionData = _partitionedData[acceleratorIndex];

                var buffer = await accelerator.Memory.AllocateAndCopyAsync<float>(partitionData);
                await SimulateProcessingWork(accelerator, buffer);

                lock (_buffers)
                {
                    _buffers.Add(buffer);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task WorkloadDistribution() => await ExecuteWorkloadType(WorkloadType);

    private async Task ExecuteWorkloadType(string workloadType)
    {
        switch (workloadType)
        {
            case "Independent":
                await ExecuteIndependentWorkload();
                break;
            case "Coordinated":
                await ExecuteCoordinatedWorkload();
                break;
            case "PipelinedParallel":
                await ExecutePipelinedParallelWorkload();
                break;
        }
    }

    private async Task ExecuteIndependentWorkload()
    {
        // Each accelerator processes its partition independently
        var tasks = new List<Task>();

        for (var i = 0; i < AcceleratorCount && i < _accelerators.Count; i++)
        {
            var acceleratorIndex = i;
            tasks.Add(ProcessPartitionIndependently(acceleratorIndex));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessPartitionIndependently(int acceleratorIndex)
    {
        var accelerator = _accelerators[acceleratorIndex];
        var partitionData = _partitionedData[acceleratorIndex];

        var inputBuffer = await accelerator.Memory.AllocateAndCopyAsync<float>(partitionData);
        var outputBuffer = await accelerator.Memory.AllocateAsync(partitionData.Length * sizeof(float));

        // Simulate independent processing
        await SimulateProcessingWork(accelerator, inputBuffer);

        // Copy result back
        var result = new float[partitionData.Length];
        await inputBuffer.CopyToHostAsync<float>(result);
        await outputBuffer.CopyFromHostAsync<float>(result);

        lock (_buffers)
        {
            _buffers.Add(inputBuffer);
            _buffers.Add(outputBuffer);
        }
    }

    private async Task ExecuteCoordinatedWorkload()
    {
        // Accelerators coordinate and share intermediate results
        var coordinatorAccelerator = _accelerators[0];
        var intermediateBuffers = new List<IMemoryBuffer>();

        // Phase 1: Each accelerator processes its partition
        var phase1Tasks = new List<Task<IMemoryBuffer>>();

        for (var i = 0; i < AcceleratorCount && i < _accelerators.Count; i++)
        {
            var acceleratorIndex = i;
            phase1Tasks.Add(ProcessPartitionPhase1(acceleratorIndex));
        }

        var phase1Results = await Task.WhenAll(phase1Tasks);
        intermediateBuffers.AddRange(phase1Results);

        // Phase 2: Coordinator aggregates results
        var aggregatedBuffer = await coordinatorAccelerator.Memory.AllocateAsync(DataSize * sizeof(float));

        // Simulate aggregation work
        foreach (var buffer in phase1Results)
        {
            // Copy intermediate result to coordinator
            var tempData = new float[buffer.SizeInBytes / sizeof(float)];
            await buffer.CopyToHostAsync<float>(tempData);
        }

        _buffers.AddRange(intermediateBuffers);
        _buffers.Add(aggregatedBuffer);
    }

    private async Task<IMemoryBuffer> ProcessPartitionPhase1(int acceleratorIndex)
    {
        var accelerator = _accelerators[acceleratorIndex];
        var partitionData = _partitionedData[acceleratorIndex];

        var buffer = await accelerator.Memory.AllocateAndCopyAsync<float>(partitionData);
        await SimulateProcessingWork(accelerator, buffer);

        return buffer;
    }

    private async Task ExecutePipelinedParallelWorkload()
    {
        // Pipeline processing across accelerators
        const int pipelineStages = 3;
#pragma warning disable IDE0059 // currentData is used in loop iterations
        var currentData = _inputData;
#pragma warning restore IDE0059

        for (var stage = 0; stage < pipelineStages; stage++)
        {
            var tasks = new List<Task<float[]>>();

            // Process stage across multiple accelerators
            for (var i = 0; i < AcceleratorCount && i < _accelerators.Count; i++)
            {
                var acceleratorIndex = i;
                var currentStage = stage;

                tasks.Add(ProcessPipelineStage(acceleratorIndex, currentStage, _partitionedData[acceleratorIndex]));
            }

            var stageOutputs = await Task.WhenAll(tasks);

            // Combine results for next stage
            currentData = CombinePartitionResults(stageOutputs);

            // Update partitioned data for next stage
            UpdatePartitionedData(currentData);
        }
    }

    private async Task<float[]> ProcessPipelineStage(int acceleratorIndex, int stage, float[] stageInput)
    {
        var accelerator = _accelerators[acceleratorIndex];
        var buffer = await accelerator.Memory.AllocateAndCopyAsync<float>(stageInput);

        // Simulate stage-specific processing
        await Task.Delay(stage + 1); // Different processing time per stage
        await SimulateProcessingWork(accelerator, buffer);

        var result = new float[stageInput.Length];
        await buffer.CopyToHostAsync<float>(result);

        lock (_buffers)
        {
            _buffers.Add(buffer);
        }

        return result;
    }

    private static float[] CombinePartitionResults(float[][] partitionResults)
    {
        var totalLength = partitionResults.Sum(p => p.Length);
        var combined = new float[totalLength];
        var offset = 0;

        foreach (var partition in partitionResults)
        {
            Array.Copy(partition, 0, combined, offset, partition.Length);
            offset += partition.Length;
        }

        return combined;
    }

    private void UpdatePartitionedData(float[] newData)
    {
        var partitionSize = newData.Length / AcceleratorCount;

        for (var i = 0; i < AcceleratorCount; i++)
        {
            var startIndex = i * partitionSize;
            var endIndex = (i == AcceleratorCount - 1) ? newData.Length : (i + 1) * partitionSize;
            var currentPartitionSize = endIndex - startIndex;

            _partitionedData[i] = new float[currentPartitionSize];
            Array.Copy(newData, startIndex, _partitionedData[i], 0, currentPartitionSize);
        }
    }

    [Benchmark]
    public async Task CrossAcceleratorMemoryTransfer()
    {
        if (AcceleratorCount < 2)
        {
            return;
        }

        var sourceAccelerator = _accelerators[0];
        var targetAccelerator = _accelerators[1];

        // Allocate on source
        var sourceBuffer = await sourceAccelerator.Memory.AllocateAndCopyAsync<float>(_inputData);

        // Transfer to target (via host memory)
        var tempData = new float[DataSize];
        await sourceBuffer.CopyToHostAsync<float>(tempData);

        var targetBuffer = await targetAccelerator.Memory.AllocateAndCopyAsync<float>(tempData);

        _buffers.Add(sourceBuffer);
        _buffers.Add(targetBuffer);
    }

    [Benchmark]
    public async Task AcceleratorSynchronization()
    {
        var syncTasks = new List<Task>();

        // Start work on all accelerators
        for (var i = 0; i < AcceleratorCount && i < _accelerators.Count; i++)
        {
            var acceleratorIndex = i;
            syncTasks.Add(Task.Run(async () =>
            {
                var accelerator = _accelerators[acceleratorIndex];
                var buffer = await accelerator.Memory.AllocateAndCopyAsync<float>(_partitionedData[acceleratorIndex]);

                await SimulateProcessingWork(accelerator, buffer);

                // Synchronize accelerator
                await accelerator.SynchronizeAsync();

                lock (_buffers)
                {
                    _buffers.Add(buffer);
                }
            }));
        }

        // Wait for all accelerators to complete and synchronize
        await Task.WhenAll(syncTasks);

        // Final synchronization barrier
        var finalSyncTasks = _accelerators.Select(acc => acc.SynchronizeAsync().AsTask());
        await Task.WhenAll(finalSyncTasks);
    }

    [Benchmark]
    public double ScalingEfficiency()
    {
        // Calculate scaling efficiency
        var singleAcceleratorTime = 100.0; // Simulated baseline (ms)
        var multiAcceleratorTime = singleAcceleratorTime / AcceleratorCount * 1.2; // With 20% overhead

        var idealSpeedup = AcceleratorCount;
        var actualSpeedup = singleAcceleratorTime / multiAcceleratorTime;

        return actualSpeedup / idealSpeedup * 100.0; // Efficiency percentage
    }

    [Benchmark]
    public async Task LoadBalancing()
    {
        // Simulate uneven workload and test load balancing
        var workloads = new int[AcceleratorCount];
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        var random = new Random(42);
#pragma warning restore CA5394

        // Create uneven workloads
        for (var i = 0; i < AcceleratorCount; i++)
        {
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
            workloads[i] = random.Next(50, 200); // 50-200ms of simulated work
#pragma warning restore CA5394
        }

        var tasks = new List<Task>();

        for (var i = 0; i < AcceleratorCount && i < _accelerators.Count; i++)
        {
            var acceleratorIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                var accelerator = _accelerators[acceleratorIndex];
                var buffer = await accelerator.Memory.AllocateAndCopyAsync<float>(_partitionedData[acceleratorIndex]);

                // Simulate variable workload
                await Task.Delay(workloads[acceleratorIndex]);

                lock (_buffers)
                {
                    _buffers.Add(buffer);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private static async Task SimulateProcessingWork(IAccelerator accelerator, IMemoryBuffer buffer)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        // Simulate computational work on the accelerator
        await Task.Delay(10); // Base processing time

        // Simulate memory access patterns
        var tempData = new float[Math.Min(1024, (int)(buffer.SizeInBytes / sizeof(float)))];
        await buffer.CopyToHostAsync<float>(tempData);

        // Simulate computation
        for (var i = 0; i < tempData.Length; i++)
        {
            tempData[i] = tempData[i] * 2.0f + 1.0f;
        }

        await buffer.CopyFromHostAsync<float>(tempData.AsMemory(0, Math.Min(tempData.Length, (int)(buffer.SizeInBytes / sizeof(float)))));
    }

    public void Dispose()
    {
        try
        {
            Cleanup().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore disposal errors in finalizer
        }
        GC.SuppressFinalize(this);
    }
}
