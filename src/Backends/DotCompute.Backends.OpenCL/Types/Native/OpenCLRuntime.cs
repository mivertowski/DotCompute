// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// P/Invoke declarations for OpenCL runtime functions.
/// Provides low-level access to OpenCL native library.
/// </summary>
internal static class OpenCLRuntime
{
    private const string LibraryName = "OpenCL";

    /// <summary>
    /// Get available OpenCL platforms.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetPlatformIDs(
        uint numEntries,
        [Out] nint[]? platforms,
        out uint numPlatforms);

    /// <summary>
    /// Get platform information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetPlatformInfo(
        nint platform,
        PlatformInfo paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Get available devices for a platform.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetDeviceIDs(
        nint platform,
        DeviceType deviceType,
        uint numEntries,
        [Out] nint[]? devices,
        out uint numDevices);

    /// <summary>
    /// Get device information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetDeviceInfo(
        nint device,
        DeviceInfo paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Create OpenCL context.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nint clCreateContext(
        [In] nint[]? properties,
        uint numDevices,
        [In] nint[] devices,
        nint pfnNotify,
        nint userData,
        out OpenCLError errcodeRet);

    /// <summary>
    /// Release context reference.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clReleaseContext(nint context);

    /// <summary>
    /// Create command queue.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nint clCreateCommandQueue(
        nint context,
        nint device,
        ulong properties,
        out OpenCLError errcodeRet);

    /// <summary>
    /// Release command queue reference.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clReleaseCommandQueue(nint commandQueue);

    /// <summary>
    /// Create buffer object.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nint clCreateBuffer(
        nint context,
        MemoryFlags flags,
        nuint size,
        nint hostPtr,
        out OpenCLError errcodeRet);

    /// <summary>
    /// Release memory object reference.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clReleaseMemObject(nint memobj);

    /// <summary>
    /// Create program from source.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nint clCreateProgramWithSource(
        nint context,
        uint count,
        [In] string[] strings,
        [In] nuint[]? lengths,
        out OpenCLError errcodeRet);

    /// <summary>
    /// Build program.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "OpenCL API uses ANSI C strings, CharSet.Ansi is appropriate")]
    internal static extern OpenCLError clBuildProgram(
        nint program,
        uint numDevices,
        [In] nint[]? deviceList,
        string? options,
        nint pfnNotify,
        nint userData);

    /// <summary>
    /// Get program build information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetProgramBuildInfo(
        nint program,
        nint device,
        uint paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Release program reference.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clReleaseProgram(nint program);

    /// <summary>
    /// Create kernel object.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "OpenCL API uses ANSI C strings, CharSet.Ansi is appropriate")]
    internal static extern nint clCreateKernel(
        nint program,
        string kernelName,
        out OpenCLError errcodeRet);

    /// <summary>
    /// Set kernel argument.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clSetKernelArg(
        nint kernel,
        uint argIndex,
        nuint argSize,
        nint argValue);

    /// <summary>
    /// Release kernel reference.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clReleaseKernel(nint kernel);

    /// <summary>
    /// Enqueue kernel for execution.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clEnqueueNDRangeKernel(
        nint commandQueue,
        nint kernel,
        uint workDim,
        [In] nuint[]? globalWorkOffset,
        [In] nuint[] globalWorkSize,
        [In] nuint[]? localWorkSize,
        uint numEventsInWaitList,
        [In] nint[]? eventWaitList,
        out nint evt);

    /// <summary>
    /// Enqueue buffer write operation.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clEnqueueWriteBuffer(
        nint commandQueue,
        nint buffer,
        uint blockingWrite,
        nuint offset,
        nuint size,
        nint ptr,
        uint numEventsInWaitList,
        [In] nint[]? eventWaitList,
        out nint evt);

    /// <summary>
    /// Enqueue buffer read operation.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clEnqueueReadBuffer(
        nint commandQueue,
        nint buffer,
        uint blockingRead,
        nuint offset,
        nuint size,
        nint ptr,
        uint numEventsInWaitList,
        [In] nint[]? eventWaitList,
        out nint evt);

    /// <summary>
    /// Fill buffer with a pattern.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clEnqueueFillBuffer(
        nint commandQueue,
        nint buffer,
        nint pattern,
        nuint patternSize,
        nuint offset,
        nuint size,
        uint numEventsInWaitList,
        [In] nint[]? eventWaitList,
        nint evt);

    /// <summary>
    /// Copy buffer to buffer.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clEnqueueCopyBuffer(
        nint commandQueue,
        nint srcBuffer,
        nint dstBuffer,
        nuint srcOffset,
        nuint dstOffset,
        nuint size,
        uint numEventsInWaitList,
        [In] nint[]? eventWaitList,
        nint evt);

    /// <summary>
    /// Wait for events to complete.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clWaitForEvents(
        uint numEvents,
        [In] nint[] eventList);

    /// <summary>
    /// Release event reference.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clReleaseEvent(nint evt);

    /// <summary>
    /// Flush command queue.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clFlush(nint commandQueue);

    /// <summary>
    /// Finish command queue (synchronous).
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clFinish(nint commandQueue);

    /// <summary>
    /// Get event profiling information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetEventProfilingInfo(
        nint evt,
        uint paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Create user event.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nint clCreateUserEvent(
        nint context,
        out OpenCLError errorCode);

    /// <summary>
    /// Set user event status.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clSetUserEventStatus(
        nint evt,
        int executionStatus);
}

/// <summary>
/// Helper methods for OpenCL runtime operations.
/// </summary>
internal static class OpenCLRuntimeHelpers
{
    /// <summary>
    /// Gets string information from OpenCL platform.
    /// </summary>
    internal static string GetPlatformInfoString(nint platform, PlatformInfo info)
    {
        var error = OpenCLRuntime.clGetPlatformInfo(platform, info, 0, nint.Zero, out var size);
        if (error != OpenCLError.Success)
        {
            return string.Empty;
        }

        var buffer = new byte[size];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                error = OpenCLRuntime.clGetPlatformInfo(platform, info, size, (nint)ptr, out _);
                if (error != OpenCLError.Success)
                {
                    return string.Empty;
                }
            }
        }

        return Encoding.UTF8.GetString(buffer, 0, (int)size - 1); // Remove null terminator
    }

    /// <summary>
    /// Gets string information from OpenCL device.
    /// </summary>
    internal static string GetDeviceInfoString(nint device, DeviceInfo info)
    {
        var error = OpenCLRuntime.clGetDeviceInfo(device, info, 0, nint.Zero, out var size);
        if (error != OpenCLError.Success)
        {
            return string.Empty;
        }

        var buffer = new byte[size];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                error = OpenCLRuntime.clGetDeviceInfo(device, info, size, (nint)ptr, out _);
                if (error != OpenCLError.Success)
                {
                    return string.Empty;
                }
            }
        }

        return Encoding.UTF8.GetString(buffer, 0, (int)size - 1); // Remove null terminator
    }

    /// <summary>
    /// Gets numeric information from OpenCL device.
    /// </summary>
    internal static T GetDeviceInfo<T>(nint device, DeviceInfo info) where T : unmanaged
    {
        T value = default;
        unsafe
        {
            var error = OpenCLRuntime.clGetDeviceInfo(device, info, (nuint)sizeof(T), (nint)(&value), out _);
            if (error != OpenCLError.Success)
            {
                return default;
            }
        }
        return value;
    }
}