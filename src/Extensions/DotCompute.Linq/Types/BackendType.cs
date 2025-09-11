// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Compute.Enums;

namespace DotCompute.Linq.Types
{
    /// <summary>
    /// Type alias for ComputeBackendType to maintain compatibility with LINQ provider.
    /// </summary>
    /// <remarks>
    /// This alias provides a shortened name for use within the LINQ provider
    /// while maintaining compatibility with the core compute backend enumeration.
    /// </remarks>
    public enum BackendType
    {
        /// <summary>
        /// CPU backend using SIMD instructions.
        /// </summary>
        CPU = ComputeBackendType.CPU,

        /// <summary>
        /// CUDA backend for NVIDIA GPUs.
        /// </summary>
        CUDA = ComputeBackendType.CUDA,

        /// <summary>
        /// OpenCL backend for cross-platform computing.
        /// </summary>
        OpenCL = ComputeBackendType.OpenCL,

        /// <summary>
        /// Metal backend for Apple devices.
        /// </summary>
        Metal = ComputeBackendType.Metal,

        /// <summary>
        /// Vulkan Compute backend.
        /// </summary>
        Vulkan = ComputeBackendType.Vulkan,

        /// <summary>
        /// DirectCompute backend for Windows.
        /// </summary>
        DirectCompute = ComputeBackendType.DirectCompute
    }

    /// <summary>
    /// Extension methods for converting between BackendType and ComputeBackendType.
    /// </summary>
    public static class BackendTypeExtensions
    {
        /// <summary>
        /// Converts a BackendType to ComputeBackendType.
        /// </summary>
        /// <param name="backendType">The backend type to convert.</param>
        /// <returns>The corresponding ComputeBackendType.</returns>
        public static ComputeBackendType ToComputeBackendType(this BackendType backendType)
            => (ComputeBackendType)backendType;

        /// <summary>
        /// Converts a ComputeBackendType to BackendType.
        /// </summary>
        /// <param name="computeBackendType">The compute backend type to convert.</param>
        /// <returns>The corresponding BackendType.</returns>
        public static BackendType ToBackendType(this ComputeBackendType computeBackendType)
            => (BackendType)computeBackendType;
    }
}