// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// CUDA error record for tracking and analysis.
/// </summary>
public readonly record struct CudaErrorRecord
{
    public CudaError Error { get; init; }
    public string Operation { get; init; }
    public string Context { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public int ThreadId { get; init; }
    public string? StackTrace { get; init; }
}