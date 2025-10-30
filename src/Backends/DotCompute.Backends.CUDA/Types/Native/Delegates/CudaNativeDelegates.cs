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

    /// <summary>
    /// CUDA event delegate
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint CudaEvent();
}
