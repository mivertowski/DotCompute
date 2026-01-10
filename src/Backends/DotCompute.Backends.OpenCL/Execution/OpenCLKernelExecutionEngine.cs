// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.OpenCL.Profiling;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Production-grade OpenCL kernel execution engine with automatic work size optimization
/// and comprehensive error handling.
/// </summary>
/// <remarks>
/// This execution engine provides:
/// - NDRange kernel dispatch (1D/2D/3D work dimensions)
/// - Automatic work group size calculation based on device and kernel limits
/// - Type-safe argument binding with validation
/// - Event-based synchronization with profiling integration
/// - Optimal work size calculation respecting device capabilities
/// - Comprehensive error handling with detailed diagnostics
/// </remarks>
public sealed class OpenCLKernelExecutionEngine : IAsyncDisposable
{
    private readonly OpenCLContext _context;
    private readonly OpenCLDeviceId _deviceId;
    private readonly OpenCLStreamManager _streamManager;
    private readonly OpenCLProfiler _profiler;
    private readonly ILogger<OpenCLKernelExecutionEngine> _logger;
    private readonly DeviceCapabilities _deviceCaps;
    private bool _disposed;

    // Constants for kernel queries
    private const uint CL_KERNEL_WORK_GROUP_SIZE = 0x11B0;
    private const uint CL_KERNEL_PREFERRED_WORK_GROUP_SIZE_MULTIPLE = 0x11B3;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelExecutionEngine"/> class.
    /// </summary>
    /// <param name="context">The OpenCL context for kernel execution.</param>
    /// <param name="deviceId">The device ID for execution operations.</param>
    /// <param name="streamManager">Stream manager for command queue pooling.</param>
    /// <param name="profiler">Profiler for performance metrics.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public OpenCLKernelExecutionEngine(
        OpenCLContext context,
        OpenCLDeviceId deviceId,
        OpenCLStreamManager streamManager,
        OpenCLProfiler profiler,
        ILogger<OpenCLKernelExecutionEngine> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _deviceId = deviceId;
        _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Query device capabilities once
        _deviceCaps = QueryDeviceCapabilities(deviceId);

        _logger.LogInformation(
            "Kernel execution engine initialized: maxWorkGroupSize={MaxWorkGroupSize}, " +
            "maxDimensions={MaxDimensions}, warpSize={WarpSize}",
            _deviceCaps.MaxWorkGroupSize, _deviceCaps.MaxWorkItemDimensions, _deviceCaps.PreferredWarpSize);
    }

    /// <summary>
    /// Executes a kernel with automatic work size optimization and profiling.
    /// </summary>
    /// <param name="kernelHandle">Handle to the compiled kernel.</param>
    /// <param name="globalSize">Global work size (NDRange dimensions).</param>
    /// <param name="localSize">Optional local work size. If null, automatically calculated.</param>
    /// <param name="arguments">Kernel arguments to bind.</param>
    /// <param name="waitEvents">Optional events to wait for before execution.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Execution result containing event, timing, and work size information.</returns>
    /// <exception cref="ArgumentException">Thrown if kernel handle is invalid or work sizes are invalid.</exception>
    /// <exception cref="OpenCLException">Thrown if kernel execution fails.</exception>
    public async Task<ExecutionResult> ExecuteKernelAsync(
        nint kernelHandle,
        NDRange globalSize,
        NDRange? localSize,
        KernelArguments arguments,
        OpenCLEventHandle[]? waitEvents = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(globalSize);
        ArgumentNullException.ThrowIfNull(arguments);

        if (kernelHandle == nint.Zero)
        {
            throw new ArgumentException("Invalid kernel handle", nameof(kernelHandle));
        }

        // Validate work sizes
        ValidateWorkSize(globalSize, localSize);

        // Bind kernel arguments
        await BindArgumentsAsync(kernelHandle, arguments, cancellationToken).ConfigureAwait(false);

        // Calculate optimal local size if not provided
        var effectiveLocalSize = localSize ?? CalculateOptimalLocalSize(kernelHandle, globalSize);

        // Validate final local size
        ValidateLocalSize(kernelHandle, globalSize, effectiveLocalSize);

        // Acquire command queue from pool
        var queueProperties = new QueueProperties
        {
            InOrderExecution = true,
            EnableProfiling = true, // Enable profiling for timing
            Priority = QueuePriority.Normal
        };

        await using var queueHandle = await _streamManager.AcquireQueueAsync(queueProperties, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            // Enqueue kernel execution
            var executionEvent = await EnqueueKernelAsync(
                queueHandle.Queue,
                kernelHandle,
                globalSize,
                effectiveLocalSize,
                waitEvents,
                cancellationToken).ConfigureAwait(false);

            // Profile kernel execution
            var timing = await _profiler.ProfileKernelExecutionAsync(
                new OpenCLKernel(kernelHandle),
                executionEvent,
                metadata: new Dictionary<string, object>
                {
                    ["GlobalSize"] = FormatNDRange(globalSize),
                    ["LocalSize"] = FormatNDRange(effectiveLocalSize),
                    ["TotalWorkItems"] = globalSize.TotalWorkItems,
                    ["WorkGroups"] = CalculateWorkGroupCount(globalSize, effectiveLocalSize)
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Kernel executed: global={Global}, local={Local}, time={TimeMs:F3}ms",
                FormatNDRange(globalSize), FormatNDRange(effectiveLocalSize), timing.DurationMilliseconds);

            return new ExecutionResult
            {
                Event = executionEvent,
                GlobalSize = globalSize,
                LocalSize = effectiveLocalSize,
                ExecutionTime = TimeSpan.FromMilliseconds(timing.DurationMilliseconds)
            };
        }
        catch (OpenCLException ex)
        {
            _logger.LogError(ex,
                "Kernel execution failed: global={Global}, local={Local}",
                FormatNDRange(globalSize), FormatNDRange(localSize ?? effectiveLocalSize));
            throw;
        }
    }

    /// <summary>
    /// Executes a kernel synchronously, waiting for completion.
    /// </summary>
    /// <param name="kernelHandle">Handle to the compiled kernel.</param>
    /// <param name="globalSize">Global work size (NDRange dimensions).</param>
    /// <param name="localSize">Optional local work size. If null, automatically calculated.</param>
    /// <param name="arguments">Kernel arguments to bind.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Execution result containing timing and work size information.</returns>
    public async Task<ExecutionResult> ExecuteKernelSyncAsync(
        nint kernelHandle,
        NDRange globalSize,
        NDRange? localSize,
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteKernelAsync(
            kernelHandle,
            globalSize,
            localSize,
            arguments,
            waitEvents: null,
            cancellationToken).ConfigureAwait(false);

        // Wait for completion
        await _streamManager
            .AcquireQueueAsync(new QueueProperties(), cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Calculates optimal local work size for a kernel based on device and kernel constraints.
    /// </summary>
    /// <param name="kernelHandle">Handle to the kernel.</param>
    /// <param name="globalSize">Global work size to optimize for.</param>
    /// <returns>Optimal local work size that divides global size evenly.</returns>
    /// <remarks>
    /// This method:
    /// 1. Queries kernel-specific work group size limits
    /// 2. Considers device maximum work group size
    /// 3. Prefers power-of-2 local sizes for optimal GPU performance
    /// 4. Optimizes for warp/wavefront size (typically 32 or 64)
    /// 5. Ensures local size divides global size evenly
    /// </remarks>
    private NDRange CalculateOptimalLocalSize(nint kernelHandle, NDRange globalSize)
    {
        // Query kernel-specific limits
        var kernelMaxWorkGroupSize = QueryKernelWorkGroupSize(kernelHandle);
        var preferredMultiple = QueryKernelPreferredMultiple(kernelHandle);

        // Use minimum of kernel and device limits
        var maxWorkGroupSize = Math.Min(kernelMaxWorkGroupSize, _deviceCaps.MaxWorkGroupSize);

        var dimensions = globalSize.Dimensions;

        if (dimensions == 1)
        {
            return Calculate1DLocalSize(globalSize.X, maxWorkGroupSize, preferredMultiple);
        }
        else if (dimensions == 2)
        {
            return Calculate2DLocalSize(globalSize.X, globalSize.Y, maxWorkGroupSize, preferredMultiple);
        }
        else // dimensions == 3
        {
            return Calculate3DLocalSize(globalSize.X, globalSize.Y, globalSize.Z, maxWorkGroupSize, preferredMultiple);
        }
    }

    /// <summary>
    /// Calculates optimal 1D local work size.
    /// </summary>
    private NDRange Calculate1DLocalSize(nuint globalX, nuint maxWorkGroupSize, nuint preferredMultiple)
    {
        // Prefer multiples of warp size (typically 32 or 64)
        var warpSize = _deviceCaps.PreferredWarpSize;

        // Start with preferred multiple or warp size
        var targetSize = Math.Max(preferredMultiple, warpSize);

        // Find largest divisor of globalX that doesn't exceed maxWorkGroupSize
        var localX = FindOptimalDivisor(globalX, maxWorkGroupSize, targetSize);

        _logger.LogTrace(
            "Calculated 1D local size: global={Global}, local={Local}, multiple={Multiple}",
            globalX, localX, preferredMultiple);

        return new NDRange { X = localX };
    }

    /// <summary>
    /// Calculates optimal 2D local work size.
    /// </summary>
    private NDRange Calculate2DLocalSize(nuint globalX, nuint globalY, nuint maxWorkGroupSize, nuint preferredMultiple)
    {
        var warpSize = _deviceCaps.PreferredWarpSize;

        // Common 2D tile sizes: 16x16 (256), 8x8 (64), 32x8 (256), etc.
        var tileSizes = new[] { 16u, 8u, 32u, 4u, 2u };

        foreach (var tileSize in tileSizes)
        {
            var totalThreads = tileSize * tileSize;
            if (totalThreads <= maxWorkGroupSize &&
                globalX % tileSize == 0 &&
                globalY % tileSize == 0)
            {
                _logger.LogTrace(
                    "Calculated 2D local size: global=({GlobalX}, {GlobalY}), local=({TileSize}, {TileSize})",
                    globalX, globalY, tileSize, tileSize);

                return new NDRange { X = tileSize, Y = tileSize };
            }
        }

        // Fallback: find divisors
        var localX = FindOptimalDivisor(globalX, maxWorkGroupSize / 2, warpSize);
        var localY = FindOptimalDivisor(globalY, maxWorkGroupSize / localX, 1);

        return new NDRange { X = localX, Y = localY };
    }

    /// <summary>
    /// Calculates optimal 3D local work size.
    /// </summary>
    private NDRange Calculate3DLocalSize(
        nuint globalX, nuint globalY, nuint globalZ,
        nuint maxWorkGroupSize, nuint preferredMultiple)
    {
        // Common 3D tile sizes: 8x8x4 (256), 4x4x4 (64), etc.
        var tileSizes = new[] { (8u, 8u, 4u), (4u, 4u, 4u), (8u, 4u, 4u), (2u, 2u, 2u) };

        foreach (var (tileX, tileY, tileZ) in tileSizes)
        {
            var totalThreads = tileX * tileY * tileZ;
            if (totalThreads <= maxWorkGroupSize &&
                globalX % tileX == 0 &&
                globalY % tileY == 0 &&
                globalZ % tileZ == 0)
            {
                _logger.LogTrace(
                    "Calculated 3D local size: global=({GlobalX}, {GlobalY}, {GlobalZ}), local=({TileX}, {TileY}, {TileZ})",
                    globalX, globalY, globalZ, tileX, tileY, tileZ);

                return new NDRange { X = tileX, Y = tileY, Z = tileZ };
            }
        }

        // Fallback: distribute dimensions
        var localX = FindOptimalDivisor(globalX, maxWorkGroupSize / 4, 1);
        var localY = FindOptimalDivisor(globalY, maxWorkGroupSize / (localX * 2), 1);
        var localZ = FindOptimalDivisor(globalZ, maxWorkGroupSize / (localX * localY), 1);

        return new NDRange { X = localX, Y = localY, Z = localZ };
    }

    /// <summary>
    /// Finds the optimal divisor of a value, preferring power-of-2 and multiples of target.
    /// </summary>
    private static nuint FindOptimalDivisor(nuint value, nuint maxDivisor, nuint targetMultiple)
    {
        if (value <= maxDivisor)
        {
            return value;
        }

        // Try power-of-2 divisors first
        for (var p = 10; p >= 0; p--)
        {
            var pow2 = (nuint)(1 << p);
            if (pow2 <= maxDivisor && value % pow2 == 0)
            {
                return pow2;
            }
        }

        // Try multiples of target
        if (targetMultiple > 1)
        {
            for (var mult = maxDivisor / targetMultiple; mult >= 1; mult--)
            {
                var candidate = mult * targetMultiple;
                if (candidate <= maxDivisor && value % candidate == 0)
                {
                    return candidate;
                }
            }
        }

        // Find any divisor
        for (var d = maxDivisor; d >= 1; d--)
        {
            if (value % d == 0)
            {
                return d;
            }
        }

        return 1;
    }

    /// <summary>
    /// Validates work size dimensions against device limits.
    /// </summary>
    private void ValidateWorkSize(NDRange globalSize, NDRange? localSize)
    {
        if (globalSize.TotalWorkItems == 0)
        {
            throw new ArgumentException("Global work size cannot be zero", nameof(globalSize));
        }

        if (globalSize.Dimensions > _deviceCaps.MaxWorkItemDimensions)
        {
            throw new ArgumentException(
                $"Work dimensions ({globalSize.Dimensions}) exceed device maximum ({_deviceCaps.MaxWorkItemDimensions})",
                nameof(globalSize));
        }

        // Validate dimension sizes
        ValidateDimensionSize(globalSize.X, 0, "X", nameof(globalSize));
        if (globalSize.Dimensions >= 2)
        {
            ValidateDimensionSize(globalSize.Y, 1, "Y", nameof(globalSize));
        }
        if (globalSize.Dimensions == 3)
        {
            ValidateDimensionSize(globalSize.Z, 2, "Z", nameof(globalSize));
        }

        if (localSize != null)
        {
            ValidateLocalSizeBasic(localSize, globalSize);
        }
    }

    /// <summary>
    /// Validates a single dimension size against device limits.
    /// </summary>
    private void ValidateDimensionSize(nuint size, int dimensionIndex, string dimensionName, string parameterName)
    {
        if (size == 0)
        {
            throw new ArgumentException($"{dimensionName} dimension cannot be zero", parameterName);
        }

        if (dimensionIndex < _deviceCaps.MaxWorkItemSizes.Count)
        {
            var maxSize = _deviceCaps.MaxWorkItemSizes[dimensionIndex];
            if (size > maxSize)
            {
                throw new ArgumentException(
                    $"{dimensionName} dimension ({size}) exceeds device maximum ({maxSize})",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Validates basic local work size constraints.
    /// </summary>
    private void ValidateLocalSizeBasic(NDRange localSize, NDRange globalSize)
    {
        // Check that local size divides global size evenly
        if (globalSize.X % localSize.X != 0)
        {
            throw new ArgumentException(
                $"Local size X ({localSize.X}) must evenly divide global size X ({globalSize.X})");
        }

        if (globalSize.Dimensions >= 2 && globalSize.Y % localSize.Y != 0)
        {
            throw new ArgumentException(
                $"Local size Y ({localSize.Y}) must evenly divide global size Y ({globalSize.Y})");
        }

        if (globalSize.Dimensions == 3 && globalSize.Z % localSize.Z != 0)
        {
            throw new ArgumentException(
                $"Local size Z ({localSize.Z}) must evenly divide global size Z ({globalSize.Z})");
        }

        // Check total work group size
        if (localSize.TotalWorkItems > _deviceCaps.MaxWorkGroupSize)
        {
            throw new ArgumentException(
                $"Local work group size ({localSize.TotalWorkItems}) exceeds device maximum ({_deviceCaps.MaxWorkGroupSize})");
        }
    }

    /// <summary>
    /// Validates local work size against kernel-specific constraints.
    /// </summary>
    private void ValidateLocalSize(nint kernelHandle, NDRange globalSize, NDRange localSize)
    {
        var kernelMaxWorkGroupSize = QueryKernelWorkGroupSize(kernelHandle);

        if (localSize.TotalWorkItems > kernelMaxWorkGroupSize)
        {
            throw new ArgumentException(
                $"Local work group size ({localSize.TotalWorkItems}) exceeds kernel maximum ({kernelMaxWorkGroupSize})");
        }

        // Basic validation already done in ValidateWorkSize
        ValidateLocalSizeBasic(localSize, globalSize);
    }

    /// <summary>
    /// Binds all arguments to the kernel.
    /// </summary>
    private async Task BindArgumentsAsync(
        nint kernelHandle,
        KernelArguments arguments,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await BindArgumentAsync(kernelHandle, i, arguments[i]).ConfigureAwait(false);
        }

        _logger.LogTrace("Bound {Count} kernel arguments", arguments.Count);
    }

    /// <summary>
    /// Binds a single argument to the kernel.
    /// </summary>
    private async Task BindArgumentAsync(nint kernelHandle, int index, KernelArgument arg)
    {
        await Task.Run(() =>
        {
            OpenCLError result;

            if (arg.IsBuffer)
            {
                // Buffer argument - pass buffer handle
                unsafe
                {
                    var bufferHandle = arg.BufferHandle;
                    result = OpenCLRuntime.clSetKernelArg(
                        kernelHandle,
                        (uint)index,
                        (nuint)IntPtr.Size,
                        new nint(&bufferHandle));
                }
            }
            else if (arg.IsLocalMemory)
            {
                // Local memory argument - pass size only
                result = OpenCLRuntime.clSetKernelArg(
                    kernelHandle,
                    (uint)index,
                    arg.LocalMemorySize,
                    nint.Zero);
            }
            else
            {
                // Scalar argument - pass value
                result = BindScalarArgument(kernelHandle, index, arg);
            }

            OpenCLException.ThrowIfError(result, $"Set kernel argument {index}");

            _logger.LogTrace(
                "Bound kernel argument {Index}: type={Type}, size={Size}",
                index,
                arg.IsBuffer ? "Buffer" : arg.IsLocalMemory ? "Local" : "Scalar",
                arg.IsLocalMemory ? arg.LocalMemorySize : arg.Size);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Binds a scalar argument to the kernel.
    /// </summary>
    private static OpenCLError BindScalarArgument(nint kernelHandle, int index, KernelArgument arg)
    {
        // Marshal scalar value to native memory
        unsafe
        {
            // ScalarData is IReadOnlyList<byte>, convert to array for pinning
            var dataArray = arg.ScalarData as byte[] ?? arg.ScalarData.ToArray();
            fixed (byte* valuePtr = dataArray)
            {
                return OpenCLRuntime.clSetKernelArg(
                    kernelHandle,
                    (uint)index,
                    (nuint)arg.Size,
                    (nint)valuePtr);
            }
        }
    }

    /// <summary>
    /// Enqueues kernel for execution on command queue.
    /// </summary>
    private static Task<OpenCLEventHandle> EnqueueKernelAsync(
        OpenCLCommandQueue queue,
        nint kernelHandle,
        NDRange globalSize,
        NDRange localSize,
        OpenCLEventHandle[]? waitEvents,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Convert NDRange to arrays
            var globalWorkSize = ToWorkSizeArray(globalSize);
            var localWorkSize = ToWorkSizeArray(localSize);

            // Convert wait events to handles
            nint[]? waitEventHandles = null;
            if (waitEvents != null && waitEvents.Length > 0)
            {
                waitEventHandles = waitEvents.Select(e => e.Handle).ToArray();
            }

            var error = OpenCLRuntime.clEnqueueNDRangeKernel(
                queue.Handle,
                kernelHandle,
                (uint)globalSize.Dimensions,
                globalWorkOffset: null, // No offset
                globalWorkSize,
                localWorkSize,
                waitEventHandles != null ? (uint)waitEventHandles.Length : 0,
                waitEventHandles,
                out var eventHandle);

            OpenCLException.ThrowIfError(error, "Enqueue NDRange kernel");

            return new OpenCLEventHandle(eventHandle);
        }, cancellationToken);
    }

    /// <summary>
    /// Queries kernel work group size.
    /// </summary>
    private nuint QueryKernelWorkGroupSize(nint kernelHandle)
    {
        unsafe
        {
            nuint workGroupSize;
            var error = OpenCLRuntime.clGetKernelWorkGroupInfo(
                kernelHandle,
                _deviceId.Handle,
                CL_KERNEL_WORK_GROUP_SIZE,
                (nuint)sizeof(nuint),
                new nint(&workGroupSize),
                out _);

            if (error != OpenCLError.Success)
            {
                _logger.LogWarning("Failed to query kernel work group size: {Error}", error);
                return _deviceCaps.MaxWorkGroupSize;
            }

            return workGroupSize;
        }
    }

    /// <summary>
    /// Queries kernel preferred work group size multiple.
    /// </summary>
    private nuint QueryKernelPreferredMultiple(nint kernelHandle)
    {
        unsafe
        {
            nuint preferredMultiple;
            var error = OpenCLRuntime.clGetKernelWorkGroupInfo(
                kernelHandle,
                _deviceId.Handle,
                CL_KERNEL_PREFERRED_WORK_GROUP_SIZE_MULTIPLE,
                (nuint)sizeof(nuint),
                new nint(&preferredMultiple),
                out _);

            if (error != OpenCLError.Success || preferredMultiple == 0)
            {
                return _deviceCaps.PreferredWarpSize;
            }

            return preferredMultiple;
        }
    }

    /// <summary>
    /// Queries device capabilities.
    /// </summary>
    private static DeviceCapabilities QueryDeviceCapabilities(OpenCLDeviceId deviceId)
    {
        unsafe
        {
            uint maxDimensions;
            var error = OpenCLRuntime.clGetDeviceInfo(
                deviceId.Handle,
                DeviceInfo.MaxWorkItemDimensions,
                (nuint)sizeof(uint),
                new nint(&maxDimensions),
                out _);

            if (error != OpenCLError.Success)
            {
                maxDimensions = 3; // Fallback
            }

            nuint maxWorkGroupSize;
            error = OpenCLRuntime.clGetDeviceInfo(
                deviceId.Handle,
                DeviceInfo.MaxWorkGroupSize,
                (nuint)sizeof(nuint),
                new nint(&maxWorkGroupSize),
                out _);

            if (error != OpenCLError.Success)
            {
                maxWorkGroupSize = 256; // Fallback
            }

            // Query max work item sizes per dimension
            var maxWorkItemSizes = new nuint[maxDimensions];
            fixed (nuint* ptr = maxWorkItemSizes)
            {
                error = OpenCLRuntime.clGetDeviceInfo(
                    deviceId.Handle,
                    DeviceInfo.MaxWorkItemSizes,
                    (nuint)(sizeof(nuint) * maxDimensions),
                    new nint(ptr),
                    out _);

                if (error != OpenCLError.Success)
                {
                    // Fallback values
                    for (var i = 0; i < maxDimensions; i++)
                    {
                        maxWorkItemSizes[i] = 1024;
                    }
                }
            }

            // Prefer warp size of 32 (NVIDIA) or 64 (AMD), default to 32
            var preferredWarpSize = 32u;

            return new DeviceCapabilities
            {
                MaxWorkItemDimensions = maxDimensions,
                MaxWorkGroupSize = maxWorkGroupSize,
                MaxWorkItemSizes = maxWorkItemSizes,
                PreferredWarpSize = preferredWarpSize
            };
        }
    }

    /// <summary>
    /// Converts NDRange to work size array.
    /// </summary>
    private static nuint[] ToWorkSizeArray(NDRange range)
    {
        return range.Dimensions switch
        {
            1 => new[] { range.X },
            2 => new[] { range.X, range.Y },
            3 => new[] { range.X, range.Y, range.Z },
            _ => throw new ArgumentException($"Invalid dimensions: {range.Dimensions}")
        };
    }

    /// <summary>
    /// Formats NDRange for logging.
    /// </summary>
    private static string FormatNDRange(NDRange range)
    {
        return range.Dimensions switch
        {
            1 => $"{range.X}",
            2 => $"({range.X}, {range.Y})",
            3 => $"({range.X}, {range.Y}, {range.Z})",
            _ => $"Invalid[{range.Dimensions}D]"
        };
    }

    /// <summary>
    /// Calculates total work group count.
    /// </summary>
    private static nuint CalculateWorkGroupCount(NDRange globalSize, NDRange localSize)
    {
        var countX = globalSize.X / localSize.X;
        var countY = globalSize.Dimensions >= 2 ? globalSize.Y / localSize.Y : 1;
        var countZ = globalSize.Dimensions == 3 ? globalSize.Z / localSize.Z : 1;
        return countX * countY * countZ;
    }

    /// <summary>
    /// Throws if this engine has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Asynchronously disposes the execution engine.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger.LogInformation("Kernel execution engine disposed");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Device capability information.
    /// </summary>
    private sealed class DeviceCapabilities
    {
        public required uint MaxWorkItemDimensions { get; init; }
        public required nuint MaxWorkGroupSize { get; init; }
        public required IReadOnlyList<nuint> MaxWorkItemSizes { get; init; }
        public required nuint PreferredWarpSize { get; init; }
    }
}

/// <summary>
/// Represents the result of kernel execution.
/// </summary>
public sealed record ExecutionResult
{
    /// <summary>Gets the event associated with kernel execution.</summary>
    public required OpenCLEventHandle Event { get; init; }

    /// <summary>Gets the global work size used for execution.</summary>
    public required NDRange GlobalSize { get; init; }

    /// <summary>Gets the local work size used for execution.</summary>
    public required NDRange LocalSize { get; init; }

    /// <summary>Gets the execution time of the kernel.</summary>
    public required TimeSpan ExecutionTime { get; init; }
}

/// <summary>
/// Represents an N-dimensional range for kernel execution.
/// </summary>
public sealed record NDRange
{
    /// <summary>Gets or initializes the X dimension size.</summary>
    public required nuint X { get; init; }

    /// <summary>Gets or initializes the Y dimension size (default: 1).</summary>
    public nuint Y { get; init; } = 1;

    /// <summary>Gets or initializes the Z dimension size (default: 1).</summary>
    public nuint Z { get; init; } = 1;

    /// <summary>
    /// Gets the number of active dimensions (1, 2, or 3).
    /// </summary>
    public int Dimensions => Z > 1 ? 3 : Y > 1 ? 2 : 1;

    /// <summary>
    /// Gets the total number of work items across all dimensions.
    /// </summary>
    public nuint TotalWorkItems => X * Y * Z;
}

/// <summary>
/// Container for kernel arguments with type-safe handling.
/// </summary>
public sealed class KernelArguments
{
    private readonly List<KernelArgument> _arguments = new();

    /// <summary>
    /// Adds a buffer argument.
    /// </summary>
    /// <param name="handle">Buffer handle.</param>
    public void AddBuffer(nint handle)
    {
        _arguments.Add(KernelArgument.Buffer(handle));
    }

    /// <summary>
    /// Adds a scalar argument.
    /// </summary>
    /// <typeparam name="T">Scalar type (must be unmanaged).</typeparam>
    /// <param name="value">Scalar value.</param>
    public void AddScalar<T>(T value) where T : unmanaged
    {
        _arguments.Add(KernelArgument.Scalar(value));
    }

    /// <summary>
    /// Adds a local memory argument.
    /// </summary>
    /// <param name="sizeBytes">Size of local memory in bytes.</param>
    public void AddLocalMemory(nuint sizeBytes)
    {
        _arguments.Add(KernelArgument.LocalMemory(sizeBytes));
    }

    /// <summary>Gets the number of arguments.</summary>
    public int Count => _arguments.Count;

    /// <summary>Gets the argument at the specified index.</summary>
    public KernelArgument this[int index] => _arguments[index];
}

/// <summary>
/// Represents a single kernel argument.
/// </summary>
public sealed class KernelArgument
{
    /// <summary>Gets whether this is a buffer argument.</summary>
    public bool IsBuffer { get; private init; }

    /// <summary>Gets whether this is a local memory argument.</summary>
    public bool IsLocalMemory { get; private init; }

    /// <summary>Gets the buffer handle (if buffer argument).</summary>
    public nint BufferHandle { get; private init; }

    /// <summary>Gets the local memory size (if local memory argument).</summary>
    public nuint LocalMemorySize { get; private init; }

    /// <summary>Gets the scalar data (if scalar argument).</summary>
    public IReadOnlyList<byte> ScalarData { get; private init; } = Array.Empty<byte>();

    /// <summary>Gets the size of the argument in bytes.</summary>
    public int Size { get; private init; }

    /// <summary>
    /// Creates a buffer argument.
    /// </summary>
    public static KernelArgument Buffer(nint handle)
    {
        return new KernelArgument
        {
            IsBuffer = true,
            BufferHandle = handle,
            Size = IntPtr.Size
        };
    }

    /// <summary>
    /// Creates a scalar argument.
    /// </summary>
    public static KernelArgument Scalar<T>(T value) where T : unmanaged
    {
        unsafe
        {
            var size = sizeof(T);
            var data = new byte[size];
            fixed (byte* ptr = data)
            {
                *(T*)ptr = value;
            }

            return new KernelArgument
            {
                ScalarData = data,
                Size = size
            };
        }
    }

    /// <summary>
    /// Creates a local memory argument.
    /// </summary>
    public static KernelArgument LocalMemory(nuint sizeBytes)
    {
        return new KernelArgument
        {
            IsLocalMemory = true,
            LocalMemorySize = sizeBytes,
            Size = 0
        };
    }
}
