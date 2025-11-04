using System;
using System.Collections.ObjectModel;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Represents a directed acyclic graph of compute operations.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 3: Expression Analysis Foundation.
/// Used for query optimization and kernel fusion.
/// </remarks>
public class OperationGraph
{
    /// <summary>
    /// Gets the operations in the graph.
    /// </summary>
    public Collection<Operation> Operations { get; init; } = new();

    /// <summary>
    /// Gets metadata associated with the graph.
    /// </summary>
    public ReadOnlyDictionary<string, object> Metadata { get; init; } = new(new Dictionary<string, object>());

    /// <summary>
    /// Gets or sets the root operation of the graph.
    /// </summary>
    public Operation? Root { get; init; }

    /// <summary>
    /// Initializes a new instance of the OperationGraph class.
    /// </summary>
    public OperationGraph()
    {
    }

    /// <summary>
    /// Initializes a new instance of the OperationGraph class with operations.
    /// </summary>
    public OperationGraph(IEnumerable<Operation> operations)
    {
        Operations = new Collection<Operation>(operations.ToList());
    }
}

/// <summary>
/// Represents a single operation in an operation graph.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 3: Expression Analysis Foundation.
/// </remarks>
public class Operation
{
    /// <summary>
    /// Gets the unique identifier for this operation.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of operation.
    /// </summary>
    public OperationType Type { get; init; }

    /// <summary>
    /// Gets the IDs of operations this operation depends on.
    /// </summary>
    public Collection<string> Dependencies { get; init; } = new();

    /// <summary>
    /// Gets metadata associated with the operation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets the estimated cost of executing this operation.
    /// </summary>
    public double EstimatedCost { get; init; }
}
