// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Kernels.Enums;

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// AST node representing an operation.
/// </summary>
internal sealed class AstNode
{
    public AstNodeType NodeType { get; set; }
    public List<AstNode> Children { get; set; } = [];
    public object? Value { get; set; }
    public int Position { get; set; }
    public string? Text { get; set; }
}