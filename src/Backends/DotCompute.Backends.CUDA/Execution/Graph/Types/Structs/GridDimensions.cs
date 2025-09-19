// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Structs
{
    /// <summary>
    /// Grid dimensions for kernel launch
    /// </summary>
    public struct GridDimensions
    {
        public uint X { get; set; }
        public uint Y { get; set; }
        public uint Z { get; set; }

        public GridDimensions(uint x, uint y = 1, uint z = 1)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}