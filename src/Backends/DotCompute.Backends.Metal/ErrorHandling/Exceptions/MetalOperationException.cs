// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.ErrorHandling.Exceptions;

/// <summary>
/// Exception for Metal operation failures.
/// </summary>
public sealed class MetalOperationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetalOperationException"/> class.
    /// </summary>
    public MetalOperationException()
        : base("Metal operation failed")
    {
        Operation = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalOperationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public MetalOperationException(string message)
        : base(message)
    {
        Operation = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalOperationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MetalOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Operation = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalOperationException"/> class.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="message">The message.</param>
    public MetalOperationException(string operation, string message)
        : base($"Metal operation '{operation}' failed: {message}")
    {
        Operation = operation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalOperationException"/> class.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MetalOperationException(string operation, string message, Exception innerException)
        : base($"Metal operation '{operation}' failed: {message}", innerException)
    {
        Operation = operation;
    }

    /// <summary>
    /// Gets the operation.
    /// </summary>
    public string Operation { get; }
}
