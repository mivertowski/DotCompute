// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
// Mock CUDA types for testing without CUDA backend dependency

namespace DotCompute.Hardware.Cuda.Tests.Helpers
{
    /// <summary>
    /// Mock CUDA types for testing without CUDA backend dependency
    /// </summary>
    public class LaunchConfiguration
    {
        public int BlockSizeX { get; set; } = 1;
        public int BlockSizeY { get; set; } = 1;
        public int BlockSizeZ { get; set; } = 1;
        public int GridSizeX { get; set; } = 1;
        public int GridSizeY { get; set; } = 1;
        public int GridSizeZ { get; set; } = 1;
    }

    public class CudaGraph
    {
        public string Name { get; }
        public CudaGraph(string name) => Name = name;
    }

    public class CudaGraphExecutable
    {
        private readonly CudaGraphTestWrapper _wrapper;
        public CudaGraphExecutable(CudaGraphTestWrapper wrapper) => _wrapper = wrapper;

        public static void UpdateKernelNode(object node, params object[] arguments) { }
        public static async ValueTask LaunchAsync(object? stream = null) => await Task.Delay(1).ConfigureAwait(false);
        public static async ValueTask LaunchAsync(params object[] arguments) => await Task.Delay(1).ConfigureAwait(false);
    }

    /// <summary>
    /// Test wrapper for CUDA graph operations to provide simplified API for testing.
    /// This wrapper bridges the gap between the test expectations and actual CUDA graph implementation.
    /// </summary>
    public class CudaGraphTestWrapper
    {
        private readonly CudaGraph _graph;
        private readonly object _graphObject;

        public CudaGraphTestWrapper(object? graphObject)
        {
            _graphObject = graphObject ?? throw new ArgumentNullException(nameof(graphObject));
            _graph = graphObject as CudaGraph ?? new CudaGraph("TestGraph");
        }

        /// <summary>
        /// Adds a kernel node to the graph.
        /// </summary>
        public static object AddKernel(ICompiledKernel kernel, LaunchConfiguration config, params object[] arguments)
        {
            // Store kernel information for later execution
            // In a real implementation, this would add a kernel node to the CUDA graph
            // For testing purposes, we'll return a mock node object
            return new GraphNode { NodeType = "Kernel", Kernel = kernel };
        }

        /// <summary>
        /// Adds a memory copy operation to the graph.
        /// </summary>
        public static void AddMemoryCopy<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, long count = 0) where T : unmanaged
        {
            // Store memory copy information for later execution
            // In a real implementation, this would add a memory copy node to the CUDA graph
        }

        /// <summary>
        /// Adds a memory set operation to the graph.
        /// </summary>
        public static object AddMemset<T>(IUnifiedMemoryBuffer<T> buffer, T value) where T : unmanaged
        {
            // Store memory set information for later execution
            return new GraphNode { NodeType = "Memset" };
        }

        /// <summary>
        /// Instantiates the graph for execution.
        /// </summary>
        public CudaGraphExecutable Instantiate()
        {
            return new CudaGraphExecutable(this);
        }

        /// <summary>
        /// Gets the underlying graph object for advanced operations.
        /// </summary>
        public object UnderlyingGraph => _graphObject;
    }

    // CudaGraphExecutable moved to top of file as mock implementation

    /// <summary>
    /// Represents a node in the CUDA graph.
    /// </summary>
    public class GraphNode
    {
        public string NodeType { get; set; } = "Unknown";
        public ICompiledKernel? Kernel { get; set; }
    }

    /// <summary>
    /// Extension methods for graph operations in tests.
    /// </summary>
    public static class CudaGraphTestExtensions
    {
        /// <summary>
        /// Wraps a graph object in a test wrapper for simplified API.
        /// </summary>
        public static CudaGraphTestWrapper AsTestWrapper(this object? graphObject)
        {
            return new CudaGraphTestWrapper(graphObject);
        }

        /// <summary>
        /// Adds a kernel to the graph using test wrapper.
        /// </summary>
        public static object AddKernel(this object graphObject, ICompiledKernel kernel, LaunchConfiguration config, params object[] arguments)
        {
            return CudaGraphTestWrapper.AddKernel(kernel, config, arguments);
        }

        /// <summary>
        /// Adds a dependency between two graph nodes.
        /// </summary>
        public static void AddDependency(this object graphObject, object fromNode, object toNode)
        {
            // Mock implementation for testing
        }

        /// <summary>
        /// Adds a memory copy to the graph using test wrapper.
        /// </summary>
        public static void AddMemoryCopy<T>(this object graphObject, IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, long count = 0) where T : unmanaged
        {
            CudaGraphTestWrapper.AddMemoryCopy(source, destination, count);
        }

        /// <summary>
        /// Instantiates the graph using test wrapper.
        /// </summary>
        public static CudaGraphExecutable Instantiate(this object graphObject)
        {
            var wrapper = new CudaGraphTestWrapper(graphObject);
            return wrapper.Instantiate();
        }
    }
}