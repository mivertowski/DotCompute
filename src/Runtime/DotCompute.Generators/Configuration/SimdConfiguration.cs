// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration
{
    /// <summary>
    /// Configuration constants for SIMD operations and vectorization optimizations.
    /// </summary>
    /// <remarks>
    /// These constants define thresholds and sizes used to determine when to use
    /// different SIMD instruction sets and how to optimize vector operations.
    /// The values are based on empirical performance testing and hardware characteristics.
    /// </remarks>
    public static class SimdConfiguration
    {
        #region Array Size Thresholds
        
        /// <summary>
        /// Minimum array size for SIMD optimization to be beneficial.
        /// </summary>
        /// <remarks>
        /// Below this threshold, the overhead of setting up SIMD operations
        /// outweighs the performance benefits. Scalar operations are more
        /// efficient for small arrays due to better cache utilization and
        /// reduced instruction overhead.
        /// </remarks>
        public const int SmallArrayThreshold = 32;
        
        /// <summary>
        /// Minimum size for vectorized operations to provide performance benefits.
        /// </summary>
        /// <remarks>
        /// This threshold ensures that there are enough elements to process
        /// at least a few full vectors, making the vectorization worthwhile.
        /// </remarks>
        public const int MinimumVectorSize = 32;
        
        /// <summary>
        /// Minimum size for AVX-512 operations to be beneficial over AVX2.
        /// </summary>
        /// <remarks>
        /// AVX-512 has higher setup costs and power consumption, so it's only
        /// beneficial for larger data sets where the wider vectors can be fully utilized.
        /// </remarks>
        public const int MinimumAvx512Size = 64;
        
        /// <summary>
        /// Default chunk size for parallel processing distribution.
        /// </summary>
        /// <remarks>
        /// This size balances work distribution across CPU cores while maintaining
        /// good cache locality. Smaller chunks increase overhead, while larger chunks
        /// may lead to load imbalance.
        /// </remarks>
        public const int DefaultParallelChunkSize = 1024;
        
        #endregion
        
        #region Vector Sizes
        
        /// <summary>
        /// Number of 32-bit float elements in an AVX2 vector (256-bit / 32-bit).
        /// </summary>
        /// <remarks>
        /// AVX2 provides 256-bit wide vectors, which can hold 8 single-precision
        /// floating-point values or 4 double-precision values.
        /// </remarks>
        public const int Avx2FloatVectorSize = 8;
        
        /// <summary>
        /// Number of 64-bit double elements in an AVX2 vector (256-bit / 64-bit).
        /// </summary>
        public const int Avx2DoubleVectorSize = 4;
        
        /// <summary>
        /// Number of 32-bit integer elements in an AVX2 vector (256-bit / 32-bit).
        /// </summary>
        public const int Avx2Int32VectorSize = 8;
        
        /// <summary>
        /// Number of 32-bit float elements in an AVX-512 vector (512-bit / 32-bit).
        /// </summary>
        /// <remarks>
        /// AVX-512 doubles the vector width of AVX2, providing 512-bit wide vectors
        /// for maximum throughput on supported processors.
        /// </remarks>
        public const int Avx512FloatVectorSize = 16;
        
        /// <summary>
        /// Number of 64-bit double elements in an AVX-512 vector (512-bit / 64-bit).
        /// </summary>
        public const int Avx512DoubleVectorSize = 8;
        
        /// <summary>
        /// Number of 32-bit integer elements in an AVX-512 vector (512-bit / 32-bit).
        /// </summary>
        public const int Avx512Int32VectorSize = 16;
        
        #endregion
        
        #region Loop Unrolling Factors
        
        /// <summary>
        /// Default unrolling factor for vectorized loops.
        /// </summary>
        /// <remarks>
        /// Unrolling by 4 allows processing of 4 vectors per iteration,
        /// which helps hide memory latency and improves instruction-level parallelism.
        /// </remarks>
        public const int DefaultUnrollFactor = 4;
        
        /// <summary>
        /// Maximum unrolling factor to prevent code bloat.
        /// </summary>
        /// <remarks>
        /// Excessive unrolling can lead to instruction cache misses and
        /// register pressure, degrading performance.
        /// </remarks>
        public const int MaxUnrollFactor = 8;
        
        #endregion
        
        #region Performance Tuning
        
        /// <summary>
        /// Prefetch distance in cache lines for memory streaming operations.
        /// </summary>
        /// <remarks>
        /// Prefetching data ahead of time helps hide memory latency by ensuring
        /// data is in cache when needed. The distance is measured in cache lines (64 bytes).
        /// </remarks>
        public const int PrefetchDistance = 8;
        
        /// <summary>
        /// Alignment requirement in bytes for optimal SIMD performance.
        /// </summary>
        /// <remarks>
        /// Aligned memory access is faster on most architectures. AVX requires
        /// 32-byte alignment, while AVX-512 benefits from 64-byte alignment.
        /// </remarks>
        public const int OptimalAlignment = 64;
        
        #endregion
    }
}