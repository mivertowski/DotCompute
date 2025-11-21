// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.Barriers;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.RingKernels;
using System;

namespace DotCompute.Samples.RingKernels;

/// <summary>
/// Sample Ring Kernels demonstrating Orleans.GpuBridge.Core integration features.
/// These samples showcase timestamp tracking, processing modes, memory ordering,
/// barriers, and fairness control introduced in DotCompute v0.5.0-alpha.
/// </summary>
public static class AdvancedRingKernelSamples
{
    /// <summary>
    /// Sample 1: High-throughput batch processing with GPU timestamps.
    /// Optimized for maximum throughput with batch message processing and hardware timestamps.
    /// </summary>
    [RingKernel(
        KernelId = "HighThroughputProcessor",
        Capacity = 4096,
        InputQueueSize = 512,
        OutputQueueSize = 512,
        MaxInputMessageSizeBytes = 65792,
        MaxOutputMessageSizeBytes = 65792,
        ProcessingMode = RingProcessingMode.Batch,
        MaxMessagesPerIteration = 16,
        EnableTimestamps = true,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessBatchWithTimestamps(ReadOnlySpan<float> input, Span<float> output)
    {
        // Batch processing: handles up to 16 messages per iteration for high throughput
        // GPU timestamps track kernel execution time via clock64()
        int idx = 0; // Ring Kernel processes messages via input/output queues
        if (idx < output.Length)
        {
            output[idx] = input[idx] * 2.0f;
        }
    }

    /// <summary>
    /// Sample 2: Low-latency continuous processing for real-time systems.
    /// Single message per iteration for minimum latency with release-acquire memory ordering.
    /// </summary>
    [RingKernel(
        KernelId = "LowLatencyRealtime",
        Capacity = 1024,
        MessageQueueSize = 256, // Unified queue size (overrides Input/OutputQueueSize)
        MaxInputMessageSizeBytes = 32768,
        MaxOutputMessageSizeBytes = 32768,
        ProcessingMode = RingProcessingMode.Continuous,
        MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
        EnableCausalOrdering = true,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessContinuousLowLatency(ReadOnlySpan<double> input, Span<double> output)
    {
        // Continuous mode: processes one message per iteration for minimum latency
        // Release-acquire ensures message writes visible before queue updates
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] + 1.0;
        }
    }

    /// <summary>
    /// Sample 3: Adaptive processing with dynamic batch sizing.
    /// Automatically switches between batch and continuous based on queue depth.
    /// </summary>
    [RingKernel(
        KernelId = "AdaptiveProcessor",
        Capacity = 2048,
        InputQueueSize = 512,
        OutputQueueSize = 512,
        MaxInputMessageSizeBytes = 65792,
        MaxOutputMessageSizeBytes = 65792,
        ProcessingMode = RingProcessingMode.Adaptive,
        MaxMessagesPerIteration = 32, // Fairness: max 32 messages per iteration
        EnableTimestamps = true,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessAdaptive(ReadOnlySpan<int> input, Span<int> output)
    {
        // Adaptive mode: batch size = (queue_depth > 10) ? 16 : 1
        // Automatically balances latency vs throughput based on load
        // Fairness control prevents actor starvation in multi-actor systems
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] * 3;
        }
    }

    /// <summary>
    /// Sample 4: Thread-block barrier synchronization for coordinated processing.
    /// Uses __syncthreads() for block-level synchronization between threads.
    /// </summary>
    [RingKernel(
        KernelId = "BarrierCoordinated",
        Capacity = 1024,
        InputQueueSize = 256,
        OutputQueueSize = 256,
        MaxInputMessageSizeBytes = 65792,
        MaxOutputMessageSizeBytes = 65792,
        UseBarriers = true,
        BarrierScope = BarrierScope.ThreadBlock,
        MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessWithBarrier(ReadOnlySpan<float> input, Span<float> output)
    {
        // Block-level barrier: ensures all threads in block synchronize
        // Useful for shared memory operations and multi-stage algorithms
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] * 4.0f;
        }
    }

    /// <summary>
    /// Sample 5: Grid-wide barrier for cooperative launch patterns.
    /// Requires cooperative kernel launch for grid.sync() operations.
    /// </summary>
    [RingKernel(
        KernelId = "GridWideBarrier",
        Capacity = 8192,
        InputQueueSize = 1024,
        OutputQueueSize = 1024,
        MaxInputMessageSizeBytes = 65792,
        MaxOutputMessageSizeBytes = 65792,
        UseBarriers = true,
        BarrierScope = BarrierScope.Grid,
        MemoryConsistency = MemoryConsistencyModel.Sequential,
        EnableTimestamps = true,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessGridWide(ReadOnlySpan<double> input, Span<double> output)
    {
        // Grid-wide barrier: synchronizes all threads across all blocks
        // Sequential consistency: full memory barriers for total order visibility
        // Use for algorithms requiring global synchronization points
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] / 2.0;
        }
    }

    /// <summary>
    /// Sample 6: Warp-level synchronization for SIMD operations.
    /// Uses __syncwarp() for 32-thread warp synchronization.
    /// </summary>
    [RingKernel(
        KernelId = "WarpLevelSync",
        Capacity = 512,
        MessageQueueSize = 128, // Unified queue size
        MaxInputMessageSizeBytes = 32768,
        MaxOutputMessageSizeBytes = 32768,
        UseBarriers = true,
        BarrierScope = BarrierScope.Warp,
        MemoryConsistency = MemoryConsistencyModel.Relaxed,
        ProcessingMode = RingProcessingMode.Batch,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessWarpSync(ReadOnlySpan<int> input, Span<int> output)
    {
        // Warp-level barrier: synchronizes 32 threads (one warp)
        // Relaxed memory model for maximum performance (manual fencing required)
        // Ideal for SIMD operations within a warp
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] << 1;
        }
    }

    /// <summary>
    /// Sample 7: Actor system with fairness control and causal ordering.
    /// Prevents actor starvation with iteration limiting and release-acquire semantics.
    /// </summary>
    [RingKernel(
        KernelId = "ActorSystemFair",
        Capacity = 2048,
        InputQueueSize = 512,
        OutputQueueSize = 512,
        MaxInputMessageSizeBytes = 65792,
        MaxOutputMessageSizeBytes = 65792,
        ProcessingMode = RingProcessingMode.Adaptive,
        MaxMessagesPerIteration = 8, // Fairness: limit to 8 messages per actor
        MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
        EnableCausalOrdering = true,
        EnableTimestamps = true,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessActorFair(ReadOnlySpan<float> input, Span<float> output)
    {
        // Fairness control: prevents single actor from monopolizing resources
        // Causal ordering: ensures message visibility follows happens-before relation
        // Adaptive mode: balances latency and throughput dynamically
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] + input[idx];
        }
    }

    /// <summary>
    /// Sample 8: High-performance streaming with sequential consistency.
    /// Maximum correctness guarantees for complex distributed algorithms.
    /// </summary>
    [RingKernel(
        KernelId = "SequentialStreaming",
        Capacity = 4096,
        MessageQueueSize = 1024, // Large unified queue for streaming
        MaxInputMessageSizeBytes = 131072, // 128KB messages
        MaxOutputMessageSizeBytes = 131072,
        ProcessingMode = RingProcessingMode.Batch,
        MaxMessagesPerIteration = 64,
        MemoryConsistency = MemoryConsistencyModel.Sequential,
        UseBarriers = true,
        BarrierScope = BarrierScope.ThreadBlock,
        EnableTimestamps = true,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessSequentialStream(ReadOnlySpan<double> input, Span<double> output)
    {
        // Sequential consistency: strongest ordering guarantees
        // All threads observe operations in the same global order
        // Trade-off: ~40% performance overhead for total order visibility
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = Math.Sqrt(input[idx]);
        }
    }

    /// <summary>
    /// Sample 9: Minimal latency configuration for real-time event processing.
    /// Optimized for sub-millisecond response times with continuous processing.
    /// </summary>
    [RingKernel(
        KernelId = "UltraLowLatency",
        Capacity = 512,
        MessageQueueSize = 64, // Small queue for minimal buffering
        MaxInputMessageSizeBytes = 16384, // 16KB messages
        MaxOutputMessageSizeBytes = 16384,
        ProcessingMode = RingProcessingMode.Continuous, // Single message = minimal latency
        MemoryConsistency = MemoryConsistencyModel.Relaxed, // No memory fences = maximum speed
        EnableTimestamps = true, // Track latency with GPU timestamps
        Backends = [KernelBackend.CUDA])]
    public static void ProcessUltraLowLatency(ReadOnlySpan<float> input, Span<float> output)
    {
        // Ultra-low latency: single message + relaxed memory + small queues
        // GPU timestamps track end-to-end latency via clock64()
        // Ideal for high-frequency trading, robotics, gaming
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] * 0.5f;
        }
    }

    /// <summary>
    /// Sample 10: Multi-stage pipeline with all features enabled.
    /// Comprehensive demonstration of Orleans.GpuBridge.Core integration.
    /// </summary>
    [RingKernel(
        KernelId = "ComprehensivePipeline",
        Capacity = 8192,
        InputQueueSize = 2048,
        OutputQueueSize = 2048,
        MaxInputMessageSizeBytes = 65792,
        MaxOutputMessageSizeBytes = 65792,
        ProcessingMode = RingProcessingMode.Adaptive,
        MaxMessagesPerIteration = 32,
        UseBarriers = true,
        BarrierScope = BarrierScope.ThreadBlock,
        MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
        EnableCausalOrdering = true,
        EnableTimestamps = true,
        Backends = [KernelBackend.CUDA])]
    public static void ProcessComprehensive(ReadOnlySpan<double> input, Span<double> output)
    {
        // Comprehensive configuration:
        // - Adaptive processing: dynamic batch sizing based on load
        // - Fairness control: max 32 messages per iteration
        // - Thread-block barriers: coordinated multi-stage processing
        // - Release-acquire + causal ordering: message passing semantics
        // - GPU timestamps: performance telemetry with clock64()
        int idx = 0;
        if (idx < output.Length)
        {
            output[idx] = input[idx] * input[idx];
        }
    }
}
