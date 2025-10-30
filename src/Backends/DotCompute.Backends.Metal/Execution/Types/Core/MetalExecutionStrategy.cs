// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Defines the execution strategy for Metal operations
/// </summary>
public enum MetalExecutionStrategy
{
    /// <summary>
    /// Optimize for maximum throughput
    /// </summary>
    Throughput,

    /// <summary>
    /// Optimize for minimum latency
    /// </summary>
    Latency,

    /// <summary>
    /// Balance between throughput and latency
    /// </summary>
    Balanced,

    /// <summary>
    /// Optimize for power efficiency (Apple Silicon)
    /// </summary>
    PowerEfficient,

    /// <summary>
    /// Custom strategy based on runtime conditions
    /// </summary>
    Adaptive
}
