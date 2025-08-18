// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core;

/// <summary>
/// Context for kernel execution.
/// </summary>
public sealed class KernelExecutionContext
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the kernel arguments.
    /// </summary>
    public object[]? Arguments { get; init; }

    /// <summary>
    /// Gets or sets the global work size.
    /// </summary>
    public IReadOnlyList<long>? WorkDimensions { get; init; }

    /// <summary>
    /// Gets or sets the local work size.
    /// </summary>
    public IReadOnlyList<long>? LocalWorkSize { get; init; }

    /// <summary>
    /// Gets or sets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
