// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Host callback node parameters
    /// </summary>
    public class HostNodeParams
    {
        /// <summary>
        /// Gets or sets the function.
        /// </summary>
        /// <value>The function.</value>
        public Action<nint> Function { get; set; } = null!;
        /// <summary>
        /// Gets or sets the user data.
        /// </summary>
        /// <value>The user data.</value>
        public nint UserData { get; set; }
    }
}