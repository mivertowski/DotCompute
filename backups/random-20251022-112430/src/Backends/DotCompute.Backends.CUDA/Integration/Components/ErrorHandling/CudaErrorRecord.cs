// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// CUDA error record for tracking and analysis.
/// </summary>
public readonly record struct CudaErrorRecord
{
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>The error.</value>
    public CudaError Error { get; init; }
    /// <summary>
    /// Gets or sets the operation.
    /// </summary>
    /// <value>The operation.</value>
    public string Operation { get; init; }
    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    /// <value>The context.</value>
    public string Context { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    /// <value>The thread id.</value>
    public int ThreadId { get; init; }
    /// <summary>
    /// Gets or sets the stack trace.
    /// </summary>
    /// <value>The stack trace.</value>
    public string? StackTrace { get; init; }
}