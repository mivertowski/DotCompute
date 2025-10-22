// <copyright file="LayerAssignmentStrategy.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines strategies for assigning neural network layers or computational stages to devices in model parallelism.
    /// These strategies determine how to partition large models across multiple devices for optimal performance.
    /// </summary>
    public enum LayerAssignmentStrategy
    {
        /// <summary>
        /// Automatic assignment based on memory requirements and device capabilities.
        /// The system analyzes layer memory footprints and computational requirements to optimize placement.
        /// Provides the best balance between performance and memory utilization without manual intervention.
        /// </summary>
        Automatic,

        /// <summary>
        /// Sequential assignment of layers to devices in order.
        /// Layers are assigned to devices in sequence, moving to the next device when capacity is reached.
        /// Simple and predictable, suitable when layers have similar computational requirements.
        /// </summary>
        Sequential,

        /// <summary>
        /// Interleaved assignment for better load balancing.
        /// Distributes layers across devices in an interleaved pattern to balance computational load.
        /// Helps prevent hotspots and improves parallel efficiency for models with varying layer complexities.
        /// </summary>
        Interleaved,

        /// <summary>
        /// Custom assignment specified by user or domain-specific logic.
        /// Allows explicit control over which layers are placed on which devices.
        /// Enables optimization for specific model architectures or hardware configurations.
        /// </summary>
        Custom
    }
}
