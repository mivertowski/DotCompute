// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Extensions
{
    /// <summary>
    /// Extension methods for IUnifiedMemoryBuffer to provide missing methods
    /// and backward compatibility for tests and legacy code.
    /// </summary>
    public static class IUnifiedMemoryBufferExtensions
    {
        /// <summary>
        /// Gets the element count for a typed memory buffer.
        /// Calculates based on the buffer size and element size.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="buffer">The buffer to get element count for.</param>
        /// <returns>The number of elements in the buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static long ElementCount<T>(this IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            
            unsafe
            {
                return buffer.SizeInBytes / sizeof(T);
            }
        }

        /// <summary>
        /// Generic copy from operation with offset support.
        /// This method provides the generic type parameter interface required by test code.
        /// </summary>
        /// <typeparam name="TBuffer">The buffer element type.</typeparam>
        /// <typeparam name="TSource">The source element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="source">The source memory to copy from.</param>
        /// <param name="offsetInBytes">The byte offset in the target buffer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the copy operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        /// <exception cref="ArgumentException">Thrown when types don't match or parameters are invalid.</exception>
        public static ValueTask CopyFromAsync<TBuffer, TSource>(this IUnifiedMemoryBuffer<TBuffer> buffer, 
            ReadOnlyMemory<TSource> source, long offsetInBytes, CancellationToken cancellationToken = default) 
            where TBuffer : unmanaged 
            where TSource : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offsetInBytes);

            unsafe
            {
                // Verify type compatibility
                if (sizeof(TBuffer) != sizeof(TSource))
                {
                    throw new ArgumentException($"Type size mismatch: {typeof(TBuffer).Name} ({sizeof(TBuffer)} bytes) != {typeof(TSource).Name} ({sizeof(TSource)} bytes)");
                }

                // Convert to byte-compatible memory
                var sourceBytes = MemoryMarshal.Cast<TSource, byte>(source.Span);
                var sourceMemory = sourceBytes.ToArray().AsMemory(); // Convert span to memory
                
                // Use the base interface method for cross-type copying
                return ((IUnifiedMemoryBuffer)buffer).CopyFromAsync<byte>(sourceMemory, offsetInBytes, cancellationToken);
            }
        }

        /// <summary>
        /// Generic copy from operation with element offset support.
        /// This overload uses element-based offset instead of byte-based offset.
        /// </summary>
        /// <typeparam name="TBuffer">The buffer element type.</typeparam>
        /// <typeparam name="TSource">The source element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="source">The source memory to copy from.</param>
        /// <param name="offsetInElements">The element offset in the target buffer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the copy operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        /// <exception cref="ArgumentException">Thrown when types don't match or parameters are invalid.</exception>
        public static ValueTask CopyFromAsync<TBuffer, TSource>(this IUnifiedMemoryBuffer<TBuffer> buffer, 
            ReadOnlyMemory<TSource> source, int offsetInElements, CancellationToken cancellationToken = default) 
            where TBuffer : unmanaged 
            where TSource : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offsetInElements);

            unsafe
            {
                var offsetInBytes = offsetInElements * sizeof(TBuffer);
                return buffer.CopyFromAsync<TBuffer, TSource>(source, offsetInBytes, cancellationToken);
            }
        }

        /// <summary>
        /// Simplified generic copy from operation for same-type transfers.
        /// This method handles the common case where buffer and source have the same type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="source">The source memory to copy from.</param>
        /// <param name="offsetInBytes">The byte offset in the target buffer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the copy operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static ValueTask CopyFromAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            ReadOnlyMemory<T> source, long offsetInBytes, CancellationToken cancellationToken = default) 
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            
            // Use the base interface method
            return ((IUnifiedMemoryBuffer)buffer).CopyFromAsync(source, offsetInBytes, cancellationToken);
        }

        /// <summary>
        /// Reads data from the buffer into a Span at the specified offset.
        /// This handles span parameters by being non-async.
        /// </summary>
        public static void Read<T>(this IUnifiedMemoryBuffer<T> buffer, 
            Span<T> destination, int offset) where T : unmanaged
        {
            if (offset < 0)
            {

                throw new ArgumentOutOfRangeException(nameof(offset));
            }


            unsafe
            {
                var span = buffer.AsSpan();
                if (offset + destination.Length <= span.Length)
                {
                    span.Slice(offset, destination.Length).CopyTo(destination);
                }
                else
                {
                    throw new ArgumentException("Read size with offset exceeds buffer capacity");
                }
            }
        }

        /// <summary>
        /// Async wrapper for ReadAsync that delegates to synchronous span operations.
        /// This supports the test pattern of ReadAsync(array.AsSpan(), offset).
        /// </summary>
        public static ValueTask ReadAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            Span<T> destination, int offset) where T : unmanaged
        {
            buffer.Read(destination, offset);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Reads data from the buffer asynchronously into a span via array parameter.
        /// This matches the expected test signature pattern.
        /// </summary>
        public static ValueTask ReadAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            T[] destination, int offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return buffer.ReadAsync(destination.AsMemory(), offset, cancellationToken);
        }

        /// <summary>
        /// Reads data from the buffer asynchronously into a memory location.
        /// This matches the expected test signature.
        /// </summary>
        public static ValueTask ReadAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            Memory<T> destination, int offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (offset < 0)
            {

                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            // Note: This is a simplified synchronous implementation for test compatibility
            // In a real implementation, this would perform actual async memory operations

            unsafe
            {
                var span = buffer.AsSpan();
                if (offset + destination.Length <= span.Length)
                {
                    span.Slice(offset, destination.Length).CopyTo(destination.Span);
                }
                else
                {
                    throw new ArgumentException("Read size with offset exceeds buffer capacity");
                }
            }
            
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Reads data from the buffer asynchronously. 
        /// This is a compatibility method that maps to existing buffer operations.
        /// </summary>
        public static async ValueTask<T[]> ReadAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            var elementCount = buffer.ElementCount();
            var result = new T[elementCount];
            
            // Simulate async read operation
            await Task.Yield();
            
            // Note: This is a simplified implementation for test compatibility
            // In a real implementation, this would perform actual memory operations
            unsafe
            {
                var span = buffer.AsSpan();
                span.CopyTo(result.AsSpan());
            }
            
            return result;
        }

        /// <summary>
        /// Writes data to the buffer from a ReadOnlySpan at the specified offset.
        /// This handles span parameters by being non-async.
        /// </summary>
        public static void Write<T>(this IUnifiedMemoryBuffer<T> buffer, 
            ReadOnlySpan<T> data, int offset) where T : unmanaged
        {
            if (offset < 0)
            {

                throw new ArgumentOutOfRangeException(nameof(offset));
            }


            unsafe
            {
                var span = buffer.AsSpan();
                if (offset + data.Length <= span.Length)
                {
                    data.CopyTo(span.Slice(offset));
                }
                else
                {
                    throw new ArgumentException("Data size with offset exceeds buffer capacity");
                }
            }
        }

        /// <summary>
        /// Async wrapper for WriteAsync that delegates to synchronous span operations.
        /// This supports the test pattern of WriteAsync(array.AsSpan(), offset).
        /// </summary>
        public static ValueTask WriteAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            ReadOnlySpan<T> data, int offset) where T : unmanaged
        {
            buffer.Write(data, offset);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Writes data to the buffer asynchronously from array parameter.
        /// This supports the test pattern of WriteAsync(array.AsSpan(), offset).
        /// </summary>
        public static ValueTask WriteAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            T[] data, int offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return buffer.WriteAsync(data.AsMemory(), offset, cancellationToken);
        }

        /// <summary>
        /// Writes data to the buffer asynchronously with memory input.
        /// This matches the expected test signature.
        /// </summary>
        public static ValueTask WriteAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            ReadOnlyMemory<T> data, int offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (offset < 0)
            {

                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            // Note: This is a simplified synchronous implementation for test compatibility
            // In a real implementation, this would perform actual async memory operations

            unsafe
            {
                var span = buffer.AsSpan();
                if (offset + data.Length <= span.Length)
                {
                    data.Span.CopyTo(span.Slice(offset));
                }
                else
                {
                    throw new ArgumentException("Data size with offset exceeds buffer capacity");
                }
            }
            
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Writes data to the buffer asynchronously.
        /// This is a compatibility method that maps to existing buffer operations.
        /// </summary>
        public static async ValueTask WriteAsync<T>(this IUnifiedMemoryBuffer<T> buffer, 
            T[] data, CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (data == null)
            {

                throw new ArgumentNullException(nameof(data));
            }

            // Simulate async write operation

            await Task.Yield();
            
            // Note: This is a simplified implementation for test compatibility
            // In a real implementation, this would perform actual memory operations
            unsafe
            {
                var span = buffer.AsSpan();
                if (data.Length <= span.Length)
                {
                    data.AsSpan().CopyTo(span);
                }
                else
                {
                    throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
                }
            }
        }

        // Additional utility methods for compatibility

        /// <summary>
        /// Gets whether the buffer supports unified memory access.
        /// This property is commonly referenced in tests.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="buffer">The buffer to check.</param>
        /// <returns>True if the buffer supports unified memory, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static bool IsUnifiedMemory<T>(this IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            
            // Check if buffer implements unified memory patterns
            return buffer.IsOnHost && buffer.IsOnDevice;
        }

        /// <summary>
        /// Gets the device pointer if available for direct access patterns.
        /// This property is commonly used in low-level operations.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="buffer">The buffer to get device pointer from.</param>
        /// <returns>The device pointer, or IntPtr.Zero if not available.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static IntPtr GetDevicePointer<T>(this IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            
            try
            {
                var deviceMemory = buffer.GetDeviceMemory();
                return deviceMemory.Handle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Validates buffer state before operations.
        /// Common pattern used in test validation.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="buffer">The buffer to validate.</param>
        /// <param name="operationName">The name of the operation being performed.</param>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when buffer is disposed.</exception>
        public static void ValidateBuffer<T>(this IUnifiedMemoryBuffer<T> buffer, string operationName = "operation") where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentException.ThrowIfNullOrEmpty(operationName);

            if (buffer.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(buffer), $"Cannot perform {operationName} on disposed buffer");
            }
        }

        /// <summary>
        /// Ensures buffer is in the correct state for host operations.
        /// This method provides thread-safe synchronization commonly needed in tests.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="buffer">The buffer to prepare.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the preparation operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static async ValueTask EnsureReadyForHostOperationAsync<T>(this IUnifiedMemoryBuffer<T> buffer, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);

            buffer.ValidateBuffer("host operation preparation");

            if (!buffer.IsOnHost)
            {
                await buffer.EnsureOnHostAsync(cancellationToken: cancellationToken);
            }

            await buffer.SynchronizeAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Ensures buffer is in the correct state for device operations.
        /// This method provides thread-safe synchronization commonly needed in tests.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="buffer">The buffer to prepare.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the preparation operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static async ValueTask EnsureReadyForDeviceOperationAsync<T>(this IUnifiedMemoryBuffer<T> buffer, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);

            buffer.ValidateBuffer("device operation preparation");

            if (!buffer.IsOnDevice)
            {
                await buffer.EnsureOnDeviceAsync(cancellationToken: cancellationToken);
            }

            await buffer.SynchronizeAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Creates a safe view of the buffer that handles disposal automatically.
        /// This pattern is commonly used in test cleanup scenarios.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <returns>A memory view that is safe to use beyond buffer lifetime.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static Memory<T> ToSafeMemory<T>(this IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            buffer.ValidateBuffer("memory copy");

            // Create a copy of the buffer contents that survives buffer disposal
            var copy = new T[buffer.Length];
            var sourceMemory = buffer.AsMemory();
            sourceMemory.CopyTo(copy.AsMemory());
            return copy.AsMemory();
        }

        /// <summary>
        /// Gets performance metrics for the buffer if available.
        /// This method provides insights commonly needed in performance tests.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="buffer">The buffer to get metrics for.</param>
        /// <returns>Buffer metrics or null if not available.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static BufferMetrics? GetMetrics<T>(this IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);

            return new BufferMetrics
            {
                SizeInBytes = buffer.SizeInBytes,
                ElementCount = buffer.Length,
                IsOnHost = buffer.IsOnHost,
                IsOnDevice = buffer.IsOnDevice,
                IsDirty = buffer.IsDirty,
                IsDisposed = buffer.IsDisposed,
                State = buffer.State
            };
        }
    }

    /// <summary>
    /// Metrics information for memory buffers.
    /// Used by extension methods to provide performance insights.
    /// </summary>
    public sealed class BufferMetrics
    {
        /// <summary>Gets or sets the buffer size in bytes.</summary>
        public long SizeInBytes { get; init; }

        /// <summary>Gets or sets the element count.</summary>
        public int ElementCount { get; init; }

        /// <summary>Gets or sets whether the buffer is available on host.</summary>
        public bool IsOnHost { get; init; }

        /// <summary>Gets or sets whether the buffer is available on device.</summary>
        public bool IsOnDevice { get; init; }

        /// <summary>Gets or sets whether the buffer needs synchronization.</summary>
        public bool IsDirty { get; init; }

        /// <summary>Gets or sets whether the buffer is disposed.</summary>
        public bool IsDisposed { get; init; }

        /// <summary>Gets or sets the current buffer state.</summary>
        public BufferState State { get; init; }

        /// <summary>
        /// Returns a string representation of the buffer metrics.
        /// </summary>
        /// <returns>A formatted string with buffer information.</returns>
        public override string ToString()
        {
            return $"BufferMetrics {{ Size: {SizeInBytes} bytes, Elements: {ElementCount}, " +
                   $"Host: {IsOnHost}, Device: {IsOnDevice}, Dirty: {IsDirty}, " +
                   $"State: {State}, Disposed: {IsDisposed} }}";
        }
    }
}