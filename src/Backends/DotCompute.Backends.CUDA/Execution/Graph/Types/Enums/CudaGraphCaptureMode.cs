// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Enums
{
    /// <summary>
    /// CUDA graph capture mode
    /// </summary>
    public enum CudaGraphCaptureMode : uint
    {
        Global = 0,
        ThreadLocal = 1,
        Relaxed = 2
    }
}