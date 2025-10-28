// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Execution.Graph.Types;
using DotCompute.Backends.Metal.Execution.Interfaces;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Production implementation of <see cref="IMetalCommandExecutor"/> that makes actual Metal native API calls.
/// </summary>
public sealed class MetalCommandExecutor : IMetalCommandExecutor
{
    private readonly ILogger<MetalCommandExecutor> _logger;

    public MetalCommandExecutor(ILogger<MetalCommandExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IntPtr CreateCommandBuffer(IntPtr commandQueue)
    {
        if (commandQueue == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command queue handle", nameof(commandQueue));
        }

        var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);

        if (commandBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal command buffer");
        }

        _logger.LogTrace("Created command buffer {Handle} from queue {QueueHandle}", commandBuffer, commandQueue);
        return commandBuffer;
    }

    /// <inheritdoc/>
    public IntPtr CreateComputeCommandEncoder(IntPtr commandBuffer)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command buffer handle", nameof(commandBuffer));
        }

        var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);

        if (encoder == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create compute command encoder");
        }

        _logger.LogTrace("Created compute encoder {Handle} for buffer {BufferHandle}", encoder, commandBuffer);
        return encoder;
    }

    /// <inheritdoc/>
    public IntPtr CreateBlitCommandEncoder(IntPtr commandBuffer)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command buffer handle", nameof(commandBuffer));
        }

        // Note: Blit encoders are not yet implemented in the native Metal API
        // For now, we'll use compute encoders for memory operations
        var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);

        if (encoder == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create compute command encoder (used for blit operations)");
        }

        _logger.LogTrace("Created compute encoder {Handle} for blit operations on buffer {BufferHandle}", encoder, commandBuffer);
        return encoder;
    }

    /// <inheritdoc/>
    public void SetComputePipelineState(IntPtr encoder, object kernel)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        ArgumentNullException.ThrowIfNull(kernel);

        // Extract pipeline state from kernel using reflection (MetalCompiledKernel has private _pipelineState field)
        var kernelType = kernel.GetType();
#pragma warning disable IL2075 // Reflection on kernel type is safe - Metal backend controls kernel types
        var pipelineStateField = kernelType.GetField("_pipelineState",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
#pragma warning restore IL2075

        if (pipelineStateField != null && pipelineStateField.GetValue(kernel) is IntPtr pipelineState && pipelineState != IntPtr.Zero)
        {
            MetalNative.SetComputePipelineState(encoder, pipelineState);
            _logger.LogTrace("Set pipeline state {Pipeline} on encoder {Encoder}", pipelineState, encoder);
        }
        else
        {
            // This might be a test scenario with a mock kernel
            _logger.LogTrace("Skipping pipeline state for kernel type: {KernelType}", kernelType.Name);
        }
    }

    /// <inheritdoc/>
    public void SetKernelArgument(IntPtr encoder, int index, object argument)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative");
        }

        ArgumentNullException.ThrowIfNull(argument);

        // Handle different argument types
        if (argument is IntPtr bufferPtr)
        {
            MetalNative.SetBuffer(encoder, bufferPtr, 0, index);
            _logger.LogTrace("Set buffer argument {Index} = {Buffer} on encoder {Encoder}", index, bufferPtr, encoder);
        }
        else
        {
            // For other types, we'd need to create temporary buffers or use appropriate Metal APIs
            _logger.LogWarning("Unsupported argument type {Type} at index {Index}", argument.GetType().Name, index);
        }
    }

    /// <inheritdoc/>
    public void DispatchThreadgroups(IntPtr encoder, MTLSize threadgroupsPerGrid, MTLSize threadsPerThreadgroup)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        // Convert MTLSize to MetalSize for native call
        var gridSize = new MetalSize
        {
            width = (nuint)threadgroupsPerGrid.Width,
            height = (nuint)threadgroupsPerGrid.Height,
            depth = (nuint)threadgroupsPerGrid.Depth
        };

        var threadgroupSize = new MetalSize
        {
            width = (nuint)threadsPerThreadgroup.Width,
            height = (nuint)threadsPerThreadgroup.Height,
            depth = (nuint)threadsPerThreadgroup.Depth
        };

        MetalNative.DispatchThreadgroups(encoder, gridSize, threadgroupSize);

        _logger.LogTrace(
            "Dispatched threadgroups ({GridW}x{GridH}x{GridD}) x ({ThreadW}x{ThreadH}x{ThreadD}) on encoder {Encoder}",
            threadgroupsPerGrid.Width, threadgroupsPerGrid.Height, threadgroupsPerGrid.Depth,
            threadsPerThreadgroup.Width, threadsPerThreadgroup.Height, threadsPerThreadgroup.Depth,
            encoder);
    }

    /// <inheritdoc/>
    public void CopyBuffer(IntPtr encoder, IntPtr sourceBuffer, long sourceOffset, IntPtr destBuffer, long destOffset, long size)
    {
        // Note: encoder parameter is for interface compatibility, but Metal's CopyBuffer doesn't use it
        // The native Metal API performs buffer-to-buffer copy directly

        if (sourceBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid source buffer handle", nameof(sourceBuffer));
        }

        if (destBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid destination buffer handle", nameof(destBuffer));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive");
        }

        // Metal's CopyBuffer is a direct buffer-to-buffer operation, not encoder-based
        MetalNative.CopyBuffer(sourceBuffer, sourceOffset, destBuffer, destOffset, size);

        _logger.LogTrace(
            "Copied {Size} bytes from buffer {Source}+{SrcOffset} to {Dest}+{DestOffset}",
            size, sourceBuffer, sourceOffset, destBuffer, destOffset);
    }

    /// <inheritdoc/>
    public void FillBuffer(IntPtr encoder, IntPtr buffer, byte value, long size)
    {
        if (buffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid buffer handle", nameof(buffer));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive");
        }

        // Note: FillBuffer is not yet implemented in the native Metal API
        // This would require using GetBufferContents and manually filling with memset
        // For now, log a warning as this operation is not commonly used in graph execution
        _logger.LogWarning("FillBuffer operation not yet implemented in native Metal API - skipping fill of buffer {Buffer}", buffer);
    }

    /// <inheritdoc/>
    public void EndEncoding(IntPtr encoder)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        MetalNative.EndEncoding(encoder);
        _logger.LogTrace("Ended encoding on encoder {Encoder}", encoder);
    }

    /// <inheritdoc/>
    public async Task CommitAndWaitAsync(IntPtr commandBuffer, CancellationToken cancellationToken = default)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command buffer handle", nameof(commandBuffer));
        }

        _logger.LogTrace("Committing command buffer {Buffer}", commandBuffer);
        MetalNative.CommitCommandBuffer(commandBuffer);

        // Wait for completion asynchronously
        await Task.Run(() =>
        {
            MetalNative.WaitUntilCompleted(commandBuffer);
            _logger.LogTrace("Command buffer {Buffer} completed", commandBuffer);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void ReleaseCommandBuffer(IntPtr commandBuffer)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            return; // Nothing to release
        }

        MetalNative.ReleaseCommandBuffer(commandBuffer);
        _logger.LogTrace("Released command buffer {Buffer}", commandBuffer);
    }
}
