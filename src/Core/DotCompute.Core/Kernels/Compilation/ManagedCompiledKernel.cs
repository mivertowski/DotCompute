// <copyright file="ManagedCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;

using System;
namespace DotCompute.Core.Kernels.Compilation;

/// <summary>
/// Represents a compiled kernel ready for execution.
/// Encapsulates the compiled binary, metadata, and execution parameters for a kernel.
/// </summary>
public sealed class ManagedCompiledKernel : ICompiledKernel, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the kernel unique identifier.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the kernel name.
    /// Used for identification and logging purposes.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the compiled binary data.
    /// Contains the machine code or intermediate representation of the kernel.
    /// </summary>
    public required byte[] Binary { get; init; }

    /// <summary>
    /// Gets the kernel handle (platform-specific).
    /// May be a pointer to native kernel object or managed handle.
    /// </summary>
    public IntPtr Handle { get; init; }

    /// <summary>
    /// Gets the kernel parameters.
    /// Defines the arguments expected by the kernel during execution.
    /// </summary>
    public required KernelParameter[] Parameters { get; init; }

    /// <summary>
    /// Gets the required work group size.
    /// Specifies constraints on thread block dimensions for optimal performance.
    /// </summary>
    public int[]? RequiredWorkGroupSize { get; init; }

    /// <summary>
    /// Gets the shared memory size in bytes.
    /// Amount of shared/local memory required per work group.
    /// </summary>
    public int SharedMemorySize { get; init; }

    /// <summary>
    /// Gets the compilation log.
    /// Contains warnings, optimization notes, and diagnostic information from compilation.
    /// </summary>
    public string? CompilationLog { get; init; }

    /// <summary>
    /// Gets performance metadata from compilation.
    /// May include register usage, occupancy estimates, and optimization hints.
    /// </summary>
    public Dictionary<string, object>? PerformanceMetadata { get; init; }

    /// <summary>
    /// Gets whether the kernel is compiled and ready for execution.
    /// </summary>
    public bool IsCompiled => Handle != IntPtr.Zero && Binary.Length > 0;

    /// <summary>
    /// Converts this ManagedCompiledKernel to an Abstractions CompiledKernel.
    /// </summary>
    /// <returns>A CompiledKernel instance with the same properties.</returns>
    public CompiledKernel ToCompiledKernel()
    {
        var metadata = PerformanceMetadata ?? [];
        metadata["Handle"] = Handle;
        metadata["EntryPoint"] = Parameters.Length > 0 ? Name : "main";
        metadata["SharedMemorySize"] = SharedMemorySize;
        if (RequiredWorkGroupSize != null)
        {
            metadata["RequiredWorkGroupSize"] = RequiredWorkGroupSize;
        }


        return new CompiledKernel
        {
            Name = Name,
            CompiledBinary = Binary,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Executes the compiled kernel asynchronously.
    /// </summary>
    /// <param name="arguments">The kernel arguments for execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the kernel is not compiled.</exception>
    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        if (!IsCompiled)
        {
            throw new InvalidOperationException($"Kernel '{Name}' is not compiled and cannot be executed.");
        }

        // Platform-specific execution logic would go here
        // This is a placeholder for the actual implementation
        // STUB - TODO
        await Task.Yield();

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Disposes the compiled kernel asynchronously.
    /// Releases native resources and cleans up the kernel handle.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Release native handle if allocated
        if (Handle != IntPtr.Zero)
        {
            // Platform-specific cleanup would go here
            // STUB - TODO
            await Task.Yield();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the compiled kernel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Release native handle if allocated
        if (Handle != IntPtr.Zero)
        {
            // Platform-specific cleanup would go here
            // STUB - TODO
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throws an exception if the kernel has been disposed.
    /// </summary>
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Finalizer to ensure native resources are released.
    /// </summary>
    ~ManagedCompiledKernel()
    {
        Dispose();
    }
}
