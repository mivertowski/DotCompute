// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// CUDA graph execution update result enumeration
    /// </summary>
    public enum CudaGraphExecUpdateResult : uint
    {
        Success = 0,
        Error = 1,
        ErrorTopologyChanged = 2,
        ErrorNodeTypeChanged = 3,
        ErrorFunctionChanged = 4,
        ErrorParametersChanged = 5,
        ErrorNotSupported = 6,
        ErrorUnsupportedFunctionChange = 7,
        ErrorAttributesChanged = 8
    }
}