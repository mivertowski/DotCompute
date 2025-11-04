// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Orchestrates Metal kernel execution with explicit grid and thread group dimensions.
/// Manages command buffer lifecycle, encoder setup, and synchronization.
/// </summary>
public sealed class MetalExecutionEngine : IDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly MetalKernelParameterBinder _parameterBinder;
    private readonly ILogger<MetalExecutionEngine>? _logger;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalExecutionEngine"/> class.
    /// </summary>
    /// <param name="device">The Metal device handle.</param>
    /// <param name="commandQueue">The Metal command queue handle.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public MetalExecutionEngine(IntPtr device, IntPtr commandQueue, ILogger<MetalExecutionEngine>? logger = null)
    {
        if (device == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(device), "Metal device handle cannot be zero.");
        }

        if (commandQueue == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(commandQueue), "Metal command queue handle cannot be zero.");
        }

        _device = device;
        _commandQueue = commandQueue;
        _logger = logger;

        // Initialize parameter binder with same logger context
        var binderLogger = logger?.IsEnabled(LogLevel.Trace) == true
            ? LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace))
                .CreateLogger<MetalKernelParameterBinder>()
            : null;
        _parameterBinder = new MetalKernelParameterBinder(binderLogger);
    }

    /// <summary>
    /// Executes a compiled Metal kernel with explicit grid and thread group dimensions.
    /// </summary>
    /// <param name="kernel">The compiled Metal kernel to execute.</param>
    /// <param name="gridDim">The grid dimensions (number of thread groups to dispatch).</param>
    /// <param name="blockDim">The thread group dimensions (threads per thread group).</param>
    /// <param name="buffers">Buffer parameters to bind to the kernel.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous execution operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when kernel or buffers is null.</exception>
    /// <exception cref="ArgumentException">Thrown when grid or block dimensions are invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when Metal operations fail.</exception>
    /// <remarks>
    /// <para>
    /// Grid dimensions specify how many thread groups to launch. For example, gridDim=(10, 1, 1)
    /// launches 10 thread groups in the X dimension.
    /// </para>
    /// <para>
    /// Block dimensions specify threads per thread group. For example, blockDim=(256, 1, 1)
    /// creates thread groups with 256 threads each.
    /// </para>
    /// <para>
    /// Total threads = gridDim × blockDim. For the example above: 10 × 256 = 2,560 threads.
    /// </para>
    /// </remarks>
    public async Task ExecuteAsync(
        MetalCompiledKernel kernel,
        GridDimensions gridDim,
        GridDimensions blockDim,
        IUnifiedMemoryBuffer[] buffers,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);
        ArgumentNullException.ThrowIfNull(kernel, nameof(kernel));
        ArgumentNullException.ThrowIfNull(buffers, nameof(buffers));

        ValidateGridDimensions(gridDim, nameof(gridDim));
        ValidateGridDimensions(blockDim, nameof(blockDim));

        if (!kernel.IsReady)
        {
            throw new ArgumentException("Kernel is not ready for execution.", nameof(kernel));
        }

        _logger?.LogDebug("Executing kernel with grid({GridX}, {GridY}, {GridZ}), block({BlockX}, {BlockY}, {BlockZ})",
            gridDim.X, gridDim.Y, gridDim.Z, blockDim.X, blockDim.Y, blockDim.Z);

        // Validate buffer compatibility
        if (!_parameterBinder.ValidateBuffers(buffers))
        {
            throw new ArgumentException("One or more buffers are not valid Metal buffers.", nameof(buffers));
        }

        // Create command buffer
        IntPtr commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
        if (commandBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal command buffer.");
        }

        try
        {
            // Create compute command encoder
            IntPtr encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
            if (encoder == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create compute command encoder.");
            }

            try
            {
                // Get the pipeline state from the compiled kernel
                IntPtr pipelineState = GetPipelineStateFromKernel(kernel);
                if (pipelineState == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get pipeline state from compiled kernel.");
                }

                // Set the compute pipeline state
                MetalNative.SetComputePipelineState(encoder, pipelineState);

                // Bind buffer parameters
                _parameterBinder.BindParameters(encoder, buffers);

                // Convert GridDimensions to Metal sizes
                // Note: gridDim already represents NUMBER OF THREADGROUPS (not total threads!)
                // blockDim represents THREADS PER THREADGROUP
                // This matches CUDA semantics: gridDim = number of blocks, blockDim = threads per block
                var metalThreadgroupSize = new MetalSize
                {
                    width = (nuint)blockDim.X,
                    height = (nuint)blockDim.Y,
                    depth = (nuint)blockDim.Z
                };

                var metalGridSize = new MetalSize
                {
                    width = (nuint)gridDim.X,   // Number of threadgroups in X
                    height = (nuint)gridDim.Y,  // Number of threadgroups in Y
                    depth = (nuint)gridDim.Z    // Number of threadgroups in Z
                };

                _logger?.LogTrace("Dispatching: threadgroups({GroupX}, {GroupY}, {GroupZ}), threads per group({ThreadX}, {ThreadY}, {ThreadZ})",
                    metalGridSize.width, metalGridSize.height, metalGridSize.depth,
                    metalThreadgroupSize.width, metalThreadgroupSize.height, metalThreadgroupSize.depth);

                // Dispatch the kernel
                MetalNative.DispatchThreadgroups(encoder, metalGridSize, metalThreadgroupSize);
            }
            finally
            {
                // Always end encoding before releasing, even if an exception occurred
                MetalNative.EndEncoding(encoder);
                MetalNative.ReleaseEncoder(encoder);
            }

            // Setup completion handler
            var tcs = new TaskCompletionSource<bool>();
            MetalNative.SetCommandBufferCompletionHandler(commandBuffer, (status) =>
            {
                if (status == MetalCommandBufferStatus.Completed)
                {
                    _ = tcs.TrySetResult(true);
                    _logger?.LogDebug("Kernel execution completed successfully");
                }
                else
                {
                    var error = new InvalidOperationException($"Metal kernel execution failed with status: {status}");
                    _ = tcs.TrySetException(error);
                    _logger?.LogError(error, "Kernel execution failed");
                }
            });

            // Commit the command buffer
            MetalNative.CommitCommandBuffer(commandBuffer);

            // Wait for completion
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            // Command buffers cannot be reused after commit - always release
            MetalNative.ReleaseCommandBuffer(commandBuffer);
        }
    }

    /// <summary>
    /// Executes a compiled Metal kernel with explicit grid and thread group dimensions (synchronous version).
    /// </summary>
    /// <param name="kernel">The compiled Metal kernel to execute.</param>
    /// <param name="gridDim">The grid dimensions (number of thread groups to dispatch).</param>
    /// <param name="blockDim">The thread group dimensions (threads per thread group).</param>
    /// <param name="buffers">Buffer parameters to bind to the kernel.</param>
    /// <exception cref="ArgumentNullException">Thrown when kernel or buffers is null.</exception>
    /// <exception cref="ArgumentException">Thrown when grid or block dimensions are invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when Metal operations fail.</exception>
    /// <remarks>
    /// This is a synchronous convenience method that blocks until kernel execution completes.
    /// For better performance in async contexts, use <see cref="ExecuteAsync"/> instead.
    /// </remarks>
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks or awaiters is intentional in this synchronous wrapper method
    public void Execute(
        MetalCompiledKernel kernel,
        GridDimensions gridDim,
        GridDimensions blockDim,
        params IUnifiedMemoryBuffer[] buffers)
    {
        ExecuteAsync(kernel, gridDim, blockDim, buffers, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
#pragma warning restore VSTHRD002

    /// <summary>
    /// Validates grid dimensions to ensure they are within acceptable ranges.
    /// </summary>
    /// <param name="dimensions">The dimensions to validate.</param>
    /// <param name="paramName">The parameter name for exception messages.</param>
    /// <exception cref="ArgumentException">Thrown when dimensions are invalid.</exception>
    private static void ValidateGridDimensions(GridDimensions dimensions, string paramName)
    {
        if (dimensions.X <= 0 || dimensions.Y <= 0 || dimensions.Z <= 0)
        {
            throw new ArgumentException(
                $"Grid dimensions must be positive. Got: ({dimensions.X}, {dimensions.Y}, {dimensions.Z})",
                paramName);
        }

        // Metal has practical limits on dispatch dimensions
        // These are conservative limits that work across all Metal devices
        const int MaxDispatchDimension = 65535;

        if (dimensions.X > MaxDispatchDimension || dimensions.Y > MaxDispatchDimension || dimensions.Z > MaxDispatchDimension)
        {
            throw new ArgumentException(
                $"Grid dimensions exceed maximum allowed ({MaxDispatchDimension}). Got: ({dimensions.X}, {dimensions.Y}, {dimensions.Z})",
                paramName);
        }
    }

    /// <summary>
    /// Extracts the pipeline state handle from a compiled Metal kernel using reflection.
    /// </summary>
    /// <param name="kernel">The compiled kernel.</param>
    /// <returns>The pipeline state handle.</returns>
    /// <exception cref="InvalidOperationException">Thrown when pipeline state cannot be extracted.</exception>
    /// <remarks>
    /// This method uses reflection to access the private _pipelineState field.
    /// This is necessary because MetalCompiledKernel doesn't expose the pipeline state publicly.
    /// </remarks>
    private static IntPtr GetPipelineStateFromKernel(MetalCompiledKernel kernel)
    {
        var kernelType = typeof(MetalCompiledKernel);

#pragma warning disable IL2075 // Reflection on kernel type is safe - Metal backend controls kernel types
        var pipelineStateField = kernelType.GetField("_pipelineState",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
#pragma warning restore IL2075

        if (pipelineStateField == null)
        {
            throw new InvalidOperationException(
                "Unable to access pipeline state from MetalCompiledKernel. Internal structure may have changed.");
        }

        var pipelineStateValue = pipelineStateField.GetValue(kernel);
        if (pipelineStateValue is IntPtr pipelineState)
        {
            return pipelineState;
        }

        throw new InvalidOperationException(
            "Pipeline state field exists but has unexpected type or null value.");
    }

    /// <summary>
    /// Disposes the execution engine and releases native resources.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Note: _device and _commandQueue are not owned by this class, so we don't release them
        // They are managed by MetalAccelerator

        GC.SuppressFinalize(this);
    }
}
