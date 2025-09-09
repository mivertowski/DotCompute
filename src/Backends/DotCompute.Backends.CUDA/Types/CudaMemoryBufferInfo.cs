// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA unified memory buffer implementation.
    /// </summary>
    public sealed class CudaMemoryBufferInfo
    {
        public nint DevicePointer { get; set; }
        public long SizeInBytes { get; set; }
        public bool IsManaged { get; set; }
        public int DeviceId { get; set; }
    }
}