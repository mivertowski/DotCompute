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
        /// <summary>
        /// Gets or sets the block size x.
        /// </summary>
        /// <value>The block size x.</value>
        public int BlockSizeX { get; set; } = 1;
        /// <summary>
        /// Gets or sets the block size y.
        /// </summary>
        /// <value>The block size y.</value>
        public int BlockSizeY { get; set; } = 1;
        /// <summary>
        /// Gets or sets the block size z.
        /// </summary>
        /// <value>The block size z.</value>
        public int BlockSizeZ { get; set; } = 1;
        /// <summary>
        /// Gets or sets the grid size x.
        /// </summary>
        /// <value>The grid size x.</value>
        public int GridSizeX { get; set; } = 1;
        /// <summary>
        /// Gets or sets the grid size y.
        /// </summary>
        /// <value>The grid size y.</value>
        public int GridSizeY { get; set; } = 1;
        /// <summary>
        /// Gets or sets the grid size z.
        /// </summary>
        /// <value>The grid size z.</value>
        public int GridSizeZ { get; set; } = 1;
    }
    /// <summary>
    /// A class that represents cuda graph.
    /// </summary>

    public class CudaGraph(string name)
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; } = name;
    }
    /// <summary>
    /// A class that represents cuda graph executable.
    /// </summary>

    public class CudaGraphExecutable(CudaGraphTestWrapper wrapper)
    {
        /// <summary>
        /// Updates the kernel node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="arguments">The arguments.</param>
        public static void UpdateKernelNode(object node, params object[] arguments) { }
        /// <summary>
        /// Gets launch asynchronously.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The result of the operation.</returns>
        public static async ValueTask LaunchAsync(object? stream = null) => await Task.Delay(1).ConfigureAwait(false);
        /// <summary>
        /// Gets launch asynchronously.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The result of the operation.</returns>
        public static async ValueTask LaunchAsync(params object[] arguments) => await Task.Delay(1).ConfigureAwait(false);
    }

    /// <summary>
    /// Test wrapper for CUDA graph operations to provide simplified API for testing.
    /// This wrapper bridges the gap between the test expectations and actual CUDA graph implementation.
    /// </summary>
    public class CudaGraphTestWrapper(object? graphObject)
    {
        private readonly object _graphObject = graphObject ?? throw new ArgumentNullException(nameof(graphObject));

        /// <summary>
        /// Adds a kernel node to the graph.
        /// </summary>
        public static object AddKernel(ICompiledKernel kernel, LaunchConfiguration config, params object[] arguments)
            // Store kernel information for later execution
            // In a real implementation, this would add a kernel node to the CUDA graph
            // For testing purposes, we'll return a mock node object
            => new GraphNode { NodeType = "Kernel", Kernel = kernel };

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
            // Store memory set information for later execution
            => new GraphNode { NodeType = "Memset" };

        /// <summary>
        /// Instantiates the graph for execution.
        /// </summary>
        public CudaGraphExecutable Instantiate() => new(this);

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
        /// <summary>
        /// Gets or sets the node type.
        /// </summary>
        /// <value>The node type.</value>
        public string NodeType { get; set; } = "Unknown";
        /// <summary>
        /// Gets or sets the kernel.
        /// </summary>
        /// <value>The kernel.</value>
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
        public static CudaGraphTestWrapper AsTestWrapper(this object? graphObject) => new(graphObject);

        /// <summary>
        /// Adds a kernel to the graph using test wrapper.
        /// </summary>
        public static object AddKernel(this object graphObject, ICompiledKernel kernel, LaunchConfiguration config, params object[] arguments) => CudaGraphTestWrapper.AddKernel(kernel, config, arguments);

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
        public static void AddMemoryCopy<T>(this object graphObject, IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, long count = 0) where T : unmanaged => CudaGraphTestWrapper.AddMemoryCopy(source, destination, count);

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