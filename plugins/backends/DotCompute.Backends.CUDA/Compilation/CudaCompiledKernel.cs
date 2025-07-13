// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Represents a compiled CUDA kernel ready for execution
/// </summary>
public class CudaCompiledKernel : ICompiledKernel
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly byte[] _ptxData;
    private IntPtr _module;
    private IntPtr _function;
    private bool _disposed;

    public string Name { get; }
    public string EntryPoint { get; }
    public KernelMetadata Metadata { get; }

    public CudaCompiledKernel(
        CudaContext context,
        string name,
        string entryPoint,
        byte[] ptxData,
        CompilationOptions? options,
        ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ptxData = ptxData ?? throw new ArgumentNullException(nameof(ptxData));
        
        Name = name ?? throw new ArgumentNullException(nameof(name));
        EntryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));

        // Create metadata
        Metadata = new KernelMetadata
        {
            Name = name,
            EntryPoint = entryPoint,
            CompilationOptions = options,
            CompiledSize = ptxData.Length,
            CompilationTime = DateTime.UtcNow
        };

        LoadModule();
    }

    private void LoadModule()
    {
        try
        {
            _context.MakeCurrent();

            // Pin PTX data
            var handle = GCHandle.Alloc(_ptxData, GCHandleType.Pinned);
            try
            {
                var ptxPtr = handle.AddrOfPinnedObject();

                // Load module from PTX
                var result = CudaRuntime.cuModuleLoadData(ref _module, ptxPtr);
                CudaRuntime.CheckError(result, "Module load");

                // Get function handle
                result = CudaRuntime.cuModuleGetFunction(ref _function, _module, EntryPoint);
                CudaRuntime.CheckError(result, "Get function");

                _logger.LogDebug("Loaded CUDA module for kernel '{Name}' with entry point '{EntryPoint}'", 
                    Name, EntryPoint);
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            throw new CompilationException($"Failed to load CUDA module for kernel '{Name}'", ex);
        }
    }

    public IKernelExecution CreateExecution(ExecutionConfiguration configuration)
    {
        ThrowIfDisposed();
        
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        return new CudaKernelExecution(this, _context, _function, configuration, _logger);
    }

    public byte[] GetBinary()
    {
        ThrowIfDisposed();
        return (byte[])_ptxData.Clone();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaCompiledKernel));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (_module != IntPtr.Zero)
            {
                _context.MakeCurrent();
                CudaRuntime.cuModuleUnload(_module);
                _module = IntPtr.Zero;
                _function = IntPtr.Zero;
            }

            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA compiled kernel disposal");
        }
    }
}

/// <summary>
/// CUDA kernel execution implementation
/// </summary>
internal class CudaKernelExecution : IKernelExecution
{
    private readonly CudaCompiledKernel _kernel;
    private readonly CudaContext _context;
    private readonly IntPtr _function;
    private readonly ExecutionConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly List<IntPtr> _argumentPointers;
    private readonly List<GCHandle> _argumentHandles;

    public CudaKernelExecution(
        CudaCompiledKernel kernel,
        CudaContext context,
        IntPtr function,
        ExecutionConfiguration configuration,
        ILogger logger)
    {
        _kernel = kernel;
        _context = context;
        _function = function;
        _configuration = configuration;
        _logger = logger;
        _argumentPointers = new List<IntPtr>();
        _argumentHandles = new List<GCHandle>();
    }

    public IKernelExecution SetArgument(int index, object value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        // Ensure we have enough space for this argument
        while (_argumentPointers.Count <= index)
        {
            _argumentPointers.Add(IntPtr.Zero);
        }

        // Handle different argument types
        if (value is CudaMemoryBuffer cudaBuffer)
        {
            // Device pointer
            var devicePtr = cudaBuffer.DevicePointer;
            var handle = GCHandle.Alloc(devicePtr, GCHandleType.Pinned);
            _argumentHandles.Add(handle);
            _argumentPointers[index] = handle.AddrOfPinnedObject();
        }
        else if (value is CudaMemoryBufferView cudaView)
        {
            // Device pointer from view
            var devicePtr = cudaView.DevicePointer;
            var handle = GCHandle.Alloc(devicePtr, GCHandleType.Pinned);
            _argumentHandles.Add(handle);
            _argumentPointers[index] = handle.AddrOfPinnedObject();
        }
        else
        {
            // Value type - pin the value
            var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            _argumentHandles.Add(handle);
            _argumentPointers[index] = handle.AddrOfPinnedObject();
        }

        return this;
    }

    public void Execute()
    {
        try
        {
            _context.MakeCurrent();

            // Prepare kernel arguments
            var argPtrs = _argumentPointers.ToArray();
            var argPtrsHandle = GCHandle.Alloc(argPtrs, GCHandleType.Pinned);

            try
            {
                // Calculate grid and block dimensions
                var (gridDim, blockDim) = CalculateLaunchDimensions();

                _logger.LogDebug("Executing CUDA kernel '{Name}' with grid({GridX},{GridY},{GridZ}) block({BlockX},{BlockY},{BlockZ})",
                    _kernel.Name, gridDim.X, gridDim.Y, gridDim.Z, blockDim.X, blockDim.Y, blockDim.Z);

                // Launch kernel
                var result = CudaRuntime.cuLaunchKernel(
                    _function,
                    (uint)gridDim.X, (uint)gridDim.Y, (uint)gridDim.Z,
                    (uint)blockDim.X, (uint)blockDim.Y, (uint)blockDim.Z,
                    (uint)_configuration.SharedMemorySize,
                    _context.Stream,
                    argPtrsHandle.AddrOfPinnedObject(),
                    IntPtr.Zero);

                CudaRuntime.CheckError(result, "Kernel launch");

                // Synchronize if requested
                if (!_configuration.AsyncExecution)
                {
                    _context.Synchronize();
                }
            }
            finally
            {
                argPtrsHandle.Free();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute CUDA kernel '{Name}'", _kernel.Name);
            throw new ExecutionException($"Failed to execute CUDA kernel '{_kernel.Name}'", ex);
        }
        finally
        {
            // Clean up argument handles
            foreach (var handle in _argumentHandles)
            {
                handle.Free();
            }
            _argumentHandles.Clear();
        }
    }

    private (Dim3 gridDim, Dim3 blockDim) CalculateLaunchDimensions()
    {
        var globalWorkSize = _configuration.GlobalWorkSize;
        var localWorkSize = _configuration.LocalWorkSize;

        // If local work size not specified, use default
        if (localWorkSize == null || localWorkSize.All(s => s == 0))
        {
            // Default to 256 threads per block (1D)
            localWorkSize = new[] { 256, 1, 1 };
        }

        // Calculate grid dimensions
        var gridDim = new Dim3
        {
            X = (globalWorkSize[0] + localWorkSize[0] - 1) / localWorkSize[0],
            Y = globalWorkSize.Length > 1 ? (globalWorkSize[1] + localWorkSize[1] - 1) / localWorkSize[1] : 1,
            Z = globalWorkSize.Length > 2 ? (globalWorkSize[2] + localWorkSize[2] - 1) / localWorkSize[2] : 1
        };

        var blockDim = new Dim3
        {
            X = localWorkSize[0],
            Y = localWorkSize.Length > 1 ? localWorkSize[1] : 1,
            Z = localWorkSize.Length > 2 ? localWorkSize[2] : 1
        };

        return (gridDim, blockDim);
    }

    public void Dispose()
    {
        // Clean up any remaining argument handles
        foreach (var handle in _argumentHandles)
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
        _argumentHandles.Clear();
    }

    private struct Dim3
    {
        public int X;
        public int Y;
        public int Z;
    }
}