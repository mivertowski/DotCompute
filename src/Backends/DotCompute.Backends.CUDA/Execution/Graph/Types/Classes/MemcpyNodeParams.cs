// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Memory copy node parameters
    /// </summary>
    public class MemcpyNodeParams
    {
        public nint Source { get; set; }
        public nint Destination { get; set; }
        public nuint ByteCount { get; set; }
        public MemcpyKind Kind { get; set; }
    }
}