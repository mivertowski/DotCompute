// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using LinqCompilationOptions = DotCompute.Linq.Compilation.Options.CompilationOptions;

namespace DotCompute.Linq.Compilation.Context;

/// <summary>
/// Represents the context for compiling a LINQ expression.
/// </summary>
/// <remarks>
/// This class encapsulates all the information needed to compile a LINQ expression
/// into a compute plan, including the target accelerator, expression tree,
/// parameters, and compilation options.
/// </remarks>
public class CompilationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationContext"/> class.
    /// </summary>
    /// <param name="accelerator">The target accelerator for execution.</param>
    /// <param name="expression">The LINQ expression tree to compile.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accelerator"/> or <paramref name="expression"/> is null.
    /// </exception>
    public CompilationContext(IAccelerator accelerator, Expression expression)
    {
        Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Parameters = [];
        Options = new DotCompute.Abstractions.CompilationOptions();
    }

    /// <summary>
    /// Gets the target accelerator for compilation and execution.
    /// </summary>
    /// <value>
    /// The accelerator that will execute the compiled compute plan.
    /// </value>
    public IAccelerator Accelerator { get; }

    /// <summary>
    /// Gets the expression tree to compile.
    /// </summary>
    /// <value>
    /// The LINQ expression tree representing the query to be compiled.
    /// </value>
    public Expression Expression { get; }

    /// <summary>
    /// Gets the compilation parameters.
    /// </summary>
    /// <value>
    /// A dictionary containing parameters that will be passed to the compiled kernels.
    /// </value>
    /// <remarks>
    /// These parameters typically include constant values and external data
    /// referenced in the expression tree.
    /// </remarks>
    public Dictionary<string, object> Parameters { get; }

    /// <summary>
    /// Gets or sets the compilation options.
    /// </summary>
    /// <value>
    /// The options that control various aspects of the compilation process.
    /// </value>
    public DotCompute.Abstractions.CompilationOptions Options { get; set; }
}