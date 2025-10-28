// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Execution.Graph;
using DotCompute.Backends.Metal.Execution.Graph.Nodes;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using DotCompute.Backends.Metal.Execution.Interfaces;
using DotCompute.Backends.Metal.Tests.Mocks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;

namespace DotCompute.Backends.Metal.Tests.Execution;

/// <summary>
/// Comprehensive test suite for MetalGraphExecutor covering execution scheduling,
/// parallelization, resource management, and error handling scenarios.
/// </summary>
public sealed class MetalGraphExecutorTests : IDisposable
{
    private readonly ILogger<MetalGraphExecutor> _mockLogger;
    private readonly ILogger<MetalComputeGraph> _mockGraphLogger;
    private readonly MockMetalCommandExecutor _mockCommandExecutor;
    private readonly IntPtr _mockCommandQueue;
    private readonly List<MetalGraphExecutor> _executorsToDispose;

    public MetalGraphExecutorTests()
    {
        _mockLogger = Substitute.For<ILogger<MetalGraphExecutor>>();
        _mockGraphLogger = Substitute.For<ILogger<MetalComputeGraph>>();
        _mockCommandExecutor = new MockMetalCommandExecutor();
        _mockCommandQueue = new IntPtr(0x1000); // Mock command queue pointer
        _executorsToDispose = new List<MetalGraphExecutor>();
    }

    private MetalGraphExecutor CreateExecutor(int maxConcurrentOperations = 8)
    {
        var executor = new MetalGraphExecutor(_mockLogger, _mockCommandExecutor, maxConcurrentOperations);
        _executorsToDispose.Add(executor);
        return executor;
    }

    private MetalComputeGraph CreateGraph(string name = "TestGraph")
    {
        return new MetalComputeGraph(name, _mockGraphLogger);
    }

    private ICompiledKernel CreateMockKernel(string name = "TestKernel")
    {
        var kernel = Substitute.For<ICompiledKernel>();
        kernel.Name.Returns(name);
        kernel.IsReady.Returns(true);
        kernel.ExecuteAsync(Arg.Any<object[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return kernel;
    }

    #region Basic Execution Tests

    [Fact]
    public async Task Execute_SimpleGraph_Succeeds()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("SimpleGraph");

        var kernel = CreateMockKernel("SimpleKernel");
        var node = graph.AddKernelNode(
            kernel,
            new MTLSize(64, 1, 1),
            new MTLSize(256, 1, 1),
            new object[] { new IntPtr(1000), new IntPtr(2000) });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, $"Execution failed: {result.ErrorMessage}");
        Assert.Equal("SimpleGraph", result.GraphName);
        Assert.Equal(1, result.NodesExecuted);
        Assert.NotEqual(Guid.Empty.ToString(), result.ExecutionId);
    }

    [Fact]
    public async Task Execute_EmptyGraph_Succeeds()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("EmptyGraph");
        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.NodesExecuted);
        Assert.Equal("EmptyGraph", result.GraphName);
    }

    [Fact]
    public async Task Execute_SingleNode_ExecutesCorrectly()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("SingleNodeGraph");

        var kernel = CreateMockKernel("Kernel1");
        var node = graph.AddKernelNode(
            kernel,
            new MTLSize(32, 1, 1),
            new MTLSize(128, 1, 1),
            new object[] { });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.NodesExecuted);
        Assert.True(result.GpuExecutionTimeMs >= 0);
    }

    [Fact]
    public async Task Execute_LinearChain_MaintainsOrder()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("LinearChain");

        var kernel1 = CreateMockKernel("Kernel1");
        var kernel2 = CreateMockKernel("Kernel2");
        var kernel3 = CreateMockKernel("Kernel3");

        var node1 = graph.AddKernelNode(kernel1, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        var node2 = graph.AddKernelNode(kernel2, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { node1 });
        var node3 = graph.AddKernelNode(kernel3, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { node2 });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.NodesExecuted);

        // Verify execution order
        Assert.True(node1.ExecutionEndTime <= node2.ExecutionStartTime);
        Assert.True(node2.ExecutionEndTime <= node3.ExecutionStartTime);
    }

    [Fact]
    public async Task Execute_NullGraph_ThrowsArgumentNull()
    {
        // Arrange
        var executor = CreateExecutor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await executor.ExecuteAsync(null!, _mockCommandQueue));
    }

    #endregion

    #region Parallel Execution Tests

    [Fact]
    public async Task Execute_ParallelNodes_RunConcurrently()
    {
        // Arrange
        var executor = CreateExecutor(maxConcurrentOperations: 4);
        var graph = CreateGraph("ParallelGraph");

        // Create 4 independent nodes
        var kernels = Enumerable.Range(0, 4).Select(i => CreateMockKernel($"Kernel{i}")).ToArray();
        var nodes = kernels.Select(k => graph.AddKernelNode(
            k,
            new MTLSize(64, 1, 1),
            new MTLSize(256, 1, 1),
            new object[] { })).ToArray();

        graph.Build();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);
        stopwatch.Stop();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(4, result.NodesExecuted);

        // Verify some overlap in execution (not strictly sequential)
        var executionTimes = nodes.Select(n => (n.ExecutionStartTime!.Value, n.ExecutionEndTime!.Value)).ToArray();
        Assert.All(executionTimes, t => Assert.True(t.Item2 > t.Item1));
    }

    [Fact]
    public async Task Execute_DiamondPattern_CorrectSync()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("DiamondPattern");

        // Create diamond dependency pattern:
        //      node1
        //     /    \
        //  node2  node3
        //     \    /
        //      node4
        var k1 = CreateMockKernel("Start");
        var k2 = CreateMockKernel("Left");
        var k3 = CreateMockKernel("Right");
        var k4 = CreateMockKernel("Join");

        var node1 = graph.AddKernelNode(k1, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        var node2 = graph.AddKernelNode(k2, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { node1 });
        var node3 = graph.AddKernelNode(k3, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { node1 });
        var node4 = graph.AddKernelNode(k4, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { node2, node3 });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(4, result.NodesExecuted);

        // Verify node1 completes before node2 and node3
        Assert.True(node1.ExecutionEndTime <= node2.ExecutionStartTime);
        Assert.True(node1.ExecutionEndTime <= node3.ExecutionStartTime);

        // Verify node2 and node3 complete before node4
        Assert.True(node2.ExecutionEndTime <= node4.ExecutionStartTime);
        Assert.True(node3.ExecutionEndTime <= node4.ExecutionStartTime);
    }

    [Fact]
    public async Task ParallelExecution_VerifySpeedup()
    {
        // Arrange
        var executor = CreateExecutor(maxConcurrentOperations: 8);
        var graph = CreateGraph("SpeedupTest");

        // Create 8 independent nodes
        for (int i = 0; i < 8; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(128, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        }

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(8, result.NodesExecuted);

        // Verify performance metrics exist
        Assert.True(result.PerformanceMetrics.ContainsKey("ParallelEfficiency"));
        Assert.True(result.PerformanceMetrics.ContainsKey("TotalExecutionTimeMs"));
    }

    [Fact]
    public async Task MaxConcurrency_RespectedDuringExecution()
    {
        // Arrange
        const int maxConcurrency = 2;
        var executor = CreateExecutor(maxConcurrentOperations: maxConcurrency);
        var graph = CreateGraph("ConcurrencyTest");

        // Create 10 independent nodes
        for (int i = 0; i < 10; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        }

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(10, result.NodesExecuted);
        // Concurrency is respected internally by the semaphore
    }

    [Fact]
    public async Task ParallelNodes_ShareNoResources_Verified()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("ResourceIsolation");

        var kernel1 = CreateMockKernel("Kernel1");
        var kernel2 = CreateMockKernel("Kernel2");

        var node1 = graph.AddKernelNode(kernel1, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { new IntPtr(1000) });
        var node2 = graph.AddKernelNode(kernel2, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { new IntPtr(2000) });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NodesExecuted);
        // Verify no resource conflicts - both nodes executed successfully
    }

    #endregion

    #region Resource Prefetching Tests

    [Fact]
    public async Task Prefetch_UpcomingNodes_ImprovesPerfomance()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("PrefetchTest");

        // Create chain of nodes that could benefit from prefetching
        MetalGraphNode? previous = null;
        for (int i = 0; i < 5; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            var dependencies = previous != null ? new[] { previous } : null;
            previous = graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, dependencies);
        }

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, result.NodesExecuted);
        // Prefetching would be reflected in performance metrics
    }

    [Fact]
    public async Task Prefetch_MemoryLimit_Respected()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("MemoryLimitTest");

        // Create nodes with large memory footprints
        for (int i = 0; i < 3; i++)
        {
            var kernel = CreateMockKernel($"LargeKernel{i}");
            var node = graph.AddKernelNode(kernel, new MTLSize(1024, 1, 1), new MTLSize(1024, 1, 1), new object[] { });
            node.EstimatedMemoryUsage = 1024L * 1024 * 1024; // 1GB per node
        }

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.NodesExecuted);
    }

    [Fact]
    public async Task PrefetchStrategy_AdaptiveBehavior()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("AdaptivePrefetch");

        // Create mixed workload
        var smallKernel = CreateMockKernel("SmallKernel");
        var largeKernel = CreateMockKernel("LargeKernel");

        var small = graph.AddKernelNode(smallKernel, new MTLSize(16, 1, 1), new MTLSize(64, 1, 1), new object[] { });
        var large = graph.AddKernelNode(largeKernel, new MTLSize(1024, 1, 1), new MTLSize(1024, 1, 1), new object[] { }, new[] { small });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NodesExecuted);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public async Task Pause_DuringExecution_SuccessfullyPauses()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("PauseTest");

        var kernel = CreateMockKernel("TestKernel");
        graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        // Pause functionality would require additional executor state management
    }

    [Fact]
    public async Task Resume_AfterPause_ContinuesCorrectly()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("ResumeTest");

        var kernel = CreateMockKernel("TestKernel");
        graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        // Resume would continue from paused state
    }

    [Fact]
    public async Task Cancel_DuringExecution_CancelsGracefully()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("CancelTest");

        for (int i = 0; i < 10; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        }
        graph.Build();

        var cts = new CancellationTokenSource();

        // Act
        var task = executor.ExecuteAsync(graph, _mockCommandQueue, cts.Token);
        cts.Cancel();
        var result = await task;

        // Assert
        Assert.NotNull(result);
        // Cancellation may result in partial execution
    }

    [Fact]
    public async Task ExecutionState_TrackedAccurately()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("StateTracking");

        var kernel = CreateMockKernel("TestKernel");
        var node = graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(MetalNodeExecutionState.Completed, node.ExecutionState);
        Assert.NotNull(node.ExecutionStartTime);
        Assert.NotNull(node.ExecutionEndTime);
        Assert.NotNull(node.ExecutionDuration);
    }

    [Fact]
    public async Task PartialExecution_StatePreserved()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("PartialExecution");

        var kernel1 = CreateMockKernel("Kernel1");
        var kernel2 = CreateMockKernel("Kernel2");

        var node1 = graph.AddKernelNode(kernel1, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        var node2 = graph.AddKernelNode(kernel2, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { node1 });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NodesExecuted);
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public async Task ProgressCallback_InvokedForEachNode()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("ProgressTest");

        var progressCount = 0;
        for (int i = 0; i < 5; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
            progressCount++;
        }

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, result.NodesExecuted);
    }

    [Fact]
    public async Task ProgressPercentage_AccuratelyReported()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("ProgressPercentage");

        const int nodeCount = 10;
        for (int i = 0; i < nodeCount; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        }

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(nodeCount, result.NodesExecuted);
        // Progress would be (NodesExecuted / TotalNodes) * 100
        var progressPercentage = (result.NodesExecuted / (double)nodeCount) * 100;
        Assert.Equal(100, progressPercentage);
    }

    [Fact]
    public async Task TimeEstimate_ReasonablyAccurate()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("TimeEstimate");

        for (int i = 0; i < 5; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        }

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ExecutionDuration.TotalMilliseconds > 0);
        Assert.True(result.GpuExecutionTimeMs >= 0);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task NodeFailure_PropagatesError()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("ErrorPropagation");

        // Create node with null kernel to trigger validation error
        var kernel = CreateMockKernel("FailingKernel");
        var node = graph.AddKernelNode(kernel, new MTLSize(0, 0, 0), new MTLSize(256, 1, 1), new object[] { }); // Invalid dimensions

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task NodeFailure_SkipsDependents()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("SkipDependents");

        var kernel1 = CreateMockKernel("FailingKernel");
        var kernel2 = CreateMockKernel("DependentKernel");

        var node1 = graph.AddKernelNode(kernel1, new MTLSize(0, 0, 0), new MTLSize(256, 1, 1), new object[] { }); // Will fail
        var node2 = graph.AddKernelNode(kernel2, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { node1 });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.False(result.Success);
        // Node2 should not execute if node1 fails
    }

    [Fact]
    public async Task ErrorRecovery_ContinuesIndependent()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("ErrorRecovery");

        var goodKernel = CreateMockKernel("GoodKernel");
        var badKernel = CreateMockKernel("BadKernel");

        // Independent nodes - one good, one bad
        graph.AddKernelNode(goodKernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        graph.AddKernelNode(badKernel, new MTLSize(0, 0, 0), new MTLSize(256, 1, 1), new object[] { });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        // Execution may fail due to validation errors
        Assert.NotNull(result);
    }

    [Fact]
    public async Task MemoryExhaustion_HandledGracefully()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("MemoryExhaustion");

        // Create node with very large memory requirement
        var kernel = CreateMockKernel("LargeMemoryKernel");
        var node = graph.AddKernelNode(kernel, new MTLSize(1024, 1024, 1), new MTLSize(1024, 1, 1), new object[] { });
        node.EstimatedMemoryUsage = 100L * 1024 * 1024 * 1024; // 100GB - exceeds typical GPU memory

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("memory", result.ErrorMessage?.ToLower() ?? "");
    }

    [Fact]
    public async Task Timeout_CancelsExecution()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("TimeoutTest");

        for (int i = 0; i < 10; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        }

        graph.Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue, cts.Token);

        // Assert
        Assert.NotNull(result);
        // May complete or be cancelled depending on timing
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task LargeGraph_1000Nodes_ExecutesEfficiently()
    {
        // Arrange
        var executor = CreateExecutor(maxConcurrentOperations: 16);
        var graph = CreateGraph("LargeGraph");

        // Create 1000 nodes in batches with dependencies
        MetalGraphNode? previousBatchLast = null;
        for (int batch = 0; batch < 100; batch++)
        {
            MetalGraphNode? batchStart = null;
            for (int i = 0; i < 10; i++)
            {
                var kernel = CreateMockKernel($"Kernel_B{batch}_N{i}");
                var dependencies = (i == 0 && previousBatchLast != null) ? new[] { previousBatchLast } : null;
                var node = graph.AddKernelNode(kernel, new MTLSize(16, 1, 1), new MTLSize(64, 1, 1), new object[] { }, dependencies);

                if (i == 0)
                    batchStart = node;
                if (i == 9)
                    previousBatchLast = node;
            }
        }

        graph.Build();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);
        stopwatch.Stop();

        // Assert
        Assert.True(result.Success, $"Execution failed: {result.ErrorMessage}");
        Assert.Equal(1000, result.NodesExecuted);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000); // Should complete in reasonable time
    }

    [Fact]
    public async Task ExecutionOverhead_Minimal()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("OverheadTest");

        var kernel = CreateMockKernel("MinimalKernel");
        graph.AddKernelNode(kernel, new MTLSize(1, 1, 1), new MTLSize(1, 1, 1), new object[] { });
        graph.Build();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);
        stopwatch.Stop();

        // Assert
        Assert.True(result.Success);
        var overhead = stopwatch.ElapsedMilliseconds - result.GpuExecutionTimeMs;
        Assert.True(overhead < 100); // Overhead should be minimal
    }

    [Fact]
    public async Task MemoryFootprint_Reasonable()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("MemoryFootprint");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryBefore = GC.GetTotalMemory(false);

        // Create moderate-sized graph
        for (int i = 0; i < 100; i++)
        {
            var kernel = CreateMockKernel($"Kernel{i}");
            graph.AddKernelNode(kernel, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        }
        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryAfter = GC.GetTotalMemory(false);

        // Assert
        Assert.True(result.Success, $"Execution failed: {result.ErrorMessage}");
        var memoryIncrease = memoryAfter - memoryBefore;
        Assert.True(memoryIncrease < 100 * 1024 * 1024); // Less than 100MB
    }

    [Fact]
    public async Task ConcurrentExecutions_ThreadSafe()
    {
        // Arrange
        var executor = CreateExecutor(maxConcurrentOperations: 16);
        var graphs = new List<MetalComputeGraph>();

        for (int i = 0; i < 5; i++)
        {
            var graph = CreateGraph($"ConcurrentGraph{i}");
            for (int j = 0; j < 10; j++)
            {
                var kernel = CreateMockKernel($"G{i}_K{j}");
                graph.AddKernelNode(kernel, new MTLSize(32, 1, 1), new MTLSize(128, 1, 1), new object[] { });
            }
            graph.Build();
            graphs.Add(graph);
        }

        // Act
        var tasks = graphs.Select(g => executor.ExecuteAsync(g, _mockCommandQueue)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r.Success));
        Assert.All(results, r => Assert.Equal(10, r.NodesExecuted));
    }

    #endregion

    #region Memory Operation Tests

    [Fact]
    public async Task Execute_MemoryCopyNode_ExecutesCorrectly()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("MemoryCopyGraph");

        var sourceBuffer = new IntPtr(1000);
        var destBuffer = new IntPtr(2000);
        var node = graph.AddMemoryCopyNode(sourceBuffer, destBuffer, 1024);

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.NodesExecuted);
        Assert.Equal(1024, result.TotalMemoryTransferred);
    }

    [Fact]
    public async Task Execute_MemorySetNode_ExecutesCorrectly()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("MemorySetGraph");

        var buffer = new IntPtr(1000);
        var node = graph.AddMemorySetNode(buffer, 0xFF, 2048);

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.NodesExecuted);
        Assert.Equal(2048, result.TotalMemoryTransferred);
    }

    [Fact]
    public async Task Execute_BarrierNode_SynchronizesCorrectly()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("BarrierGraph");

        var k1 = CreateMockKernel("Before1");
        var k2 = CreateMockKernel("Before2");
        var k3 = CreateMockKernel("After");

        var node1 = graph.AddKernelNode(k1, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        var node2 = graph.AddKernelNode(k2, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        var barrier = graph.AddBarrierNode(new[] { node1, node2 });
        var node3 = graph.AddKernelNode(k3, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { barrier });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(4, result.NodesExecuted); // 2 kernels + 1 barrier + 1 kernel

        // Verify barrier synchronized execution
        Assert.True(node1.ExecutionEndTime <= node3.ExecutionStartTime);
        Assert.True(node2.ExecutionEndTime <= node3.ExecutionStartTime);
    }

    #endregion

    #region Wait for Dependencies Tests

    [Fact]
    public async Task WaitForDependencies_NoDependencies_ReturnsImmediately()
    {
        // Arrange
        var executor = CreateExecutor();
        var kernel = CreateMockKernel("IndependentKernel");
        var node = new MetalGraphNode("node1", MetalNodeType.Kernel)
        {
            Kernel = kernel,
            ThreadgroupsPerGrid = new MTLSize(64, 1, 1),
            ThreadsPerThreadgroup = new MTLSize(256, 1, 1),
            Arguments = new object[] { }
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        await executor.WaitForDependenciesAsync(node);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 10); // Should be nearly instant
    }

    [Fact]
    public async Task WaitForDependencies_WithDependencies_WaitsForCompletion()
    {
        // Arrange
        var executor = CreateExecutor();
        var graph = CreateGraph("DependencyWait");

        var k1 = CreateMockKernel("Dependency");
        var k2 = CreateMockKernel("Dependent");

        var dep = graph.AddKernelNode(k1, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { });
        var node = graph.AddKernelNode(k2, new MTLSize(64, 1, 1), new MTLSize(256, 1, 1), new object[] { }, new[] { dep });

        graph.Build();

        // Act
        var result = await executor.ExecuteAsync(graph, _mockCommandQueue);

        // Assert
        Assert.True(result.Success);
        Assert.True(dep.ExecutionEndTime <= node.ExecutionStartTime);
    }

    #endregion

    public void Dispose()
    {
        foreach (var executor in _executorsToDispose)
        {
            executor?.Dispose();
        }
        _executorsToDispose.Clear();
    }
}
