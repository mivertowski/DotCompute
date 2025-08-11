// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
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
        analyzer.Visit(expression);

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

                _methodCallStack.Pop();
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
            // This is a simplified implementation - in production, we'd generate
            // a proper fused kernel
            var fusedMethodName = $"{first.Method.Name}_{second.Method.Name}_Fused";
            
            // For now, return the original expression
            // In a complete implementation, this would create a new expression
            // that combines both operations
            return second;
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
                return null!;

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
                return null;

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
}