// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

/// <summary>
/// Specifies how a ring kernel processes messages from its input queue.
/// </summary>
/// <remarks>
/// <para>
/// Ring kernels can operate in different processing modes to optimize for either
/// latency or throughput depending on the workload characteristics.
/// </para>
/// <para>
/// <b>Continuous Mode</b> processes one message at a time with minimal latency overhead,
/// ideal for latency-sensitive applications like real-time actor systems.
/// </para>
/// <para>
/// <b>Batch Mode</b> processes multiple messages per iteration to maximize throughput,
/// suitable for high-volume message processing where latency is less critical.
/// </para>
/// <para>
/// <b>Adaptive Mode</b> dynamically switches between continuous and batch processing
/// based on queue depth, providing a balance between latency and throughput.
/// </para>
/// </remarks>
public enum RingProcessingMode
{
    /// <summary>
    /// Process one message per iteration for minimum latency.
    /// </summary>
    /// <remarks>
    /// In Continuous mode, the ring kernel processes a single message per dispatch
    /// loop iteration. This minimizes end-to-end message latency at the cost of
    /// lower peak throughput. Best for latency-critical applications like actor
    /// request-response patterns or real-time event processing.
    /// </remarks>
    Continuous = 0,

    /// <summary>
    /// Process multiple messages per iteration for maximum throughput.
    /// </summary>
    /// <remarks>
    /// In Batch mode, the ring kernel processes multiple messages (up to a configured
    /// batch size) per dispatch loop iteration. This maximizes message throughput by
    /// amortizing kernel dispatch overhead across multiple messages. Best for high-volume
    /// data processing where individual message latency is less important than overall
    /// throughput.
    /// </remarks>
    Batch = 1,

    /// <summary>
    /// Dynamically switch between Continuous and Batch based on queue depth.
    /// </summary>
    /// <remarks>
    /// In Adaptive mode, the ring kernel monitors its input queue depth and switches
    /// processing strategy dynamically:
    /// <list type="bullet">
    /// <item>Low queue depth (&lt; threshold): Process single messages (Continuous mode)</item>
    /// <item>High queue depth (≥ threshold): Process batches (Batch mode)</item>
    /// </list>
    /// This provides the best of both worlds - low latency when queue is shallow,
    /// high throughput when queue is deep. Recommended for most workloads with
    /// variable message arrival rates.
    /// </remarks>
    Adaptive = 2
}
