// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Execution.Graph.Types;

namespace DotCompute.Backends.Metal.Execution.Interfaces;

/// <summary>
/// Abstraction for Metal command execution operations.
/// This interface decouples graph execution logic from low-level Metal API calls,
/// enabling testability and potential support for other GPU backends.
/// </summary>
public interface IMetalCommandExecutor
{
    /// <summary>
    /// Creates a Metal command buffer from the command queue.
    /// </summary>
    /// <param name="commandQueue">The Metal command queue.</param>
    /// <returns>Handle to the created command buffer.</returns>
    public IntPtr CreateCommandBuffer(IntPtr commandQueue);

    /// <summary>
    /// Creates a compute command encoder for kernel execution.
    /// </summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <returns>Handle to the created compute encoder.</returns>
    public IntPtr CreateComputeCommandEncoder(IntPtr commandBuffer);

    /// <summary>
    /// Creates a blit command encoder for memory operations.
    /// </summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <returns>Handle to the created blit encoder.</returns>
    public IntPtr CreateBlitCommandEncoder(IntPtr commandBuffer);

    /// <summary>
    /// Sets the compute pipeline state for kernel execution.
    /// </summary>
    /// <param name="encoder">The compute encoder.</param>
    /// <param name="kernel">The kernel to execute.</param>
    public void SetComputePipelineState(IntPtr encoder, object kernel);

    /// <summary>
    /// Sets a kernel argument at the specified index.
    /// </summary>
    /// <param name="encoder">The compute encoder.</param>
    /// <param name="index">The argument index.</param>
    /// <param name="argument">The argument value.</param>
    public void SetKernelArgument(IntPtr encoder, int index, object argument);

    /// <summary>
    /// Dispatches compute threadgroups for kernel execution.
    /// </summary>
    /// <param name="encoder">The compute encoder.</param>
    /// <param name="threadgroupsPerGrid">Number of threadgroups per grid.</param>
    /// <param name="threadsPerThreadgroup">Number of threads per threadgroup.</param>
    public void DispatchThreadgroups(IntPtr encoder, MTLSize threadgroupsPerGrid, MTLSize threadsPerThreadgroup);

    /// <summary>
    /// Copies data between Metal buffers.
    /// </summary>
    /// <param name="encoder">The blit encoder.</param>
    /// <param name="sourceBuffer">Source buffer handle.</param>
    /// <param name="sourceOffset">Offset in source buffer.</param>
    /// <param name="destBuffer">Destination buffer handle.</param>
    /// <param name="destOffset">Offset in destination buffer.</param>
    /// <param name="size">Number of bytes to copy.</param>
    public void CopyBuffer(IntPtr encoder, IntPtr sourceBuffer, long sourceOffset, IntPtr destBuffer, long destOffset, long size);

    /// <summary>
    /// Fills a Metal buffer with a specific byte value.
    /// </summary>
    /// <param name="encoder">The blit encoder.</param>
    /// <param name="buffer">Buffer handle.</param>
    /// <param name="value">Fill value.</param>
    /// <param name="size">Number of bytes to fill.</param>
    public void FillBuffer(IntPtr encoder, IntPtr buffer, byte value, long size);

    /// <summary>
    /// Ends command encoding.
    /// </summary>
    /// <param name="encoder">The encoder to end.</param>
    public void EndEncoding(IntPtr encoder);

    /// <summary>
    /// Commits a command buffer and waits for completion.
    /// </summary>
    /// <param name="commandBuffer">The command buffer to commit.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the command buffer finishes execution.</returns>
    public Task CommitAndWaitAsync(IntPtr commandBuffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a command buffer and its resources.
    /// </summary>
    /// <param name="commandBuffer">The command buffer to release.</param>
    public void ReleaseCommandBuffer(IntPtr commandBuffer);
}
