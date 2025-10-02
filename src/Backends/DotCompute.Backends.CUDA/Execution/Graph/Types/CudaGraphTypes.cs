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
        public string Name { get; } = name;
        public GraphConfiguration Configuration { get; } = configuration;
        public IntPtr Handle { get; set; }
        public List<CudaGraphNode> Nodes { get; set; } = [];
        public bool IsCaptured { get; set; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Executable graph instance
    /// </summary>
    public class CudaGraphExecutable
    {
        public IntPtr Handle { get; set; }
        public Graph.CudaGraph Graph { get; set; } = null!;
        public DateTime InstantiatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Graph node for CUDA operations (renamed to avoid conflict)
    /// </summary>
    public class CudaGraphNode
    {
        public IntPtr Handle { get; set; }
        public GraphNodeType Type { get; set; }
        public string Id { get; set; } = string.Empty;
        public List<CudaGraphNode> Dependencies { get; set; } = [];
        public object? UserData { get; set; }
        public CudaGraph? ChildGraph { get; set; }
    }
}