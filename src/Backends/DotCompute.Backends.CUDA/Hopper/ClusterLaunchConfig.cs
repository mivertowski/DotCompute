// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Globalization;

namespace DotCompute.Backends.CUDA.Hopper
{
    /// <summary>
    /// Launch configuration for a Hopper (sm_90+) Thread Block Cluster kernel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Thread Block Clusters group multiple thread blocks that can share shared memory
    /// (DSMEM) and synchronize at a granularity tighter than a grid. This struct captures
    /// the dimensions required by <c>cuLaunchKernelEx</c> together with the cluster dimension
    /// supplied as a <c>CU_LAUNCH_ATTRIBUTE_CLUSTER_DIMENSION</c> attribute.
    /// </para>
    /// <para>
    /// <see cref="Validate"/> enforces the portability constraints inherited from
    /// RustCompute's <c>cluster.rs</c>: blocks-per-cluster ≤ 8, power-of-two cluster size,
    /// and grid dimensions evenly divisible by cluster dimensions.
    /// </para>
    /// </remarks>
    public readonly record struct ClusterLaunchConfig
    {
        /// <summary>Cluster dimension X (blocks per cluster in X). Must be a positive power of 2.</summary>
        public uint ClusterDimX { get; init; }

        /// <summary>Cluster dimension Y (blocks per cluster in Y). Must be a positive power of 2.</summary>
        public uint ClusterDimY { get; init; }

        /// <summary>Cluster dimension Z (blocks per cluster in Z). Must be a positive power of 2.</summary>
        public uint ClusterDimZ { get; init; }

        /// <summary>Grid dimension X. Must be divisible by <see cref="ClusterDimX"/>.</summary>
        public uint GridDimX { get; init; }

        /// <summary>Grid dimension Y. Must be divisible by <see cref="ClusterDimY"/>.</summary>
        public uint GridDimY { get; init; }

        /// <summary>Grid dimension Z. Must be divisible by <see cref="ClusterDimZ"/>.</summary>
        public uint GridDimZ { get; init; }

        /// <summary>Block dimension X (threads per block in X). Must be positive.</summary>
        public uint BlockDimX { get; init; }

        /// <summary>Block dimension Y (threads per block in Y). Must be positive.</summary>
        public uint BlockDimY { get; init; }

        /// <summary>Block dimension Z (threads per block in Z). Must be positive.</summary>
        public uint BlockDimZ { get; init; }

        /// <summary>Dynamic shared memory per block in bytes.</summary>
        public uint SharedMemBytes { get; init; }

        /// <summary>Stream handle (<c>CUstream</c>) to launch on. <see cref="IntPtr.Zero"/> selects the default stream.</summary>
        public IntPtr Stream { get; init; }

        /// <summary>
        /// Creates a <see cref="ClusterLaunchConfig"/> from three-tuples for grid, block and cluster dimensions.
        /// </summary>
        /// <param name="grid">Grid dimensions (X, Y, Z).</param>
        /// <param name="block">Block dimensions (X, Y, Z).</param>
        /// <param name="cluster">Cluster dimensions (X, Y, Z) in blocks.</param>
        /// <param name="sharedMemBytes">Dynamic shared memory per block in bytes.</param>
        /// <param name="stream">Optional stream handle. Defaults to the null/default stream.</param>
        /// <returns>A configured <see cref="ClusterLaunchConfig"/>.</returns>
        public static ClusterLaunchConfig Create(
            (uint X, uint Y, uint Z) grid,
            (uint X, uint Y, uint Z) block,
            (uint X, uint Y, uint Z) cluster,
            uint sharedMemBytes = 0,
            IntPtr stream = default)
            => new()
            {
                GridDimX = grid.X,
                GridDimY = grid.Y,
                GridDimZ = grid.Z,
                BlockDimX = block.X,
                BlockDimY = block.Y,
                BlockDimZ = block.Z,
                ClusterDimX = cluster.X,
                ClusterDimY = cluster.Y,
                ClusterDimZ = cluster.Z,
                SharedMemBytes = sharedMemBytes,
                Stream = stream,
            };

        /// <summary>Total number of blocks per cluster (X * Y * Z).</summary>
        public uint BlocksPerCluster => ClusterDimX * ClusterDimY * ClusterDimZ;

        /// <summary>
        /// Total number of clusters in the grid, computed as <c>grid / cluster</c> in each dimension.
        /// </summary>
        public uint ClusterCount
        {
            get
            {
                var cx = ClusterDimX == 0 ? 1u : ClusterDimX;
                var cy = ClusterDimY == 0 ? 1u : ClusterDimY;
                var cz = ClusterDimZ == 0 ? 1u : ClusterDimZ;
                return (GridDimX / cx) * (GridDimY / cy) * (GridDimZ / cz);
            }
        }

        /// <summary>
        /// Validates the configuration against portability rules. Throws <see cref="ArgumentException"/>
        /// on the first violation with a message describing the failing invariant.
        /// </summary>
        /// <exception cref="ArgumentException">One or more invariants are violated.</exception>
        public void Validate()
        {
            // Block dimensions must be positive.
            if (BlockDimX == 0 || BlockDimY == 0 || BlockDimZ == 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Block dimensions must be positive (got X={0}, Y={1}, Z={2}).",
                        BlockDimX, BlockDimY, BlockDimZ));
            }

            // Cluster dimensions must be positive.
            if (ClusterDimX == 0 || ClusterDimY == 0 || ClusterDimZ == 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Cluster dimensions must be positive (got X={0}, Y={1}, Z={2}). Use (1,1,1) to disable clustering.",
                        ClusterDimX, ClusterDimY, ClusterDimZ));
            }

            // Grid dimensions must be positive.
            if (GridDimX == 0 || GridDimY == 0 || GridDimZ == 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Grid dimensions must be positive (got X={0}, Y={1}, Z={2}).",
                        GridDimX, GridDimY, GridDimZ));
            }

            // Each cluster dimension must be a power of 2 so that the hardware scheduler can pack clusters.
            if (!IsPowerOfTwo(ClusterDimX))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "ClusterDimX ({0}) must be a power of 2.", ClusterDimX));
            }

            if (!IsPowerOfTwo(ClusterDimY))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "ClusterDimY ({0}) must be a power of 2.", ClusterDimY));
            }

            if (!IsPowerOfTwo(ClusterDimZ))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "ClusterDimZ ({0}) must be a power of 2.", ClusterDimZ));
            }

            // Portable cluster size cap: 8 blocks per cluster (Hopper). Blackwell lifts this to 16 but
            // we follow the portable Hopper contract here so kernels remain architecture-independent.
            var blocksPerCluster = BlocksPerCluster;
            if (blocksPerCluster > HopperFeatures.MaxPortableClusterSize)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "BlocksPerCluster ({0}) exceeds the portable Hopper maximum ({1}). "
                        + "Reduce cluster dimensions or gate on Blackwell (max {2}).",
                        blocksPerCluster,
                        HopperFeatures.MaxPortableClusterSize,
                        HopperFeatures.MaxBlackwellClusterSize));
            }

            // Grid must tile evenly with clusters — partial clusters are not permitted.
            if (GridDimX % ClusterDimX != 0 || GridDimY % ClusterDimY != 0 || GridDimZ % ClusterDimZ != 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Grid dimensions ({0},{1},{2}) must be divisible by cluster dimensions ({3},{4},{5}).",
                        GridDimX, GridDimY, GridDimZ, ClusterDimX, ClusterDimY, ClusterDimZ));
            }
        }

        private static bool IsPowerOfTwo(uint value) => value > 0 && (value & (value - 1)) == 0;
    }
}
