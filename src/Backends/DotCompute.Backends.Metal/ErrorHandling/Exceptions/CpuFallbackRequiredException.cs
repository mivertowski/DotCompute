// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.ErrorHandling.Exceptions;

/// <summary>
/// Exception indicating CPU fallback is required.
/// </summary>
public sealed class CpuFallbackRequiredException : Exception
{
    /// <summary>
    /// Gets the reason why CPU fallback is required.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    public CpuFallbackRequiredException()
        : base("CPU fallback required")
    {
        Reason = "CPU fallback required";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    /// <param name="reason">The reason why CPU fallback is required.</param>
    public CpuFallbackRequiredException(string reason)
        : base($"CPU fallback required: {reason}")
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackRequiredException"/> class.
    /// </summary>
    /// <param name="reason">The reason why CPU fallback is required.</param>
    /// <param name="innerException">The inner exception.</param>
    public CpuFallbackRequiredException(string reason, Exception innerException)
        : base($"CPU fallback required: {reason}", innerException)
    {
        Reason = reason;
    }
}
