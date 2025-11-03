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

        static CudaRuntimeCore()
        {
            // Help .NET find CUDA libraries on Linux
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Dynamically detect CUDA installation
                var cudaPath = DetectCudaInstallation();
                if (!string.IsNullOrEmpty(cudaPath))
                {
                    var currentPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
                    var cudaLib64 = Path.Combine(cudaPath, "lib64");

                    if (!currentPath.Contains(cudaLib64, StringComparison.OrdinalIgnoreCase))
                    {
                        // Security: Validate path before modifying environment
                        if (Directory.Exists(cudaLib64) && Path.IsPathFullyQualified(cudaLib64))
                        {
                            Environment.SetEnvironmentVariable("LD_LIBRARY_PATH",
                                $"{cudaLib64}:{currentPath}");
                            // Use Debug output instead of Console in production
                            System.Diagnostics.Debug.WriteLine($"Added CUDA library path: {cudaLib64}");
                        }
                    }
                }
            }

            // Install custom resolver for better library loading
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    NativeLibrary.SetDllImportResolver(typeof(CudaRuntimeCore).Assembly, (libraryName, assembly, searchPath) =>
                    {
                        if (libraryName == CUDA_LIBRARY)
                        {
                            var candidatePaths = new[]
                            {
                                "libcudart.so",
                                "libcudart.so.12",
                                "libcudart.so.11",
                                "/usr/local/cuda/lib64/libcudart.so",
                                "/opt/cuda/lib64/libcudart.so"
                            };

                            foreach (var path in candidatePaths)
                            {
                                if (NativeLibrary.TryLoad(path, out var handle))
                                {

                                    return handle;
                                }
                            }
                        }
                        return IntPtr.Zero;
                    });
                }
                catch
                {
                    // Resolver installation failed, continue with default behavior
                }
            }
        }

        private static string DetectCudaInstallation()
        {
            var candidatePaths = new[]
            {
                "/usr/local/cuda",
                "/opt/cuda",
                Environment.GetEnvironmentVariable("CUDA_PATH") ?? ""
            };

            return candidatePaths.FirstOrDefault(path =>

                !string.IsNullOrEmpty(path) && Directory.Exists(Path.Combine(path, "lib64"))) ?? "";
        }

        #region Runtime Management

        /// <summary>
        /// Initialize the CUDA runtime.
        /// </summary>
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaFree(IntPtr devPtr);

        /// <summary>
        /// Get the last error from a CUDA runtime call.
        /// </summary>
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGetLastError();

        /// <summary>
        /// Get the error string for a CUDA error.
        /// </summary>
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial IntPtr cudaGetErrorString(CudaError error);

        /// <summary>
        /// Get the name of a CUDA error.
        /// </summary>
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial IntPtr cudaGetErrorName(CudaError error);

        #endregion

        #region Driver API Integration

        /// <summary>
        /// Initialize the CUDA driver API.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuInit(uint flags);

        /// <summary>
        /// Get CUDA driver version.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuDriverGetVersion(out int driverVersion);

        #endregion

        #region Module Management

        /// <summary>
        /// Load a module from PTX code.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuModuleLoadData(out IntPtr module, IntPtr image);

        /// <summary>
        /// Load a module from PTX code with options.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuModuleLoadDataEx(
            out IntPtr module,
            IntPtr image,
            uint numOptions,
            IntPtr[] options,
            IntPtr[] optionValues);

        /// <summary>
        /// Get a function handle from a module.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - UTF-8 marshaling is explicitly specified
        internal static partial CudaError cuModuleGetFunction(
            out IntPtr hfunc,
            IntPtr hmod,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#pragma warning restore CA2101

        /// <summary>
        /// Unload a module.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuModuleUnload(IntPtr hmod);

        #endregion

        #region Kernel Launch

        /// <summary>
        /// Launch a kernel function.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuLaunchKernel(
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
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuLaunchCooperativeKernel(
            IntPtr f,
            uint gridDimX, uint gridDimY, uint gridDimZ,
            uint blockDimX, uint blockDimY, uint blockDimZ,
            uint sharedMemBytes,
            IntPtr hStream,
            IntPtr[] kernelParams);

        #endregion

        #region Context Management

        /// <summary>
        /// Create a CUDA context.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxCreate(out IntPtr pctx, uint flags, int dev);

        /// <summary>
        /// Destroy a CUDA context.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxDestroy(IntPtr ctx);

        /// <summary>
        /// Push a context onto the current CPU thread.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxPushCurrent(IntPtr ctx);

        /// <summary>
        /// Pop the current context from the current CPU thread.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxPopCurrent(out IntPtr pctx);

        /// <summary>
        /// Set the current context.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxSetCurrent(IntPtr ctx);

        /// <summary>
        /// Get the current context.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxGetCurrent(out IntPtr pctx);

        /// <summary>
        /// Synchronize the current context.
        /// </summary>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxSynchronize();

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
