// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Factories;

/// <summary>
/// Performance profile for accelerator configuration.
/// </summary>
public enum PerformanceProfile
{
    /// <summary>
    /// Balanced performance and power consumption.
    /// </summary>
    Balanced,

    /// <summary>
    /// Maximum performance, higher power consumption.
    /// </summary>
    MaxPerformance,

    /// <summary>
    /// Low latency optimization for real-time workloads.
    /// </summary>
    LowLatency,


    /// <summary>
    /// Power-efficient mode, reduced performance.
    /// </summary>
    PowerSaver
}
