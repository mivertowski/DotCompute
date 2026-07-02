// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Native
{
    /// <summary>
    /// Core CUDA runtime functions including initialization, error handling, and basic operations.
    /// </summary>
    public static partial class CudaRuntimeCore
    {
        private const string CUDA_LIBRARY = "cudart";
#if WINDOWS
        private const string CUDA_DRIVER_LIBRARY = "nvcuda";
#else
        private const string CUDA_DRIVER_LIBRARY = "cuda";
#endif

        // NOTE: this class intentionally has NO static constructor / resolver registration.
        // The assembly-wide native-library resolver lives in CudaRuntime and is registered from a
        // [ModuleInitializer] (CudaRuntime.EnsureCudaResolverRegistered), which runs before the
        // first P/Invoke anywhere in this assembly. Only one DllImportResolver may be registered
        // per assembly — the duplicate this class used to install always threw (swallowed) and
        // was dead code, and its Linux-only gating was the root of GH #182 on Windows.

        #region Runtime Management

        /// <summary>
        /// Initialize the CUDA runtime.
        /// </summary>
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaFree(IntPtr devPtr);

        /// <summary>
        /// Get the last error from a CUDA runtime call.
        /// </summary>
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetLastError();

        /// <summary>
        /// Get the error string for a CUDA error.
        /// </summary>
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern IntPtr cudaGetErrorString(CudaError error);

        /// <summary>
        /// Get the name of a CUDA error.
        /// </summary>
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern IntPtr cudaGetErrorName(CudaError error);

        #endregion

        #region Driver API Integration

        /// <summary>
        /// Initialize the CUDA driver API.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuInit(uint flags);

        /// <summary>
        /// Get CUDA driver version.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuDriverGetVersion(out int driverVersion);

        #endregion

        #region Module Management

        /// <summary>
        /// Load a module from PTX code.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuModuleLoadData(out IntPtr module, IntPtr image);

        /// <summary>
        /// Load a module from PTX code with options.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuModuleLoadDataEx(
            out IntPtr module,
            IntPtr image,
            uint numOptions,
            IntPtr[] options,
            IntPtr[] optionValues);

        /// <summary>
        /// Get a function handle from a module.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - UTF-8 marshaling is explicitly specified
        internal static extern CudaError cuModuleGetFunction(
            out IntPtr hfunc,
            IntPtr hmod,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#pragma warning restore CA2101

        /// <summary>
        /// Unload a module.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuModuleUnload(IntPtr hmod);

        #endregion

        #region Kernel Launch

        /// <summary>
        /// Launch a kernel function.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuLaunchKernel(
            IntPtr f,
            uint gridDimX, uint gridDimY, uint gridDimZ,
            uint blockDimX, uint blockDimY, uint blockDimZ,
            uint sharedMemBytes,
            IntPtr hStream,
            IntPtr[] kernelParams,
            IntPtr[] extra);

        /// <summary>
        /// Launch a cooperative kernel.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuLaunchCooperativeKernel(
            IntPtr f,
            uint gridDimX, uint gridDimY, uint gridDimZ,
            uint blockDimX, uint blockDimY, uint blockDimZ,
            uint sharedMemBytes,
            IntPtr hStream,
            IntPtr kernelParams);

        #endregion

        #region Context Management

        /// <summary>
        /// Create a CUDA context.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxCreate(out IntPtr pctx, uint flags, int dev);

        /// <summary>
        /// Destroy a CUDA context.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxDestroy(IntPtr ctx);

        /// <summary>
        /// Push a context onto the current CPU thread.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxPushCurrent(IntPtr ctx);

        /// <summary>
        /// Pop the current context from the current CPU thread.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxPopCurrent(out IntPtr pctx);

        /// <summary>
        /// Set the current context.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxSetCurrent(IntPtr ctx);

        /// <summary>
        /// Get the current context.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxGetCurrent(out IntPtr pctx);

        /// <summary>
        /// Get the device ordinal for the current context.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxGetDevice(out int device);

        /// <summary>
        /// Synchronize the current context.
        /// </summary>
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxSynchronize();

        #endregion

        #region Utility Functions

        /// <summary>
        /// Helper method to get error string from error code.
        /// </summary>
        public static string GetErrorString(CudaError error)
        {
            var ptr = cudaGetErrorString(error);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? $"Unknown error: {error}" : $"Unknown error: {error}";
        }

        /// <summary>
        /// Helper method to get error name from error code.
        /// </summary>
        public static string GetErrorName(CudaError error)
        {
            var ptr = cudaGetErrorName(error);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? error.ToString() : error.ToString();
        }

        /// <summary>
        /// Check and throw exception for CUDA errors.
        /// </summary>
        public static void CheckCudaError(CudaError error, string operation = "")
        {
            if (error != CudaError.Success)
            {
                var message = string.IsNullOrEmpty(operation)
                    ? $"CUDA error: {GetErrorName(error)} - {GetErrorString(error)}"
                    : $"CUDA error in {operation}: {GetErrorName(error)} - {GetErrorString(error)}";
                throw new CudaException(message, error);
            }
        }

        #endregion
    }
}
