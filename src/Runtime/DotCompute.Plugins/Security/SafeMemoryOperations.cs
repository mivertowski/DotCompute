// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Provides safe memory operations with comprehensive bounds checking and security validation.
/// </summary>
public static class SafeMemoryOperations
{
    /// <summary>
    /// The max allocation size.
    /// </summary>
    /// <summary>
    /// Maximum allowed memory allocation size to prevent DoS attacks.
    /// </summary>
    public const int MaxAllocationSize = 1024 * 1024 * 1024; // 1GB

    /// <summary>
    /// Maximum allowed array length to prevent integer overflow attacks.
    /// </summary>
    public static readonly int MaxArrayLength = Array.MaxLength;

    /// <summary>
    /// Safely copies data between memory spans with comprehensive bounds checking.
    /// </summary>
    /// <typeparam name="T">The type of elements to copy.</typeparam>
    /// <param name="source">The source span.</param>
    /// <param name="destination">The destination span.</param>
    /// <param name="length">The number of elements to copy.</param>
    /// <returns>The number of elements actually copied.</returns>
    public static int SafeCopy<T>(ReadOnlySpan<T> source, Span<T> destination, int length) where T : unmanaged
    {
        // Input validation
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }

        if (length == 0)
        {
            return 0;
        }

        // Bounds checking
        var maxCopyLength = Math.Min(Math.Min(source.Length, destination.Length), length);


        if (maxCopyLength == 0)
        {
            return 0;
        }

        // Check for potential integer overflow
        var elementSize = Unsafe.SizeOf<T>();
        if (maxCopyLength > int.MaxValue / elementSize)
        {
            throw new OverflowException($"Copy operation would exceed maximum size: {maxCopyLength} * {elementSize}");
        }

        try
        {
            // Perform safe copy with bounds verification
            var sourceSlice = source[..maxCopyLength];
            var destinationSlice = destination[..maxCopyLength];


            sourceSlice.CopyTo(destinationSlice);


            return maxCopyLength;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Safe copy operation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Safely allocates memory with size validation and initialization.
    /// </summary>
    /// <typeparam name="T">The type of elements to allocate.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <param name="initialize">Whether to initialize the memory to default values.</param>
    /// <returns>A safe memory allocation wrapper.</returns>
    public static SafeMemoryAllocation<T> SafeAllocate<T>(int length, bool initialize = true) where T : unmanaged
    {
        // Input validation
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }

        if (length == 0)
        {
            return new SafeMemoryAllocation<T>([], 0);
        }

        // Size validation to prevent DoS
        var elementSize = Unsafe.SizeOf<T>();
        if (length > MaxAllocationSize / elementSize)
        {
            throw new OutOfMemoryException($"Requested allocation size too large: {length} * {elementSize} bytes");
        }

        // Prevent array length overflow
        if (length > MaxArrayLength)
        {
            throw new OutOfMemoryException($"Requested array length exceeds maximum: {length} > {MaxArrayLength}");
        }

        try
        {
            // Allocate memory safely
            var array = new T[length];

            // Initialize if requested

            if (initialize && length > 0)
            {
                array.AsSpan().Clear();
            }


            return new SafeMemoryAllocation<T>(array, length);
        }
        catch (OutOfMemoryException)
        {
            // Re-throw OutOfMemoryException as-is
            throw;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Safe allocation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Safely fills a memory span with a specified value with bounds checking.
    /// </summary>
    /// <typeparam name="T">The type of elements to fill.</typeparam>
    /// <param name="destination">The destination span to fill.</param>
    /// <param name="value">The value to fill with.</param>
    /// <param name="count">The number of elements to fill.</param>
    /// <returns>The number of elements actually filled.</returns>
    public static int SafeFill<T>(Span<T> destination, T value, int count) where T : unmanaged
    {
        // Input validation
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");
        }

        if (count == 0 || destination.IsEmpty)
        {
            return 0;
        }

        // Bounds checking
        var actualCount = Math.Min(destination.Length, count);


        try
        {
            // Perform safe fill operation
            destination[..actualCount].Fill(value);
            return actualCount;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Safe fill operation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Safely compares two memory spans with length validation.
    /// </summary>
    /// <typeparam name="T">The type of elements to compare.</typeparam>
    /// <param name="span1">The first span to compare.</param>
    /// <param name="span2">The second span to compare.</param>
    /// <param name="length">The maximum number of elements to compare.</param>
    /// <returns>True if the spans are equal up to the specified length, false otherwise.</returns>
    public static bool SafeEquals<T>(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2, int length) where T : IEquatable<T>
    {
        // Input validation
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }

        if (length == 0)
        {
            return true;
        }

        // Bounds checking
        var compareLength = Math.Min(Math.Min(span1.Length, span2.Length), length);


        try
        {
            // Perform safe comparison
            return span1[..compareLength].SequenceEqual(span2[..compareLength]);
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Safe comparison operation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Safely clears memory with comprehensive validation.
    /// </summary>
    /// <typeparam name="T">The type of elements to clear.</typeparam>
    /// <param name="span">The span to clear.</param>
    /// <param name="length">The number of elements to clear.</param>
    /// <returns>The number of elements actually cleared.</returns>
    public static int SafeClear<T>(Span<T> span, int length) where T : unmanaged
    {
        // Input validation
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }

        if (length == 0 || span.IsEmpty)
        {
            return 0;
        }

        // Bounds checking
        var clearLength = Math.Min(span.Length, length);


        try
        {
            // Perform secure clear operation
            span[..clearLength].Clear();

            // Additional security: overwrite with random data if dealing with sensitive types

            if (IsSensitiveType<T>())
            {
                SecureOverwrite(span[..clearLength]);
            }


            return clearLength;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Safe clear operation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that a buffer operation is safe to perform.
    /// </summary>
    /// <param name="bufferSize">The size of the buffer.</param>
    /// <param name="offset">The offset into the buffer.</param>
    /// <param name="length">The length of the operation.</param>
    /// <returns>True if the operation is safe, false otherwise.</returns>
    public static bool ValidateBufferOperation(int bufferSize, int offset, int length)
    {
        // Check for negative values
        if (bufferSize < 0 || offset < 0 || length < 0)
        {
            return false;
        }

        // Check for integer overflow
        try
        {
            var endPosition = checked(offset + length);
            return endPosition <= bufferSize;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Safely calculates the hash of a memory span with bounds checking.
    /// </summary>
    /// <typeparam name="T">The type of elements to hash.</typeparam>
    /// <param name="span">The span to hash.</param>
    /// <param name="maxLength">The maximum number of elements to hash.</param>
    /// <returns>A hash code for the span contents.</returns>
    public static int SafeGetHashCode<T>(ReadOnlySpan<T> span, int maxLength = int.MaxValue) where T : unmanaged
    {
        // Input validation
        if (maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length cannot be negative");
        }

        if (span.IsEmpty || maxLength == 0)
        {
            return 0;
        }

        // Bounds checking
        var hashLength = Math.Min(span.Length, maxLength);


        try
        {
            // Calculate hash safely
            var hashCode = new HashCode();
            var spanToHash = span[..hashLength];


            foreach (var item in spanToHash)
            {
                hashCode.Add(item);
            }


            return hashCode.ToHashCode();
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Safe hash calculation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Determines if a type contains sensitive data that requires secure clearing.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is considered sensitive.</returns>
    private static bool IsSensitiveType<T>()
    {
        var type = typeof(T);

        // Check for types that might contain sensitive data

        return type == typeof(byte) ||
               type == typeof(char) ||
               type.Name.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Securely overwrites memory with random data.
    /// </summary>
    /// <typeparam name="T">The type of elements to overwrite.</typeparam>
    /// <param name="span">The span to overwrite.</param>
    private static void SecureOverwrite<T>(Span<T> span) where T : unmanaged
    {
        if (span.IsEmpty)
        {
            return;
        }


        try
        {
            var bytes = MemoryMarshal.AsBytes(span);
            Random.Shared.NextBytes(bytes);
        }
        catch
        {
            // If secure overwrite fails, just clear normally
            span.Clear();
        }
    }
}

/// <summary>
/// Represents a safe memory allocation with automatic bounds checking and disposal.
/// </summary>
/// <typeparam name="T">The type of elements in the allocation.</typeparam>
public sealed class SafeMemoryAllocation<T> : IDisposable where T : unmanaged
{
    private T[] _array;
    private readonly int _length;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeMemoryAllocation{T}"/> class.
    /// </summary>
    /// <param name="array">The underlying array.</param>
    /// <param name="length">The logical length of the allocation.</param>
    internal SafeMemoryAllocation(T[] array, int length)
    {
        _array = array ?? throw new ArgumentNullException(nameof(array));
        _length = length;
    }

    /// <summary>
    /// Gets the length of the allocation.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets whether the allocation has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets a span representing the allocated memory with bounds checking.
    /// </summary>
    public Span<T> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _array.AsSpan(0, _length);
        }
    }

    /// <summary>
    /// Gets a read-only span representing the allocated memory with bounds checking.
    /// </summary>
    public ReadOnlySpan<T> ReadOnlySpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _array.AsSpan(0, _length);
        }
    }

    /// <summary>
    /// Safely accesses an element at the specified index with bounds checking.
    /// </summary>
    /// <param name="index">The index to access.</param>
    /// <returns>The element at the specified index.</returns>
    public ref T this[int index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);


            if (index < 0 || index >= _length)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range [0, {_length})");
            }


            return ref _array[index];
        }
    }

    /// <summary>
    /// Gets a safe slice of the allocation with bounds checking.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The length of the slice.</param>
    /// <returns>A span representing the slice.</returns>
    public Span<T> Slice(int start, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (start < 0 || start > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), $"Start {start} is out of range [0, {_length}]");
        }


        if (length < 0 || start + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"Length {length} extends beyond allocation bounds");
        }


        return _array.AsSpan(start, length);
    }

    /// <summary>
    /// Safely fills the allocation with the specified value.
    /// </summary>
    /// <param name="value">The value to fill with.</param>
    public void Fill(T value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Span.Fill(value);
    }

    /// <summary>
    /// Safely clears the allocation.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = SafeMemoryOperations.SafeClear(Span, _length);
    }

    /// <summary>
    /// Disposes the allocation and securely clears its contents.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Securely clear contents before disposal

        try
        {
            _ = SafeMemoryOperations.SafeClear(_array.AsSpan(0, _length), _length);
        }
        catch
        {
            // Ignore errors during disposal
        }


        _array = [];
        _disposed = true;
    }
}
