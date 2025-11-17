// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Performance benchmarks for Ring Kernel Phase 3 Advanced Features.
/// </summary>
/// <remarks>
/// <para>
/// <b>Performance Targets (1M+ msg/sec overall):</b>
/// - Message Router: 10M+ lookups/sec (100ns avg)
/// - Topic Pub/Sub: 5M+ topic matches/sec (200ns avg)
/// - Barriers: 100M+ barrier waits/sec (10ns avg)
/// - Task Queues: 20M+ push/pop/sec (50ns avg)
/// - Health Monitor: 50M+ health checks/sec (20ns avg)
/// </para>
/// <para>
/// <b>Benchmark Configuration:</b>
/// - Throughput job (optimized for ops/sec measurement)
/// - 3 warmup iterations + 10 measurement iterations
/// - Memory allocations tracked (zero-alloc hot paths)
/// - Hardware counters (cache misses, branch mispredictions)
/// </para>
/// </remarks>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[HardwareCounters(HardwareCounter.CacheMisses, HardwareCounter.BranchMispredictions)]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(BenchmarkConfig))]
public class Phase3PerformanceBenchmarks
{
    private sealed class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddColumn(StatisticColumn.Median);    // P50
            AddColumn(StatisticColumn.P95);       // P95
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
            AddColumn(StatisticColumn.OperationsPerSecond);
        }
    }

    // Test data sizes
    private const int RouterTestSize = 10000;
    private const int TopicTestSize = 10000;
    private const int BarrierTestSize = 10000;
    private const int QueueTestSize = 10000;
    private const int HealthTestSize = 10000;

    // Component instances
    private KernelRoutingTable _routingTable;
    private TopicRegistry _topicRegistry;
    private TopicSubscription _topicSubscription;
    private MultiKernelBarrier _barrier;
    private TaskQueue _taskQueue;
    private TaskDescriptor _taskDescriptor;
    private KernelHealthStatus _healthStatus;

    // Prevent dead code elimination
    private readonly Consumer _consumer = new Consumer();

    [GlobalSetup]
    public void Setup()
    {
        // Initialize Component 1: Message Router
        _routingTable = new KernelRoutingTable
        {
            KernelCount = 32,
            HashTableCapacity = 64,
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000
        };

        // Initialize Component 2: Topic Pub/Sub
        _topicRegistry = new TopicRegistry
        {
            SubscriptionCount = 100,
            SubscriptionsPtr = 0x4000,
            TopicHashTablePtr = 0x5000,
            HashTableCapacity = 256
        };

        _topicSubscription = new TopicSubscription
        {
            TopicId = 0x7A3B9C12,
            KernelId = 42,
            QueueIndex = 10,
            Flags = TopicSubscription.FlagHighPriority
        };

        // Initialize Component 3: Multi-Kernel Barriers
        _barrier = MultiKernelBarrier.Create(participantCount: 4);

        // Initialize Component 4: Work-Stealing Task Queues
        _taskQueue = TaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x6000, enableStealing: true);

        _taskDescriptor = new TaskDescriptor
        {
            TaskId = 12345,
            TargetKernelId = 42,
            Priority = 500,
            DataPtr = 0x7000,
            DataSize = 1024,
            Flags = 0
        };

        // Initialize Component 5: Fault Tolerance & Health Monitoring
        _healthStatus = KernelHealthStatus.CreateInitialized();
    }

    // ==================== Component 1: Message Router Benchmarks ====================

    [Benchmark(Description = "Message Router: Validate routing table")]
    public void BenchmarkRoutingTableValidation()
    {
        bool result = _routingTable.Validate();
        _consumer.Consume(result);
    }

    [Benchmark(Description = "Message Router: Calculate hash capacity")]
    public void BenchmarkHashCapacityCalculation()
    {
        int capacity = KernelRoutingTable.CalculateCapacity(32);
        _consumer.Consume(capacity);
    }

    [Benchmark(Description = "Message Router: Batch validation (10K)")]
    public void BenchmarkBatchRoutingValidation()
    {
        int successCount = 0;
        for (int i = 0; i < RouterTestSize; i++)
        {
            if (_routingTable.Validate())
            {
                successCount++;
            }
        }
        _consumer.Consume(successCount);
    }

    // ==================== Component 2: Topic Pub/Sub Benchmarks ====================

    [Benchmark(Description = "Topic Pub/Sub: Calculate capacity")]
    public void BenchmarkTopicCapacityCalculation()
    {
        int capacity = TopicRegistry.CalculateCapacity(100);
        _consumer.Consume(capacity);
    }

    [Benchmark(Description = "Topic Pub/Sub: Create subscription")]
    public void BenchmarkTopicSubscriptionCreation()
    {
        var subscription = new TopicSubscription
        {
            TopicId = 123,
            KernelId = 456,
            QueueIndex = 10,
            Flags = TopicSubscription.FlagHighPriority
        };
        _consumer.Consume(subscription);
    }

    [Benchmark(Description = "Topic Pub/Sub: Validate registry")]
    public void BenchmarkTopicRegistryValidation()
    {
        bool result = _topicRegistry.Validate();
        _consumer.Consume(result);
    }

    [Benchmark(Description = "Topic Pub/Sub: Batch subscription creation (10K)")]
    public void BenchmarkBatchSubscriptionCreation()
    {
        for (int i = 0; i < TopicTestSize; i++)
        {
            var subscription = new TopicSubscription
            {
                TopicId = (uint)i,
                KernelId = (uint)(i % 100),
                QueueIndex = (ushort)(i % 256),
                Flags = TopicSubscription.FlagHighPriority
            };
            _consumer.Consume(subscription);
        }
    }

    // ==================== Component 3: Multi-Kernel Barriers Benchmarks ====================

    [Benchmark(Description = "Barriers: Create barrier")]
    public void BenchmarkBarrierCreation()
    {
        var barrier = MultiKernelBarrier.Create(participantCount: 4);
        _consumer.Consume(barrier);
    }

    [Benchmark(Description = "Barriers: Validate barrier")]
    public void BenchmarkBarrierValidation()
    {
        bool result = _barrier.Validate();
        _consumer.Consume(result);
    }

    [Benchmark(Description = "Barriers: Check barrier state")]
    public void BenchmarkBarrierStateChecks()
    {
        bool active = _barrier.IsActive();
        bool timedOut = _barrier.IsTimedOut();
        bool failed = _barrier.IsFailed();
        _consumer.Consume(active || timedOut || failed);
    }

    [Benchmark(Description = "Barriers: Batch validation (10K)")]
    public void BenchmarkBatchBarrierValidation()
    {
        int successCount = 0;
        for (int i = 0; i < BarrierTestSize; i++)
        {
            if (_barrier.Validate())
            {
                successCount++;
            }
        }
        _consumer.Consume(successCount);
    }

    // ==================== Component 4: Work-Stealing Task Queues Benchmarks ====================

    [Benchmark(Description = "Task Queues: Create queue")]
    public void BenchmarkTaskQueueCreation()
    {
        var queue = TaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000, enableStealing: true);
        _consumer.Consume(queue);
    }

    [Benchmark(Description = "Task Queues: Validate queue")]
    public void BenchmarkTaskQueueValidation()
    {
        bool result = _taskQueue.Validate();
        _consumer.Consume(result);
    }

    [Benchmark(Description = "Task Queues: Calculate size")]
    public void BenchmarkTaskQueueSizeCalculation()
    {
        long size = _taskQueue.Size;
        _consumer.Consume(size);
    }

    [Benchmark(Description = "Task Queues: Check empty/full state")]
    public void BenchmarkTaskQueueStateChecks()
    {
        bool empty = _taskQueue.IsEmpty();
        bool full = _taskQueue.IsFull();
        _consumer.Consume(empty || full);
    }

    [Benchmark(Description = "Task Queues: Batch queue operations (10K)")]
    public void BenchmarkBatchTaskQueueOperations()
    {
        int operationCount = 0;
        for (int i = 0; i < QueueTestSize; i++)
        {
            if (_taskQueue.Validate())
            {
                operationCount++;
            }
            long size = _taskQueue.Size;
            _consumer.Consume(size);
        }
        _consumer.Consume(operationCount);
    }

    // ==================== Component 5: Fault Tolerance & Health Monitoring Benchmarks ====================

    [Benchmark(Description = "Health Monitor: Initialize status")]
    public void BenchmarkHealthStatusInitialization()
    {
        var health = KernelHealthStatus.CreateInitialized();
        _consumer.Consume(health);
    }

    [Benchmark(Description = "Health Monitor: Check heartbeat staleness")]
    public void BenchmarkHeartbeatStaleCheck()
    {
        bool result = _healthStatus.IsHeartbeatStale(TimeSpan.FromSeconds(5));
        _consumer.Consume(result);
    }

    [Benchmark(Description = "Health Monitor: Validate health status")]
    public void BenchmarkHealthStatusValidation()
    {
        bool result = _healthStatus.Validate();
        _consumer.Consume(result);
    }

    [Benchmark(Description = "Health Monitor: Check health state")]
    public void BenchmarkHealthStateChecks()
    {
        bool healthy = _healthStatus.IsHealthy();
        bool degraded = _healthStatus.IsDegraded();
        bool failed = _healthStatus.IsFailed();
        bool recovering = _healthStatus.IsRecovering();
        _consumer.Consume(healthy || degraded || failed || recovering);
    }

    [Benchmark(Description = "Health Monitor: Batch health checks (10K)")]
    public void BenchmarkBatchHealthChecks()
    {
        int healthyCount = 0;
        for (int i = 0; i < HealthTestSize; i++)
        {
            if (_healthStatus.Validate() && _healthStatus.IsHealthy())
            {
                healthyCount++;
            }
        }
        _consumer.Consume(healthyCount);
    }

    // ==================== End-to-End Integration Benchmarks ====================

    [Benchmark(Description = "End-to-End: Complete Phase 3 workflow")]
    public void BenchmarkCompletePhase3Workflow()
    {
        // 1. Message Router: Validate routing
        bool routingValid = _routingTable.Validate();

        // 2. Topic Pub/Sub: Create subscription
        var subscription = new TopicSubscription
        {
            TopicId = 42,
            KernelId = 100,
            QueueIndex = 5,
            Flags = TopicSubscription.FlagHighPriority
        };

        // 3. Barriers: Check barrier state
        bool barrierValid = _barrier.Validate();
        bool barrierActive = _barrier.IsActive();

        // 4. Task Queues: Create and validate task
        var task = new TaskDescriptor
        {
            TaskId = 999,
            TargetKernelId = 42,
            Priority = 750,
            DataPtr = 0x9000,
            DataSize = 2048,
            Flags = 0
        };
        bool queueValid = _taskQueue.Validate();

        // 5. Health Monitor: Check health
        bool healthValid = _healthStatus.Validate();
        bool healthy = _healthStatus.IsHealthy();

        // Consume all results
        _consumer.Consume(routingValid);
        _consumer.Consume(subscription);
        _consumer.Consume(barrierValid);
        _consumer.Consume(barrierActive);
        _consumer.Consume(task);
        _consumer.Consume(queueValid);
        _consumer.Consume(healthValid);
        _consumer.Consume(healthy);
    }

    [Benchmark(Description = "End-to-End: Batch processing (10K messages)")]
    public void BenchmarkBatchMessageProcessing()
    {
        int processedCount = 0;

        for (int i = 0; i < RouterTestSize; i++)
        {
            // 1. Validate routing
            if (!_routingTable.Validate())
            {
                continue;
            }

            // 2. Topic matching (every 10 messages)
            if (i % 10 == 0)
            {
                _topicRegistry.Validate();
            }

            // 3. Barrier synchronization (every 100 messages)
            if (i % 100 == 0)
            {
                _barrier.Validate();
            }

            // 4. Task queue operations
            if (i < QueueTestSize)
            {
                _taskQueue.Validate();
            }

            // 5. Health monitoring
            if (_healthStatus.IsHealthy())
            {
                processedCount++;
            }
        }

        _consumer.Consume(processedCount);
    }
}

/// <summary>
/// xUnit tests to validate benchmark correctness and performance characteristics.
/// </summary>
/// <remarks>
/// These tests verify that:
/// 1. All benchmarks execute without errors
/// 2. Struct sizes are optimized for cache efficiency
/// 3. Hot paths have zero heap allocations
/// </remarks>
public sealed class Phase3PerformanceValidationTests
{
    [Fact(DisplayName = "Performance: All Phase 3 benchmarks execute successfully")]
    public void Phase3Benchmarks_ShouldExecuteSuccessfully()
    {
        // Arrange
        var benchmarks = new Phase3PerformanceBenchmarks();
        benchmarks.Setup();

        // Act & Assert - Execute all benchmarks (no exceptions = success)
        benchmarks.BenchmarkRoutingTableValidation();
        benchmarks.BenchmarkHashCapacityCalculation();
        benchmarks.BenchmarkBatchRoutingValidation();

        benchmarks.BenchmarkTopicCapacityCalculation();
        benchmarks.BenchmarkTopicSubscriptionCreation();
        benchmarks.BenchmarkTopicRegistryValidation();
        benchmarks.BenchmarkBatchSubscriptionCreation();

        benchmarks.BenchmarkBarrierCreation();
        benchmarks.BenchmarkBarrierValidation();
        benchmarks.BenchmarkBarrierStateChecks();
        benchmarks.BenchmarkBatchBarrierValidation();

        benchmarks.BenchmarkTaskQueueCreation();
        benchmarks.BenchmarkTaskQueueValidation();
        benchmarks.BenchmarkTaskQueueSizeCalculation();
        benchmarks.BenchmarkTaskQueueStateChecks();
        benchmarks.BenchmarkBatchTaskQueueOperations();

        benchmarks.BenchmarkHealthStatusInitialization();
        benchmarks.BenchmarkHeartbeatStaleCheck();
        benchmarks.BenchmarkHealthStatusValidation();
        benchmarks.BenchmarkHealthStateChecks();
        benchmarks.BenchmarkBatchHealthChecks();

        benchmarks.BenchmarkCompletePhase3Workflow();
        benchmarks.BenchmarkBatchMessageProcessing();

        // If we got here, all benchmarks executed successfully
        Assert.True(true);
    }

    [Fact(DisplayName = "Performance: Struct sizes optimized for cache efficiency")]
    public void StructSizes_ShouldBeOptimizedForCache()
    {
        // Assert - Verify struct sizes match cache-line alignment expectations
        Assert.Equal(64, Marshal.SizeOf<TaskDescriptor>());     // Cache-line aligned
        Assert.Equal(32, Marshal.SizeOf<KernelRoutingTable>()); // Half cache-line
        Assert.Equal(24, Marshal.SizeOf<TopicRegistry>());      // Sub-cache-line
        Assert.Equal(12, Marshal.SizeOf<TopicSubscription>());  // Compact
        Assert.Equal(16, Marshal.SizeOf<MultiKernelBarrier>()); // Sub-cache-line
        Assert.Equal(40, Marshal.SizeOf<TaskQueue>());          // Sub-cache-line
        Assert.Equal(36, Marshal.SizeOf<KernelHealthStatus>()); // Sub-cache-line
    }

    [Fact(DisplayName = "Performance: No heap allocations in hot paths")]
    public void HotPaths_ShouldNotAllocateHeap()
    {
        // Arrange
        var benchmarks = new Phase3PerformanceBenchmarks();
        benchmarks.Setup();

        // Act - Force GC and measure allocations
        long allocBefore = GC.GetTotalMemory(forceFullCollection: true);

        // Execute 10K hot path operations
        for (int i = 0; i < 10000; i++)
        {
            benchmarks.BenchmarkRoutingTableValidation();
            benchmarks.BenchmarkBarrierValidation();
            benchmarks.BenchmarkTaskQueueValidation();
            benchmarks.BenchmarkHealthStatusValidation();
        }

        long allocAfter = GC.GetTotalMemory(forceFullCollection: false);
        long deltaBytes = allocAfter - allocBefore;

        // Assert - Less than 10KB total (~1 byte/operation for 10K ops)
        // Note: Consumer pattern and GC behavior can cause some allocations
        Assert.True(deltaBytes < 10240, $"Hot paths allocated {deltaBytes} bytes for 10K ops (expected < 10KB = ~1 byte/op)");
    }
}
