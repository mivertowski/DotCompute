// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Kernels;

/// <summary>
/// Represents a compiled Metal kernel ready for execution.
/// </summary>
public sealed class MetalCompiledKernel : ICompiledKernel
{
    private readonly KernelDefinition _definition;
    private readonly IntPtr _pipelineState;
    private readonly IntPtr _function;
    private readonly IntPtr _commandQueue;
    private readonly ILogger _logger;
    private readonly int _maxTotalThreadsPerThreadgroup;
    private readonly (int x, int y, int z) _threadExecutionWidth;
    private readonly CompilationMetadata _metadata;
    private int _disposed;

    public MetalCompiledKernel(
        KernelDefinition definition,
        IntPtr pipelineState,
        IntPtr function,
        IntPtr commandQueue,
        int maxTotalThreadsPerThreadgroup,
        (int x, int y, int z) threadExecutionWidth,
        CompilationMetadata metadata,
        ILogger logger)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _pipelineState = pipelineState;
        _function = function;
        _commandQueue = commandQueue;
        _maxTotalThreadsPerThreadgroup = maxTotalThreadsPerThreadgroup;
        _threadExecutionWidth = threadExecutionWidth;
        _metadata = metadata;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => _definition.Name;

    /// <inheritdoc/>
    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (_disposed > 0)
        {
            throw new ObjectDisposedException(nameof(MetalCompiledKernel));
        }

        _logger.LogTrace("Executing Metal kernel: {Name}", Name);

        // Create command buffer
        var commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
        if (commandBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create command buffer");
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

                _logger.LogTrace(
                    "Dispatching kernel with grid size: ({GridX}, {GridY}, {GridZ}), threadgroup size: ({ThreadX}, {ThreadY}, {ThreadZ})",
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
                    tcs.TrySetResult(true);
                    _logger.LogTrace("Metal kernel execution completed: {Name}", Name);
                }
                else
                {
                    var error = new InvalidOperationException($"Metal kernel execution failed with status: {status}");
                    tcs.TrySetException(error);
                    _logger.LogError(error, "Metal kernel execution failed: {Name}", Name);
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
            MetalNative.ReleaseCommandBuffer(commandBuffer);
        }
    }

    private void SetKernelArguments(IntPtr encoder, KernelArguments arguments)
    {
        var bufferIndex = 0;

        // Set buffer arguments
        foreach (var arg in arguments.Arguments)
        {
            if (arg is IMemoryBuffer memoryBuffer)
            {
                // Ensure it's a Metal buffer
                if (memoryBuffer is MetalMemoryBuffer metalMemory)
                {
                    MetalNative.SetBuffer(encoder, metalMemory.Buffer, 0, bufferIndex);
                }
                else if (memoryBuffer is MetalMemoryBufferView view)
                {
                    // Handle view - we need to get the parent buffer
                    // This is a simplification - in production we'd need access to the parent
                    throw new NotSupportedException("Memory buffer views are not yet supported as kernel arguments");
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
                    uint x = (uint)dim3.X;
                    uint y = (uint)dim3.Y;
                    uint z = (uint)dim3.Z;
                    
                    MetalNative.SetBytes(encoder, (IntPtr)(&x), sizeof(uint), bufferIndex++);
                    MetalNative.SetBytes(encoder, (IntPtr)(&y), sizeof(uint), bufferIndex++);
                    MetalNative.SetBytes(encoder, (IntPtr)(&z), sizeof(uint), bufferIndex);
                }
            }
            else
            {
                // Handle scalar values
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

            bufferIndex++;
        }
    }

    private (MetalSize gridSize, MetalSize threadgroupSize) CalculateDispatchDimensions(KernelArguments arguments)
    {
        // Look for dimension information in the arguments
        // This is a simplified implementation - in production we'd have a more robust way
        // to pass execution configuration
        var threadgroupSize = new MetalSize
        {
            width = (nuint)_threadExecutionWidth.x,
            height = 1,
            depth = 1
        };

        // Default grid size - this should be configurable
        var gridSize = new MetalSize
        {
            width = 256,
            height = 1,
            depth = 1
        };

        // Adjust threadgroup size based on kernel characteristics
        var totalThreads = threadgroupSize.width * threadgroupSize.height * threadgroupSize.depth;
        if (totalThreads > (nuint)_maxTotalThreadsPerThreadgroup)
        {
            threadgroupSize.width = (nuint)_maxTotalThreadsPerThreadgroup;
        }

        return (gridSize, threadgroupSize);
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
            byte b => new[] { b },
            sbyte sb => new[] { (byte)sb },
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await Task.Run(() =>
        {
            // Release pipeline state
            if (_pipelineState != IntPtr.Zero)
            {
                MetalNative.ReleasePipelineState(_pipelineState);
            }
        }).ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }

    ~MetalCompiledKernel()
    {
        if (_disposed == 0 && _pipelineState != IntPtr.Zero)
        {
            MetalNative.ReleasePipelineState(_pipelineState);
        }
    }
}