// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Execution.Graph.Types.Structs;

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Graph node parameters for kernel execution
    /// </summary>
    public class KernelNodeParams
    {
        public nint Function { get; set; }
        public GridDimensions GridDim { get; set; }

        public BlockDimensions BlockDim { get; set; }

        public uint SharedMemoryBytes { get; set; }
        public nint KernelParams { get; set; }
    }
}