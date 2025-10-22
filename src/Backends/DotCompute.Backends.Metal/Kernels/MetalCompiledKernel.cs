// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Memory;
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Metal backend has dynamic logging requirements

namespace DotCompute.Backends.Metal.Kernels;


/// <summary>
/// Represents a compiled Metal kernel ready for execution.
/// </summary>
public sealed class MetalCompiledKernel(
KernelDefinition definition,
IntPtr pipelineState,
IntPtr commandQueue,
int maxTotalThreadsPerThreadgroup,
(int x, int y, int z) threadExecutionWidth,
CompilationMetadata metadata,
ILogger logger,
MetalCommandBufferPool? commandBufferPool = null) : ICompiledKernel
{
    private readonly KernelDefinition _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    private readonly IntPtr _pipelineState = pipelineState;
    private readonly IntPtr _commandQueue = commandQueue;
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    [SuppressMessage("IDisposableAnalyzers.Correctness", "CA2213:Disposable fields should be disposed",
        Justification = "Command buffer pool is shared infrastructure managed by MetalAccelerator lifecycle. See Dispose() method comment.")]
    private readonly MetalCommandBufferPool? _commandBufferPool = commandBufferPool;
    private readonly int _maxTotalThreadsPerThreadgroup = maxTotalThreadsPerThreadgroup;
    private readonly (int x, int y, int z) _threadExecutionWidth = threadExecutionWidth;
    private readonly CompilationMetadata _metadata = metadata;
    private int _disposed;

    /// <inheritdoc/>
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public string Name => _definition.Name;

    /// <inheritdoc/>
    public bool IsReady => _disposed == 0 && _pipelineState != IntPtr.Zero;

    /// <inheritdoc/>
    public static string BackendType => "Metal";

    /// <inheritdoc/>
    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        // arguments is non-nullable, no need for null check

        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        _logger.LogDebug("Executing kernel: {Name}", Name);

        // Get command buffer from pool or create new one
        IntPtr commandBuffer;
        if (_commandBufferPool != null)
        {
            commandBuffer = _commandBufferPool.GetCommandBuffer();
        }
        else
        {
            commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
            if (commandBuffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create command buffer");
            }
        }

        try
        {
            // Create compute command encoder
            var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
            if (encoder == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create compute command encoder");
            }

            try
            {
                // Set the compute pipeline state
                MetalNative.SetComputePipelineState(encoder, _pipelineState);

                // Set kernel arguments
                SetKernelArguments(encoder, arguments);

                // Calculate dispatch dimensions
                var (gridSize, threadgroupSize) = CalculateDispatchDimensions(arguments);

                _logger.LogDebug("Kernel dispatch: grid({Width}, {Height}, {Depth}), threadgroup({TWidth}, {THeight}, {TDepth})",
                    gridSize.width, gridSize.height, gridSize.depth,
                    threadgroupSize.width, threadgroupSize.height, threadgroupSize.depth);

                // Dispatch the kernel
                MetalNative.DispatchThreadgroups(encoder, gridSize, threadgroupSize);

                // End encoding
                MetalNative.EndEncoding(encoder);
            }
            finally
            {
                MetalNative.ReleaseEncoder(encoder);
            }

            // Add completion handler
            var tcs = new TaskCompletionSource<bool>();
            MetalNative.SetCommandBufferCompletionHandler(commandBuffer, (status) =>
            {
                if (status == MetalCommandBufferStatus.Completed)
                {
                    _ = tcs.TrySetResult(true);
                    _logger.LogDebug("Kernel execution completed: {Name}", Name);
                }
                else
                {
                    var error = new InvalidOperationException($"Metal kernel execution failed with status: {status}");
                    _ = tcs.TrySetException(error);
                    _logger.LogError(error, "Kernel execution failed: {Name}", Name);
                }
            });

            // Commit the command buffer
            MetalNative.CommitCommandBuffer(commandBuffer);

            // Wait for completion
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                _ = await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            if (_commandBufferPool != null)
            {
                _commandBufferPool.ReturnCommandBuffer(commandBuffer);
            }
            else
            {
                MetalNative.ReleaseCommandBuffer(commandBuffer);
            }
        }
    }

    private void SetKernelArguments(IntPtr encoder, KernelArguments arguments)
    {
        var bufferIndex = 0;

        // Set buffer arguments
        foreach (var arg in arguments.Arguments)
        {
            if (arg is IUnifiedMemoryBuffer memoryBuffer)
            {
                // Ensure it's a Metal buffer
                if (memoryBuffer is MetalMemoryBuffer metalMemory)
                {
                    MetalNative.SetBuffer(encoder, metalMemory.Buffer, 0, bufferIndex);
                }
                else if (memoryBuffer is MetalMemoryBufferView view)
                {
                    // Handle buffer view with proper offset
                    var parentBuffer = view.ParentBuffer;
                    var offsetBytes = view.Offset;
                    MetalNative.SetBuffer(encoder, parentBuffer, (nuint)offsetBytes, bufferIndex);
                }
                else
                {
                    throw new ArgumentException($"Argument at index {bufferIndex} is not a Metal buffer");
                }
            }
            else if (arg is Dim3 dim3)
            {
                // Handle dimensions as three separate uint arguments
                unsafe
                {
                    var x = (uint)dim3.X;
                    var y = (uint)dim3.Y;
                    var z = (uint)dim3.Z;

                    MetalNative.SetBytes(encoder, (IntPtr)(&x), sizeof(uint), bufferIndex++);
                    MetalNative.SetBytes(encoder, (IntPtr)(&y), sizeof(uint), bufferIndex++);
                    MetalNative.SetBytes(encoder, (IntPtr)(&z), sizeof(uint), bufferIndex);
                }
            }
            else
            {
                // Handle scalar values
                if (arg != null)
                {
                    var size = GetScalarSize(arg);
                    var bytes = GetScalarBytes(arg);
                    unsafe
                    {
                        fixed (byte* ptr = bytes)
                        {
                            MetalNative.SetBytes(encoder, (IntPtr)ptr, (nuint)size, bufferIndex);
                        }
                    }
                }
                else
                {
                    // Handle null arguments by setting zero bytes
                    unsafe
                    {
                        var nullValue = 0;
                        MetalNative.SetBytes(encoder, (IntPtr)(&nullValue), sizeof(int), bufferIndex);
                    }
                }
            }

            bufferIndex++;
        }
    }

    private (MetalSize gridSize, MetalSize threadgroupSize) CalculateDispatchDimensions(KernelArguments arguments)
    {
        // Calculate optimal dispatch dimensions based on kernel arguments and device capabilities
        var threadgroupSize = CalculateOptimalThreadgroupSize();
        var gridSize = CalculateOptimalGridSize(arguments, threadgroupSize);

        _logger.LogDebug("Kernel calculated dispatch: grid({Width}, {Height}, {Depth}), threadgroup({TWidth}, {THeight}, {TDepth})",
            gridSize.width, gridSize.height, gridSize.depth,
            threadgroupSize.width, threadgroupSize.height, threadgroupSize.depth);

        return (gridSize, threadgroupSize);
    }

    private MetalSize CalculateOptimalThreadgroupSize()
    {
        // Use thread execution width as a baseline for optimal threadgroup size
        var width = Math.Min(_threadExecutionWidth.x, _maxTotalThreadsPerThreadgroup);
        var height = 1;
        var depth = 1;

        // For larger work, consider 2D threadgroups if beneficial
        if (_maxTotalThreadsPerThreadgroup >= 64)
        {
            if (width > 32)
            {
                height = Math.Min(width / 32, 4);
                width = width / height;
            }
        }

        return new MetalSize
        {
            width = (nuint)width,
            height = (nuint)height,
            depth = (nuint)depth
        };
    }

    private MetalSize CalculateOptimalGridSize(KernelArguments arguments, MetalSize threadgroupSize)
    {
        // Look for dimension information in kernel arguments
        var workDimensions = ExtractWorkDimensionsFromArguments(arguments);

        var gridWidth = Math.Max(1, (int)Math.Ceiling((double)workDimensions.x / threadgroupSize.width));
        var gridHeight = Math.Max(1, (int)Math.Ceiling((double)workDimensions.y / threadgroupSize.height));
        var gridDepth = Math.Max(1, (int)Math.Ceiling((double)workDimensions.z / threadgroupSize.depth));

        return new MetalSize
        {
            width = (nuint)gridWidth,
            height = (nuint)gridHeight,
            depth = (nuint)gridDepth
        };
    }

    private (long x, long y, long z) ExtractWorkDimensionsFromArguments(KernelArguments arguments)
    {
        // Look for Dim3 arguments that specify work dimensions
        foreach (var arg in arguments.Arguments)
        {
            if (arg is Dim3 dim3)
            {
                return (dim3.X, dim3.Y, dim3.Z);
            }
        }

        // Default work dimensions based on kernel metadata
        var defaultWorkSize = _metadata.EstimatedWorkSize ?? 1024;
        return (defaultWorkSize, 1, 1);
    }

    private static int GetScalarSize(object value)
    {
        return value switch
        {
            byte => sizeof(byte),
            sbyte => sizeof(sbyte),
            short => sizeof(short),
            ushort => sizeof(ushort),
            int => sizeof(int),
            uint => sizeof(uint),
            long => sizeof(long),
            ulong => sizeof(ulong),
            float => sizeof(float),
            double => sizeof(double),
            bool => sizeof(bool),
            _ => throw new NotSupportedException($"Scalar type {value.GetType()} is not supported")
        };
    }

    private static byte[] GetScalarBytes(object value)
    {
        return value switch
        {
            byte b => [b],
            sbyte sb => [(byte)sb],
            short s => BitConverter.GetBytes(s),
            ushort us => BitConverter.GetBytes(us),
            int i => BitConverter.GetBytes(i),
            uint ui => BitConverter.GetBytes(ui),
            long l => BitConverter.GetBytes(l),
            ulong ul => BitConverter.GetBytes(ul),
            float f => BitConverter.GetBytes(f),
            double d => BitConverter.GetBytes(d),
            bool b => BitConverter.GetBytes(b),
            _ => throw new NotSupportedException($"Scalar type {value.GetType()} is not supported")
        };
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Run(Dispose).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Release pipeline state
        if (_pipelineState != IntPtr.Zero)
        {
            MetalNative.ReleasePipelineState(_pipelineState);
        }

        // Note: _commandBufferPool is shared and managed by MetalAccelerator lifecycle, not disposed here

        GC.SuppressFinalize(this);
    }

    ~MetalCompiledKernel()
    {
        if (_disposed == 0 && _pipelineState != IntPtr.Zero)
        {
            MetalNative.ReleasePipelineState(_pipelineState);
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Convert object array to KernelArguments
        var kernelArguments = new KernelArguments(parameters);

        // Call the existing ExecuteAsync method
        await ExecuteAsync(kernelArguments, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public object Metadata => new
    {
        Name = Name,
        BackendType = BackendType,
        IsReady = IsReady,
        MaxTotalThreadsPerThreadgroup = _maxTotalThreadsPerThreadgroup,
        ThreadExecutionWidth = _threadExecutionWidth,
        CompilationMetadata = _metadata,
        KernelDefinition = new
        {
            _definition.Name,
            _definition.EntryPoint,
            _definition.Language,
            // No Parameters property available in KernelDefinition
            ParameterCount = 0
        }
    };
}

/// <summary>
/// Metadata associated with compiled Metal kernels.
/// </summary>
public class CompilationMetadata
{
    /// <summary>
    /// Gets or sets the estimated work size for the kernel.
    /// </summary>
    public long? EstimatedWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the compilation time in milliseconds.
    /// </summary>
    public double CompilationTimeMs { get; set; }

    /// <summary>
    /// Gets or sets whether the kernel supports thread divergence.
    /// </summary>
    public bool SupportsThreadDivergence { get; set; }

    /// <summary>
    /// Gets the memory usage characteristics.
    /// </summary>
    public Dictionary<string, object> MemoryUsage { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets any compiler warnings generated during compilation.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets additional metadata properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}
