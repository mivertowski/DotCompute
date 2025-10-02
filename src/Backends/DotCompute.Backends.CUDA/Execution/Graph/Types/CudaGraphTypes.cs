// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Execution.Graph.Configuration;
using DotCompute.Backends.CUDA.Execution.Graph.Enums;

namespace DotCompute.Backends.CUDA.Execution.Graph.Types
{
    /// <summary>
    /// CUDA Graph representation
    /// </summary>
    public class CudaGraph(string name, GraphConfiguration configuration)
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; } = name;
        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        /// <value>The configuration.</value>
        public GraphConfiguration Configuration { get; } = configuration;
        /// <summary>
        /// Gets or sets the handle.
        /// </summary>
        /// <value>The handle.</value>
        public IntPtr Handle { get; set; }
        /// <summary>
        /// Gets or sets the nodes.
        /// </summary>
        /// <value>The nodes.</value>
        public IList<CudaGraphNode> Nodes { get; } = [];
        /// <summary>
        /// Gets or sets a value indicating whether captured.
        /// </summary>
        /// <value>The is captured.</value>
        public bool IsCaptured { get; set; }
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Executable graph instance
    /// </summary>
    public class CudaGraphExecutable
    {
        /// <summary>
        /// Gets or sets the handle.
        /// </summary>
        /// <value>The handle.</value>
        public IntPtr Handle { get; set; }
        /// <summary>
        /// Gets or sets the graph.
        /// </summary>
        /// <value>The graph.</value>
        public Graph.CudaGraph Graph { get; set; } = null!;
        /// <summary>
        /// Gets or sets the instantiated at.
        /// </summary>
        /// <value>The instantiated at.</value>
        public DateTime InstantiatedAt { get; set; }
        /// <summary>
        /// Gets or sets the updated at.
        /// </summary>
        /// <value>The updated at.</value>
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Graph node for CUDA operations (renamed to avoid conflict)
    /// </summary>
    public class CudaGraphNode
    {
        /// <summary>
        /// Gets or sets the handle.
        /// </summary>
        /// <value>The handle.</value>
        public IntPtr Handle { get; set; }
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public GraphNodeType Type { get; set; }
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the dependencies.
        /// </summary>
        /// <value>The dependencies.</value>
        public IList<CudaGraphNode> Dependencies { get; } = [];
        /// <summary>
        /// Gets or sets the user data.
        /// </summary>
        /// <value>The user data.</value>
        public object? UserData { get; set; }
        /// <summary>
        /// Gets or sets the child graph.
        /// </summary>
        /// <value>The child graph.</value>
        public CudaGraph? ChildGraph { get; set; }
    }
}