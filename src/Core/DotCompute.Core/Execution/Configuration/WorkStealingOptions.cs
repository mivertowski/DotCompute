// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Configuration
{
    /// <summary>
    /// Configuration options for work stealing execution.
    /// Work stealing is a dynamic load balancing technique where idle devices can "steal" work
    /// from busy devices, automatically adapting to varying workloads and device performance
    /// characteristics. This approach is particularly effective for irregular workloads,
    /// heterogeneous hardware setups, and scenarios where work distribution is difficult to predict.
    /// </summary>
    public class WorkStealingOptions
    {
        /// <summary>
        /// Gets or sets the initial work chunk size.
        /// This determines the granularity of work items when the computation is first divided.
        /// Larger chunks reduce overhead but may lead to load imbalance, while smaller chunks
        /// provide better load distribution but increase management overhead.
        /// </summary>
        /// <value>
        /// The initial work chunk size. Default is 1024.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Large chunks (1024+): Good for compute-intensive tasks with low stealing overhead</description></item>
        /// <item><description>Medium chunks (256-1024): Balanced approach for most workloads</description></item>
        /// <item><description>Small chunks (64-256): Better for irregular workloads and load balancing</description></item>
        /// </list>
        /// The optimal size depends on the computation complexity and device characteristics.
        /// </remarks>
        /// <example>
        /// <code>
        /// var options = new WorkStealingOptions
        /// {
        ///     InitialChunkSize = 512,
        ///     MinChunkSize = 32,
        ///     StealingStrategy = StealingStrategy.RichestVictim
        /// };
        /// </code>
        /// </example>
        public int InitialChunkSize { get; set; } = 1024;

        /// <summary>
        /// Gets or sets the minimum work chunk size.
        /// This prevents work chunks from being subdivided beyond a certain point, ensuring
        /// that the overhead of work stealing doesn't exceed the benefit. Work items smaller
        /// than this threshold will not be further divided during stealing operations.
        /// </summary>
        /// <value>
        /// The minimum work chunk size. Default is 64.
        /// </value>
        /// <remarks>
        /// The minimum chunk size should be chosen based on:
        /// <list type="bullet">
        /// <item><description>The overhead cost of task creation and management</description></item>
        /// <item><description>The minimum amount of work needed for efficient device utilization</description></item>
        /// <item><description>Memory transfer costs for very small work items</description></item>
        /// </list>
        /// Setting this too low can lead to thrashing, while setting it too high may limit load balancing effectiveness.
        /// </remarks>
        public int MinChunkSize { get; set; } = 64;

        /// <summary>
        /// Gets or sets the stealing strategy used to select victims for work stealing.
        /// Different strategies optimize for different scenarios such as NUMA topology,
        /// device heterogeneity, and communication patterns.
        /// </summary>
        /// <value>
        /// The stealing strategy to use. Default is <see cref="StealingStrategy.RandomVictim"/>.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><see cref="StealingStrategy.RandomVictim"/>: Simple and effective for homogeneous systems</description></item>
        /// <item><description><see cref="StealingStrategy.RichestVictim"/>: Targets devices with the most work for maximum impact</description></item>
        /// <item><description><see cref="StealingStrategy.NearestVictim"/>: NUMA-aware strategy minimizing communication costs</description></item>
        /// <item><description><see cref="StealingStrategy.Hierarchical"/>: Multi-level stealing for large-scale systems</description></item>
        /// </list>
        /// </remarks>
        public StealingStrategy StealingStrategy { get; set; } = StealingStrategy.RandomVictim;

        /// <summary>
        /// Gets or sets the work queue depth per device.
        /// This determines how many work items each device can hold in its local queue.
        /// Deeper queues can improve performance by reducing contention but require more memory
        /// and may increase the scope of work available for stealing.
        /// </summary>
        /// <value>
        /// The work queue depth for each device. Default is 16.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Shallow queues (4-8): Lower memory usage, higher contention</description></item>
        /// <item><description>Medium queues (8-32): Good balance for most applications</description></item>
        /// <item><description>Deep queues (32+): Lower contention, higher memory usage</description></item>
        /// </list>
        /// The optimal depth depends on the rate of work generation vs. consumption and the stealing frequency.
        /// </remarks>
        public int WorkQueueDepth { get; set; } = 16;

        /// <summary>
        /// Converts work stealing options to data parallelism options for device selection.
        /// This provides a compatible configuration for the underlying device management system
        /// while maintaining work stealing semantics and optimizations.
        /// </summary>
        /// <returns>
        /// A <see cref="DataParallelismOptions"/> instance configured for work stealing requirements.
        /// </returns>
        /// <remarks>
        /// The returned options are configured with:
        /// <list type="bullet">
        /// <item><description>All available devices (MaxDevices = null) for maximum stealing opportunities</description></item>
        /// <item><description>Peer-to-peer disabled since work stealing manages its own transfers</description></item>
        /// <item><description>Lock-free synchronization for minimal overhead during stealing</description></item>
        /// <item><description>Computation-communication overlap enabled to hide stealing latency</description></item>
        /// <item><description>Dynamic load balancing to complement the work stealing mechanism</description></item>
        /// </list>
        /// </remarks>
        public static DataParallelismOptions ToDataParallelOptions() => new()
        {
            MaxDevices = null, // Use all available devices
            EnablePeerToPeer = false, // Work stealing manages its own transfers
            SyncStrategy = SynchronizationStrategy.LockFree,
            OverlapComputeAndCommunication = true,
            LoadBalancing = LoadBalancingStrategy.Dynamic
        };
    }
}
