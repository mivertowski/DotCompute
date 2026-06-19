// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

namespace DotCompute.Backends.Metal.Kernels;

/// <summary>
/// AOT-friendly accessor for the private <c>_pipelineState</c> field on
/// <see cref="MetalCompiledKernel"/>.
///
/// <para>
/// The execution layer needs the raw <c>MTLComputePipelineState</c> handle to bind
/// to a command encoder, but <see cref="MetalCompiledKernel"/> deliberately keeps the
/// native handle private. Using <see cref="UnsafeAccessorAttribute"/> instead of
/// <c>FieldInfo.GetValue</c> resolves the field at source-gen time, removes the
/// runtime reflection call (and its associated trim/AOT warnings), and lets the JIT
/// inline the access into a single field load.
/// </para>
/// </summary>
internal static class MetalCompiledKernelAccessor
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pipelineState")]
    private static extern ref IntPtr PipelineStateRef(MetalCompiledKernel kernel);

    /// <summary>
    /// Returns the native <c>MTLComputePipelineState</c> handle for the given compiled kernel.
    /// </summary>
    public static IntPtr PipelineState(MetalCompiledKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        return PipelineStateRef(kernel);
    }
}
