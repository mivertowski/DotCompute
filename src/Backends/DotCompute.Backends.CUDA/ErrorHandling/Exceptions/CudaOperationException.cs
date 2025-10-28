// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.ErrorHandling.Exceptions;

/// <summary>
/// Exception for CUDA operation failures.
/// </summary>
public sealed class CudaOperationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CudaOperationException"/> class.
    /// </summary>
    public CudaOperationException()
    {
        Operation = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaOperationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public CudaOperationException(string message) : base(message)
    {
        Operation = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaOperationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CudaOperationException(string message, Exception innerException) : base(message, innerException)
    {
        Operation = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaOperationException"/> class.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="message">The message.</param>
    public CudaOperationException(string operation, string message)
        : base($"CUDA operation '{operation}' failed: {message}")
    {
        Operation = operation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaOperationException"/> class.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CudaOperationException(string operation, string message, Exception innerException)
        : base($"CUDA operation '{operation}' failed: {message}", innerException)
    {
        Operation = operation;
    }

    /// <summary>
    /// Gets the operation.
    /// </summary>
    /// <value>
    /// The operation.
    /// </value>
    public string Operation { get; }
}