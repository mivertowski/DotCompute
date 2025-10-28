// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.ErrorHandling.Exceptions;

/// <summary>
/// Exception indicating CPU fallback is required.
/// </summary>
public sealed class CpuFallbackRequiredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    /// <param name="reason">The reason.</param>
    public CpuFallbackRequiredException(string reason)
        : base($"CPU fallback required: {reason}")
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="innerException">The inner exception.</param>
    public CpuFallbackRequiredException(string reason, Exception innerException)
        : base($"CPU fallback required: {reason}", innerException)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the reason.
    /// </summary>
    public string Reason { get; }
}
