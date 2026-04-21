// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Globalization;

namespace DotCompute.Backends.CUDA.Hopper
{
    /// <summary>
    /// Configuration for Distributed Shared Memory (DSMEM) across a Hopper Thread Block Cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each block in a cluster contributes a portion of its shared memory to the cluster's DSMEM
    /// pool. Blocks can access peer shared memory via <c>cluster.map_shared_rank()</c>, enabling
    /// intra-cluster K2K messaging without round-tripping through global memory.
    /// </para>
    /// <para>
    /// Mirrors RustCompute's <c>dsmem.rs</c> with the simplified shape requested by the .NET layer:
    /// a single "per-block bytes" knob and a cluster block count.
    /// </para>
    /// </remarks>
    public readonly record struct DsmemConfig
    {
        /// <summary>Bytes of shared memory reserved per block for DSMEM. Must be positive.</summary>
        public uint PerBlockBytes { get; init; }

        /// <summary>Number of blocks per cluster. Must be in [1, 8] (Hopper portable cluster size).</summary>
        public uint ClusterBlockCount { get; init; }

        /// <summary>Hopper's default opt-in shared memory per block in bytes (228 KiB).</summary>
        public const uint HopperMaxSharedMemoryPerBlock = 228 * 1024;

        /// <summary>
        /// Total bytes of DSMEM across the cluster, computed as <c>PerBlockBytes * ClusterBlockCount</c>.
        /// </summary>
        public uint TotalDsmemBytes => PerBlockBytes * ClusterBlockCount;

        /// <summary>
        /// Constructs a DSMEM configuration.
        /// </summary>
        /// <param name="perBlockBytes">Shared memory bytes reserved per block for DSMEM.</param>
        /// <param name="clusterBlockCount">Number of blocks per cluster.</param>
        /// <returns>A DSMEM configuration.</returns>
        public static DsmemConfig Create(uint perBlockBytes, uint clusterBlockCount)
            => new()
            {
                PerBlockBytes = perBlockBytes,
                ClusterBlockCount = clusterBlockCount,
            };

        /// <summary>
        /// Validates the DSMEM configuration against the device's maximum shared memory per block.
        /// </summary>
        /// <param name="maxSharedMemoryPerBlock">
        /// Device limit: <c>cudaDevAttrMaxSharedMemoryPerBlockOptin</c> (bytes). Defaults to Hopper's
        /// 228 KiB if not supplied — callers should pass the real device attribute when available.
        /// </param>
        /// <param name="maxTotalDsmemBytes">
        /// Optional cap on the total DSMEM footprint across the cluster. <c>0</c> disables the check.
        /// </param>
        /// <exception cref="ArgumentException">One or more invariants are violated.</exception>
        public void Validate(uint maxSharedMemoryPerBlock = HopperMaxSharedMemoryPerBlock, ulong maxTotalDsmemBytes = 0)
        {
            if (PerBlockBytes == 0)
            {
                throw new ArgumentException("PerBlockBytes must be positive.");
            }

            if (ClusterBlockCount == 0)
            {
                throw new ArgumentException("ClusterBlockCount must be positive.");
            }

            if (ClusterBlockCount > HopperFeatures.MaxPortableClusterSize)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "ClusterBlockCount ({0}) exceeds the portable Hopper maximum ({1}).",
                        ClusterBlockCount, HopperFeatures.MaxPortableClusterSize));
            }

            if (maxSharedMemoryPerBlock > 0 && PerBlockBytes > maxSharedMemoryPerBlock)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "PerBlockBytes ({0}) exceeds the device's MaxSharedMemoryPerBlockOptin ({1}).",
                        PerBlockBytes, maxSharedMemoryPerBlock));
            }

            if (maxTotalDsmemBytes > 0)
            {
                var total = (ulong)TotalDsmemBytes;
                if (total > maxTotalDsmemBytes)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture,
                            "TotalDsmemBytes ({0}) exceeds the configured cluster DSMEM cap ({1}).",
                            total, maxTotalDsmemBytes));
                }
            }
        }
    }
}
