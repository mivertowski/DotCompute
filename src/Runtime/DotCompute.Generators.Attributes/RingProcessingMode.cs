// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

/// <summary>
/// Specifies how a ring kernel processes messages from its input queue.
/// </summary>
public enum RingProcessingMode
{
    /// <summary>
    /// Process one message per iteration for minimum latency.
    /// </summary>
    Continuous = 0,

    /// <summary>
    /// Process multiple messages per iteration for maximum throughput.
    /// </summary>
    Batch = 1,

    /// <summary>
    /// Dynamically switch between Continuous and Batch based on queue depth.
    /// </summary>
    Adaptive = 2
}
