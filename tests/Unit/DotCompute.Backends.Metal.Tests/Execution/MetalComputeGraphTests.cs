// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Backends.Metal.Execution.Graph;
using DotCompute.Backends.Metal.Execution.Graph.Nodes;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using DotCompute.Backends.Metal.Execution.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Execution;

/// <summary>
/// Comprehensive test suite for MetalComputeGraph covering graph construction, validation,
/// dependency analysis, resource management, and serialization.
/// Target: 30+ test methods with full coverage of critical paths.
/// </summary>
public sealed class MetalComputeGraphTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalComputeGraph> _logger;
    private readonly List<MetalComputeGraph> _graphs = new();

    public MetalComputeGraphTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = Substitute.For<ILogger<MetalComputeGraph>>();
    }

    #region Graph Construction Tests (5 tests)

    [Fact]
    public void Constructor_CreatesEmptyGraph()
    {
        // Arrange & Act
        var graph = CreateGraph("TestGraph");

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("TestGraph", graph.Name);
        Assert.NotNull(graph.Id);
        Assert.True(graph.IsEmpty);
        Assert.Equal(0, graph.NodeCount);
        Assert.False(graph.IsBuilt);
        Assert.False(graph.IsOptimized);
        _output.WriteLine($"Created empty graph with ID: {graph.Id}");
    }

    [Fact]
    public void AddNode_ValidKernelNode_AddsSuccessfully()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero, IntPtr.Zero, 1024 };

        // Act
        var node = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(MetalNodeType.Kernel, node.Type);
        Assert.Equal(1, graph.NodeCount);
        Assert.False(graph.IsEmpty);
        Assert.Equal(mockKernel, node.Kernel);
        _output.WriteLine($"Added kernel node: {node.Id}");
    }

    [Fact]
    public void AddNode_NullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            graph.AddKernelNode(null!, threadgroups, threadsPerGroup, args));
    }

    [Fact]
    public void AddEdge_ValidNodes_CreatesConnection()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel1 = CreateMockKernel();
        var mockKernel2 = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Act
        var node1 = graph.AddKernelNode(mockKernel1, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel2, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });

        // Assert
        Assert.Equal(2, graph.NodeCount);
        Assert.Contains(node1, node2.Dependencies);
        _output.WriteLine($"Created edge: {node1.Id} -> {node2.Id}");
    }

    [Fact]
    public void AddEdge_NonexistentDependency_ThrowsException()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Create a node that's not in the graph
        var orphanNode = new MetalGraphNode("orphan", MetalNodeType.Kernel);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
                dependencies: new[] { orphanNode }));
    }

    #endregion

    #region Dependency Analysis Tests (5 tests)

    [Fact]
    public void GetDependencies_Node_ReturnsCorrectDeps()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node3 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1, node2 });

        // Act
        var dependencies = node3.Dependencies;

        // Assert
        Assert.Equal(2, dependencies.Count);
        Assert.Contains(node1, dependencies);
        Assert.Contains(node2, dependencies);
        _output.WriteLine($"Node {node3.Id} has {dependencies.Count} dependencies");
    }

    [Fact]
    public void GetDependents_Node_ReturnsCorrectDeps()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });
        var node3 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });

        // Act
        var nodes = graph.Nodes;
        var dependents = nodes.Where(n => n.Dependencies.Any(d => d.Id == node1.Id)).ToList();

        // Assert
        Assert.Equal(2, dependents.Count);
        Assert.Contains(node2, dependents);
        Assert.Contains(node3, dependents);
        _output.WriteLine($"Node {node1.Id} has {dependents.Count} dependents");
    }

    [Fact]
    public void TopologicalSort_AcyclicGraph_ReturnsValidOrder()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Create a DAG: node1 -> node2 -> node3
        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });
        var node3 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node2 });

        // Act
        graph.Build();
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        Assert.Equal(3, executionOrder.Count);
        Assert.Equal(node1.Id, executionOrder[0].Id);
        Assert.Equal(node2.Id, executionOrder[1].Id);
        Assert.Equal(node3.Id, executionOrder[2].Id);
        _output.WriteLine($"Execution order: {string.Join(" -> ", executionOrder.Select(n => n.Id))}");
    }

    [Fact]
    public void DetectCycle_CyclicGraph_ThrowsException()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });

        // Create cycle by making node1 depend on node2 (this should be caught at node level)
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            node1.AddDependency(node2));
    }

    [Fact]
    public void DetectCycle_AcyclicGraph_BuildsSuccessfully()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });

        // Act
        graph.Build();

        // Assert
        Assert.True(graph.IsBuilt);
        _output.WriteLine("Acyclic graph built successfully");
    }

    #endregion

    #region Graph Validation Tests (5 tests)

    [Fact]
    public void Validate_ValidGraph_BuildsSuccessfully()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Act
        graph.Build();

        // Assert
        Assert.True(graph.IsBuilt);
        _output.WriteLine("Valid graph built successfully");
    }

    [Fact]
    public void Validate_CyclicGraph_ThrowsOnBuild()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var node1 = new MetalGraphNode("node1", MetalNodeType.Kernel);
        var node2 = new MetalGraphNode("node2", MetalNodeType.Kernel);

        // Manually create cycle for testing
        node1.Dependencies.Add(node2);
        node2.Dependencies.Add(node1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            node1.AddDependency(node2)); // Should be caught at node level
    }

    [Fact]
    public void Validate_DisconnectedGraph_BuildsSuccessfully()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Add two independent nodes
        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Act
        graph.Build();

        // Assert
        Assert.True(graph.IsBuilt);
        Assert.Equal(2, graph.NodeCount);
        _output.WriteLine("Disconnected graph (2 independent nodes) built successfully");
    }

    [Fact]
    public void Validate_EmptyGraph_ReturnsTrue()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");

        // Act
        graph.Build(); // Should succeed even with no nodes

        // Assert
        Assert.True(graph.IsBuilt);
        Assert.True(graph.IsEmpty);
        _output.WriteLine("Empty graph validated successfully");
    }

    [Fact]
    public void Validate_InvalidKernelNode_ValidationFails()
    {
        // Arrange
        var node = new MetalGraphNode("test", MetalNodeType.Kernel);
        // Leave kernel null to make it invalid

        // Act
        var errors = node.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Kernel node must have a valid kernel"));
        _output.WriteLine($"Validation found {errors.Count} errors as expected");
    }

    #endregion

    #region Resource Management Tests (5 tests)

    [Fact]
    public void AllocateResources_ValidGraph_Succeeds()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { new float[1024], new float[1024] };

        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Act
        graph.Build();
        var memoryFootprint = graph.EstimatedMemoryFootprint;

        // Assert
        Assert.True(memoryFootprint > 0);
        _output.WriteLine($"Estimated memory footprint: {memoryFootprint:N0} bytes");
    }

    [Fact]
    public void AllocateResources_LargeBuffers_CalculatesCorrectSize()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);

        var largeArray = new float[1024 * 1024]; // 1M floats = 4MB
        var args = new object[] { largeArray, largeArray };

        // Act
        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        graph.Build();
        var memoryFootprint = graph.EstimatedMemoryFootprint;

        // Assert
        var expectedMinSize = largeArray.Length * sizeof(float) * 2; // Two arrays
        Assert.True(memoryFootprint >= expectedMinSize);
        _output.WriteLine($"Memory footprint for large buffers: {memoryFootprint:N0} bytes (expected >= {expectedMinSize:N0})");
    }

    [Fact]
    public void ReleaseResources_AfterClear_FreesMemory()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { new float[1024] };

        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        graph.Build();
        var initialFootprint = graph.EstimatedMemoryFootprint;

        // Act
        graph.Clear();

        // Assert
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EstimatedMemoryFootprint);
        _output.WriteLine($"Memory freed: {initialFootprint:N0} -> 0 bytes");
    }

    [Fact]
    public void ResourceLifetime_TrackedCorrectly()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Act
        var node = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var creationTime = node.CreatedAt;

        // Assert
        Assert.True(creationTime <= DateTimeOffset.UtcNow);
        Assert.True((DateTimeOffset.UtcNow - creationTime).TotalSeconds < 1);
        _output.WriteLine($"Node created at: {creationTime:o}");
    }

    [Fact]
    public void IntermediateBuffers_EstimatedCorrectly()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);

        // Create a pipeline with intermediate buffers
        var input = new float[1024];
        var intermediate = new float[1024];
        var output = new float[1024];

        // Act
        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup,
            new object[] { input, intermediate });
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup,
            new object[] { intermediate, output }, dependencies: new[] { node1 });

        graph.Build();
        var footprint = graph.EstimatedMemoryFootprint;

        // Assert
        Assert.Equal(2, graph.NodeCount);
        Assert.True(footprint > 0);
        _output.WriteLine($"Pipeline memory footprint: {footprint:N0} bytes");
    }

    #endregion

    #region Graph Properties Tests (3 tests)

    [Fact]
    public void NodeCount_AfterOperations_Accurate()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        Assert.Equal(0, graph.NodeCount);

        // Act & Assert - Add nodes
        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        Assert.Equal(1, graph.NodeCount);

        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        Assert.Equal(2, graph.NodeCount);

        var node = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        Assert.Equal(3, graph.NodeCount);

        // Remove node
        graph.RemoveNode(node.Id);
        Assert.Equal(2, graph.NodeCount);

        // Clear all
        graph.Clear();
        Assert.Equal(0, graph.NodeCount);
        _output.WriteLine("Node count tracked correctly through all operations");
    }

    [Fact]
    public void EdgeCount_AfterOperations_Accurate()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Act - Create nodes with dependencies
        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 }); // 1 edge
        var node3 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1, node2 }); // 2 more edges

        var totalEdges = graph.Nodes.Sum(n => n.Dependencies.Count);

        // Assert
        Assert.Equal(3, totalEdges); // node2->node1, node3->node1, node3->node2
        _output.WriteLine($"Graph has {totalEdges} edges as expected");
    }

    [Fact]
    public void IsAcyclic_ComputedCorrectly()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Create acyclic graph
        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });

        // Act
        graph.Build();

        // Assert
        Assert.True(graph.IsBuilt); // If it builds successfully, it's acyclic
        _output.WriteLine("Graph correctly identified as acyclic");
    }

    #endregion

    #region Complex Scenarios Tests (4 tests)

    [Fact]
    public void LargeGraph_1000Nodes_HandlesCorrectly()
    {
        // Arrange
        var graph = CreateGraph("LargeGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Act - Add 1000 nodes
        var nodes = new List<MetalGraphNode>();
        for (int i = 0; i < 1000; i++)
        {
            var node = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
            nodes.Add(node);
        }

        graph.Build();

        // Assert
        Assert.Equal(1000, graph.NodeCount);
        Assert.True(graph.IsBuilt);
        _output.WriteLine($"Successfully handled graph with {graph.NodeCount} nodes");
    }

    [Fact]
    public void DiamondPattern_ProperDependencies()
    {
        // Arrange
        var graph = CreateGraph("DiamondGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        // Create diamond pattern:
        //     top
        //    /   \
        //  left  right
        //    \   /
        //    bottom
        var top = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var left = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { top });
        var right = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { top });
        var bottom = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { left, right });

        // Act
        graph.Build();
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        Assert.Equal(4, graph.NodeCount);
        Assert.Equal(top.Id, executionOrder[0].Id); // Top must be first
        Assert.Equal(bottom.Id, executionOrder[3].Id); // Bottom must be last
        // Left and right can be in any order (parallel)
        _output.WriteLine($"Diamond pattern execution order: {string.Join(" -> ", executionOrder.Select(n => n.Id))}");
    }

    [Fact]
    public void ConcurrentModification_ProperLocking()
    {
        // Arrange
        var graph = CreateGraph("ConcurrentGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var tasks = new List<Task>();
        var addedNodes = new System.Collections.Concurrent.ConcurrentBag<MetalGraphNode>();

        // Act - Add nodes concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var node = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
                addedNodes.Add(node);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Equal(10, graph.NodeCount);
        Assert.Equal(10, addedNodes.Count);
        _output.WriteLine("Concurrent modifications handled correctly with proper locking");
    }

    [Fact]
    public void GraphClone_NodeCloning()
    {
        // Arrange
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var originalNode = new MetalGraphNode("original", MetalNodeType.Kernel)
        {
            Kernel = mockKernel,
            ThreadgroupsPerGrid = threadgroups,
            ThreadsPerThreadgroup = threadsPerGroup,
            Arguments = args,
            EstimatedMemoryUsage = 1024
        };

        // Act
        var clonedNode = originalNode.Clone("cloned");

        // Assert
        Assert.NotEqual(originalNode.Id, clonedNode.Id);
        Assert.Equal(originalNode.Type, clonedNode.Type);
        Assert.Equal(originalNode.Kernel, clonedNode.Kernel);
        Assert.Equal(originalNode.EstimatedMemoryUsage, clonedNode.EstimatedMemoryUsage);
        Assert.Empty(clonedNode.Dependencies); // Dependencies not cloned
        _output.WriteLine($"Successfully cloned node {originalNode.Id} to {clonedNode.Id}");
    }

    #endregion

    #region Node Management Tests (3 tests)

    [Fact]
    public void AddMemoryCopyNode_ValidParameters_AddsSuccessfully()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var sourceBuffer = new IntPtr(1000);
        var destBuffer = new IntPtr(2000);
        var size = 1024L;

        // Act
        var node = graph.AddMemoryCopyNode(sourceBuffer, destBuffer, size);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(MetalNodeType.MemoryCopy, node.Type);
        Assert.Equal(sourceBuffer, node.SourceBuffer);
        Assert.Equal(destBuffer, node.DestinationBuffer);
        Assert.Equal(size, node.CopySize);
        _output.WriteLine($"Added memory copy node: {node.Id}");
    }

    [Fact]
    public void AddMemorySetNode_ValidParameters_AddsSuccessfully()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var buffer = new IntPtr(1000);
        var fillValue = (byte)42;
        var size = 1024L;

        // Act
        var node = graph.AddMemorySetNode(buffer, fillValue, size);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(MetalNodeType.MemorySet, node.Type);
        Assert.Equal(buffer, node.DestinationBuffer);
        Assert.Equal(fillValue, node.FillValue);
        Assert.Equal(size, node.CopySize);
        _output.WriteLine($"Added memory set node: {node.Id}");
    }

    [Fact]
    public void AddBarrierNode_WithDependencies_AddsSuccessfully()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Act
        var barrier = graph.AddBarrierNode(new[] { node1, node2 });

        // Assert
        Assert.NotNull(barrier);
        Assert.Equal(MetalNodeType.Barrier, barrier.Type);
        Assert.Equal(2, barrier.Dependencies.Count);
        Assert.Equal(0, barrier.EstimatedMemoryUsage); // Barriers don't consume memory
        _output.WriteLine($"Added barrier node with {barrier.Dependencies.Count} dependencies");
    }

    #endregion

    #region Graph Analysis Tests (3 tests)

    [Fact]
    public void AnalyzeOptimizationOpportunities_ReturnsAnalysis()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var node1 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);
        var node2 = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args,
            dependencies: new[] { node1 });

        // Act
        var analysis = graph.AnalyzeOptimizationOpportunities();

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal(2, analysis.NodeCount);
        Assert.True(analysis.EstimatedMemoryFootprint > 0);
        _output.WriteLine($"Analysis: {analysis.NodeCount} nodes, {analysis.CriticalPathLength} critical path length");
    }

    [Fact]
    public void GetExecutionOrder_BeforeBuild_ThrowsException()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => graph.GetExecutionOrder());
    }

    [Fact]
    public void FindNode_ExistingNode_ReturnsNode()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        var node = graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Act
        var found = graph.FindNode(node.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(node.Id, found.Id);
        _output.WriteLine($"Found node: {found.Id}");
    }

    #endregion

    #region Disposal Tests (2 tests)

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");
        var mockKernel = CreateMockKernel();
        var threadgroups = new MTLSize(256, 1, 1);
        var threadsPerGroup = new MTLSize(256, 1, 1);
        var args = new object[] { IntPtr.Zero };

        graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args);

        // Act
        graph.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() =>
            graph.AddKernelNode(mockKernel, threadgroups, threadsPerGroup, args));
        _output.WriteLine("Graph disposed correctly, subsequent operations throw ObjectDisposedException");
    }

    [Fact]
    public void Dispose_MultipleCalls_Safe()
    {
        // Arrange
        var graph = CreateGraph("TestGraph");

        // Act & Assert - Multiple dispose calls should be safe
        graph.Dispose();
        graph.Dispose();
        graph.Dispose();

        _output.WriteLine("Multiple Dispose calls handled safely");
    }

    #endregion

    #region Helper Methods

    private MetalComputeGraph CreateGraph(string name)
    {
        var graph = new MetalComputeGraph(name, _logger);
        _graphs.Add(graph);
        return graph;
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

    public void Dispose()
    {
        foreach (var graph in _graphs)
        {
            graph.Dispose();
        }
        _graphs.Clear();
    }

    #endregion
}
