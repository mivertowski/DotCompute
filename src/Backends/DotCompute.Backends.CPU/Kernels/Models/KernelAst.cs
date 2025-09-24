// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// Abstract Syntax Tree representation of a kernel.
/// </summary>
internal sealed class KernelAst
{
    public List<AstNode> Operations { get; set; } = [];
    public List<AstNode> MemoryOperations { get; set; } = [];
    public List<string> Variables { get; set; } = [];
    public List<string> Parameters { get; set; } = [];
    public List<string> FunctionCalls { get; set; } = [];
    public bool HasConditionals { get; set; }
    public bool HasLoops { get; set; }
    public bool HasRecursion { get; set; }
    public bool HasIndirectMemoryAccess { get; set; }
    public bool HasComplexControlFlow { get; set; }
    public int ComplexityScore { get; set; }
    public long EstimatedInstructions { get; set; }
}