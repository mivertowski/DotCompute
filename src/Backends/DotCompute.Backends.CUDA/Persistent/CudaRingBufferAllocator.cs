// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Persistent
{
    /// <summary>
    /// Manages ring buffer allocations for persistent kernels.
    /// Ring buffers enable efficient temporal data management for wave propagation and similar algorithms.
    /// </summary>
    public sealed class CudaRingBufferAllocator : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly List<RingBufferAllocation> _allocations;
        private bool _disposed;

        public CudaRingBufferAllocator(CudaContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _allocations = [];
        }

        /// <summary>
        /// Allocates a ring buffer with the specified depth and element count.
        /// </summary>
        /// <typeparam name="T">The element type (must be unmanaged)</typeparam>
        /// <param name="depth">Number of temporal slices in the ring buffer</param>
        /// <param name="elementsPerSlice">Number of elements in each slice</param>
        /// <returns>A ring buffer allocation</returns>
        public async ValueTask<IRingBuffer<T>> AllocateRingBufferAsync<T>(int depth, long elementsPerSlice) 
            where T : unmanaged
        {
            ThrowIfDisposed();

            if (depth < 2)
            {

                throw new ArgumentException("Ring buffer depth must be at least 2", nameof(depth));
            }


            if (elementsPerSlice <= 0)
            {

                throw new ArgumentException("Elements per slice must be positive", nameof(elementsPerSlice));
            }


            var elementSize = Marshal.SizeOf<T>();
            var sliceBytes = elementsPerSlice * elementSize;
            var totalBytes = sliceBytes * depth;

            _logger.LogDebug("Allocating ring buffer: depth={Depth}, slice={SliceBytes} bytes, total={TotalBytes} bytes",
                depth, sliceBytes, totalBytes);

            // Allocate contiguous device memory for all slices
            var devicePtr = IntPtr.Zero;
            var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)totalBytes);
            CudaRuntime.CheckError(result, "allocating ring buffer");

            // Initialize to zero
            result = CudaRuntime.cudaMemset(devicePtr, 0, (nuint)totalBytes);
            CudaRuntime.CheckError(result, "initializing ring buffer");

            var ringBuffer = new RingBuffer<T>(
                devicePtr,
                depth,
                elementsPerSlice,
                sliceBytes,
                _context,
                _logger);

            var allocation = new RingBufferAllocation(devicePtr, totalBytes, ringBuffer);
            _allocations.Add(allocation);

            _logger.LogInformation("Created ring buffer at {Ptr:X} with {Depth} slices of {Elements} elements",
                devicePtr.ToInt64(), depth, elementsPerSlice);

            return await Task.FromResult(ringBuffer);
        }

        /// <summary>
        /// Creates a specialized ring buffer for wave propagation simulations.
        /// </summary>
        public async ValueTask<IWaveRingBuffer<T>> AllocateWaveBufferAsync<T>(
            int gridWidth, 
            int gridHeight = 1, 
            int gridDepth = 1,
            int temporalDepth = 3) where T : unmanaged
        {
            var elementsPerSlice = (long)gridWidth * gridHeight * gridDepth;
            var ringBuffer = await AllocateRingBufferAsync<T>(temporalDepth, elementsPerSlice);
            
            return new WaveRingBuffer<T>(
                ringBuffer,
                gridWidth,
                gridHeight,
                gridDepth);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {

                throw new ObjectDisposedException(nameof(CudaRingBufferAllocator));
            }

        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            foreach (var allocation in _allocations)
            {
                try
                {
                    allocation.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing ring buffer allocation");
                }
            }

            _allocations.Clear();
            _disposed = true;
        }

        private sealed class RingBufferAllocation : IDisposable
        {
            public IntPtr DevicePointer { get; }
            public long TotalBytes { get; }
            public object RingBuffer { get; }

            public RingBufferAllocation(IntPtr devicePointer, long totalBytes, object ringBuffer)
            {
                DevicePointer = devicePointer;
                TotalBytes = totalBytes;
                RingBuffer = ringBuffer;
            }

            public void Dispose()
            {
                if (DevicePointer != IntPtr.Zero)
                {
                    _ = CudaRuntime.cudaFree(DevicePointer);
                }

                if (RingBuffer is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Interface for ring buffer operations.
    /// </summary>
    public interface IRingBuffer<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Gets the depth (number of temporal slices) of the ring buffer.
        /// </summary>
        int Depth { get; }

        /// <summary>
        /// Gets the number of elements per slice.
        /// </summary>
        long ElementsPerSlice { get; }

        /// <summary>
        /// Gets the device pointer to a specific slice.
        /// </summary>
        IntPtr GetSlicePointer(int sliceIndex);

        /// <summary>
        /// Advances the ring buffer by one time step.
        /// </summary>
        void Advance();

        /// <summary>
        /// Gets the current time step index.
        /// </summary>
        int CurrentTimeStep { get; }

        /// <summary>
        /// Copies data to a specific slice.
        /// </summary>
        ValueTask CopyToSliceAsync(int sliceIndex, ReadOnlyMemory<T> data);

        /// <summary>
        /// Copies data from a specific slice.
        /// </summary>
        ValueTask CopyFromSliceAsync(int sliceIndex, Memory<T> data);
    }

    /// <summary>
    /// Specialized ring buffer for wave propagation simulations.
    /// </summary>
    public interface IWaveRingBuffer<T> : IRingBuffer<T> where T : unmanaged
    {
        /// <summary>
        /// Gets the grid dimensions.
        /// </summary>
        (int Width, int Height, int Depth) GridDimensions { get; }

        /// <summary>
        /// Gets pointer to current time step data (u^n).
        /// </summary>
        IntPtr Current { get; }

        /// <summary>
        /// Gets pointer to previous time step data (u^{n-1}).
        /// </summary>
        IntPtr Previous { get; }

        /// <summary>
        /// Gets pointer to two time steps ago data (u^{n-2}).
        /// </summary>
        IntPtr TwoStepsAgo { get; }

        /// <summary>
        /// Swaps buffers for next time step.
        /// </summary>
        void SwapBuffers();
    }

    /// <summary>
    /// Implementation of ring buffer for device memory.
    /// </summary>
    internal sealed class RingBuffer<T> : IRingBuffer<T> where T : unmanaged
    {
        private readonly IntPtr _basePointer;
        private readonly long _sliceBytes;
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private int _currentIndex;
        private bool _disposed;

        public int Depth { get; }
        public long ElementsPerSlice { get; }
        public int CurrentTimeStep => _currentIndex;

        public RingBuffer(
            IntPtr basePointer,
            int depth,
            long elementsPerSlice,
            long sliceBytes,
            CudaContext context,
            ILogger logger)
        {
            _basePointer = basePointer;
            Depth = depth;
            ElementsPerSlice = elementsPerSlice;
            _sliceBytes = sliceBytes;
            _context = context;
            _logger = logger;
            _currentIndex = 0;
        }

        public IntPtr GetSlicePointer(int sliceIndex)
        {
            if (sliceIndex < 0 || sliceIndex >= Depth)
            {

                throw new ArgumentOutOfRangeException(nameof(sliceIndex));
            }


            var actualIndex = (_currentIndex + sliceIndex) % Depth;
            return _basePointer + (int)(actualIndex * _sliceBytes);
        }

        public void Advance()
        {
            _currentIndex = (_currentIndex + 1) % Depth;
            _logger.LogTrace("Ring buffer advanced to index {Index}", _currentIndex);
        }

        public async ValueTask CopyToSliceAsync(int sliceIndex, ReadOnlyMemory<T> data)
        {
            ThrowIfDisposed();

            var slicePtr = GetSlicePointer(sliceIndex);
            var handle = GCHandle.Alloc(data.ToArray(), GCHandleType.Pinned);
            try
            {
                var result = CudaRuntime.cudaMemcpy(
                    slicePtr,
                    handle.AddrOfPinnedObject(),
                    (nuint)_sliceBytes,
                    CudaMemcpyKind.HostToDevice);
                CudaRuntime.CheckError(result, "copying to ring buffer slice");
            }
            finally
            {
                handle.Free();
            }

            await Task.CompletedTask;
        }

        public async ValueTask CopyFromSliceAsync(int sliceIndex, Memory<T> data)
        {
            ThrowIfDisposed();

            var slicePtr = GetSlicePointer(sliceIndex);
            var array = new T[ElementsPerSlice];
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            try
            {
                var result = CudaRuntime.cudaMemcpy(
                    handle.AddrOfPinnedObject(),
                    slicePtr,
                    (nuint)_sliceBytes,
                    CudaMemcpyKind.DeviceToHost);
                CudaRuntime.CheckError(result, "copying from ring buffer slice");
                array.CopyTo(data.Span);
            }
            finally
            {
                handle.Free();
            }

            await Task.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {

                throw new ObjectDisposedException(nameof(RingBuffer<T>));
            }

        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Specialized ring buffer implementation for wave simulations.
    /// </summary>
    internal sealed class WaveRingBuffer<T> : IWaveRingBuffer<T> where T : unmanaged
    {
        private readonly IRingBuffer<T> _ringBuffer;
        private readonly int _width;
        private readonly int _height;
        private readonly int _depth;

        public WaveRingBuffer(IRingBuffer<T> ringBuffer, int width, int height, int depth)
        {
            _ringBuffer = ringBuffer ?? throw new ArgumentNullException(nameof(ringBuffer));
            _width = width;
            _height = height;
            _depth = depth;
        }

        public int Depth => _ringBuffer.Depth;
        public long ElementsPerSlice => _ringBuffer.ElementsPerSlice;
        public int CurrentTimeStep => _ringBuffer.CurrentTimeStep;
        
        public (int Width, int Height, int Depth) GridDimensions => (_width, _height, _depth);

        public IntPtr Current => _ringBuffer.GetSlicePointer(0);
        public IntPtr Previous => _ringBuffer.GetSlicePointer(Depth - 1);
        public IntPtr TwoStepsAgo => Depth >= 3 ? _ringBuffer.GetSlicePointer(Depth - 2) : IntPtr.Zero;

        public IntPtr GetSlicePointer(int sliceIndex) => _ringBuffer.GetSlicePointer(sliceIndex);
        public void Advance() => _ringBuffer.Advance();
        public void SwapBuffers() => Advance();

        public ValueTask CopyToSliceAsync(int sliceIndex, ReadOnlyMemory<T> data) => _ringBuffer.CopyToSliceAsync(sliceIndex, data);

        public ValueTask CopyFromSliceAsync(int sliceIndex, Memory<T> data) => _ringBuffer.CopyFromSliceAsync(sliceIndex, data);

        public void Dispose() => _ringBuffer.Dispose();
    }
}