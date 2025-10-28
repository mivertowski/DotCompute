// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.OpenCL.Types.Native;

namespace DotCompute.Backends.OpenCL.Interop;

/// <summary>
/// Comprehensive P/Invoke wrappers for the OpenCL native API.
/// Provides complete access to OpenCL 1.2+ functionality with production-grade error handling.
/// </summary>
/// <remarks>
/// This class provides direct bindings to the OpenCL C API. Platform-specific library loading
/// is handled automatically:
/// - Windows: OpenCL.dll
/// - Linux: libOpenCL.so (or libOpenCL.so.1)
/// - macOS: OpenCL.framework
///
/// All methods use Cdecl calling convention and safe directory search paths for security.
/// Error codes are returned as <see cref="CLResultCode"/> and should be checked using
/// <see cref="ThrowIfFailed(CLResultCode, string)"/>.
///
/// Note: Parameter names use snake_case to match the OpenCL C API specification exactly.
/// This is intentional and suppressed via CA1707. P/Invoke methods are public to allow
/// advanced usage scenarios (suppressed via CA1401).
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "P/Invoke wrapper must match OpenCL C API naming conventions exactly")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible",
    Justification = "Public access allows advanced scenarios and is safe for OpenCL API")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifiers should not contain type names",
    Justification = "Parameter names like 'ptr' match OpenCL C API specification")]
public static class OpenCLNative
{
    private const string LibName = "OpenCL";

    #region Platform APIs

    /// <summary>
    /// Query the list of available OpenCL platforms.
    /// </summary>
    /// <param name="num_entries">Number of platform IDs that can be added to platforms array.</param>
    /// <param name="platforms">Array to receive platform IDs, or null to query count only.</param>
    /// <param name="num_platforms">Returns the number of available platforms.</param>
    /// <returns>Error code indicating success or failure.</returns>
    /// <remarks>
    /// Call with platforms=null to query the number of platforms, then allocate array and call again.
    /// </remarks>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetPlatformIDs(
        uint num_entries,
        [Out] nint[]? platforms,
        out uint num_platforms);

    /// <summary>
    /// Query specific information about an OpenCL platform.
    /// </summary>
    /// <param name="platform">Platform ID to query.</param>
    /// <param name="param_name">Information parameter to query (name, vendor, version, etc.).</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetPlatformInfo(
        nint platform,
        PlatformInfo param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    #endregion

    #region Device APIs

    /// <summary>
    /// Query the list of available devices on a platform.
    /// </summary>
    /// <param name="platform">Platform to query devices from.</param>
    /// <param name="device_type">Type of devices to query (GPU, CPU, Accelerator, etc.).</param>
    /// <param name="num_entries">Number of device IDs that can be added to devices array.</param>
    /// <param name="devices">Array to receive device IDs, or null to query count only.</param>
    /// <param name="num_devices">Returns the number of available devices.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetDeviceIDs(
        nint platform,
        DeviceType device_type,
        uint num_entries,
        [Out] nint[]? devices,
        out uint num_devices);

    /// <summary>
    /// Query specific information about an OpenCL device.
    /// </summary>
    /// <param name="device">Device ID to query.</param>
    /// <param name="param_name">Information parameter to query (compute units, memory, extensions, etc.).</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetDeviceInfo(
        nint device,
        DeviceInfo param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    #endregion

    #region Context Management

    /// <summary>
    /// Create an OpenCL context for a list of devices.
    /// </summary>
    /// <param name="properties">Context properties (platform, etc.) terminated by 0, or null.</param>
    /// <param name="num_devices">Number of devices in the devices array.</param>
    /// <param name="devices">Array of device IDs to include in the context.</param>
    /// <param name="pfn_notify">Callback function for error reporting, or IntPtr.Zero.</param>
    /// <param name="user_data">User data to pass to callback function.</param>
    /// <param name="errcode_ret">Returns error code if context creation fails.</param>
    /// <returns>Handle to the created context, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateContext(
        [In] nint[]? properties,
        uint num_devices,
        [In] nint[] devices,
        nint pfn_notify,
        nint user_data,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Create an OpenCL context from a device type.
    /// </summary>
    /// <param name="properties">Context properties (platform, etc.) terminated by 0, or null.</param>
    /// <param name="device_type">Type of device to create context for.</param>
    /// <param name="pfn_notify">Callback function for error reporting, or IntPtr.Zero.</param>
    /// <param name="user_data">User data to pass to callback function.</param>
    /// <param name="errcode_ret">Returns error code if context creation fails.</param>
    /// <returns>Handle to the created context, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateContextFromType(
        [In] nint[]? properties,
        DeviceType device_type,
        nint pfn_notify,
        nint user_data,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Increment the context reference count.
    /// </summary>
    /// <param name="context">Context to retain.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clRetainContext(nint context);

    /// <summary>
    /// Decrement the context reference count and release when it reaches zero.
    /// </summary>
    /// <param name="context">Context to release.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clReleaseContext(nint context);

    /// <summary>
    /// Query information about a context.
    /// </summary>
    /// <param name="context">Context to query.</param>
    /// <param name="param_name">Information parameter to query.</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetContextInfo(
        nint context,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    #endregion

    #region Command Queue Management

    /// <summary>
    /// Create a command queue on a specific device (OpenCL 1.x API, deprecated in 2.0+).
    /// </summary>
    /// <param name="context">Context in which to create the command queue.</param>
    /// <param name="device">Device to associate with the command queue.</param>
    /// <param name="properties">Queue properties (profiling, out-of-order execution).</param>
    /// <param name="errcode_ret">Returns error code if queue creation fails.</param>
    /// <returns>Handle to the created command queue, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateCommandQueue(
        nint context,
        nint device,
        ulong properties,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Create a command queue with properties (OpenCL 2.0+ API).
    /// </summary>
    /// <param name="context">Context in which to create the command queue.</param>
    /// <param name="device">Device to associate with the command queue.</param>
    /// <param name="properties">Array of queue properties terminated by 0, or null.</param>
    /// <param name="errcode_ret">Returns error code if queue creation fails.</param>
    /// <returns>Handle to the created command queue, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateCommandQueueWithProperties(
        nint context,
        nint device,
        [In] nint[]? properties,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Increment the command queue reference count.
    /// </summary>
    /// <param name="command_queue">Command queue to retain.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clRetainCommandQueue(nint command_queue);

    /// <summary>
    /// Decrement the command queue reference count and release when it reaches zero.
    /// </summary>
    /// <param name="command_queue">Command queue to release.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clReleaseCommandQueue(nint command_queue);

    /// <summary>
    /// Issues all previously queued commands to the device.
    /// </summary>
    /// <param name="command_queue">Command queue to flush.</param>
    /// <returns>Error code indicating success or failure.</returns>
    /// <remarks>
    /// Guarantees that all queued commands will be issued to the device, but does not wait for completion.
    /// </remarks>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clFlush(nint command_queue);

    /// <summary>
    /// Blocks until all previously queued commands have completed.
    /// </summary>
    /// <param name="command_queue">Command queue to finish.</param>
    /// <returns>Error code indicating success or failure.</returns>
    /// <remarks>
    /// This is a synchronous blocking call that waits for all commands in the queue to complete.
    /// </remarks>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clFinish(nint command_queue);

    #endregion

    #region Memory Object Management

    /// <summary>
    /// Create a buffer object (memory allocation on the device).
    /// </summary>
    /// <param name="context">Context in which to create the buffer.</param>
    /// <param name="flags">Memory flags (read/write access, host pointer usage).</param>
    /// <param name="size">Size in bytes of the buffer to allocate.</param>
    /// <param name="host_ptr">Host memory pointer to initialize from, or IntPtr.Zero.</param>
    /// <param name="errcode_ret">Returns error code if buffer creation fails.</param>
    /// <returns>Handle to the created buffer, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateBuffer(
        nint context,
        MemoryFlags flags,
        nuint size,
        nint host_ptr,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Create a sub-buffer from an existing buffer object.
    /// </summary>
    /// <param name="buffer">Parent buffer to create sub-buffer from.</param>
    /// <param name="flags">Memory flags for the sub-buffer.</param>
    /// <param name="buffer_create_type">Type of sub-buffer to create (region).</param>
    /// <param name="buffer_create_info">Pointer to structure describing the sub-buffer.</param>
    /// <param name="errcode_ret">Returns error code if sub-buffer creation fails.</param>
    /// <returns>Handle to the created sub-buffer, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateSubBuffer(
        nint buffer,
        MemoryFlags flags,
        uint buffer_create_type,
        nint buffer_create_info,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Increment the memory object reference count.
    /// </summary>
    /// <param name="memobj">Memory object to retain.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clRetainMemObject(nint memobj);

    /// <summary>
    /// Decrement the memory object reference count and release when it reaches zero.
    /// </summary>
    /// <param name="memobj">Memory object to release.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clReleaseMemObject(nint memobj);

    /// <summary>
    /// Query information about a memory object.
    /// </summary>
    /// <param name="memobj">Memory object to query.</param>
    /// <param name="param_name">Information parameter to query.</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetMemObjectInfo(
        nint memobj,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    #endregion

    #region Buffer Operations

    /// <summary>
    /// Enqueue a command to read from a buffer object to host memory.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the read operation.</param>
    /// <param name="buffer">Buffer object to read from.</param>
    /// <param name="blocking_read">True for synchronous blocking read, false for async.</param>
    /// <param name="offset">Offset in bytes in the buffer to read from.</param>
    /// <param name="size">Size in bytes of data to read.</param>
    /// <param name="ptr">Host memory pointer to write data to.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clEnqueueReadBuffer(
        nint command_queue,
        nint buffer,
        uint blocking_read,
        nuint offset,
        nuint size,
        nint ptr,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle);

    /// <summary>
    /// Enqueue a command to write to a buffer object from host memory.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the write operation.</param>
    /// <param name="buffer">Buffer object to write to.</param>
    /// <param name="blocking_write">True for synchronous blocking write, false for async.</param>
    /// <param name="offset">Offset in bytes in the buffer to write to.</param>
    /// <param name="size">Size in bytes of data to write.</param>
    /// <param name="ptr">Host memory pointer to read data from.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clEnqueueWriteBuffer(
        nint command_queue,
        nint buffer,
        uint blocking_write,
        nuint offset,
        nuint size,
        nint ptr,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle);

    /// <summary>
    /// Enqueue a command to copy from one buffer object to another.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the copy operation.</param>
    /// <param name="src_buffer">Source buffer object.</param>
    /// <param name="dst_buffer">Destination buffer object.</param>
    /// <param name="src_offset">Offset in bytes in the source buffer.</param>
    /// <param name="dst_offset">Offset in bytes in the destination buffer.</param>
    /// <param name="size">Size in bytes of data to copy.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clEnqueueCopyBuffer(
        nint command_queue,
        nint src_buffer,
        nint dst_buffer,
        nuint src_offset,
        nuint dst_offset,
        nuint size,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle);

    /// <summary>
    /// Enqueue a command to fill a buffer object with a pattern.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the fill operation.</param>
    /// <param name="buffer">Buffer object to fill.</param>
    /// <param name="pattern">Pointer to the pattern to fill with.</param>
    /// <param name="pattern_size">Size in bytes of the pattern.</param>
    /// <param name="offset">Offset in bytes in the buffer to start filling.</param>
    /// <param name="size">Size in bytes to fill.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clEnqueueFillBuffer(
        nint command_queue,
        nint buffer,
        nint pattern,
        nuint pattern_size,
        nuint offset,
        nuint size,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle);

    /// <summary>
    /// Enqueue a command to map a region of a buffer object into the host address space.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the map operation.</param>
    /// <param name="buffer">Buffer object to map.</param>
    /// <param name="blocking_map">True for synchronous blocking map, false for async.</param>
    /// <param name="map_flags">Flags indicating read/write access to the mapped region.</param>
    /// <param name="offset">Offset in bytes in the buffer to map.</param>
    /// <param name="size">Size in bytes of the region to map.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <param name="errcode_ret">Returns error code if map fails.</param>
    /// <returns>Host pointer to the mapped region, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clEnqueueMapBuffer(
        nint command_queue,
        nint buffer,
        uint blocking_map,
        ulong map_flags,
        nuint offset,
        nuint size,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Enqueue a command to unmap a previously mapped region of a memory object.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the unmap operation.</param>
    /// <param name="memobj">Memory object to unmap.</param>
    /// <param name="mapped_ptr">Host pointer returned by clEnqueueMapBuffer or clEnqueueMapImage.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clEnqueueUnmapMemObject(
        nint command_queue,
        nint memobj,
        nint mapped_ptr,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle);

    #endregion

    #region Program Management

    /// <summary>
    /// Create a program object from source code.
    /// </summary>
    /// <param name="context">Context in which to create the program.</param>
    /// <param name="count">Number of strings in the strings array.</param>
    /// <param name="strings">Array of source code strings.</param>
    /// <param name="lengths">Array of string lengths, or null if strings are null-terminated.</param>
    /// <param name="errcode_ret">Returns error code if program creation fails.</param>
    /// <returns>Handle to the created program, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateProgramWithSource(
        nint context,
        uint count,
        [In] string[] strings,
        [In] nuint[]? lengths,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Create a program object from binary data.
    /// </summary>
    /// <param name="context">Context in which to create the program.</param>
    /// <param name="num_devices">Number of devices the binaries are for.</param>
    /// <param name="device_list">Array of device IDs.</param>
    /// <param name="lengths">Array of binary sizes for each device.</param>
    /// <param name="binaries">Array of pointers to binary data for each device.</param>
    /// <param name="binary_status">Returns the binary load status for each device.</param>
    /// <param name="errcode_ret">Returns error code if program creation fails.</param>
    /// <returns>Handle to the created program, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateProgramWithBinary(
        nint context,
        uint num_devices,
        [In] nint[] device_list,
        [In] nuint[] lengths,
        [In] nint[] binaries,
        [Out] int[]? binary_status,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Build (compile and link) a program executable from the program source or binary.
    /// </summary>
    /// <param name="program">Program to build.</param>
    /// <param name="num_devices">Number of devices to build for, or 0 for all devices.</param>
    /// <param name="device_list">Array of device IDs to build for, or null for all devices.</param>
    /// <param name="options">Compiler options string, or null.</param>
    /// <param name="pfn_notify">Callback function to invoke when build completes, or IntPtr.Zero.</param>
    /// <param name="user_data">User data to pass to callback function.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments",
        Justification = "OpenCL API uses ANSI C strings, CharSet.Ansi is appropriate")]
    public static extern CLResultCode clBuildProgram(
        nint program,
        uint num_devices,
        [In] nint[]? device_list,
        string? options,
        nint pfn_notify,
        nint user_data);

    /// <summary>
    /// Query build information for a program (build log, status, options, etc.).
    /// </summary>
    /// <param name="program">Program to query.</param>
    /// <param name="device">Device to query build information for.</param>
    /// <param name="param_name">Information parameter to query.</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetProgramBuildInfo(
        nint program,
        nint device,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    /// <summary>
    /// Increment the program reference count.
    /// </summary>
    /// <param name="program">Program to retain.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clRetainProgram(nint program);

    /// <summary>
    /// Decrement the program reference count and release when it reaches zero.
    /// </summary>
    /// <param name="program">Program to release.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clReleaseProgram(nint program);

    /// <summary>
    /// Query information about a program.
    /// </summary>
    /// <param name="program">Program to query.</param>
    /// <param name="param_name">Information parameter to query.</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetProgramInfo(
        nint program,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    #endregion

    #region Kernel Management

    /// <summary>
    /// Create a kernel object from a program.
    /// </summary>
    /// <param name="program">Program containing the kernel function.</param>
    /// <param name="kernel_name">Name of the kernel function.</param>
    /// <param name="errcode_ret">Returns error code if kernel creation fails.</param>
    /// <returns>Handle to the created kernel, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments",
        Justification = "OpenCL API uses ANSI C strings, CharSet.Ansi is appropriate")]
    public static extern nint clCreateKernel(
        nint program,
        string kernel_name,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Create kernel objects for all kernels in a program.
    /// </summary>
    /// <param name="program">Program containing kernel functions.</param>
    /// <param name="num_kernels">Number of kernels that can be stored in kernels array.</param>
    /// <param name="kernels">Array to receive kernel handles, or null to query count only.</param>
    /// <param name="num_kernels_ret">Returns the number of kernels in the program.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clCreateKernelsInProgram(
        nint program,
        uint num_kernels,
        [Out] nint[]? kernels,
        out uint num_kernels_ret);

    /// <summary>
    /// Set the argument value for a specific kernel argument.
    /// </summary>
    /// <param name="kernel">Kernel object.</param>
    /// <param name="arg_index">Argument index (0-based).</param>
    /// <param name="arg_size">Size in bytes of the argument value.</param>
    /// <param name="arg_value">Pointer to data that should be used as the argument value.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clSetKernelArg(
        nint kernel,
        uint arg_index,
        nuint arg_size,
        nint arg_value);

    /// <summary>
    /// Increment the kernel reference count.
    /// </summary>
    /// <param name="kernel">Kernel to retain.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clRetainKernel(nint kernel);

    /// <summary>
    /// Decrement the kernel reference count and release when it reaches zero.
    /// </summary>
    /// <param name="kernel">Kernel to release.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clReleaseKernel(nint kernel);

    /// <summary>
    /// Query information about a kernel object.
    /// </summary>
    /// <param name="kernel">Kernel to query.</param>
    /// <param name="param_name">Information parameter to query.</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetKernelInfo(
        nint kernel,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    /// <summary>
    /// Query device-specific information about a kernel object.
    /// </summary>
    /// <param name="kernel">Kernel to query.</param>
    /// <param name="device">Device to query work-group information for.</param>
    /// <param name="param_name">Information parameter to query (work-group size, local memory, etc.).</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetKernelWorkGroupInfo(
        nint kernel,
        nint device,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    /// <summary>
    /// Query argument information for a kernel (OpenCL 1.2+).
    /// </summary>
    /// <param name="kernel">Kernel to query argument information for.</param>
    /// <param name="arg_index">Argument index to query.</param>
    /// <param name="param_name">Information parameter to query (address qualifier, access qualifier, type name, etc.).</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    /// <remarks>
    /// This API is only available in OpenCL 1.2 and later. Check platform version before calling.
    /// </remarks>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetKernelArgInfo(
        nint kernel,
        uint arg_index,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    #endregion

    #region Kernel Execution

    /// <summary>
    /// Enqueue a kernel for execution on a device with an N-dimensional index space.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the kernel execution.</param>
    /// <param name="kernel">Kernel object to execute.</param>
    /// <param name="work_dim">Number of dimensions in the work-items (1, 2, or 3).</param>
    /// <param name="global_work_offset">Global ID offset for each dimension, or null.</param>
    /// <param name="global_work_size">Number of global work-items for each dimension.</param>
    /// <param name="local_work_size">Number of work-items per work-group for each dimension, or null for automatic.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clEnqueueNDRangeKernel(
        nint command_queue,
        nint kernel,
        uint work_dim,
        [In] nuint[]? global_work_offset,
        [In] nuint[] global_work_size,
        [In] nuint[]? local_work_size,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle);

    /// <summary>
    /// Enqueue a command to execute a kernel on a single work-item.
    /// </summary>
    /// <param name="command_queue">Command queue to enqueue the kernel execution.</param>
    /// <param name="kernel">Kernel object to execute.</param>
    /// <param name="num_events_in_wait_list">Number of events to wait for before executing.</param>
    /// <param name="event_wait_list">Array of events to wait for, or null.</param>
    /// <param name="event_handle">Returns an event handle for this operation.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clEnqueueTask(
        nint command_queue,
        nint kernel,
        uint num_events_in_wait_list,
        [In] nint[]? event_wait_list,
        out nint event_handle);

    #endregion

    #region Event Management

    /// <summary>
    /// Create a user event object.
    /// </summary>
    /// <param name="context">Context in which to create the user event.</param>
    /// <param name="errcode_ret">Returns error code if event creation fails.</param>
    /// <returns>Handle to the created user event, or IntPtr.Zero on failure.</returns>
    /// <remarks>
    /// User events allow the host to manually signal event completion using clSetUserEventStatus.
    /// </remarks>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern nint clCreateUserEvent(
        nint context,
        out CLResultCode errcode_ret);

    /// <summary>
    /// Set the execution status of a user event.
    /// </summary>
    /// <param name="event_handle">User event to update.</param>
    /// <param name="execution_status">Execution status (CL_COMPLETE = 0, negative for errors).</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clSetUserEventStatus(
        nint event_handle,
        int execution_status);

    /// <summary>
    /// Wait for commands identified by event objects to complete.
    /// </summary>
    /// <param name="num_events">Number of events to wait for.</param>
    /// <param name="event_list">Array of event handles to wait for.</param>
    /// <returns>Error code indicating success or failure.</returns>
    /// <remarks>
    /// This is a synchronous blocking call that waits for all specified events to complete.
    /// </remarks>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clWaitForEvents(
        uint num_events,
        [In] nint[] event_list);

    /// <summary>
    /// Increment the event reference count.
    /// </summary>
    /// <param name="event_handle">Event to retain.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clRetainEvent(nint event_handle);

    /// <summary>
    /// Decrement the event reference count and release when it reaches zero.
    /// </summary>
    /// <param name="event_handle">Event to release.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clReleaseEvent(nint event_handle);

    /// <summary>
    /// Query information about an event object.
    /// </summary>
    /// <param name="event_handle">Event to query.</param>
    /// <param name="param_name">Information parameter to query (status, reference count, etc.).</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetEventInfo(
        nint event_handle,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    /// <summary>
    /// Query profiling information for a command associated with an event.
    /// </summary>
    /// <param name="event_handle">Event to query profiling information for.</param>
    /// <param name="param_name">Profiling parameter to query (queued time, submit time, start time, end time).</param>
    /// <param name="param_value_size">Size in bytes of memory pointed to by param_value.</param>
    /// <param name="param_value">Pointer to memory where profiling information will be written.</param>
    /// <param name="param_value_size_ret">Returns the actual size of the data being queried.</param>
    /// <returns>Error code indicating success or failure.</returns>
    /// <remarks>
    /// Requires the command queue to be created with CL_QUEUE_PROFILING_ENABLE.
    /// </remarks>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clGetEventProfilingInfo(
        nint event_handle,
        uint param_name,
        nuint param_value_size,
        nint param_value,
        out nuint param_value_size_ret);

    /// <summary>
    /// Register a callback function for a specific command execution status.
    /// </summary>
    /// <param name="event_handle">Event to register callback for.</param>
    /// <param name="command_exec_callback_type">Execution status to trigger callback (CL_COMPLETE, etc.).</param>
    /// <param name="pfn_notify">Callback function pointer.</param>
    /// <param name="user_data">User data to pass to callback function.</param>
    /// <returns>Error code indicating success or failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern CLResultCode clSetEventCallback(
        nint event_handle,
        int command_exec_callback_type,
        nint pfn_notify,
        nint user_data);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Throws an <see cref="OpenCLException"/> if the result code indicates an error.
    /// </summary>
    /// <param name="result">The OpenCL result code to check.</param>
    /// <param name="operationName">Optional name of the operation for error context.</param>
    /// <exception cref="OpenCLException">Thrown if result is not Success.</exception>
    public static void ThrowIfFailed(CLResultCode result, string? operationName = null)
    {
        if (result != CLResultCode.Success)
        {
            var errorMessage = GetErrorMessage(result);
            var message = operationName != null
                ? $"OpenCL operation '{operationName}' failed: {errorMessage}"
                : $"OpenCL operation failed: {errorMessage}";

            throw new OpenCLException((OpenCLError)result, message);
        }
    }

    /// <summary>
    /// Gets a human-readable error message for an OpenCL result code.
    /// </summary>
    /// <param name="result">The result code to get a message for.</param>
    /// <returns>A descriptive error message.</returns>
    public static string GetErrorMessage(CLResultCode result)
    {
        return result switch
        {
            CLResultCode.Success => "Success",
            CLResultCode.DeviceNotFound => "Device not found",
            CLResultCode.DeviceNotAvailable => "Device not available",
            CLResultCode.CompilerNotAvailable => "Compiler not available",
            CLResultCode.MemObjectAllocationFailure => "Memory object allocation failure",
            CLResultCode.OutOfResources => "Out of resources",
            CLResultCode.OutOfHostMemory => "Out of host memory",
            CLResultCode.ProfilingInfoNotAvailable => "Profiling information not available",
            CLResultCode.MemCopyOverlap => "Memory copy overlap",
            CLResultCode.ImageFormatMismatch => "Image format mismatch",
            CLResultCode.ImageFormatNotSupported => "Image format not supported",
            CLResultCode.BuildProgramFailure => "Build program failure",
            CLResultCode.MapFailure => "Map failure",
            CLResultCode.MisalignedSubBufferOffset => "Misaligned sub-buffer offset",
            CLResultCode.ExecStatusErrorForEventsInWaitList => "Execution status error for events in wait list",
            CLResultCode.CompileProgramFailure => "Compile program failure",
            CLResultCode.LinkerNotAvailable => "Linker not available",
            CLResultCode.LinkProgramFailure => "Link program failure",
            CLResultCode.DevicePartitionFailed => "Device partition failed",
            CLResultCode.KernelArgInfoNotAvailable => "Kernel argument info not available",
            CLResultCode.InvalidValue => "Invalid value",
            CLResultCode.InvalidDeviceType => "Invalid device type",
            CLResultCode.InvalidPlatform => "Invalid platform",
            CLResultCode.InvalidDevice => "Invalid device",
            CLResultCode.InvalidContext => "Invalid context",
            CLResultCode.InvalidQueueProperties => "Invalid queue properties",
            CLResultCode.InvalidCommandQueue => "Invalid command queue",
            CLResultCode.InvalidHostPtr => "Invalid host pointer",
            CLResultCode.InvalidMemObject => "Invalid memory object",
            CLResultCode.InvalidImageFormatDescriptor => "Invalid image format descriptor",
            CLResultCode.InvalidImageSize => "Invalid image size",
            CLResultCode.InvalidSampler => "Invalid sampler",
            CLResultCode.InvalidBinary => "Invalid binary",
            CLResultCode.InvalidBuildOptions => "Invalid build options",
            CLResultCode.InvalidProgram => "Invalid program",
            CLResultCode.InvalidProgramExecutable => "Invalid program executable",
            CLResultCode.InvalidKernelName => "Invalid kernel name",
            CLResultCode.InvalidKernelDefinition => "Invalid kernel definition",
            CLResultCode.InvalidKernel => "Invalid kernel",
            CLResultCode.InvalidArgIndex => "Invalid argument index",
            CLResultCode.InvalidArgValue => "Invalid argument value",
            CLResultCode.InvalidArgSize => "Invalid argument size",
            CLResultCode.InvalidKernelArgs => "Invalid kernel arguments",
            CLResultCode.InvalidWorkDimension => "Invalid work dimension",
            CLResultCode.InvalidWorkGroupSize => "Invalid work-group size",
            CLResultCode.InvalidWorkItemSize => "Invalid work-item size",
            CLResultCode.InvalidGlobalOffset => "Invalid global offset",
            CLResultCode.InvalidEventWaitList => "Invalid event wait list",
            CLResultCode.InvalidEvent => "Invalid event",
            CLResultCode.InvalidOperation => "Invalid operation",
            CLResultCode.InvalidGLObject => "Invalid GL object",
            CLResultCode.InvalidBufferSize => "Invalid buffer size",
            CLResultCode.InvalidMipLevel => "Invalid mip-map level",
            CLResultCode.InvalidGlobalWorkSize => "Invalid global work size",
            CLResultCode.InvalidProperty => "Invalid property",
            CLResultCode.InvalidImageDescriptor => "Invalid image descriptor",
            CLResultCode.InvalidCompilerOptions => "Invalid compiler options",
            CLResultCode.InvalidLinkerOptions => "Invalid linker options",
            CLResultCode.InvalidDevicePartitionCount => "Invalid device partition count",
            _ => $"Unknown error code: {(int)result}"
        };
    }

    #endregion
}

/// <summary>
/// OpenCL result codes (error codes).
/// Complete enumeration of all OpenCL error codes from the OpenCL specification.
/// </summary>
public enum CLResultCode : int
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,

    // Runtime and device errors (negative values from -1 to -19)
    /// <summary>No device matching the specified criteria was found.</summary>
    DeviceNotFound = -1,
    /// <summary>The device is currently not available.</summary>
    DeviceNotAvailable = -2,
    /// <summary>The OpenCL compiler is not available on this platform.</summary>
    CompilerNotAvailable = -3,
    /// <summary>Failed to allocate resources required by the memory object.</summary>
    MemObjectAllocationFailure = -4,
    /// <summary>Failed to allocate resources required by the OpenCL implementation.</summary>
    OutOfResources = -5,
    /// <summary>Failed to allocate resources required by the host.</summary>
    OutOfHostMemory = -6,
    /// <summary>Profiling information is not available for this event.</summary>
    ProfilingInfoNotAvailable = -7,
    /// <summary>Memory copy regions overlap (undefined behavior).</summary>
    MemCopyOverlap = -8,
    /// <summary>Image format mismatch between source and destination.</summary>
    ImageFormatMismatch = -9,
    /// <summary>The specified image format is not supported.</summary>
    ImageFormatNotSupported = -10,
    /// <summary>Failed to build the program executable.</summary>
    BuildProgramFailure = -11,
    /// <summary>Failed to map the requested region into host address space.</summary>
    MapFailure = -12,
    /// <summary>Sub-buffer offset is not aligned to device requirements.</summary>
    MisalignedSubBufferOffset = -13,
    /// <summary>Execution status error for events in the wait list.</summary>
    ExecStatusErrorForEventsInWaitList = -14,
    /// <summary>Failed to compile the program source.</summary>
    CompileProgramFailure = -15,
    /// <summary>The linker is not available on this platform.</summary>
    LinkerNotAvailable = -16,
    /// <summary>Failed to link the program executable.</summary>
    LinkProgramFailure = -17,
    /// <summary>Failed to partition the device.</summary>
    DevicePartitionFailed = -18,
    /// <summary>Kernel argument information is not available.</summary>
    KernelArgInfoNotAvailable = -19,

    // Compile-time errors (negative values from -30 to -69)
    /// <summary>An invalid value was specified as an argument.</summary>
    InvalidValue = -30,
    /// <summary>An invalid device type was specified.</summary>
    InvalidDeviceType = -31,
    /// <summary>The platform ID is not valid.</summary>
    InvalidPlatform = -32,
    /// <summary>The device ID is not valid.</summary>
    InvalidDevice = -33,
    /// <summary>The context is not valid.</summary>
    InvalidContext = -34,
    /// <summary>The queue properties are not valid.</summary>
    InvalidQueueProperties = -35,
    /// <summary>The command queue is not valid.</summary>
    InvalidCommandQueue = -36,
    /// <summary>The host pointer is not valid.</summary>
    InvalidHostPtr = -37,
    /// <summary>The memory object is not valid.</summary>
    InvalidMemObject = -38,
    /// <summary>The image format descriptor is not valid.</summary>
    InvalidImageFormatDescriptor = -39,
    /// <summary>The image size exceeds the maximum supported size.</summary>
    InvalidImageSize = -40,
    /// <summary>The sampler is not valid.</summary>
    InvalidSampler = -41,
    /// <summary>The program binary is not valid for the specified devices.</summary>
    InvalidBinary = -42,
    /// <summary>The build options string is not valid.</summary>
    InvalidBuildOptions = -43,
    /// <summary>The program object is not valid.</summary>
    InvalidProgram = -44,
    /// <summary>There is no valid program executable for this device.</summary>
    InvalidProgramExecutable = -45,
    /// <summary>The kernel name is not found in the program.</summary>
    InvalidKernelName = -46,
    /// <summary>The kernel definition is not valid.</summary>
    InvalidKernelDefinition = -47,
    /// <summary>The kernel object is not valid.</summary>
    InvalidKernel = -48,
    /// <summary>The argument index is not valid.</summary>
    InvalidArgIndex = -49,
    /// <summary>The argument value is not valid.</summary>
    InvalidArgValue = -50,
    /// <summary>The argument size is not valid.</summary>
    InvalidArgSize = -51,
    /// <summary>The kernel arguments are not set correctly.</summary>
    InvalidKernelArgs = -52,
    /// <summary>The work dimension is not valid (must be 1, 2, or 3).</summary>
    InvalidWorkDimension = -53,
    /// <summary>The work-group size is not valid for this kernel or device.</summary>
    InvalidWorkGroupSize = -54,
    /// <summary>The work-item size is not valid.</summary>
    InvalidWorkItemSize = -55,
    /// <summary>The global work offset is not valid.</summary>
    InvalidGlobalOffset = -56,
    /// <summary>The event wait list is not valid.</summary>
    InvalidEventWaitList = -57,
    /// <summary>The event object is not valid.</summary>
    InvalidEvent = -58,
    /// <summary>The operation is not a valid operation.</summary>
    InvalidOperation = -59,
    /// <summary>The GL object is not valid.</summary>
    InvalidGLObject = -60,
    /// <summary>The buffer size is not valid.</summary>
    InvalidBufferSize = -61,
    /// <summary>The mip-map level is not valid.</summary>
    InvalidMipLevel = -62,
    /// <summary>The global work size is not valid.</summary>
    InvalidGlobalWorkSize = -63,
    /// <summary>The property is not valid.</summary>
    InvalidProperty = -64,
    /// <summary>The image descriptor is not valid.</summary>
    InvalidImageDescriptor = -65,
    /// <summary>The compiler options are not valid.</summary>
    InvalidCompilerOptions = -66,
    /// <summary>The linker options are not valid.</summary>
    InvalidLinkerOptions = -67,
    /// <summary>The device partition count is not valid.</summary>
    InvalidDevicePartitionCount = -68
}
