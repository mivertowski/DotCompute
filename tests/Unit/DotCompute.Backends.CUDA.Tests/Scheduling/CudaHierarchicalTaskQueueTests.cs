// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Scheduling;
using DotCompute.Abstractions.Temporal;
using DotCompute.Abstractions.Timing;
using DotCompute.Backends.CUDA.Scheduling;
using DotCompute.Backends.CUDA.Temporal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Scheduling;

/// <summary>
/// Unit tests for CudaHierarchicalTaskQueue implementation.
/// Tests priority-based scheduling, HLC temporal ordering, work stealing, and rebalancing.
/// </summary>
public sealed class CudaHierarchicalTaskQueueTests : IDisposable
{
    private readonly IHybridLogicalClock _mockHlc;
    private readonly List<CudaHierarchicalTaskQueue> _queues = new();

    public CudaHierarchicalTaskQueueTests()
    {
        var timingProvider = Substitute.For<ITimingProvider>();
        timingProvider.GetGpuTimestampAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(DateTime.UtcNow.Ticks * 100L));

        _mockHlc = new CudaHybridLogicalClock(timingProvider);
    }

    public void Dispose()
    {
        foreach (var queue in _queues)
        {
            queue?.Dispose();
        }
        _queues.Clear();

        (_mockHlc as IDisposable)?.Dispose();
    }

    private CudaHierarchicalTaskQueue CreateQueue(string queueId = "test-queue")
    {
        var queue = new CudaHierarchicalTaskQueue(queueId, _mockHlc, NullLogger<CudaHierarchicalTaskQueue>.Instance);
        _queues.Add(queue);
        return queue;
    }

    private async Task<PrioritizedTask> CreateTaskAsync(
        TaskPriority priority = TaskPriority.Normal,
        TaskFlags flags = TaskFlags.Stealable | TaskFlags.PromotionEligible)
    {
        var timestamp = await _mockHlc.TickAsync();

        return new PrioritizedTask
        {
            TaskId = Guid.NewGuid(),
            Priority = priority,
            EnqueueTimestamp = timestamp,
            DataPointer = IntPtr.Zero,
            DataSize = 1024,
            KernelId = 0,
            Flags = flags
        };
    }

    // --- Constructor Tests ---

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        var queue = CreateQueue("test-queue");

        queue.Should().NotBeNull();
        queue.QueueId.Should().Be("test-queue");
        queue.TotalTaskCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullQueueId_ShouldThrow()
    {
        Action act = () => new CudaHierarchicalTaskQueue(null!, _mockHlc);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyQueueId_ShouldThrow()
    {
        Action act = () => new CudaHierarchicalTaskQueue("", _mockHlc);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullHlc_ShouldThrow()
    {
        Action act = () => new CudaHierarchicalTaskQueue("test", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // --- Enqueue/Dequeue Tests ---

    [Fact]
    public async Task TryEnqueue_SingleTask_ShouldSucceed()
    {
        var queue = CreateQueue();
        var task = await CreateTaskAsync(TaskPriority.Normal);
        var timestamp = await _mockHlc.TickAsync();

        var result = queue.TryEnqueue(task, TaskPriority.Normal, timestamp);

        result.Should().BeTrue();
        queue.TotalTaskCount.Should().Be(1);
        queue.PriorityCounts.NormalPriority.Should().Be(1);
    }

    [Fact]
    public async Task TryDequeue_EmptyQueue_ShouldReturnFalse()
    {
        var queue = CreateQueue();

        var result = queue.TryDequeue(out var task);

        result.Should().BeFalse();
        task.Should().Be(default(PrioritizedTask));
    }

    [Fact]
    public async Task TryDequeue_AfterEnqueue_ShouldReturnTask()
    {
        var queue = CreateQueue();
        var originalTask = await CreateTaskAsync(TaskPriority.Normal);
        var timestamp = await _mockHlc.TickAsync();

        queue.TryEnqueue(originalTask, TaskPriority.Normal, timestamp);

        var result = queue.TryDequeue(out var dequeuedTask);

        result.Should().BeTrue();
        dequeuedTask.TaskId.Should().Be(originalTask.TaskId);
        queue.TotalTaskCount.Should().Be(0);
    }

    // --- Priority Ordering Tests ---

    [Fact]
    public async Task TryDequeue_MultiplePriorities_ShouldDequeueHighPriorityFirst()
    {
        var queue = CreateQueue();

        var lowTask = await CreateTaskAsync(TaskPriority.Low);
        var normalTask = await CreateTaskAsync(TaskPriority.Normal);
        var highTask = await CreateTaskAsync(TaskPriority.High);

        // Enqueue in reverse order
        queue.TryEnqueue(lowTask, TaskPriority.Low, await _mockHlc.TickAsync());
        queue.TryEnqueue(normalTask, TaskPriority.Normal, await _mockHlc.TickAsync());
        queue.TryEnqueue(highTask, TaskPriority.High, await _mockHlc.TickAsync());

        // Dequeue should return high priority first
        queue.TryDequeue(out var first);
        first.TaskId.Should().Be(highTask.TaskId);

        queue.TryDequeue(out var second);
        second.TaskId.Should().Be(normalTask.TaskId);

        queue.TryDequeue(out var third);
        third.TaskId.Should().Be(lowTask.TaskId);
    }

    // --- HLC Temporal Ordering Tests ---

    [Fact]
    public async Task TryDequeue_SamePriority_ShouldDequeueBHlcTimestampFirst()
    {
        var queue = CreateQueue();

        var timestamp1 = await _mockHlc.TickAsync();
        await Task.Delay(10); // Ensure different timestamps
        var timestamp2 = await _mockHlc.TickAsync();
        await Task.Delay(10);
        var timestamp3 = await _mockHlc.TickAsync();

        var task1 = await CreateTaskAsync(TaskPriority.Normal);
        var task2 = await CreateTaskAsync(TaskPriority.Normal);
        var task3 = await CreateTaskAsync(TaskPriority.Normal);

        // Enqueue in reverse chronological order
        queue.TryEnqueue(task3, TaskPriority.Normal, timestamp3);
        queue.TryEnqueue(task1, TaskPriority.Normal, timestamp1);
        queue.TryEnqueue(task2, TaskPriority.Normal, timestamp2);

        // Should dequeue in HLC timestamp order (earliest first)
        queue.TryDequeue(out var first);
        first.EnqueueTimestamp.Should().Be(timestamp1);

        queue.TryDequeue(out var second);
        second.EnqueueTimestamp.Should().Be(timestamp2);

        queue.TryDequeue(out var third);
        third.EnqueueTimestamp.Should().Be(timestamp3);
    }

    // --- Work Stealing Tests ---

    [Fact]
    public async Task TrySteal_EmptyQueue_ShouldReturnFalse()
    {
        var queue = CreateQueue();

        var result = queue.TrySteal(out var task);

        result.Should().BeFalse();
        task.Should().Be(default(PrioritizedTask));
    }

    [Fact]
    public async Task TrySteal_StealableTask_ShouldSucceed()
    {
        var queue = CreateQueue();
        var task = await CreateTaskAsync(TaskPriority.Normal, TaskFlags.Stealable);
        var timestamp = await _mockHlc.TickAsync();

        queue.TryEnqueue(task, TaskPriority.Normal, timestamp);

        var result = queue.TrySteal(out var stolenTask);

        result.Should().BeTrue();
        stolenTask.TaskId.Should().Be(task.TaskId);
        queue.TotalTaskCount.Should().Be(0);
    }

    [Fact]
    public async Task TrySteal_NonStealableTask_ShouldReturnFalse()
    {
        var queue = CreateQueue();
        var task = await CreateTaskAsync(TaskPriority.Normal, TaskFlags.None); // Not stealable
        var timestamp = await _mockHlc.TickAsync();

        queue.TryEnqueue(task, TaskPriority.Normal, timestamp);

        var result = queue.TrySteal(out var stolenTask);

        result.Should().BeFalse();
        queue.TotalTaskCount.Should().Be(1); // Task still in queue
    }

    [Fact]
    public async Task TrySteal_PreferredPriority_ShouldStealFromThatLevel()
    {
        var queue = CreateQueue();

        var highTask = await CreateTaskAsync(TaskPriority.High, TaskFlags.Stealable);
        var normalTask = await CreateTaskAsync(TaskPriority.Normal, TaskFlags.Stealable);

        queue.TryEnqueue(highTask, TaskPriority.High, await _mockHlc.TickAsync());
        queue.TryEnqueue(normalTask, TaskPriority.Normal, await _mockHlc.TickAsync());

        // Prefer stealing from Normal priority
        var result = queue.TrySteal(out var stolenTask, TaskPriority.Normal);

        result.Should().BeTrue();
        stolenTask.TaskId.Should().Be(normalTask.TaskId);
    }

    // --- Peek Tests ---

    [Fact]
    public async Task TryPeek_EmptyQueue_ShouldReturnFalse()
    {
        var queue = CreateQueue();

        var result = queue.TryPeek(out var task);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryPeek_WithTasks_ShouldReturnHighestPriorityWithoutRemoving()
    {
        var queue = CreateQueue();

        var normalTask = await CreateTaskAsync(TaskPriority.Normal);
        var highTask = await CreateTaskAsync(TaskPriority.High);

        queue.TryEnqueue(normalTask, TaskPriority.Normal, await _mockHlc.TickAsync());
        queue.TryEnqueue(highTask, TaskPriority.High, await _mockHlc.TickAsync());

        var result = queue.TryPeek(out var peekedTask);

        result.Should().BeTrue();
        peekedTask.TaskId.Should().Be(highTask.TaskId);
        queue.TotalTaskCount.Should().Be(2); // Tasks still in queue
    }

    // --- Age-Based Promotion Tests ---

    [Fact]
    public async Task PromoteAgedTasks_OldTask_ShouldPromote()
    {
        var queue = CreateQueue();

        // Create timestamps with explicit values
        var oldTimestamp = new HlcTimestamp
        {
            PhysicalTimeNanos = 1_000_000_000L, // 1 second after epoch
            LogicalCounter = 0
        };

        var currentTime = new HlcTimestamp
        {
            PhysicalTimeNanos = 3_500_000_000L, // 3.5 seconds after epoch (2.5 seconds later)
            LogicalCounter = 0
        };

        var task = new PrioritizedTask
        {
            TaskId = Guid.NewGuid(),
            Priority = TaskPriority.Low,
            EnqueueTimestamp = oldTimestamp,
            DataPointer = IntPtr.Zero,
            DataSize = 1024,
            KernelId = 0,
            Flags = TaskFlags.Stealable | TaskFlags.PromotionEligible
        };

        queue.TryEnqueue(task, TaskPriority.Low, oldTimestamp);

        // Verify task is in Low priority queue
        queue.PriorityCounts.LowPriority.Should().Be(1);

        // Promote with current time (task is 2.5 seconds old, threshold is 1 second)
        var promotedCount = queue.PromoteAgedTasks(currentTime, TimeSpan.FromSeconds(1));

        promotedCount.Should().BeGreaterThan(0);
        queue.PriorityCounts.LowPriority.Should().Be(0);
        queue.PriorityCounts.NormalPriority.Should().Be(1);
    }

    [Fact]
    public async Task PromoteAgedTasks_NewTask_ShouldNotPromote()
    {
        var queue = CreateQueue();

        var recentTimestamp = await _mockHlc.TickAsync();
        var task = await CreateTaskAsync(TaskPriority.Low, TaskFlags.PromotionEligible);

        queue.TryEnqueue(task, TaskPriority.Low, recentTimestamp);

        var currentTime = await _mockHlc.TickAsync();

        var promotedCount = queue.PromoteAgedTasks(currentTime, TimeSpan.FromSeconds(10));

        promotedCount.Should().Be(0);
        queue.PriorityCounts.LowPriority.Should().Be(1);
    }

    [Fact]
    public async Task PromoteAgedTasks_NonEligibleTask_ShouldNotPromote()
    {
        var queue = CreateQueue();

        var oldTimestamp = await _mockHlc.TickAsync();
        var task = await CreateTaskAsync(TaskPriority.Low, TaskFlags.None); // Not eligible

        queue.TryEnqueue(task, TaskPriority.Low, oldTimestamp);

        await Task.Delay(50);
        var currentTime = await _mockHlc.TickAsync();

        var futureTime = new HlcTimestamp
        {
            PhysicalTimeNanos = currentTime.PhysicalTimeNanos + (2L * 1_000_000_000L),
            LogicalCounter = currentTime.LogicalCounter
        };

        var promotedCount = queue.PromoteAgedTasks(futureTime, TimeSpan.FromSeconds(1));

        promotedCount.Should().Be(0);
        queue.PriorityCounts.LowPriority.Should().Be(1);
    }

    // --- Rebalancing Tests ---

    [Fact]
    public async Task Rebalance_HighLoad_ShouldDemoteTasks()
    {
        var queue = CreateQueue();

        // Add many normal priority tasks
        for (int i = 0; i < 20; i++)
        {
            var task = await CreateTaskAsync(TaskPriority.Normal);
            queue.TryEnqueue(task, TaskPriority.Normal, await _mockHlc.TickAsync());
        }

        var initialNormalCount = queue.PriorityCounts.NormalPriority;

        var result = queue.Rebalance(0.9); // High load

        result.DemotedCount.Should().BeGreaterThan(0);
        queue.PriorityCounts.NormalPriority.Should().BeLessThan(initialNormalCount);
        queue.PriorityCounts.LowPriority.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Rebalance_LowLoad_ShouldPromoteTasks()
    {
        var queue = CreateQueue();

        // Add low priority tasks
        for (int i = 0; i < 10; i++)
        {
            var task = await CreateTaskAsync(TaskPriority.Low, TaskFlags.PromotionEligible);
            queue.TryEnqueue(task, TaskPriority.Low, await _mockHlc.TickAsync());
        }

        var initialLowCount = queue.PriorityCounts.LowPriority;

        var result = queue.Rebalance(0.1); // Low load

        result.PromotedCount.Should().BeGreaterThan(0);
        queue.PriorityCounts.LowPriority.Should().BeLessThan(initialLowCount);
        queue.PriorityCounts.NormalPriority.Should().BeGreaterThan(0);
    }

    // --- Statistics Tests ---

    [Fact]
    public async Task GetStatistics_EmptyQueue_ShouldReturnZeroCounts()
    {
        var queue = CreateQueue();

        var stats = queue.GetStatistics();

        stats.Counts.Total.Should().Be(0);
        stats.TotalEnqueued.Should().Be(0);
        stats.TotalDequeued.Should().Be(0);
        stats.TotalStolen.Should().Be(0);
    }

    [Fact]
    public async Task GetStatistics_AfterOperations_ShouldReflectActivity()
    {
        var queue = CreateQueue();

        var task1 = await CreateTaskAsync(TaskPriority.High);
        var task2 = await CreateTaskAsync(TaskPriority.Normal);

        queue.TryEnqueue(task1, TaskPriority.High, await _mockHlc.TickAsync());
        queue.TryEnqueue(task2, TaskPriority.Normal, await _mockHlc.TickAsync());
        queue.TryDequeue(out _);

        var stats = queue.GetStatistics();

        stats.TotalEnqueued.Should().Be(2);
        stats.TotalDequeued.Should().Be(1);
        stats.Counts.Total.Should().Be(1);
    }

    // --- Clear Tests ---

    [Fact]
    public async Task Clear_WithTasks_ShouldRemoveAll()
    {
        var queue = CreateQueue();

        for (int i = 0; i < 10; i++)
        {
            var task = await CreateTaskAsync((TaskPriority)(i % 3));
            queue.TryEnqueue(task, task.Priority, await _mockHlc.TickAsync());
        }

        queue.Clear();

        queue.TotalTaskCount.Should().Be(0);
        queue.PriorityCounts.Total.Should().Be(0);
    }

    // --- Disposal Tests ---

    [Fact]
    public void Dispose_MultipleTimes_ShouldBeIdempotent()
    {
        var queue = CreateQueue();

        queue.Dispose();
        Action act = () => queue.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task TryEnqueue_AfterDisposal_ShouldThrow()
    {
        var queue = CreateQueue();
        queue.Dispose();

        var task = await CreateTaskAsync();
        var timestamp = await _mockHlc.TickAsync();

        Action act = () => queue.TryEnqueue(task, TaskPriority.Normal, timestamp);

        act.Should().Throw<ObjectDisposedException>();
    }

    // --- Stress Tests ---

    [Fact]
    public async Task StressTest_1000Tasks_ShouldMaintainOrdering()
    {
        var queue = CreateQueue();
        const int taskCount = 1000;

        var enqueuedTasks = new List<(PrioritizedTask task, HlcTimestamp timestamp)>();

        // Enqueue 1000 tasks with varying priorities
        for (int i = 0; i < taskCount; i++)
        {
            var priority = (TaskPriority)(i % 3);
            var task = await CreateTaskAsync(priority);
            var timestamp = await _mockHlc.TickAsync();

            queue.TryEnqueue(task, priority, timestamp);
            enqueuedTasks.Add((task, timestamp));
        }

        queue.TotalTaskCount.Should().Be(taskCount);

        // Dequeue all and verify priority ordering
        TaskPriority? lastPriority = null;
        int dequeuedCount = 0;

        while (queue.TryDequeue(out var task))
        {
            if (lastPriority.HasValue)
            {
                // Priority should be same or lower (Higher value = lower priority)
                ((int)task.Priority).Should().BeGreaterThanOrEqualTo((int)lastPriority.Value);
            }

            lastPriority = task.Priority;
            dequeuedCount++;
        }

        dequeuedCount.Should().Be(taskCount);
    }

    [Fact]
    public async Task ConcurrentEnqueueDequeue_ShouldBeThreadSafe()
    {
        var queue = CreateQueue();
        const int operationsPerThread = 100;
        const int threadCount = 4;

        var enqueueTask = Task.Run(async () =>
        {
            for (int i = 0; i < operationsPerThread; i++)
            {
                var task = await CreateTaskAsync(TaskPriority.Normal);
                var timestamp = await _mockHlc.TickAsync();
                queue.TryEnqueue(task, TaskPriority.Normal, timestamp);
            }
        });

        var dequeueTask = Task.Run(() =>
        {
            int dequeueCount = 0;
            while (dequeueCount < operationsPerThread)
            {
                if (queue.TryDequeue(out _))
                {
                    dequeueCount++;
                }
            }
        });

        await Task.WhenAll(enqueueTask, dequeueTask);

        // Final count should be 0 (all dequeued)
        queue.TotalTaskCount.Should().Be(0);
    }
}
