// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA graph update parameters.
    /// </summary>
    public sealed class CudaGraphUpdateParameters
    {
        public bool UpdateNodeParams { get; set; } = true;
        public bool UpdateKernelParams { get; set; } = true;
        public bool PreserveTopology { get; set; } = true;
        public IntPtr SourceGraph { get; set; }
    }
}