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
        DirectCompute = ComputeBackendType.DirectCompute,

        /// <summary>
        /// ROCm backend for AMD GPUs.
        /// </summary>
        ROCm = ComputeBackendType.ROCm
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

        /// <summary>
        /// Parses a string to BackendType with fallback to CPU.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <returns>The corresponding BackendType, or CPU if parsing fails.</returns>
        public static BackendType ParseBackendType(string value) =>
            Enum.TryParse<BackendType>(value, true, out var result) ? result : BackendType.CPU;

        /// <summary>
        /// Tries to parse a string to BackendType.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <param name="result">The parsed BackendType if successful.</param>
        /// <returns>True if parsing was successful; otherwise, false.</returns>
        public static bool TryParseBackendType(string value, out BackendType result) =>
            Enum.TryParse<BackendType>(value, true, out result);

        /// <summary>
        /// Converts a string to BackendType for comparison operations.
        /// </summary>
        /// <param name="value">The string value to convert.</param>
        /// <returns>The corresponding BackendType.</returns>
        public static BackendType ToBackendType(string value) => ParseBackendType(value);

        /// <summary>
        /// Compares a string with a BackendType.
        /// </summary>
        /// <param name="stringValue">The string value to compare.</param>
        /// <param name="backendType">The BackendType to compare with.</param>
        /// <returns>True if they represent the same backend type.</returns>
        public static bool Equals(string stringValue, BackendType backendType) =>
            TryParseBackendType(stringValue, out var parsed) && parsed == backendType;

        /// <summary>
        /// Checks if a string value matches a BackendType.
        /// </summary>
        /// <param name="stringValue">The string value to check.</param>
        /// <param name="backendType">The BackendType to match against.</param>
        /// <returns>True if the string matches the BackendType.</returns>
        public static bool IsBackendType(string stringValue, BackendType backendType) =>
            Equals(stringValue, backendType);
    }
}