// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Compilation;

/// <summary>
/// Represents a compiled Ring Kernel with Metal library and pipeline state.
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="CompiledKernel"/> with Ring Kernel-specific metadata
/// including the discovered kernel information, Metal library handle, and compute pipeline state.
/// </para>
/// <para>
/// Lifecycle:
/// <list type="number">
/// <item><description>Created by MetalRingKernelCompiler after successful compilation</description></item>
/// <item><description>Metal library compiled from MSL source</description></item>
/// <item><description>Compute pipeline state created for kernel execution</description></item>
/// <item><description>Library and pipeline released on disposal</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class MetalCompiledRingKernel : CompiledKernel
{
    /// <summary>
    /// Gets the discovered kernel metadata from reflection.
    /// </summary>
    public DiscoveredMetalRingKernel DiscoveredKernel { get; }

    /// <summary>
    /// Gets the Metal library handle.
    /// </summary>
    public IntPtr LibraryHandle { get; }

    /// <summary>
    /// Gets the compute pipeline state handle for kernel execution.
    /// </summary>
    public IntPtr PipelineStateHandle { get; }

    /// <summary>
    /// Gets the kernel function handle from the library.
    /// </summary>
    public IntPtr KernelFunctionHandle { get; }

    /// <summary>
    /// Gets the MSL source code.
    /// </summary>
    public string MslSource { get; }

    /// <summary>
    /// Gets the Metal device this kernel was compiled for.
    /// </summary>
    public IntPtr MetalDevice { get; }

    /// <summary>
    /// Gets the compilation timestamp.
    /// </summary>
    public DateTime CompilationTimestamp { get; }

    /// <summary>
    /// Gets the thread execution width (SIMD size) for this kernel.
    /// </summary>
    /// <remarks>
    /// On Apple Silicon, this is typically 32 threads.
    /// </remarks>
    public int ThreadExecutionWidth { get; }

    /// <summary>
    /// Gets the maximum total threads per threadgroup for this kernel.
    /// </summary>
    public int MaxTotalThreadsPerThreadgroup { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalCompiledRingKernel"/> class.
    /// </summary>
    /// <param name="discoveredKernel">The discovered kernel metadata.</param>
    /// <param name="libraryHandle">The Metal library handle.</param>
    /// <param name="pipelineStateHandle">The compute pipeline state handle.</param>
    /// <param name="kernelFunctionHandle">The kernel function handle.</param>
    /// <param name="mslSource">The MSL source code.</param>
    /// <param name="metalDevice">The Metal device.</param>
    /// <param name="threadExecutionWidth">The thread execution width.</param>
    /// <param name="maxTotalThreadsPerThreadgroup">The maximum threads per threadgroup.</param>
    [SetsRequiredMembers]
    public MetalCompiledRingKernel(
        DiscoveredMetalRingKernel discoveredKernel,
        IntPtr libraryHandle,
        IntPtr pipelineStateHandle,
        IntPtr kernelFunctionHandle,
        string mslSource,
        IntPtr metalDevice,
        int threadExecutionWidth = 32,
        int maxTotalThreadsPerThreadgroup = 1024)
        : base(discoveredKernel.KernelId, null)
    {
        Name = discoveredKernel.KernelId;
        ArgumentNullException.ThrowIfNull(discoveredKernel);
        ArgumentException.ThrowIfNullOrWhiteSpace(mslSource);

        if (libraryHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Library handle cannot be zero", nameof(libraryHandle));
        }

        if (pipelineStateHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Pipeline state handle cannot be zero", nameof(pipelineStateHandle));
        }

        if (metalDevice == IntPtr.Zero)
        {
            throw new ArgumentException("Metal device cannot be zero", nameof(metalDevice));
        }

        DiscoveredKernel = discoveredKernel;
        LibraryHandle = libraryHandle;
        PipelineStateHandle = pipelineStateHandle;
        KernelFunctionHandle = kernelFunctionHandle;
        MslSource = mslSource;
        MetalDevice = metalDevice;
        ThreadExecutionWidth = threadExecutionWidth;
        MaxTotalThreadsPerThreadgroup = maxTotalThreadsPerThreadgroup;
        CompilationTimestamp = DateTime.UtcNow;

        // Store in metadata for compatibility
        Metadata["DiscoveredKernel"] = discoveredKernel;
        Metadata["LibraryHandle"] = libraryHandle;
        Metadata["PipelineStateHandle"] = pipelineStateHandle;
        Metadata["KernelFunctionHandle"] = kernelFunctionHandle;
        Metadata["MslSize"] = mslSource.Length;
        Metadata["MetalDevice"] = metalDevice;
        Metadata["ThreadExecutionWidth"] = threadExecutionWidth;
        Metadata["MaxTotalThreadsPerThreadgroup"] = maxTotalThreadsPerThreadgroup;
        Metadata["CompilationTimestamp"] = CompilationTimestamp;

        // Store MSL source similarly to PTX
        Ptx = mslSource; // Reuse Ptx property for source storage
    }

    /// <summary>
    /// Gets a value indicating whether this kernel is valid for execution.
    /// </summary>
    public bool IsValid => !IsDisposed &&
                          LibraryHandle != IntPtr.Zero &&
                          PipelineStateHandle != IntPtr.Zero &&
                          MetalDevice != IntPtr.Zero;

    /// <summary>
    /// Gets the kernel entry point name (function name in MSL).
    /// </summary>
    public string KernelEntryPoint => $"{SanitizeKernelName(DiscoveredKernel.KernelId)}_kernel";

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                // Managed cleanup (if any)
            }

            // Unmanaged cleanup: Release Metal resources
            // Note: Metal uses ARC, but we track resources for explicit lifecycle management
            if (PipelineStateHandle != IntPtr.Zero)
            {
                try
                {
                    MetalNative.ReleasePipelineState(PipelineStateHandle);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            if (KernelFunctionHandle != IntPtr.Zero)
            {
                try
                {
                    MetalNative.ReleaseFunction(KernelFunctionHandle);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            if (LibraryHandle != IntPtr.Zero)
            {
                try
                {
                    MetalNative.ReleaseLibrary(LibraryHandle);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Sanitizes a kernel ID to create a valid MSL function name.
    /// </summary>
    private static string SanitizeKernelName(string kernelId)
    {
        var sanitized = new System.Text.StringBuilder(kernelId.Length);
        foreach (var c in kernelId)
        {
            if (char.IsLetterOrDigit(c))
            {
                sanitized.Append(c);
            }
            else
            {
                sanitized.Append('_');
            }
        }
        return sanitized.ToString();
    }
}
