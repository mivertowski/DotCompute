// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL;

/// <summary>
/// Represents an OpenCL context for a specific device.
/// Manages the OpenCL context, command queue, and device state.
/// </summary>
internal sealed class OpenCLContext : IDisposable
{
    private readonly ILogger<OpenCLContext> _logger;
    private readonly OpenCLDeviceInfo _deviceInfo;
    private readonly object _lock = new();

    private Context _context;
    private CommandQueue _commandQueue;
    private bool _disposed;

    /// <summary>
    /// Gets the device information for this context.
    /// </summary>
    public OpenCLDeviceInfo DeviceInfo => _deviceInfo;

    /// <summary>
    /// Gets the OpenCL context handle.
    /// </summary>
    public Context Context => _context;

    /// <summary>
    /// Gets the command queue handle.
    /// </summary>
    public CommandQueue CommandQueue => _commandQueue;

    /// <summary>
    /// Gets whether this context has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLContext"/> class.
    /// </summary>
    /// <param name="deviceInfo">The device information.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLContext(OpenCLDeviceInfo deviceInfo, ILogger<OpenCLContext> logger)
    {
        _deviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Initialize();
    }

    /// <summary>
    /// Creates a buffer in device memory.
    /// </summary>
    /// <param name="flags">Memory flags for the buffer.</param>
    /// <param name="size">Size of the buffer in bytes.</param>
    /// <param name="hostPtr">Optional host pointer for buffer initialization.</param>
    /// <returns>The created buffer handle.</returns>
    public MemObject CreateBuffer(MemoryFlags flags, nuint size, nint hostPtr = default)
    {
        ThrowIfDisposed();

        _logger.LogDebug($"Creating OpenCL buffer: size={size}, flags={flags}");

        var buffer = OpenCLRuntime.clCreateBuffer(_context, flags, size, hostPtr, out var error);
        OpenCLException.ThrowIfError(error, "Create buffer");

        return buffer;
    }

    /// <summary>
    /// Creates a program from source code.
    /// </summary>
    /// <param name="source">OpenCL kernel source code.</param>
    /// <returns>The created program handle.</returns>
    public Program CreateProgramFromSource(string source)
    {
        ThrowIfDisposed();

        _logger.LogDebug($"Creating OpenCL program from source ({source.Length} chars)");

        var sources = new[] { source };
        var program = OpenCLRuntime.clCreateProgramWithSource(_context, 1, sources, null, out var error);
        OpenCLException.ThrowIfError(error, "Create program from source");

        return program;
    }

    /// <summary>
    /// Builds a program for the device.
    /// </summary>
    /// <param name="program">Program to build.</param>
    /// <param name="options">Build options (optional).</param>
    public void BuildProgram(Program program, string? options = null)
    {
        ThrowIfDisposed();

        _logger.LogDebug($"Building OpenCL program with options: {options ?? "none"}");

        var devices = new[] { _deviceInfo.DeviceId.Handle };
        var error = OpenCLRuntime.clBuildProgram(program, 1, devices, options, nint.Zero, nint.Zero);

        if (error != OpenCLError.Success)
        {
            // Get build log for debugging
            var buildLog = GetProgramBuildLog(program);
            _logger.LogError("Program build failed: {Error}\nBuild log:\n{BuildLog}", error, buildLog);
            throw new OpenCLException(error, $"Program build failed: {buildLog}");
        }

        _logger.LogDebug("Program built successfully");
    }

    /// <summary>
    /// Creates a kernel from a built program.
    /// </summary>
    /// <param name="program">Built program containing the kernel.</param>
    /// <param name="kernelName">Name of the kernel function.</param>
    /// <returns>The created kernel handle.</returns>
    public Kernel CreateKernel(Program program, string kernelName)
    {
        ThrowIfDisposed();

        _logger.LogDebug($"Creating kernel: {kernelName}");

        var kernel = OpenCLRuntime.clCreateKernel(program, kernelName, out var error);
        OpenCLException.ThrowIfError(error, $"Create kernel '{kernelName}'");

        return kernel;
    }

    /// <summary>
    /// Enqueues a buffer write operation.
    /// </summary>
    /// <param name="buffer">Target buffer.</param>
    /// <param name="data">Source data pointer.</param>
    /// <param name="size">Number of bytes to write.</param>
    /// <param name="blocking">Whether to block until completion.</param>
    /// <returns>Event handle for the operation.</returns>
    public Event EnqueueWriteBuffer(MemObject buffer, nint data, nuint size, bool blocking = false)
    {
        ThrowIfDisposed();

        var error = OpenCLRuntime.clEnqueueWriteBuffer(
            _commandQueue,
            buffer,
            blocking ? 1u : 0u,
            0,
            size,
            data,
            0,
            null,
            out var evt);

        OpenCLException.ThrowIfError(error, "Enqueue write buffer");
        return evt;
    }

    /// <summary>
    /// Enqueues a buffer read operation.
    /// </summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="data">Target data pointer.</param>
    /// <param name="size">Number of bytes to read.</param>
    /// <param name="blocking">Whether to block until completion.</param>
    /// <returns>Event handle for the operation.</returns>
    public Event EnqueueReadBuffer(MemObject buffer, nint data, nuint size, bool blocking = false)
    {
        ThrowIfDisposed();

        var error = OpenCLRuntime.clEnqueueReadBuffer(
            _commandQueue,
            buffer,
            blocking ? 1u : 0u,
            0,
            size,
            data,
            0,
            null,
            out var evt);

        OpenCLException.ThrowIfError(error, "Enqueue read buffer");
        return evt;
    }

    /// <summary>
    /// Enqueues a kernel for execution.
    /// </summary>
    /// <param name="kernel">Kernel to execute.</param>
    /// <param name="workDimensions">Number of work dimensions (1, 2, or 3).</param>
    /// <param name="globalWorkSize">Global work size for each dimension.</param>
    /// <param name="localWorkSize">Local work size for each dimension (optional).</param>
    /// <returns>Event handle for the operation.</returns>
    public Event EnqueueKernel(
        Kernel kernel,
        uint workDimensions,
        nuint[] globalWorkSize,
        nuint[]? localWorkSize = null)
    {
        ThrowIfDisposed();

        if (globalWorkSize.Length != workDimensions)
            throw new ArgumentException("Global work size array length must match work dimensions");

        if (localWorkSize != null && localWorkSize.Length != workDimensions)
            throw new ArgumentException("Local work size array length must match work dimensions");

        var error = OpenCLRuntime.clEnqueueNDRangeKernel(
            _commandQueue,
            kernel,
            workDimensions,
            null, // global work offset
            globalWorkSize,
            localWorkSize,
            0,
            null,
            out var evt);

        OpenCLException.ThrowIfError(error, "Enqueue kernel");
        return evt;
    }

    /// <summary>
    /// Waits for events to complete.
    /// </summary>
    /// <param name="events">Events to wait for.</param>
    public void WaitForEvents(params Event[] events)
    {
        ThrowIfDisposed();

        if (events.Length == 0) return;

        var eventHandles = events.Select(e => e.Handle).ToArray();
        var error = OpenCLRuntime.clWaitForEvents((uint)events.Length, eventHandles);
        OpenCLException.ThrowIfError(error, "Wait for events");
    }

    /// <summary>
    /// Flushes the command queue.
    /// </summary>
    public void Flush()
    {
        ThrowIfDisposed();
        var error = OpenCLRuntime.clFlush(_commandQueue);
        OpenCLException.ThrowIfError(error, "Flush command queue");
    }

    /// <summary>
    /// Finishes all commands in the queue (blocking).
    /// </summary>
    public void Finish()
    {
        ThrowIfDisposed();
        var error = OpenCLRuntime.clFinish(_commandQueue);
        OpenCLException.ThrowIfError(error, "Finish command queue");
    }

    /// <summary>
    /// Releases an OpenCL object reference.
    /// </summary>
    public static void ReleaseObject(nint handle, Func<nint, OpenCLError> releaseFunction, string objectType)
    {
        if (handle != nint.Zero)
        {
            var error = releaseFunction(handle);
            if (error != OpenCLError.Success)
            {
                // Log but don't throw during cleanup
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to release {objectType}: {error}");
            }
        }
    }

    /// <summary>
    /// Initializes the OpenCL context and command queue.
    /// </summary>
    private void Initialize()
    {
        _logger.LogDebug($"Initializing OpenCL context for device: {_deviceInfo.Name}");

        try
        {
            // Create context
            var devices = new[] { _deviceInfo.DeviceId.Handle };
            _context = OpenCLRuntime.clCreateContext(null, 1, devices, nint.Zero, nint.Zero, out var error);
            OpenCLException.ThrowIfError(error, "Create context");

            // Create command queue
            _commandQueue = OpenCLRuntime.clCreateCommandQueue(_context, _deviceInfo.DeviceId, 0, out error);
            OpenCLException.ThrowIfError(error, "Create command queue");

            _logger.LogInformation("OpenCL context initialized successfully for device: {_deviceInfo.Name}");
        }
        catch
        {
            // Clean up on failure
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the build log for a program.
    /// </summary>
    private string GetProgramBuildLog(Program program)
    {
        try
        {
            const uint buildLog = 0x1183; // CL_PROGRAM_BUILD_LOG

            var error = OpenCLRuntime.clGetProgramBuildInfo(
                program,
                _deviceInfo.DeviceId,
                buildLog,
                0,
                nint.Zero,
                out var logSize);

            if (error != OpenCLError.Success || logSize == 0)
                return string.Empty;

            var logBuffer = new byte[logSize];
            unsafe
            {
                fixed (byte* ptr = logBuffer)
                {
                    error = OpenCLRuntime.clGetProgramBuildInfo(
                        program,
                        _deviceInfo.DeviceId,
                        buildLog,
                        logSize,
                        (nint)ptr,
                        out _);

                    if (error != OpenCLError.Success)
                        return string.Empty;
                }
            }

            return System.Text.Encoding.UTF8.GetString(logBuffer, 0, (int)logSize - 1);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Throws if this context has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OpenCLContext));
    }

    /// <summary>
    /// Disposes the OpenCL context and associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing OpenCL context");

            ReleaseObject(_commandQueue.Handle, OpenCLRuntime.clReleaseCommandQueue, "command queue");
            ReleaseObject(_context.Handle, OpenCLRuntime.clReleaseContext, "context");

            _disposed = true;
        }
    }
}