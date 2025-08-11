// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Compute;

/// <summary>
/// Context for kernel execution containing work dimensions and arguments.
/// </summary>
public class KernelExecutionContext
{
    /// <summary>
    /// Name of the kernel being executed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Arguments passed to the kernel.
    /// </summary>
    public object[] Arguments { get; set; } = Array.Empty<object>();

    /// <summary>
    /// Work dimensions (global work size).
    /// </summary>
    public long[]? WorkDimensions { get; set; }

    /// <summary>
    /// Local work size for work group optimization.
    /// </summary>
    public long[]? LocalWorkSize { get; set; }

    /// <summary>
    /// Cancellation token for the execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Additional metadata for kernel execution.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Performance hints for optimization.
    /// </summary>
    public Dictionary<string, object> PerformanceHints { get; set; } = new();
}