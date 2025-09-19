// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Memory set node parameters
    /// </summary>
    public class MemsetNodeParams
    {
        public nint Destination { get; set; }
        public uint Value { get; set; }
        public uint ElementSize { get; set; }
        public nuint Width { get; set; }
        public nuint Height { get; set; }
        public nuint Pitch { get; set; }
    }
}