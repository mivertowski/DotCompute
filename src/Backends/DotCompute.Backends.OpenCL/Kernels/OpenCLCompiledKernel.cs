// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Kernels;

/// <summary>
/// OpenCL implementation of a compiled kernel.
/// Manages kernel execution with proper argument binding and work group configuration.
/// </summary>
internal sealed class OpenCLCompiledKernel : ICompiledKernel
{
    private readonly OpenCLContext _context;
    private readonly ILogger<OpenCLCompiledKernel> _logger;
    private readonly object _lock = new();

    private readonly Guid _id;
    private readonly string _name;
    private readonly Kernel _kernel;
    private readonly Program _program;
    private bool _disposed;

    /// <summary>
    /// Gets the kernel unique identifier.
    /// </summary>
    public Guid Id => _id;

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the OpenCL kernel handle.
    /// </summary>
    public Kernel Kernel => _kernel;

    /// <summary>
    /// Gets whether the kernel has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLCompiledKernel"/> class.
    /// </summary>
    /// <param name="context">The OpenCL context.</param>
    /// <param name="program">The compiled program containing the kernel.</param>
    /// <param name="kernel">The OpenCL kernel handle.</param>
    /// <param name="name">The kernel name.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLCompiledKernel(
        OpenCLContext context,
        Program program,
        Kernel kernel,
        string name,
        ILogger<OpenCLCompiledKernel> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _program = program;
        _kernel = kernel;
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _id = Guid.NewGuid();

        _logger.LogDebug("Created OpenCL compiled kernel: {KernelName} (ID: {KernelId})", _name, _id);
    }

    /// <summary>
    /// Executes the kernel with the given arguments.
    /// </summary>
    /// <param name="arguments">Kernel arguments including buffers and scalar values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (arguments.Arguments == null || arguments.Arguments.Count == 0)
        {
            _logger.LogWarning("Executing kernel {KernelName} with no arguments", _name);
        }

        _logger.LogDebug("Executing OpenCL kernel: {KernelName} with {ArgumentCount} arguments", _name, arguments.Arguments?.Count ?? 0);

        try
        {
            // Set kernel arguments
            await SetKernelArgumentsAsync(arguments, cancellationToken);

            // Determine work group configuration
            var workConfig = DetermineWorkGroupConfiguration(arguments);

            // Execute kernel
            var executionEvent = _context.EnqueueKernel(
                _kernel,
                workConfig.WorkDimensions,
                workConfig.GlobalWorkSize,
                workConfig.LocalWorkSize);

            // Wait for completion
            _context.WaitForEvents(executionEvent);

            // Release event
            OpenCLContext.ReleaseObject(executionEvent.Handle, OpenCLRuntime.clReleaseEvent, "execution event");

            _logger.LogDebug("Successfully executed OpenCL kernel: {KernelName}", _name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute OpenCL kernel: {KernelName}", _name);
            throw;
        }
    }

    /// <summary>
    /// Sets kernel arguments from the provided argument collection.
    /// </summary>
    private async Task SetKernelArgumentsAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        if (arguments.Arguments == null)
        {
            return;
        }

        uint argIndex = 0;
        foreach (var argument in arguments.Arguments)
        {
            await SetKernelArgumentAsync(argIndex++, argument!, cancellationToken);
        }
    }

    /// <summary>
    /// Sets a single kernel argument.
    /// </summary>
    private async Task SetKernelArgumentAsync(uint index, object argument, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogTrace("Setting kernel argument {Index}: {Type}", index, argument?.GetType().Name ?? "null");

        // Handle buffer arguments
        if (argument is IUnifiedMemoryBuffer memoryBuffer)
        {
            await SetBufferArgumentAsync(index, memoryBuffer, cancellationToken);
            return;
        }

        // Handle scalar arguments
        await Task.Run(() => SetScalarArgument(index, argument!), cancellationToken);
    }

    /// <summary>
    /// Sets a buffer argument.
    /// </summary>
    private async Task SetBufferArgumentAsync(uint index, IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Try to extract OpenCL buffer handle
            if (buffer is Memory.OpenCLMemoryBuffer<float> floatBuffer)
            {
                SetBufferArgument(index, floatBuffer.Buffer);
            }
            else if (buffer is Memory.OpenCLMemoryBuffer<double> doubleBuffer)
            {
                SetBufferArgument(index, doubleBuffer.Buffer);
            }
            else if (buffer is Memory.OpenCLMemoryBuffer<int> intBuffer)
            {
                SetBufferArgument(index, intBuffer.Buffer);
            }
            else if (buffer is Memory.OpenCLMemoryBuffer<uint> uintBuffer)
            {
                SetBufferArgument(index, uintBuffer.Buffer);
            }
            else
            {
                // Generic fallback - try to get buffer handle via reflection
                [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Buffer property access is fallback mechanism for known buffer types")]
                static PropertyInfo? GetBufferProperty(object buffer) => buffer.GetType().GetProperty("Buffer");

                var bufferProperty = GetBufferProperty(buffer);
                if (bufferProperty?.GetValue(buffer) is MemObject clBuffer)
                {
                    SetBufferArgument(index, clBuffer);
                }
                else
                {
                    throw new ArgumentException($"Unsupported buffer type: {buffer.GetType().Name}");
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Sets a buffer argument using the OpenCL buffer handle.
    /// </summary>
    private void SetBufferArgument(uint index, MemObject buffer)
    {
        unsafe
        {
            var bufferHandle = buffer.Handle;
            var error = OpenCLRuntime.clSetKernelArg(
                _kernel,
                index,
                (nuint)sizeof(nint),
                (nint)(&bufferHandle));

            OpenCLException.ThrowIfError(error, $"Set buffer argument {index}");
        }
    }

    /// <summary>
    /// Sets a scalar argument.
    /// </summary>
    private void SetScalarArgument(uint index, object argument)
    {
        switch (argument)
        {
            case int intValue:
                SetScalarArgument(index, intValue);
                break;
            case uint uintValue:
                SetScalarArgument(index, uintValue);
                break;
            case long longValue:
                SetScalarArgument(index, longValue);
                break;
            case ulong ulongValue:
                SetScalarArgument(index, ulongValue);
                break;
            case float floatValue:
                SetScalarArgument(index, floatValue);
                break;
            case double doubleValue:
                SetScalarArgument(index, doubleValue);
                break;
            case IntPtr ptrValue:
                // IntPtr (nint) is used for local memory size specification in OpenCL
                SetLocalMemoryArgument(index, ptrValue);
                break;
            case nuint nuintValue:
                SetScalarArgument(index, nuintValue);
                break;
            default:
                throw new ArgumentException($"Unsupported argument type: {argument?.GetType().Name ?? "null"}");
        }
    }

    /// <summary>
    /// Sets a typed scalar argument.
    /// </summary>
    private void SetScalarArgument<T>(uint index, T value) where T : unmanaged
    {
        unsafe
        {
            var error = OpenCLRuntime.clSetKernelArg(
                _kernel,
                index,
                (nuint)sizeof(T),
                (nint)(&value));

            OpenCLException.ThrowIfError(error, $"Set scalar argument {index}");
        }
    }

    /// <summary>
    /// Sets a local memory argument.
    /// In OpenCL, local memory is specified by passing the size in bytes and a null pointer.
    /// </summary>
    private void SetLocalMemoryArgument(uint index, IntPtr sizeInBytes)
    {
        var error = OpenCLRuntime.clSetKernelArg(
            _kernel,
            index,
            (nuint)sizeInBytes,
            IntPtr.Zero); // null pointer indicates local memory

        OpenCLException.ThrowIfError(error, $"Set local memory argument {index}");
    }

    /// <summary>
    /// Determines the work group configuration for kernel execution.
    /// </summary>
    private WorkGroupConfiguration DetermineWorkGroupConfiguration(KernelArguments arguments)
    {
        uint workDimensions;
        nuint[] globalWorkSize;
        nuint[]? localWorkSize = null;

        // Check if KernelArguments has LaunchConfiguration
        if (arguments.LaunchConfiguration != null)
        {
            var config = arguments.LaunchConfiguration;

            // Determine dimensions based on GridSize
            if (config.GridSize.Z > 1)
            {
                workDimensions = 3u;
                globalWorkSize = [(nuint)config.GridSize.X, (nuint)config.GridSize.Y, (nuint)config.GridSize.Z];

                if (config.BlockSize.X > 0)
                {
                    localWorkSize = [(nuint)config.BlockSize.X, (nuint)config.BlockSize.Y, (nuint)config.BlockSize.Z];
                }
            }
            else if (config.GridSize.Y > 1)
            {
                workDimensions = 2u;
                globalWorkSize = [(nuint)config.GridSize.X, (nuint)config.GridSize.Y];

                if (config.BlockSize.X > 0)
                {
                    localWorkSize = [(nuint)config.BlockSize.X, (nuint)config.BlockSize.Y];
                }
            }
            else
            {
                workDimensions = 1u;
                globalWorkSize = [(nuint)config.GridSize.X];

                if (config.BlockSize.X > 0)
                {
                    localWorkSize = [(nuint)config.BlockSize.X];
                }
            }
        }
        else
        {
            // Default to 1D configuration
            workDimensions = 1u;

            // Estimate work size based on buffer sizes
            var maxElements = EstimateWorkSizeFromBuffers(arguments);
            globalWorkSize = [maxElements];

            // Check if kernel uses local memory (has IntPtr arguments)
            var usesLocalMemory = HasLocalMemoryArguments(arguments);

            if (usesLocalMemory)
            {
                // Kernels using local memory REQUIRE explicit local work group size
                // Use 256 as a safe default that works on most devices
                var maxWorkGroupSize = _context.DeviceInfo.MaxWorkGroupSize;
                var localSize = Math.Min(maxWorkGroupSize, 256);
                localWorkSize = [localSize];

                // Ensure global work size is multiple of local work size
                var remainder = (long)globalWorkSize[0] % (long)localSize;
                if (remainder != 0)
                {
                    globalWorkSize[0] += (nuint)((long)localSize - remainder);
                }
            }
            else
            {
                // For kernels without local memory, let OpenCL choose optimal size
                localWorkSize = null;
            }
        }

        _logger.LogTrace("Work group config: dimensions={Dimensions}, global=[{Global}], local=[{Local}]",
            workDimensions,
            string.Join(", ", globalWorkSize),
            localWorkSize != null ? string.Join(", ", localWorkSize) : "auto");

        return new WorkGroupConfiguration
        {
            WorkDimensions = workDimensions,
            GlobalWorkSize = globalWorkSize,
            LocalWorkSize = localWorkSize
        };
    }

    /// <summary>
    /// Checks if kernel uses local memory by detecting IntPtr arguments.
    /// </summary>
    private static bool HasLocalMemoryArguments(KernelArguments arguments)
    {
        if (arguments.Arguments == null)
        {
            return false;
        }

        return arguments.Arguments.Any(arg => arg is IntPtr);
    }

    /// <summary>
    /// Estimates work size based on buffer arguments.
    /// </summary>
    private static nuint EstimateWorkSizeFromBuffers(KernelArguments arguments)
    {
        if (arguments.Arguments == null)
        {
            return 1;
        }

        nuint maxElements = 1;
        foreach (var arg in arguments.Arguments)
        {
            if (arg is IUnifiedMemoryBuffer buffer)
            {
                maxElements = Math.Max(maxElements, (nuint)Math.Abs(buffer.SizeInBytes / 4)); // Estimate assuming 4-byte elements
            }
        }

        return maxElements;
    }

    /// <summary>
    /// Throws if this kernel has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Disposes the OpenCL kernel and associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogDebug("Disposing OpenCL kernel: {KernelName} (ID: {KernelId})", _name, _id);

            OpenCLContext.ReleaseObject(_kernel.Handle, OpenCLRuntime.clReleaseKernel, "kernel");
            OpenCLContext.ReleaseObject(_program.Handle, OpenCLRuntime.clReleaseProgram, "program");

            _disposed = true;
        }
    }

    /// <summary>
    /// Disposes the OpenCL kernel and associated resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await Task.Run(Dispose);
    }

    /// <summary>
    /// Work group configuration for kernel execution.
    /// </summary>
    private sealed class WorkGroupConfiguration
    {
        public uint WorkDimensions { get; init; }
        public nuint[] GlobalWorkSize { get; init; } = [];
        public nuint[]? LocalWorkSize { get; init; }
    }
}
