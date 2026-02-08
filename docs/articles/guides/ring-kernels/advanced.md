# Advanced Ring Kernel Programming

This guide covers advanced topics in Ring Kernel programming, including complex message patterns, optimization techniques, multi-kernel coordination, and production deployment strategies.

## Table of Contents

- [Advanced Configuration](#advanced-configuration)
- [Orleans.GpuBridge.Core Integration](#orleansgpubridgecore-integration-v062)
- [Complex Message Patterns](#complex-message-patterns)
- [State Management](#state-management)
- [Multi-Kernel Coordination](#multi-kernel-coordination)
- [Performance Optimization](#performance-optimization)
- [Error Handling and Recovery](#error-handling-and-recovery)
- [Production Deployment](#production-deployment)
- [Backend-Specific Considerations](#backend-specific-considerations)

## Advanced Configuration

### RingKernelLaunchOptions Deep Dive

The `RingKernelLaunchOptions` class (v0.6.2+) provides granular control over message queue behavior, enabling fine-tuned performance optimization for specific workloads.

#### Power-User Configuration

```csharp
public class RingKernelConfiguration
{
    public static RingKernelLaunchOptions CreateForOrleansActors()
    {
        // Orleans.GpuBridge: Actor-based distributed GPU computation
        return new RingKernelLaunchOptions
        {
            QueueCapacity = 4096,              // Handle actor request bursts
            DeduplicationWindowSize = 1024,     // Retry protection
            BackpressureStrategy = BackpressureStrategy.Block,  // Guaranteed delivery
            EnablePriorityQueue = true          // High-priority actor requests
        };
    }

    public static RingKernelLaunchOptions CreateForGraphAnalytics()
    {
        // Vertex-centric graph processing (PageRank, BFS, etc.)
        return new RingKernelLaunchOptions
        {
            QueueCapacity = 16384,             // Large message bursts in BSP supersteps
            DeduplicationWindowSize = 1024,     // Standard deduplication
            BackpressureStrategy = BackpressureStrategy.DropOldest,  // Newest data wins
            EnablePriorityQueue = false         // FIFO for graph messages
        };
    }

    public static RingKernelLaunchOptions CreateForRealtimeTelemetry()
    {
        // Real-time sensor data processing
        return new RingKernelLaunchOptions
        {
            QueueCapacity = 512,               // Low latency, small buffer
            DeduplicationWindowSize = 256,      // Recent duplicate detection
            BackpressureStrategy = BackpressureStrategy.DropOldest,  // Latest telemetry wins
            EnablePriorityQueue = false         // FIFO for time-series data
        };
    }

    public static RingKernelLaunchOptions CreateForMLInference()
    {
        // Batch ML inference with backpressure handling
        return new RingKernelLaunchOptions
        {
            QueueCapacity = 8192,              // Large batches
            DeduplicationWindowSize = 1024,     // Standard deduplication
            BackpressureStrategy = BackpressureStrategy.Reject,  // Fail fast on overload
            EnablePriorityQueue = true          // High-priority inference requests
        };
    }
}

// Usage in production
var options = RingKernelConfiguration.CreateForOrleansActors();
await runtime.LaunchAsync("actor_kernel", gridSize: 128, blockSize: 256, options);
```

#### Dynamic Configuration Adjustment

Ring Kernel queues can be reconfigured at runtime by terminating and relaunching with new options:

```csharp
// Phase 1: Low-latency warmup
var warmupOptions = RingKernelLaunchOptions.LowLatencyDefaults();
await runtime.LaunchAsync("kernel_id", 1, 256, warmupOptions);

// Process warmup messages...

// Phase 2: High-throughput production
await runtime.TerminateAsync("kernel_id");
var productionOptions = RingKernelLaunchOptions.HighThroughputDefaults();
await runtime.LaunchAsync("kernel_id", 128, 256, productionOptions);

// Process production workload...
```

#### Validation and Error Handling

```csharp
var options = new RingKernelLaunchOptions
{
    QueueCapacity = 5000,  // ❌ Not a power of 2
    DeduplicationWindowSize = 2048  // ❌ Exceeds maximum (1024)
};

try
{
    options.Validate();
}
catch (ArgumentOutOfRangeException ex)
{
    // QueueCapacity must be a power of 2 (16, 32, 64, ..., 1048576)
    _logger.LogError(ex, "Invalid launch options: {Message}", ex.Message);
}

// ✅ Fix: Use nearest valid values
options.QueueCapacity = 4096;  // Next power of 2
options.DeduplicationWindowSize = 1024;  // Maximum allowed
options.Validate();  // Now succeeds
```

#### Memory Budget Estimation

Calculate total memory usage for queue configuration:

```csharp
public static long EstimateMemoryUsage(RingKernelLaunchOptions options, int numQueues)
{
    const int MessageOverhead = 32;  // Bytes per message (IRingKernelMessage)
    const int QueueStructure = 64;   // Queue metadata

    long queueStorage = options.QueueCapacity * MessageOverhead;
    long dedupStorage = options.DeduplicationWindowSize * 4;  // Hash table (4 bytes per entry)
    long perQueue = QueueStructure + queueStorage + dedupStorage;

    return perQueue * numQueues;
}

// Example: 8 queues with ProductionDefaults
var options = RingKernelLaunchOptions.ProductionDefaults();
long totalBytes = EstimateMemoryUsage(options, numQueues: 8);
double totalMB = totalBytes / (1024.0 * 1024.0);

_logger.LogInformation(
    "Estimated memory usage: {Memory:F2} MB for {Queues} queues",
    totalMB, 8);
// Output: "Estimated memory usage: 1.28 MB for 8 queues"
```

## Orleans.GpuBridge.Core Integration (v0.6.2+)

The Orleans.GpuBridge.Core integration adds advanced GPU-level features for actor-based distributed computing:

### Processing Modes

Ring Kernels support three processing modes to balance latency and throughput:

```csharp
// Option 1: Continuous Mode (default) - Minimum latency
// Process one message per iteration for real-time responsiveness
[RingKernel(
    ProcessingMode = RingProcessingMode.Continuous,
    EnableTimestamps = true)]
public static void RealtimeProcessor(
    MessageQueue<SensorData> incoming,
    MessageQueue<ProcessedData> outgoing)
{
    // Single message processed per dispatch loop iteration
    // Best for: Real-time telemetry, low-latency game servers
}

// Option 2: Batch Mode - Maximum throughput
// Process fixed batch sizes for high-throughput workloads
[RingKernel(
    ProcessingMode = RingProcessingMode.Batch,
    MaxMessagesPerIteration = 64)]
public static void BatchProcessor(
    MessageQueue<WorkItem> incoming,
    MessageQueue<Result> outgoing)
{
    // Up to 64 messages processed per iteration
    // Best for: ML inference batching, bulk data processing
}

// Option 3: Adaptive Mode - Balanced performance
// Dynamic batch sizing based on queue depth
[RingKernel(
    ProcessingMode = RingProcessingMode.Adaptive,
    MaxMessagesPerIteration = 32)]
public static void AdaptiveProcessor(
    MessageQueue<Request> incoming,
    MessageQueue<Response> outgoing)
{
    // Batch size adjusts: 1 when queue shallow, up to 32 when deep
    // Best for: Orleans actors, variable load workloads
}
```

**Performance Characteristics**:
| Mode | Latency | Throughput | Use Case |
|------|---------|------------|----------|
| Continuous | ~10μs | 100K msgs/sec | Real-time, gaming |
| Batch | ~100μs | 1M+ msgs/sec | ML inference, ETL |
| Adaptive | ~20-50μs | 500K-1M msgs/sec | Orleans actors |

### GPU Hardware Timestamps

Enable GPU-side hardware timestamp tracking for temporal consistency:

```csharp
[RingKernel(
    EnableTimestamps = true,
    ProcessingMode = RingProcessingMode.Continuous)]
public static void TemporallyConsistentKernel(
    MessageQueue<VertexUpdate> incoming,
    MessageQueue<VertexUpdate> outgoing,
    Span<float> state)
{
    // GPU timestamps (1ns resolution on CC 6.0+) automatically track:
    // - Kernel launch time
    // - Per-message processing timestamps
    // - Total execution cycles

    // Enables temporal ordering for distributed graph algorithms
    // and Orleans actor causality tracking
}
```

**Generated CUDA Code**:
```cuda
// When EnableTimestamps = true
long long kernel_start_time = clock64();

// Per-message timestamp
long long message_timestamp = clock64();

// Final metrics
if (tid == 0 && bid == 0)
{
    long long kernel_end_time = clock64();
    control_block->total_execution_cycles = kernel_end_time - kernel_start_time;
}
```

### Fairness Control

Prevent actor starvation in multi-tenant GPU systems:

```csharp
// Actor system with fairness guarantees
[RingKernel(
    ProcessingMode = RingProcessingMode.Adaptive,
    MaxMessagesPerIteration = 16,  // Fairness limit per dispatch
    EnableCausalOrdering = true,
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire)]
public static void FairActorKernel(
    MessageQueue<ActorMessage> incoming,
    MessageQueue<ActorMessage> outgoing)
{
    // Each actor processes at most 16 messages per iteration
    // Other actors get CPU/GPU time even under heavy load
    // Critical for Orleans grain fairness on GPU
}
```

**Why Fairness Matters**:
- Single actor with high message rate shouldn't starve others
- Enables predictable latency across all actors
- Required for Orleans grain scheduling semantics

### Unified Queue Configuration

Simplify symmetric input/output configurations:

```csharp
// Instead of separate InputQueueSize/OutputQueueSize
[RingKernel(
    MessageQueueSize = 4096,  // Sets BOTH input and output
    QueueCapacity = 8192)]
public static void SymmetricKernel(
    MessageQueue<DataPacket> incoming,
    MessageQueue<DataPacket> outgoing)
{
    // Both queues use 4096 capacity
    // Common for bidirectional actor communication
}

// Override for asymmetric patterns
[RingKernel(
    InputQueueSize = 8192,   // High-volume input
    OutputQueueSize = 512)]  // Lower-volume output
public static void AsymmetricKernel(
    MessageQueue<SmallRequest> incoming,
    MessageQueue<LargeResponse> outgoing)
{
    // Asymmetric: many small requests → fewer large responses
}
```

### Complete Orleans Actor Example

Production-ready Orleans.GpuBridge.Core configuration:

```csharp
[RingKernel(
    Mode = RingKernelMode.Persistent,
    Domain = RingKernelDomain.General,

    // Orleans.GpuBridge.Core attributes
    ProcessingMode = RingProcessingMode.Adaptive,
    MaxMessagesPerIteration = 32,
    EnableTimestamps = true,
    MessageQueueSize = 4096,

    // Memory consistency for actor message passing
    UseBarriers = true,
    BarrierScope = BarrierScope.ThreadBlock,
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
    EnableCausalOrdering = true)]
public static void OrleansGrainKernel(
    MessageQueue<GrainMessage> incoming,
    MessageQueue<GrainMessage> outgoing,
    Span<GrainState> state)
{
    int tid = Kernel.ThreadId.X;

    // Adaptive batching: process 1-32 messages based on queue depth
    // GPU timestamps track causality for Orleans virtual time
    // Release-acquire ensures message visibility across grains

    if (incoming.TryDequeue(out var msg))
    {
        // Process grain message
        var newState = ProcessGrainMessage(state[tid], msg);
        state[tid] = newState;

        // Send response with causal ordering
        outgoing.Enqueue(new GrainMessage
        {
            GrainId = msg.ReplyTo,
            Payload = newState.ToPayload()
        });
    }

    Kernel.Barrier();  // Synchronize for state consistency
}
```

This configuration provides:
- **Adaptive throughput**: 500K-1M messages/sec under varying load
- **Temporal consistency**: GPU hardware timestamps for causality
- **Actor fairness**: Maximum 32 messages per grain per dispatch
- **Memory safety**: Release-acquire semantics for grain communication

## Complex Message Patterns

### Producer-Consumer Pattern

```csharp
// Producer kernel generates work
[RingKernel(Mode = RingKernelMode.Persistent)]
public class ProducerKernel
{
    private int _sequenceNumber = 0;

    public async Task GenerateWork()
    {
        while (true)
        {
            // Generate work item
            var workItem = new WorkMessage
            {
                Id = _sequenceNumber++,
                Data = GenerateData(),
                Timestamp = GetTimestamp()
            };

            // Send to consumer
            await SendToKernel("consumer_kernel", workItem);

            // Rate limiting
            await Task.Delay(10);
        }
    }
}

// Consumer kernel processes work
[RingKernel(Mode = RingKernelMode.Persistent)]
public class ConsumerKernel
{
    public void ProcessWork(WorkMessage work)
    {
        // Process work item
        var result = ExpensiveComputation(work.Data);

        // Send result back
        SendResult(new ResultMessage
        {
            WorkId = work.Id,
            Result = result,
            ProcessingTimeUs = GetTimestamp() - work.Timestamp
        });
    }
}
```

### Request-Reply Pattern

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class RequestReplyKernel
{
    private Dictionary<int, ReplyContext> _pendingRequests = new();

    public void ProcessMessage(Message msg)
    {
        switch (msg.Type)
        {
            case MessageType.Request:
                HandleRequest(msg);
                break;

            case MessageType.Reply:
                HandleReply(msg);
                break;
        }
    }

    private void HandleRequest(Message msg)
    {
        // Process request
        var response = ComputeResponse(msg.RequestData);

        // Send reply
        var reply = new Message
        {
            Type = MessageType.Reply,
            RequestId = msg.RequestId,
            ReplyData = response,
            ReceiverId = msg.SenderId
        };

        SendMessage(msg.SenderId, reply);
    }

    private void HandleReply(Message msg)
    {
        // Match reply to pending request
        if (_pendingRequests.TryGetValue(msg.RequestId, out var context))
        {
            context.Complete(msg.ReplyData);
            _pendingRequests.Remove(msg.RequestId);
        }
    }
}
```

### Scatter-Gather Pattern

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class ScatterGatherKernel
{
    private class GatherState
    {
        public int TotalFragments;
        public int ReceivedFragments;
        public List<FragmentResult> Results = new();
    }

    private Dictionary<int, GatherState> _gatherStates = new();

    public void ProcessMessage(Message msg)
    {
        if (msg.Type == MessageType.ScatterRequest)
        {
            ScatterWork(msg);
        }
        else if (msg.Type == MessageType.FragmentResult)
        {
            GatherResult(msg);
        }
    }

    private void ScatterWork(Message msg)
    {
        var workItems = PartitionWork(msg.Data);
        var jobId = msg.RequestId;

        // Initialize gather state
        _gatherStates[jobId] = new GatherState
        {
            TotalFragments = workItems.Count,
            ReceivedFragments = 0
        };

        // Scatter work to worker kernels
        for (int i = 0; i < workItems.Count; i++)
        {
            SendToWorker(i % GetWorkerCount(), new WorkFragment
            {
                JobId = jobId,
                FragmentId = i,
                Data = workItems[i]
            });
        }
    }

    private void GatherResult(Message msg)
    {
        var state = _gatherStates[msg.JobId];
        state.Results.Add(msg.FragmentResult);
        state.ReceivedFragments++;

        // Check if all fragments received
        if (state.ReceivedFragments == state.TotalFragments)
        {
            // Combine results
            var finalResult = CombineResults(state.Results);

            // Send completed result
            SendResult(new CompletedJob
            {
                JobId = msg.JobId,
                Result = finalResult
            });

            // Cleanup
            _gatherStates.Remove(msg.JobId);
        }
    }
}
```

### Pipeline Pattern

```csharp
// Stage 1: Input validation and preprocessing
[RingKernel(Mode = RingKernelMode.Persistent)]
public class Stage1_Preprocess
{
    public void ProcessMessage(InputData input)
    {
        // Validate input
        if (!Validate(input))
        {
            SendError(new ValidationError { InputId = input.Id });
            return;
        }

        // Preprocess
        var preprocessed = Normalize(input.Data);

        // Send to stage 2
        SendToKernel("stage2_transform", new PreprocessedData
        {
            Id = input.Id,
            Data = preprocessed
        });
    }
}

// Stage 2: Transform and enrich
[RingKernel(Mode = RingKernelMode.Persistent)]
public class Stage2_Transform
{
    public void ProcessMessage(PreprocessedData data)
    {
        // Apply transformations
        var transformed = ApplyTransform(data.Data);

        // Enrich with metadata
        var enriched = EnrichMetadata(transformed);

        // Send to stage 3
        SendToKernel("stage3_aggregate", new TransformedData
        {
            Id = data.Id,
            Data = enriched
        });
    }
}

// Stage 3: Aggregate and output
[RingKernel(Mode = RingKernelMode.Persistent)]
public class Stage3_Aggregate
{
    private RunningAggregates _aggregates = new();

    public void ProcessMessage(TransformedData data)
    {
        // Update aggregates
        _aggregates.Update(data);

        // Check if ready to emit
        if (_aggregates.ShouldEmit())
        {
            SendResult(new AggregatedOutput
            {
                WindowId = _aggregates.WindowId,
                Metrics = _aggregates.GetMetrics()
            });

            _aggregates.Reset();
        }
    }
}
```

## State Management

### Persistent State

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class StatefulKernel
{
    // Persistent state (survives message processing)
    private MovingAverage _stats = new MovingAverage(windowSize: 1000);
    private Dictionary<int, UserState> _userStates = new();
    private long _totalProcessed = 0;

    public void ProcessMessage(UserEvent evt)
    {
        // Get or create user state
        if (!_userStates.TryGetValue(evt.UserId, out var userState))
        {
            userState = new UserState();
            _userStates[evt.UserId] = userState;
        }

        // Update user state
        userState.LastEventTime = evt.Timestamp;
        userState.EventCount++;

        // Update global stats
        _stats.Add(evt.Value);
        _totalProcessed++;

        // Periodic state checkpoint
        if (_totalProcessed % 10000 == 0)
        {
            CheckpointState();
        }
    }

    private void CheckpointState()
    {
        // Serialize state to host memory
        var checkpoint = new StateCheckpoint
        {
            TotalProcessed = _totalProcessed,
            UserStates = _userStates.ToArray(),
            StatsSnapshot = _stats.GetSnapshot()
        };

        SendCheckpoint(checkpoint);
    }
}
```

### Bounded State with Eviction

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class LRUCacheKernel
{
    private const int MaxEntries = 1000;
    private Dictionary<int, CacheEntry> _cache = new();
    private LinkedList<int> _lruList = new();

    public void ProcessMessage(CacheRequest request)
    {
        if (request.Type == RequestType.Get)
        {
            HandleGet(request.Key);
        }
        else if (request.Type == RequestType.Put)
        {
            HandlePut(request.Key, request.Value);
        }
    }

    private void HandleGet(int key)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            // Move to front (most recently used)
            _lruList.Remove(entry.ListNode);
            entry.ListNode = _lruList.AddFirst(key);

            SendReply(new CacheResponse
            {
                Found = true,
                Value = entry.Value
            });
        }
        else
        {
            SendReply(new CacheResponse { Found = false });
        }
    }

    private void HandlePut(int key, string value)
    {
        // Check if eviction needed
        if (_cache.Count >= MaxEntries && !_cache.ContainsKey(key))
        {
            // Evict least recently used
            int lruKey = _lruList.Last.Value;
            _lruList.RemoveLast();
            _cache.Remove(lruKey);
        }

        // Add or update entry
        if (_cache.TryGetValue(key, out var entry))
        {
            // Update existing
            entry.Value = value;
            _lruList.Remove(entry.ListNode);
            entry.ListNode = _lruList.AddFirst(key);
        }
        else
        {
            // Add new
            _cache[key] = new CacheEntry
            {
                Value = value,
                ListNode = _lruList.AddFirst(key)
            };
        }
    }
}
```

## Multi-Kernel Coordination

### Coordinator Pattern

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class CoordinatorKernel
{
    private List<string> _workerIds = new();
    private Dictionary<string, WorkerStats> _workerStats = new();
    private Queue<WorkItem> _workQueue = new();

    public void ProcessMessage(Message msg)
    {
        switch (msg.Type)
        {
            case MessageType.WorkerRegistration:
                RegisterWorker(msg.WorkerId);
                break;

            case MessageType.WorkRequest:
                EnqueueWork(msg.WorkItem);
                break;

            case MessageType.WorkerReady:
                AssignWork(msg.WorkerId);
                break;

            case MessageType.WorkComplete:
                HandleCompletion(msg);
                break;

            case MessageType.WorkerHeartbeat:
                UpdateWorkerStats(msg);
                break;
        }
    }

    private void RegisterWorker(string workerId)
    {
        _workerIds.Add(workerId);
        _workerStats[workerId] = new WorkerStats
        {
            Id = workerId,
            State = WorkerState.Idle,
            TasksCompleted = 0
        };

        SendToWorker(workerId, new RegistrationAck());
    }

    private void AssignWork(string workerId)
    {
        if (_workQueue.Count > 0)
        {
            var work = _workQueue.Dequeue();
            _workerStats[workerId].State = WorkerState.Busy;

            SendToWorker(workerId, new WorkAssignment
            {
                WorkId = work.Id,
                Data = work.Data
            });
        }
        else
        {
            _workerStats[workerId].State = WorkerState.Idle;
        }
    }

    private void HandleCompletion(Message msg)
    {
        var stats = _workerStats[msg.WorkerId];
        stats.TasksCompleted++;
        stats.State = WorkerState.Idle;

        // Send result to client
        SendResult(new WorkResult
        {
            WorkId = msg.WorkId,
            Result = msg.Result
        });

        // Assign next work if available
        AssignWork(msg.WorkerId);
    }

    // Periodic load balancing
    public void PeriodicTasks()
    {
        RebalanceWork();
        CheckWorkerHealth();
    }

    private void RebalanceWork()
    {
        // Find overloaded and underloaded workers
        var avgLoad = _workerStats.Values.Average(s => s.QueueDepth);

        foreach (var worker in _workerStats.Values)
        {
            if (worker.QueueDepth > avgLoad * 1.5)
            {
                // Migrate some work to idle workers
                MigrateWork(worker.Id, count: 10);
            }
        }
    }
}

[RingKernel(Mode = RingKernelMode.Persistent)]
public class WorkerKernel
{
    private string _coordinatorId;

    public void Initialize(string coordinatorId)
    {
        _coordinatorId = coordinatorId;

        // Register with coordinator
        SendToKernel(_coordinatorId, new WorkerRegistration
        {
            WorkerId = GetMyId()
        });
    }

    public void ProcessMessage(Message msg)
    {
        if (msg.Type == MessageType.WorkAssignment)
        {
            ProcessWork(msg.WorkAssignment);
        }
    }

    private void ProcessWork(WorkAssignment assignment)
    {
        // Do work
        var result = PerformComputation(assignment.Data);

        // Report completion
        SendToKernel(_coordinatorId, new WorkComplete
        {
            WorkerId = GetMyId(),
            WorkId = assignment.WorkId,
            Result = result
        });

        // Request more work
        SendToKernel(_coordinatorId, new WorkerReady
        {
            WorkerId = GetMyId()
        });
    }
}
```

### Barrier Synchronization

```csharp
[RingKernel(Mode = RingKernelMode.Persistent, Domain = RingKernelDomain.GraphAnalytics)]
public class BarrierSyncKernel
{
    private int _currentSuperstep = 0;
    private int _participantCount;
    private int _arrivedCount = 0;
    private List<Message> _bufferedMessages = new();

    public void ProcessMessage(Message msg)
    {
        switch (msg.Type)
        {
            case MessageType.Compute:
                ProcessComputePhase(msg);
                break;

            case MessageType.BarrierArrival:
                HandleBarrierArrival();
                break;

            case MessageType.BarrierRelease:
                ReleaseBarrier();
                break;
        }
    }

    private void ProcessComputePhase(Message msg)
    {
        // Process local computation
        var result = LocalComputation(msg.Data);

        // Buffer messages for next superstep
        foreach (var neighbor in GetNeighbors())
        {
            _bufferedMessages.Add(new Message
            {
                Type = MessageType.Compute,
                ReceiverId = neighbor,
                Data = result
            });
        }

        // Arrive at barrier
        SendBarrierArrival();
    }

    private void SendBarrierArrival()
    {
        _arrivedCount++;

        if (_arrivedCount == _participantCount)
        {
            // All participants arrived - release barrier
            _currentSuperstep++;
            BroadcastBarrierRelease();
        }
    }

    private void ReleaseBarrier()
    {
        // Send buffered messages
        foreach (var msg in _bufferedMessages)
        {
            SendMessage(msg.ReceiverId, msg);
        }

        _bufferedMessages.Clear();
        _arrivedCount = 0;
    }
}
```

## Performance Optimization

### Message Batching

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class BatchingKernel
{
    private const int BatchSize = 64;
    private List<Message> _batch = new(BatchSize);
    private long _lastFlushTime;

    public void ProcessMessage(Message msg)
    {
        _batch.Add(msg);

        // Flush on batch size or timeout
        if (_batch.Count >= BatchSize ||
            (GetTimestamp() - _lastFlushTime) > 1000) // 1ms timeout
        {
            FlushBatch();
        }
    }

    private void FlushBatch()
    {
        if (_batch.Count == 0) return;

        // Process batch in one go (better cache locality)
        var results = ProcessBatch(_batch);

        // Send all results
        foreach (var result in results)
        {
            SendResult(result);
        }

        _batch.Clear();
        _lastFlushTime = GetTimestamp();
    }

    private List<Result> ProcessBatch(List<Message> messages)
    {
        // Vectorized processing with better cache usage
        var results = new List<Result>(messages.Count);

        for (int i = 0; i < messages.Count; i++)
        {
            results.Add(ProcessSingle(messages[i]));
        }

        return results;
    }
}
```

### Memory Pool Allocation

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class PooledKernel
{
    private ObjectPool<WorkBuffer> _bufferPool = new(capacity: 256);

    public void ProcessMessage(Message msg)
    {
        // Rent from pool instead of allocating
        var buffer = _bufferPool.Rent();

        try
        {
            // Use buffer for processing
            buffer.CopyFrom(msg.Data);
            var result = ProcessData(buffer);

            SendResult(result);
        }
        finally
        {
            // Return to pool
            buffer.Clear();
            _bufferPool.Return(buffer);
        }
    }
}

public class ObjectPool<T> where T : new()
{
    private Queue<T> _pool;
    private int _capacity;

    public ObjectPool(int capacity)
    {
        _capacity = capacity;
        _pool = new Queue<T>(capacity);

        // Pre-allocate objects
        for (int i = 0; i < capacity; i++)
        {
            _pool.Enqueue(new T());
        }
    }

    public T Rent()
    {
        return _pool.Count > 0 ? _pool.Dequeue() : new T();
    }

    public void Return(T obj)
    {
        if (_pool.Count < _capacity)
        {
            _pool.Enqueue(obj);
        }
    }
}
```

### SIMD Vectorization

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class VectorizedKernel
{
    public void ProcessMessage(ArrayMessage msg)
    {
        int length = msg.Data.Length;
        int vectorWidth = 8; // Process 8 elements at once

        // Vectorized loop
        for (int i = 0; i < length - vectorWidth; i += vectorWidth)
        {
            // Load vector
            var v1 = LoadVector(msg.Data, i);
            var v2 = LoadVector(msg.Weights, i);

            // Vectorized computation
            var result = MultiplyAdd(v1, v2);

            // Store vector
            StoreVector(msg.Output, i, result);
        }

        // Handle remainder
        for (int i = (length / vectorWidth) * vectorWidth; i < length; i++)
        {
            msg.Output[i] = msg.Data[i] * msg.Weights[i];
        }
    }
}
```

### Lock-Free Data Structures

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class LockFreeKernel
{
    // Lock-free stack using compare-and-swap
    private class LockFreeStack<T>
    {
        private class Node
        {
            public T Value;
            public Node? Next;
        }

        private AtomicReference<Node?> _head = new(null);

        public void Push(T value)
        {
            var newNode = new Node { Value = value };

            while (true)
            {
                var currentHead = _head.Load();
                newNode.Next = currentHead;

                if (_head.CompareExchange(currentHead, newNode))
                {
                    return; // Success
                }
                // Retry on failure (another thread modified head)
            }
        }

        public bool TryPop(out T value)
        {
            while (true)
            {
                var currentHead = _head.Load();

                if (currentHead == null)
                {
                    value = default!;
                    return false;
                }

                var nextNode = currentHead.Next;

                if (_head.CompareExchange(currentHead, nextNode))
                {
                    value = currentHead.Value;
                    return true;
                }
                // Retry on failure
            }
        }
    }

    private LockFreeStack<WorkItem> _pendingWork = new();

    public void ProcessMessage(Message msg)
    {
        if (msg.Type == MessageType.WorkItem)
        {
            _pendingWork.Push(msg.WorkItem);
        }
        else if (msg.Type == MessageType.ProcessRequest)
        {
            if (_pendingWork.TryPop(out var work))
            {
                ProcessWork(work);
            }
        }
    }
}
```

### GPU Thread Barriers and Memory Ordering

GPU thread barriers in ring kernels coordinate threads **within a single kernel instance**, distinct from the application-level barrier synchronization shown above (which coordinates **between kernel instances** using messages).

#### Ring Kernel Memory Model

Ring kernels default to **Release-Acquire** consistency (unlike regular kernels which default to Relaxed) because message passing requires proper causality:

```csharp
[RingKernel(
    UseBarriers = true,
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,  // ✅ Default for ring kernels
    EnableCausalOrdering = true)]                               // ✅ Default true
public static void MessagePassingKernel(
    MessageQueue<VertexUpdate> incoming,
    MessageQueue<VertexUpdate> outgoing,
    Span<float> state)
{
    int tid = Kernel.ThreadId.X;

    // Dequeue message with acquire semantics
    if (incoming.TryDequeue(out var msg))
    {
        // Update local state
        state[tid] = msg.Value;
    }

    Kernel.Barrier();  // Synchronize threads

    // Read neighbor state with acquire semantics
    int neighbor = (tid + 1) % Kernel.BlockDim.X;
    float neighborValue = state[neighbor];  // ✅ Guaranteed to see write

    // Enqueue result with release semantics
    outgoing.Enqueue(new VertexUpdate
    {
        VertexId = tid,
        Value = (state[tid] + neighborValue) / 2.0f
    });
}
```

**Why Release-Acquire by Default?**
- **Regular kernels**: Data-parallel, independent threads → Relaxed (fastest)
- **Ring kernels**: Message passing, inter-thread communication → ReleaseAcquire (safe)
- **Overhead**: 15% performance cost, but amortized over persistent kernel lifetime

#### Shared Memory Reduction with Barriers

Common pattern: reduce incoming messages using shared memory and barriers.

```csharp
[RingKernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.ThreadBlock,
    BlockDimensions = new[] { 256 })]
public static void MessageReduction(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing)
{
    var shared = Kernel.AllocateShared<int>(256);
    int tid = Kernel.ThreadId.X;

    // Phase 1: Each thread dequeues one message
    shared[tid] = incoming.TryDequeue(out var msg) ? msg : 0;
    Kernel.Barrier();

    // Phase 2: Tree reduction in shared memory
    for (int stride = 128; stride > 0; stride /= 2)
    {
        if (tid < stride)
        {
            shared[tid] += shared[tid + stride];
        }
        Kernel.Barrier();  // Wait for each level
    }

    // Phase 3: Thread 0 sends reduced result
    if (tid == 0)
    {
        outgoing.Enqueue(shared[0]);
    }
}
```

**Performance**: 10-100× faster than atomic operations for reductions

#### Warp-Level Barriers for Message Processing

For high-throughput message processing, use warp-level barriers (CUDA/Metal only):

```csharp
[RingKernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.Warp,
    Backends = KernelBackends.CUDA | KernelBackends.Metal)]
public static void WarpMessageProcessor(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing)
{
    int tid = Kernel.ThreadId.X;
    int laneId = tid % 32;

    // Each warp processes messages independently
    int value = incoming.TryDequeue(out var msg) ? msg : 0;

    // Warp-level reduction (faster than threadblock)
    for (int offset = 16; offset > 0; offset /= 2)
    {
        value += WarpShuffle(value, laneId + offset);
        Kernel.Barrier();  // Warp barrier (~1-5ns)
    }

    // First thread in warp sends result
    if (laneId == 0)
    {
        outgoing.Enqueue(value);
    }
}
```

**Latency**: Warp barriers ~1-5ns vs ThreadBlock barriers ~10-20ns

#### Memory Consistency Trade-offs

Choose the right consistency model for your use case:

```csharp
// Option 1: Relaxed (expert use only - requires manual fences)
[RingKernel(
    UseBarriers = true,
    MemoryConsistency = MemoryConsistencyModel.Relaxed,
    EnableCausalOrdering = false)]
public static void RelaxedKernel(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing)
{
    // ⚠️ Must manually fence message operations
    int value = incoming.TryDequeue(out var msg) ? msg : 0;

    Kernel.MemoryFence();  // Manual fence required!
    Kernel.Barrier();

    outgoing.Enqueue(value);
    Kernel.MemoryFence();  // Manual fence required!
}

// Option 2: ReleaseAcquire (recommended default)
[RingKernel(
    UseBarriers = true,
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,  // ✅ Default
    EnableCausalOrdering = true)]                               // ✅ Default
public static void ReleaseAcquireKernel(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing)
{
    // ✅ Automatic fencing for message passing
    int value = incoming.TryDequeue(out var msg) ? msg : 0;
    Kernel.Barrier();
    outgoing.Enqueue(value);
}

// Option 3: Sequential (debugging race conditions)
[RingKernel(
    UseBarriers = true,
    MemoryConsistency = MemoryConsistencyModel.Sequential)]
public static void SequentialKernel(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing)
{
    // ✅ Strongest guarantees but 40% overhead
    int value = incoming.TryDequeue(out var msg) ? msg : 0;
    Kernel.Barrier();
    outgoing.Enqueue(value);
}
```

**Performance Comparison**:
| Model | Performance | Safety | Use Case |
|-------|-------------|--------|----------|
| Relaxed | 1.0× | Manual | Expert optimization only |
| ReleaseAcquire | 0.85× (15% overhead) | Automatic | **Recommended default** |
| Sequential | 0.60× (40% overhead) | Strongest | Debugging |

#### Common Pitfalls

**Pitfall 1: Mixing Barrier Scopes**
```csharp
// ❌ WRONG: Barrier scope doesn't match communication pattern
[RingKernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.Warp,     // ❌ Only syncs 32 threads
    BlockDimensions = new[] { 256 })]     // 256 threads total
public static void WrongScope(MessageQueue<int> incoming)
{
    var shared = Kernel.AllocateShared<int>(256);
    int tid = Kernel.ThreadId.X;

    shared[tid] = incoming.TryDequeue(out var msg) ? msg : 0;
    Kernel.Barrier();  // ❌ Only syncs within warp (32 threads)

    // 💀 Reading from other warps is unsafe!
    int sum = shared[0] + shared[64] + shared[128];
}

// ✅ CORRECT: Barrier scope matches communication
[RingKernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.ThreadBlock,  // ✅ Syncs all 256 threads
    BlockDimensions = new[] { 256 })]
public static void CorrectScope(MessageQueue<int> incoming)
{
    var shared = Kernel.AllocateShared<int>(256);
    int tid = Kernel.ThreadId.X;

    shared[tid] = incoming.TryDequeue(out var msg) ? msg : 0;
    Kernel.Barrier();  // ✅ All threads synchronized

    int sum = shared[0] + shared[64] + shared[128];  // ✅ Safe
}
```

**Pitfall 2: Message Queue Operations Without Proper Ordering**
```csharp
// ❌ WRONG: Relaxed consistency without manual fences
[RingKernel(
    MemoryConsistency = MemoryConsistencyModel.Relaxed,
    EnableCausalOrdering = false)]
public static void UnsafeMessagePassing(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing,
    Span<int> state)
{
    int tid = Kernel.ThreadId.X;

    // Write state
    state[tid] = ComputeValue();

    // Enqueue message
    outgoing.Enqueue(tid);  // 💀 May be visible before state write!
}

// ✅ CORRECT: ReleaseAcquire ensures proper ordering
[RingKernel(
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
    EnableCausalOrdering = true)]
public static void SafeMessagePassing(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing,
    Span<int> state)
{
    int tid = Kernel.ThreadId.X;

    state[tid] = ComputeValue();
    outgoing.Enqueue(tid);  // ✅ Release: state write visible before enqueue
}
```

**See Also**: [Barriers and Memory Ordering](../advanced/barriers-and-memory-ordering.md) for comprehensive barrier documentation

## Error Handling and Recovery

### Retry with Exponential Backoff

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class ResilientKernel
{
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 100;

    public async Task ProcessMessage(Message msg)
    {
        int retries = 0;

        while (retries < MaxRetries)
        {
            try
            {
                var result = await ProcessWithTimeout(msg, TimeSpan.FromSeconds(5));
                SendResult(result);
                return;
            }
            catch (TimeoutException)
            {
                retries++;

                if (retries >= MaxRetries)
                {
                    SendError(new ProcessingError
                    {
                        MessageId = msg.Id,
                        Reason = "Max retries exceeded"
                    });
                    return;
                }

                // Exponential backoff
                int delayMs = BaseDelayMs * (1 << retries);
                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                SendError(new ProcessingError
                {
                    MessageId = msg.Id,
                    Exception = ex.ToString()
                });
                return;
            }
        }
    }
}
```

### Circuit Breaker Pattern

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class CircuitBreakerKernel
{
    private enum CircuitState { Closed, Open, HalfOpen }

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private int _failureThreshold = 5;
    private long _openTimestamp;
    private long _halfOpenTimeout = 30_000; // 30 seconds

    public void ProcessMessage(Message msg)
    {
        if (_state == CircuitState.Open)
        {
            // Check if we should transition to half-open
            if (GetTimestamp() - _openTimestamp > _halfOpenTimeout)
            {
                _state = CircuitState.HalfOpen;
            }
            else
            {
                SendError(new CircuitOpenError
                {
                    MessageId = msg.Id
                });
                return;
            }
        }

        try
        {
            var result = ProcessWithExternalDependency(msg);

            // Success - reset if in half-open
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
            }

            SendResult(result);
        }
        catch (Exception)
        {
            _failureCount++;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openTimestamp = GetTimestamp();
            }

            SendError(new ProcessingError { MessageId = msg.Id });
        }
    }
}
```

### Dead Letter Queue

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class DeadLetterKernel
{
    private Queue<DeadLetter> _deadLetterQueue = new();
    private const int MaxDeadLetters = 1000;

    public void ProcessMessage(Message msg)
    {
        try
        {
            ValidateAndProcess(msg);
        }
        catch (ValidationException ex)
        {
            // Unrecoverable error - send to dead letter queue
            AddToDeadLetterQueue(new DeadLetter
            {
                OriginalMessage = msg,
                Reason = ex.Message,
                Timestamp = GetTimestamp()
            });
        }
        catch (Exception ex)
        {
            // Transient error - retry
            RetryMessage(msg);
        }
    }

    private void AddToDeadLetterQueue(DeadLetter letter)
    {
        if (_deadLetterQueue.Count >= MaxDeadLetters)
        {
            // Evict oldest
            _deadLetterQueue.Dequeue();
        }

        _deadLetterQueue.Enqueue(letter);

        // Notify monitoring system
        SendAlert(new DeadLetterAlert
        {
            MessageId = letter.OriginalMessage.Id,
            Reason = letter.Reason
        });
    }

    // Periodic dead letter processing
    public void ProcessDeadLetters()
    {
        var batch = new List<DeadLetter>();

        while (_deadLetterQueue.Count > 0 && batch.Count < 100)
        {
            batch.Add(_deadLetterQueue.Dequeue());
        }

        SendDeadLetterBatch(batch);
    }
}
```

## Production Deployment

### Health Monitoring

```csharp
public class RingKernelHealthMonitor
{
    private readonly IRingKernelRuntime _runtime;
    private readonly ILogger _logger;

    public async Task<HealthReport> CheckHealthAsync(string kernelId)
    {
        var report = new HealthReport { KernelId = kernelId };

        try
        {
            // Get status
            var status = await _runtime.GetStatusAsync(kernelId);
            report.IsActive = status.IsActive;
            report.MessagesProcessed = status.MessagesProcessed;

            // Get metrics
            var metrics = await _runtime.GetMetricsAsync(kernelId);
            report.Throughput = metrics.ThroughputMsgsPerSec;
            report.QueueUtilization = metrics.InputQueueUtilization;

            // Check for issues
            if (metrics.InputQueueUtilization > 0.9)
            {
                report.Warnings.Add("Input queue near capacity");
            }

            if (metrics.ThroughputMsgsPerSec < 1000)
            {
                report.Warnings.Add("Low throughput detected");
            }

            if (!status.IsActive && status.MessagesPending > 0)
            {
                report.Errors.Add("Kernel inactive with pending messages");
            }

            report.Status = report.Errors.Count == 0 ? "Healthy" : "Unhealthy";
        }
        catch (Exception ex)
        {
            report.Status = "Error";
            report.Errors.Add($"Health check failed: {ex.Message}");
        }

        return report;
    }
}
```

### Graceful Shutdown

```csharp
public class RingKernelManager
{
    private readonly IRingKernelRuntime _runtime;
    private readonly List<string> _activeKernels = new();

    public async Task ShutdownGracefullyAsync(TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);

        try
        {
            // Step 1: Stop accepting new messages
            foreach (var kernelId in _activeKernels)
            {
                await _runtime.DeactivateAsync(kernelId, cts.Token);
            }

            // Step 2: Wait for queues to drain
            await WaitForQueuesEmpty(cts.Token);

            // Step 3: Terminate kernels
            foreach (var kernelId in _activeKernels)
            {
                await _runtime.TerminateAsync(kernelId, cts.Token);
            }

            // Step 4: Dispose runtime
            await _runtime.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            // Timeout - force shutdown
            _logger.LogWarning("Graceful shutdown timeout - forcing termination");
            await ForceShutdown();
        }
    }

    private async Task WaitForQueuesEmpty(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool allEmpty = true;

            foreach (var kernelId in _activeKernels)
            {
                var status = await _runtime.GetStatusAsync(kernelId);
                if (status.MessagesPending > 0)
                {
                    allEmpty = false;
                    break;
                }
            }

            if (allEmpty) return;

            await Task.Delay(100, cancellationToken);
        }
    }
}
```

### Metrics Collection

```csharp
public class RingKernelMetricsCollector
{
    private readonly IRingKernelRuntime _runtime;
    private readonly IMetricsExporter _exporter;

    public async Task CollectMetricsAsync(string kernelId)
    {
        var metrics = await _runtime.GetMetricsAsync(kernelId);
        var status = await _runtime.GetStatusAsync(kernelId);

        // Export to monitoring system
        _exporter.RecordGauge("ring_kernel.throughput", metrics.ThroughputMsgsPerSec,
            new[] { new KeyValuePair<string, object>("kernel_id", kernelId) });

        _exporter.RecordGauge("ring_kernel.latency_ms", metrics.AvgProcessingTimeMs,
            new[] { new KeyValuePair<string, object>("kernel_id", kernelId) });

        _exporter.RecordGauge("ring_kernel.queue_utilization", metrics.InputQueueUtilization,
            new[] { new KeyValuePair<string, object>("kernel_id", kernelId) });

        _exporter.RecordCounter("ring_kernel.messages_processed", status.MessagesProcessed,
            new[] { new KeyValuePair<string, object>("kernel_id", kernelId) });
    }
}
```

## Backend-Specific Considerations

### CUDA-Specific Optimizations

```csharp
// Use CUDA streams for concurrent kernel execution
[RingKernel(
    Mode = RingKernelMode.Persistent,
    MessagingStrategy = MessagePassingStrategy.AtomicQueue,
    Backend = KernelBackends.CUDA)]
public class CudaOptimizedKernel
{
    // Use shared memory for high-frequency communication
    [ThreadgroupMemory(size: 48 * 1024)] // 48KB shared memory
    private byte[] _sharedBuffer;

    // Warp-level primitives for efficiency
    public void ProcessMessage(Message msg)
    {
        // Warp shuffle for intra-warp communication
        int warpResult = WarpReduce(msg.Value);

        // Use shared memory for inter-warp communication
        if (IsFirstThreadInWarp())
        {
            _sharedBuffer[GetWarpId()] = warpResult;
        }

        ThreadgroupBarrier();

        // Process aggregated results
        if (GetThreadId() == 0)
        {
            int totalResult = AggregateWarpResults(_sharedBuffer);
            SendResult(totalResult);
        }
    }
}
```

### Metal-Specific Optimizations

```csharp
// Metal-specific features for Apple Silicon
[RingKernel(
    Mode = RingKernelMode.Persistent,
    MessagingStrategy = MessagePassingStrategy.SharedMemory,
    Backend = KernelBackends.Metal)]
public class MetalOptimizedKernel
{
    // Use Metal Performance Shaders for common operations
    public void ProcessMessage(ImageMessage msg)
    {
        // Metal has unified memory - zero-copy access
        var image = AccessUnifiedBuffer(msg.ImageBuffer);

        // Use MPS for high-performance image processing
        var filtered = ApplyMPSGaussianBlur(image);

        SendResult(filtered);
    }
}
```

### OpenCL-Specific Optimizations

```csharp
// OpenCL cross-platform optimizations
[RingKernel(
    Mode = RingKernelMode.Persistent,
    MessagingStrategy = MessagePassingStrategy.AtomicQueue,
    Backend = KernelBackends.OpenCL)]
public class OpenCLOptimizedKernel
{
    // Detect device capabilities at runtime
    public void Initialize()
    {
        bool hasDoublePrecision = CheckExtension("cl_khr_fp64");
        bool hasAtomics = CheckExtension("cl_khr_int64_base_atomics");

        ConfigureForDevice(hasDoublePrecision, hasAtomics);
    }

    public void ProcessMessage(Message msg)
    {
        // Use device-appropriate precision
        if (_useDoublePrecision)
        {
            ProcessDouble(msg);
        }
        else
        {
            ProcessFloat(msg);
        }
    }
}
```

## Summary

Advanced Ring Kernel programming enables:
- ✅ Complex message patterns (producer-consumer, scatter-gather, pipelines)
- ✅ Sophisticated state management with eviction strategies
- ✅ Multi-kernel coordination with barriers and load balancing
- ✅ Performance optimization (batching, pooling, SIMD, lock-free)
- ✅ Production-grade error handling and recovery
- ✅ Comprehensive monitoring and graceful shutdown
- ✅ Backend-specific optimizations for maximum performance

Master these patterns to build scalable, high-performance GPU-resident applications!
