// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
namespace DotCompute.Linq.Expressions;
/// <summary>
/// Engine for inferring and validating types in LINQ expression trees for GPU compilation.
/// </summary>
public sealed class TypeInferenceEngine : ITypeInferenceEngine
{
    private readonly ILogger<TypeInferenceEngine> _logger;
    private readonly Dictionary<Type, TypeCapabilities> _typeCapabilities;
    private readonly HashSet<Type> _supportedTypes;
    /// <summary>
    /// Initializes a new instance of the <see cref="TypeInferenceEngine"/> class.
    /// </summary>
    public TypeInferenceEngine(ILogger<TypeInferenceEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _typeCapabilities = InitializeTypeCapabilities();
        _supportedTypes = InitializeSupportedTypes();
    }
    /// Infers types throughout an expression tree and validates GPU compatibility.
    public TypeInferenceResult InferTypes(Expression expression)
        ArgumentNullException.ThrowIfNull(expression);
        _logger.LogDebugMessage($"Starting type inference for expression: {expression.NodeType}");
        var visitor = new TypeInferenceVisitor(_supportedTypes, _typeCapabilities, _logger);
        var result = visitor.AnalyzeExpression(expression);
        _logger.LogDebugMessage($"Type inference completed. Found {result.InferredTypes.Count} unique types, {result.ValidationErrors.Count} validation errors");
        return result;
    /// Validates that all types in an expression are GPU-compatible.
    public TypeValidationResult ValidateTypes(Expression expression)
        var inferenceResult = InferTypes(expression);
        var errors = new List<TypeValidationError>();
        // Check each inferred type for GPU compatibility
        foreach (var typeInfo in inferenceResult.InferredTypes.Values)
        {
            if (!IsGpuCompatible(typeInfo.Type))
            {
                errors.Add(new TypeValidationError(
                    typeInfo.Type,
                    $"Type {typeInfo.Type.Name} is not GPU-compatible",
                    typeInfo.UsageLocations.First(),
                    TypeValidationSeverity.Error));
            }
            // Check for potential performance issues
            if (HasPerformanceIssues(typeInfo.Type))
                    $"Type {typeInfo.Type.Name} may have performance issues on GPU",
                    TypeValidationSeverity.Warning));
        }
        // Add existing validation errors
        errors.AddRange(inferenceResult.ValidationErrors);
        return new TypeValidationResult
            IsValid = errors.All(e => e.Severity != TypeValidationSeverity.Error),
            Errors = errors,
            InferredTypes = inferenceResult.InferredTypes,
            TypeConversions = inferenceResult.TypeConversions
        };
    /// Suggests type optimizations for better GPU performance.
    public IEnumerable<TypeOptimizationSuggestion> SuggestOptimizations(Expression expression)
        var suggestions = new List<TypeOptimizationSuggestion>();
            // Suggest vectorization opportunities
            if (CanVectorize(typeInfo.Type))
                suggestions.Add(new TypeOptimizationSuggestion(
                    TypeOptimizationType.Vectorization,
                    $"Consider using vector types for {typeInfo.Type.Name} to improve performance",
                    GetVectorType(typeInfo.Type),
                    PerformanceImpact.High));
            // Suggest precision reductions
            if (CanReducePrecision(typeInfo.Type))
                    TypeOptimizationType.PrecisionReduction,
                    $"Consider using {GetReducedPrecisionType(typeInfo.Type).Name} instead of {typeInfo.Type.Name}",
                    GetReducedPrecisionType(typeInfo.Type),
                    PerformanceImpact.Medium));
            // Suggest memory layout optimizations
            if (RequiresMemoryLayoutOptimization(typeInfo.Type))
                    TypeOptimizationType.MemoryLayout,
                    $"Consider restructuring {typeInfo.Type.Name} for better GPU memory access",
                    null,
        return suggestions;
    /// Determines the optimal GPU type for a given .NET type.
    public Type GetOptimalGpuType(Type netType)
        ArgumentNullException.ThrowIfNull(netType);
        // Direct mappings for primitive types
        if (_typeCapabilities.TryGetValue(netType, out var capabilities))
            return capabilities.OptimalGpuType ?? netType;
        // Handle array types
        if (netType.IsArray)
            var elementType = netType.GetElementType()!;
            var optimalElementType = GetOptimalGpuType(elementType);
            // AOT-compatible array type creation
            var arrayType = typeof(Array).Assembly.GetType(optimalElementType.FullName + "[]");
            return arrayType ?? typeof(Array);
        // Handle generic types
        if (netType.IsGenericType)
            var genericDefinition = netType.GetGenericTypeDefinition();
            var typeArguments = netType.GetGenericArguments();
            var optimalArguments = typeArguments.Select(GetOptimalGpuType).ToArray();
            return genericDefinition.MakeGenericType(optimalArguments);
        // Default to original type
        return netType;
    private static Dictionary<Type, TypeCapabilities> InitializeTypeCapabilities()
        return new Dictionary<Type, TypeCapabilities>
            // Primitive types with full GPU support
            [typeof(bool)] = new TypeCapabilities
                IsGpuCompatible = true,
                OptimalGpuType = typeof(bool),
                CanVectorize = true,
                VectorType = typeof(bool), // GPU bool vector would be defined elsewhere
                MemorySize = 1,
                AlignmentRequirement = 1
            },
            [typeof(byte)] = new TypeCapabilities
                OptimalGpuType = typeof(byte),
                VectorType = typeof(byte), // GPU byte vector
            [typeof(short)] = new TypeCapabilities
                OptimalGpuType = typeof(short),
                VectorType = typeof(short), // GPU short vector
                MemorySize = 2,
                AlignmentRequirement = 2
            [typeof(int)] = new TypeCapabilities
                OptimalGpuType = typeof(int),
                VectorType = typeof(int), // GPU int vector
                MemorySize = 4,
                AlignmentRequirement = 4
            [typeof(long)] = new TypeCapabilities
                OptimalGpuType = typeof(long),
                CanVectorize = false, // Long operations may be slower on some GPUs
                VectorType = null,
                MemorySize = 8,
                AlignmentRequirement = 8
            [typeof(float)] = new TypeCapabilities
                OptimalGpuType = typeof(float),
                VectorType = typeof(float), // GPU float vector
            [typeof(double)] = new TypeCapabilities
                OptimalGpuType = typeof(double),
                VectorType = typeof(double), // GPU double vector
                AlignmentRequirement = 8,
                PerformanceNotes = "Double precision may be slower on consumer GPUs"
            [typeof(decimal)] = new TypeCapabilities
                IsGpuCompatible = false,
                OptimalGpuType = typeof(double), // Suggest double as alternative
                CanVectorize = false,
                MemorySize = 16,
                AlignmentRequirement = 16,
                PerformanceNotes = "Decimal is not natively supported on GPU, consider using double"
    private static HashSet<Type> InitializeSupportedTypes()
        return
    [
        // Primitive types
        typeof(bool), typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double),
        
        // Array types are handled dynamically
        // Struct types need individual validation
    ];
    private bool IsGpuCompatible(Type type)
        // Check direct compatibility
        if (_typeCapabilities.TryGetValue(type, out var capabilities))
            return capabilities.IsGpuCompatible;
        // Check array types
        if (type.IsArray)
            var elementType = type.GetElementType()!;
            return IsGpuCompatible(elementType);
        // Check value types (structs)
        if (type.IsValueType && !type.IsEnum)
            // All fields must be GPU-compatible
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return fields.All(field => IsGpuCompatible(field.FieldType));
        // Reference types are generally not GPU-compatible for kernels
        return false;
    private bool HasPerformanceIssues(Type type)
            return !string.IsNullOrEmpty(capabilities.PerformanceNotes);
        // Large structs can have performance issues
        if (type.IsValueType)
            var size = CalculateTypeSize(type);
            return size > 64; // Structs larger than 64 bytes may cause performance issues
    private bool CanVectorize(Type type) => _typeCapabilities.TryGetValue(type, out var capabilities) && capabilities.CanVectorize;
    private Type? GetVectorType(Type type) => _typeCapabilities.TryGetValue(type, out var capabilities) ? capabilities.VectorType : null;
    private static bool CanReducePrecision(Type type) => type == typeof(double) || type == typeof(decimal);
    private static Type GetReducedPrecisionType(Type type)
        return type switch
            _ when type == typeof(double) => typeof(float),
            _ when type == typeof(decimal) => typeof(double),
            _ => type
    private static bool RequiresMemoryLayoutOptimization(Type type)
        if (!type.IsValueType || type.IsPrimitive)
            return false;
        // Check if struct has poor alignment
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return fields.Any(f => !IsWellAligned(f.FieldType));
    private static bool IsWellAligned(Type type)
        // Simple check for alignment - more sophisticated logic would be needed in practice
        => type.IsPrimitive || (type.IsValueType && CalculateTypeSize(type) % 4 == 0);
    private static int CalculateTypeSize(Type type)
        if (type.IsPrimitive)
            return Type.GetTypeCode(type) switch
                TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte => 1,
                TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char => 2,
                TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
                TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => 8,
                TypeCode.Decimal => 16,
                _ => 4
            };
            // Sum of field sizes (simplified - doesn't account for padding)
            return fields.Sum(f => CalculateTypeSize(f.FieldType));
        return IntPtr.Size; // Reference types
}
/// Interface for type inference and validation.
public interface ITypeInferenceEngine
    public TypeInferenceResult InferTypes(Expression expression);
    public TypeValidationResult ValidateTypes(Expression expression);
    public IEnumerable<TypeOptimizationSuggestion> SuggestOptimizations(Expression expression);
    public Type GetOptimalGpuType(Type netType);
/// Result of type inference analysis.
public sealed class TypeInferenceResult
    /// Gets the inferred types mapped by type.
    public Dictionary<Type, TypeInfo> InferredTypes { get; init; } = [];
    /// Gets the required type conversions.
    public List<TypeConversion> TypeConversions { get; init; } = [];
    /// Gets validation errors found during inference.
    public List<TypeValidationError> ValidationErrors { get; init; } = [];
    /// Gets metadata about the type analysis.
    public Dictionary<string, object> Metadata { get; init; } = [];
/// Result of type validation.
public sealed class TypeValidationResult
    /// Gets whether all types are valid for GPU compilation.
    public bool IsValid { get; init; }
    /// Gets validation errors and warnings.
    public List<TypeValidationError> Errors { get; init; } = [];
    /// Gets the inferred types.
    /// Gets required type conversions.
/// Information about a type used in an expression.
public sealed class TypeInfo
    /// Gets the .NET type.
    public required Type Type { get; init; }
    /// Gets the locations where this type is used.
    public List<Expression> UsageLocations { get; init; } = [];
    /// Gets the usage context (parameter, return value, intermediate, etc.).
    public List<TypeUsageContext> UsageContexts { get; init; } = [];
    /// Gets whether this type needs conversion for GPU use.
    public bool RequiresConversion { get; init; }
    /// Gets the suggested GPU-compatible type.
    public Type? SuggestedGpuType { get; init; }
/// Represents a required type conversion.
public sealed class TypeConversion
    /// Gets the source type.
    public required Type FromType { get; init; }
    /// Gets the target type.
    public required Type ToType { get; init; }
    /// Gets the expression where conversion is needed.
    public required Expression Expression { get; init; }
    /// Gets the conversion reason.
    public required string Reason { get; init; }
    /// Gets whether the conversion is lossy.
    public bool IsLossy { get; init; }
/// Type validation error.
public sealed class TypeValidationError
    /// Initializes a new instance of the <see cref="TypeValidationError"/> class.
    public TypeValidationError(Type type, string message, Expression expression, TypeValidationSeverity severity)
        Type = type;
        Message = message;
        Expression = expression;
        Severity = severity;
    /// Gets the problematic type.
    public Type Type { get; }
    /// Gets the error message.
    public string Message { get; }
    /// Gets the expression where the error occurs.
    public Expression Expression { get; }
    /// Gets the severity of the validation error.
    public TypeValidationSeverity Severity { get; }
/// Type optimization suggestion.
public sealed class TypeOptimizationSuggestion
    /// Initializes a new instance of the <see cref="TypeOptimizationSuggestion"/> class.
    public TypeOptimizationSuggestion(
        TypeOptimizationType type,
        string description,
        Type currentType,
        Type? suggestedType,
        PerformanceImpact impact)
        Description = description;
        CurrentType = currentType;
        SuggestedType = suggestedType;
        Impact = impact;
    /// Gets the type of optimization.
    public TypeOptimizationType Type { get; }
    /// Gets the description of the optimization.
    public string Description { get; }
    /// Gets the current type.
    public Type CurrentType { get; }
    /// Gets the suggested type (if any).
    public Type? SuggestedType { get; }
    /// Gets the expected performance impact.
    public PerformanceImpact Impact { get; }
/// Capabilities of a type for GPU computation.
internal sealed class TypeCapabilities
    /// Gets or sets whether the type is GPU-compatible.
    public bool IsGpuCompatible { get; set; }
    /// Gets or sets the optimal GPU type to use instead.
    public Type? OptimalGpuType { get; set; }
    /// Gets or sets whether the type can be vectorized.
    public bool CanVectorize { get; set; }
    /// Gets or sets the vector type for this scalar type.
    public Type? VectorType { get; set; }
    /// Gets or sets the memory size in bytes.
    public int MemorySize { get; set; }
    /// Gets or sets the alignment requirement.
    public int AlignmentRequirement { get; set; }
    /// Gets or sets performance notes.
    public string? PerformanceNotes { get; set; }
/// Context in which a type is used.
public enum TypeUsageContext
    /// Used as a parameter.
    Parameter,
    /// Used as a return value.
    ReturnValue,
    /// Used in an intermediate calculation.
    Intermediate,
    /// Used as a constant value.
    Constant,
    /// Used in a binary operation.
    BinaryOperation,
    /// Used in a method call.
    MethodCall
/// Severity of type validation errors.
public enum TypeValidationSeverity
    /// Informational message.
    Info,
    /// Warning that doesn't prevent compilation.
    Warning,
    /// Error that prevents compilation.
    Error
/// Types of type optimizations.
public enum TypeOptimizationType
    /// Vectorization optimization.
    Vectorization,
    /// Precision reduction optimization.
    PrecisionReduction,
    /// Memory layout optimization.
    MemoryLayout,
    /// Type substitution optimization.
    TypeSubstitution,
    /// Alignment optimization.
    Alignment
/// Visitor for analyzing types in expression trees.
internal sealed class TypeInferenceVisitor : ExpressionVisitor
    private readonly ILogger _logger;
    private readonly Dictionary<Type, TypeInfo> _inferredTypes = [];
    private readonly List<TypeConversion> _typeConversions = [];
    private readonly List<TypeValidationError> _validationErrors = [];
    public TypeInferenceVisitor(
        HashSet<Type> supportedTypes,
        Dictionary<Type, TypeCapabilities> typeCapabilities,
        ILogger logger)
        _supportedTypes = supportedTypes;
        _typeCapabilities = typeCapabilities;
        _logger = logger;
    public TypeInferenceResult AnalyzeExpression(Expression expression)
        _ = Visit(expression);
        return new TypeInferenceResult
            InferredTypes = _inferredTypes,
            TypeConversions = _typeConversions,
            ValidationErrors = _validationErrors,
            Metadata = new Dictionary<string, object>
                ["TotalTypes"] = _inferredTypes.Count,
                ["UnsupportedTypes"] = _inferredTypes.Values.Count(t => t.RequiresConversion),
                ["RequiredConversions"] = _typeConversions.Count
    protected override Expression VisitConstant(ConstantExpression node)
        if (node.Value != null)
            AnalyzeType(node.Type, node, TypeUsageContext.Constant);
        return base.VisitConstant(node);
    protected override Expression VisitParameter(ParameterExpression node)
        AnalyzeType(node.Type, node, TypeUsageContext.Parameter);
        return base.VisitParameter(node);
    protected override Expression VisitBinary(BinaryExpression node)
        AnalyzeType(node.Type, node, TypeUsageContext.BinaryOperation);
        // Check for type mismatches that might require conversion
        if (node.Left.Type != node.Right.Type)
            CheckBinaryTypeCompatibility(node);
        return base.VisitBinary(node);
    protected override Expression VisitMethodCall(MethodCallExpression node)
        AnalyzeType(node.Type, node, TypeUsageContext.MethodCall);
        // Analyze parameter types
        var parameters = node.Method.GetParameters();
        for (var i = 0; i < node.Arguments.Count && i < parameters.Length; i++)
            var paramType = parameters[i].ParameterType;
            var argType = node.Arguments[i].Type;
            if (paramType != argType && !IsImplicitlyConvertible(argType, paramType))
                _typeConversions.Add(new TypeConversion
                {
                    FromType = argType,
                    ToType = paramType,
                    Expression = node.Arguments[i],
                    Reason = $"Method {node.Method.Name} parameter {parameters[i].Name} requires {paramType.Name}",
                    IsLossy = IsLossyConversion(argType, paramType)
                });
        return base.VisitMethodCall(node);
    protected override Expression VisitLambda<T>(Expression<T> node)
        AnalyzeType(node.ReturnType, node, TypeUsageContext.ReturnValue);
        return base.VisitLambda(node);
    private void AnalyzeType(Type type, Expression expression, TypeUsageContext context)
        if (!_inferredTypes.TryGetValue(type, out var typeInfo))
            var requiresConversion = !IsDirectlySupported(type);
            var suggestedType = requiresConversion ? GetSuggestedGpuType(type) : null;
            typeInfo = new TypeInfo
                Type = type,
                RequiresConversion = requiresConversion,
                SuggestedGpuType = suggestedType
            _inferredTypes[type] = typeInfo;
        typeInfo.UsageLocations.Add(expression);
        typeInfo.UsageContexts.Add(context);
        // Validate type compatibility
        if (!IsGpuCompatible(type))
            _validationErrors.Add(new TypeValidationError(
                type,
                $"Type {type.Name} is not GPU-compatible",
                expression,
                TypeValidationSeverity.Error));
    private void CheckBinaryTypeCompatibility(BinaryExpression node)
        var leftType = node.Left.Type;
        var rightType = node.Right.Type;
        _ = node.Type;
        // Check if operand types need conversion to common type
        var commonType = GetCommonType(leftType, rightType);
        if (commonType != null)
            if (leftType != commonType)
                    FromType = leftType,
                    ToType = commonType,
                    Expression = node.Left,
                    Reason = $"Binary operation {node.NodeType} requires common type {commonType.Name}",
                    IsLossy = IsLossyConversion(leftType, commonType)
            if (rightType != commonType)
                    FromType = rightType,
                    Expression = node.Right,
                    IsLossy = IsLossyConversion(rightType, commonType)
    private bool IsDirectlySupported(Type type)
        return _supportedTypes.Contains(type) ||
               (type.IsArray && _supportedTypes.Contains(type.GetElementType()!));
    private bool IsGpuCompatible(Type type) => _typeCapabilities.TryGetValue(type, out var capabilities) && capabilities.IsGpuCompatible;
    private Type? GetSuggestedGpuType(Type type) => _typeCapabilities.TryGetValue(type, out var capabilities) ? capabilities.OptimalGpuType : null;
    private static Type? GetCommonType(Type type1, Type type2)
        if (type1 == type2)
            return type1;
        // Numeric promotion rules (simplified)
        if (IsNumericType(type1) && IsNumericType(type2))
            var code1 = Type.GetTypeCode(type1);
            var code2 = Type.GetTypeCode(type2);
            // Return the "wider" type
            return GetWiderType(code1, code2) == code1 ? type1 : type2;
        return null;
    private static bool IsNumericType(Type type)
        var code = Type.GetTypeCode(type);
        return code is >= TypeCode.SByte and <= TypeCode.Decimal;
    private static TypeCode GetWiderType(TypeCode code1, TypeCode code2)
        var order = new[]
        TypeCode.SByte, TypeCode.Byte, TypeCode.Int16, TypeCode.UInt16,
        TypeCode.Int32, TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64,
        TypeCode.Single, TypeCode.Double, TypeCode.Decimal
    };
        var index1 = Array.IndexOf(order, code1);
        var index2 = Array.IndexOf(order, code2);
        return index1 > index2 ? code1 : code2;
    private static bool IsImplicitlyConvertible(Type from, Type to) => to.IsAssignableFrom(from) || HasImplicitConversion(from, to);
    private static bool HasImplicitConversion(Type from, Type to)
        // Check for built-in numeric conversions
        if (IsNumericType(from) && IsNumericType(to))
            var fromCode = Type.GetTypeCode(from);
            var toCode = Type.GetTypeCode(to);
            return GetWiderType(fromCode, toCode) == toCode;
    private static bool IsLossyConversion(Type from, Type to)
            return GetWiderType(fromCode, toCode) == fromCode; // Converting to narrower type
