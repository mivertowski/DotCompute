// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Represents a compiled CUDA kernel ready for execution
/// </summary>
public class CudaCompiledKernel : ICompiledKernel, IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly byte[] _ptxData;
    private IntPtr _module;
    private IntPtr _function;
    private bool _disposed;

    public string Name { get; }

    private readonly string _entryPoint;

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
        _entryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));

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
                result = CudaRuntime.cuModuleGetFunction(ref _function, _module, _entryPoint);
                CudaRuntime.CheckError(result, "Get function");

                _logger.LogDebug("Loaded CUDA module for kernel '{Name}' with entry point '{EntryPoint}'",
                    Name, _entryPoint);
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load CUDA module for kernel '{Name}'", ex);
        }
    }

    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments.Length == 0)
        {
            throw new ArgumentException("No arguments provided for kernel execution", nameof(arguments));
        }

        try
        {
            _context.MakeCurrent();

            // Prepare kernel arguments
            var argPointers = new List<IntPtr>();
            var handles = new List<GCHandle>();

            try
            {
                // Process each argument
                foreach (var arg in arguments.Arguments)
                {
                    if (arg is CudaMemoryBuffer cudaBuffer)
                    {
                        // Device pointer
                        var devicePtr = cudaBuffer.DevicePointer;
                        var handle = GCHandle.Alloc(devicePtr, GCHandleType.Pinned);
                        handles.Add(handle);
                        argPointers.Add(handle.AddrOfPinnedObject());
                    }
                    else if (arg is CudaMemoryBufferView cudaView)
                    {
                        // Device pointer from view
                        var devicePtr = cudaView.DevicePointer;
                        var handle = GCHandle.Alloc(devicePtr, GCHandleType.Pinned);
                        handles.Add(handle);
                        argPointers.Add(handle.AddrOfPinnedObject());
                    }
                    else
                    {
                        // Value type - pin the value
                        var handle = GCHandle.Alloc(arg, GCHandleType.Pinned);
                        handles.Add(handle);
                        argPointers.Add(handle.AddrOfPinnedObject());
                    }
                }

                // Convert to array and pin
                var argPtrs = argPointers.ToArray();
                var argPtrsHandle = GCHandle.Alloc(argPtrs, GCHandleType.Pinned);

                try
                {
                    // Default launch configuration (256 threads per block)
                    uint blockSize = 256;
                    uint gridSize = 1; // Should be calculated based on problem size

                    _logger.LogDebug("Executing CUDA kernel '{Name}' with grid={GridSize} block={BlockSize}",
                        Name, gridSize, blockSize);

                    // Launch kernel
                    var result = CudaRuntime.cuLaunchKernel(
                        _function,
                        gridSize, 1, 1,    // Grid dimensions
                        blockSize, 1, 1,   // Block dimensions
                        0,                 // Shared memory size
                        _context.Stream,
                        argPtrsHandle.AddrOfPinnedObject(),
                        IntPtr.Zero);

                    CudaRuntime.CheckError(result, "Kernel launch");

                    // Synchronize
                    await Task.Run(() => _context.Synchronize(), cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    argPtrsHandle.Free();
                }
            }
            finally
            {
                // Clean up argument handles
                foreach (var handle in handles)
                {
                    handle.Free();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute CUDA kernel '{Name}'", Name);
            throw new InvalidOperationException($"Failed to execute CUDA kernel '{Name}'", ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaCompiledKernel));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_module != IntPtr.Zero)
            {
                await Task.Run(() =>
                {
                    _context.MakeCurrent();
                    CudaRuntime.cuModuleUnload(_module);
                    _module = IntPtr.Zero;
                    _function = IntPtr.Zero;
                }).ConfigureAwait(false);
            }

            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA compiled kernel disposal");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

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
