// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Linq.Expressions;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Translates LINQ lambda expressions to GPU code (CUDA, OpenCL, Metal).
/// </summary>
/// <remarks>
/// <para>
/// This class provides a centralized mechanism for converting C# lambda expressions
/// into GPU shader code. It supports:
/// </para>
/// <list type="bullet">
/// <item><description>Arithmetic operations: +, -, *, /, %</description></item>
/// <item><description>Comparison operations: ==, !=, &lt;, &lt;=, &gt;, &gt;=</description></item>
/// <item><description>Logical operations: &amp;&amp;, ||, !</description></item>
/// <item><description>Math functions: Sqrt, Pow, Abs, Min, Max, etc.</description></item>
/// <item><description>Member access for struct fields</description></item>
/// <item><description>Type-appropriate constant formatting</description></item>
/// </list>
/// <para>
/// The translator handles GPU-specific requirements like:
/// - Type suffixes for constants (2.0f for float, 2.0 for double)
/// - Function name mapping (.NET to GPU intrinsics)
/// - Parameter substitution for kernel input variables
/// </para>
/// </remarks>
public sealed class GpuExpressionTranslator
{
    private readonly GpuBackendType _backend;
    private int _tempVarCounter;

    /// <summary>
    /// Supported GPU backend types for code generation.
    /// </summary>
    public enum GpuBackendType
    {
        /// <summary>NVIDIA CUDA backend.</summary>
        Cuda,
        /// <summary>OpenCL cross-platform backend.</summary>
        OpenCL,
        /// <summary>Apple Metal backend.</summary>
        Metal
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuExpressionTranslator"/> class.
    /// </summary>
    /// <param name="backend">The target GPU backend for code generation.</param>
    public GpuExpressionTranslator(GpuBackendType backend = GpuBackendType.Cuda)
    {
        _backend = backend;
        _tempVarCounter = 0;
    }

    /// <summary>
    /// Translates a lambda expression to GPU code with parameter substitution.
    /// </summary>
    /// <param name="lambda">The lambda expression to translate.</param>
    /// <param name="inputVar">The GPU variable name to substitute for the lambda parameter.</param>
    /// <returns>The GPU code string representing the lambda body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when lambda or inputVar is null.</exception>
    /// <example>
    /// <code>
    /// // Lambda: x => x.Id
    /// var translator = new GpuExpressionTranslator(GpuBackendType.Cuda);
    /// string code = translator.TranslateLambda(lambda, "element");
    /// // Result: "element.Id"
    /// </code>
    /// </example>
    public string TranslateLambda(LambdaExpression lambda, string inputVar)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        ArgumentNullException.ThrowIfNull(inputVar);

        var body = lambda.Body;
        var parameter = lambda.Parameters[0];

        // Replace parameter references with input variable
        var visitor = new ParameterReplacementVisitor(parameter, inputVar);
        var replaced = visitor.Visit(body);

        return ExpressionToCode(replaced);
    }

    /// <summary>
    /// Translates an expression to GPU code.
    /// </summary>
    /// <param name="expression">The expression to translate.</param>
    /// <returns>The GPU code string.</returns>
    public string TranslateExpression(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ExpressionToCode(expression);
    }

    /// <summary>
    /// Gets the appropriate GPU type name for a .NET type.
    /// </summary>
    /// <param name="type">The .NET type to map.</param>
    /// <returns>The GPU type name.</returns>
    public string MapTypeToGpu(Type type)
    {
        return _backend switch
        {
            GpuBackendType.Cuda => MapTypeToCuda(type),
            GpuBackendType.OpenCL => MapTypeToOpenCL(type),
            GpuBackendType.Metal => MapTypeToMetal(type),
            _ => MapTypeToCuda(type)
        };
    }

    /// <summary>
    /// Converts an expression tree to GPU code.
    /// </summary>
    private string ExpressionToCode(Expression expr)
    {
        return expr switch
        {
            BinaryExpression binary => GenerateBinaryExpression(binary),
            UnaryExpression unary when unary.NodeType == ExpressionType.Convert =>
                GenerateCast(unary),
            UnaryExpression unary =>
                $"{GetOperator(unary.NodeType)}({ExpressionToCode(unary.Operand)})",
            ConstantExpression constant => FormatConstant(constant),
            MemberExpression member => GenerateMemberAccess(member),
            MethodCallExpression method => GenerateMethodCall(method),
            ParameterExpression parameter => parameter.Name ?? $"temp{_tempVarCounter++}",
            ConditionalExpression conditional => GenerateConditional(conditional),
            NewExpression newExpr => GenerateNewExpression(newExpr),
            _ => $"/* Unsupported: {expr.NodeType} */"
        };
    }

    /// <summary>
    /// Generates code for a binary expression.
    /// </summary>
    private string GenerateBinaryExpression(BinaryExpression binary)
    {
        var left = ExpressionToCode(binary.Left);
        var op = GetOperator(binary.NodeType);
        var right = ExpressionToCode(binary.Right);

        return $"({left} {op} {right})";
    }

    /// <summary>
    /// Generates code for a type cast.
    /// </summary>
    private string GenerateCast(UnaryExpression unary)
    {
        var targetType = MapTypeToGpu(unary.Type);
        var operand = ExpressionToCode(unary.Operand);

        // For simple numeric conversions, just return the operand
        // GPU compilers handle implicit conversions well
        if (IsNumericType(unary.Type) && IsNumericType(unary.Operand.Type))
        {
            // Only add explicit cast for potentially lossy conversions
            if (RequiresExplicitCast(unary.Operand.Type, unary.Type))
            {
                return $"(({targetType})({operand}))";
            }
            return operand;
        }

        return $"(({targetType})({operand}))";
    }

    /// <summary>
    /// Generates code for member access (field/property).
    /// </summary>
    private string GenerateMemberAccess(MemberExpression member)
    {
        if (member.Expression == null)
        {
            // Static member access
            return member.Member.Name;
        }

        var obj = ExpressionToCode(member.Expression);
        return $"{obj}.{member.Member.Name}";
    }

    /// <summary>
    /// Generates code for a method call.
    /// </summary>
    private string GenerateMethodCall(MethodCallExpression method)
    {
        var args = string.Join(", ", method.Arguments.Select(ExpressionToCode));

        // Map common .NET methods to GPU intrinsics
        var methodName = MapMethodToGpu(method.Method.Name);

        if (method.Object != null)
        {
            // Instance method
            var obj = ExpressionToCode(method.Object);
            return $"{methodName}({obj}, {args})";
        }

        return $"{methodName}({args})";
    }

    /// <summary>
    /// Generates code for a conditional (ternary) expression.
    /// </summary>
    private string GenerateConditional(ConditionalExpression conditional)
    {
        var test = ExpressionToCode(conditional.Test);
        var ifTrue = ExpressionToCode(conditional.IfTrue);
        var ifFalse = ExpressionToCode(conditional.IfFalse);

        return $"(({test}) ? ({ifTrue}) : ({ifFalse}))";
    }

    /// <summary>
    /// Generates code for a new expression (struct initialization).
    /// </summary>
    private string GenerateNewExpression(NewExpression newExpr)
    {
        // For GPU code, we typically need to handle struct initialization
        // This is a simplified implementation
        if (newExpr.Arguments.Count == 0)
        {
            return $"(({MapTypeToGpu(newExpr.Type)}){{0}})";
        }

        var args = string.Join(", ", newExpr.Arguments.Select(ExpressionToCode));
        return $"(({MapTypeToGpu(newExpr.Type)}){{{args}}})";
    }

    /// <summary>
    /// Formats a constant expression with appropriate type suffix.
    /// </summary>
    private string FormatConstant(ConstantExpression constant)
    {
        if (constant.Value == null)
        {
            return "0"; // GPU doesn't have null concept
        }

        return constant.Type switch
        {
            Type t when t == typeof(float) => FormatFloatConstant(constant.Value),
            Type t when t == typeof(double) => FormatDoubleConstant(constant.Value),
            Type t when t == typeof(long) => $"{constant.Value}LL",
            Type t when t == typeof(ulong) => $"{constant.Value}ULL",
            Type t when t == typeof(uint) => $"{constant.Value}u",
            Type t when t == typeof(bool) => (bool)constant.Value ? "1" : "0",
            _ => constant.Value.ToString() ?? "0"
        };
    }

    /// <summary>
    /// Formats a float constant with proper GPU syntax.
    /// </summary>
    private static string FormatFloatConstant(object? value)
    {
        if (value is float floatValue)
        {
            var strValue = floatValue.ToString("0.0#################", CultureInfo.InvariantCulture);
            return $"{strValue}f";
        }
        return "0.0f";
    }

    /// <summary>
    /// Formats a double constant with proper GPU syntax.
    /// </summary>
    private static string FormatDoubleConstant(object? value)
    {
        if (value is double doubleValue)
        {
            return doubleValue.ToString("0.0#################", CultureInfo.InvariantCulture);
        }
        return "0.0";
    }

    /// <summary>
    /// Gets the GPU operator string for an expression type.
    /// </summary>
    private static string GetOperator(ExpressionType type)
    {
        return type switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Modulo => "%",
            ExpressionType.Equal => "==",
            ExpressionType.NotEqual => "!=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.AndAlso => "&&",
            ExpressionType.OrElse => "||",
            ExpressionType.Not => "!",
            ExpressionType.Negate => "-",
            ExpressionType.And => "&",
            ExpressionType.Or => "|",
            ExpressionType.ExclusiveOr => "^",
            ExpressionType.LeftShift => "<<",
            ExpressionType.RightShift => ">>",
            _ => $"/* Op: {type} */"
        };
    }

    /// <summary>
    /// Maps a .NET method name to GPU intrinsic.
    /// </summary>
    private string MapMethodToGpu(string methodName)
    {
        // Standard math functions that work across all GPU backends
        return methodName switch
        {
            "Sqrt" => "sqrt",
            "Pow" => "pow",
            "Abs" => _backend == GpuBackendType.Metal ? "abs" : "fabs",
            "Min" => _backend == GpuBackendType.Metal ? "min" : "fminf",
            "Max" => _backend == GpuBackendType.Metal ? "max" : "fmaxf",
            "Floor" => "floor",
            "Ceiling" => "ceil",
            "Round" => "round",
            "Sin" => "sin",
            "Cos" => "cos",
            "Tan" => "tan",
            "Asin" => "asin",
            "Acos" => "acos",
            "Atan" => "atan",
            "Atan2" => "atan2",
            "Exp" => "exp",
            "Log" => "log",
            "Log10" => "log10",
            "Log2" => "log2",
            "Clamp" => "clamp",
            _ => methodName.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Maps a .NET type to CUDA type.
    /// </summary>
    private static string MapTypeToCuda(Type type)
    {
        return type switch
        {
            Type t when t == typeof(byte) => "unsigned char",
            Type t when t == typeof(sbyte) => "char",
            Type t when t == typeof(short) => "short",
            Type t when t == typeof(ushort) => "unsigned short",
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(uint) => "unsigned int",
            Type t when t == typeof(long) => "long long",
            Type t when t == typeof(ulong) => "unsigned long long",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(bool) => "int", // CUDA uses int for bool in kernel params
            _ => "int" // Default fallback
        };
    }

    /// <summary>
    /// Maps a .NET type to OpenCL type.
    /// </summary>
    private static string MapTypeToOpenCL(Type type)
    {
        return type switch
        {
            Type t when t == typeof(byte) => "uchar",
            Type t when t == typeof(sbyte) => "char",
            Type t when t == typeof(short) => "short",
            Type t when t == typeof(ushort) => "ushort",
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(uint) => "uint",
            Type t when t == typeof(long) => "long",
            Type t when t == typeof(ulong) => "ulong",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(bool) => "int",
            _ => "int"
        };
    }

    /// <summary>
    /// Maps a .NET type to Metal type.
    /// </summary>
    private static string MapTypeToMetal(Type type)
    {
        return type switch
        {
            Type t when t == typeof(byte) => "uint8_t",
            Type t when t == typeof(sbyte) => "int8_t",
            Type t when t == typeof(short) => "int16_t",
            Type t when t == typeof(ushort) => "uint16_t",
            Type t when t == typeof(int) => "int32_t",
            Type t when t == typeof(uint) => "uint32_t",
            Type t when t == typeof(long) => "int64_t",
            Type t when t == typeof(ulong) => "uint64_t",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double", // Note: FP64 support varies on Metal
            Type t when t == typeof(bool) => "bool",
            _ => "int32_t"
        };
    }

    /// <summary>
    /// Checks if a type is numeric.
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double);
    }

    /// <summary>
    /// Determines if an explicit cast is required between types.
    /// </summary>
    private static bool RequiresExplicitCast(Type from, Type to)
    {
        // Conversions that might lose precision
        if (to == typeof(int) && (from == typeof(long) || from == typeof(float) || from == typeof(double)))
            return true;
        if (to == typeof(float) && from == typeof(double))
            return true;
        if (to == typeof(short) && (from == typeof(int) || from == typeof(long)))
            return true;
        if (to == typeof(byte) && from != typeof(byte))
            return true;

        return false;
    }

    /// <summary>
    /// Visitor that replaces parameter expressions with a named variable reference.
    /// </summary>
    private sealed class ParameterReplacementVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly string _replacement;

        public ParameterReplacementVisitor(ParameterExpression parameter, string replacement)
        {
            _parameter = parameter;
            _replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == _parameter)
            {
                return Expression.Parameter(node.Type, _replacement);
            }
            return base.VisitParameter(node);
        }
    }
}
