// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Persistent
{
    /// <summary>
    /// LoggerMessage delegates for CudaRingBufferAllocator.
    /// </summary>
    public sealed partial class CudaRingBufferAllocator
    {
        // Event IDs 6000-6049

        [LoggerMessage(
            EventId = 6000,
            Level = LogLevel.Debug,
            Message = "Allocating ring buffer: depth={Depth}, slice={SliceBytes} bytes, total={TotalBytes} bytes")]
        private partial void LogAllocatingRingBuffer(int depth, long sliceBytes, long totalBytes);

        [LoggerMessage(
            EventId = 6001,
            Level = LogLevel.Information,
            Message = "Created ring buffer at {DevicePtr} with {Depth} slices of {ElementsPerSlice} elements")]
        private partial void LogRingBufferCreated(long devicePtr, int depth, long elementsPerSlice);

        [LoggerMessage(
            EventId = 6002,
            Level = LogLevel.Error,
            Message = "Error disposing ring buffer allocation")]
        private partial void LogDisposeError(Exception ex);
    }

    internal sealed partial class RingBuffer<T> where T : unmanaged
    {
        // Event IDs 6010-6029

        [LoggerMessage(
            EventId = 6010,
            Level = LogLevel.Trace,
            Message = "Ring buffer advanced to index {Index}")]
        private partial void LogAdvanced(int index);
    }
}
