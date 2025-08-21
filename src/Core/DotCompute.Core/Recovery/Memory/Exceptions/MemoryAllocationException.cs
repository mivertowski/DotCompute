// <copyright file="MemoryAllocationException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Exceptions;

/// <summary>
/// Exception thrown when memory allocation fails after recovery attempts.
/// Indicates that the system could not allocate the requested memory even after recovery operations.
/// </summary>
public class MemoryAllocationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the MemoryAllocationException class.
    /// </summary>
    public MemoryAllocationException()
        : base("Memory allocation failed after recovery attempts.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the MemoryAllocationException class with a message.
    /// </summary>
    /// <param name="message">The error message describing the allocation failure.</param>
    public MemoryAllocationException(string message) 
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the MemoryAllocationException class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message describing the allocation failure.</param>
    /// <param name="innerException">The exception that caused this allocation failure.</param>
    public MemoryAllocationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}