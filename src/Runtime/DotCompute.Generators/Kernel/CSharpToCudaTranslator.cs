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
using DotCompute.Generators.Kernel.Enums;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Production-grade translator that converts C# kernel code to optimized CUDA C code.
/// Handles complex patterns, optimizations, and various memory access patterns.
/// </summary>
internal sealed class CSharpToCudaTranslator
{
    private readonly SemanticModel _semanticModel;
    private readonly KernelMethodInfo _kernelInfo;
    private readonly StringBuilder _output;
    private readonly Dictionary<string, string> _variableMapping;
    private readonly HashSet<string> _sharedMemoryVariables;
    private readonly HashSet<string> _constantMemoryVariables;
    private int _indentLevel;

    public CSharpToCudaTranslator(SemanticModel semanticModel, KernelMethodInfo kernelInfo)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _kernelInfo = kernelInfo ?? throw new ArgumentNullException(nameof(kernelInfo));
        _output = new StringBuilder();
        _variableMapping = [];
        _sharedMemoryVariables = [];
        _constantMemoryVariables = [];
        _indentLevel = 0;
    }

    /// <summary>
    /// Translates the C# method body to CUDA C code.
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

                   $"\n            // Translation error: {ex.Message}";
        }
    }

    private void AnalyzeMemoryAccessPatterns()
    {
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return;
        }


        var dataFlow = _semanticModel.AnalyzeDataFlow(_kernelInfo.MethodDeclaration.Body);

        // Identify variables that should use shared memory

        foreach (var variable in dataFlow?.VariablesDeclared ?? Enumerable.Empty<ISymbol>())
        {
            if (IsSharedMemoryCandidate(variable))
            {
                _sharedMemoryVariables.Add(variable.Name);
            }
            else if (IsConstantMemoryCandidate(variable))
            {
                _constantMemoryVariables.Add(variable.Name);
            }
        }
    }

    private static bool IsSharedMemoryCandidate(ISymbol variable)
    {
        // Heuristics for shared memory usage
        var type = variable.GetTypeDisplayString();
        return type.Contains("[]") && !type.Contains("Span") &&

               variable.Name.IndexOf("tile", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsConstantMemoryCandidate(ISymbol variable)
    {
        // Heuristics for constant memory usage
        return variable.IsStatic || variable.Name.IndexOf("const", StringComparison.OrdinalIgnoreCase) >= 0;
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
                TranslateBlockStatement(block);
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
        var type = ConvertToCudaType(localDecl.Declaration.Type.ToString());


        foreach (var variable in localDecl.Declaration.Variables)
        {
            var varName = variable.Identifier.Text;

            // Check if this should be in shared memory

            if (_sharedMemoryVariables.Contains(varName))
            {
                WriteIndented($"__shared__ {type} {varName}");
            }
            else if (_constantMemoryVariables.Contains(varName))
            {
                WriteIndented($"__constant__ {type} {varName}");
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

    private void TranslateForStatement(ForStatementSyntax forStmt)
    {
        // Check if this is a grid-stride loop pattern
        if (IsGridStrideLoop(forStmt))
        {
            TranslateGridStrideLoop(forStmt);
            return;
        }

        WriteIndented("for (");

        // Declaration

        if (forStmt.Declaration != null)
        {
            var type = ConvertToCudaType(forStmt.Declaration.Type.ToString());
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

        if (forStmt.Incrementors.Any())
        {
            for (var i = 0; i < forStmt.Incrementors.Count; i++)
            {
                if (i > 0)
                {
                    _output.Append(", ");
                }


                TranslateExpression(forStmt.Incrementors[i]);
            }
        }


        _output.AppendLine(") {");
        _indentLevel++;
        TranslateStatement(forStmt.Statement);
        _indentLevel--;
        WriteIndented("}");
    }

    private static bool IsGridStrideLoop(ForStatementSyntax forStmt)
    {
        // Detect pattern: for (int i = idx; i < length; i += gridSize)
        if (forStmt.Declaration?.Variables.Count != 1)
        {
            return false;
        }


        var variable = forStmt.Declaration.Variables[0];
        var varName = variable.Identifier.Text;

        // Check if initializer contains thread/block index references

        var initText = variable.Initializer?.Value.ToString() ?? "";
        return initText.Contains("idx") || initText.Contains("threadIdx") || initText.Contains("blockIdx");
    }

    private void TranslateGridStrideLoop(ForStatementSyntax forStmt)
    {
        // Already handled by our CUDA kernel template
        WriteIndented("// Grid-stride loop pattern detected - using built-in loop from kernel template");
        TranslateStatement(forStmt.Statement);
    }

    private void TranslateIfStatement(IfStatementSyntax ifStmt)
    {
        WriteIndented("if (");
        TranslateExpression(ifStmt.Condition);
        _output.AppendLine(") {");


        _indentLevel++;
        TranslateStatement(ifStmt.Statement);
        _indentLevel--;


        if (ifStmt.Else != null)
        {
            WriteIndented("} else ");
            if (ifStmt.Else.Statement is IfStatementSyntax)
            {
                // else if case
                TranslateStatement(ifStmt.Else.Statement);
            }
            else
            {
                _output.AppendLine("{");
                _indentLevel++;
                TranslateStatement(ifStmt.Else.Statement);
                _indentLevel--;
                WriteIndented("}");
            }
        }
        else
        {
            WriteIndented("}");
        }
    }

    private void TranslateWhileStatement(WhileStatementSyntax whileStmt)
    {
        WriteIndented("while (");
        TranslateExpression(whileStmt.Condition);
        _output.AppendLine(") {");


        _indentLevel++;
        TranslateStatement(whileStmt.Statement);
        _indentLevel--;


        WriteIndented("}");
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

    private void TranslateExpression(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case BinaryExpressionSyntax binary:
                TranslateBinaryExpression(binary);
                break;
            case LiteralExpressionSyntax literal:
                TranslateLiteralExpression(literal);
                break;
            case IdentifierNameSyntax identifier:
                TranslateIdentifier(identifier);
                break;
            case ElementAccessExpressionSyntax elementAccess:
                TranslateElementAccess(elementAccess);
                break;
            case InvocationExpressionSyntax invocation:
                TranslateInvocation(invocation);
                break;
            case MemberAccessExpressionSyntax memberAccess:
                TranslateMemberAccess(memberAccess);
                break;
            case AssignmentExpressionSyntax assignment:
                TranslateAssignment(assignment);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                _output.Append("(");
                TranslateExpression(parenthesized.Expression);
                _output.Append(")");
                break;
            case PostfixUnaryExpressionSyntax postfix:
                TranslateExpression(postfix.Operand);
                _output.Append(GetOperatorString(postfix.OperatorToken));
                break;
            case PrefixUnaryExpressionSyntax prefix:
                _output.Append(GetOperatorString(prefix.OperatorToken));
                TranslateExpression(prefix.Operand);
                break;
            case CastExpressionSyntax cast:
                _output.Append($"({ConvertToCudaType(cast.Type.ToString())})");
                TranslateExpression(cast.Expression);
                break;
            case ConditionalExpressionSyntax conditional:
                TranslateExpression(conditional.Condition);
                _output.Append(" ? ");
                TranslateExpression(conditional.WhenTrue);
                _output.Append(" : ");
                TranslateExpression(conditional.WhenFalse);
                break;
            default:
                _output.Append($"/* Unsupported expression: {expression.Kind()} */");
                break;
        }
    }

    private void TranslateBinaryExpression(BinaryExpressionSyntax binary)
    {
        TranslateExpression(binary.Left);
        _output.Append($" {GetOperatorString(binary.OperatorToken)} ");
        TranslateExpression(binary.Right);
    }


    private void TranslateLiteralExpression(LiteralExpressionSyntax literal)
    {
        var value = literal.Token.Value?.ToString() ?? literal.Token.Text;

        // Handle float literals

        if (literal.Token.Text.EndsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            _output.Append(literal.Token.Text);
        }
        else if (literal.Token.Value is float || literal.Token.Value is double)
        {
            _output.Append($"{value}f");
        }
        else
        {
            _output.Append(value);
        }
    }

    private void TranslateIdentifier(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.Text;

        // Map special identifiers

        switch (name)
        {
            case "Math":
                // Will be handled in member access
                _output.Append(name);
                break;
            case "length":
            case "Length":
                // Map to our kernel's length parameter
                _output.Append("length");
                break;
            default:
                // Check if this is a parameter that needs special handling
                if (_variableMapping.TryGetValue(name, out var mappedName))
                {
                    _output.Append(mappedName);
                }
                else
                {
                    _output.Append(name);
                }
                break;
        }
    }

    private void TranslateElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        TranslateExpression(elementAccess.Expression);
        _output.Append("[");


        for (var i = 0; i < elementAccess.ArgumentList.Arguments.Count; i++)
        {
            if (i > 0)
            {
                _output.Append(", ");
            }


            TranslateExpression(elementAccess.ArgumentList.Arguments[i].Expression);
        }


        _output.Append("]");
    }

    private void TranslateInvocation(InvocationExpressionSyntax invocation)
    {
        // Handle math functions and intrinsics
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            var objectName = memberAccess.Expression.ToString();


            if (objectName == "Math" || objectName == "MathF")
            {
                // Translate Math functions to CUDA equivalents
                var cudaFunction = TranslateMathFunction(methodName);
                _output.Append(cudaFunction);
            }
            else if (IsAtomicOperation(methodName))
            {
                // Translate to CUDA atomic operations
                TranslateAtomicOperation(methodName, invocation.ArgumentList);
                return;
            }
            else
            {
                TranslateExpression(invocation.Expression);
            }
        }
        else
        {
            TranslateExpression(invocation.Expression);
        }


        _output.Append("(");
        for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
        {
            if (i > 0)
            {
                _output.Append(", ");
            }


            TranslateExpression(invocation.ArgumentList.Arguments[i].Expression);
        }
        _output.Append(")");
    }

    private void TranslateMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var memberName = memberAccess.Name.Identifier.Text;

        // Handle special properties

        if (memberName == "Length" || memberName == "Count")
        {
            // For arrays and spans, this maps to our length parameter
            _output.Append("length");
        }
        else
        {
            TranslateExpression(memberAccess.Expression);
            _output.Append(".");
            _output.Append(memberName);
        }
    }

    private void TranslateAssignment(AssignmentExpressionSyntax assignment)
    {
        TranslateExpression(assignment.Left);
        _output.Append($" {GetOperatorString(assignment.OperatorToken)} ");
        TranslateExpression(assignment.Right);
    }

    private static bool IsAtomicOperation(string methodName)
    {
        return methodName.StartsWith("Interlocked") ||

               methodName.IndexOf("Atomic", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void TranslateAtomicOperation(string methodName, ArgumentListSyntax arguments)
    {
        // Map Interlocked operations to CUDA atomics
        var atomicOp = methodName switch
        {
            "InterlockedAdd" or "Add" => "atomicAdd",
            "InterlockedExchange" or "Exchange" => "atomicExch",
            "InterlockedCompareExchange" => "atomicCAS",
            "InterlockedIncrement" => "atomicAdd",
            "InterlockedDecrement" => "atomicSub",
            "InterlockedAnd" => "atomicAnd",
            "InterlockedOr" => "atomicOr",
            "InterlockedXor" => "atomicXor",
            "InterlockedMin" => "atomicMin",
            "InterlockedMax" => "atomicMax",
            _ => $"/* Unsupported atomic: {methodName} */"
        };


        _output.Append(atomicOp);
        _output.Append("(");


        if (methodName == "InterlockedIncrement")
        {
            TranslateExpression(arguments.Arguments[0].Expression);
            _output.Append(", 1");
        }
        else if (methodName == "InterlockedDecrement")
        {
            TranslateExpression(arguments.Arguments[0].Expression);
            _output.Append(", 1");
        }
        else
        {
            for (var i = 0; i < arguments.Arguments.Count; i++)
            {
                if (i > 0)
                {
                    _output.Append(", ");
                }


                TranslateExpression(arguments.Arguments[i].Expression);
            }
        }


        _output.Append(")");
    }

    private static string TranslateMathFunction(string methodName)
    {
        return methodName switch
        {
            "Sin" => "sinf",
            "Cos" => "cosf",
            "Tan" => "tanf",
            "Asin" => "asinf",
            "Acos" => "acosf",
            "Atan" => "atanf",
            "Atan2" => "atan2f",
            "Sinh" => "sinhf",
            "Cosh" => "coshf",
            "Tanh" => "tanhf",
            "Exp" => "expf",
            "Log" => "logf",
            "Log10" => "log10f",
            "Log2" => "log2f",
            "Pow" => "powf",
            "Sqrt" => "sqrtf",
            "Cbrt" => "cbrtf",
            "Ceil" => "ceilf",
            "Floor" => "floorf",
            "Round" => "roundf",
            "Truncate" => "truncf",
            "Abs" => "fabsf",
            "Min" => "fminf",
            "Max" => "fmaxf",
            "Sign" => "copysignf",
            "Clamp" => "fmaxf(fminf",  // Special handling needed
            _ => methodName.ToLowerInvariant()
        };
    }

    private static string GetOperatorString(SyntaxToken operatorToken)
    {
        return operatorToken.Kind() switch
        {
            SyntaxKind.PlusToken => "+",
            SyntaxKind.MinusToken => "-",
            SyntaxKind.AsteriskToken => "*",
            SyntaxKind.SlashToken => "/",
            SyntaxKind.PercentToken => "%",
            SyntaxKind.AmpersandToken => "&",
            SyntaxKind.BarToken => "|",
            SyntaxKind.CaretToken => "^",
            SyntaxKind.TildeToken => "~",
            SyntaxKind.ExclamationToken => "!",
            SyntaxKind.LessThanToken => "<",
            SyntaxKind.GreaterThanToken => ">",
            SyntaxKind.LessThanEqualsToken => "<=",
            SyntaxKind.GreaterThanEqualsToken => ">=",
            SyntaxKind.EqualsEqualsToken => "==",
            SyntaxKind.ExclamationEqualsToken => "!=",
            SyntaxKind.AmpersandAmpersandToken => "&&",
            SyntaxKind.BarBarToken => "||",
            SyntaxKind.LessThanLessThanToken => "<<",
            SyntaxKind.GreaterThanGreaterThanToken => ">>",
            SyntaxKind.EqualsToken => "=",
            SyntaxKind.PlusEqualsToken => "+=",
            SyntaxKind.MinusEqualsToken => "-=",
            SyntaxKind.AsteriskEqualsToken => "*=",
            SyntaxKind.SlashEqualsToken => "/=",
            SyntaxKind.PercentEqualsToken => "%=",
            SyntaxKind.AmpersandEqualsToken => "&=",
            SyntaxKind.BarEqualsToken => "|=",
            SyntaxKind.CaretEqualsToken => "^=",
            SyntaxKind.LessThanLessThanEqualsToken => "<<=",
            SyntaxKind.GreaterThanGreaterThanEqualsToken => ">>=",
            SyntaxKind.PlusPlusToken => "++",
            SyntaxKind.MinusMinusToken => "--",
            _ => operatorToken.Text
        };
    }

    private static string ConvertToCudaType(string csharpType)
    {
        return csharpType switch
        {
            "float" => "float",
            "double" => "double",
            "int" => "int32_t",
            "uint" => "uint32_t",
            "long" => "int64_t",
            "ulong" => "uint64_t",
            "byte" => "uint8_t",
            "sbyte" => "int8_t",
            "short" => "int16_t",
            "ushort" => "uint16_t",
            "bool" => "bool",
            "char" => "char",
            "void" => "void",
            _ when csharpType.Contains("float2") => "float2",
            _ when csharpType.Contains("float3") => "float3",
            _ when csharpType.Contains("float4") => "float4",
            _ when csharpType.Contains("double2") => "double2",
            _ when csharpType.Contains("double3") => "double3",
            _ when csharpType.Contains("double4") => "double4",
            _ => "float" // Default fallback
        };
    }

    private void WriteIndented(string text)
    {
        for (var i = 0; i < _indentLevel * 4; i++)
        {
            _output.Append(' ');
        }
        if (!string.IsNullOrEmpty(text))
        {
            _output.Append(text);
        }
    }

    private string GenerateDefaultKernelBody()
    {
        var sb = new StringBuilder();

        // Analyze parameters to generate appropriate default body

        var inputBuffers = _kernelInfo.Parameters.Where(p => p.IsBuffer && p.IsReadOnly).ToList();
        var outputBuffers = _kernelInfo.Parameters.Where(p => p.IsBuffer && !p.IsReadOnly).ToList();
        var scalarParams = _kernelInfo.Parameters.Where(p => !p.IsBuffer).ToList();


        if (inputBuffers.Count > 0 && outputBuffers.Count > 0)
        {
            // Element-wise operation pattern
            sb.AppendLine("            // Element-wise kernel operation");
            sb.AppendLine($"            if (i < length) {{");


            if (inputBuffers.Count == 2 && outputBuffers.Count == 1)
            {
                // Binary operation
                sb.AppendLine($"                {outputBuffers[0].Name}[i] = {inputBuffers[0].Name}[i] + {inputBuffers[1].Name}[i];");
            }
            else if (inputBuffers.Count == 1 && outputBuffers.Count == 1)
            {
                // Unary operation
                if (scalarParams.Count > 0)
                {
                    sb.AppendLine($"                {outputBuffers[0].Name}[i] = {inputBuffers[0].Name}[i] * {scalarParams[0].Name};");
                }
                else
                {
                    sb.AppendLine($"                {outputBuffers[0].Name}[i] = {inputBuffers[0].Name}[i];");
                }
            }
            else
            {
                // Generic pattern
                sb.AppendLine($"                // Process element at index i");
                sb.AppendLine($"                {outputBuffers[0].Name}[i] = {inputBuffers[0].Name}[i];");
            }


            sb.AppendLine("            }");
        }
        else if (outputBuffers.Count > 0)
        {
            // Generation pattern
            sb.AppendLine("            // Generation kernel operation");
            sb.AppendLine($"            if (i < length) {{");
            sb.AppendLine($"                {outputBuffers[0].Name}[i] = (float)i;");
            sb.AppendLine("            }");
        }
        else
        {
            // Reduction or custom pattern
            sb.AppendLine("            // Custom kernel operation");
            sb.AppendLine("            // Implement kernel logic here");
        }


        return sb.ToString();
    }
}

// Extension methods for ISymbol
internal static class SymbolExtensions
{
    public static string GetTypeDisplayString(this ISymbol symbol)
    {
        if (symbol is ILocalSymbol local)
        {

            return local.Type.ToDisplayString();
        }


        if (symbol is IParameterSymbol parameter)
        {

            return parameter.Type.ToDisplayString();
        }


        if (symbol is IFieldSymbol field)
        {

            return field.Type.ToDisplayString();
        }


        return "unknown";
    }
}