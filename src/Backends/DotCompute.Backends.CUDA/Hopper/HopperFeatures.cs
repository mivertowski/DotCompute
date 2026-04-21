// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Hopper
{
    /// <summary>
    /// Capability gate for Hopper (H100/H200 — sm_90) features.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exposes compile-time/runtime predicates for each feature that was introduced
    /// by the Hopper architecture (Thread Block Clusters, Tensor Memory Accelerator,
    /// Distributed Shared Memory) plus the CUDA 11.2+ async memory pool APIs that are
    /// commonly paired with Hopper-class persistent kernels.
    /// </para>
    /// <para>
    /// Paired <c>EnsureSupported</c> variants throw <see cref="NotSupportedException"/> with
    /// a descriptive message so callers can fail fast on older architectures.
    /// </para>
    /// </remarks>
    public static class HopperFeatures
    {
        /// <summary>The compute-capability major version that introduced Hopper features (9.0).</summary>
        public const int HopperMajor = 9;

        /// <summary>The compute-capability major version that introduced async memory pools (6.0 — Pascal).</summary>
        public const int AsyncMemPoolMajor = 6;

        /// <summary>
        /// Maximum portable cluster size (blocks per cluster) on Hopper — matches RustCompute's
        /// <c>MAX_PORTABLE_CLUSTER_SIZE</c>.
        /// </summary>
        public const int MaxPortableClusterSize = 8;

        /// <summary>
        /// Maximum cluster size on Blackwell (B200+) — matches RustCompute's
        /// <c>MAX_BLACKWELL_CLUSTER_SIZE</c>.
        /// </summary>
        public const int MaxBlackwellClusterSize = 16;

        /// <summary>
        /// Returns <c>true</c> when the device supports Thread Block Cluster launch
        /// (compute capability 9.0 / Hopper or newer).
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <returns><c>true</c> if cluster launch is supported; otherwise <c>false</c>.</returns>
        public static bool IsClusterLaunchSupported(int major, int minor)
            => major >= HopperMajor;

        /// <summary>
        /// Returns <c>true</c> when the device supports the Tensor Memory Accelerator
        /// (<c>cp.async.bulk</c> family of instructions — Hopper and newer).
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <returns><c>true</c> if TMA is supported; otherwise <c>false</c>.</returns>
        public static bool IsTmaSupported(int major, int minor)
            => major >= HopperMajor;

        /// <summary>
        /// Returns <c>true</c> when the device supports Distributed Shared Memory across a
        /// Thread Block Cluster (Hopper and newer).
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <returns><c>true</c> if DSMEM is supported; otherwise <c>false</c>.</returns>
        public static bool IsDsmemSupported(int major, int minor)
            => major >= HopperMajor;

        /// <summary>
        /// Returns <c>true</c> when the device supports the stream-ordered memory pool
        /// APIs (<c>cuMemAllocAsync</c>/<c>cuMemFreeAsync</c>). Memory pools became standard
        /// on Pascal (CC 6.0) and later; the async entry points are supported from CUDA 11.2
        /// upward on any such device.
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <returns><c>true</c> if async memory pool allocation is supported; otherwise <c>false</c>.</returns>
        public static bool IsAsyncMemPoolSupported(int major, int minor)
            => major >= AsyncMemPoolMajor;

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> if cluster launch is not supported on the device.
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <exception cref="NotSupportedException">Compute capability is below 9.0.</exception>
        public static void EnsureClusterLaunchSupported(int major, int minor)
        {
            if (!IsClusterLaunchSupported(major, minor))
            {
                throw new NotSupportedException(
                    $"Thread Block Cluster launch requires compute capability {HopperMajor}.0+ (Hopper). "
                    + $"Device reports CC {major}.{minor}.");
            }
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> if TMA is not supported on the device.
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <exception cref="NotSupportedException">Compute capability is below 9.0.</exception>
        public static void EnsureTmaSupported(int major, int minor)
        {
            if (!IsTmaSupported(major, minor))
            {
                throw new NotSupportedException(
                    $"Tensor Memory Accelerator (cp.async.bulk) requires compute capability {HopperMajor}.0+ (Hopper). "
                    + $"Device reports CC {major}.{minor}.");
            }
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> if DSMEM is not supported on the device.
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <exception cref="NotSupportedException">Compute capability is below 9.0.</exception>
        public static void EnsureDsmemSupported(int major, int minor)
        {
            if (!IsDsmemSupported(major, minor))
            {
                throw new NotSupportedException(
                    $"Distributed Shared Memory (DSMEM) requires compute capability {HopperMajor}.0+ (Hopper). "
                    + $"Device reports CC {major}.{minor}.");
            }
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> if async memory pool allocation is not supported.
        /// </summary>
        /// <param name="major">Compute capability major version.</param>
        /// <param name="minor">Compute capability minor version.</param>
        /// <exception cref="NotSupportedException">Compute capability is below 6.0.</exception>
        public static void EnsureAsyncMemPoolSupported(int major, int minor)
        {
            if (!IsAsyncMemPoolSupported(major, minor))
            {
                throw new NotSupportedException(
                    $"Stream-ordered memory pool allocation requires compute capability {AsyncMemPoolMajor}.0+. "
                    + $"Device reports CC {major}.{minor}.");
            }
        }
    }
}
