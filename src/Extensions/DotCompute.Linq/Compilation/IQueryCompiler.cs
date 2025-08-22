// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Compilation.Context;
using DotCompute.Linq.Compilation.Plans;
using DotCompute.Linq.Compilation.Validation;

namespace DotCompute.Linq.Compilation;


/// <summary>
/// Defines the interface for compiling LINQ expression trees into compute plans.
/// </summary>
public interface IQueryCompiler
{
    /// <summary>
    /// Compiles an expression tree into an executable compute plan.
    /// </summary>
    /// <param name="context">The compilation context containing the expression and accelerator.</param>
    /// <returns>A compute plan that can be executed on the accelerator.</returns>
    public IComputePlan Compile(CompilationContext context);

    /// <summary>
    /// Validates whether an expression can be compiled for GPU execution.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>A validation result indicating whether compilation is possible.</returns>
    public ValidationResult Validate(Expression expression);
}







