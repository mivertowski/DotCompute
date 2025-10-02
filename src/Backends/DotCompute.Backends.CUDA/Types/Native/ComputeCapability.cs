// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Native
{
    /// <summary>
    /// A class that represents compute capability.
    /// </summary>
    public static class ComputeCapability
    {
        /// <summary>
        /// Gets the arch string.
        /// </summary>
        /// <param name="major">The major.</param>
        /// <param name="minor">The minor.</param>
        /// <returns>The arch string.</returns>
        public static string GetArchString(int major, int minor) => $"compute_{major}{minor}";
        /// <summary>
        /// Gets the code string.
        /// </summary>
        /// <param name="major">The major.</param>
        /// <param name="minor">The minor.</param>
        /// <returns>The code string.</returns>

        public static string GetCodeString(int major, int minor) => $"sm_{major}{minor}";
        /// <summary>
        /// Gets parse from device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <returns>The result of the operation.</returns>

        public static (int major, int minor) ParseFromDevice(int deviceId)
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            CudaRuntime.CheckError(result, "getting device properties");
            return (props.Major, props.Minor);
        }
        /// <summary>
        /// A class that represents known capabilities.
        /// </summary>

        // Common compute capabilities
#pragma warning disable CA1724 // Type names should not match namespaces - Common is a descriptive nested class name in this context
#pragma warning disable CA1034 // Nested types should not be visible - Common is appropriately nested within ComputeCapability
        public static class KnownCapabilities
        {
            public static readonly (int major, int minor) Kepler = (3, 5);
            public static readonly (int major, int minor) Maxwell = (5, 0);
            public static readonly (int major, int minor) Pascal = (6, 0);
            public static readonly (int major, int minor) Volta = (7, 0);
            public static readonly (int major, int minor) Turing = (7, 5);
            public static readonly (int major, int minor) Ampere = (8, 0);
            public static readonly (int major, int minor) Ada = (8, 9);
            public static readonly (int major, int minor) Hopper = (9, 0);
        }
#pragma warning restore CA1034
#pragma warning restore CA1724
    }
}