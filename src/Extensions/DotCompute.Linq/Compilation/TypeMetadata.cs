using System;
using System.Collections.Generic;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Metadata about types used in a query for code generation.
/// </summary>
/// <remarks>
/// Aggregates all type information needed to generate efficient
/// kernel code for different backends (CPU, CUDA, etc.).
/// </remarks>
public class TypeMetadata
{
    /// <summary>
    /// Gets the input type of the query (source collection element type).
    /// </summary>
    public required Type InputType { get; init; }

    /// <summary>
    /// Gets the output/result type of the query.
    /// </summary>
    public required Type ResultType { get; init; }

    /// <summary>
    /// Gets the intermediate types used in operations, keyed by operation ID.
    /// </summary>
    public required Dictionary<string, Type> IntermediateTypes { get; init; }

    /// <summary>
    /// Gets a value indicating whether any type requires unsafe code.
    /// </summary>
    public bool RequiresUnsafe { get; init; }

    /// <summary>
    /// Gets a value indicating whether all numeric types support SIMD.
    /// </summary>
    public bool IsSimdCompatible { get; init; }

    /// <summary>
    /// Gets a value indicating whether any type is nullable.
    /// </summary>
    public bool HasNullableTypes { get; init; }
}
