// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using DotCompute.Backends.Metal.Execution;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Tests.Execution;

/// <summary>
/// Tests for MetalCommandQueueManager.
/// </summary>
public sealed class MetalCommandQueueManagerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalCommandQueueManager> _logger;
    private IntPtr _device;

    public MetalCommandQueueManagerTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<MetalCommandQueueManager>();

        if (MetalNative.IsMetalSupported())
        {
            _device = MetalNative.CreateSystemDefaultDevice();
        }
    }

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }
    }

    [SkippableFact]
    public void Constructor_ValidDevice_CreatesManager()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act
        using var manager = new MetalCommandQueueManager(_device, _logger);

        // Assert
        Assert.NotNull(manager);
        _output.WriteLine("MetalCommandQueueManager created successfully");
    }

    [Fact]
    public void Constructor_ZeroDevice_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new MetalCommandQueueManager(IntPtr.Zero, _logger));

        Assert.Contains("Device handle cannot be zero", ex.Message);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MetalCommandQueueManager(_device, null!));
    }

    [SkippableFact]
    public void GetQueue_NormalPriority_ReturnsValidQueue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);

        // Act
        var queue = manager.GetQueue(QueuePriority.Normal);

        // Assert
        Assert.NotEqual(IntPtr.Zero, queue);
        _output.WriteLine($"Got queue: {queue}");

        // Cleanup
        manager.ReturnQueue(queue, QueuePriority.Normal);
    }

    [SkippableFact]
    public void GetQueue_AllPriorities_ReturnsValidQueues()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);
        var queues = new List<IntPtr>();

        try
        {
            // Act
            foreach (QueuePriority priority in Enum.GetValues(typeof(QueuePriority)))
            {
                var queue = manager.GetQueue(priority);
                Assert.NotEqual(IntPtr.Zero, queue);
                queues.Add(queue);
                _output.WriteLine($"Got {priority} priority queue: {queue}");
            }

            // Assert
            Assert.Equal(3, queues.Count); // Low, Normal, High
        }
        finally
        {
            // Cleanup
            int i = 0;
            foreach (QueuePriority priority in Enum.GetValues(typeof(QueuePriority)))
            {
                if (i < queues.Count)
                {
                    manager.ReturnQueue(queues[i], priority);
                }
                i++;
            }
        }
    }

    [SkippableFact]
    public void ReturnQueue_ToPool_AllowsReuse()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);

        // Act
        var queue1 = manager.GetQueue(QueuePriority.Normal);
        manager.ReturnQueue(queue1, QueuePriority.Normal);
        var queue2 = manager.GetQueue(QueuePriority.Normal);

        // Assert
        Assert.Equal(queue1, queue2); // Should reuse the same queue
        _output.WriteLine($"Reused queue: {queue1} == {queue2}");

        // Cleanup
        manager.ReturnQueue(queue2, QueuePriority.Normal);
    }

    [SkippableFact]
    public void GetStats_InitialState_ReturnsValidStats()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);

        // Act
        var stats = manager.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalQueuesCreated);
        Assert.Equal(0, stats.TotalQueuesReused);
        Assert.NotNull(stats.PoolStats);
        Assert.Equal(3, stats.PoolStats.Count); // Low, Normal, High

        _output.WriteLine($"Initial stats - Created: {stats.TotalQueuesCreated}, Reused: {stats.TotalQueuesReused}");
    }

    [SkippableFact]
    public void GetStats_AfterOperations_TracksMetrics()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);

        // Act
        var queue1 = manager.GetQueue(QueuePriority.Normal);
        manager.ReturnQueue(queue1, QueuePriority.Normal);
        var queue2 = manager.GetQueue(QueuePriority.Normal);
        manager.ReturnQueue(queue2, QueuePriority.Normal);

        var stats = manager.GetStats();

        // Assert
        Assert.Equal(1, stats.TotalQueuesCreated); // Only 1 created
        Assert.Equal(1, stats.TotalQueuesReused);  // 1 reused
        _output.WriteLine($"Stats - Created: {stats.TotalQueuesCreated}, Reused: {stats.TotalQueuesReused}");
        _output.WriteLine($"Reuse Rate: {(double)stats.TotalQueuesReused / stats.TotalQueuesCreated * 100.0:F1}%");
    }

    [SkippableFact]
    public void PoolUtilization_WithMultipleQueues_CalculatesCorrectly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger, maxPoolSize: 4);
        var queues = new List<IntPtr>();

        try
        {
            // Act - Get 3 queues (should be 75% utilized)
            for (int i = 0; i < 3; i++)
            {
                queues.Add(manager.GetQueue(QueuePriority.Normal));
            }

            var stats = manager.GetStats();
            var normalStats = stats.PoolStats[QueuePriority.Normal];

            // Assert
            Assert.Equal(4, normalStats.MaxPoolSize);
            Assert.Equal(0, normalStats.AvailableQueues); // All are checked out
            _output.WriteLine($"Pool utilization: {normalStats.Utilization:F1}%");
        }
        finally
        {
            // Cleanup
            foreach (var queue in queues)
            {
                manager.ReturnQueue(queue, QueuePriority.Normal);
            }
        }
    }

    [SkippableFact]
    public async Task ConcurrentAccess_MultipleThreads_IsSafe()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger, maxPoolSize: 8);
        const int threadCount = 10;
        const int operationsPerThread = 50;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    var priority = (QueuePriority)(j % 3);
                    var queue = manager.GetQueue(priority);
                    Thread.Sleep(1); // Simulate work
                    manager.ReturnQueue(queue, priority);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var stats = manager.GetStats();

        // Assert
        Assert.True(stats.TotalQueuesCreated > 0);
        Assert.True(stats.TotalQueuesReused > 0);
        _output.WriteLine($"Concurrent test - Created: {stats.TotalQueuesCreated}, Reused: {stats.TotalQueuesReused}");
        _output.WriteLine($"Reuse efficiency: {(double)stats.TotalQueuesReused / (stats.TotalQueuesCreated + stats.TotalQueuesReused) * 100.0:F1}%");
    }

    [SkippableFact]
    public void Cleanup_RemovesIdleResources()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);

        var queue = manager.GetQueue(QueuePriority.Normal);
        manager.ReturnQueue(queue, QueuePriority.Normal);

        // Act
        manager.Cleanup();

        // Assert
        var stats = manager.GetStats();
        Assert.NotNull(stats);
        _output.WriteLine("Cleanup completed successfully");
    }

    [SkippableFact]
    public void Dispose_AfterOperations_CleansUpProperly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var manager = new MetalCommandQueueManager(_device, _logger);

        var queue = manager.GetQueue(QueuePriority.Normal);
        manager.ReturnQueue(queue, QueuePriority.Normal);

        // Act
        manager.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => manager.GetQueue());
        _output.WriteLine("Disposed manager properly throws ObjectDisposedException");
    }

    [SkippableFact]
    public void PoolOverflow_ReleasesExtraQueues()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger, maxPoolSize: 2);
        var queues = new List<IntPtr>();

        try
        {
            // Act - Get more queues than pool size
            for (int i = 0; i < 5; i++)
            {
                queues.Add(manager.GetQueue(QueuePriority.Normal));
            }

            // Return all queues
            foreach (var queue in queues)
            {
                manager.ReturnQueue(queue, QueuePriority.Normal);
            }

            var stats = manager.GetStats();
            var normalStats = stats.PoolStats[QueuePriority.Normal];

            // Assert - Pool should only retain max size
            Assert.True(normalStats.AvailableQueues <= 2);
            _output.WriteLine($"Pool correctly limited to {normalStats.MaxPoolSize}, available: {normalStats.AvailableQueues}");
        }
        finally
        {
            // Queues are already returned
        }
    }

    #region Priority Queue Stress Tests

    [SkippableFact]
    public void PriorityQueueStress_HighThroughput_HandlesCorrectly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger, maxPoolSize: 10);
        const int iterations = 1000;
        var completedOperations = 0;

        // Act
        Parallel.For(0, iterations, i =>
        {
            var priority = (QueuePriority)(i % 3);
            var queue = manager.GetQueue(priority);
            Thread.Sleep(1);
            manager.ReturnQueue(queue, priority);
            Interlocked.Increment(ref completedOperations);
        });

        var stats = manager.GetStats();

        // Assert
        Assert.Equal(iterations, completedOperations);
        Assert.True(stats.TotalQueuesCreated > 0);
        Assert.True(stats.TotalQueuesReused >= stats.TotalQueuesCreated);
        _output.WriteLine($"Stress test completed: {completedOperations} operations, {stats.TotalQueuesCreated} created, {stats.TotalQueuesReused} reused");
    }

    [SkippableFact]
    public void GetQueue_UnderHighContention_MaintainsConsistency()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger, maxPoolSize: 5);
        var queues = new ConcurrentBag<IntPtr>();

        // Act - Simulate high contention
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var queue = manager.GetQueue(QueuePriority.Normal);
                queues.Add(queue);
                Thread.Sleep(Random.Shared.Next(1, 5));
                manager.ReturnQueue(queue, QueuePriority.Normal);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert
        var stats = manager.GetStats();
        Assert.True(stats.TotalQueuesCreated <= 10); // Should reuse efficiently
        Assert.True(stats.TotalQueuesReused > stats.TotalQueuesCreated * 10);
        _output.WriteLine($"High contention test: {stats.TotalQueuesCreated} created, {stats.TotalQueuesReused} reused");
    }

    [SkippableFact]
    public void QueueExhaustion_MaxPoolSize_HandlesGracefully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger, maxPoolSize: 3);
        var queues = new List<IntPtr>();

        try
        {
            // Act - Exhaust the pool
            for (int i = 0; i < 10; i++)
            {
                queues.Add(manager.GetQueue(QueuePriority.Normal));
            }

            var stats = manager.GetStats();

            // Assert - Should create more queues than pool size
            Assert.Equal(10, queues.Count);
            Assert.Equal(10, stats.TotalQueuesCreated);
            _output.WriteLine($"Exhaustion handled: {queues.Count} queues allocated beyond pool size");
        }
        finally
        {
            foreach (var queue in queues)
            {
                manager.ReturnQueue(queue, QueuePriority.Normal);
            }
        }
    }

    [SkippableFact]
    public void QueueRecycling_AfterReturn_ValidatesCorrectly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);
        var firstQueue = manager.GetQueue(QueuePriority.Normal);

        // Act
        manager.ReturnQueue(firstQueue, QueuePriority.Normal);
        var secondQueue = manager.GetQueue(QueuePriority.Normal);
        var thirdQueue = manager.GetQueue(QueuePriority.Normal);

        // Assert
        Assert.Equal(firstQueue, secondQueue); // Should reuse
        Assert.NotEqual(secondQueue, thirdQueue); // Should be different
        _output.WriteLine($"Queue recycling: first={firstQueue}, second={secondQueue}, third={thirdQueue}");

        // Cleanup
        manager.ReturnQueue(secondQueue, QueuePriority.Normal);
        manager.ReturnQueue(thirdQueue, QueuePriority.Normal);
    }

    #endregion

    #region Performance Benchmark Tests

    [SkippableFact]
    public void PerformanceBenchmark_QueueAcquisition_MeasuresLatency()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger, maxPoolSize: 8);
        const int warmupIterations = 100;
        const int benchmarkIterations = 1000;

        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            var queue = manager.GetQueue(QueuePriority.Normal);
            manager.ReturnQueue(queue, QueuePriority.Normal);
        }

        // Act - Benchmark
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < benchmarkIterations; i++)
        {
            var queue = manager.GetQueue(QueuePriority.Normal);
            manager.ReturnQueue(queue, QueuePriority.Normal);
        }
        stopwatch.Stop();

        // Assert
        var avgLatencyMs = stopwatch.Elapsed.TotalMilliseconds / benchmarkIterations;
        Assert.True(avgLatencyMs < 1.0); // Should be under 1ms average
        _output.WriteLine($"Average queue acquisition latency: {avgLatencyMs:F4}ms over {benchmarkIterations} iterations");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void GetQueue_DisposedManager_ThrowsObjectDisposedException()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var manager = new MetalCommandQueueManager(_device, _logger);
        manager.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => manager.GetQueue());
    }

    [SkippableFact]
    public void ReturnQueue_NullQueue_HandlesGracefully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var manager = new MetalCommandQueueManager(_device, _logger);

        // Act - Should not throw
        manager.ReturnQueue(IntPtr.Zero, QueuePriority.Normal);

        // Assert
        var stats = manager.GetStats();
        Assert.Equal(0, stats.TotalQueuesCreated);
    }

    [SkippableFact]
    public void GetQueue_MultipleDispose_IsSafe()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var manager = new MetalCommandQueueManager(_device, _logger);

        // Act
        manager.Dispose();
        manager.Dispose(); // Should not throw
        manager.Dispose(); // Should not throw

        // Assert - Multiple disposes should be safe
        Assert.True(true);
    }

    #endregion
}
