// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Compilation;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Represents a compiled Ring Kernel with PTX module and function pointer.
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="CompiledKernel"/> with Ring Kernel-specific metadata
/// including the discovered kernel information, PTX module handle, and kernel function pointer.
/// </para>
/// <para>
/// Lifecycle:
/// <list type="number">
/// <item><description>Created by <see cref="CudaRingKernelCompiler"/> after successful compilation</description></item>
/// <item><description>PTX module loaded into CUDA context</description></item>
/// <item><description>Function pointer retrieved for cooperative kernel launch</description></item>
/// <item><description>Module unloaded on disposal</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class CudaCompiledRingKernel : CompiledKernel
{
    /// <summary>
    /// Gets the discovered kernel metadata from reflection.
    /// </summary>
    public DiscoveredRingKernel DiscoveredKernel { get; }

    /// <summary>
    /// Gets the PTX module handle.
    /// </summary>
    public IntPtr ModuleHandle { get; }

    /// <summary>
    /// Gets the kernel function pointer for cooperative launch.
    /// </summary>
    public IntPtr FunctionPointer { get; }

    /// <summary>
    /// Gets the PTX source code bytes.
    /// </summary>
    public ReadOnlyMemory<byte> PtxBytes { get; }

    /// <summary>
    /// Gets the CUDA context this kernel was compiled for.
    /// </summary>
    public IntPtr CudaContext { get; }

    /// <summary>
    /// Gets the compilation timestamp.
    /// </summary>
    public DateTime CompilationTimestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaCompiledRingKernel"/> class.
    /// </summary>
    /// <param name="discoveredKernel">The discovered kernel metadata.</param>
    /// <param name="moduleHandle">The PTX module handle.</param>
    /// <param name="functionPointer">The kernel function pointer.</param>
    /// <param name="ptxBytes">The PTX source code bytes.</param>
    /// <param name="cudaContext">The CUDA context.</param>
    public CudaCompiledRingKernel(
        DiscoveredRingKernel discoveredKernel,
        IntPtr moduleHandle,
        IntPtr functionPointer,
        ReadOnlyMemory<byte> ptxBytes,
        IntPtr cudaContext)
        : base(discoveredKernel.KernelId, null)
    {
        Name = discoveredKernel.KernelId;
        ArgumentNullException.ThrowIfNull(discoveredKernel);

        if (moduleHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Module handle cannot be zero", nameof(moduleHandle));
        }

        if (functionPointer == IntPtr.Zero)
        {
            throw new ArgumentException("Function pointer cannot be zero", nameof(functionPointer));
        }

        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentException("CUDA context cannot be zero", nameof(cudaContext));
        }

        DiscoveredKernel = discoveredKernel;
        ModuleHandle = moduleHandle;
        FunctionPointer = functionPointer;
        PtxBytes = ptxBytes;
        CudaContext = cudaContext;
        CompilationTimestamp = DateTime.UtcNow;

        // Store in metadata for compatibility
        Metadata["DiscoveredKernel"] = discoveredKernel;
        Metadata["ModuleHandle"] = moduleHandle;
        Metadata["FunctionPointer"] = functionPointer;
        Metadata["PtxSize"] = ptxBytes.Length;
        Metadata["CudaContext"] = cudaContext;
        Metadata["CompilationTimestamp"] = CompilationTimestamp;
        Ptx = System.Text.Encoding.UTF8.GetString(ptxBytes.Span);
    }

    /// <summary>
    /// Gets a value indicating whether this kernel is valid for execution.
    /// </summary>
    public bool IsValid => !IsDisposed && ModuleHandle != IntPtr.Zero && FunctionPointer != IntPtr.Zero && CudaContext != IntPtr.Zero;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                // Managed cleanup (if any)
            }

            // Unmanaged cleanup: Unload PTX module
            if (ModuleHandle != IntPtr.Zero && CudaContext != IntPtr.Zero)
            {
                try
                {
                    // Set current context before unloading module
                    _ = Native.CudaRuntime.cuCtxSetCurrent(CudaContext);
                    _ = Native.CudaRuntime.cuModuleUnload(ModuleHandle);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            base.Dispose(disposing);
        }
    }
}
