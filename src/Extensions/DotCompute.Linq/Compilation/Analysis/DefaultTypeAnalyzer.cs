// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Runtime.InteropServices;
using DotCompute.Linq.Types;
namespace DotCompute.Linq.Compilation.Analysis;
/// <summary>
/// Default implementation of type analyzer for common .NET types.
/// </summary>
public class DefaultTypeAnalyzer : ITypeAnalyzer
{
    private readonly Type _targetType;
    /// <summary>
    /// Initializes a new instance of the DefaultTypeAnalyzer class.
    /// </summary>
    /// <param name="targetType">The type this analyzer handles.</param>
    public DefaultTypeAnalyzer(Type targetType)
    {
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }
    /// <inheritdoc />
    public Type TargetType => _targetType;
    public TypeUsageInfo AnalyzeUsage(Expression expression, AnalysisContext context)
        var usageCount = CountTypeUsage(expression);
        var memoryPattern = DetermineMemoryUsagePattern(expression);
        var size = GetTypeSize(_targetType);
        return new TypeUsageInfo
        {
            Type = _targetType,
            UsageFrequency = usageCount,
            UsageCount = usageCount,
            RequiresSpecialization = RequiresSpecialization(_targetType),
            MemoryPattern = (MemoryAccessPattern)(int)memoryPattern,
            SupportsSimd = SupportsVectorization(),
            EstimatedSize = size,
            Hints = GetOptimizationHints(expression).Select(h => h.Description ?? h.ToString() ?? string.Empty).ToList()
        };
    public bool SupportsVectorization()
        // Primitive numeric types generally support vectorization
        return _targetType.IsPrimitive && IsNumericType(_targetType);
    public int GetOptimalAlignment()
        // Return alignment based on type size for optimal SIMD operations
        return size switch
            1 => 16,  // byte: align to 16-byte boundary for SIMD
            2 => 16,  // short: align to 16-byte boundary
            4 => 16,  // int/float: align to 16-byte boundary
            8 => 32,  // long/double: align to 32-byte boundary for AVX
            16 => 32, // Vector128: align to 32-byte boundary
            _ => Math.Max(size, 16)
    public double EstimateOperationComplexity(ExpressionType operation)
        var baseComplexity = GetBaseComplexity(_targetType);
        var operationMultiplier = operation switch
            ExpressionType.Add or ExpressionType.Subtract => 1.0,
            ExpressionType.Multiply => 1.5,
            ExpressionType.Divide => 3.0,
            ExpressionType.Modulo => 4.0,
            ExpressionType.Power => 10.0,
            ExpressionType.Convert => GetConversionComplexity(_targetType),
            _ => 1.0
        return baseComplexity * operationMultiplier;
    public IEnumerable<OptimizationHint> GetOptimizationHints(Expression expression)
        var hints = new List<OptimizationHint>();
        // Vectorization hint
        if (SupportsVectorization())
            hints.Add(new OptimizationHint(
                OptimizationHintType.Vectorization,
                $"Type {_targetType.Name} supports SIMD vectorization",
                OptimizationImpact.High));
        }
        // Memory layout hint
        if (RequiresSpecialization(_targetType))
                OptimizationHintType.MemoryCoalescing,
                $"Type {_targetType.Name} may benefit from memory layout optimization",
                OptimizationImpact.Medium));
        // GPU compatibility hint
        if (IsGpuCompatible(_targetType))
                OptimizationHintType.BackendSpecific,
                $"Type {_targetType.Name} is compatible with GPU execution",
        return hints;
    public TypeUsageInfo Analyze(Expression expression, object? context = null)
        var analysisContext = context as AnalysisContext ?? new AnalysisContext();
        return AnalyzeUsage(expression, analysisContext);
    /// Counts the usage of the target type in an expression.
    private int CountTypeUsage(Expression expression)
        var count = 0;
        CountTypeUsageRecursive(expression, ref count);
        return count;
    /// Recursively counts type usage in an expression tree.
    private void CountTypeUsageRecursive(Expression expression, ref int count)
        if (expression.Type == _targetType)
            count++;
        switch (expression)
            case BinaryExpression binary:
                CountTypeUsageRecursive(binary.Left, ref count);
                CountTypeUsageRecursive(binary.Right, ref count);
                break;
            case UnaryExpression unary:
                CountTypeUsageRecursive(unary.Operand, ref count);
            case MethodCallExpression methodCall:
                foreach (var arg in methodCall.Arguments)
                {
                    CountTypeUsageRecursive(arg, ref count);
                }
                if (methodCall.Object != null)
                    CountTypeUsageRecursive(methodCall.Object, ref count);
            case ConditionalExpression conditional:
                CountTypeUsageRecursive(conditional.Test, ref count);
                CountTypeUsageRecursive(conditional.IfTrue, ref count);
                CountTypeUsageRecursive(conditional.IfFalse, ref count);
            case LambdaExpression lambda:
                CountTypeUsageRecursive(lambda.Body, ref count);
    /// Determines the memory usage pattern for the type.
    private static MemoryUsagePattern DetermineMemoryUsagePattern(Expression expression)
        // Simple heuristic - can be enhanced with more sophisticated analysis
        return expression switch
            MethodCallExpression method when IsSequentialMethod(method) => MemoryUsagePattern.Sequential,
            MethodCallExpression => MemoryUsagePattern.Random,
            BinaryExpression => MemoryUsagePattern.ReadWrite,
            _ => MemoryUsagePattern.ReadOnly
    /// Checks if a method call represents sequential access.
    private static bool IsSequentialMethod(MethodCallExpression method)
        return method.Method.Name switch
            "Select" or "Where" or "Take" or "Skip" => true,
            _ => false
    /// Gets the size of a type in bytes.
    private static int GetTypeSize(Type type)
        if (type.IsPrimitive)
            return Marshal.SizeOf(type);
        if (type.IsValueType)
            try
            {
                return Marshal.SizeOf(type);
            }
            catch
                // Fallback for types that can't be marshalled
                return IntPtr.Size; // Use pointer size as estimate
        // Reference types
        return IntPtr.Size;
    /// Determines if a type requires specialization for optimal performance.
    private static bool RequiresSpecialization(Type type)
        // Complex types, large value types, or generic types may require specialization
        return !type.IsPrimitive && (type.IsValueType || type.IsGenericType);
    /// Checks if a type is numeric.
    private static bool IsNumericType(Type type)
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    /// Gets the base complexity for a type.
    private static double GetBaseComplexity(Type type)
        return type switch
            _ when type == typeof(byte) || type == typeof(sbyte) => 1.0,
            _ when type == typeof(short) || type == typeof(ushort) => 1.0,
            _ when type == typeof(int) || type == typeof(uint) => 1.0,
            _ when type == typeof(long) || type == typeof(ulong) => 1.2,
            _ when type == typeof(float) => 1.5,
            _ when type == typeof(double) => 2.0,
            _ when type == typeof(decimal) => 5.0,
            _ => 3.0 // Default for complex types
    /// Gets the complexity of type conversion operations.
    private static double GetConversionComplexity(Type type)
            _ when type.IsPrimitive => 1.5,
            _ when type.IsValueType => 2.0,
            _ => 3.0
    /// Checks if a type is compatible with GPU execution.
    private static bool IsGpuCompatible(Type type)
        // Only primitive types and simple value types are typically GPU-compatible
        return type.IsPrimitive ||
               (type.IsValueType && !type.IsEnum && type.IsLayoutSequential);
}
