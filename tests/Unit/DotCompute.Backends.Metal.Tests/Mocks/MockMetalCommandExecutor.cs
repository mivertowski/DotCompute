// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Execution.Graph.Types;
using DotCompute.Backends.Metal.Execution.Interfaces;

namespace DotCompute.Backends.Metal.Tests.Mocks;

/// <summary>
/// Mock implementation of <see cref="IMetalCommandExecutor"/> for unit testing.
/// Simulates Metal command execution without requiring actual GPU hardware.
/// </summary>
public sealed class MockMetalCommandExecutor : IMetalCommandExecutor
{
    private int _nextHandle = 0x1000;
    private readonly Dictionary<IntPtr, string> _handleTypes = new();
    private readonly object _lock = new();

    public MockMetalCommandExecutor()
    {
    }

    private IntPtr AllocateHandle(string type)
    {
        lock (_lock)
        {
            var handle = new IntPtr(_nextHandle++);
            _handleTypes[handle] = type;
            return handle;
        }
    }

    /// <inheritdoc/>
    public IntPtr CreateCommandBuffer(IntPtr commandQueue)
    {
        if (commandQueue == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command queue handle", nameof(commandQueue));
        }

        return AllocateHandle("CommandBuffer");
    }

    /// <inheritdoc/>
    public IntPtr CreateComputeCommandEncoder(IntPtr commandBuffer)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command buffer handle", nameof(commandBuffer));
        }

        return AllocateHandle("ComputeEncoder");
    }

    /// <inheritdoc/>
    public IntPtr CreateBlitCommandEncoder(IntPtr commandBuffer)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command buffer handle", nameof(commandBuffer));
        }

        return AllocateHandle("BlitEncoder");
    }

    /// <inheritdoc/>
    public void SetComputePipelineState(IntPtr encoder, object kernel)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        ArgumentNullException.ThrowIfNull(kernel);

        // Mock implementation - just validate parameters
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

        // Mock implementation - just validate parameters
    }

    /// <inheritdoc/>
    public void DispatchThreadgroups(IntPtr encoder, MTLSize threadgroupsPerGrid, MTLSize threadsPerThreadgroup)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        // Mock implementation - just validate parameters
    }

    /// <inheritdoc/>
    public void CopyBuffer(IntPtr encoder, IntPtr sourceBuffer, long sourceOffset, IntPtr destBuffer, long destOffset, long size)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

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

        // Mock implementation - just validate parameters
    }

    /// <inheritdoc/>
    public void FillBuffer(IntPtr encoder, IntPtr buffer, byte value, long size)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        if (buffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid buffer handle", nameof(buffer));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive");
        }

        // Mock implementation - just validate parameters
    }

    /// <inheritdoc/>
    public void EndEncoding(IntPtr encoder)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid encoder handle", nameof(encoder));
        }

        // Mock implementation - just validate parameters
    }

    /// <inheritdoc/>
    public async Task CommitAndWaitAsync(IntPtr commandBuffer, CancellationToken cancellationToken = default)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid command buffer handle", nameof(commandBuffer));
        }

        // Simulate minimal GPU execution time
        await Task.Delay(1, cancellationToken);
    }

    /// <inheritdoc/>
    public void ReleaseCommandBuffer(IntPtr commandBuffer)
    {
        if (commandBuffer == IntPtr.Zero)
        {
            return; // Nothing to release
        }

        // Mock implementation - just remove from tracking
        lock (_lock)
        {
            _handleTypes.Remove(commandBuffer);
        }
    }
}
