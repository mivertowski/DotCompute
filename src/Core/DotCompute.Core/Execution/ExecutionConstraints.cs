// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Defines constraints and preferences for execution plan generation.
    /// Used to guide the selection of devices, strategies, and optimization techniques.
    /// </summary>
    public sealed class ExecutionConstraints
    {
        /// <summary>Gets or initializes the maximum number of devices to use for execution.</summary>
        /// <value>The maximum device count, or null for no limit</value>
        public int? MaxDevices { get; init; }

        /// <summary>Gets or initializes the preferred device IDs for execution.</summary>
        /// <value>Read-only list of device identifiers, or null for automatic selection</value>
        public IReadOnlyList<string>? PreferredDeviceIds { get; init; }

        /// <summary>Gets or initializes the load balancing strategy to use.</summary>
        /// <value>The load balancing strategy, or null for automatic selection</value>
        public LoadBalancingStrategy? LoadBalancingStrategy { get; init; }

        /// <summary>Gets or initializes the synchronization strategy between devices.</summary>
        /// <value>The synchronization strategy, or null for automatic selection</value>
        public SynchronizationStrategy? SynchronizationStrategy { get; init; }

        /// <summary>Gets or initializes the layer assignment strategy for model parallel execution.</summary>
        /// <value>The layer assignment strategy, or null for automatic selection</value>
        public LayerAssignmentStrategy? LayerAssignmentStrategy { get; init; }

        /// <summary>Gets or initializes the communication backend for inter-device communication.</summary>
        /// <value>The communication backend, or null for automatic selection</value>
        public CommunicationBackend? CommunicationBackend { get; init; }

        /// <summary>Gets or initializes the memory optimization level to apply.</summary>
        /// <value>The memory optimization level, or null for automatic selection</value>
        public MemoryOptimizationLevel? MemoryOptimizationLevel { get; init; }

        /// <summary>Gets or initializes the pipeline scheduling strategy for pipeline parallel execution.</summary>
        /// <value>The pipeline scheduling strategy, or null for automatic selection</value>
        public PipelineSchedulingStrategy? PipelineSchedulingStrategy { get; init; }

        /// <summary>Gets or initializes the microbatch size for pipeline execution.</summary>
        /// <value>The microbatch size, or null for automatic selection</value>
        public int? MicrobatchSize { get; init; }

        /// <summary>Gets or initializes the buffer depth for pipeline execution.</summary>
        /// <value>The buffer depth for double/triple buffering, or null for automatic selection</value>
        public int? BufferDepth { get; init; }

        /// <summary>Gets or initializes whether peer-to-peer communication is enabled.</summary>
        /// <value>True to enable peer-to-peer communication, false to disable</value>
        public bool EnablePeerToPeer { get; init; } = true;

        /// <summary>Gets or initializes the maximum allowed execution time.</summary>
        /// <value>The maximum execution time, or null for no limit</value>
        public TimeSpan? MaxExecutionTime { get; init; }

        /// <summary>Gets or initializes the maximum memory usage allowed.</summary>
        /// <value>The maximum memory usage in bytes, or null for no limit</value>
        public long? MaxMemoryUsage { get; init; }
    }
}
