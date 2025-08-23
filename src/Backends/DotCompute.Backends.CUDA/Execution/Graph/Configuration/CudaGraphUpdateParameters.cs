// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;

namespace DotCompute.Backends.CUDA.Execution.Graph.Configuration
{
    /// <summary>
    /// Contains parameters for updating an existing CUDA graph instance with new values.
    /// Enables dynamic modification of graph parameters without requiring complete re-instantiation.
    /// </summary>
    /// <remarks>
    /// Graph updates allow for efficient parameter changes in repetitive workloads where the
    /// computational structure remains the same but input parameters vary between executions.
    /// This is particularly useful for iterative algorithms and dynamic simulations.
    /// </remarks>
    public sealed class CudaGraphUpdateParameters
    {
        /// <summary>
        /// Gets or sets the handle to the source graph containing the updated structure.
        /// </summary>
        /// <value>An <see cref="IntPtr"/> representing the native CUDA graph handle containing the new structure.</value>
        /// <remarks>
        /// This handle references a CUDA graph that contains the updated topology or parameters
        /// that should be applied to the target graph instance during the update operation.
        /// </remarks>
        public IntPtr SourceGraph { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of parameters that have been updated and need to be applied.
        /// </summary>
        /// <value>A dictionary mapping parameter names to their new values.</value>
        /// <remarks>
        /// This collection contains the specific parameters that have changed and need to be
        /// updated in the graph instance. The keys represent parameter identifiers, and the
        /// values represent the new parameter values to be applied.
        /// </remarks>
        public Dictionary<string, object> UpdatedParameters { get; set; } = [];
    }
}