// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Execution;

namespace DotCompute.Backends.CPU.Extensions;

/// <summary>
/// Extension methods for KernelExecutionContext to support CPU backend operations.
/// DEPRECATED: These methods now exist on the context itself. Extensions removed to avoid conflicts.
/// </summary>
[Obsolete("Extension methods are deprecated. Use methods directly on KernelExecutionContext instead.")]
public static class KernelExecutionContextExtensions
{
    // NOTE: All extension methods removed to avoid recursive call conflicts.
    // The KernelExecutionContext class now has these methods built-in:
    // - SetParameter(int index, object value)
    // - GetBuffer(int index)
    // - GetScalar<T>(int index)
}