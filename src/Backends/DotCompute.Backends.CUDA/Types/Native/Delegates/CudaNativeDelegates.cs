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

    // NOTE: do not declare delegates named after CUDA HANDLE types here (CudaEvent,
    // CudaKernelFunc, CudaFunc, ...). CUevent/cudaFunction_t/kernel "func" parameters are opaque
    // driver handles (nint), not callbacks. Delegates by those names silently satisfied handle
    // slots in structs and P/Invokes: a delegate at a [FieldOffset] made the CLR refuse to load
    // CudaLaunchAttributeValue — breaking reflection over this entire assembly (GH #182) — and a
    // delegate where native expects `const void* func` marshals as a host callback thunk the
    // driver would try to execute as GPU code.
}
