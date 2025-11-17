# Phase 3: Multi-Kernel Coordination - Design Document

**Status:** In Progress
**Created:** 2025-11-17
**Target Completion:** 2025-11-24 (7 days)

---

## Executive Summary

Phase 3 extends the Ring Kernel system with multi-kernel coordination capabilities, enabling complex GPU-resident workflows with message routing, pub/sub messaging, barrier synchronization, dynamic task distribution, and fault recovery.

### Key Goals
- **Kernel-to-Kernel Routing:** Direct message passing between ring kernels
- **Topic-Based Pub/Sub:** Decoupled messaging with topic subscriptions
- **Multi-Kernel Barriers:** Grid-wide and cross-kernel synchronization
- **Work-Stealing Queues:** Dynamic load balancing between kernels
- **Fault Tolerance:** Automatic detection and recovery from kernel failures

### Success Metrics
- ✅ Run 5+ kernel pipeline for 24 hours
- ✅ Handle 1M+ messages/sec throughput
- ✅ Automatic recovery from kernel failures
- ✅ Zero data loss during fault recovery

---

## Architecture Overview

### Component Hierarchy

```
Multi-Kernel Coordination System
├── Message Router
│   ├── Routing Table (GPU memory)
│   ├── Direct Routing (kernel ID → queue)
│   └── Topic Routing (topic → subscriber list)
│
├── Pub/Sub System
│   ├── Topic Registry (host + GPU)
│   ├── Subscription Manager
│   └── Message Dispatcher
│
├── Barrier Manager
│   ├── Grid-Wide Barriers (within kernel)
│   ├── Multi-Kernel Barriers (across kernels)
│   └── Barrier Coordination (host-assisted)
│
├── Task Queue System
│   ├── Per-Kernel Task Queues
│   ├── Work-Stealing Protocol
│   └── Load Balancing Heuristics
│
└── Fault Recovery System
    ├── Health Monitor (heartbeat tracking)
    ├── Failure Detector (timeout-based)
    └── Recovery Manager (kernel restart)
```

---

## Component 1: Message Router

### Design

**Purpose:** Route messages between ring kernels with minimal latency and maximum throughput.

**Routing Modes:**
1. **Direct Routing:** Message sent directly to specific kernel ID
2. **Topic Routing:** Message broadcast to all topic subscribers
3. **Load-Balanced Routing:** Round-robin or least-loaded kernel selection

### Data Structures

```csharp
/// <summary>
/// GPU-resident routing table for kernel-to-kernel messaging.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct KernelRoutingTable
{
    /// <summary>
    /// Number of active kernels in the system.
    /// </summary>
    public int KernelCount;

    /// <summary>
    /// Device pointer to array of kernel control blocks.
    /// </summary>
    public long KernelControlBlocksPtr;

    /// <summary>
    /// Device pointer to array of output queue pointers (one per kernel).
    /// </summary>
    public long OutputQueuesPtr;

    /// <summary>
    /// Device pointer to routing hash table (kernel ID → queue index).
    /// </summary>
    public long RoutingHashTablePtr;

    /// <summary>
    /// Hash table capacity (power of 2).
    /// </summary>
    public int HashTableCapacity;
}
```

### CUDA Implementation

```cuda
// File: KernelRouter.cu

/// <summary>
/// Routes a message to a specific kernel by ID.
/// </summary>
/// <param name="routing_table">Routing table with kernel queue mappings</param>
/// <param name="target_kernel_id">Target kernel identifier (hash of kernel name)</param>
/// <param name="message_buffer">Serialized message bytes</param>
/// <param name="message_size">Message size in bytes</param>
/// <returns>true if message was successfully enqueued, false if queue full</returns>
__device__ bool route_message_to_kernel(
    const kernel_routing_table* routing_table,
    uint32_t target_kernel_id,
    const unsigned char* message_buffer,
    int32_t message_size)
{
    // Hash table lookup to find queue index
    uint32_t hash = target_kernel_id % routing_table->hash_table_capacity;
    uint32_t* hash_table = (uint32_t*)routing_table->routing_hash_table_ptr;

    // Linear probing for collision resolution
    for (int probe = 0; probe < routing_table->hash_table_capacity; probe++)
    {
        uint32_t index = (hash + probe) % routing_table->hash_table_capacity;
        uint32_t entry = hash_table[index];

        uint32_t entry_kernel_id = entry >> 16;  // Upper 16 bits
        uint32_t queue_index = entry & 0xFFFF;   // Lower 16 bits

        if (entry_kernel_id == target_kernel_id)
        {
            // Found target kernel, enqueue message
            unsigned char** output_queues = (unsigned char**)routing_table->output_queues_ptr;
            unsigned char* target_queue = output_queues[queue_index];

            return enqueue_message(target_queue, message_buffer, message_size);
        }

        if (entry == 0)
        {
            // Empty slot, kernel not found
            return false;
        }
    }

    return false; // Kernel not found after full probe
}
```

---

## Component 2: Topic-Based Pub/Sub

### Design

**Purpose:** Decoupled messaging where kernels publish to topics and subscribers receive all messages for their subscribed topics.

**Topic Naming:** String-based topics hashed to 32-bit IDs (e.g., "physics.particles" → 0x7A3B9C12)

**Subscription Model:**
- Kernels subscribe to topics at initialization
- Messages published to topics are broadcast to all subscribers
- Supports wildcard subscriptions (e.g., "physics.*")

### Data Structures

```csharp
/// <summary>
/// Topic subscription entry in GPU memory.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TopicSubscription
{
    /// <summary>
    /// Topic ID (hash of topic name).
    /// </summary>
    public uint TopicId;

    /// <summary>
    /// Subscriber kernel ID.
    /// </summary>
    public uint KernelId;

    /// <summary>
    /// Queue index for this subscriber.
    /// </summary>
    public ushort QueueIndex;

    /// <summary>
    /// Subscription flags (wildcard, priority, etc.).
    /// </summary>
    public ushort Flags;
}

/// <summary>
/// Topic registry for pub/sub messaging.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TopicRegistry
{
    /// <summary>
    /// Number of active subscriptions.
    /// </summary>
    public int SubscriptionCount;

    /// <summary>
    /// Device pointer to array of TopicSubscription entries.
    /// </summary>
    public long SubscriptionsPtr;

    /// <summary>
    /// Device pointer to topic hash table (topic ID → subscription index).
    /// </summary>
    public long TopicHashTablePtr;

    /// <summary>
    /// Hash table capacity.
    /// </summary>
    public int HashTableCapacity;
}
```

### CUDA Implementation

```cuda
// File: TopicPubSub.cu

/// <summary>
/// Publishes a message to all subscribers of a topic.
/// </summary>
__device__ int publish_to_topic(
    const topic_registry* registry,
    const kernel_routing_table* routing_table,
    uint32_t topic_id,
    const unsigned char* message_buffer,
    int32_t message_size)
{
    int messages_sent = 0;

    // Linear scan through subscriptions (can be optimized with hash table)
    topic_subscription* subscriptions = (topic_subscription*)registry->subscriptions_ptr;

    for (int i = 0; i < registry->subscription_count; i++)
    {
        if (subscriptions[i].topic_id == topic_id)
        {
            uint32_t kernel_id = subscriptions[i].kernel_id;

            // Route message to subscriber
            if (route_message_to_kernel(routing_table, kernel_id, message_buffer, message_size))
            {
                messages_sent++;
            }
        }
    }

    return messages_sent;
}
```

---

## Component 3: Barrier Synchronization

### Design

**Purpose:** Synchronize execution across threads, blocks, and multiple kernels.

**Barrier Scopes:**
1. **Thread Block Barrier:** All threads in a block (`__syncthreads()`)
2. **Grid-Wide Barrier:** All blocks in a kernel (cooperative groups)
3. **Multi-Kernel Barrier:** All participating kernels (host-coordinated)

### Multi-Kernel Barrier Protocol

**Phase 1: Arrival**
- Each kernel atomically increments global arrival counter
- Kernel spins/sleeps until counter reaches barrier participant count

**Phase 2: Departure**
- Host or designated kernel resets arrival counter
- All kernels proceed past barrier

### Data Structures

```csharp
/// <summary>
/// Multi-kernel barrier state in GPU memory.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct MultiKernelBarrier
{
    /// <summary>
    /// Number of kernels that must arrive at barrier.
    /// </summary>
    public int ParticipantCount;

    /// <summary>
    /// Atomic counter for arrived kernels.
    /// </summary>
    public int ArrivedCount;

    /// <summary>
    /// Barrier generation number (incremented after each barrier completion).
    /// </summary>
    public int Generation;

    /// <summary>
    /// Barrier flags (active, timeout, etc.).
    /// </summary>
    public int Flags;
}
```

### CUDA Implementation

```cuda
// File: MultiKernelBarrier.cu

/// <summary>
/// Waits at a multi-kernel barrier.
/// </summary>
__device__ void wait_at_multi_kernel_barrier(
    multi_kernel_barrier* barrier,
    int timeout_cycles)
{
    // Record arrival generation
    int my_generation = barrier->generation;

    // Atomically increment arrival count
    int arrived = atomicAdd(&barrier->arrived_count, 1) + 1;

    // Last kernel to arrive resets for next barrier
    if (arrived == barrier->participant_count)
    {
        atomicExch(&barrier->arrived_count, 0);
        atomicAdd(&barrier->generation, 1);
        __threadfence_system(); // Ensure visibility to all kernels
        return;
    }

    // Spin-wait for barrier completion (generation change)
    long long start_cycle = clock64();
    while (barrier->generation == my_generation)
    {
        if (timeout_cycles > 0 && (clock64() - start_cycle) > timeout_cycles)
        {
            // Timeout - mark barrier as failed
            atomicOr(&barrier->flags, BARRIER_FLAG_TIMEOUT);
            return;
        }

        __nanosleep(100); // Sleep 100ns to reduce power consumption
    }
}
```

---

## Component 4: Dynamic Task Queues with Work-Stealing

### Design

**Purpose:** Distribute work dynamically between kernels for load balancing.

**Work-Stealing Protocol:**
1. Each kernel has a local task queue (deque)
2. Idle kernels attempt to steal tasks from busy kernels
3. Uses lock-free algorithms for high concurrency

**Stealing Strategy:**
- **Victim Selection:** Random or least-recently-checked kernel
- **Steal Amount:** Half of victim's queue (batch stealing)
- **Backoff:** Exponential backoff on failed steal attempts

### Data Structures

```csharp
/// <summary>
/// Per-kernel task queue with work-stealing support.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TaskQueue
{
    /// <summary>
    /// Atomic head pointer (for local push/pop - owner side).
    /// </summary>
    public long Head;

    /// <summary>
    /// Atomic tail pointer (for remote steal - thief side).
    /// </summary>
    public long Tail;

    /// <summary>
    /// Queue capacity (power of 2).
    /// </summary>
    public int Capacity;

    /// <summary>
    /// Device pointer to task array.
    /// </summary>
    public long TasksPtr;

    /// <summary>
    /// Owner kernel ID.
    /// </summary>
    public uint OwnerId;

    /// <summary>
    /// Queue flags (active, stealing-enabled, etc.).
    /// </summary>
    public uint Flags;
}
```

### CUDA Implementation

```cuda
// File: WorkStealingQueue.cu

/// <summary>
/// Attempts to steal tasks from another kernel's queue.
/// </summary>
__device__ int steal_tasks(
    task_queue* victim_queue,
    task_queue* my_queue,
    int max_steal_count)
{
    // Read tail atomically
    long tail = atomicAdd(&victim_queue->tail, 0);  // Atomic read
    long head = atomicAdd(&victim_queue->head, 0);  // Atomic read

    long queue_size = head - tail;
    if (queue_size <= 1)
    {
        return 0; // Nothing to steal (keep 1 task for owner)
    }

    // Steal up to half of victim's queue
    int steal_count = min(max_steal_count, (int)(queue_size / 2));

    // Atomically increment victim's tail
    long steal_tail = atomicAdd(&victim_queue->tail, steal_count);

    // Verify we didn't race with victim's pop
    long current_head = atomicAdd(&victim_queue->head, 0);
    long actual_available = current_head - steal_tail;

    if (actual_available < steal_count)
    {
        // Race detected, return stolen slots
        atomicAdd(&victim_queue->tail, -(steal_count - (int)actual_available));
        steal_count = max(0, (int)actual_available);
    }

    // Copy stolen tasks to my queue
    task_descriptor* victim_tasks = (task_descriptor*)victim_queue->tasks_ptr;
    task_descriptor* my_tasks = (task_descriptor*)my_queue->tasks_ptr;

    int my_head = atomicAdd(&my_queue->head, steal_count);
    int mask = my_queue->capacity - 1;

    for (int i = 0; i < steal_count; i++)
    {
        int victim_index = (steal_tail + i) & (victim_queue->capacity - 1);
        int my_index = (my_head + i) & mask;
        my_tasks[my_index] = victim_tasks[victim_index];
    }

    return steal_count;
}
```

---

## Component 5: Fault Tolerance and Recovery

### Design

**Purpose:** Detect kernel failures and automatically recover without data loss.

**Failure Detection:**
- **Heartbeat Monitoring:** Each kernel updates timestamp periodically
- **Timeout Detection:** Host monitors for stale timestamps
- **Error Counter Threshold:** Too many errors triggers failure

**Recovery Strategies:**
1. **Checkpoint/Restore:** Periodic state snapshots
2. **Message Replay:** Re-send messages from last checkpoint
3. **Kernel Restart:** Relaunch failed kernel with restored state

### Data Structures

```csharp
/// <summary>
/// Health monitoring data for a ring kernel.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct KernelHealthStatus
{
    /// <summary>
    /// Last heartbeat timestamp (ticks).
    /// </summary>
    public long LastHeartbeatTicks;

    /// <summary>
    /// Consecutive failed heartbeats.
    /// </summary>
    public int FailedHeartbeats;

    /// <summary>
    /// Total errors encountered.
    /// </summary>
    public int ErrorCount;

    /// <summary>
    /// Kernel state (healthy, degraded, failed).
    /// </summary>
    public int State;

    /// <summary>
    /// Last checkpoint ID.
    /// </summary>
    public long LastCheckpointId;
}
```

### Recovery Manager (Host-Side)

```csharp
/// <summary>
/// Monitors kernel health and triggers recovery on failure.
/// </summary>
public class KernelRecoveryManager
{
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(5);
    private readonly int _maxConsecutiveErrors = 10;

    public async Task MonitorKernelsAsync(
        IReadOnlyList<RingKernelInstance> kernels,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            foreach (var kernel in kernels)
            {
                var health = await ReadHealthStatusAsync(kernel);

                // Check for stale heartbeat
                var timeSinceHeartbeat = DateTime.UtcNow.Ticks - health.LastHeartbeatTicks;
                if (timeSinceHeartbeat > _heartbeatTimeout.Ticks)
                {
                    await HandleKernelFailureAsync(kernel, "Heartbeat timeout");
                    continue;
                }

                // Check for excessive errors
                if (health.ErrorCount > _maxConsecutiveErrors)
                {
                    await HandleKernelFailureAsync(kernel, "Error threshold exceeded");
                }
            }
        }
    }

    private async Task HandleKernelFailureAsync(
        RingKernelInstance kernel,
        string reason)
    {
        // 1. Mark kernel as failed
        kernel.State = KernelState.Failed;

        // 2. Load last checkpoint
        var checkpoint = await LoadCheckpointAsync(kernel);

        // 3. Restart kernel with restored state
        var newKernel = await RestartKernelAsync(kernel, checkpoint);

        // 4. Replay messages since checkpoint
        await ReplayMessagesAsync(newKernel, checkpoint.MessageLogId);

        // 5. Resume normal operation
        newKernel.State = KernelState.Running;
    }
}
```

---

## Implementation Plan

### Week 1: Foundation (Days 1-2)
- [ ] Implement `KernelRoutingTable` and direct routing
- [ ] Implement `TopicRegistry` and pub/sub system
- [ ] Write unit tests for routing and pub/sub

### Week 1: Synchronization (Days 3-4)
- [ ] Implement `MultiKernelBarrier` with spin-wait
- [ ] Implement grid-wide barrier using cooperative groups
- [ ] Write integration tests for barriers

### Week 2: Work-Stealing (Days 5-6)
- [ ] Implement `TaskQueue` with lock-free deque
- [ ] Implement work-stealing protocol with random victim selection
- [ ] Write performance tests for load balancing

### Week 2: Fault Recovery (Day 7)
- [ ] Implement `KernelHealthStatus` monitoring
- [ ] Implement checkpoint/restore mechanism
- [ ] Implement `KernelRecoveryManager`
- [ ] Write end-to-end recovery tests

### Integration & Validation
- [ ] Multi-kernel pipeline test (5+ kernels)
- [ ] Throughput benchmark (target: 1M+ msg/sec)
- [ ] 24-hour stability test
- [ ] Fault injection testing

---

## Performance Targets

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Message Routing Latency | < 1μs | GPU timer (clock64()) |
| Topic Pub/Sub Latency | < 5μs | GPU timer (clock64()) |
| Barrier Synchronization | < 100μs | Grid-wide cooperative groups |
| Work-Stealing Overhead | < 2% | Comparison with static allocation |
| Message Throughput | > 1M msg/sec | Integration test with counter |
| Failure Detection Time | < 5 sec | Heartbeat timeout |
| Recovery Time | < 10 sec | Checkpoint restore + replay |

---

## Success Criteria

✅ **Functionality:**
- Kernel-to-kernel routing with 99.99% success rate
- Topic pub/sub with broadcast to all subscribers
- Multi-kernel barriers complete without deadlock
- Work-stealing achieves load balance (stddev < 10%)
- Fault recovery with zero data loss

✅ **Performance:**
- 1M+ messages/sec sustained throughput
- Sub-microsecond routing latency
- 24-hour stability test (no crashes, no memory leaks)

✅ **Quality:**
- 90%+ test coverage
- Production-ready error handling
- Comprehensive logging and telemetry

---

## Appendix A: Message Format Extensions

To support multi-kernel coordination, we extend `IRingKernelMessage` with routing metadata:

```csharp
public interface IRoutableMessage : IRingKernelMessage
{
    /// <summary>
    /// Target kernel ID (0 = broadcast to topic).
    /// </summary>
    uint TargetKernelId { get; set; }

    /// <summary>
    /// Topic ID for pub/sub routing (0 = direct routing).
    /// </summary>
    uint TopicId { get; set; }

    /// <summary>
    /// Routing flags (require-ack, timeout, etc.).
    /// </summary>
    byte RoutingFlags { get; set; }
}
```

---

## Appendix B: CUDA Kernel Template

```cuda
// Ring kernel with multi-kernel coordination support

__global__ void cooperative_ring_kernel(
    ring_kernel_control_block* control_block,
    kernel_routing_table* routing_table,
    topic_registry* topic_registry,
    multi_kernel_barrier* barrier,
    task_queue* my_queue,
    task_queue* all_queues,  // Array of all kernel queues
    int kernel_count)
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    while (!control_block->should_terminate)
    {
        // Process local tasks
        task_descriptor task;
        if (pop_task(my_queue, &task))
        {
            process_task(&task, control_block, routing_table);
            continue;
        }

        // Attempt work-stealing from random victim
        int victim_index = (tid + clock()) % kernel_count;
        if (steal_tasks(&all_queues[victim_index], my_queue, 8) > 0)
        {
            continue; // Successfully stole work
        }

        // Wait at barrier if no work available
        if (barrier->participant_count > 0)
        {
            wait_at_multi_kernel_barrier(barrier, 1000000); // 1ms timeout
        }

        // Update heartbeat
        if (tid == 0)
        {
            control_block->last_activity_ticks = clock64();
        }
    }

    // Mark as terminated
    if (tid == 0)
    {
        atomicExch(&control_block->has_terminated, 1);
    }
}
```

---

**Document Version:** 1.0
**Last Updated:** 2025-11-17
**Author:** Claude Code + Michael Ivertowski
**Status:** Design Complete - Ready for Implementation
