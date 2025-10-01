// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Initialization;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA
{

    /// <summary>
    /// Manages CUDA context lifecycle and operations
    /// </summary>
    public sealed class CudaContext : IDisposable
    {
        private IntPtr _context;
        private IntPtr _stream;
        private readonly int _deviceId;
        private bool _disposed;

        public IntPtr Handle => _context;
        public IntPtr Stream => _stream;
        public int DeviceId => _deviceId;

        public CudaContext(int deviceId)
        {
            _deviceId = deviceId;
            Initialize();
        }

        public CudaContext(IntPtr contextPtr, int deviceId)
        {
            _context = contextPtr;
            _deviceId = deviceId;
            // Context already created, just initialize stream
            var result = CudaRuntime.cudaStreamCreate(ref _stream);
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"Failed to create CUDA stream: {result}");
            }
        }

        private void Initialize()
        {
            // Ensure CUDA runtime is properly initialized
            if (!CudaInitializer.EnsureInitialized())
            {
                var errorMsg = CudaInitializer.InitializationErrorMessage ?? "Unknown CUDA initialization error";
                throw new AcceleratorException($"Failed to initialize CUDA runtime: {errorMsg}");
            }

            // Initialize CUDA driver API if not already done
            var initResult = CudaRuntime.cuInit(0);
            // CUDA_ERROR_ALREADY_INITIALIZED = 4 for driver API
            if (initResult != CudaError.Success && initResult != (CudaError)4)
            {
                throw new AcceleratorException($"Failed to initialize CUDA driver: {initResult}");
            }

            // Set the active device
            var result = CudaRuntime.cudaSetDevice(_deviceId);
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"Failed to set CUDA device {_deviceId}: {result}");
            }

            // Create primary context
            result = CudaRuntime.cuDevicePrimaryCtxRetain(ref _context, _deviceId);
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"Failed to create CUDA context: {result}");
            }

            // Set current context
            result = CudaRuntime.cuCtxSetCurrent(_context);
            if (result != CudaError.Success)
            {
                _ = CudaRuntime.cuDevicePrimaryCtxRelease(_deviceId);
                throw new AcceleratorException($"Failed to set CUDA context: {result}");
            }

            // Create default stream
            result = CudaRuntime.cudaStreamCreate(ref _stream);
            if (result != CudaError.Success)
            {
                _ = CudaRuntime.cuDevicePrimaryCtxRelease(_deviceId);
                throw new AcceleratorException($"Failed to create CUDA stream: {result}");
            }
        }

        public void Reinitialize()
        {
            Cleanup();
            Initialize();
        }

        public void MakeCurrent()
        {
            ThrowIfDisposed();

            var result = CudaRuntime.cuCtxSetCurrent(_context);
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"Failed to make CUDA context current: {result}");
            }
        }

        public void Synchronize()
        {
            ThrowIfDisposed();

            var result = CudaRuntime.cudaStreamSynchronize(_stream);
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"Failed to synchronize CUDA stream: {result}");
            }
        }

        private void Cleanup()
        {
            if (_stream != IntPtr.Zero)
            {
                _ = CudaRuntime.cudaStreamDestroy(_stream);
                _stream = IntPtr.Zero;
            }

            if (_context != IntPtr.Zero)
            {
                _ = CudaRuntime.cuDevicePrimaryCtxRelease(_deviceId);
                _context = IntPtr.Zero;
            }
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Cleanup();
            }

            _disposed = true;
        }

        /// <summary>
        /// Converts this CudaContext to an IAccelerator
        /// </summary>
        public IAccelerator ToIAccelerator()
        {
            return new CudaAccelerator(DeviceId);
        }
    }
}
