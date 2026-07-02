// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native.Delegates
{
    /// <summary>
    /// Host function callback delegate for CUDA operations
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CudaHostFn(nint userData);

    /// <summary>
    /// CUDA kernel function delegate
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint CudaKernelFunc();

    /// <summary>
    /// Generic CUDA function delegate
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint CudaFunc();

    // NOTE: do not declare a delegate named "CudaEvent" here. A CUevent is an opaque driver
    // HANDLE (nint), not a callback. A delegate by that name silently satisfied the
    // programmaticEvent member of the CudaLaunchAttributeValue union, and — being a managed
    // reference type at a [FieldOffset] — made the CLR refuse to load that struct, which broke
    // reflection over this entire assembly (GH #182).
}
