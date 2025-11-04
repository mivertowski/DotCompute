using DotCompute.Abstractions;
using DotCompute.Linq.CodeGeneration;
// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Interface for kernel code generators that compile operation graphs to executable code.
/// </summary>
public interface IKernelGenerator
{
    /// <summary>
    /// Generates a compiled kernel from an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for code generation.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>A compiled delegate that executes the kernel.</returns>
    Delegate Generate(OperationGraph graph, TypeMetadata metadata, CompilationOptions options);
}
