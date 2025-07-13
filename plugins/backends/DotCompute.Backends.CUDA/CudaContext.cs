// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Manages CUDA context lifecycle and operations
/// </summary>
public class CudaContext : IDisposable
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

    private void Initialize()
    {
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
            CudaRuntime.cuDevicePrimaryCtxRelease(_deviceId);
            throw new AcceleratorException($"Failed to set CUDA context: {result}");
        }

        // Create default stream
        result = CudaRuntime.cudaStreamCreate(ref _stream);
        if (result != CudaError.Success)
        {
            CudaRuntime.cuDevicePrimaryCtxRelease(_deviceId);
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
            CudaRuntime.cudaStreamDestroy(_stream);
            _stream = IntPtr.Zero;
        }

        if (_context != IntPtr.Zero)
        {
            CudaRuntime.cuDevicePrimaryCtxRelease(_deviceId);
            _context = IntPtr.Zero;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaContext));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        Cleanup();
        _disposed = true;
    }
}