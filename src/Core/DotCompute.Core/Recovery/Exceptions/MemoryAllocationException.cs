// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Exceptions;

/// <summary>
/// The exception that is thrown when memory allocation fails after all recovery attempts have been exhausted.
/// </summary>
/// <remarks>
/// This exception is typically thrown by memory management systems when they are unable to
/// satisfy a memory allocation request even after performing various recovery strategies
/// such as garbage collection, defragmentation, and emergency cleanup operations.
/// It indicates that the system has reached a critical memory state where no additional
/// memory can be made available.
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var buffer = memoryManager.AllocateBuffer(requestedSize);
/// }
/// catch (MemoryAllocationException ex)
/// {
///     logger.LogError(ex, "Failed to allocate {Size} bytes after recovery attempts", requestedSize);
///     // Handle the allocation failure gracefully
/// }
/// </code>
/// </example>
public class MemoryAllocationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryAllocationException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <remarks>
    /// Use this constructor when you want to provide a specific error message
    /// that describes the context of the allocation failure, such as the
    /// amount of memory requested or the recovery strategies that were attempted.
    /// </remarks>
    /// <example>
    /// throw new MemoryAllocationException("Failed to allocate 1024MB after 3 recovery attempts");
    /// </example>
    public MemoryAllocationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryAllocationException"/> class
    /// with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or a null reference
    /// if no inner exception is specified.
    /// </param>
    /// <remarks>
    /// Use this constructor when the memory allocation failure was caused by another
    /// exception, such as an OutOfMemoryException from the underlying system or
    /// an exception from a memory pool implementation. The inner exception provides
    /// additional context about the root cause of the failure.
    /// </remarks>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     // Attempt low-level memory allocation
    ///     var memory = Marshal.AllocHGlobal(size);
    /// }
    /// catch (OutOfMemoryException ex)
    /// {
    ///     throw new MemoryAllocationException(
    ///         $"System memory allocation failed for {size} bytes after recovery", ex);
    /// }
    /// </code>
    /// </example>
    public MemoryAllocationException(string message, Exception innerException) : base(message, innerException) { }
    /// <summary>
    /// Initializes a new instance of the MemoryAllocationException class.
    /// </summary>
    public MemoryAllocationException()
    {
    }
}
