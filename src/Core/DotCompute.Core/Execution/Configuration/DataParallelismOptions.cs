// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Configuration
{
    /// <summary>
    /// Configuration options for data parallelism execution.
    /// Data parallelism distributes data across multiple devices while keeping the model replicated on each device.
    /// This is ideal for training and inference scenarios where the model fits on individual devices
    /// but the dataset is large enough to benefit from parallel processing.
    /// </summary>
    public class DataParallelismOptions
    {
        /// <summary>
        /// Gets or sets the target device IDs to use for execution.
        /// If null or empty, all available devices will be used.
        /// Device IDs should follow the format "device_type:device_index" (e.g., "cuda:0", "opencl:1").
        /// </summary>
        /// <value>
        /// A read-only list of device identifier strings, or null to use all available devices.
        /// </value>
        /// <example>
        /// <code>
        /// var options = new DataParallelismOptions
        /// {
        ///     TargetDevices = new[] { "cuda:0", "cuda:1", "opencl:0" }
        /// };
        /// </code>
        /// </example>
        public IReadOnlyList<string>? TargetDevices { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of devices to use.
        /// This provides a cap on device usage even when more devices are available.
        /// If null, all available devices (or those specified in TargetDevices) will be used.
        /// </summary>
        /// <value>
        /// The maximum number of devices to use, or null for no limit.
        /// </value>
        /// <remarks>
        /// This setting is useful for limiting resource usage or when testing scalability
        /// with different numbers of devices.
        /// </remarks>
        public int? MaxDevices { get; set; }

        /// <summary>
        /// Gets or sets whether to enable peer-to-peer GPU transfers.
        /// When enabled, GPUs can transfer data directly without going through system memory,
        /// significantly improving communication performance for multi-GPU setups.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable peer-to-peer transfers; otherwise, <c>false</c>.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// P2P transfers are only available on supported hardware configurations.
        /// If not supported, the system will fall back to host-based transfers automatically.
        /// </remarks>
        public bool EnablePeerToPeer { get; set; } = true;

        /// <summary>
        /// Gets or sets the synchronization strategy for coordinating work across devices.
        /// Different strategies offer trade-offs between latency, throughput, and resource usage.
        /// </summary>
        /// <value>
        /// The synchronization strategy to use. Default is <see cref="SynchronizationStrategy.EventBased"/>.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><see cref="SynchronizationStrategy.EventBased"/>: Uses device events for fine-grained synchronization</description></item>
        /// <item><description><see cref="SynchronizationStrategy.Barrier"/>: Uses barrier synchronization for bulk operations</description></item>
        /// <item><description><see cref="SynchronizationStrategy.LockFree"/>: Uses atomic operations for minimal overhead</description></item>
        /// <item><description><see cref="SynchronizationStrategy.HostBased"/>: Uses CPU-based synchronization</description></item>
        /// </list>
        /// </remarks>
        public SynchronizationStrategy SyncStrategy { get; set; } = SynchronizationStrategy.EventBased;

        /// <summary>
        /// Gets or sets whether to overlap computation with communication.
        /// When enabled, data transfers and computations can occur simultaneously,
        /// hiding communication latency and improving overall throughput.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable computation-communication overlap; otherwise, <c>false</c>.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// This optimization requires careful kernel scheduling and may increase memory usage
        /// due to additional buffering requirements.
        /// </remarks>
        public bool OverlapComputeAndCommunication { get; set; } = true;

        /// <summary>
        /// Gets or sets the load balancing strategy for distributing work across devices.
        /// Different strategies optimize for different scenarios such as homogeneous vs. heterogeneous hardware.
        /// </summary>
        /// <value>
        /// The load balancing strategy to use. Default is <see cref="LoadBalancingStrategy.Adaptive"/>.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><see cref="LoadBalancingStrategy.RoundRobin"/>: Equal distribution, good for homogeneous devices</description></item>
        /// <item><description><see cref="LoadBalancingStrategy.Weighted"/>: Distribution based on device capabilities</description></item>
        /// <item><description><see cref="LoadBalancingStrategy.Adaptive"/>: Dynamic adjustment based on performance feedback</description></item>
        /// <item><description><see cref="LoadBalancingStrategy.Dynamic"/>: Real-time load balancing with work stealing</description></item>
        /// <item><description><see cref="LoadBalancingStrategy.Manual"/>: User-specified distribution</description></item>
        /// </list>
        /// </remarks>
        public LoadBalancingStrategy LoadBalancing { get; set; } = LoadBalancingStrategy.Adaptive;
    }
}
