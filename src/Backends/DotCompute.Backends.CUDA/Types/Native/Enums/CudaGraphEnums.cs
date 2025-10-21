// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda graph exec update result enumeration.
    /// </summary>
    /// <summary>
    /// CUDA graph execution update result enumeration
    /// </summary>
    public enum CudaGraphExecUpdateResult : uint
    {
        /// <summary>
        /// Graph update completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Graph update failed with an error.
        /// </summary>
        Error = 1,

        /// <summary>
        /// Graph topology changed, preventing in-place update.
        /// </summary>
        ErrorTopologyChanged = 2,

        /// <summary>
        /// Graph node type changed, preventing in-place update.
        /// </summary>
        ErrorNodeTypeChanged = 3,

        /// <summary>
        /// Kernel function changed, preventing in-place update.
        /// </summary>
        ErrorFunctionChanged = 4,

        /// <summary>
        /// Kernel parameters changed in an incompatible way.
        /// </summary>
        ErrorParametersChanged = 5,

        /// <summary>
        /// Requested update operation is not supported.
        /// </summary>
        ErrorNotSupported = 6,

        /// <summary>
        /// Function change is not supported for in-place update.
        /// </summary>
        ErrorUnsupportedFunctionChange = 7,

        /// <summary>
        /// Node attributes changed, preventing in-place update.
        /// </summary>
        ErrorAttributesChanged = 8
    }
}
