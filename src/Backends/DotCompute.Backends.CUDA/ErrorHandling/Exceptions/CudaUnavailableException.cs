// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.ErrorHandling.Exceptions;

/// <summary>
/// Exception thrown when CUDA is unavailable.
/// </summary>
public sealed class CudaUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CudaUnavailableException"/> class.
    /// </summary>
    public CudaUnavailableException()
        : base("CUDA is not available on this system")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public CudaUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CudaUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}