// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

/// <summary>
/// Defines memory consistency models for GPU memory operations.
/// </summary>
public enum MemoryConsistencyModel
{
    /// <summary>
    /// Relaxed consistency - no ordering guarantees (GPU default).
    /// Performance: 1.0x (baseline, no overhead).
    /// </summary>
    Relaxed = 0,

    /// <summary>
    /// Release-Acquire consistency - causal ordering between threads.
    /// Performance: 0.85x (15% overhead).
    /// Recommended for message passing and producer-consumer patterns.
    /// </summary>
    ReleaseAcquire = 1,

    /// <summary>
    /// Sequential consistency - total order across all threads.
    /// Performance: 0.60x (40% overhead).
    /// Use sparingly, only when total order is required.
    /// </summary>
    Sequential = 2
}
