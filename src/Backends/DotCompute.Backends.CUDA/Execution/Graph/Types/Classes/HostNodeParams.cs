// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Parameters for a CUDA graph host callback node.
    /// </summary>
    /// <remarks>
    /// This class defines parameters for host callback operations within a CUDA graph.
    /// Host nodes execute CPU code as part of the graph execution sequence, allowing
    /// integration of CPU-side operations (logging, I/O, synchronization) within
    /// GPU workflows. The callback runs on the CPU thread when the graph reaches this node.
    /// </remarks>
    public class HostNodeParams
    {
        /// <summary>
        /// Gets or sets the host callback function to execute when the node is reached.
        /// </summary>
        /// <value>
        /// A managed delegate that will be invoked on the CPU when graph execution
        /// reaches this node. The parameter is a pointer to user-provided data.
        /// The callback should complete quickly to avoid blocking graph execution.
        /// </value>
        public Action<nint> Function { get; set; } = null!;

        /// <summary>
        /// Gets or sets the user-provided data pointer passed to the callback function.
        /// </summary>
        /// <value>
        /// Native pointer to user-defined data that will be passed to the callback.
        /// Can be used to pass context, state, or output buffers to the host function.
        /// The caller is responsible for memory management of this data.
        /// </value>
        public nint UserData { get; set; }
    }
}
