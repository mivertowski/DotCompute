using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Optimization;
using OperationType = DotCompute.Linq.Optimization.OperationType;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Visits LINQ expression trees and builds an operation graph for optimization and compilation.
/// </summary>
/// <remarks>
/// This visitor traverses LINQ expression trees, identifies supported operations,
/// extracts lambda expressions, and builds a directed acyclic graph (DAG) of operations
/// with proper dependency tracking. The resulting operation graph is used for:
/// - Backend selection and optimization
/// - Kernel fusion opportunities
/// - Memory access pattern analysis
/// - Parallel execution planning
/// </remarks>
public class ExpressionTreeVisitor : ExpressionVisitor
{
    private readonly Collection<Operation> _operations = new();
    private readonly Dictionary<Expression, string> _expressionToNodeId = new();
    private readonly Stack<string> _dependencyStack = new();
    private int _nodeCounter;
    private string? _currentNodeId;

    /// <summary>
    /// Visits an expression tree and extracts operations into an operation graph.
    /// </summary>
    /// <param name="expression">The expression tree to analyze.</param>
    /// <returns>An operation graph representing the computation structure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when expression is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the expression contains unsupported operations.</exception>
    public new OperationGraph Visit(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Clear state from previous visits
        _operations.Clear();
        _expressionToNodeId.Clear();
        _dependencyStack.Clear();
        _nodeCounter = 0;
        _currentNodeId = null;

        // Visit the expression tree
        var result = base.Visit(expression);

        // Build metadata
        var metadata = new Dictionary<string, object>
        {
            ["OriginalExpression"] = expression,
            ["ResultExpression"] = result,
            ["TotalOperations"] = _operations.Count,
            ["AnalysisTimestamp"] = DateTime.UtcNow
        };

        // Find root operation (last operation with no dependents)
        var root = _operations.Count > 0 ? _operations[^1] : null;

        return new OperationGraph
        {
            Operations = _operations,
            Metadata = new ReadOnlyDictionary<string, object>(metadata),
            Root = root
        };
    }

    /// <summary>
    /// Visits a method call expression, identifying LINQ operations.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Check if this is a LINQ method
        if (IsLinqMethod(node, out string? methodName) && methodName != null)
        {
            var operationType = MapMethodToOperationType(methodName);

            if (operationType == null)
            {
                throw new NotSupportedException(
                    $"LINQ method '{methodName}' is not supported for GPU acceleration. " +
                    $"Supported methods include: Select, Where, Sum, Count, Average, OrderBy, GroupBy, Join, etc.");
            }

            // Create operation node
            var nodeId = GenerateNodeId();
            var previousNodeId = _currentNodeId;
            _currentNodeId = nodeId;

            // Visit arguments first to establish dependencies
            var dependencies = new Collection<string>();

            // Visit the source (first argument for extension methods)
            if (node.Object != null)
            {
                _dependencyStack.Push(nodeId);
                Visit(node.Object);
                if (_dependencyStack.Count > 0 && _dependencyStack.Peek() == nodeId)
                {
                    _dependencyStack.Pop();
                }
            }
            else if (node.Arguments.Count > 0)
            {
                _dependencyStack.Push(nodeId);
                Visit(node.Arguments[0]);
                if (_dependencyStack.Count > 0 && _dependencyStack.Peek() == nodeId)
                {
                    _dependencyStack.Pop();
                }
            }

            // Collect dependencies
            if (previousNodeId != null)
            {
                dependencies.Add(previousNodeId);
            }

            // Extract lambda expressions and other metadata
            var metadata = ExtractOperationMetadata(node, methodName);

            // Create the operation
            var operation = new Operation
            {
                Id = nodeId,
                Type = operationType.Value,
                Dependencies = dependencies,
                Metadata = new ReadOnlyDictionary<string, object>(metadata),
                EstimatedCost = EstimateOperationCost(operationType.Value, metadata)
            };

            _operations.Add(operation);
            _expressionToNodeId[node] = nodeId;

            return node;
        }

        // Not a LINQ method, continue visiting
        return base.VisitMethodCall(node);
    }

    /// <summary>
    /// Visits a unary expression (e.g., conversions, negations).
    /// </summary>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Check for non-deterministic operations
        if (node.NodeType == ExpressionType.Quote)
        {
            return base.VisitUnary(node);
        }

        return base.VisitUnary(node);
    }

    /// <summary>
    /// Visits a binary expression (e.g., arithmetic, comparisons).
    /// </summary>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Check for non-deterministic operations in the binary expression
        DetectNonDeterministicOperations(node);

        return base.VisitBinary(node);
    }

    /// <summary>
    /// Visits a lambda expression and extracts its structure.
    /// </summary>
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Check for unsupported operations in lambda body
        DetectNonDeterministicOperations(node.Body);

        return base.VisitLambda(node);
    }

    /// <summary>
    /// Visits a constant expression.
    /// </summary>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return base.VisitConstant(node);
    }

    /// <summary>
    /// Checks if a method call is a LINQ method.
    /// </summary>
    private static bool IsLinqMethod(MethodCallExpression node, out string? methodName)
    {
        methodName = node.Method.Name;

        // Check if it's from Queryable or Enumerable
        var declaringType = node.Method.DeclaringType;
        if (declaringType == null)
        {
            return false;
        }

        return declaringType == typeof(Queryable) ||
               declaringType == typeof(Enumerable) ||
               declaringType.Name == "ComputeQueryableExtensions";
    }

    /// <summary>
    /// Maps a LINQ method name to an operation type.
    /// </summary>
    private static OperationType? MapMethodToOperationType(string methodName)
    {
        return methodName switch
        {
            // Map operations
            "Select" or "Cast" => OperationType.Map,

            // Filter operations
            "Where" or "OfType" => OperationType.Filter,

            // Reduce operations
            "Aggregate" or "Sum" or "Count" or "LongCount" or
            "Average" or "Min" or "Max" or "Any" or "All" => OperationType.Reduce,

            // Scan operations
            "TakeWhile" or "SkipWhile" => OperationType.Scan,

            // Join operations
            "Join" or "GroupJoin" => OperationType.Join,

            // GroupBy operations
            "GroupBy" => OperationType.GroupBy,

            // OrderBy operations
            "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" => OperationType.OrderBy,

            // Set operations (treated as aggregates)
            "Distinct" or "Union" or "Intersect" or "Except" => OperationType.Aggregate,

            // Take/Skip operations (treated as filters)
            "Take" or "Skip" or "First" or "FirstOrDefault" or
            "Last" or "LastOrDefault" or "Single" or "SingleOrDefault" => OperationType.Filter,

            // Unsupported operations
            "Zip" or "SelectMany" or "Concat" or "Reverse" => null,

            _ => null
        };
    }

    /// <summary>
    /// Extracts metadata from a method call expression.
    /// </summary>
    private static Dictionary<string, object> ExtractOperationMetadata(MethodCallExpression node, string methodName)
    {
        var metadata = new Dictionary<string, object>
        {
            ["MethodName"] = methodName,
            ["SourceExpression"] = node.ToString()
        };

        // Extract lambda expression if present
        var lambdaArg = node.Arguments.FirstOrDefault(arg => arg is UnaryExpression { Operand: LambdaExpression });
        if (lambdaArg is UnaryExpression { Operand: LambdaExpression lambda })
        {
            metadata["Lambda"] = lambda;
            metadata["LambdaParameters"] = lambda.Parameters.Count;
            metadata["LambdaBody"] = lambda.Body.ToString();
        }

        // Extract type information
        if (node.Method.ReturnType != null)
        {
            metadata["ReturnType"] = node.Method.ReturnType;
        }

        // Extract generic type arguments
        if (node.Method.IsGenericMethod)
        {
            var genericArgs = node.Method.GetGenericArguments();
            metadata["GenericTypeArguments"] = genericArgs;
            metadata["ElementType"] = genericArgs[0];
        }

        // Special handling for specific operations
        switch (methodName)
        {
            case "GroupBy":
                // Extract key selector and element selector
                if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression keySelector })
                {
                    metadata["KeySelector"] = keySelector;
                }
                break;

            case "Join" or "GroupJoin":
                // Extract key selectors
                if (node.Arguments.Count > 2)
                {
                    if (node.Arguments[2] is UnaryExpression { Operand: LambdaExpression outerKeySelector })
                    {
                        metadata["OuterKeySelector"] = outerKeySelector;
                    }

                    if (node.Arguments.Count > 3 && node.Arguments[3] is UnaryExpression { Operand: LambdaExpression innerKeySelector })
                    {
                        metadata["InnerKeySelector"] = innerKeySelector;
                    }
                }
                break;

            case "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending":
                // Extract key selector
                if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression orderKeySelector })
                {
                    metadata["KeySelector"] = orderKeySelector;
                    metadata["Descending"] = methodName.Contains("Descending", StringComparison.Ordinal);
                }
                break;
        }

        return metadata;
    }

    /// <summary>
    /// Estimates the computational cost of an operation.
    /// </summary>
    private static double EstimateOperationCost(OperationType type, Dictionary<string, object> metadata)
    {
        // Base costs (arbitrary units for comparison)
        return type switch
        {
            OperationType.Map => 1.0,
            OperationType.Filter => 1.5,
            OperationType.Reduce => 3.0,
            OperationType.Scan => 2.0,
            OperationType.Join => 5.0,
            OperationType.GroupBy => 4.0,
            OperationType.OrderBy => 4.5,
            OperationType.Aggregate => 3.5,
            _ => 1.0
        };
    }

    /// <summary>
    /// Detects non-deterministic or unsupported operations in expressions.
    /// </summary>
    private static void DetectNonDeterministicOperations(Expression expression)
    {
        // Check for method calls to non-deterministic functions
        if (expression is MethodCallExpression methodCall)
        {
            var method = methodCall.Method;

            // Check for DateTime.Now, Random, Guid.NewGuid, etc.
            if ((method.DeclaringType == typeof(DateTime) && method.Name == "get_Now") ||
                (method.DeclaringType == typeof(Random) && method.Name == "Next") ||
                (method.DeclaringType == typeof(Guid) && method.Name == "NewGuid"))
            {
                throw new NotSupportedException(
                    $"Non-deterministic operation '{method.Name}' is not supported in GPU kernels. " +
                    $"GPU operations must be deterministic and produce consistent results. " +
                    $"Consider passing random values as input parameters instead.");
            }

            // Check for database-specific operations (e.g., EF Core extensions)
            if (method.DeclaringType?.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true)
            {
                throw new NotSupportedException(
                    $"Entity Framework operation '{method.Name}' cannot be accelerated on GPU. " +
                    $"Only in-memory LINQ operations are supported.");
            }
        }

        // Check member access for non-deterministic properties
        if (expression is MemberExpression memberExpr)
        {
            if (memberExpr.Member.Name == "Now" && memberExpr.Member.DeclaringType == typeof(DateTime))
            {
                throw new NotSupportedException(
                    "DateTime.Now is not supported in GPU kernels. Use a constant timestamp instead.");
            }
        }
    }

    /// <summary>
    /// Generates a unique node identifier.
    /// </summary>
    private string GenerateNodeId()
    {
        return $"op_{_nodeCounter++}";
    }
}
