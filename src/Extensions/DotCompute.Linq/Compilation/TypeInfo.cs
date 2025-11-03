using System;
using System.Collections.ObjectModel;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Represents type information extracted from LINQ expressions.
/// </summary>
/// <remarks>
/// Stores detailed type information including generic arguments,
/// element types, and capabilities (SIMD, unsafe, nullable).
/// </remarks>
public class TypeInfo
{
    /// <summary>
    /// Gets the CLR type.
    /// </summary>
    public Type Type { get; init; }

    /// <summary>
    /// Gets the element type for collection types (e.g., T in IEnumerable&lt;T&gt;).
    /// </summary>
    public Type? ElementType { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a nullable value type.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether this type can use SIMD operations.
    /// </summary>
    public bool IsSimdCapable { get; init; }

    /// <summary>
    /// Gets a value indicating whether this type requires unsafe code.
    /// </summary>
    public bool RequiresUnsafe { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a collection type.
    /// </summary>
    public bool IsCollection { get; init; }

    /// <summary>
    /// Gets the generic type arguments.
    /// </summary>
    public ReadOnlyCollection<Type> GenericArguments { get; init; } = new(Array.Empty<Type>());

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeInfo"/> class.
    /// </summary>
    /// <param name="type">The CLR type.</param>
    public TypeInfo(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }
}
