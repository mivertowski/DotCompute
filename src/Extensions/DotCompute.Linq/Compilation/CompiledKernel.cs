using System;
using DotCompute.Abstractions;
using DotCompute.Linq.CodeGeneration;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Represents a compiled GPU kernel ready for execution.
/// </summary>
/// <remarks>
/// Contains backend-specific handles and metadata for kernel launch:
/// - CUDA: CUmodule (ModuleHandle) and CUfunction (KernelHandle)
/// - OpenCL: cl_program (ModuleHandle) and cl_kernel (KernelHandle)
/// - Metal: MTLLibrary (ModuleHandle) and MTLFunction (KernelHandle)
/// </remarks>
public sealed class CompiledKernel : IDisposable
{
    /// <summary>
    /// Gets the compute backend this kernel was compiled for.
    /// </summary>
    public ComputeBackend Backend { get; init; }

    /// <summary>
    /// Gets the backend-specific kernel function handle.
    /// </summary>
    /// <remarks>
    /// - CUDA: CUfunction pointer
    /// - OpenCL: cl_kernel pointer
    /// - Metal: MTLFunction object pointer
    /// </remarks>
    public nint KernelHandle { get; init; }

    /// <summary>
    /// Gets the backend-specific module/program/library handle.
    /// </summary>
    /// <remarks>
    /// - CUDA: CUmodule pointer
    /// - OpenCL: cl_program pointer
    /// - Metal: MTLLibrary object pointer
    /// </remarks>
    public nint ModuleHandle { get; init; }

    /// <summary>
    /// Gets the type metadata for kernel parameter marshaling.
    /// </summary>
    public TypeMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the recommended grid dimensions for optimal GPU utilization.
    /// </summary>
    /// <remarks>
    /// These dimensions are calculated based on:
    /// - Data size and GPU hardware characteristics
    /// - Backend-specific occupancy optimization
    /// - Memory bandwidth and compute capability
    /// </remarks>
    public GridDimensions RecommendedGrid { get; init; }

    /// <summary>
    /// Gets the kernel entry point name.
    /// </summary>
    public string EntryPoint { get; init; }

    /// <summary>
    /// Gets the compilation timestamp (for cache invalidation).
    /// </summary>
    public DateTimeOffset CompiledAt { get; init; }

    /// <summary>
    /// Gets whether this kernel has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets or sets the internal ICompiledKernel reference (bridge to backend).
    /// </summary>
    /// <remarks>
    /// Phase 6 Option A: This property bridges the LINQ CompiledKernel DTO with
    /// the backend ICompiledKernel implementation. When set, execution uses the
    /// backend kernel directly instead of raw handles.
    /// </remarks>
    internal DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel? __InternalKernelReference { get; init; }

    /// <summary>
    /// Initializes a new compiled kernel.
    /// </summary>
    public CompiledKernel()
    {
        CompiledAt = DateTimeOffset.UtcNow;
        EntryPoint = string.Empty;
        Metadata = null!;
        RecommendedGrid = null!;
    }

    /// <summary>
    /// Releases GPU resources associated with this kernel.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;

        // Backend-specific cleanup handled by implementers
        // CUDA: cuModuleUnload(ModuleHandle)
        // OpenCL: clReleaseKernel(KernelHandle), clReleaseProgram(ModuleHandle)
        // Metal: Release NSObject references

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Validates that the kernel is ready for execution.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if kernel has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if kernel handles are invalid.</exception>
    public void ValidateForExecution()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(CompiledKernel), "Kernel has been disposed");

        if (KernelHandle == nint.Zero)
            throw new InvalidOperationException("Kernel handle is null - compilation may have failed");

        if (ModuleHandle == nint.Zero)
            throw new InvalidOperationException("Module handle is null - compilation may have failed");
    }
}
