// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Kernels.Enums;

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// AST node representing an operation.
/// </summary>
internal sealed class AstNode
{
    /// <summary>
    /// Gets or sets the node type.
    /// </summary>
    /// <value>The node type.</value>
    public AstNodeType NodeType { get; set; }
    /// <summary>
    /// Gets or sets the children.
    /// </summary>
    /// <value>The children.</value>
    public IList<AstNode> Children { get; } = [];
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    /// <value>The value.</value>
    public object? Value { get; set; }
    /// <summary>
    /// Gets or sets the position.
    /// </summary>
    /// <value>The position.</value>
    public int Position { get; set; }
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    /// <value>The text.</value>
    public string? Text { get; set; }
}
