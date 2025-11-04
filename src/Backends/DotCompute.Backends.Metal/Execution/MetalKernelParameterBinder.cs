// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Handles parameter binding for Metal kernel execution.
/// Maps IUnifiedMemoryBuffer instances to Metal buffer bindings on compute command encoders.
/// </summary>
public sealed class MetalKernelParameterBinder
{
    private readonly ILogger<MetalKernelParameterBinder>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalKernelParameterBinder"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public MetalKernelParameterBinder(ILogger<MetalKernelParameterBinder>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Binds buffer parameters to a Metal compute command encoder.
    /// </summary>
    /// <param name="encoder">The Metal compute command encoder.</param>
    /// <param name="buffers">Array of buffers to bind as kernel parameters.</param>
    /// <exception cref="ArgumentNullException">Thrown when encoder is zero or buffers is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any buffer is not a Metal buffer or has invalid state.</exception>
    /// <remarks>
    /// Buffers are bound sequentially starting at index 0. The order must match the kernel's parameter declaration.
    /// For example, a kernel declared as:
    /// <code>
    /// kernel void myKernel(device float* a [[buffer(0)]],
    ///                      device float* b [[buffer(1)]],
    ///                      device float* result [[buffer(2)]])
    /// </code>
    /// Requires buffers to be passed in the order [a, b, result].
    /// </remarks>
    public void BindParameters(IntPtr encoder, params IUnifiedMemoryBuffer[] buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers, nameof(buffers));

        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(encoder), "Compute command encoder handle cannot be zero.");
        }

        _logger?.LogDebug("Binding {Count} parameters to Metal compute encoder", buffers.Length);

        for (int i = 0; i < buffers.Length; i++)
        {
            BindBuffer(encoder, buffers[i], i);
        }

        _logger?.LogDebug("Successfully bound all {Count} parameters", buffers.Length);
    }

    /// <summary>
    /// Binds a single buffer to a specific index on the compute encoder.
    /// </summary>
    /// <param name="encoder">The Metal compute command encoder.</param>
    /// <param name="buffer">The buffer to bind.</param>
    /// <param name="index">The binding index (must match [[buffer(N)]] in kernel).</param>
    /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
    /// <exception cref="ArgumentException">Thrown when buffer is not a Metal buffer or has invalid state.</exception>
    public void BindBuffer(IntPtr encoder, IUnifiedMemoryBuffer buffer, int index)
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(encoder), "Compute command encoder handle cannot be zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));

        // Extract Metal-specific buffer
        IntPtr metalBufferHandle = ExtractMetalBuffer(buffer);

        if (metalBufferHandle == IntPtr.Zero)
        {
            throw new ArgumentException(
                $"Buffer at index {index} has invalid Metal buffer handle (null pointer).",
                nameof(buffer));
        }

        // Bind buffer at index with zero offset
        MetalNative.SetBuffer(encoder, metalBufferHandle, 0, index);

        _logger?.LogTrace("Bound buffer at index {Index} (handle: 0x{Handle:X})", index, metalBufferHandle);
    }

    /// <summary>
    /// Binds a buffer with a specific offset.
    /// </summary>
    /// <param name="encoder">The Metal compute command encoder.</param>
    /// <param name="buffer">The buffer to bind.</param>
    /// <param name="offset">The offset in bytes into the buffer.</param>
    /// <param name="index">The binding index (must match [[buffer(N)]] in kernel).</param>
    /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
    /// <exception cref="ArgumentException">Thrown when buffer is not a Metal buffer or has invalid state.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when offset is negative.</exception>
    public void BindBufferWithOffset(IntPtr encoder, IUnifiedMemoryBuffer buffer, nuint offset, int index)
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(encoder), "Compute command encoder handle cannot be zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));

        // Extract Metal-specific buffer
        IntPtr metalBufferHandle = ExtractMetalBuffer(buffer);

        if (metalBufferHandle == IntPtr.Zero)
        {
            throw new ArgumentException(
                $"Buffer at index {index} has invalid Metal buffer handle (null pointer).",
                nameof(buffer));
        }

        // Validate offset doesn't exceed buffer size
        if (offset >= (nuint)buffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"Offset ({offset}) exceeds buffer size ({buffer.SizeInBytes}).");
        }

        // Bind buffer at index with specified offset
        MetalNative.SetBuffer(encoder, metalBufferHandle, offset, index);

        _logger?.LogTrace("Bound buffer at index {Index} with offset {Offset} (handle: 0x{Handle:X})",
            index, offset, metalBufferHandle);
    }

    /// <summary>
    /// Binds raw bytes as a parameter (for small data like scalars or constants).
    /// </summary>
    /// <param name="encoder">The Metal compute command encoder.</param>
    /// <param name="data">Pointer to the data to bind.</param>
    /// <param name="length">Length of the data in bytes.</param>
    /// <param name="index">The binding index (must match [[buffer(N)]] in kernel).</param>
    /// <exception cref="ArgumentNullException">Thrown when data pointer is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length or index is negative.</exception>
    /// <remarks>
    /// This method is useful for binding small constant data (e.g., matrix dimensions, scalar parameters).
    /// For data larger than 4KB, consider using a buffer instead for better performance.
    /// The data is copied into the command buffer, so the source memory doesn't need to remain valid.
    /// </remarks>
    public void BindBytes(IntPtr encoder, IntPtr data, nuint length, int index)
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(encoder), "Compute command encoder handle cannot be zero.");
        }

        if (data == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(data), "Data pointer cannot be zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));

        if (length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Data length must be greater than zero.");
        }

        MetalNative.SetBytes(encoder, data, length, index);

        _logger?.LogTrace("Bound {Length} bytes at index {Index}", length, index);
    }

    /// <summary>
    /// Binds a typed value as a parameter (convenience wrapper for BindBytes).
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the value.</typeparam>
    /// <param name="encoder">The Metal compute command encoder.</param>
    /// <param name="value">The value to bind.</param>
    /// <param name="index">The binding index (must match [[buffer(N)]] in kernel).</param>
    /// <exception cref="ArgumentNullException">Thrown when encoder is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is negative.</exception>
    public unsafe void BindValue<T>(IntPtr encoder, T value, int index) where T : unmanaged
    {
        if (encoder == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(encoder), "Compute command encoder handle cannot be zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));

        nuint size = (nuint)sizeof(T);
        MetalNative.SetBytes(encoder, new IntPtr(&value), size, index);

        _logger?.LogTrace("Bound value of type {TypeName} ({Size} bytes) at index {Index}",
            typeof(T).Name, size, index);
    }

    /// <summary>
    /// Extracts the native Metal buffer handle from a unified memory buffer.
    /// </summary>
    /// <param name="buffer">The unified memory buffer.</param>
    /// <returns>The native Metal buffer handle.</returns>
    /// <exception cref="ArgumentException">Thrown when buffer is not a Metal buffer type.</exception>
    private static IntPtr ExtractMetalBuffer(IUnifiedMemoryBuffer buffer)
    {
        // Try to extract Metal buffer handle
        switch (buffer)
        {
            case MetalMemoryBuffer metalBuffer:
                return metalBuffer.Buffer;

            case MetalUnifiedMemoryBuffer unifiedBuffer:
                // For unified buffers, we need the underlying Metal buffer handle
                return unifiedBuffer.NativeHandle;

            case MetalPooledBuffer pooledBuffer:
                // For pooled buffers, get the underlying buffer's handle
                return pooledBuffer.UnderlyingBuffer.Buffer;

            default:
                throw new ArgumentException(
                    $"Buffer type '{buffer.GetType().Name}' is not a supported Metal buffer type. " +
                    $"Only MetalMemoryBuffer, MetalUnifiedMemoryBuffer, and MetalPooledBuffer are supported.",
                    nameof(buffer));
        }
    }

    /// <summary>
    /// Validates that all buffers are Metal-compatible before binding.
    /// </summary>
    /// <param name="buffers">Array of buffers to validate.</param>
    /// <returns>True if all buffers are valid Metal buffers; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when buffers is null.</exception>
    public bool ValidateBuffers(params IUnifiedMemoryBuffer[] buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers, nameof(buffers));

        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] == null)
            {
                _logger?.LogWarning("Buffer at index {Index} is null", i);
                return false;
            }

            try
            {
                IntPtr handle = ExtractMetalBuffer(buffers[i]);
                if (handle == IntPtr.Zero)
                {
                    _logger?.LogWarning("Buffer at index {Index} has null Metal buffer handle", i);
                    return false;
                }
            }
            catch (ArgumentException ex)
            {
                _logger?.LogWarning(ex, "Buffer at index {Index} is not a valid Metal buffer", i);
                return false;
            }
        }

        return true;
    }
}
