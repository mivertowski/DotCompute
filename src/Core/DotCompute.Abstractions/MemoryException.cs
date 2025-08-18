// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Exception thrown when memory operations fail.
/// </summary>
public class MemoryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the MemoryException class.
    /// </summary>
    public MemoryException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the MemoryException class with a specified error message.
    /// </summary>
    public MemoryException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the MemoryException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public MemoryException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
