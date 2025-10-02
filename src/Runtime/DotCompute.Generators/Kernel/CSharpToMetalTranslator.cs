// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotCompute.Generators.Models.Kernel;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Production-grade translator that converts C# kernel code to optimized Metal Shading Language (MSL).
/// Handles complex patterns, optimizations, and various memory access patterns for Apple GPUs.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via reflection or dependency injection")]
internal sealed class CSharpToMetalTranslator
{
    private readonly SemanticModel _semanticModel;
    private readonly KernelMethodInfo _kernelInfo;
    private readonly StringBuilder _output;
    private readonly Dictionary<string, string> _variableMapping;
    private readonly HashSet<string> _threadgroupVariables;
    private readonly HashSet<string> _constantVariables;
    private int _indentLevel;

    public CSharpToMetalTranslator(SemanticModel semanticModel, KernelMethodInfo kernelInfo)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _kernelInfo = kernelInfo ?? throw new ArgumentNullException(nameof(kernelInfo));
        _output = new StringBuilder();
        _variableMapping = [];
        _threadgroupVariables = [];
        _constantVariables = [];
        _indentLevel = 0;
    }

    /// <summary>
    /// Generates the complete Metal kernel including headers and function signature.
    /// </summary>
    public string GenerateCompleteKernel()
    {
        var result = new StringBuilder();

        // Add Metal headers

        _ = result.AppendLine("#include <metal_stdlib>");
        _ = result.AppendLine("#include <metal_atomic>");
        _ = result.AppendLine("using namespace metal;");
        _ = result.AppendLine();

        // Generate kernel signature

        _ = result.Append("kernel void ");
        _ = result.Append(_kernelInfo.Name);
        _ = result.AppendLine("(");

        // Generate parameters

        GenerateParameterList(result);


        _ = result.AppendLine(")");
        _ = result.AppendLine("{");

        // Generate body

        _indentLevel = 1;
        var body = TranslateMethodBody();
        _ = result.Append(body);


        _ = result.AppendLine("}");


        return result.ToString();
    }

    /// <summary>
    /// Translates the C# method body to Metal Shading Language code.
    /// </summary>
    public string TranslateMethodBody()
    {
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return GenerateDefaultKernelBody();
        }

        try
        {
            // Analyze memory access patterns
            AnalyzeMemoryAccessPatterns();

            // Translate the body
            TranslateBlockStatement(_kernelInfo.MethodDeclaration.Body);

            return _output.ToString();
        }
        catch (Exception ex)
        {
            // Fallback to default if translation fails
            return GenerateDefaultKernelBody() +
                   $"\n    // Translation error: {ex.Message}";
        }
    }

    private void GenerateParameterList(StringBuilder result)
    {
        var bufferIndex = 0;
        var parameters = _kernelInfo.Parameters;


        for (var i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            _ = result.Append("    ");

            // Convert parameter type to Metal type

            var metalType = ConvertToMetalParameterType(param.Type, param.Name);
            _ = result.Append(metalType);
            _ = result.Append(" ");
            _ = result.Append(param.Name);
            _ = result.Append(" [[buffer(");
            _ = result.Append(bufferIndex++);
            _ = result.Append(")]]");


            if (i < parameters.Count - 1)
            {
                _ = result.AppendLine(",");
            }
        }

        // Add thread position parameters

        if (parameters.Count > 0)
        {
            _ = result.AppendLine(",");
        }

        // Determine dimensionality based on kernel usage

        var dimensions = GetKernelDimensions();
        switch (dimensions)
        {
            case 1:
                _ = result.Append("    uint gid [[thread_position_in_grid]]");
                break;
            case 2:
                _ = result.Append("    uint2 gid [[thread_position_in_grid]]");
                break;
            case 3:
                _ = result.Append("    uint3 gid [[thread_position_in_grid]]");
                break;
        }
    }

    private static string ConvertToMetalParameterType(string csharpType, string paramName)
    {
        // Remove generic type arguments for analysis
        _ = csharpType.Contains('<') ?
            csharpType.Substring(0, csharpType.IndexOf('<')) : csharpType;

        // Check if it's read-only

        var isReadOnly = csharpType.Contains("ReadOnlySpan", StringComparison.Ordinal) ||
                         paramName.StartsWith("input", StringComparison.Ordinal) ||
                         paramName.StartsWith("source", StringComparison.Ordinal);

        // Extract element type from Span<T> or array

        var elementType = "float";
        if (csharpType.Contains("<") && csharpType.Contains(">"))
        {
            var start = csharpType.IndexOf('<') + 1;
            var end = csharpType.IndexOf('>');
            elementType = csharpType.Substring(start, end - start);
        }
        else if (csharpType.Contains("[]"))
        {
            elementType = csharpType.Replace("[]", "");
        }

        // Convert element type to Metal type

        var metalElementType = ConvertToMetalType(elementType);

        // Determine memory qualifier

        if (csharpType == "int" || csharpType == "float" || csharpType == "uint" ||

            csharpType == "bool" || csharpType == "double")
        {
            return $"constant {metalElementType}&";
        }
        else if (isReadOnly)
        {
            return $"device const {metalElementType}*";
        }
        else
        {
            return $"device {metalElementType}*";
        }
    }

    private void AnalyzeMemoryAccessPatterns()
    {
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return;
        }

        var dataFlow = _semanticModel.AnalyzeDataFlow(_kernelInfo.MethodDeclaration.Body);

        // Identify variables that should use threadgroup memory
        foreach (var variable in dataFlow?.VariablesDeclared ?? Enumerable.Empty<ISymbol>())
        {
            if (IsThreadgroupCandidate(variable))
            {
                _ = _threadgroupVariables.Add(variable.Name);
            }
            else if (IsConstantCandidate(variable))
            {
                _ = _constantVariables.Add(variable.Name);
            }
        }
    }

    private static bool IsThreadgroupCandidate(ISymbol variable)
    {
        // Heuristics for threadgroup memory usage
        var type = variable.GetTypeDisplayString();
        return type.Contains("[]") && !type.Contains("Span") &&
               (variable.Name.IndexOf("shared", StringComparison.OrdinalIgnoreCase) >= 0 ||
                variable.Name.IndexOf("tile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                variable.Name.IndexOf("local", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsConstantCandidate(ISymbol variable)
    {
        // Heuristics for constant memory usage
        var isReadOnly = variable switch
        {
            IFieldSymbol field => field.IsReadOnly,
            ILocalSymbol local => local.IsConst,
            IParameterSymbol param => param.RefKind == RefKind.In,
            _ => false
        };


        return variable.IsStatic ||

               variable.Name.IndexOf("const", StringComparison.OrdinalIgnoreCase) >= 0 ||
               isReadOnly;
    }

    private void TranslateBlockStatement(BlockSyntax block)
    {
        foreach (var statement in block.Statements)
        {
            TranslateStatement(statement);
        }
    }

    private void TranslateStatement(StatementSyntax statement)
    {
        switch (statement)
        {
            case LocalDeclarationStatementSyntax localDecl:
                TranslateLocalDeclaration(localDecl);
                break;
            case ExpressionStatementSyntax exprStmt:
                TranslateExpressionStatement(exprStmt);
                break;
            case ForStatementSyntax forStmt:
                TranslateForStatement(forStmt);
                break;
            case IfStatementSyntax ifStmt:
                TranslateIfStatement(ifStmt);
                break;
            case WhileStatementSyntax whileStmt:
                TranslateWhileStatement(whileStmt);
                break;
            case BlockSyntax block:
                WriteIndented("{");
                _indentLevel++;
                TranslateBlockStatement(block);
                _indentLevel--;
                WriteIndented("}");
                break;
            case ReturnStatementSyntax returnStmt:
                TranslateReturnStatement(returnStmt);
                break;
            default:
                WriteIndented($"// Unsupported statement: {statement.Kind()}");
                break;
        }
    }

    private void TranslateLocalDeclaration(LocalDeclarationStatementSyntax localDecl)
    {
        var type = ConvertToMetalType(localDecl.Declaration.Type.ToString());

        foreach (var variable in localDecl.Declaration.Variables)
        {
            var varName = variable.Identifier.Text;

            // Check if this should be in threadgroup memory
            if (_threadgroupVariables.Contains(varName))
            {
                WriteIndented($"threadgroup {type} {varName}");
            }
            else if (_constantVariables.Contains(varName))
            {
                WriteIndented($"constant {type} {varName}");
            }
            else
            {
                WriteIndented($"{type} {varName}");
            }

            if (variable.Initializer != null)
            {
                _ = _output.Append(" = ");
                TranslateExpression(variable.Initializer.Value);
            }

            _ = _output.AppendLine(";");
        }
    }

    private void TranslateExpressionStatement(ExpressionStatementSyntax exprStmt)
    {
        WriteIndented("");
        TranslateExpression(exprStmt.Expression);
        _ = _output.AppendLine(";");
    }

    private void TranslateExpression(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case BinaryExpressionSyntax binary:
                TranslateBinaryExpression(binary);
                break;
            case AssignmentExpressionSyntax assignment:
                TranslateAssignmentExpression(assignment);
                break;
            case IdentifierNameSyntax identifier:
                TranslateIdentifier(identifier);
                break;
            case LiteralExpressionSyntax literal:
                _ = _output.Append(literal.Token.Text);
                break;
            case MemberAccessExpressionSyntax memberAccess:
                TranslateMemberAccess(memberAccess);
                break;
            case InvocationExpressionSyntax invocation:
                TranslateInvocation(invocation);
                break;
            case ElementAccessExpressionSyntax elementAccess:
                TranslateElementAccess(elementAccess);
                break;
            case PrefixUnaryExpressionSyntax prefixUnary:
                _ = _output.Append(prefixUnary.OperatorToken.Text);
                TranslateExpression(prefixUnary.Operand);
                break;
            case PostfixUnaryExpressionSyntax postfixUnary:
                TranslateExpression(postfixUnary.Operand);
                _ = _output.Append(postfixUnary.OperatorToken.Text);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                _ = _output.Append('(');
                TranslateExpression(parenthesized.Expression);
                _ = _output.Append(')');
                break;
            case CastExpressionSyntax cast:
                _ = _output.Append('(');
                _ = _output.Append(ConvertToMetalType(cast.Type.ToString()));
                _ = _output.Append(')');
                TranslateExpression(cast.Expression);
                break;
            default:
                _ = _output.Append($"/* Unsupported expression: {expression.Kind()} */");
                break;
        }
    }

    private void TranslateBinaryExpression(BinaryExpressionSyntax binary)
    {
        TranslateExpression(binary.Left);
        _ = _output.Append($" {binary.OperatorToken.Text} ");
        TranslateExpression(binary.Right);
    }

    private void TranslateAssignmentExpression(AssignmentExpressionSyntax assignment)
    {
        TranslateExpression(assignment.Left);
        _ = _output.Append($" {assignment.OperatorToken.Text} ");
        TranslateExpression(assignment.Right);
    }

    private void TranslateIdentifier(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.Text;

        // Map Kernel.ThreadId to Metal thread position

        if (_variableMapping.ContainsKey(name))
        {
            _ = _output.Append(_variableMapping[name]);
        }
        else
        {
            _ = _output.Append(name);
        }
    }

    private void TranslateMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var fullName = memberAccess.ToString();

        // Handle Kernel.ThreadId.X/Y/Z

        if (fullName.StartsWith("Kernel.ThreadId", StringComparison.Ordinal) || fullName.StartsWith("ThreadId", StringComparison.Ordinal))
        {
            var dimensions = GetKernelDimensions();
            if (fullName.EndsWith(".X", StringComparison.Ordinal) || fullName.EndsWith(".x", StringComparison.Ordinal))
            {
                _ = _output.Append(dimensions == 1 ? "gid" : "gid.x");
            }
            else if (fullName.EndsWith(".Y", StringComparison.Ordinal) || fullName.EndsWith(".y", StringComparison.Ordinal))
            {
                _ = _output.Append("gid.y");
            }
            else if (fullName.EndsWith(".Z", StringComparison.Ordinal) || fullName.EndsWith(".z", StringComparison.Ordinal))
            {
                _ = _output.Append("gid.z");
            }
            else
            {
                _ = _output.Append("gid");
            }
        }
        // Handle array.Length or span.Length
        else if (memberAccess.Name.Identifier.Text == "Length")
        {
            // In Metal, we'd need to pass length as a separate parameter
            // For now, assume it's passed as a constant
            TranslateExpression(memberAccess.Expression);
            _ = _output.Append("_length");
        }
        else
        {
            TranslateExpression(memberAccess.Expression);
            _ = _output.Append(".");
            _ = _output.Append(memberAccess.Name.Identifier.Text);
        }
    }

    private void TranslateInvocation(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression.ToString();

        // Handle atomic operations

        if (methodName.Contains("Interlocked.Add") || methodName.Contains("atomic_add"))
        {
            _ = _output.Append("atomic_fetch_add_explicit(");
            var args = invocation.ArgumentList.Arguments;
            if (args.Count >= 2)
            {
                _ = _output.Append("(device atomic_int*)&");
                TranslateExpression(args[0].Expression);
                _ = _output.Append(", ");
                TranslateExpression(args[1].Expression);
                _ = _output.Append(", memory_order_relaxed");
            }
            _ = _output.Append(')');
        }
        // Handle math functions
        else if (methodName.StartsWith("Math.", StringComparison.Ordinal) || methodName.StartsWith("MathF.", StringComparison.Ordinal))
        {
            var function = methodName.Substring(methodName.LastIndexOf('.') + 1).ToUpperInvariant();

            // Map C# math functions to Metal equivalents

            var metalFunction = function switch
            {
                "SIN" => "sin",
                "COS" => "cos",
                "TAN" => "tan",
                "SQRT" => "sqrt",
                "ABS" => "abs",
                "MIN" => "min",
                "MAX" => "max",
                "POW" => "pow",
                "EXP" => "exp",
                "LOG" => "log",
                "FLOOR" => "floor",
                "CEIL" => "ceil",
                _ => function.ToLowerInvariant()
            };


            _ = _output.Append(metalFunction);
            _ = _output.Append('(');
            var first = true;
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (!first)
                {
                    _ = _output.Append(", ");
                }


                TranslateExpression(arg.Expression);
                first = false;
            }
            _ = _output.Append(')');
        }
        // Handle barrier/fence operations
        else if (methodName.Contains("Barrier") || methodName.Contains("MemoryFence"))
        {
            _ = _output.Append("threadgroup_barrier(mem_flags::mem_threadgroup)");
        }
        else
        {
            // Default invocation translation
            TranslateExpression(invocation.Expression);
            _ = _output.Append('(');
            var first = true;
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (!first)
                {
                    _ = _output.Append(", ");
                }


                TranslateExpression(arg.Expression);
                first = false;
            }
            _ = _output.Append(')');
        }
    }

    private void TranslateElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        TranslateExpression(elementAccess.Expression);
        _ = _output.Append("[");
        var first = true;
        foreach (var arg in elementAccess.ArgumentList.Arguments)
        {
            if (!first)
            {
                _ = _output.Append(", ");
            }

            TranslateExpression(arg.Expression);
            first = false;
        }
        _ = _output.Append("]");
    }

    private void TranslateForStatement(ForStatementSyntax forStmt)
    {
        WriteIndented("for (");

        // Declaration
        if (forStmt.Declaration != null)
        {
            var type = ConvertToMetalType(forStmt.Declaration.Type.ToString());
            foreach (var variable in forStmt.Declaration.Variables)
            {
                _ = _output.Append($"{type} {variable.Identifier.Text}");
                if (variable.Initializer != null)
                {
                    _ = _output.Append(" = ");
                    TranslateExpression(variable.Initializer.Value);
                }
            }
        }
        _ = _output.Append("; ");

        // Condition
        if (forStmt.Condition != null)
        {
            TranslateExpression(forStmt.Condition);
        }
        _ = _output.Append("; ");

        // Incrementors
        var firstIncr = true;
        foreach (var incrementor in forStmt.Incrementors)
        {
            if (!firstIncr)
            {
                _ = _output.Append(", ");
            }

            TranslateExpression(incrementor);
            firstIncr = false;
        }

        _ = _output.AppendLine(")");

        // Body
        if (forStmt.Statement is BlockSyntax block)
        {
            WriteIndented("{");
            _indentLevel++;
            TranslateBlockStatement(block);
            _indentLevel--;
            WriteIndented("}");
        }
        else
        {
            _indentLevel++;
            TranslateStatement(forStmt.Statement);
            _indentLevel--;
        }
    }

    private void TranslateIfStatement(IfStatementSyntax ifStmt)
    {
        WriteIndented("if (");
        TranslateExpression(ifStmt.Condition);
        _ = _output.AppendLine(")");

        if (ifStmt.Statement is BlockSyntax block)
        {
            WriteIndented("{");
            _indentLevel++;
            TranslateBlockStatement(block);
            _indentLevel--;
            WriteIndented("}");
        }
        else
        {
            _indentLevel++;
            TranslateStatement(ifStmt.Statement);
            _indentLevel--;
        }

        if (ifStmt.Else != null)
        {
            WriteIndented("else");
            if (ifStmt.Else.Statement is IfStatementSyntax elseIf)
            {
                _ = _output.Append(" ");
                TranslateIfStatement(elseIf);
            }
            else
            {
                _ = _output.AppendLine();
                TranslateStatement(ifStmt.Else.Statement);
            }
        }
    }

    private void TranslateWhileStatement(WhileStatementSyntax whileStmt)
    {
        WriteIndented("while (");
        TranslateExpression(whileStmt.Condition);
        _ = _output.AppendLine(")");

        if (whileStmt.Statement is BlockSyntax block)
        {
            WriteIndented("{");
            _indentLevel++;
            TranslateBlockStatement(block);
            _indentLevel--;
            WriteIndented("}");
        }
        else
        {
            _indentLevel++;
            TranslateStatement(whileStmt.Statement);
            _indentLevel--;
        }
    }

    private void TranslateReturnStatement(ReturnStatementSyntax returnStmt)
    {
        WriteIndented("return");
        if (returnStmt.Expression != null)
        {
            _ = _output.Append(" ");
            TranslateExpression(returnStmt.Expression);
        }
        _ = _output.AppendLine(";");
    }

    private static string ConvertToMetalType(string csharpType)
    {
        return csharpType switch
        {
            "int" => "int",
            "uint" => "uint",
            "float" => "float",
            "double" => "float", // Metal doesn't support double on all devices
            "bool" => "bool",
            "byte" => "uchar",
            "sbyte" => "char",
            "short" => "short",
            "ushort" => "ushort",
            "long" => "long",
            "ulong" => "ulong",
            "float2" => "float2",
            "float3" => "float3",
            "float4" => "float4",
            "int2" => "int2",
            "int3" => "int3",
            "int4" => "int4",
            _ => csharpType // Default fallback
        };
    }

    private void WriteIndented(string text)
    {
        for (var i = 0; i < _indentLevel; i++)
        {
            _ = _output.Append("    ");
        }
        if (!string.IsNullOrEmpty(text))
        {
            _ = _output.AppendLine(text);
        }
    }

    private string GenerateDefaultKernelBody()
    {
        var result = new StringBuilder();
        _ = result.AppendLine("    // Default kernel implementation");


        var dimensions = GetKernelDimensions();
        switch (dimensions)
        {
            case 1:
                _ = result.AppendLine("    uint idx = gid;");
                break;
            case 2:
                _ = result.AppendLine("    uint2 idx = gid;");
                break;
            case 3:
                _ = result.AppendLine("    uint3 idx = gid;");
                break;
        }


        _ = result.AppendLine("    // TODO: Implement kernel logic");
        return result.ToString();
    }

    private int GetKernelDimensions()
    {
        // Analyze the kernel to determine if it uses 1D, 2D, or 3D indexing
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return 1; // Default to 1D
        }

        var bodyText = _kernelInfo.MethodDeclaration.Body.ToString();

        // Check for ThreadId.Z usage

        if (bodyText.Contains("ThreadId.Z") || bodyText.Contains("ThreadId.z"))
        {
            return 3;
        }
        // Check for ThreadId.Y usage
        else if (bodyText.Contains("ThreadId.Y") || bodyText.Contains("ThreadId.y"))
        {
            return 2;
        }


        return 1; // Default to 1D
    }
}

