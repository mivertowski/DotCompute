// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// Abstract Syntax Tree representation of a kernel.
/// </summary>
internal sealed class KernelAst
{
    /// <summary>
    /// Gets or sets the operations.
    /// </summary>
    /// <value>The operations.</value>
    public IList<AstNode> Operations { get; } = [];
    /// <summary>
    /// Gets or sets the memory operations.
    /// </summary>
    /// <value>The memory operations.</value>
    public IList<AstNode> MemoryOperations { get; } = [];
    /// <summary>
    /// Gets or sets the variables.
    /// </summary>
    /// <value>The variables.</value>
    public IList<string> Variables { get; } = [];
    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    /// <value>The parameters.</value>
    public IList<string> Parameters { get; } = [];
    /// <summary>
    /// Gets or sets the function calls.
    /// </summary>
    /// <value>The function calls.</value>
    public IList<string> FunctionCalls { get; } = [];
    /// <summary>
    /// Gets or sets a value indicating whether conditionals.
    /// </summary>
    /// <value>The has conditionals.</value>
    public bool HasConditionals { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether loops.
    /// </summary>
    /// <value>The has loops.</value>
    public bool HasLoops { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether recursion.
    /// </summary>
    /// <value>The has recursion.</value>
    public bool HasRecursion { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether indirect memory access.
    /// </summary>
    /// <value>The has indirect memory access.</value>
    public bool HasIndirectMemoryAccess { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether complex control flow.
    /// </summary>
    /// <value>The has complex control flow.</value>
    public bool HasComplexControlFlow { get; set; }
    /// <summary>
    /// Gets or sets the complexity score.
    /// </summary>
    /// <value>The complexity score.</value>
    public int ComplexityScore { get; set; }
    /// <summary>
    /// Gets or sets the estimated instructions.
    /// </summary>
    /// <value>The estimated instructions.</value>
    public long EstimatedInstructions { get; set; }
}