// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;

#if !DOTCOMPUTE_NO_OPENCL

namespace DotCompute.Core.Kernels;

/// <summary>
/// P/Invoke definitions and wrappers for OpenCL runtime API calls.
/// </summary>
internal static class OpenCLInterop
{
    private const string OpenCLLibrary = "OpenCL";

    #region OpenCL Constants

    public const uint CL_SUCCESS = 0;
    public const uint CL_DEVICE_NOT_FOUND = unchecked((uint)-1);
    public const uint CL_DEVICE_NOT_AVAILABLE = unchecked((uint)-2);
    public const uint CL_COMPILER_NOT_AVAILABLE = unchecked((uint)-3);
    public const uint CL_MEM_OBJECT_ALLOCATION_FAILURE = unchecked((uint)-4);
    public const uint CL_OUT_OF_RESOURCES = unchecked((uint)-5);
    public const uint CL_OUT_OF_HOST_MEMORY = unchecked((uint)-6);
    public const uint CL_PROFILING_INFO_NOT_AVAILABLE = unchecked((uint)-7);
    public const uint CL_MEM_COPY_OVERLAP = unchecked((uint)-8);
    public const uint CL_IMAGE_FORMAT_MISMATCH = unchecked((uint)-9);
    public const uint CL_IMAGE_FORMAT_NOT_SUPPORTED = unchecked((uint)-10);
    public const uint CL_BUILD_PROGRAM_FAILURE = unchecked((uint)-11);
    public const uint CL_MAP_FAILURE = unchecked((uint)-12);

    // Device types
    public const ulong CL_DEVICE_TYPE_DEFAULT = 1 << 0;
    public const ulong CL_DEVICE_TYPE_CPU = 1 << 1;
    public const ulong CL_DEVICE_TYPE_GPU = 1 << 2;
    public const ulong CL_DEVICE_TYPE_ACCELERATOR = 1 << 3;
    public const ulong CL_DEVICE_TYPE_ALL = 0xFFFFFFFF;

    // Memory flags
    public const ulong CL_MEM_READ_WRITE = 1 << 0;
    public const ulong CL_MEM_WRITE_ONLY = 1 << 1;
    public const ulong CL_MEM_READ_ONLY = 1 << 2;
    public const ulong CL_MEM_USE_HOST_PTR = 1 << 3;
    public const ulong CL_MEM_ALLOC_HOST_PTR = 1 << 4;
    public const ulong CL_MEM_COPY_HOST_PTR = 1 << 5;

    // Event command execution status
    public const int CL_QUEUED = 0x3;
    public const int CL_SUBMITTED = 0x2;
    public const int CL_RUNNING = 0x1;
    public const int CL_COMPLETE = 0x0;

    // Program info
    public const uint CL_PROGRAM_BUILD_STATUS = 0x1181;
    public const uint CL_PROGRAM_BUILD_LOG = 0x1183;
    public const uint CL_PROGRAM_BINARIES = 0x1166;
    public const uint CL_PROGRAM_BINARY_SIZES = 0x1165;

    // Build status
    public const int CL_BUILD_SUCCESS = 0;
    public const int CL_BUILD_NONE = -1;
    public const int CL_BUILD_ERROR = -2;
    public const int CL_BUILD_IN_PROGRESS = -3;

    // Device info
    public const uint CL_DEVICE_MAX_WORK_GROUP_SIZE = 0x1004;
    public const uint CL_DEVICE_MAX_WORK_ITEM_SIZES = 0x1005;
    public const uint CL_DEVICE_MAX_WORK_ITEM_DIMENSIONS = 0x1003;

    // Kernel work group info
    public const uint CL_KERNEL_WORK_GROUP_SIZE = 0x11B0;
    public const uint CL_KERNEL_PREFERRED_WORK_GROUP_SIZE_MULTIPLE = 0x11B3;

    // Profiling info
    public const uint CL_PROFILING_COMMAND_QUEUED = 0x1280;
    public const uint CL_PROFILING_COMMAND_SUBMIT = 0x1281;
    public const uint CL_PROFILING_COMMAND_START = 0x1282;
    public const uint CL_PROFILING_COMMAND_END = 0x1283;

    // Command queue properties
    public const ulong CL_QUEUE_PROFILING_ENABLE = 1 << 1;

    #endregion

    #region Platform API

    [DllImport(OpenCLLibrary, EntryPoint = "clGetPlatformIDs")]
    public static extern uint GetPlatformIDs(
        uint num_entries,
        [Out] IntPtr[]? platforms,
        out uint num_platforms);

    #endregion

    #region Device API

    [DllImport(OpenCLLibrary, EntryPoint = "clGetDeviceIDs")]
    public static extern uint GetDeviceIDs(
        IntPtr platform,
        ulong device_type,
        uint num_entries,
        [Out] IntPtr[]? devices,
        out uint num_devices);

    [DllImport(OpenCLLibrary, EntryPoint = "clGetDeviceInfo")]
    public static extern uint GetDeviceInfo(
        IntPtr device,
        uint param_name,
        UIntPtr param_value_size,
        IntPtr param_value,
        out UIntPtr param_value_size_ret);

    #endregion

    #region Context API

    [DllImport(OpenCLLibrary, EntryPoint = "clCreateContext")]
    public static extern IntPtr CreateContext(
        IntPtr properties,
        uint num_devices,
        IntPtr[] devices,
        IntPtr pfn_notify,
        IntPtr user_data,
        out uint errcode_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clReleaseContext")]
    public static extern uint ReleaseContext(IntPtr context);

    #endregion

    #region Command Queue API

    [DllImport(OpenCLLibrary, EntryPoint = "clCreateCommandQueue")]
    public static extern IntPtr CreateCommandQueue(
        IntPtr context,
        IntPtr device,
        ulong properties,
        out uint errcode_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clReleaseCommandQueue")]
    public static extern uint ReleaseCommandQueue(IntPtr command_queue);

    [DllImport(OpenCLLibrary, EntryPoint = "clFinish")]
    public static extern uint Finish(IntPtr command_queue);

    [DllImport(OpenCLLibrary, EntryPoint = "clFlush")]
    public static extern uint Flush(IntPtr command_queue);

    #endregion

    #region Program API

    [DllImport(OpenCLLibrary, EntryPoint = "clCreateProgramWithSource")]
    public static extern IntPtr CreateProgramWithSource(
        IntPtr context,
        uint count,
        string[] strings,
        UIntPtr[]? lengths,
        out uint errcode_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clBuildProgram")]
    public static extern uint BuildProgram(
        IntPtr program,
        uint num_devices,
        IntPtr[]? device_list,
        string? options,
        IntPtr pfn_notify,
        IntPtr user_data);

    [DllImport(OpenCLLibrary, EntryPoint = "clGetProgramBuildInfo")]
    public static extern uint GetProgramBuildInfo(
        IntPtr program,
        IntPtr device,
        uint param_name,
        UIntPtr param_value_size,
        IntPtr param_value,
        out UIntPtr param_value_size_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clGetProgramInfo")]
    public static extern uint GetProgramInfo(
        IntPtr program,
        uint param_name,
        UIntPtr param_value_size,
        IntPtr param_value,
        out UIntPtr param_value_size_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clReleaseProgram")]
    public static extern uint ReleaseProgram(IntPtr program);

    #endregion

    #region Kernel API

    [DllImport(OpenCLLibrary, EntryPoint = "clCreateKernel")]
    public static extern IntPtr CreateKernel(
        IntPtr program,
        string kernel_name,
        out uint errcode_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clSetKernelArg")]
    public static extern uint SetKernelArg(
        IntPtr kernel,
        uint arg_index,
        UIntPtr arg_size,
        IntPtr arg_value);

    [DllImport(OpenCLLibrary, EntryPoint = "clGetKernelWorkGroupInfo")]
    public static extern uint GetKernelWorkGroupInfo(
        IntPtr kernel,
        IntPtr device,
        uint param_name,
        UIntPtr param_value_size,
        IntPtr param_value,
        out UIntPtr param_value_size_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clReleaseKernel")]
    public static extern uint ReleaseKernel(IntPtr kernel);

    #endregion

    #region Memory API

    [DllImport(OpenCLLibrary, EntryPoint = "clCreateBuffer")]
    public static extern IntPtr CreateBuffer(
        IntPtr context,
        ulong flags,
        UIntPtr size,
        IntPtr host_ptr,
        out uint errcode_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clEnqueueWriteBuffer")]
    public static extern uint EnqueueWriteBuffer(
        IntPtr command_queue,
        IntPtr buffer,
        uint blocking_write,
        UIntPtr offset,
        UIntPtr size,
        IntPtr ptr,
        uint num_events_in_wait_list,
        IntPtr[]? event_wait_list,
        out IntPtr eventHandle);

    [DllImport(OpenCLLibrary, EntryPoint = "clEnqueueReadBuffer")]
    public static extern uint EnqueueReadBuffer(
        IntPtr command_queue,
        IntPtr buffer,
        uint blocking_read,
        UIntPtr offset,
        UIntPtr size,
        IntPtr ptr,
        uint num_events_in_wait_list,
        IntPtr[]? event_wait_list,
        out IntPtr eventHandle);

    [DllImport(OpenCLLibrary, EntryPoint = "clReleaseMemObject")]
    public static extern uint ReleaseMemObject(IntPtr memobj);

    #endregion

    #region Execution API

    [DllImport(OpenCLLibrary, EntryPoint = "clEnqueueNDRangeKernel")]
    public static extern uint EnqueueNDRangeKernel(
        IntPtr command_queue,
        IntPtr kernel,
        uint work_dim,
        UIntPtr[]? global_work_offset,
        UIntPtr[] global_work_size,
        UIntPtr[]? local_work_size,
        uint num_events_in_wait_list,
        IntPtr[]? event_wait_list,
        out IntPtr eventHandle);

    #endregion

    #region Event API

    [DllImport(OpenCLLibrary, EntryPoint = "clWaitForEvents")]
    public static extern uint WaitForEvents(
        uint num_events,
        IntPtr[] event_list);

    [DllImport(OpenCLLibrary, EntryPoint = "clGetEventProfilingInfo")]
    public static extern uint GetEventProfilingInfo(
        IntPtr eventHandle,
        uint param_name,
        UIntPtr param_value_size,
        IntPtr param_value,
        out UIntPtr param_value_size_ret);

    [DllImport(OpenCLLibrary, EntryPoint = "clReleaseEvent")]
    public static extern uint ReleaseEvent(IntPtr eventHandle);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets available OpenCL platforms.
    /// </summary>
    public static IntPtr[] GetAvailablePlatforms()
    {
        if (!IsOpenCLAvailable())
            return Array.Empty<IntPtr>();

        try
        {
            uint numPlatforms;
            var result = GetPlatformIDs(0, null, out numPlatforms);
            
            if (result != CL_SUCCESS || numPlatforms == 0)
                return Array.Empty<IntPtr>();

            var platforms = new IntPtr[numPlatforms];
            result = GetPlatformIDs(numPlatforms, platforms, out _);
            
            return result == CL_SUCCESS ? platforms : Array.Empty<IntPtr>();
        }
        catch
        {
            return Array.Empty<IntPtr>();
        }
    }

    /// <summary>
    /// Gets available OpenCL devices for a platform.
    /// </summary>
    public static IntPtr[] GetAvailableDevices(IntPtr platform, ulong deviceType = CL_DEVICE_TYPE_ALL)
    {
        if (!IsOpenCLAvailable())
            return Array.Empty<IntPtr>();

        try
        {
            uint numDevices;
            var result = GetDeviceIDs(platform, deviceType, 0, null, out numDevices);
            
            if (result != CL_SUCCESS || numDevices == 0)
                return Array.Empty<IntPtr>();

            var devices = new IntPtr[numDevices];
            result = GetDeviceIDs(platform, deviceType, numDevices, devices, out _);
            
            return result == CL_SUCCESS ? devices : Array.Empty<IntPtr>();
        }
        catch
        {
            return Array.Empty<IntPtr>();
        }
    }

    /// <summary>
    /// Gets device information as a UIntPtr value.
    /// </summary>
    public static UIntPtr GetDeviceInfoUIntPtr(IntPtr device, uint paramName)
    {
        if (!IsOpenCLAvailable())
            return UIntPtr.Zero;

        try
        {
            var buffer = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                var result = GetDeviceInfo(device, paramName, (UIntPtr)IntPtr.Size, buffer, out _);
                return result == CL_SUCCESS ? (UIntPtr)Marshal.ReadIntPtr(buffer) : UIntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return UIntPtr.Zero;
        }
    }

    /// <summary>
    /// Gets device information as an array of UIntPtr values.
    /// </summary>
    public static UIntPtr[] GetDeviceInfoUIntPtrArray(IntPtr device, uint paramName, int maxElements)
    {
        if (!IsOpenCLAvailable())
            return Array.Empty<UIntPtr>();

        try
        {
            var bufferSize = IntPtr.Size * maxElements;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                var result = GetDeviceInfo(device, paramName, (UIntPtr)bufferSize, buffer, out var actualSize);
                if (result != CL_SUCCESS)
                    return Array.Empty<UIntPtr>();

                var elementCount = (int)actualSize.ToUInt64() / IntPtr.Size;
                var values = new UIntPtr[elementCount];
                
                for (int i = 0; i < elementCount; i++)
                {
                    values[i] = (UIntPtr)Marshal.ReadIntPtr(buffer, i * IntPtr.Size);
                }
                
                return values;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return Array.Empty<UIntPtr>();
        }
    }

    /// <summary>
    /// Gets kernel work group information as a UIntPtr value.
    /// </summary>
    public static UIntPtr GetKernelWorkGroupInfoUIntPtr(IntPtr kernel, IntPtr device, uint paramName)
    {
        if (!IsOpenCLAvailable())
            return UIntPtr.Zero;

        try
        {
            var buffer = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                var result = GetKernelWorkGroupInfo(kernel, device, paramName, (UIntPtr)IntPtr.Size, buffer, out _);
                return result == CL_SUCCESS ? (UIntPtr)Marshal.ReadIntPtr(buffer) : UIntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return UIntPtr.Zero;
        }
    }

    /// <summary>
    /// Gets program build log.
    /// </summary>
    public static string GetProgramBuildLog(IntPtr program, IntPtr device)
    {
        if (!IsOpenCLAvailable())
            return "OpenCL not available";

        try
        {
            // First get the size of the build log
            var result = GetProgramBuildInfo(program, device, CL_PROGRAM_BUILD_LOG, UIntPtr.Zero, IntPtr.Zero, out var logSize);
            if (result != CL_SUCCESS || logSize.ToUInt64() == 0)
                return "No build log available";

            // Allocate buffer and get the log
            var buffer = Marshal.AllocHGlobal((int)logSize.ToUInt64());
            try
            {
                result = GetProgramBuildInfo(program, device, CL_PROGRAM_BUILD_LOG, logSize, buffer, out _);
                if (result != CL_SUCCESS)
                    return "Failed to retrieve build log";

                return Marshal.PtrToStringAnsi(buffer) ?? "Empty build log";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            return $"Error retrieving build log: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets program build status.
    /// </summary>
    public static int GetProgramBuildStatus(IntPtr program, IntPtr device)
    {
        if (!IsOpenCLAvailable())
            return CL_BUILD_ERROR;

        try
        {
            var buffer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                var result = GetProgramBuildInfo(program, device, CL_PROGRAM_BUILD_STATUS, (UIntPtr)sizeof(int), buffer, out _);
                return result == CL_SUCCESS ? Marshal.ReadInt32(buffer) : CL_BUILD_ERROR;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return CL_BUILD_ERROR;
        }
    }

    /// <summary>
    /// Gets program binary.
    /// </summary>
    public static byte[] GetProgramBinary(IntPtr program)
    {
        if (!IsOpenCLAvailable())
            return Array.Empty<byte>();

        try
        {
            // Get binary sizes
            var result = GetProgramInfo(program, CL_PROGRAM_BINARY_SIZES, UIntPtr.Zero, IntPtr.Zero, out var sizeBufferSize);
            if (result != CL_SUCCESS)
                return Array.Empty<byte>();

            var sizeBuffer = Marshal.AllocHGlobal((int)sizeBufferSize.ToUInt64());
            try
            {
                result = GetProgramInfo(program, CL_PROGRAM_BINARY_SIZES, sizeBufferSize, sizeBuffer, out _);
                if (result != CL_SUCCESS)
                    return Array.Empty<byte>();

                var binarySize = (UIntPtr)Marshal.ReadIntPtr(sizeBuffer);
                if (binarySize.ToUInt64() == 0)
                    return Array.Empty<byte>();

                // Get the binary
                var binaryBuffer = Marshal.AllocHGlobal((int)binarySize.ToUInt64());
                var binaryPtrBuffer = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    Marshal.WriteIntPtr(binaryPtrBuffer, binaryBuffer);
                    
                    result = GetProgramInfo(program, CL_PROGRAM_BINARIES, (UIntPtr)IntPtr.Size, binaryPtrBuffer, out _);
                    if (result != CL_SUCCESS)
                        return Array.Empty<byte>();

                    var binary = new byte[binarySize.ToUInt64()];
                    Marshal.Copy(binaryBuffer, binary, 0, binary.Length);
                    return binary;
                }
                finally
                {
                    Marshal.FreeHGlobal(binaryBuffer);
                    Marshal.FreeHGlobal(binaryPtrBuffer);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(sizeBuffer);
            }
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Gets profiling information from an event.
    /// </summary>
    public static ulong GetEventProfilingInfoULong(IntPtr eventHandle, uint paramName)
    {
        if (!IsOpenCLAvailable())
            return 0;

        try
        {
            var buffer = Marshal.AllocHGlobal(sizeof(ulong));
            try
            {
                var result = GetEventProfilingInfo(eventHandle, paramName, (UIntPtr)sizeof(ulong), buffer, out _);
                return result == CL_SUCCESS ? (ulong)Marshal.ReadInt64(buffer) : 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Checks if OpenCL is available on the system.
    /// </summary>
    public static bool IsOpenCLAvailable()
    {
        try
        {
            uint numPlatforms;
            var result = GetPlatformIDs(0, null, out numPlatforms);
            return result == CL_SUCCESS;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Throws an OpenCL exception if the error code indicates failure.
    /// </summary>
    public static void ThrowOnError(uint errorCode, string operation)
    {
        if (errorCode == CL_SUCCESS)
            return;

        var errorName = GetErrorName(errorCode);
        throw new InvalidOperationException($"OpenCL {operation} failed with error {errorName} ({errorCode})");
    }

    /// <summary>
    /// Gets a human-readable error name for an OpenCL error code.
    /// </summary>
    private static string GetErrorName(uint errorCode)
    {
        return errorCode switch
        {
            CL_SUCCESS => "CL_SUCCESS",
            CL_DEVICE_NOT_FOUND => "CL_DEVICE_NOT_FOUND",
            CL_DEVICE_NOT_AVAILABLE => "CL_DEVICE_NOT_AVAILABLE",
            CL_COMPILER_NOT_AVAILABLE => "CL_COMPILER_NOT_AVAILABLE",
            CL_MEM_OBJECT_ALLOCATION_FAILURE => "CL_MEM_OBJECT_ALLOCATION_FAILURE",
            CL_OUT_OF_RESOURCES => "CL_OUT_OF_RESOURCES",
            CL_OUT_OF_HOST_MEMORY => "CL_OUT_OF_HOST_MEMORY",
            CL_PROFILING_INFO_NOT_AVAILABLE => "CL_PROFILING_INFO_NOT_AVAILABLE",
            CL_MEM_COPY_OVERLAP => "CL_MEM_COPY_OVERLAP",
            CL_IMAGE_FORMAT_MISMATCH => "CL_IMAGE_FORMAT_MISMATCH",
            CL_IMAGE_FORMAT_NOT_SUPPORTED => "CL_IMAGE_FORMAT_NOT_SUPPORTED",
            CL_BUILD_PROGRAM_FAILURE => "CL_BUILD_PROGRAM_FAILURE",
            CL_MAP_FAILURE => "CL_MAP_FAILURE",
            _ => $"UNKNOWN_ERROR_{errorCode}"
        };
    }

    #endregion
}

#else

namespace DotCompute.Core.Kernels;

/// <summary>
/// Stub implementation when OpenCL is not available.
/// </summary>
internal static class OpenCLInterop
{
    public const uint CL_SUCCESS = 0;
    public const uint CL_DEVICE_NOT_FOUND = unchecked((uint)-1);
    public const uint CL_BUILD_PROGRAM_FAILURE = unchecked((uint)-11);
    
    public const ulong CL_DEVICE_TYPE_ALL = 0xFFFFFFFF;
    public const ulong CL_QUEUE_PROFILING_ENABLE = 1 << 1;
    
    public const uint CL_DEVICE_MAX_WORK_GROUP_SIZE = 0x1004;
    public const uint CL_DEVICE_MAX_WORK_ITEM_SIZES = 0x1005;
    public const uint CL_KERNEL_PREFERRED_WORK_GROUP_SIZE_MULTIPLE = 0x11B3;
    
    public const uint CL_PROFILING_COMMAND_QUEUED = 0x1280;
    public const uint CL_PROFILING_COMMAND_SUBMIT = 0x1281;
    public const uint CL_PROFILING_COMMAND_START = 0x1282;
    public const uint CL_PROFILING_COMMAND_END = 0x1283;
    
    public const int CL_BUILD_SUCCESS = 0;
    public const uint CL_PROGRAM_BUILD_LOG = 0x1183;
    public const uint CL_PROGRAM_BUILD_STATUS = 0x1181;

    public static bool IsOpenCLAvailable() => false;
    public static IntPtr[] GetAvailablePlatforms() => Array.Empty<IntPtr>();
    public static IntPtr[] GetAvailableDevices(IntPtr platform, ulong deviceType) => Array.Empty<IntPtr>();
    public static void ThrowOnError(uint errorCode, string operation) { }
    
    // Stub implementations for all other methods
    public static IntPtr CreateContext(IntPtr properties, uint numDevices, IntPtr[] devices, IntPtr pfnNotify, IntPtr userData, out uint errcode) 
    { errcode = CL_SUCCESS; return IntPtr.Zero; }
    public static uint ReleaseContext(IntPtr context) => CL_SUCCESS;
    public static IntPtr CreateCommandQueue(IntPtr context, IntPtr device, ulong properties, out uint errcode) 
    { errcode = CL_SUCCESS; return IntPtr.Zero; }
    public static uint ReleaseCommandQueue(IntPtr commandQueue) => CL_SUCCESS;
    public static IntPtr CreateProgramWithSource(IntPtr context, uint count, string[] strings, UIntPtr[]? lengths, out uint errcode) 
    { errcode = CL_SUCCESS; return IntPtr.Zero; }
    public static uint BuildProgram(IntPtr program, uint numDevices, IntPtr[]? deviceList, string? options, IntPtr pfnNotify, IntPtr userData) 
    => CL_SUCCESS;
    public static uint ReleaseProgram(IntPtr program) => CL_SUCCESS;
    public static IntPtr CreateKernel(IntPtr program, string kernelName, out uint errcode) 
    { errcode = CL_SUCCESS; return IntPtr.Zero; }
    public static uint ReleaseKernel(IntPtr kernel) => CL_SUCCESS;
    public static uint SetKernelArg(IntPtr kernel, uint argIndex, UIntPtr argSize, IntPtr argValue) => CL_SUCCESS;
    public static uint EnqueueNDRangeKernel(IntPtr commandQueue, IntPtr kernel, uint workDim, UIntPtr[]? globalWorkOffset, UIntPtr[] globalWorkSize, UIntPtr[]? localWorkSize, uint numEvents, IntPtr[]? eventList, out IntPtr eventHandle) 
    { eventHandle = IntPtr.Zero; return CL_SUCCESS; }
    public static uint WaitForEvents(uint numEvents, IntPtr[] eventList) => CL_SUCCESS;
    public static uint ReleaseEvent(IntPtr eventHandle) => CL_SUCCESS;
    public static UIntPtr GetDeviceInfoUIntPtr(IntPtr device, uint paramName) => UIntPtr.Zero;
    public static UIntPtr[] GetDeviceInfoUIntPtrArray(IntPtr device, uint paramName, int maxElements) => Array.Empty<UIntPtr>();
    public static UIntPtr GetKernelWorkGroupInfoUIntPtr(IntPtr kernel, IntPtr device, uint paramName) => UIntPtr.Zero;
    public static string GetProgramBuildLog(IntPtr program, IntPtr device) => "OpenCL not available";
    public static int GetProgramBuildStatus(IntPtr program, IntPtr device) => CL_BUILD_SUCCESS;
    public static byte[] GetProgramBinary(IntPtr program) => Array.Empty<byte>();
    public static ulong GetEventProfilingInfoULong(IntPtr eventHandle, uint paramName) => 0;
}

#endif