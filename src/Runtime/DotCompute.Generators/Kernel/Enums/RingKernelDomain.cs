// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

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
    /// <remarks>
    /// Use this when the kernel doesn't fit into specific categories or
    /// when you want to control optimizations manually.
    /// </remarks>
    General = 0,

    /// <summary>
    /// Graph analytics domain using vertex-centric message passing patterns.
    /// </summary>
    /// <remarks>
    /// Optimizations include:
    /// - Sparse data structure handling (adjacency lists)
    /// - Irregular workload load balancing
    /// - Vertex-centric computation patterns (Pregel model)
    /// - Graph traversal optimizations
    ///
    /// Examples: PageRank, BFS, shortest paths, connected components.
    /// </remarks>
    GraphAnalytics = 1,

    /// <summary>
    /// Spatial simulation domain with stencil operations on regular grids.
    /// </summary>
    /// <remarks>
    /// Optimizations include:
    /// - Stencil pattern recognition and optimization
    /// - Halo exchange protocols for boundary data
    /// - Shared memory usage for spatial locality
    /// - Regular grid memory layout optimization
    ///
    /// Examples: Wave propagation, heat diffusion, fluid dynamics, cellular automata.
    /// </remarks>
    SpatialSimulation = 2,

    /// <summary>
    /// Actor model domain with mailbox-based message passing.
    /// </summary>
    /// <remarks>
    /// Optimizations include:
    /// - Per-actor mailbox queue management
    /// - Message-driven execution patterns
    /// - Actor lifecycle management (creation, supervision, termination)
    /// - Priority-based message handling
    ///
    /// Examples: GPU-native actors, particle systems with interactions, agent simulations.
    /// </remarks>
    ActorModel = 3
}
