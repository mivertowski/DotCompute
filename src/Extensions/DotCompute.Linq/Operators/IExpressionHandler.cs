// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;

namespace DotCompute.Linq.Operators
{

/// <summary>
/// Interface for handling specific types of expressions during kernel generation.
/// </summary>
public interface IExpressionHandler
{
    /// <summary>
    /// Handles the given expression and generates appropriate kernel code.
    /// </summary>
    /// <param name="expression">The expression to handle.</param>
    /// <param name="context">The generation context.</param>
    /// <returns>The generated kernel code fragment.</returns>
    string Handle(Expression expression, KernelGenerationContext context);
    
    /// <summary>
    /// Determines if this handler can process the given expression.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <returns>True if the handler can process the expression; otherwise, false.</returns>
    bool CanHandle(Expression expression);
}

/// <summary>
/// Basic expression handler for method calls.
/// </summary>
internal class MethodCallExpressionHandler : IExpressionHandler
{
    public bool CanHandle(Expression expression)
    {
        return expression.NodeType == ExpressionType.Call;
    }

    public string Handle(Expression expression, KernelGenerationContext context)
    {
        if (expression is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            
            return methodName switch
            {
                "Select" => "// Map operation kernel code here",
                "Where" => "// Filter operation kernel code here",
                "Sum" => "// Reduction operation kernel code here",
                _ => "// Generic method call kernel code here"
            };
        }
        
        return "// Unsupported method call";
    }
}

/// <summary>
/// Binary expression handler for arithmetic operations.
/// </summary>
internal class BinaryExpressionHandler : IExpressionHandler
{
    public bool CanHandle(Expression expression)
    {
        return expression.NodeType is ExpressionType.Add or ExpressionType.Subtract 
                                   or ExpressionType.Multiply or ExpressionType.Divide
                                   or ExpressionType.GreaterThan or ExpressionType.LessThan
                                   or ExpressionType.Equal or ExpressionType.NotEqual;
    }

    public string Handle(Expression expression, KernelGenerationContext context)
    {
        if (expression is BinaryExpression binaryExpr)
        {
            var op = binaryExpr.NodeType switch
            {
                ExpressionType.Add => "+",
                ExpressionType.Subtract => "-",
                ExpressionType.Multiply => "*",
                ExpressionType.Divide => "/",
                ExpressionType.GreaterThan => ">",
                ExpressionType.LessThan => "<",
                ExpressionType.Equal => "==",
                ExpressionType.NotEqual => "!=",
                _ => "?"
            };
            
            return $"left {op} right";
        }
        
        return "// Unsupported binary operation";
    }
}}
