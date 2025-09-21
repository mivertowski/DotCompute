// <copyright file="KernelCompilationResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using DotCompute.Linq.Operators.Execution;
namespace DotCompute.Linq.Operators.Models;
/// <summary>
/// Represents the result of a kernel compilation operation.
/// </summary>
public class KernelCompilationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the compilation was successful.
    /// </summary>
    public bool Success { get; set; }
    /// Gets or sets the compiled kernel if compilation was successful.
    public ICompiledKernel? CompiledKernel { get; set; }
    /// Gets or sets the error message if compilation failed.
    public string? ErrorMessage { get; set; }
    /// Gets or sets the time taken for compilation.
    public TimeSpan CompilationTime { get; set; }
}
