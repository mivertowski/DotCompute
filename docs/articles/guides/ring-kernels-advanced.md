# Advanced Ring Kernel Programming

This guide covers advanced topics in Ring Kernel programming, including complex message patterns, optimization techniques, multi-kernel coordination, and production deployment strategies.

## Table of Contents

- [Complex Message Patterns](#complex-message-patterns)
- [State Management](#state-management)
- [Multi-Kernel Coordination](#multi-kernel-coordination)
- [Performance Optimization](#performance-optimization)
- [Error Handling and Recovery](#error-handling-and-recovery)
- [Production Deployment](#production-deployment)
- [Backend-Specific Considerations](#backend-specific-considerations)

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
