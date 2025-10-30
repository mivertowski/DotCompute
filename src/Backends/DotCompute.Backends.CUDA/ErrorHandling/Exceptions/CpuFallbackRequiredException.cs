// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.ErrorHandling.Exceptions;

/// <summary>
/// Exception indicating CPU fallback is required.
/// </summary>
public sealed class CpuFallbackRequiredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    public CpuFallbackRequiredException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public CpuFallbackRequiredException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CpuFallbackRequiredException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the reason for fallback.
    /// </summary>
    /// <value>
    /// The reason.
    /// </value>
    public string? Reason { get; init; }
}
