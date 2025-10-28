// <copyright file="LoadBalancingStrategy.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines strategies for distributing computational workload across multiple devices.
    /// These strategies determine how work is allocated to achieve optimal performance and utilization.
    /// </summary>
    public enum LoadBalancingStrategy
    {
        /// <summary>
        /// Equal distribution across devices using round-robin allocation.
        /// Work is distributed evenly in a circular manner, regardless of device capabilities.
        /// Simple and predictable, but may not account for device performance differences.
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Distribution based on device capabilities and performance characteristics.
        /// Work allocation considers factors like memory, compute units, and historical performance.
        /// Provides better utilization when devices have significantly different capabilities.
        /// </summary>
        Weighted,

        /// <summary>
        /// Adaptive distribution based on real-time performance feedback.
        /// Continuously adjusts work distribution based on actual execution times and device status.
        /// Self-optimizing approach that adapts to changing conditions and workload characteristics.
        /// </summary>
        Adaptive,

        /// <summary>
        /// Dynamic load balancing with work stealing capabilities.
        /// Allows devices to redistribute work among themselves during execution.
        /// Provides the best load balancing for irregular workloads with runtime dependencies.
        /// </summary>
        Dynamic,

        /// <summary>
        /// Manual assignment by user or application logic.
        /// Work distribution is explicitly controlled by the application.
        /// Provides maximum control but requires detailed knowledge of workload and device characteristics.
        /// </summary>
        Manual
    }
}
