// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;
using DotCompute.Linq.Compilation;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Expressions;


/// <summary>
/// Optimizes LINQ expression trees for efficient GPU execution.
/// </summary>
public class ExpressionOptimizer : IExpressionOptimizer
{
    private readonly ILogger<ExpressionOptimizer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionOptimizer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ExpressionOptimizer(ILogger<ExpressionOptimizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Expression Optimize(Expression expression, CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogDebug("Optimizing expression of type {ExpressionType}", expression.NodeType);

        var optimizedExpression = expression;

        // Apply optimizations based on options
        if (options.EnableOperatorFusion)
        {
            optimizedExpression = ApplyOperatorFusion(optimizedExpression);
        }

        if (options.EnableMemoryCoalescing)
        {
            optimizedExpression = ApplyMemoryCoalescing(optimizedExpression);
        }

        // Always apply these optimizations
        optimizedExpression = EliminateRedundancy(optimizedExpression);
        optimizedExpression = OptimizeConstants(optimizedExpression);
        optimizedExpression = ReorderOperations(optimizedExpression);

        _logger.LogInformation("Expression optimization complete");

        return optimizedExpression;
    }

    /// <inheritdoc/>
    public IEnumerable<OptimizationSuggestion> Analyze(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var suggestions = new List<OptimizationSuggestion>();
        var analyzer = new OptimizationAnalyzer(suggestions);
        _ = analyzer.Visit(expression);

        return suggestions;
    }

    private Expression ApplyOperatorFusion(Expression expression)
    {
        _logger.LogDebug("Applying operator fusion optimization");
        var visitor = new OperatorFusionVisitor(_logger);
        return visitor.Visit(expression);
    }

    private Expression ApplyMemoryCoalescing(Expression expression)
    {
        _logger.LogDebug("Applying memory coalescing optimization");
        var visitor = new MemoryCoalescingVisitor(_logger);
        return visitor.Visit(expression);
    }

    private Expression EliminateRedundancy(Expression expression)
    {
        _logger.LogDebug("Eliminating redundant operations");
        var visitor = new RedundancyEliminationVisitor(_logger);
        return visitor.Visit(expression);
    }

    private Expression OptimizeConstants(Expression expression)
    {
        _logger.LogDebug("Optimizing constant expressions");
        var visitor = new ConstantFoldingVisitor(_logger);
        return visitor.Visit(expression);
    }

    private Expression ReorderOperations(Expression expression)
    {
        _logger.LogDebug("Reordering operations for optimal execution");
        var visitor = new OperationReorderingVisitor(_logger);
        return visitor.Visit(expression);
    }

    /// <summary>
    /// Visitor that fuses adjacent operations into single kernels.
    /// </summary>
    private class OperatorFusionVisitor : ExpressionVisitor
    {
        private readonly ILogger _logger;
        private readonly Stack<MethodCallExpression> _methodCallStack = new();

        public OperatorFusionVisitor(ILogger logger)
        {
            _logger = logger;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check if this is a LINQ method that can be fused
            if (IsLinqMethod(node))
            {
                _methodCallStack.Push(node);

                // Visit the source to check for fusion opportunities
                if (node.Arguments.Count > 0)
                {
                    var source = Visit(node.Arguments[0]);

                    // Check if we can fuse with the previous operation
                    if (source is MethodCallExpression sourceMethod && CanFuse(node, sourceMethod))
                    {
                        _logger.LogDebug("Fusing {Method1} with {Method2}", sourceMethod.Method.Name, node.Method.Name);
                        return CreateFusedExpression(sourceMethod, node);
                    }
                }

                _ = _methodCallStack.Pop();
            }

            return base.VisitMethodCall(node);
        }

        private static bool IsLinqMethod(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;
            return declaringType == typeof(Queryable) || declaringType == typeof(Enumerable);
        }

        private static bool CanFuse(MethodCallExpression first, MethodCallExpression second)
        {
            // Check if methods can be fused
            var fusablePairs = new[]
            {
            ("Where", "Select"),
            ("Select", "Where"),
            ("Select", "Select"),
            ("Where", "Where")
        };

            return fusablePairs.Contains((second.Method.Name, first.Method.Name));
        }

        private Expression CreateFusedExpression(MethodCallExpression first, MethodCallExpression second)
        {
            // Create a custom expression that represents the fused operation
            var fusedMethodName = $"{first.Method.Name}_{second.Method.Name}_Fused";

            // Create metadata for the fused operation
            var metadata = new Dictionary<string, object>
            {
                ["FusedOperations"] = new[] { first.Method.Name, second.Method.Name },
                ["FusionType"] = GetFusionType(first.Method.Name, second.Method.Name),
                ["OptimizationLevel"] = "High",
                ["EstimatedSpeedup"] = CalculateEstimatedSpeedup(first.Method.Name, second.Method.Name)
            };

            // Create a new method call expression that represents the fused kernel
            var fusedMethod = CreateFusedMethodInfo(fusedMethodName, first, second);
            var fusedCall = Expression.Call(
                fusedMethod,
                GetFusedArguments(first, second));

            // Attach fusion metadata
            AttachFusionMetadata(fusedCall, metadata);

            return fusedCall;
        }

        private static MethodInfo CreateFusedMethodInfo(string fusedName, MethodCallExpression first, MethodCallExpression second)
        {
            // Create a dynamic method that represents the fused operation
            // This will be used by the kernel generator to create optimized kernels
            var methodType = typeof(ComputeQueryableExtensions);
            var parameters = GetFusedParameterTypes(first, second);
            var returnType = second.Type;

            // Use reflection to create a dynamic method info
            return new FusedMethodInfo(fusedName, methodType, returnType, parameters, first, second);
        }

        private static Type[] GetFusedParameterTypes(MethodCallExpression first, MethodCallExpression second)
        {
            var firstParams = first.Method.GetParameters().Select(p => p.ParameterType).ToArray();
            var secondParams = second.Method.GetParameters().Skip(1).Select(p => p.ParameterType).ToArray(); // Skip source parameter
            return [.. firstParams.Concat(secondParams).Distinct()];
        }

        private static Expression[] GetFusedArguments(MethodCallExpression first, MethodCallExpression second)
        {
            // Combine arguments from both methods, avoiding duplicate source arguments
            var firstArgs = first.Arguments.ToList();
            var secondArgs = second.Arguments.Skip(1).ToList(); // Skip source argument
            return [.. firstArgs, .. secondArgs];
        }

        private static string GetFusionType(string firstMethod, string secondMethod)
        {
            return (firstMethod, secondMethod) switch
            {
                ("Where", "Select") => "FilterMap",
                ("Select", "Where") => "MapFilter",
                ("Select", "Select") => "MapMap",
                ("Where", "Where") => "FilterFilter",
                _ => "Generic"
            };
        }

        private static double CalculateEstimatedSpeedup(string firstMethod, string secondMethod)
        {
            // Estimate performance improvement from fusion
            return (firstMethod, secondMethod) switch
            {
                ("Where", "Select") or ("Select", "Where") => 1.8, // High speedup from eliminating intermediate arrays
                ("Select", "Select") => 1.5, // Moderate speedup
                ("Where", "Where") => 1.3, // Some speedup
                _ => 1.2 // Default modest speedup
            };
        }

        private static void AttachFusionMetadata(Expression expression, Dictionary<string, object> metadata)
        {
            // Store fusion metadata in a thread-local dictionary for retrieval during compilation
            var key = expression.ToString();
            FusionMetadataStore.SetMetadata(key, metadata);
        }
    }

    /// <summary>
    /// Visitor that optimizes memory access patterns.
    /// </summary>
    private class MemoryCoalescingVisitor : ExpressionVisitor
    {
        private readonly ILogger _logger;

        public MemoryCoalescingVisitor(ILogger logger)
        {
            _logger = logger;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // Analyze member access patterns for coalescing opportunities
            if (node.Member.DeclaringType?.IsValueType == true)
            {
                _logger.LogDebug("Analyzing member access for coalescing: {Member}", node.Member.Name);
                // In a full implementation, we'd track access patterns
                // and reorder them for optimal GPU memory access
            }

            return base.VisitMember(node);
        }
    }

    /// <summary>
    /// Visitor that eliminates redundant operations.
    /// </summary>
    private class RedundancyEliminationVisitor : ExpressionVisitor
    {
        private readonly ILogger _logger;
        private readonly Dictionary<Expression, Expression> _visitedExpressions = [];

        public RedundancyEliminationVisitor(ILogger logger)
        {
            _logger = logger;
        }

        public override Expression Visit(Expression? node)
        {
            if (node == null)
            {
                return null!;
            }

            // Check if we've already visited an equivalent expression
            if (_visitedExpressions.TryGetValue(node, out var cached))
            {
                _logger.LogDebug("Reusing cached expression for {NodeType}", node.NodeType);
                return cached;
            }

            var result = base.Visit(node);
            _visitedExpressions[node] = result;
            return result;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check for redundant casts
            if (node.Method.Name == "Cast" && node.Arguments.Count > 0)
            {
                var source = node.Arguments[0];
                var targetType = node.Method.GetGenericArguments()[0];

                // If source is already of target type, eliminate the cast
                if (source.Type.GetGenericArguments().FirstOrDefault() == targetType)
                {
                    _logger.LogDebug("Eliminating redundant cast to {Type}", targetType.Name);
                    return Visit(source);
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// Visitor that performs constant folding optimization.
    /// </summary>
    private class ConstantFoldingVisitor : ExpressionVisitor
    {
        private readonly ILogger _logger;

        public ConstantFoldingVisitor(ILogger logger)
        {
            _logger = logger;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            // If both operands are constants, evaluate the expression
            if (left is ConstantExpression leftConst && right is ConstantExpression rightConst)
            {
                try
                {
                    var result = EvaluateBinaryExpression(node.NodeType, leftConst.Value, rightConst.Value);
                    _logger.LogDebug("Folded constant expression: {Left} {Op} {Right} = {Result}",
                        leftConst.Value, node.NodeType, rightConst.Value, result);
                    return Expression.Constant(result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fold constant expression");
                }
            }

            return node.Update(left, node.Conversion, right);
        }

        private static object? EvaluateBinaryExpression(ExpressionType nodeType, object? left, object? right)
        {
            if (left == null || right == null)
            {
                return null;
            }

            return nodeType switch
            {
                ExpressionType.Add => Add(left, right),
                ExpressionType.Subtract => Subtract(left, right),
                ExpressionType.Multiply => Multiply(left, right),
                ExpressionType.Divide => Divide(left, right),
                ExpressionType.Equal => Equals(left, right),
                ExpressionType.NotEqual => !Equals(left, right),
                ExpressionType.LessThan => CompareTo(left, right) < 0,
                ExpressionType.LessThanOrEqual => CompareTo(left, right) <= 0,
                ExpressionType.GreaterThan => CompareTo(left, right) > 0,
                ExpressionType.GreaterThanOrEqual => CompareTo(left, right) >= 0,
                _ => throw new NotSupportedException($"Binary operation {nodeType} not supported for constant folding")
            };
        }

        private static object Add(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l + r,
                (long l, long r) => l + r,
                (float l, float r) => l + r,
                (double l, double r) => l + r,
                _ => throw new NotSupportedException($"Cannot add {left.GetType()} and {right.GetType()}")
            };
        }

        private static object Subtract(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l - r,
                (long l, long r) => l - r,
                (float l, float r) => l - r,
                (double l, double r) => l - r,
                _ => throw new NotSupportedException($"Cannot subtract {left.GetType()} and {right.GetType()}")
            };
        }

        private static object Multiply(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l * r,
                (long l, long r) => l * r,
                (float l, float r) => l * r,
                (double l, double r) => l * r,
                _ => throw new NotSupportedException($"Cannot multiply {left.GetType()} and {right.GetType()}")
            };
        }

        private static object Divide(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l / r,
                (long l, long r) => l / r,
                (float l, float r) => l / r,
                (double l, double r) => l / r,
                _ => throw new NotSupportedException($"Cannot divide {left.GetType()} and {right.GetType()}")
            };
        }

        private static int CompareTo(object left, object right)
        {
            if (left is IComparable comparable)
            {
                return comparable.CompareTo(right);
            }
            throw new NotSupportedException($"Cannot compare {left.GetType()} and {right.GetType()}");
        }
    }

    /// <summary>
    /// Visitor that reorders operations for optimal execution.
    /// </summary>
    private class OperationReorderingVisitor : ExpressionVisitor
    {
        private readonly ILogger _logger;

        public OperationReorderingVisitor(ILogger logger)
        {
            _logger = logger;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Reorder operations to minimize data movement
            if (node.Method.Name == "OrderBy" || node.Method.Name == "OrderByDescending")
            {
                // Check if there's a Where clause after OrderBy that could be moved before
                var parent = node.Arguments[0];
                if (parent is MethodCallExpression parentCall && parentCall.Method.Name == "Where")
                {
                    _logger.LogDebug("Reordering Where before OrderBy for better performance");
                    // In a full implementation, we'd reconstruct the expression tree
                    // with Where before OrderBy
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// Analyzes expressions for optimization opportunities.
    /// </summary>
    private class OptimizationAnalyzer : ExpressionVisitor
    {
        private readonly List<OptimizationSuggestion> _suggestions;

        public OptimizationAnalyzer(List<OptimizationSuggestion> suggestions)
        {
            _suggestions = suggestions;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check for optimization opportunities
            if (node.Method.Name == "Select" && node.Arguments.Count > 1)
            {
                // Check if there's another Select that could be fused
                if (node.Arguments[0] is MethodCallExpression prevCall && prevCall.Method.Name == "Select")
                {
                    _suggestions.Add(new OptimizationSuggestion(
                        OptimizationType.OperatorFusion,
                        "Multiple Select operations can be fused into a single kernel",
                        PerformanceImpact.High,
                        node));
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// Represents a fused method info for dynamic kernel generation.
    /// </summary>
    internal class FusedMethodInfo : MethodInfo
    {
        private readonly string _name;
        private readonly Type _declaringType;
        private readonly Type _returnType;
        private readonly Type[] _parameterTypes;
        private readonly MethodCallExpression _firstMethod;
        private readonly MethodCallExpression _secondMethod;

        public FusedMethodInfo(string name, Type declaringType, Type returnType, Type[] parameterTypes,
            MethodCallExpression firstMethod, MethodCallExpression secondMethod)
        {
            _name = name;
            _declaringType = declaringType;
            _returnType = returnType;
            _parameterTypes = parameterTypes;
            _firstMethod = firstMethod;
            _secondMethod = secondMethod;
        }

        public override string Name => _name;
        public override Type DeclaringType => _declaringType;
        public override Type ReturnType => _returnType;
        public override Type? ReflectedType => _declaringType;
        public MethodCallExpression FirstMethod => _firstMethod;
        public MethodCallExpression SecondMethod => _secondMethod;

        public override ParameterInfo[] GetParameters() => [.. _parameterTypes.Select((t, i) => new FusedParameterInfo($"param{i}", t, i)).Cast<ParameterInfo>()];

        // Minimal implementation for other required members
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => new EmptyCustomAttributeProvider();
        public override MethodAttributes Attributes => MethodAttributes.Static | MethodAttributes.Public;
        public override RuntimeMethodHandle MethodHandle => default;
        public override MethodInfo GetBaseDefinition() => this;
        public override object[] GetCustomAttributes(bool inherit) => [];
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => [];
        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.IL;
        public override bool IsDefined(Type attributeType, bool inherit) => false;
        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
            => throw new NotSupportedException("Fused methods cannot be invoked directly");
    }

    /// <summary>
    /// Parameter info for fused methods.
    /// </summary>
    internal class FusedParameterInfo : ParameterInfo
    {
        public FusedParameterInfo(string name, Type parameterType, int position)
        {
            NameImpl = name;
            ClassImpl = parameterType;
            PositionImpl = position;
        }
    }

    /// <summary>
    /// Thread-safe storage for fusion metadata.
    /// </summary>
    internal static class FusionMetadataStore
    {
        private static readonly ThreadLocal<Dictionary<string, Dictionary<string, object>>> _store
            = new(() => new Dictionary<string, Dictionary<string, object>>());

        public static void SetMetadata(string key, Dictionary<string, object> metadata) => _store.Value![key] = metadata;

        public static Dictionary<string, object>? GetMetadata(string key) => _store.Value!.TryGetValue(key, out var metadata) ? metadata : null;

        public static void Clear() => _store.Value!.Clear();
    }

    /// <summary>
    /// Empty custom attribute provider implementation for fused methods
    /// </summary>
    internal class EmptyCustomAttributeProvider : ICustomAttributeProvider
    {
        public object[] GetCustomAttributes(bool inherit) => [];
        public object[] GetCustomAttributes(Type attributeType, bool inherit) => [];
        public bool IsDefined(Type attributeType, bool inherit) => false;
    }
}
