// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Execution.Graph.Types;
using DotCompute.Backends.CUDA.Native;

namespace DotCompute.Backends.CUDA.Execution.Graph
{
    /// <summary>
    /// Represents a CUDA computational graph that can be compiled, optimized, and executed.
    /// Provides high-performance execution through kernel fusion and optimization for RTX 2000 Ada architecture.
    /// </summary>
    /// <remarks>
    /// CUDA graphs enable efficient execution of repetitive kernel sequences by reducing CPU-GPU
    /// synchronization overhead and enabling advanced optimizations like kernel fusion.
    /// </remarks>
    public sealed class CudaGraph : IDisposable
    {
        private readonly Lock _lock = new();
        private readonly ConcurrentBag<CudaGraphNode> _nodes = [];
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaGraph"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an empty graph with default configuration. The graph must be properly
        /// configured and built before it can be instantiated for execution.
        /// </remarks>
        public CudaGraph()
        {
            CreatedAt = DateTimeOffset.UtcNow;
            Id = Guid.NewGuid().ToString();
            Name = Id; // Default name to ID for backward compatibility
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaGraph"/> class with a specified name.
        /// </summary>
        /// <param name="name">The human-readable name for this graph.</param>
        /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
        public CudaGraph(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {

                throw new ArgumentException("Graph name cannot be null or empty.", nameof(name));
            }


            CreatedAt = DateTimeOffset.UtcNow;
            Id = Guid.NewGuid().ToString();
            Name = name;
        }

        /// <summary>
        /// Gets or sets the unique identifier for this CUDA graph.
        /// </summary>
        /// <value>A string that uniquely identifies this graph instance.</value>
        /// <remarks>
        /// This property is automatically set to a unique GUID when the graph is created.
        /// It should not be modified after creation unless absolutely necessary.
        /// </remarks>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable name for this CUDA graph.
        /// </summary>
        /// <value>A string representing the graph's name for identification and debugging purposes.</value>
        /// <remarks>
        /// This property provides a more meaningful identifier than the GUID-based Id property.
        /// It is used in logging, debugging, and error messages to help identify specific graphs.
        /// The name should be unique within a given context but is not enforced by this class.
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the native CUDA graph handle.
        /// </summary>
        /// <value>An <see cref="IntPtr"/> representing the native CUDA graph handle.</value>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// Gets the thread-safe collection of graph nodes that comprise this CUDA graph.
        /// </summary>
        /// <value>A thread-safe list of <see cref="CudaGraphNode"/> instances representing the graph structure.</value>
        /// <remarks>
        /// This property provides a thread-safe view of all nodes in the graph. The underlying collection
        /// uses a concurrent data structure to ensure safe access from multiple threads.
        /// Use the <see cref="AddNode(CudaGraphNode)"/> and <see cref="RemoveNode(string)"/> methods
        /// to modify the collection safely.
        /// </remarks>
        public IList<CudaGraphNode> Nodes
        {
            get
            {
                lock (_lock)
                {
                    return _nodes.ToList().AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets or sets the list of kernel operations that comprise this graph.
        /// </summary>
        /// <value>A list of <see cref="CudaKernelOperation"/> instances representing the computational sequence.</value>
        /// <remarks>
        /// This property maintains backward compatibility with existing code that expects kernel operations.
        /// New code should prefer using the <see cref="Nodes"/> property for graph structure manipulation.
        /// </remarks>
        public IList<CudaKernelOperation> Operations { get; init; } = [];


        /// <summary>
        /// Gets or sets the timestamp when this graph was created.
        /// </summary>
        /// <value>A <see cref="DateTimeOffset"/> indicating when the graph was instantiated.</value>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the graph has been built and is ready for instantiation.
        /// </summary>
        /// <value><c>true</c> if the graph has been successfully built; otherwise, <c>false</c>.</value>
        public bool IsBuilt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the graph has been optimized for execution.
        /// </summary>
        /// <value><c>true</c> if optimization passes have been applied; otherwise, <c>false</c>.</value>
        public bool IsOptimized { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this graph was created through stream capture.
        /// </summary>
        /// <value><c>true</c> if the graph was captured from a CUDA stream; otherwise, <c>false</c>.</value>
        public bool IsCaptured { get; set; }

        /// <summary>
        /// Gets or sets the optimization options for this graph.
        /// </summary>
        /// <value>A CudaGraphOptimizationOptions instance specifying optimization behavior.</value>
        public CUDA.Types.CudaGraphOptimizationOptions Options { get; set; } = new CUDA.Types.CudaGraphOptimizationOptions();

        /// <summary>
        /// Gets the current count of nodes in the graph.
        /// </summary>
        /// <value>The number of nodes currently in the graph.</value>
        /// <remarks>
        /// This property provides an efficient way to get the node count without enumerating
        /// the entire collection. It is thread-safe and reflects the current state.
        /// </remarks>
        public int NodeCount => _nodes.Count;

        /// <summary>
        /// Gets a value indicating whether the graph is empty (contains no nodes).
        /// </summary>
        /// <value><c>true</c> if the graph contains no nodes; otherwise, <c>false</c>.</value>
        public bool IsEmpty => _nodes.IsEmpty;

        /// <summary>
        /// Adds a node to the graph in a thread-safe manner.
        /// </summary>
        /// <param name="node">The node to add to the graph.</param>
        /// <exception cref="ArgumentNullException">Thrown when node is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the graph has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a node with the same ID already exists.</exception>
        /// <remarks>
        /// This method ensures thread-safe addition of nodes to the graph. It validates that
        /// the node ID is unique within the graph before adding.
        /// </remarks>
        public void AddNode(CudaGraphNode node)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(node);

            lock (_lock)
            {
                // Check for duplicate node IDs
                if (_nodes.Any(n => n.Id == node.Id))
                {
                    throw new InvalidOperationException($"A node with ID '{node.Id}' already exists in the graph.");
                }

                _nodes.Add(node);
            }
        }

        /// <summary>
        /// Removes a node from the graph by its ID.
        /// </summary>
        /// <param name="nodeId">The ID of the node to remove.</param>
        /// <returns><c>true</c> if the node was found and removed; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when nodeId is null or whitespace.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the graph has been disposed.</exception>
        /// <remarks>
        /// This method removes the node and also cleans up any dependency relationships
        /// that reference the removed node.
        /// </remarks>
        public bool RemoveNode(string nodeId)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (string.IsNullOrWhiteSpace(nodeId))
            {

                throw new ArgumentException("Node ID cannot be null or empty.", nameof(nodeId));
            }


            lock (_lock)
            {
                var nodeToRemove = _nodes.FirstOrDefault(n => n.Id == nodeId);
                if (nodeToRemove == null)
                {

                    return false;
                }

                // Create a new collection without the removed node

                var remainingNodes = _nodes.Where(n => n.Id != nodeId).ToList();

                // Remove dependencies to the deleted node

                foreach (var node in remainingNodes)
                {
                    var depsToRemove = node.Dependencies.Where(dep => dep.Id == nodeId).ToList();
                    foreach (var dep in depsToRemove)
                    {
                        _ = node.Dependencies.Remove(dep);
                    }
                }

                // Clear and repopulate the concurrent bag
                while (!_nodes.IsEmpty)
                {
                    _ = _nodes.TryTake(out _);
                }

                foreach (var node in remainingNodes)
                {
                    _nodes.Add(node);
                }

                return true;
            }
        }

        /// <summary>
        /// Finds a node in the graph by its ID.
        /// </summary>
        /// <param name="nodeId">The ID of the node to find.</param>
        /// <returns>The node with the specified ID, or <c>null</c> if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when nodeId is null or whitespace.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the graph has been disposed.</exception>
        public CudaGraphNode? FindNode(string nodeId)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (string.IsNullOrWhiteSpace(nodeId))
            {

                throw new ArgumentException("Node ID cannot be null or empty.", nameof(nodeId));
            }


            return _nodes.FirstOrDefault(n => n.Id == nodeId);
        }

        /// <summary>
        /// Clears all nodes from the graph.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the graph has been disposed.</exception>
        /// <remarks>
        /// This method removes all nodes from the graph in a thread-safe manner.
        /// It also cleans up any resources associated with the nodes.
        /// </remarks>
        public void Clear()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_lock)
            {
                // Clean up any resources held by nodes
                foreach (var node in _nodes)
                {
                    if (node.UserData is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch
                        {
                            // Ignore disposal errors during cleanup
                        }
                    }
                }

                while (!_nodes.IsEmpty)
                {
                    _ = _nodes.TryTake(out _);
                }
            }
        }

        /// <summary>
        /// Releases the native CUDA graph resources.
        /// </summary>
        /// <remarks>
        /// This method is called automatically when the graph is disposed. It safely destroys
        /// the native CUDA graph handle and prevents further use of this graph instance.
        /// The disposal is thread-safe and will clean up all nodes and their associated resources.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }


                try
                {
                    // Clear all nodes first to clean up their resources
                    Clear();

                    // Destroy the native CUDA graph handle
                    if (Handle != IntPtr.Zero)
                    {
                        _ = CudaRuntime.cuGraphDestroy(Handle);
                        Handle = IntPtr.Zero;
                    }
                }
                catch (Exception)
                {
                    // Ignore disposal errors - the native resources may have already been released
                }
                finally
                {
                    _disposed = true;
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer for the CudaGraph class.
        /// </summary>
        /// <remarks>
        /// This finalizer ensures that native resources are released even if Dispose is not called.
        /// However, it is recommended to explicitly call Dispose or use a using statement.
        /// </remarks>
        ~CudaGraph()
        {
            Dispose();
        }
    }
}
