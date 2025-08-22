// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Expression visitor that analyzes GPU compatibility.
/// </summary>
/// <remarks>
/// This visitor traverses LINQ expression trees to determine whether they can
/// be executed on the GPU, analyzing supported operations and estimating complexity.
/// </remarks>
internal sealed class GPUCompatibilityVisitor : ExpressionVisitor
{
    private readonly ExpressionAnalysis _analysis = new() { CanExecuteOnGPU = true };
    private readonly List<Type> _inputTypes = [];
    private int _complexity;

    /// <summary>
    /// Gets the analysis result for the visited expression.
    /// </summary>
    /// <returns>
    /// An <see cref="ExpressionAnalysis"/> containing compatibility information
    /// and complexity estimates for the analyzed expression.
    /// </returns>
    public ExpressionAnalysis GetAnalysis()
    {
        _analysis.EstimatedComplexity = _complexity;
        _analysis.InputTypes = [.. _inputTypes];
        return _analysis;
    }

    /// <summary>
    /// Visits method call expressions to check GPU compatibility.
    /// </summary>
    /// <param name="node">The method call expression to analyze.</param>
    /// <returns>The potentially modified expression.</returns>
    /// <remarks>
    /// This method identifies LINQ operations and determines their GPU compatibility,
    /// setting appropriate operation types and incompatibility reasons.
    /// </remarks>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _complexity++;

        // Check for LINQ methods
        if (node.Method.DeclaringType == typeof(Enumerable) || node.Method.DeclaringType == typeof(Queryable))
        {
            switch (node.Method.Name)
            {
                case "Select":
                    _analysis.OperationType = "Map";
                    break;
                case "Where":
                    _analysis.OperationType = "Filter";
                    break;
                case "Sum":
                case "Average":
                case "Min":
                case "Max":
                    _analysis.OperationType = "Reduce";
                    break;
                default:
                    _analysis.CanExecuteOnGPU = false;
                    _analysis.Reason = $"LINQ method '{node.Method.Name}' not supported on GPU";
                    break;
            }
        }

        return base.VisitMethodCall(node);
    }

    /// <summary>
    /// Visits binary expressions to check GPU compatibility.
    /// </summary>
    /// <param name="node">The binary expression to analyze.</param>
    /// <returns>The potentially modified expression.</returns>
    /// <remarks>
    /// This method validates that binary operations are supported on the GPU,
    /// marking unsupported operations as incompatible.
    /// </remarks>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        _complexity++;

        // All basic binary operations are supported
        switch (node.NodeType)
        {
            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
            case ExpressionType.Divide:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
                break;
            default:
                _analysis.CanExecuteOnGPU = false;
                _analysis.Reason = $"Binary operation '{node.NodeType}' not supported on GPU";
                break;
        }

        return base.VisitBinary(node);
    }

    /// <summary>
    /// Visits constant expressions to extract input type information.
    /// </summary>
    /// <param name="node">The constant expression to analyze.</param>
    /// <returns>The potentially modified expression.</returns>
    /// <remarks>
    /// This method identifies array constants and adds their types to the input types collection
    /// for use in kernel generation and memory management.
    /// </remarks>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value != null)
        {
            var type = node.Value.GetType();
            if (type.IsArray)
            {
                _inputTypes.Add(type);
            }
        }

        return base.VisitConstant(node);
    }

    /// <summary>
    /// Visits lambda expressions to extract output type information.
    /// </summary>
    /// <typeparam name="T">The delegate type of the lambda expression.</typeparam>
    /// <param name="node">The lambda expression to analyze.</param>
    /// <returns>The potentially modified expression.</returns>
    /// <remarks>
    /// This method captures the return type of lambda expressions for use in
    /// kernel generation and result type inference.
    /// </remarks>
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _analysis.OutputType = node.ReturnType;
        return base.VisitLambda(node);
    }
}