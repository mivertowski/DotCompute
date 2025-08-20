// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents a compiled kernel that contains the compiled binary or bytecode
/// and can be executed on a compute device with the appropriate arguments.
/// </summary>
public class CompiledKernel : IDisposable
{
    /// <summary>
    /// Gets the unique name identifier for this compiled kernel.
    /// </summary>
    /// <value>The kernel name used for identification during execution.</value>
    public required string Name { get; init; }


    /// <summary>
    /// Gets the unique identifier for this compiled kernel instance.
    /// </summary>
    /// <value>A string representation of the kernel's unique ID, generated automatically if not specified.</value>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the compiled binary or bytecode data for this kernel.
    /// </summary>
    /// <value>
    /// The compiled kernel binary as a byte array, or null if the kernel uses a native handle
    /// or if compilation is deferred until execution time.
    /// </value>
    public byte[]? CompiledBinary { get; init; }

    /// <summary>
    /// Gets the compilation metadata associated with this kernel.
    /// </summary>
    /// <value>
    /// A dictionary containing compilation-specific metadata such as compilation options,
    /// native handles, shared memory requirements, and device-specific configuration.
    /// </value>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether this kernel instance has been disposed.
    /// </summary>
    /// <value>True if the kernel has been disposed; otherwise, false.</value>
    public bool IsDisposed { get; private set; }


    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledKernel"/> class.
    /// </summary>
    public CompiledKernel() { }


    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledKernel"/> class with the specified parameters.
    /// </summary>
    /// <param name="name">The unique name for the compiled kernel.</param>
    /// <param name="binary">The compiled binary data, or null if using native handles.</param>
    /// <param name="options">Optional compilation options to store in metadata.</param>
    /// <param name="metadata">Optional additional metadata for the kernel.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public CompiledKernel(string name, byte[]? binary, CompilationOptions? options = null, Dictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(name);


        Name = name;
        CompiledBinary = binary;
        Metadata = metadata ?? new Dictionary<string, object>();


        if (options != null)
        {
            Metadata["CompilationOptions"] = options;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledKernel"/> class for low-level usage with native handle.
    /// This constructor is typically used by backend implementations that work with native kernel objects.
    /// </summary>
    /// <param name="id">The unique identifier for this kernel instance.</param>
    /// <param name="nativeHandle">The native handle or pointer to the compiled kernel.</param>
    /// <param name="sharedMemorySize">The amount of shared memory required by this kernel in bytes.</param>
    /// <param name="configuration">The kernel configuration used during compilation.</param>
    [SetsRequiredMembers]
    public CompiledKernel(Guid id, IntPtr nativeHandle, int sharedMemorySize, KernelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);


        Name = $"Kernel_{id:N}";
        Id = id.ToString();
        CompiledBinary = null; // No binary for native handle kernels
        Metadata = new Dictionary<string, object>
        {
            ["Id"] = id,
            ["NativeHandle"] = nativeHandle,
            ["SharedMemorySize"] = sharedMemorySize,
            ["Configuration"] = configuration
        };
    }

    /// <summary>
    /// Executes the compiled kernel asynchronously with the specified arguments.
    /// </summary>
    /// <param name="arguments">The arguments to pass to the kernel during execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the kernel execution.</param>
    /// <returns>A task representing the asynchronous kernel execution.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when attempting to execute a disposed kernel.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
    /// <remarks>
    /// This is the base implementation that simply completes successfully.
    /// Backend-specific implementations should override this method to provide
    /// actual kernel execution functionality.
    /// </remarks>
    public virtual Task ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(CompiledKernel));
        }

        ArgumentNullException.ThrowIfNull(arguments);

        // Default implementation - would be overridden by backend-specific implementations
        return Task.CompletedTask;
    }

    /// <summary>
    /// Releases all resources used by this <see cref="CompiledKernel"/> instance.
    /// </summary>
    /// <remarks>
    /// This method sets the <see cref="IsDisposed"/> property to true and suppresses
    /// finalization for this object. Derived classes should override the
    /// <see cref="Dispose(bool)"/> method to release specific resources.
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="CompiledKernel"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    /// <remarks>
    /// This method is called by the <see cref="Dispose()"/> method and the finalizer.
    /// Override this method in derived classes to provide specific resource cleanup logic.
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                // Dispose managed resources here in derived classes
            }

            // Dispose unmanaged resources here in derived classes

            IsDisposed = true;
        }
    }
}