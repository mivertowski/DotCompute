// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// Context for kernel compilation.
/// </summary>
internal sealed class CpuKernelCompilationContext
{
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    /// <value>The definition.</value>
    public required KernelDefinition Definition { get; init; }
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public required CompilationOptions Options { get; init; }
    /// <summary>
    /// Gets or sets the simd capabilities.
    /// </summary>
    /// <value>The simd capabilities.</value>
    public required SimdSummary SimdCapabilities { get; init; }
    /// <summary>
    /// Gets or sets the thread pool.
    /// </summary>
    /// <value>The thread pool.</value>
    public required CpuThreadPool ThreadPool { get; init; }
    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    /// <value>The logger.</value>
    public required ILogger Logger { get; init; }
}
