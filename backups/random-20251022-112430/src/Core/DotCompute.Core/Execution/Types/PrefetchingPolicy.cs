// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines prefetching policies for pipeline execution optimization.
    /// These policies control how aggressively data is prefetched to improve performance
    /// while balancing memory usage and computational overhead.
    /// </summary>
    public enum PrefetchingPolicy
    {
        /// <summary>
        /// Conservative prefetching to minimize memory usage.
        /// Prefetches only essential data with minimal lookahead.
        /// Prioritizes memory efficiency over performance gains.
        /// Suitable for memory-constrained environments.
        /// </summary>
        Conservative,

        /// <summary>
        /// Balanced prefetching for good performance and memory usage.
        /// Provides moderate prefetching that balances performance improvements
        /// with reasonable memory overhead. Recommended for most use cases
        /// where both performance and memory efficiency are important.
        /// </summary>
        Balanced,

        /// <summary>
        /// Aggressive prefetching for maximum performance.
        /// Prefetches data extensively with significant lookahead.
        /// Prioritizes performance over memory efficiency.
        /// Best suited for performance-critical applications with abundant memory.
        /// </summary>
        Aggressive
    }
}