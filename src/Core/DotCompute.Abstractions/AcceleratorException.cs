// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions
{

/// <summary>
/// Exception thrown when accelerator operations fail.
/// </summary>
public class AcceleratorException : Exception
{
    /// <summary>
    /// Initializes a new instance of the AcceleratorException class.
    /// </summary>
    public AcceleratorException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the AcceleratorException class with a specified error message.
    /// </summary>
    public AcceleratorException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the AcceleratorException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public AcceleratorException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
}
