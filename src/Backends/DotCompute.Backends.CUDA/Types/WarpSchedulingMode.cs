// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Warp scheduling mode for CUDA execution.
    /// </summary>
    public enum WarpSchedulingMode
    {
        Default,
        Persistent,
        Dynamic,
        Cooperative
    }
}