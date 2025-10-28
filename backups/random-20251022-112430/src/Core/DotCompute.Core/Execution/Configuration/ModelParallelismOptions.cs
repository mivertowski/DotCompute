// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Configuration
{
    /// <summary>
    /// Configuration options for model parallelism execution.
    /// Model parallelism distributes different parts of a large model across multiple devices,
    /// enabling the execution of models that are too large to fit on a single device.
    /// This is particularly useful for large neural networks, scientific simulations, or
    /// any computational model where memory requirements exceed single-device capacity.
    /// </summary>
    public class ModelParallelismOptions
    {
        /// <summary>
        /// Gets or sets the layer assignment strategy for distributing model components across devices.
        /// This determines how the model is partitioned and which parts are assigned to which devices.
        /// </summary>
        /// <value>
        /// The layer assignment strategy to use. Default is <see cref="LayerAssignmentStrategy.Automatic"/>.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><see cref="LayerAssignmentStrategy.Automatic"/>: Algorithm-driven assignment based on memory and compute requirements</description></item>
        /// <item><description><see cref="LayerAssignmentStrategy.Sequential"/>: Assigns consecutive layers to devices in order</description></item>
        /// <item><description><see cref="LayerAssignmentStrategy.Interleaved"/>: Distributes layers across devices for better load balancing</description></item>
        /// <item><description><see cref="LayerAssignmentStrategy.Custom"/>: User-defined assignment scheme</description></item>
        /// </list>
        /// </remarks>
        public LayerAssignmentStrategy LayerAssignment { get; set; } = LayerAssignmentStrategy.Automatic;

        /// <summary>
        /// Gets or sets the communication backend for inter-layer transfers and synchronization.
        /// The choice of backend affects performance, scalability, and compatibility with different hardware.
        /// </summary>
        /// <value>
        /// The communication backend to use. Default is <see cref="CommunicationBackend.NCCL"/>.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><see cref="CommunicationBackend.NCCL"/>: NVIDIA's optimized library for multi-GPU communication</description></item>
        /// <item><description><see cref="CommunicationBackend.MPI"/>: Message Passing Interface for distributed computing</description></item>
        /// <item><description><see cref="CommunicationBackend.P2P"/>: Direct peer-to-peer GPU transfers</description></item>
        /// <item><description><see cref="CommunicationBackend.Host"/>: CPU-mediated communication</description></item>
        /// </list>
        /// </remarks>
        public CommunicationBackend CommunicationBackend { get; set; } = CommunicationBackend.NCCL;

        /// <summary>
        /// Gets or sets whether to enable gradient checkpointing for memory optimization.
        /// Gradient checkpointing trades computation for memory by recomputing certain intermediate
        /// values during the backward pass instead of storing them during the forward pass.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable gradient checkpointing; otherwise, <c>false</c>.
        /// Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// This optimization is particularly useful for training very large models where memory
        /// is the limiting factor. It increases computation time but can significantly reduce
        /// memory requirements, allowing larger models or batch sizes to be processed.
        /// </remarks>
        public bool EnableGradientCheckpointing { get; set; }

        /// <summary>
        /// Gets or sets the memory optimization level for managing device memory usage.
        /// Higher optimization levels provide more aggressive memory management at the cost
        /// of potential performance overhead and complexity.
        /// </summary>
        /// <value>
        /// The memory optimization level to use. Default is <see cref="MemoryOptimizationLevel.Balanced"/>.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><see cref="MemoryOptimizationLevel.None"/>: No memory optimizations, maximum performance</description></item>
        /// <item><description><see cref="MemoryOptimizationLevel.Balanced"/>: Balanced approach optimizing memory and performance</description></item>
        /// <item><description><see cref="MemoryOptimizationLevel.Aggressive"/>: Maximum memory savings, potential performance impact</description></item>
        /// </list>
        /// </remarks>
        public MemoryOptimizationLevel MemoryOptimization { get; set; } = MemoryOptimizationLevel.Balanced;

        /// <summary>
        /// Converts model parallelism options to data parallelism options for device selection.
        /// This provides a compatible configuration for the underlying device management system
        /// while maintaining model parallelism semantics.
        /// </summary>
        /// <returns>
        /// A <see cref="DataParallelismOptions"/> instance configured for model parallelism requirements.
        /// </returns>
        /// <remarks>
        /// The returned options are configured with:
        /// <list type="bullet">
        /// <item><description>All available devices (MaxDevices = null)</description></item>
        /// <item><description>Peer-to-peer transfers enabled for fast inter-device communication</description></item>
        /// <item><description>Event-based synchronization for fine-grained control</description></item>
        /// <item><description>Computation-communication overlap enabled</description></item>
        /// <item><description>Manual load balancing since layer assignment is controlled</description></item>
        /// </list>
        /// </remarks>
        public static DataParallelismOptions ToDataParallelOptions() => new()
        {
            MaxDevices = null, // Use all available devices
            EnablePeerToPeer = true,
            SyncStrategy = SynchronizationStrategy.EventBased,
            OverlapComputeAndCommunication = true,
            LoadBalancing = LoadBalancingStrategy.Manual // Layer assignment is manual
        };
    }
}
