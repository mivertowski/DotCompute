// <copyright file="IGeneratedKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Analysis;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Common interface for all generated kernel types across the system.
/// </summary>
public interface IGeneratedKernel : IDisposable
{
    /// <summary>Gets the kernel name.</summary>
    string Name { get; }

    /// <summary>Gets the kernel source code.</summary>
    string SourceCode { get; }

    /// <summary>Gets the kernel language.</summary>
    string Language { get; }

    /// <summary>Gets the target backend.</summary>
    string TargetBackend { get; }

    /// <summary>Gets the entry point function name.</summary>
    string EntryPoint { get; }

    /// <summary>Gets kernel metadata.</summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Extended interface for generated kernels with execution capabilities.
/// </summary>
public interface IExecutableGeneratedKernel : IGeneratedKernel
{
    /// <summary>Gets the compiled kernel instance.</summary>
    ICompiledKernel? CompiledKernel { get; }

    /// <summary>Gets whether the kernel is compiled and ready for execution.</summary>
    bool IsCompiled { get; }

    /// <summary>Gets the kernel parameters.</summary>
    IReadOnlyList<IKernelParameter> Parameters { get; }

    /// <summary>Executes the kernel with the given parameters.</summary>
    /// <param name="parameters">The parameters to pass to the kernel.</param>
    /// <returns>Task representing the asynchronous execution.</returns>
    Task ExecuteAsync(params object[] parameters);
}

/// <summary>
/// Extended interface for generated kernels with analysis information.
/// </summary>
public interface IAnalyzableGeneratedKernel : IGeneratedKernel
{
    /// <summary>Gets the analysis result for this kernel.</summary>
    IExpressionAnalysisResult? Analysis { get; }

    /// <summary>Gets the optimizations applied to this kernel.</summary>
    IReadOnlyList<string> Optimizations { get; }

    /// <summary>Gets the complexity metrics for this kernel.</summary>
    IComplexityMetrics? ComplexityMetrics { get; }
}

/// <summary>
/// Complete interface combining all kernel capabilities.
/// </summary>
public interface IFullGeneratedKernel : IExecutableGeneratedKernel, IAnalyzableGeneratedKernel
{
    /// <summary>Gets the memory manager associated with this kernel.</summary>
    IGpuMemoryManager? MemoryManager { get; }

    /// <summary>Gets the compilation timestamp.</summary>
    DateTimeOffset CompiledAt { get; }

    /// <summary>Gets the kernel version.</summary>
    Version Version { get; }
}

/// <summary>
/// Marker interface for different analysis result types.
/// This allows different analysis result implementations to be used.
/// </summary>
public interface IExpressionAnalysisResult
{
    /// <summary>Gets the analysis timestamp.</summary>
    DateTimeOffset AnalysisTimestamp { get; }

    /// <summary>Gets the complexity metrics.</summary>
    IComplexityMetrics ComplexityMetrics { get; }
}

/// <summary>
/// Marker interface for compiled kernel types.
/// This allows different compiled kernel implementations to be used.
/// </summary>
public interface ICompiledKernel : IDisposable
{
    /// <summary>Gets the kernel name.</summary>
    string Name { get; }

    /// <summary>Gets whether the kernel is valid and executable.</summary>
    bool IsValid { get; }
}

/// <summary>
/// Marker interface for kernel parameters.
/// </summary>
public interface IKernelParameter
{
    /// <summary>Gets the parameter name.</summary>
    string Name { get; }

    /// <summary>Gets the parameter type.</summary>
    Type Type { get; }

    /// <summary>Gets whether the parameter is a pointer.</summary>
    bool IsPointer { get; }

    /// <summary>Gets whether the parameter is input-only.</summary>
    bool IsInput { get; }

    /// <summary>Gets whether the parameter is output.</summary>
    bool IsOutput { get; }
}

/// <summary>
/// Marker interface for GPU memory managers.
/// This allows different memory manager implementations to be used.
/// </summary>
public interface IGpuMemoryManager : IDisposable
{
    /// <summary>Gets the total managed memory in bytes.</summary>
    long TotalMemory { get; }

    /// <summary>Gets the available memory in bytes.</summary>
    long AvailableMemory { get; }
}