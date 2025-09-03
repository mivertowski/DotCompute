// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DotCompute.Backends.CUDA.Native
{

    /// <summary>
    /// P/Invoke wrapper for NVRTC (NVIDIA Runtime Compilation) API with proper memory safety.
    /// </summary>
    public static class NvrtcInterop
    {
        private const string NVRTC_LIBRARY = "nvrtc";

        #region Core NVRTC Functions

        /// <summary>
        /// Gets the NVRTC version.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcVersion(out int major, out int minor);

        /// <summary>
        /// Gets supported GPU architectures.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetSupportedArchs(out IntPtr supportedArchs);

        /// <summary>
        /// Gets the number of supported architectures.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetNumSupportedArchs(out int numArchs);

        /// <summary>
        /// Creates a compilation program from source code.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcCreateProgram(
            out IntPtr prog,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string src,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? name,
            int numHeaders,
            IntPtr headers,  // Pass as IntPtr array instead
            IntPtr includeNames);  // Pass as IntPtr array instead

        /// <summary>
        /// Destroys a compilation program and frees associated resources.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcDestroyProgram(ref IntPtr prog);

        /// <summary>
        /// Compiles the program with specified options.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcCompileProgram(
            IntPtr prog,
            int numOptions,
            IntPtr options);  // Pass as IntPtr array instead

        #endregion

        #region PTX Generation

        /// <summary>
        /// Gets the size of the generated PTX code.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetPTXSize(IntPtr prog, out IntPtr ptxSizeRet);

        /// <summary>
        /// Gets the generated PTX code.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetPTX(IntPtr prog, IntPtr ptx);

        #endregion

        #region Helper Methods for String Array Marshaling

        /// <summary>
        /// Helper method to create a program with proper string array marshaling.
        /// </summary>
        internal static NvrtcResult CreateProgram(
            out IntPtr prog,
            string src,
            string? name = null,
            string[]? headers = null,
            string[]? includeNames = null)
        {
            if (headers == null || includeNames == null || headers.Length == 0)
            {
                return nvrtcCreateProgram(out prog, src, name, 0, IntPtr.Zero, IntPtr.Zero);
            }

            // Marshal headers and include names manually
            var headerPtrs = new IntPtr[headers.Length];
            var includePtrs = new IntPtr[includeNames.Length];
            
            try
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    headerPtrs[i] = Marshal.StringToHGlobalAnsi(headers[i]);
                }
                
                for (int i = 0; i < includeNames.Length; i++)
                {
                    includePtrs[i] = Marshal.StringToHGlobalAnsi(includeNames[i]);
                }

                unsafe
                {
                    fixed (IntPtr* hPtr = headerPtrs)
                    fixed (IntPtr* iPtr = includePtrs)
                    {
                        return nvrtcCreateProgram(out prog, src, name, headers.Length, 
                            new IntPtr(hPtr), new IntPtr(iPtr));
                    }
                }
            }
            finally
            {
                // Clean up allocated strings
                foreach (var ptr in headerPtrs)
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }
                
                foreach (var ptr in includePtrs)
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }
            }
        }

        /// <summary>
        /// Helper method to compile a program with proper string array marshaling.
        /// </summary>
        internal static NvrtcResult CompileProgram(IntPtr prog, string[]? options = null)
        {
            if (options == null || options.Length == 0)
            {
                return nvrtcCompileProgram(prog, 0, IntPtr.Zero);
            }

            // Marshal options manually
            var optionPtrs = new IntPtr[options.Length];
            
            try
            {
                for (int i = 0; i < options.Length; i++)
                {
                    optionPtrs[i] = Marshal.StringToHGlobalAnsi(options[i]);
                }

                unsafe
                {
                    fixed (IntPtr* oPtr = optionPtrs)
                    {
                        return nvrtcCompileProgram(prog, options.Length, new IntPtr(oPtr));
                    }
                }
            }
            finally
            {
                // Clean up allocated strings
                foreach (var ptr in optionPtrs)
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }
            }
        }

        #endregion

        #region CUBIN Generation (CUDA 11.2+)

        /// <summary>
        /// Gets the size of the generated CUBIN binary.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetCUBINSize(IntPtr prog, out IntPtr cubinSizeRet);

        /// <summary>
        /// Gets the generated CUBIN binary.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetCUBIN(IntPtr prog, IntPtr cubin);

        #endregion

        #region LTO (Link Time Optimization) Support (CUDA 11.4+)

        /// <summary>
        /// Gets the size of the LTO IR (Intermediate Representation).
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetLTOIRSize(IntPtr prog, out IntPtr ltoIRSizeRet);

        /// <summary>
        /// Gets the LTO IR (Intermediate Representation).
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetLTOIR(IntPtr prog, IntPtr ltoIR);

        #endregion

        #region OptiX Integration (CUDA 11.7+)

        /// <summary>
        /// Gets the size of the OptiX IR.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetOptiXIRSize(IntPtr prog, out IntPtr optixIRSizeRet);

        /// <summary>
        /// Gets the OptiX IR.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetOptiXIR(IntPtr prog, IntPtr optixIR);

        #endregion

        #region Program Information and Logging

        /// <summary>
        /// Gets the size of the compilation log.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetProgramLogSize(IntPtr prog, out IntPtr logSizeRet);

        /// <summary>
        /// Gets the compilation log.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetProgramLog(IntPtr prog, IntPtr log);

        #endregion

        #region Name Expression Management (for kernel introspection)

        /// <summary>
        /// Adds a name expression to track during compilation.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcAddNameExpression(IntPtr prog,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name_expression);

        /// <summary>
        /// Gets the lowered (mangled) name for a name expression.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern NvrtcResult nvrtcGetLoweredName(IntPtr prog,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name_expression,
            out IntPtr lowered_name);

        #endregion

        #region Error Handling

        /// <summary>
        /// Gets a human-readable description of an NVRTC result code.
        /// </summary>
        [DllImport(NVRTC_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern IntPtr nvrtcGetErrorString(NvrtcResult result);

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the error string for an NVRTC result code.
        /// </summary>
        public static string GetErrorString(NvrtcResult result)
        {
            var ptr = nvrtcGetErrorString(result);
            return Marshal.PtrToStringAnsi(ptr) ?? result.ToString();
        }

        /// <summary>
        /// Checks an NVRTC result and throws an exception if it indicates an error.
        /// </summary>
        public static void CheckResult(NvrtcResult result, string operation = "")
        {
            if (result != NvrtcResult.Success)
            {
                var message = string.IsNullOrEmpty(operation)
                    ? $"NVRTC error: {GetErrorString(result)}"
                    : $"NVRTC error during {operation}: {GetErrorString(result)}";
                throw new NvrtcException(message, result);
            }
        }

        /// <summary>
        /// Checks if NVRTC is available on the system.
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                var result = nvrtcVersion(out var major, out var minor);
                return result == NvrtcResult.Success && major >= 11; // Require NVRTC 11.0+
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the NVRTC version information.
        /// </summary>
        public static (int major, int minor) GetVersion()
        {
            try
            {
                var result = nvrtcVersion(out var major, out var minor);
                return result == NvrtcResult.Success ? (major, minor) : (0, 0);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// Gets all supported GPU architectures.
        /// </summary>
        public static int[] GetSupportedArchitectures()
        {
            try
            {
                var result = nvrtcGetNumSupportedArchs(out var numArchs);
                if (result != NvrtcResult.Success || numArchs <= 0)
                {
                    return [];
                }

                result = nvrtcGetSupportedArchs(out var archsPtr);
                if (result != NvrtcResult.Success || archsPtr == IntPtr.Zero)
                {
                    return [];
                }

                var supportedArchs = new int[numArchs];
                Marshal.Copy(archsPtr, supportedArchs, 0, numArchs);
                return supportedArchs;
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Safely gets the compilation log from a program.
        /// </summary>
        public static string GetCompilationLog(IntPtr program)
        {
            try
            {
                var result = nvrtcGetProgramLogSize(program, out var logSize);
                if (result != NvrtcResult.Success || logSize.ToInt64() <= 1)
                {
                    return string.Empty;
                }

                var logBuffer = Marshal.AllocHGlobal(logSize);
                try
                {
                    result = nvrtcGetProgramLog(program, logBuffer);
                    if (result != NvrtcResult.Success)
                    {
                        return string.Empty;
                    }

                    return Marshal.PtrToStringAnsi(logBuffer)?.Trim() ?? string.Empty;
                }
                finally
                {
                    Marshal.FreeHGlobal(logBuffer);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Safely gets PTX code from a compiled program.
        /// </summary>
        public static byte[] GetPtxCode(IntPtr program)
        {
            try
            {
                var result = nvrtcGetPTXSize(program, out var ptxSize);
                CheckResult(result, "getting PTX size");

                var ptxBuffer = Marshal.AllocHGlobal(ptxSize);
                try
                {
                    result = nvrtcGetPTX(program, ptxBuffer);
                    CheckResult(result, "getting PTX code");

                    var ptxBytes = new byte[ptxSize.ToInt64()];
                    Marshal.Copy(ptxBuffer, ptxBytes, 0, (int)ptxSize.ToInt64());
                    return ptxBytes;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptxBuffer);
                }
            }
            catch (NvrtcException)
            {
                throw; // Re-throw NVRTC exceptions
            }
            catch (Exception ex)
            {
                throw new NvrtcException("Failed to retrieve PTX code", NvrtcResult.InternalError, ex);
            }
        }

        /// <summary>
        /// Safely gets CUBIN binary from a compiled program (CUDA 11.2+).
        /// </summary>
        public static byte[] GetCubinCode(IntPtr program)
        {
            try
            {
                var result = nvrtcGetCUBINSize(program, out var cubinSize);
                CheckResult(result, "getting CUBIN size");

                var cubinBuffer = Marshal.AllocHGlobal(cubinSize);
                try
                {
                    result = nvrtcGetCUBIN(program, cubinBuffer);
                    CheckResult(result, "getting CUBIN code");

                    var cubinBytes = new byte[cubinSize.ToInt64()];
                    Marshal.Copy(cubinBuffer, cubinBytes, 0, (int)cubinSize.ToInt64());
                    return cubinBytes;
                }
                finally
                {
                    Marshal.FreeHGlobal(cubinBuffer);
                }
            }
            catch (NvrtcException)
            {
                throw; // Re-throw NVRTC exceptions
            }
            catch (Exception ex)
            {
                throw new NvrtcException("Failed to retrieve CUBIN code", NvrtcResult.InternalError, ex);
            }
        }

        /// <summary>
        /// Safely gets LTO IR from a compiled program (CUDA 11.4+).
        /// </summary>
        public static byte[] GetLtoIr(IntPtr program)
        {
            try
            {
                var result = nvrtcGetLTOIRSize(program, out var ltoIRSize);
                CheckResult(result, "getting LTO IR size");

                var ltoIRBuffer = Marshal.AllocHGlobal(ltoIRSize);
                try
                {
                    result = nvrtcGetLTOIR(program, ltoIRBuffer);
                    CheckResult(result, "getting LTO IR");

                    var ltoIRBytes = new byte[ltoIRSize.ToInt64()];
                    Marshal.Copy(ltoIRBuffer, ltoIRBytes, 0, (int)ltoIRSize.ToInt64());
                    return ltoIRBytes;
                }
                finally
                {
                    Marshal.FreeHGlobal(ltoIRBuffer);
                }
            }
            catch (NvrtcException)
            {
                throw; // Re-throw NVRTC exceptions
            }
            catch (Exception ex)
            {
                throw new NvrtcException("Failed to retrieve LTO IR", NvrtcResult.InternalError, ex);
            }
        }

        /// <summary>
        /// Gets the lowered (mangled) name for a kernel function.
        /// </summary>
        public static string? GetLoweredName(IntPtr program, string nameExpression)
        {
            try
            {
                var result = nvrtcGetLoweredName(program, nameExpression, out var loweredNamePtr);
                if (result != NvrtcResult.Success || loweredNamePtr == IntPtr.Zero)
                {
                    return null;
                }

                return Marshal.PtrToStringAnsi(loweredNamePtr);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Safe handle wrapper for NVRTC program handles to ensure proper resource cleanup.
    /// </summary>
    public sealed class SafeNvrtcProgramHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeNvrtcProgramHandle() : base(true)
        {
        }

        public SafeNvrtcProgramHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var h = handle;
                var result = NvrtcInterop.nvrtcDestroyProgram(ref h);
                handle = IntPtr.Zero;
                return result == NvrtcResult.Success;
            }
            return true;
        }
    }

    /// <summary>
    /// Extended NVRTC result codes including newer additions.
    /// </summary>
    public enum NvrtcResult
    {
        Success = 0,
        OutOfMemory = 1,
        ProgramCreationFailure = 2,
        InvalidInput = 3,
        InvalidProgram = 4,
        InvalidOption = 5,
        Compilation = 6,
        BuiltinOperationFailure = 7,
        NoLoweredNamesBeforeCompilation = 8,
        NoNameExpressionsAfterCompilation = 9,
        CompilationFailure = 10,
        InternalError = 11,
        InvalidType = 12,                    // CUDA 11.1+
        UnsupportedPTXVersion = 13,          // CUDA 11.1+
        InvalidArchitecture = 14,            // CUDA 11.2+
        TimeOut = 15,                        // CUDA 11.6+
        InvalidFeature = 16,                 // CUDA 11.8+
        InvalidCode = 17                     // CUDA 12.0+
    }

    /// <summary>
    /// Enhanced NVRTC compilation exception with additional context.
    /// </summary>
    public class NvrtcException : Exception
    {
        /// <summary>
        /// Gets the NVRTC result code.
        /// </summary>
        public NvrtcResult ResultCode { get; }

        /// <summary>
        /// Gets the compilation log if available.
        /// </summary>
        public string? CompilationLog { get; }

        public NvrtcException() : base() { }

        public NvrtcException(string message) : base(message)
        {
            ResultCode = NvrtcResult.InternalError;
        }

        public NvrtcException(string message, Exception innerException) : base(message, innerException)
        {
            ResultCode = NvrtcResult.InternalError;
        }

        public NvrtcException(string message, NvrtcResult resultCode) : base(message)
        {
            ResultCode = resultCode;
        }

        public NvrtcException(string message, NvrtcResult resultCode, Exception innerException)
            : base(message, innerException)
        {
            ResultCode = resultCode;
        }

        public NvrtcException(string message, NvrtcResult resultCode, string? compilationLog)
            : base(message)
        {
            ResultCode = resultCode;
            CompilationLog = compilationLog;
        }

        public NvrtcException(string message, NvrtcResult resultCode, string? compilationLog, Exception innerException)
            : base(message, innerException)
        {
            ResultCode = resultCode;
            CompilationLog = compilationLog;
        }
    }

    /// <summary>
    /// CUDA compute capability utilities with support for latest generations.
    /// </summary>
    public static class ComputeCapabilityExtensions
    {
        /// <summary>
        /// Common compute capabilities with latest GPU generations.
        /// </summary>
        public static class KnownComputeCapabilities
        {
            // Legacy generations
            public static readonly (int major, int minor) Fermi = (2, 0);
            public static readonly (int major, int minor) Kepler_35 = (3, 5);
            public static readonly (int major, int minor) Kepler_37 = (3, 7);

            // Modern generations
            public static readonly (int major, int minor) Maxwell_50 = (5, 0);
            public static readonly (int major, int minor) Maxwell_52 = (5, 2);
            public static readonly (int major, int minor) Maxwell_53 = (5, 3);

            public static readonly (int major, int minor) Pascal_60 = (6, 0);
            public static readonly (int major, int minor) Pascal_61 = (6, 1);
            public static readonly (int major, int minor) Pascal_62 = (6, 2);

            public static readonly (int major, int minor) Volta_70 = (7, 0);
            public static readonly (int major, int minor) Turing_75 = (7, 5);

            // Latest generations
            public static readonly (int major, int minor) Ampere_80 = (8, 0);
            public static readonly (int major, int minor) Ampere_86 = (8, 6);
            public static readonly (int major, int minor) Ada_89 = (8, 9);      // RTX 2000 Ada Generation

            public static readonly (int major, int minor) Hopper_90 = (9, 0);   // H100
        }

        /// <summary>
        /// Checks if a compute capability supports specific features.
        /// </summary>
        public static bool SupportsFeature(int major, int minor, ComputeFeature feature)
        {
            var capability = major * 10 + minor;

            return feature switch
            {
                ComputeFeature.DoublePrecision => capability >= 13, // Fermi 1.3+
                ComputeFeature.DynamicParallelism => capability >= 35, // Kepler 3.5+
                ComputeFeature.UnifiedMemory => capability >= 60, // Pascal 6.0+
                ComputeFeature.CooperativeGroups => capability >= 60, // Pascal 6.0+
                ComputeFeature.TensorCores => capability >= 70, // Volta 7.0+
                ComputeFeature.BFloat16 => capability >= 80, // Ampere 8.0+
                ComputeFeature.Float16x2 => capability >= 53, // Maxwell 5.3+
                ComputeFeature.Int8TensorCores => capability >= 61, // Pascal 6.1+
                ComputeFeature.SparseMatrices => capability >= 80, // Ampere 8.0+
                ComputeFeature.MMA => capability >= 70, // Volta 7.0+
                ComputeFeature.AsyncCopy => capability >= 80, // Ampere 8.0+
                ComputeFeature.ClusterApi => capability >= 90, // Hopper 9.0+
                ComputeFeature.FP8 => capability >= 89, // Ada 8.9+
                _ => false
            };
        }

        /// <summary>
        /// Gets the recommended block size for a given compute capability.
        /// </summary>
        public static int GetRecommendedBlockSize(int major, int minor)
        {
            return (major, minor) switch
            {
                // Latest generations - optimize for larger blocks
                ( >= 8, >= 9) => 512,  // Ada and beyond
                ( >= 8, >= 0) => 512,  // Ampere
                ( >= 7, >= 0) => 256,  // Volta/Turing
                ( >= 6, >= 0) => 256,  // Pascal
                ( >= 5, >= 0) => 128,  // Maxwell
                ( >= 3, >= 5) => 128,  // Kepler
                _ => 64               // Older architectures
            };
        }

        /// <summary>
        /// Gets the maximum shared memory per block for a compute capability.
        /// </summary>
        public static int GetMaxSharedMemoryPerBlock(int major, int minor)
        {
            return (major, minor) switch
            {
                ( >= 9, >= 0) => 163840,  // Hopper: 160KB
                ( >= 8, >= 9) => 102400,  // Ada: 100KB
                ( >= 8, >= 0) => 98304,   // Ampere: 96KB
                ( >= 7, >= 0) => 98304,   // Volta/Turing: 96KB
                ( >= 6, >= 0) => 65536,   // Pascal: 64KB
                ( >= 5, >= 0) => 65536,   // Maxwell: 64KB
                ( >= 3, >= 0) => 49152,   // Kepler: 48KB
                _ => 16384               // Older: 16KB
            };
        }
    }

    /// <summary>
    /// Compute capability features for feature detection.
    /// </summary>
    public enum ComputeFeature
    {
        DoublePrecision,
        DynamicParallelism,
        UnifiedMemory,
        CooperativeGroups,
        TensorCores,
        BFloat16,
        Float16x2,
        Int8TensorCores,
        SparseMatrices,
        MMA,
        AsyncCopy,
        ClusterApi,
        FP8
    }
}
