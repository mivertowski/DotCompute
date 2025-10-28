// <copyright file="StealingStrategy.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines strategies for work stealing in dynamic load balancing scenarios.
    /// These strategies determine how idle devices select victim devices to steal work from.
    /// </summary>
    public enum StealingStrategy
    {
        /// <summary>
        /// Steal from a randomly selected victim device.
        /// Simple strategy that distributes stealing attempts uniformly across all devices.
        /// Provides good load distribution with minimal overhead for victim selection.
        /// </summary>
        RandomVictim,

        /// <summary>
        /// Steal from the device with the largest work queue (richest victim).
        /// Targets devices with the most available work to maximize the benefit of stealing.
        /// Optimal for scenarios where work queue sizes vary significantly between devices.
        /// </summary>
        RichestVictim,

        /// <summary>
        /// Steal from the nearest victim device (NUMA-aware).
        /// Considers physical proximity and memory hierarchy to minimize data transfer costs.
        /// Best for systems with non-uniform memory access where locality matters for performance.
        /// </summary>
        NearestVictim,

        /// <summary>
        /// Hierarchical stealing strategy based on system topology.
        /// Uses a tree-like approach where devices first try to steal from nearby devices,
        /// then expand the search radius if no work is available locally.
        /// Balances locality with load balancing effectiveness.
        /// </summary>
        Hierarchical
    }
}
