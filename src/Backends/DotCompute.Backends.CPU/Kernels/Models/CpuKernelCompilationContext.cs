// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// Context for kernel compilation.
/// </summary>
internal sealed class CpuKernelCompilationContext
{
    public required KernelDefinition Definition { get; init; }
    public required CompilationOptions Options { get; init; }
    public required SimdSummary SimdCapabilities { get; init; }
    public required CpuThreadPool ThreadPool { get; init; }
    public required ILogger Logger { get; init; }
}