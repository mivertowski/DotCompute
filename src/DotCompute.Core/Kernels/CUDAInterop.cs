// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Core.Kernels;

#if CUDA_ENABLED || WINDOWS || LINUX

/// <summary>
/// CUDA Driver API P/Invoke definitions for low-level CUDA operations.
/// </summary>
internal static unsafe class CUDAInterop
{
    private const string CudaDriverLib = "nvcuda";
    private const string NvrtcLib = "nvrtc64_120_0"; // CUDA 12.0 NVRTC library

    #region CUDA Driver API Types

    /// <summary>
    /// CUDA result codes.
    /// </summary>
    public enum CUresult : uint
    {
        CUDA_SUCCESS = 0,
        CUDA_ERROR_INVALID_VALUE = 1,
        CUDA_ERROR_OUT_OF_MEMORY = 2,
        CUDA_ERROR_NOT_INITIALIZED = 3,
        CUDA_ERROR_DEINITIALIZED = 4,
        CUDA_ERROR_PROFILER_DISABLED = 5,
        CUDA_ERROR_PROFILER_NOT_INITIALIZED = 6,
        CUDA_ERROR_PROFILER_ALREADY_STARTED = 7,
        CUDA_ERROR_PROFILER_ALREADY_STOPPED = 8,
        CUDA_ERROR_NO_DEVICE = 100,
        CUDA_ERROR_INVALID_DEVICE = 101,
        CUDA_ERROR_INVALID_IMAGE = 200,
        CUDA_ERROR_INVALID_CONTEXT = 201,
        CUDA_ERROR_CONTEXT_ALREADY_CURRENT = 202,
        CUDA_ERROR_MAP_FAILED = 205,
        CUDA_ERROR_UNMAP_FAILED = 206,
        CUDA_ERROR_ARRAY_IS_MAPPED = 207,
        CUDA_ERROR_ALREADY_MAPPED = 208,
        CUDA_ERROR_NO_BINARY_FOR_GPU = 209,
        CUDA_ERROR_ALREADY_ACQUIRED = 210,
        CUDA_ERROR_NOT_MAPPED = 211,
        CUDA_ERROR_NOT_MAPPED_AS_ARRAY = 212,
        CUDA_ERROR_NOT_MAPPED_AS_POINTER = 213,
        CUDA_ERROR_ECC_UNCORRECTABLE = 214,
        CUDA_ERROR_UNSUPPORTED_LIMIT = 215,
        CUDA_ERROR_CONTEXT_ALREADY_IN_USE = 216,
        CUDA_ERROR_PEER_ACCESS_UNSUPPORTED = 217,
        CUDA_ERROR_INVALID_PTX = 218,
        CUDA_ERROR_INVALID_GRAPHICS_CONTEXT = 219,
        CUDA_ERROR_NVLINK_UNCORRECTABLE = 220,
        CUDA_ERROR_JIT_COMPILER_NOT_FOUND = 221,
        CUDA_ERROR_INVALID_SOURCE = 300,
        CUDA_ERROR_FILE_NOT_FOUND = 301,
        CUDA_ERROR_SHARED_OBJECT_SYMBOL_NOT_FOUND = 302,
        CUDA_ERROR_SHARED_OBJECT_INIT_FAILED = 303,
        CUDA_ERROR_OPERATING_SYSTEM = 304,
        CUDA_ERROR_INVALID_HANDLE = 400,
        CUDA_ERROR_NOT_FOUND = 500,
        CUDA_ERROR_NOT_READY = 600,
        CUDA_ERROR_ILLEGAL_ADDRESS = 700,
        CUDA_ERROR_LAUNCH_OUT_OF_RESOURCES = 701,
        CUDA_ERROR_LAUNCH_TIMEOUT = 702,
        CUDA_ERROR_LAUNCH_INCOMPATIBLE_TEXTURING = 703,
        CUDA_ERROR_PEER_ACCESS_ALREADY_ENABLED = 704,
        CUDA_ERROR_PEER_ACCESS_NOT_ENABLED = 705,
        CUDA_ERROR_PRIMARY_CONTEXT_ACTIVE = 708,
        CUDA_ERROR_CONTEXT_IS_DESTROYED = 709,
        CUDA_ERROR_ASSERT = 710,
        CUDA_ERROR_TOO_MANY_PEERS = 711,
        CUDA_ERROR_HOST_MEMORY_ALREADY_REGISTERED = 712,
        CUDA_ERROR_HOST_MEMORY_NOT_REGISTERED = 713,
        CUDA_ERROR_HARDWARE_STACK_ERROR = 714,
        CUDA_ERROR_ILLEGAL_INSTRUCTION = 715,
        CUDA_ERROR_MISALIGNED_ADDRESS = 716,
        CUDA_ERROR_INVALID_ADDRESS_SPACE = 717,
        CUDA_ERROR_INVALID_PC = 718,
        CUDA_ERROR_LAUNCH_FAILED = 719,
        CUDA_ERROR_COOPERATIVE_LAUNCH_TOO_LARGE = 720,
        CUDA_ERROR_NOT_PERMITTED = 800,
        CUDA_ERROR_NOT_SUPPORTED = 801,
        CUDA_ERROR_SYSTEM_NOT_READY = 802,
        CUDA_ERROR_SYSTEM_DRIVER_MISMATCH = 803,
        CUDA_ERROR_COMPAT_NOT_SUPPORTED_ON_DEVICE = 804,
        CUDA_ERROR_STREAM_CAPTURE_UNSUPPORTED = 900,
        CUDA_ERROR_STREAM_CAPTURE_INVALIDATED = 901,
        CUDA_ERROR_STREAM_CAPTURE_MERGE = 902,
        CUDA_ERROR_STREAM_CAPTURE_UNMATCHED = 903,
        CUDA_ERROR_STREAM_CAPTURE_UNJOINED = 904,
        CUDA_ERROR_STREAM_CAPTURE_ISOLATION = 905,
        CUDA_ERROR_STREAM_CAPTURE_IMPLICIT = 906,
        CUDA_ERROR_CAPTURED_EVENT = 907,
        CUDA_ERROR_STREAM_CAPTURE_WRONG_THREAD = 908,
        CUDA_ERROR_TIMEOUT = 909,
        CUDA_ERROR_GRAPH_EXEC_UPDATE_FAILURE = 910,
        CUDA_ERROR_UNKNOWN = 999
    }

    /// <summary>
    /// CUDA device attributes.
    /// </summary>
    public enum CUdevice_attribute : int
    {
        CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK = 1,
        CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_X = 2,
        CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Y = 3,
        CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Z = 4,
        CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_X = 5,
        CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Y = 6,
        CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Z = 7,
        CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_BLOCK = 8,
        CU_DEVICE_ATTRIBUTE_SHARED_MEMORY_PER_BLOCK = 8,
        CU_DEVICE_ATTRIBUTE_TOTAL_CONSTANT_MEMORY = 9,
        CU_DEVICE_ATTRIBUTE_WARP_SIZE = 10,
        CU_DEVICE_ATTRIBUTE_MAX_PITCH = 11,
        CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_BLOCK = 12,
        CU_DEVICE_ATTRIBUTE_REGISTERS_PER_BLOCK = 12,
        CU_DEVICE_ATTRIBUTE_CLOCK_RATE = 13,
        CU_DEVICE_ATTRIBUTE_TEXTURE_ALIGNMENT = 14,
        CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT = 16,
        CU_DEVICE_ATTRIBUTE_KERNEL_EXEC_TIMEOUT = 17,
        CU_DEVICE_ATTRIBUTE_INTEGRATED = 18,
        CU_DEVICE_ATTRIBUTE_CAN_MAP_HOST_MEMORY = 19,
        CU_DEVICE_ATTRIBUTE_COMPUTE_MODE = 20,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE_1D_WIDTH = 21,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE_2D_WIDTH = 22,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE_2D_HEIGHT = 23,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE_3D_WIDTH = 24,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE_3D_HEIGHT = 25,
        CU_DEVICE_ATTRIBUTE_MAXIMUM_TEXTURE_3D_DEPTH = 26,
        CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR = 75,
        CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR = 76,
        CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR = 39
    }

    /// <summary>
    /// CUDA context creation flags.
    /// </summary>
    public enum CUctx_flags : uint
    {
        CU_CTX_SCHED_AUTO = 0x00,
        CU_CTX_SCHED_SPIN = 0x01,
        CU_CTX_SCHED_YIELD = 0x02,
        CU_CTX_SCHED_BLOCKING_SYNC = 0x04,
        CU_CTX_BLOCKING_SYNC = 0x04,
        CU_CTX_SCHED_MASK = 0x07,
        CU_CTX_MAP_HOST = 0x08,
        CU_CTX_LMEM_RESIZE_TO_MAX = 0x10,
        CU_CTX_FLAGS_MASK = 0x1f
    }

    /// <summary>
    /// CUDA event flags.
    /// </summary>
    public enum CUevent_flags : uint
    {
        CU_EVENT_DEFAULT = 0x00,
        CU_EVENT_BLOCKING_SYNC = 0x01,
        CU_EVENT_DISABLE_TIMING = 0x02,
        CU_EVENT_INTERPROCESS = 0x04
    }

    /// <summary>
    /// CUDA stream flags.
    /// </summary>
    public enum CUstream_flags : uint
    {
        CU_STREAM_DEFAULT = 0x0,
        CU_STREAM_NON_BLOCKING = 0x1
    }

    /// <summary>
    /// CUDA function cache configuration.
    /// </summary>
    public enum CUfunc_cache : uint
    {
        CU_FUNC_CACHE_PREFER_NONE = 0x00,
        CU_FUNC_CACHE_PREFER_SHARED = 0x01,
        CU_FUNC_CACHE_PREFER_L1 = 0x02,
        CU_FUNC_CACHE_PREFER_EQUAL = 0x03
    }

    /// <summary>
    /// CUDA memory copy types.
    /// </summary>
    public enum CUmemorytype : uint
    {
        CU_MEMORYTYPE_HOST = 0x01,
        CU_MEMORYTYPE_DEVICE = 0x02,
        CU_MEMORYTYPE_ARRAY = 0x03,
        CU_MEMORYTYPE_UNIFIED = 0x04
    }

    // Handle types
    public struct CUcontext { public IntPtr Pointer; }
    public struct CUdevice { public int Value; }
    public struct CUmodule { public IntPtr Pointer; }
    public struct CUfunction { public IntPtr Pointer; }
    public struct CUstream { public IntPtr Pointer; }
    public struct CUevent { public IntPtr Pointer; }
    public struct CUdeviceptr { public IntPtr Pointer; }

    #endregion

    #region CUDA Driver API Functions

    /// <summary>
    /// Initialize the CUDA driver API.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuInit(uint flags);

    /// <summary>
    /// Get the number of compute-capable devices.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuDeviceGetCount(out int count);

    /// <summary>
    /// Get a handle to a compute device.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuDeviceGet(out CUdevice device, int ordinal);

    /// <summary>
    /// Get device attribute.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuDeviceGetAttribute(out int value, CUdevice_attribute attrib, CUdevice device);

    /// <summary>
    /// Get device name.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuDeviceGetName(byte* name, int len, CUdevice device);

    /// <summary>
    /// Create a CUDA context.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuCtxCreate(out CUcontext context, CUctx_flags flags, CUdevice device);

    /// <summary>
    /// Destroy a CUDA context.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuCtxDestroy(CUcontext context);

    /// <summary>
    /// Set the current CUDA context.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuCtxSetCurrent(CUcontext context);

    /// <summary>
    /// Get the current CUDA context.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuCtxGetCurrent(out CUcontext context);

    /// <summary>
    /// Set function cache configuration.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuCtxSetCacheConfig(CUfunc_cache config);

    /// <summary>
    /// Load a module from a file.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuModuleLoad(out CUmodule module, byte* filename);

    /// <summary>
    /// Load a module from memory (PTX or CUBIN).
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuModuleLoadData(out CUmodule module, void* image);

    /// <summary>
    /// Load a module from memory with options.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuModuleLoadDataEx(out CUmodule module, void* image, uint numOptions, uint* options, void** optionValues);

    /// <summary>
    /// Unload a module.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuModuleUnload(CUmodule module);

    /// <summary>
    /// Get a function handle from a module.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuModuleGetFunction(out CUfunction function, CUmodule module, byte* name);

    /// <summary>
    /// Allocate device memory.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuMemAlloc(out CUdeviceptr dptr, nuint bytesize);

    /// <summary>
    /// Free device memory.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuMemFree(CUdeviceptr dptr);

    /// <summary>
    /// Copy memory from host to device.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuMemcpyHtoD(CUdeviceptr dstDevice, void* srcHost, nuint ByteCount);

    /// <summary>
    /// Copy memory from device to host.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuMemcpyDtoH(void* dstHost, CUdeviceptr srcDevice, nuint ByteCount);

    /// <summary>
    /// Copy memory from device to device.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuMemcpyDtoD(CUdeviceptr dstDevice, CUdeviceptr srcDevice, nuint ByteCount);

    /// <summary>
    /// Copy memory from host to device asynchronously.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuMemcpyHtoDAsync(CUdeviceptr dstDevice, void* srcHost, nuint ByteCount, CUstream hStream);

    /// <summary>
    /// Copy memory from device to host asynchronously.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuMemcpyDtoHAsync(void* dstHost, CUdeviceptr srcDevice, nuint ByteCount, CUstream hStream);

    /// <summary>
    /// Launch a CUDA kernel.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuLaunchKernel(
        CUfunction f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        CUstream hStream,
        void** kernelParams,
        void** extra
    );

    /// <summary>
    /// Create a CUDA stream.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuStreamCreate(out CUstream stream, CUstream_flags flags);

    /// <summary>
    /// Destroy a CUDA stream.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuStreamDestroy(CUstream stream);

    /// <summary>
    /// Synchronize a CUDA stream.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuStreamSynchronize(CUstream stream);

    /// <summary>
    /// Query a CUDA stream.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuStreamQuery(CUstream stream);

    /// <summary>
    /// Create a CUDA event.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuEventCreate(out CUevent @event, CUevent_flags flags);

    /// <summary>
    /// Destroy a CUDA event.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuEventDestroy(CUevent @event);

    /// <summary>
    /// Record a CUDA event.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuEventRecord(CUevent @event, CUstream stream);

    /// <summary>
    /// Synchronize on a CUDA event.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuEventSynchronize(CUevent @event);

    /// <summary>
    /// Query a CUDA event.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuEventQuery(CUevent @event);

    /// <summary>
    /// Get elapsed time between two events.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuEventElapsedTime(out float milliseconds, CUevent start, CUevent end);

    /// <summary>
    /// Get occupancy information for a kernel.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuOccupancyMaxPotentialBlockSize(
        out int minGridSize,
        out int blockSize,
        CUfunction func,
        IntPtr blockSizeToDynamicSMemSize,
        nuint dynamicSMemSize,
        int blockSizeLimit
    );

    /// <summary>
    /// Get error string for a CUDA result.
    /// </summary>
    [DllImport(CudaDriverLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern CUresult cuGetErrorString(CUresult error, out byte* pStr);

    #endregion

    #region NVRTC API Types and Functions

    /// <summary>
    /// NVRTC result codes.
    /// </summary>
    public enum nvrtcResult
    {
        NVRTC_SUCCESS = 0,
        NVRTC_ERROR_OUT_OF_MEMORY = 1,
        NVRTC_ERROR_PROGRAM_CREATION_FAILURE = 2,
        NVRTC_ERROR_INVALID_INPUT = 3,
        NVRTC_ERROR_INVALID_PROGRAM = 4,
        NVRTC_ERROR_INVALID_OPTION = 5,
        NVRTC_ERROR_COMPILATION = 6,
        NVRTC_ERROR_BUILTIN_OPERATION_FAILURE = 7,
        NVRTC_ERROR_NO_NAME_EXPRESSIONS_AFTER_COMPILATION = 8,
        NVRTC_ERROR_NO_LOWERED_NAMES_BEFORE_COMPILATION = 9,
        NVRTC_ERROR_NAME_EXPRESSION_NOT_VALID = 10,
        NVRTC_ERROR_INTERNAL_ERROR = 11
    }

    public struct nvrtcProgram { public IntPtr Pointer; }

    /// <summary>
    /// Create an NVRTC program.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nvrtcResult nvrtcCreateProgram(
        out nvrtcProgram prog,
        byte* src,
        byte* name,
        int numHeaders,
        byte** headers,
        byte** includeNames
    );

    /// <summary>
    /// Destroy an NVRTC program.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nvrtcResult nvrtcDestroyProgram(out nvrtcProgram prog);

    /// <summary>
    /// Compile an NVRTC program.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nvrtcResult nvrtcCompileProgram(nvrtcProgram prog, int numOptions, byte** options);

    /// <summary>
    /// Get the size of the compiled PTX.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nvrtcResult nvrtcGetPTXSize(nvrtcProgram prog, out nuint ptxSizeRet);

    /// <summary>
    /// Get the compiled PTX.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nvrtcResult nvrtcGetPTX(nvrtcProgram prog, byte* ptx);

    /// <summary>
    /// Get the size of the compilation log.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nvrtcResult nvrtcGetProgramLogSize(nvrtcProgram prog, out nuint logSizeRet);

    /// <summary>
    /// Get the compilation log.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern nvrtcResult nvrtcGetProgramLog(nvrtcProgram prog, byte* log);

    /// <summary>
    /// Get error string for an NVRTC result.
    /// </summary>
    [DllImport(NvrtcLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* nvrtcGetErrorString(nvrtcResult result);

    #endregion

    #region Utility Methods

    /// <summary>
    /// Checks if CUDA is available on this system.
    /// </summary>
    public static bool IsCudaAvailable()
    {
        try
        {
            var result = cuInit(0);
            if (result != CUresult.CUDA_SUCCESS)
                return false;

            var deviceCount = 0;
            result = cuDeviceGetCount(out deviceCount);
            return result == CUresult.CUDA_SUCCESS && deviceCount > 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the error message for a CUDA result.
    /// </summary>
    public static string GetCudaErrorString(CUresult result)
    {
        try
        {
            byte* pStr;
            var errorResult = cuGetErrorString(result, out pStr);
            if (errorResult == CUresult.CUDA_SUCCESS && pStr != null)
            {
                return Marshal.PtrToStringAnsi((IntPtr)pStr) ?? result.ToString();
            }
            return result.ToString();
        }
        catch
        {
            return result.ToString();
        }
    }

    /// <summary>
    /// Gets the error message for an NVRTC result.
    /// </summary>
    public static string GetNvrtcErrorString(nvrtcResult result)
    {
        try
        {
            byte* pStr = nvrtcGetErrorString(result);
            if (pStr != null)
            {
                return Marshal.PtrToStringAnsi((IntPtr)pStr) ?? result.ToString();
            }
            return result.ToString();
        }
        catch
        {
            return result.ToString();
        }
    }

    /// <summary>
    /// Throws an exception if the CUDA result is not success.
    /// </summary>
    public static void CheckCudaResult(CUresult result, string operation)
    {
        if (result != CUresult.CUDA_SUCCESS)
        {
            var errorMessage = GetCudaErrorString(result);
            throw new InvalidOperationException($"CUDA operation '{operation}' failed with result {result}: {errorMessage}");
        }
    }

    /// <summary>
    /// Throws an exception if the NVRTC result is not success.
    /// </summary>
    public static void CheckNvrtcResult(nvrtcResult result, string operation)
    {
        if (result != nvrtcResult.NVRTC_SUCCESS)
        {
            var errorMessage = GetNvrtcErrorString(result);
            throw new InvalidOperationException($"NVRTC operation '{operation}' failed with result {result}: {errorMessage}");
        }
    }

    /// <summary>
    /// Converts a managed string to a null-terminated byte array.
    /// </summary>
    public static byte[] StringToNullTerminatedBytes(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var result = new byte[bytes.Length + 1];
        Array.Copy(bytes, result, bytes.Length);
        result[bytes.Length] = 0; // null terminator
        return result;
    }

    #endregion
}

#else

/// <summary>
/// CUDA Driver API stub for platforms without CUDA support.
/// </summary>
internal static unsafe class CUDAInterop
{
    /// <summary>
    /// Stub struct for NVRTC program handle.
    /// </summary>
    public struct nvrtcProgram { public IntPtr Pointer; }

    /// <summary>
    /// Stub struct for CUDA module handle.
    /// </summary>
    public struct CUmodule { public IntPtr Pointer; }

    /// <summary>
    /// Stub enum for NVRTC result codes.
    /// </summary>
    public enum nvrtcResult : uint
    {
        NVRTC_SUCCESS = 0,
        NVRTC_ERROR_OUT_OF_MEMORY = 1,
        NVRTC_ERROR_PROGRAM_CREATION_FAILURE = 2,
        NVRTC_ERROR_INVALID_INPUT = 3,
        NVRTC_ERROR_INVALID_PROGRAM = 4,
        NVRTC_ERROR_INVALID_OPTION = 5,
        NVRTC_ERROR_COMPILATION = 6,
        NVRTC_ERROR_BUILTIN_OPERATION_FAILURE = 7,
        NVRTC_ERROR_NO_NAME_EXPRESSIONS_AFTER_COMPILATION = 8,
        NVRTC_ERROR_NO_LOWERED_NAMES_BEFORE_COMPILATION = 9,
        NVRTC_ERROR_NAME_EXPRESSION_NOT_VALID = 10,
        NVRTC_ERROR_INTERNAL_ERROR = 11
    }

    /// <summary>
    /// Stub enum for CUDA result codes.
    /// </summary>
    public enum CUresult : uint
    {
        CUDA_SUCCESS = 0,
        CUDA_ERROR_INVALID_VALUE = 1,
        CUDA_ERROR_OUT_OF_MEMORY = 2,
        CUDA_ERROR_NOT_INITIALIZED = 3
    }

    /// <summary>
    /// Checks if CUDA is available on this system.
    /// </summary>
    public static bool IsCudaAvailable() => false;

    /// <summary>
    /// Gets the error message for a stub implementation.
    /// </summary>
    public static string GetCudaErrorString(object result) => "CUDA not available on this platform";

    /// <summary>
    /// Gets the error message for a stub implementation.
    /// </summary>
    public static string GetNvrtcErrorString(object result) => "NVRTC not available on this platform";

    /// <summary>
    /// Stub method that throws not supported exception.
    /// </summary>
    public static void CheckCudaResult(object result, string operation)
    {
        throw new NotSupportedException("CUDA is not supported on this platform. Build with CUDA_ENABLED, or run on Windows/Linux with CUDA installed.");
    }

    /// <summary>
    /// Stub method that throws not supported exception.
    /// </summary>
    public static void CheckNvrtcResult(object result, string operation)
    {
        throw new NotSupportedException("NVRTC is not supported on this platform. Build with CUDA_ENABLED, or run on Windows/Linux with CUDA installed.");
    }

    /// <summary>
    /// Converts a managed string to a null-terminated byte array.
    /// </summary>
    public static byte[] StringToNullTerminatedBytes(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var result = new byte[bytes.Length + 1];
        Array.Copy(bytes, result, bytes.Length);
        result[bytes.Length] = 0; // null terminator
        return result;
    }

    // Stub NVRTC methods
    public static nvrtcResult nvrtcCreateProgram(out nvrtcProgram prog, byte* src, byte* name, int numHeaders, byte** headers, byte** includeNames)
    {
        prog = new nvrtcProgram { Pointer = IntPtr.Zero };
        return nvrtcResult.NVRTC_ERROR_PROGRAM_CREATION_FAILURE;
    }

    public static nvrtcResult nvrtcDestroyProgram(out nvrtcProgram prog)
    {
        prog = new nvrtcProgram { Pointer = IntPtr.Zero };
        return nvrtcResult.NVRTC_SUCCESS;
    }

    public static nvrtcResult nvrtcCompileProgram(nvrtcProgram prog, int numOptions, byte** options)
    {
        return nvrtcResult.NVRTC_ERROR_COMPILATION;
    }

    public static nvrtcResult nvrtcGetPTXSize(nvrtcProgram prog, out nuint ptxSizeRet)
    {
        ptxSizeRet = 0;
        return nvrtcResult.NVRTC_ERROR_INVALID_PROGRAM;
    }

    public static nvrtcResult nvrtcGetPTX(nvrtcProgram prog, byte* ptx)
    {
        return nvrtcResult.NVRTC_ERROR_INVALID_PROGRAM;
    }

    public static nvrtcResult nvrtcGetProgramLogSize(nvrtcProgram prog, out nuint logSizeRet)
    {
        logSizeRet = 0;
        return nvrtcResult.NVRTC_ERROR_INVALID_PROGRAM;
    }

    public static nvrtcResult nvrtcGetProgramLog(nvrtcProgram prog, byte* log)
    {
        return nvrtcResult.NVRTC_ERROR_INVALID_PROGRAM;
    }

    // Stub CUDA Driver methods
    public static CUresult cuModuleLoadData(out CUmodule module, byte* image)
    {
        module = new CUmodule { Pointer = IntPtr.Zero };
        return CUresult.CUDA_ERROR_NOT_INITIALIZED;
    }

    public static CUresult cuModuleUnload(CUmodule hmod)
    {
        return CUresult.CUDA_SUCCESS;
    }
}

#endif