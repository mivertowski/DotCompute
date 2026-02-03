// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

/// <summary>
/// Specifies the application domain for domain-specific optimizations.
/// </summary>
/// <remarks>
/// Domain hints enable the compiler to apply specialized optimizations
/// tailored to specific algorithm patterns and data structures.
/// </remarks>
public enum RingKernelDomain
{
    /// <summary>
    /// General-purpose ring kernel with no domain-specific optimizations.
    /// </summary>
    General = 0,

    /// <summary>
    /// Graph analytics domain using vertex-centric message passing patterns.
    /// Optimizations include sparse data structure handling and irregular workload balancing.
    /// </summary>
    GraphAnalytics = 1,

    /// <summary>
    /// Spatial simulation domain with stencil operations on regular grids.
    /// Optimizations include stencil pattern recognition and halo exchange protocols.
    /// </summary>
    SpatialSimulation = 2,

    /// <summary>
    /// Actor model domain with mailbox-based message passing.
    /// Optimizations include per-actor mailbox queue management and message-driven execution.
    /// </summary>
    ActorModel = 3
}
