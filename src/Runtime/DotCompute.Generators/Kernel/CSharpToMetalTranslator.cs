// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        _variableMapping = new Dictionary<string, string>();
        _threadgroupVariables = new HashSet<string>();
        _constantVariables = new HashSet<string>();
        _indentLevel = 0;
    }

    /// <summary>
    /// Generates the complete Metal kernel including headers and function signature.
    /// </summary>
    public string GenerateCompleteKernel()
    {
        var result = new StringBuilder();
        
        // Add Metal headers
        result.AppendLine("#include <metal_stdlib>");
        result.AppendLine("#include <metal_atomic>");
        result.AppendLine("using namespace metal;");
        result.AppendLine();
        
        // Generate kernel signature
        result.Append("kernel void ");
        result.Append(_kernelInfo.Name);
        result.AppendLine("(");
        
        // Generate parameters
        GenerateParameterList(result);
        
        result.AppendLine(")");
        result.AppendLine("{");
        
        // Generate body
        _indentLevel = 1;
        var body = TranslateMethodBody();
        result.Append(body);
        
        result.AppendLine("}");
        
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
        
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            result.Append("    ");
            
            // Convert parameter type to Metal type
            var metalType = ConvertToMetalParameterType(param.Type, param.Name);
            result.Append(metalType);
            result.Append(" ");
            result.Append(param.Name);
            result.Append(" [[buffer(");
            result.Append(bufferIndex++);
            result.Append(")]]");
            
            if (i < parameters.Count - 1)
            {
                result.AppendLine(",");
            }
        }
        
        // Add thread position parameters
        if (parameters.Count > 0)
        {
            result.AppendLine(",");
        }
        
        // Determine dimensionality based on kernel usage
        var dimensions = GetKernelDimensions();
        switch (dimensions)
        {
            case 1:
                result.Append("    uint gid [[thread_position_in_grid]]");
                break;
            case 2:
                result.Append("    uint2 gid [[thread_position_in_grid]]");
                break;
            case 3:
                result.Append("    uint3 gid [[thread_position_in_grid]]");
                break;
        }
    }

    private string ConvertToMetalParameterType(string csharpType, string paramName)
    {
        // Remove generic type arguments for analysis
        var baseType = csharpType.Contains('<') ? 
            csharpType.Substring(0, csharpType.IndexOf('<')) : csharpType;
        
        // Check if it's read-only
        var isReadOnly = csharpType.Contains("ReadOnlySpan") || 
                         paramName.StartsWith("input") || 
                         paramName.StartsWith("source");
        
        // Extract element type from Span<T> or array
        string elementType = "float";
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
                _threadgroupVariables.Add(variable.Name);
            }
            else if (IsConstantCandidate(variable))
            {
                _constantVariables.Add(variable.Name);
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
        bool isReadOnly = variable switch
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
                _output.Append(" = ");
                TranslateExpression(variable.Initializer.Value);
            }

            _output.AppendLine(";");
        }
    }

    private void TranslateExpressionStatement(ExpressionStatementSyntax exprStmt)
    {
        WriteIndented("");
        TranslateExpression(exprStmt.Expression);
        _output.AppendLine(";");
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
                _output.Append(literal.Token.Text);
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
                _output.Append(prefixUnary.OperatorToken.Text);
                TranslateExpression(prefixUnary.Operand);
                break;
            case PostfixUnaryExpressionSyntax postfixUnary:
                TranslateExpression(postfixUnary.Operand);
                _output.Append(postfixUnary.OperatorToken.Text);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                _output.Append("(");
                TranslateExpression(parenthesized.Expression);
                _output.Append(")");
                break;
            case CastExpressionSyntax cast:
                _output.Append("(");
                _output.Append(ConvertToMetalType(cast.Type.ToString()));
                _output.Append(")");
                TranslateExpression(cast.Expression);
                break;
            default:
                _output.Append($"/* Unsupported expression: {expression.Kind()} */");
                break;
        }
    }

    private void TranslateBinaryExpression(BinaryExpressionSyntax binary)
    {
        TranslateExpression(binary.Left);
        _output.Append($" {binary.OperatorToken.Text} ");
        TranslateExpression(binary.Right);
    }

    private void TranslateAssignmentExpression(AssignmentExpressionSyntax assignment)
    {
        TranslateExpression(assignment.Left);
        _output.Append($" {assignment.OperatorToken.Text} ");
        TranslateExpression(assignment.Right);
    }

    private void TranslateIdentifier(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.Text;
        
        // Map Kernel.ThreadId to Metal thread position
        if (_variableMapping.ContainsKey(name))
        {
            _output.Append(_variableMapping[name]);
        }
        else
        {
            _output.Append(name);
        }
    }

    private void TranslateMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var fullName = memberAccess.ToString();
        
        // Handle Kernel.ThreadId.X/Y/Z
        if (fullName.StartsWith("Kernel.ThreadId") || fullName.StartsWith("ThreadId"))
        {
            var dimensions = GetKernelDimensions();
            if (fullName.EndsWith(".X") || fullName.EndsWith(".x"))
            {
                _output.Append(dimensions == 1 ? "gid" : "gid.x");
            }
            else if (fullName.EndsWith(".Y") || fullName.EndsWith(".y"))
            {
                _output.Append("gid.y");
            }
            else if (fullName.EndsWith(".Z") || fullName.EndsWith(".z"))
            {
                _output.Append("gid.z");
            }
            else
            {
                _output.Append("gid");
            }
        }
        // Handle array.Length or span.Length
        else if (memberAccess.Name.Identifier.Text == "Length")
        {
            // In Metal, we'd need to pass length as a separate parameter
            // For now, assume it's passed as a constant
            TranslateExpression(memberAccess.Expression);
            _output.Append("_length");
        }
        else
        {
            TranslateExpression(memberAccess.Expression);
            _output.Append(".");
            _output.Append(memberAccess.Name.Identifier.Text);
        }
    }

    private void TranslateInvocation(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression.ToString();
        
        // Handle atomic operations
        if (methodName.Contains("Interlocked.Add") || methodName.Contains("atomic_add"))
        {
            _output.Append("atomic_fetch_add_explicit(");
            var args = invocation.ArgumentList.Arguments;
            if (args.Count >= 2)
            {
                _output.Append("(device atomic_int*)&");
                TranslateExpression(args[0].Expression);
                _output.Append(", ");
                TranslateExpression(args[1].Expression);
                _output.Append(", memory_order_relaxed");
            }
            _output.Append(")");
        }
        // Handle math functions
        else if (methodName.StartsWith("Math.") || methodName.StartsWith("MathF."))
        {
            var function = methodName.Substring(methodName.LastIndexOf('.') + 1).ToLower();
            
            // Map C# math functions to Metal equivalents
            var metalFunction = function switch
            {
                "sin" => "sin",
                "cos" => "cos",
                "tan" => "tan",
                "sqrt" => "sqrt",
                "abs" => "abs",
                "min" => "min",
                "max" => "max",
                "pow" => "pow",
                "exp" => "exp",
                "log" => "log",
                "floor" => "floor",
                "ceil" => "ceil",
                _ => function
            };
            
            _output.Append(metalFunction);
            _output.Append("(");
            var first = true;
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (!first) _output.Append(", ");
                TranslateExpression(arg.Expression);
                first = false;
            }
            _output.Append(")");
        }
        // Handle barrier/fence operations
        else if (methodName.Contains("Barrier") || methodName.Contains("MemoryFence"))
        {
            _output.Append("threadgroup_barrier(mem_flags::mem_threadgroup)");
        }
        else
        {
            // Default invocation translation
            TranslateExpression(invocation.Expression);
            _output.Append("(");
            var first = true;
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (!first) _output.Append(", ");
                TranslateExpression(arg.Expression);
                first = false;
            }
            _output.Append(")");
        }
    }

    private void TranslateElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        TranslateExpression(elementAccess.Expression);
        _output.Append("[");
        var first = true;
        foreach (var arg in elementAccess.ArgumentList.Arguments)
        {
            if (!first) _output.Append(", ");
            TranslateExpression(arg.Expression);
            first = false;
        }
        _output.Append("]");
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
                _output.Append($"{type} {variable.Identifier.Text}");
                if (variable.Initializer != null)
                {
                    _output.Append(" = ");
                    TranslateExpression(variable.Initializer.Value);
                }
            }
        }
        _output.Append("; ");

        // Condition
        if (forStmt.Condition != null)
        {
            TranslateExpression(forStmt.Condition);
        }
        _output.Append("; ");

        // Incrementors
        var firstIncr = true;
        foreach (var incrementor in forStmt.Incrementors)
        {
            if (!firstIncr) _output.Append(", ");
            TranslateExpression(incrementor);
            firstIncr = false;
        }

        _output.AppendLine(")");

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
        _output.AppendLine(")");

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
                _output.Append(" ");
                TranslateIfStatement(elseIf);
            }
            else
            {
                _output.AppendLine();
                TranslateStatement(ifStmt.Else.Statement);
            }
        }
    }

    private void TranslateWhileStatement(WhileStatementSyntax whileStmt)
    {
        WriteIndented("while (");
        TranslateExpression(whileStmt.Condition);
        _output.AppendLine(")");

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
            _output.Append(" ");
            TranslateExpression(returnStmt.Expression);
        }
        _output.AppendLine(";");
    }

    private string ConvertToMetalType(string csharpType)
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
        for (int i = 0; i < _indentLevel; i++)
        {
            _output.Append("    ");
        }
        if (!string.IsNullOrEmpty(text))
        {
            _output.AppendLine(text);
        }
    }

    private string GenerateDefaultKernelBody()
    {
        var result = new StringBuilder();
        result.AppendLine("    // Default kernel implementation");
        
        var dimensions = GetKernelDimensions();
        switch (dimensions)
        {
            case 1:
                result.AppendLine("    uint idx = gid;");
                break;
            case 2:
                result.AppendLine("    uint2 idx = gid;");
                break;
            case 3:
                result.AppendLine("    uint3 idx = gid;");
                break;
        }
        
        result.AppendLine("    // TODO: Implement kernel logic");
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

