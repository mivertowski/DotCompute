// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Provides functionality for computing deterministic hashes of expression trees.
/// </summary>
public static class ExpressionHasher
{
    /// <summary>
    /// Computes a deterministic hash of an expression tree.
    /// </summary>
    /// <param name="expression">The expression to hash.</param>
    /// <returns>A string hash of the expression.</returns>
    public static string ComputeHash(Expression expression)
    {
        if (expression == null)
        {

            throw new ArgumentNullException(nameof(expression));
        }


        var visitor = new ExpressionStringBuilder();
        var expressionString = visitor.Visit(expression).ToString();

        // Use SHA256 to create a deterministic hash

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(expressionString));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Computes a short hash (first 16 characters) of an expression tree.
    /// </summary>
    /// <param name="expression">The expression to hash.</param>
    /// <returns>A short string hash of the expression.</returns>
    public static string ComputeShortHash(Expression expression)
    {
        var fullHash = ComputeHash(expression);
        return fullHash[..Math.Min(16, fullHash.Length)];
    }
}

/// <summary>
/// Expression visitor that builds a string representation for hashing.
/// </summary>
internal class ExpressionStringBuilder : ExpressionVisitor
{
    private readonly StringBuilder _builder = new();

    /// <summary>
    /// Returns the string representation of the visited expression.
    /// </summary>
    /// <returns>String representation of the expression tree.</returns>
    public override string ToString() => _builder.ToString();

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _builder.Append($"({node.NodeType}:");
        Visit(node.Left);
        _builder.Append(',');
        Visit(node.Right);
        _builder.Append(')');
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        _builder.Append($"({node.NodeType}:");
        Visit(node.Operand);
        _builder.Append(')');
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _builder.Append($"(Const:{node.Value}:{node.Type.Name})");
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _builder.Append($"(Param:{node.Name}:{node.Type.Name})");
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        _builder.Append($"(Member:{node.Member.Name}:");
        Visit(node.Expression);
        _builder.Append(')');
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _builder.Append($"(Call:{node.Method.Name}:{node.Method.DeclaringType?.Name}");


        if (node.Object != null)
        {
            _builder.Append(':');
            Visit(node.Object);
        }

        foreach (var arg in node.Arguments)
        {
            _builder.Append(':');
            Visit(arg);
        }

        _builder.Append(')');
        return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _builder.Append($"(Lambda:{typeof(T).Name}");


        foreach (var param in node.Parameters)
        {
            _builder.Append(':');
            Visit(param);
        }

        _builder.Append(':');
        Visit(node.Body);
        _builder.Append(')');
        return node;
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        _builder.Append("(Conditional:");
        Visit(node.Test);
        _builder.Append(':');
        Visit(node.IfTrue);
        _builder.Append(':');
        Visit(node.IfFalse);
        _builder.Append(')');
        return node;
    }

    protected override Expression VisitNew(NewExpression node)
    {
        _builder.Append($"(New:{node.Constructor?.DeclaringType?.Name}");


        foreach (var arg in node.Arguments)
        {
            _builder.Append(':');
            Visit(arg);
        }

        _builder.Append(')');
        return node;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        _builder.Append($"(NewArray:{node.NodeType}:{node.Type.GetElementType()?.Name}");


        foreach (var expr in node.Expressions)
        {
            _builder.Append(':');
            Visit(expr);
        }

        _builder.Append(')');
        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        _builder.Append("(MemberInit:");
        Visit(node.NewExpression);


        foreach (var binding in node.Bindings)
        {
            _builder.Append($":{binding.Member.Name}={binding.BindingType}");
        }

        _builder.Append(')');
        return node;
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
        _builder.Append("(ListInit:");
        Visit(node.NewExpression);


        foreach (var initializer in node.Initializers)
        {
            _builder.Append($":{initializer.AddMethod?.Name}");
            foreach (var arg in initializer.Arguments)
            {
                _builder.Append(':');
                Visit(arg);
            }
        }

        _builder.Append(')');
        return node;
    }

    public override Expression Visit(Expression? node)
    {
        if (node == null)
        {
            _builder.Append("(null)");
            return node!;
        }

        return base.Visit(node);
    }
}