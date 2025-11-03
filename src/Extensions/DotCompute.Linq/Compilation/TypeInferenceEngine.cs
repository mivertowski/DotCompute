using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Infers types for LINQ operations and validates type compatibility.
/// </summary>
/// <remarks>
/// <para>
/// The TypeInferenceEngine analyzes expression trees to extract and validate type information
/// required for kernel generation. It handles:
/// </para>
/// <list type="bullet">
/// <item>Type extraction from expressions (literals, parameters, method calls)</item>
/// <item>Generic type resolution (IEnumerable&lt;T&gt;, Func&lt;T, TResult&gt;)</item>
/// <item>Collection element type inference</item>
/// <item>Type compatibility validation for operations</item>
/// <item>SIMD capability detection for numeric types</item>
/// <item>Unsafe code requirement detection (pointers)</item>
/// </list>
/// <para>
/// This is a Phase 3 component that provides the foundation for code generation.
/// </para>
/// </remarks>
public class TypeInferenceEngine
{
    private static readonly Type[] SimdCapableTypes =
    {
        typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double)
    };

    /// <summary>
    /// Infers the type of an expression.
    /// </summary>
    /// <param name="expr">The expression to analyze.</param>
    /// <returns>Detailed type information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expr"/> is null.</exception>
    public TypeInfo InferType(Expression expr)
    {
        ArgumentNullException.ThrowIfNull(expr);

        var type = expr.Type;
        var elementType = InferElementType(type);
        var isCollection = elementType != null;
        var underlyingType = Nullable.GetUnderlyingType(type);
        var isNullable = underlyingType != null;
        var actualType = underlyingType ?? type;

        var genericArgs = type.IsGenericType
            ? new ReadOnlyCollection<Type>(type.GetGenericArguments())
            : new ReadOnlyCollection<Type>(Array.Empty<Type>());

        return new TypeInfo(type)
        {
            ElementType = elementType,
            IsNullable = isNullable,
            IsSimdCapable = IsSimdCapable(actualType),
            RequiresUnsafe = IsPointerType(actualType),
            IsCollection = isCollection,
            GenericArguments = genericArgs
        };
    }

    /// <summary>
    /// Validates that all types in an operation graph are compatible.
    /// </summary>
    /// <param name="graph">The operation graph to validate.</param>
    /// <param name="errors">Collection of validation error messages.</param>
    /// <returns>True if all types are compatible; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> is null.</exception>
    public bool ValidateTypes(OperationGraph graph, out Collection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(graph);

        errors = new Collection<string>();

        if (graph.Operations.Count == 0)
        {
            return true;
        }

        foreach (var operation in graph.Operations)
        {
            ValidateOperation(operation, graph, errors);
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Generates type metadata for code generation.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <param name="inputType">The input type of the query.</param>
    /// <param name="resultType">The result type of the query.</param>
    /// <returns>Aggregated type metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are null.</exception>
    public TypeMetadata InferTypes(OperationGraph graph, Type inputType, Type resultType)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(inputType);
        ArgumentNullException.ThrowIfNull(resultType);

        var intermediateTypes = new Dictionary<string, Type>();
        var requiresUnsafe = IsPointerType(inputType) || IsPointerType(resultType);
        var isSimdCompatible = true;
        var hasNullableTypes = Nullable.GetUnderlyingType(inputType) != null ||
                               Nullable.GetUnderlyingType(resultType) != null;

        // Analyze each operation to extract intermediate types
        foreach (var operation in graph.Operations)
        {
            if (operation.Metadata.TryGetValue("InputType", out var inputTypeObj) && inputTypeObj is Type inType)
            {
                intermediateTypes[operation.Id + "_input"] = inType;
                requiresUnsafe |= IsPointerType(inType);
                isSimdCompatible &= IsSimdCapable(inType) || !IsNumericType(inType);
                hasNullableTypes |= Nullable.GetUnderlyingType(inType) != null;
            }

            if (operation.Metadata.TryGetValue("OutputType", out var outputTypeObj) && outputTypeObj is Type outType)
            {
                intermediateTypes[operation.Id + "_output"] = outType;
                requiresUnsafe |= IsPointerType(outType);
                isSimdCompatible &= IsSimdCapable(outType) || !IsNumericType(outType);
                hasNullableTypes |= Nullable.GetUnderlyingType(outType) != null;
            }
        }

        return new TypeMetadata
        {
            InputType = inputType,
            ResultType = resultType,
            IntermediateTypes = intermediateTypes,
            RequiresUnsafe = requiresUnsafe,
            IsSimdCompatible = isSimdCompatible,
            HasNullableTypes = hasNullableTypes
        };
    }

    /// <summary>
    /// Resolves generic type parameters in a lambda expression.
    /// </summary>
    /// <param name="lambda">The lambda expression to analyze.</param>
    /// <returns>Type information for the lambda's return type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lambda"/> is null.</exception>
    public TypeInfo ResolveLambdaTypes(LambdaExpression lambda)
    {
        ArgumentNullException.ThrowIfNull(lambda);

        return InferType(lambda.Body);
    }

    /// <summary>
    /// Infers the element type of a collection type.
    /// </summary>
    /// <param name="collectionType">The collection type to analyze.</param>
    /// <returns>The element type if it's a collection; otherwise, null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collectionType"/> is null.</exception>
    public Type? InferElementType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type collectionType)
    {
        ArgumentNullException.ThrowIfNull(collectionType);

        // Check for array
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        // Check for IEnumerable<T>
        var enumerableInterface = collectionType
            .GetInterfaces()
            .Concat(new[] { collectionType })
            .FirstOrDefault(t => t.IsGenericType &&
                                 t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>));

        if (enumerableInterface != null)
        {
            return enumerableInterface.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>
    /// Checks if two types are compatible for operations.
    /// </summary>
    /// <param name="left">The left operand type.</param>
    /// <param name="right">The right operand type.</param>
    /// <returns>True if types are compatible; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either parameter is null.</exception>
    public bool AreCompatible(TypeInfo left, TypeInfo right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        // Exact match
        if (left.Type == right.Type)
        {
            return true;
        }

        var leftType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
        var rightType = Nullable.GetUnderlyingType(right.Type) ?? right.Type;

        // Nullable<T> is compatible with T
        if (leftType == rightType)
        {
            return true;
        }

        // Check numeric compatibility (no precision loss)
        if (IsNumericType(leftType) && IsNumericType(rightType))
        {
            return CanConvertWithoutLoss(rightType, leftType);
        }

        // Check reference type compatibility (inheritance/interfaces)
        if (!leftType.IsValueType && !rightType.IsValueType)
        {
            return leftType.IsAssignableFrom(rightType) || rightType.IsAssignableFrom(leftType);
        }

        // Check for implicit conversion operator
        return HasImplicitConversion(rightType, leftType);
    }

    private void ValidateOperation(Operation operation, OperationGraph graph, Collection<string> errors)
    {
        if (!operation.Metadata.TryGetValue("InputType", out var inputTypeObj) || inputTypeObj is not Type inputType)
        {
            return;
        }

        if (!operation.Metadata.TryGetValue("OutputType", out var outputTypeObj) || outputTypeObj is not Type outputType)
        {
            return;
        }

        var inputTypeInfo = new TypeInfo(inputType) { IsSimdCapable = IsSimdCapable(inputType) };
        var outputTypeInfo = new TypeInfo(outputType) { IsSimdCapable = IsSimdCapable(outputType) };

        switch (operation.Type)
        {
            case OperationType.Filter:
                ValidateFilterOperation(inputTypeInfo, outputTypeInfo, errors);
                break;

            case OperationType.Map:
                ValidateMapOperation(inputTypeInfo, outputTypeInfo, errors);
                break;

            case OperationType.Reduce:
            case OperationType.Aggregate:
                ValidateAggregateOperation(inputTypeInfo, outputTypeInfo, errors);
                break;
        }
    }

    private void ValidateFilterOperation(TypeInfo inputType, TypeInfo outputType, Collection<string> errors)
    {
        if (outputType.Type != typeof(bool))
        {
            errors.Add($"Incompatible types in Where predicate: Cannot convert '{inputType.Type.Name}' to 'bool'");
        }
    }

    private void ValidateMapOperation(TypeInfo inputType, TypeInfo outputType, Collection<string> errors)
    {
        // Map operations can transform to any type, but we check for common mistakes
        if (inputType.RequiresUnsafe && !outputType.RequiresUnsafe)
        {
            errors.Add($"Select operation converts unsafe type '{inputType.Type.Name}' to safe type. Ensure proper handling.");
        }
    }

    private void ValidateAggregateOperation(TypeInfo inputType, TypeInfo outputType, Collection<string> errors)
    {
        if (!AreCompatible(inputType, outputType))
        {
            errors.Add($"Aggregate operation requires compatible accumulator type, got '{outputType.Type.Name}' for '{inputType.Type.Name}' input");
        }
    }

    private static bool IsSimdCapable(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return SimdCapableTypes.Contains(actualType);
    }

    private static bool IsPointerType(Type type)
    {
        return type.IsPointer || type.IsByRef;
    }

    private static bool IsNumericType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType.IsPrimitive && actualType != typeof(bool) && actualType != typeof(char);
    }

    private static bool CanConvertWithoutLoss(Type from, Type to)
    {
        // Define safe numeric conversions (no precision/range loss)
        var safeConversions = new Dictionary<Type, Type[]>
        {
            [typeof(byte)] = new[] { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(sbyte)] = new[] { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(short)] = new[] { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(ushort)] = new[] { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(int)] = new[] { typeof(long), typeof(double), typeof(decimal) },
            [typeof(uint)] = new[] { typeof(long), typeof(ulong), typeof(double), typeof(decimal) },
            [typeof(long)] = new[] { typeof(decimal) },
            [typeof(ulong)] = new[] { typeof(decimal) },
            [typeof(float)] = new[] { typeof(double) }
        };

        return from == to || (safeConversions.TryGetValue(from, out var targets) && targets.Contains(to));
    }

    private static bool HasImplicitConversion([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type from, Type to)
    {
        try
        {
            var method = from.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                    m.Name == "op_Implicit" &&
                    m.ReturnType == to &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == from);

            return method != null;
        }
        catch
        {
            return false;
        }
    }
}
