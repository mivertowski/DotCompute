// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using OperationType = DotCompute.Linq.Optimization.OperationType;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Represents a single operation node in the operation graph.
/// </summary>
/// <remarks>
/// Each node represents a discrete LINQ operation (Select, Where, etc.) with its
/// associated type information, dependencies, and metadata for code generation.
/// </remarks>
public sealed class OperationNode
{
    /// <summary>
    /// Gets the unique identifier for this operation node.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the type of LINQ operation this node represents.
    /// </summary>
    public required OperationType Type { get; init; }

    /// <summary>
    /// Gets the expression tree representing this operation.
    /// </summary>
    public required Expression Expression { get; init; }

    /// <summary>
    /// Gets the type information for the input to this operation.
    /// </summary>
    public required TypeInfo InputType { get; init; }

    /// <summary>
    /// Gets the type information for the output of this operation.
    /// </summary>
    public required TypeInfo OutputType { get; init; }

    /// <summary>
    /// Gets the identifiers of nodes this operation depends on.
    /// </summary>
    /// <remarks>
    /// Dependencies establish execution order and data flow in the operation graph.
    /// </remarks>
    public required IReadOnlyList<string> Dependencies { get; init; }

    /// <summary>
    /// Gets additional metadata associated with this operation.
    /// </summary>
    /// <remarks>
    /// Metadata can include optimization hints, backend-specific information,
    /// or custom annotations for code generation.
    /// </remarks>
    public required IReadOnlyDictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Gets the lambda expression for operations that use predicates or selectors.
    /// </summary>
    /// <value>
    /// The lambda expression for operations like Select, Where, OrderBy;
    /// otherwise, null for operations without lambda expressions.
    /// </value>
    public LambdaExpression? Lambda { get; init; }

    /// <summary>
    /// Gets the constant value for constant expressions.
    /// </summary>
    /// <value>
    /// The constant value if this node represents a constant expression;
    /// otherwise, null.
    /// </value>
    public object? ConstantValue { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationNode"/> class.
    /// </summary>
    public OperationNode()
    {
        Dependencies = new Collection<string>();
        Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
    }

    /// <summary>
    /// Determines whether this operation can be fused with another operation.
    /// </summary>
    /// <param name="other">The other operation node to check fusion compatibility with.</param>
    /// <returns>
    /// <c>true</c> if the operations can be fused into a single kernel; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <remarks>
    /// Operation fusion is an optimization technique that combines multiple operations
    /// into a single kernel to reduce memory traffic and improve performance.
    /// </remarks>
    public bool CanFuseWith(OperationNode other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Note: Type compatibility check removed - can be added later with TypeInferenceEngine.AreCompatible()
        // For now, rely on operation type patterns for fusion detection

        // Check if operations are fusible types
        return (Type, other.Type) switch
        {
            (OperationType.Map, OperationType.Map) => true,        // Select + Select
            (OperationType.Map, OperationType.Filter) => true,     // Select + Where
            (OperationType.Filter, OperationType.Map) => true,     // Where + Select
            (OperationType.Filter, OperationType.Filter) => true,  // Where + Where
            _ => false
        };
    }

    /// <summary>
    /// Gets a value indicating whether this operation requires GPU acceleration.
    /// </summary>
    /// <returns>
    /// <c>true</c> if GPU acceleration is beneficial or required; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This is a heuristic based on operation type and metadata.
    /// Operations on large datasets typically benefit from GPU acceleration.
    /// </remarks>
    public bool RequiresGpu()
    {
        // Check for explicit GPU hint in metadata
        if (Metadata.TryGetValue("PreferGpu", out var preferGpu) && preferGpu is bool useGpu)
        {
            return useGpu;
        }

        // Check for data size hints
        if (Metadata.TryGetValue("DataSize", out var dataSize) && dataSize is long size)
        {
            // Heuristic: GPU beneficial for datasets > 10K elements
            return size > 10_000;
        }

        // Certain operations benefit more from GPU
        return Type switch
        {
            OperationType.Aggregate => true,
            OperationType.Reduce => true,
            OperationType.Map when IsComplexTransform() => true,  // Map = Select
            _ => false
        };
    }

    private bool IsComplexTransform()
    {
        // Check if the lambda involves complex mathematical operations
        if (Lambda is null)
        {
            return false;
        }

        // Simple heuristic: check for method calls (Math.*, etc.)
        return Metadata.TryGetValue("ComplexTransform", out var complex) &&
               complex is bool isComplex && isComplex;
    }
}
