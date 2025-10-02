// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.Helpers;

/// <summary>
/// Test wrapper for CUDA graph operations to simplify test code.
/// </summary>
public static class CudaGraphTestWrapper
{
    /// <summary>
    /// Adds a memory copy operation to the graph.
    /// </summary>
    public static object AddMemoryCopy(IUnifiedMemoryBuffer<float> source, IUnifiedMemoryBuffer<float> destination, int sizeInBytes)
    {
        // This is a test helper - actual implementation would use CUDA graph API
        return new { Type = "MemoryCopy", Source = source, Destination = destination, Size = sizeInBytes };
    }

    /// <summary>
    /// Adds a kernel execution node to the graph.
    /// </summary>
    public static object AddKernel(ICompiledKernel kernel, LaunchConfiguration config, params object[] args)
    {
        // This is a test helper - actual implementation would use CUDA graph API
        return new { Type = "Kernel", Kernel = kernel, Config = config, Arguments = args };
    }
}

/// <summary>
/// Test wrapper for CUDA graph executable operations.
/// </summary>
public static class CudaGraphExecutable
{
    /// <summary>
    /// Launches a graph asynchronously.
    /// </summary>
    public static Task LaunchAsync(object executableGraph)
    {
        // This is a test helper - actual implementation would execute the graph
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for test graph operations.
/// </summary>
public static class CudaGraphTestExtensions
{
    /// <summary>
    /// Creates a test graph.
    /// </summary>
    public static TestGraph CreateGraph(this IAccelerator accelerator)
    {
        return new TestGraph();
    }
}

/// <summary>
/// Test graph implementation for unit testing.
/// </summary>
public class TestGraph
{
    /// <summary>
    /// Adds a dependency between nodes.
    /// </summary>
    public static void AddDependency(object from, object to)
    {
        // Test implementation
    }

    /// <summary>
    /// Instantiates the graph for execution.
    /// </summary>
    public static object Instantiate()
    {
        // Test implementation
        return new { Type = "ExecutableGraph" };
    }
}

/// <summary>
/// Launch configuration for CUDA graph tests.
/// </summary>
public class LaunchConfiguration
{
    public int GridSizeX { get; set; } = 1;
    public int GridSizeY { get; set; } = 1;
    public int GridSizeZ { get; set; } = 1;
    public int BlockSizeX { get; set; } = 256;
    public int BlockSizeY { get; set; } = 1;
    public int BlockSizeZ { get; set; } = 1;
    public int SharedMemoryBytes { get; set; }
}