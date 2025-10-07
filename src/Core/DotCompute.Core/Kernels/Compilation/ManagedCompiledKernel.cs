// <copyright file="ManagedCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Kernels;
using System.Reflection;
using System.Runtime.InteropServices;
namespace DotCompute.Core.Kernels.Compilation;

/// <summary>
/// Represents a compiled kernel ready for execution.
/// Encapsulates the compiled binary, metadata, and execution parameters for a kernel.
/// </summary>
public sealed class ManagedCompiledKernel : AbstractionsMemory.ICompiledKernel
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
    public required IReadOnlyList<byte> Binary { get; init; }

    /// <summary>
    /// Gets the kernel handle (platform-specific).
    /// May be a pointer to native kernel object or managed handle.
    /// </summary>
    public IntPtr Handle { get; init; }

    /// <summary>
    /// Gets the kernel parameters.
    /// Defines the arguments expected by the kernel during execution.
    /// </summary>
    public required IReadOnlyList<KernelParameter> Parameters { get; init; }

    /// <summary>
    /// Gets the required work group size.
    /// Specifies constraints on thread block dimensions for optimal performance.
    /// </summary>
    public IReadOnlyList<int>? RequiredWorkGroupSize { get; init; }

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
    public bool IsCompiled => Handle != IntPtr.Zero && Binary.Count > 0;

    /// <summary>
    /// Converts this ManagedCompiledKernel to an Abstractions CompiledKernel.
    /// </summary>
    /// <returns>A CompiledKernel instance with the same properties.</returns>
    public CompiledKernel ToCompiledKernel()
    {
        var metadata = PerformanceMetadata ?? [];
        metadata["Handle"] = Handle;
        metadata["EntryPoint"] = Parameters.Count > 0 ? Name : "main";
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

        // Execute the managed compiled kernel
        // The binary should contain IL code or delegate reference for managed execution

        try
        {
            // Validate parameters against kernel definition
            if (arguments.Count != Parameters.Count)
            {
                throw new ArgumentException(
                    $"Kernel '{Name}' expects {Parameters.Count} parameters but received {arguments.Count}");
            }

            // For managed kernels, the Handle should point to a delegate or reflection method
            if (Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Kernel '{Name}' handle is null. Cannot execute uninitialized managed kernel.");
            }

            // Convert the handle back to a managed delegate or MethodInfo
            // This is a production pattern for managed kernel execution
            var kernelDelegate = GetKernelDelegate();

            if (kernelDelegate != null)
            {
                // Execute using delegate
                await ExecuteWithDelegateAsync(kernelDelegate, arguments, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback: execute using reflection on the binary
                await ExecuteWithReflectionAsync(arguments, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation as expected
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to execute managed kernel '{Name}': {ex.Message}", ex);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Gets the kernel delegate from the handle for managed execution.
    /// </summary>
    /// <returns>The kernel delegate if available, null otherwise.</returns>
    private Delegate? GetKernelDelegate()
    {
        try
        {
            // For managed kernels, the handle can be a GCHandle to a delegate
            if (Handle != IntPtr.Zero)
            {
                var gcHandle = GCHandle.FromIntPtr(Handle);
                if (gcHandle.IsAllocated && gcHandle.Target is Delegate kernelDelegate)
                {
                    return kernelDelegate;
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Handle is not a valid GCHandle, fallback to reflection
        }

        return null;
    }

    /// <summary>
    /// Executes the kernel using a managed delegate.
    /// </summary>
    /// <param name="kernelDelegate">The delegate to execute.</param>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    private static async ValueTask ExecuteWithDelegateAsync(Delegate kernelDelegate, KernelArguments arguments, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Convert KernelArguments to object array for delegate invocation
            var args = new object[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                args[i] = arguments.Arguments[i] ?? throw new ArgumentNullException($"Argument {i} is null");
            }

            // Invoke the delegate
            _ = kernelDelegate.DynamicInvoke(args);

        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the kernel using reflection on the compiled binary.
    /// </summary>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    private async ValueTask ExecuteWithReflectionAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Load assembly from binary data
                var assembly = Assembly.Load(Binary);

                // Find the kernel entry point (typically the first public method in the first type)
                var types = assembly.GetTypes();
                var kernelType = types.FirstOrDefault(t => t.IsPublic)
                    ?? throw new InvalidOperationException($"No public types found in kernel '{Name}' assembly");

                var methods = kernelType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var entryMethod = methods.FirstOrDefault(m => m.IsStatic && m.IsPublic)
                    ?? throw new InvalidOperationException($"No suitable entry method found in kernel '{Name}' type '{kernelType.Name}'");

                // Convert KernelArguments to object array for method invocation
                var args = new object[arguments.Count];
                for (var i = 0; i < arguments.Count; i++)
                {
                    args[i] = arguments.Arguments[i] ?? throw new ArgumentNullException($"Argument {i} is null");
                }

                // Invoke the method
                _ = entryMethod.Invoke(null, args);
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loaderExceptions = string.Join(", ", ex.LoaderExceptions.Select(e => e?.Message ?? "Unknown"));
                throw new InvalidOperationException(
                    $"Failed to load kernel '{Name}' types: {loaderExceptions}", ex);
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException(
                    $"Kernel '{Name}' execution failed: {ex.InnerException?.Message ?? ex.Message}",
                    ex.InnerException ?? ex);
            }

        }, cancellationToken).ConfigureAwait(false);
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

        // Release managed handle if allocated
        if (Handle != IntPtr.Zero)
        {
            try
            {
                // If the handle is a GCHandle, free it properly
                var gcHandle = GCHandle.FromIntPtr(Handle);
                if (gcHandle.IsAllocated)
                {
                    gcHandle.Free();
                }
            }
            catch (InvalidOperationException)
            {
                // Handle was not a valid GCHandle - this is acceptable for managed kernels
                // that might use other handle types
            }

            // Note: Handle is init-only and cannot be cleared, but GCHandle has been freed
        }

        // Allow derived classes to perform additional cleanup
        await Task.Yield();

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

        // Release managed handle if allocated
        if (Handle != IntPtr.Zero)
        {
            try
            {
                // If the handle is a GCHandle, free it properly
                var gcHandle = GCHandle.FromIntPtr(Handle);
                if (gcHandle.IsAllocated)
                {
                    gcHandle.Free();
                }
            }
            catch (InvalidOperationException)
            {
                // Handle was not a valid GCHandle - this is acceptable for managed kernels
                // that might use other handle types
            }

            // Note: Handle is init-only and cannot be cleared, but GCHandle has been freed
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
