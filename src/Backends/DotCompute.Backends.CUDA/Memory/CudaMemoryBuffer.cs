// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Native;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Represents a CUDA memory buffer allocated on the GPU device.
    /// </summary>
    public sealed class CudaMemoryBuffer : IUnifiedMemoryBuffer, IDisposable
    {
        private readonly CudaDevice _device;
        private readonly nint _devicePointer;
        private readonly long _sizeInBytes;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryBuffer"/> class.
        /// </summary>
        /// <param name="device">The CUDA device.</param>
        /// <param name="devicePointer">The device memory pointer.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <param name="options">Memory allocation options.</param>
        public CudaMemoryBuffer(CudaDevice device, nint devicePointer, long sizeInBytes, MemoryOptions options = MemoryOptions.None)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _devicePointer = devicePointer;
            _sizeInBytes = sizeInBytes;
            Options = options;
        }

        /// <summary>
        /// Gets the device memory pointer.
        /// </summary>
        public nint DevicePointer => _devicePointer;

        /// <inheritdoc/>
        public long SizeInBytes => _sizeInBytes;

        /// <inheritdoc/>
        public BufferState State => _disposed ? BufferState.Disposed : BufferState.Allocated;
        
        /// <inheritdoc/>
        public MemoryOptions Options { get; }
        
        /// <inheritdoc/>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Copies data from this buffer to another buffer.
        /// </summary>
        public unsafe void CopyTo(CudaMemoryBuffer destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            
            if (destination.SizeInBytes < SizeInBytes)
                throw new ArgumentException("Destination buffer is too small.", nameof(destination));

            // Use CUDA runtime to copy
            CudaRuntime.cudaMemcpy(destination.DevicePointer, DevicePointer, 
                (nuint)SizeInBytes, CudaMemcpyKind.DeviceToDevice);
        }

        /// <summary>
        /// Copies data from host memory to this device buffer.
        /// </summary>
        public unsafe void CopyFromHost<T>(ReadOnlySpan<T> source) where T : unmanaged
        {
            var bytesToCopy = source.Length * sizeof(T);
            if (bytesToCopy > SizeInBytes)
                throw new ArgumentException("Source data exceeds buffer size.", nameof(source));

            fixed (T* ptr = source)
            {
                CudaRuntime.cudaMemcpy(DevicePointer, (nint)ptr, 
                    (nuint)bytesToCopy, CudaMemcpyKind.HostToDevice);
            }
        }

        /// <summary>
        /// Copies data from this device buffer to host memory.
        /// </summary>
        public unsafe void CopyToHost<T>(Span<T> destination) where T : unmanaged
        {
            var bytesToCopy = destination.Length * sizeof(T);
            if (bytesToCopy > SizeInBytes)
                throw new ArgumentException("Destination span is larger than buffer.", nameof(destination));

            fixed (T* ptr = destination)
            {
                CudaRuntime.cudaMemcpy((nint)ptr, DevicePointer, 
                    (nuint)bytesToCopy, CudaMemcpyKind.DeviceToHost);
            }
        }

        /// <summary>
        /// Allocates a new CUDA memory buffer.
        /// </summary>
        public static CudaMemoryBuffer Allocate(CudaDevice device, long sizeInBytes, MemoryOptions options = MemoryOptions.None)
        {
            var ptr = CudaRuntime.cudaMalloc((nuint)sizeInBytes);
            return new CudaMemoryBuffer(device, ptr, sizeInBytes, options);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_devicePointer != nint.Zero)
                {
                    CudaRuntime.cudaFree(_devicePointer);
                }
                _disposed = true;
            }
        }
        
        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}