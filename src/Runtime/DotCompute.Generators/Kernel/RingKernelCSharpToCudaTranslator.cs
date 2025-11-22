// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CA1834 // StringBuilder char append optimization - clarity preferred
#pragma warning disable CA1819 // Properties returning arrays - simple DTO class

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DotCompute.Generators.Kernel.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Specialized translator for Ring Kernel methods that converts C# code with RingKernelContext
/// API calls to optimized CUDA C code with K2K messaging and synchronization support.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via factory")]
internal sealed class RingKernelCSharpToCudaTranslator : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly RingKernelBodyAnalysisResult _analysisResult;
    private readonly StringBuilder _output = new();
    private readonly HashSet<string> _requiredHelpers = new();
    private int _indentLevel;

    /// <summary>
    /// Initializes a new instance of the translator.
    /// </summary>
    public RingKernelCSharpToCudaTranslator(
        SemanticModel semanticModel,
        RingKernelBodyAnalysisResult analysisResult)
        : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
        _analysisResult = analysisResult;
    }

    /// <summary>
    /// Translates a ring kernel method to CUDA C code.
    /// </summary>
    public static RingKernelTranslationResult Translate(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        RingKernelBodyAnalysisResult analysisResult)
    {
        var translator = new RingKernelCSharpToCudaTranslator(semanticModel, analysisResult);
        return translator.TranslateMethod(methodDeclaration);
    }

    private RingKernelTranslationResult TranslateMethod(MethodDeclarationSyntax method)
    {
        var body = method.Body;
        if (body == null && method.ExpressionBody == null)
        {
            return new RingKernelTranslationResult
            {
                CudaCode = "// Empty method body",
                RequiredHelpers = Array.Empty<string>(),
                Success = true
            };
        }

        try
        {
            if (body != null)
            {
                TranslateBlock(body);
            }
            else if (method.ExpressionBody != null)
            {
                WriteIndented("");
                TranslateExpression(method.ExpressionBody.Expression);
                _output.AppendLine(";");
            }

            return new RingKernelTranslationResult
            {
                CudaCode = _output.ToString(),
                RequiredHelpers = _requiredHelpers.ToArray(),
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new RingKernelTranslationResult
            {
                CudaCode = $"// Translation failed: {ex.Message}",
                RequiredHelpers = Array.Empty<string>(),
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private void TranslateBlock(BlockSyntax block)
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
            case DoStatementSyntax doStmt:
                TranslateDoWhileStatement(doStmt);
                break;
            case BlockSyntax block:
                TranslateBlock(block);
                break;
            case ReturnStatementSyntax returnStmt:
                TranslateReturnStatement(returnStmt);
                break;
            case BreakStatementSyntax:
                WriteIndented("break;\n");
                break;
            case ContinueStatementSyntax:
                WriteIndented("continue;\n");
                break;
            default:
                WriteIndented($"// Unsupported statement: {statement.Kind()}\n");
                break;
        }
    }

    private void TranslateLocalDeclaration(LocalDeclarationStatementSyntax localDecl)
    {
        var type = ConvertToCudaType(localDecl.Declaration.Type.ToString());

        foreach (var variable in localDecl.Declaration.Variables)
        {
            WriteIndented($"{type} {variable.Identifier.Text}");

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
            case InvocationExpressionSyntax invocation:
                TranslateInvocation(invocation);
                break;
            case MemberAccessExpressionSyntax memberAccess:
                TranslateMemberAccess(memberAccess);
                break;
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
            case AssignmentExpressionSyntax assignment:
                TranslateAssignment(assignment);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                _output.Append('(');
                TranslateExpression(parenthesized.Expression);
                _output.Append(')');
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
                _output.Append($"/* Unsupported: {expression.Kind()} */");
                break;
        }
    }

    private void TranslateInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            var targetName = GetTargetName(memberAccess.Expression);

            // Check if this is a RingKernelContext API call
            if (IsContextParameter(targetName))
            {
                TranslateContextApiCall(methodName, invocation.ArgumentList);
                return;
            }

            // Handle Math functions
            var objectName = memberAccess.Expression.ToString();
            if (string.Equals(objectName, "Math", StringComparison.Ordinal) ||
                string.Equals(objectName, "MathF", StringComparison.Ordinal))
            {
                _output.Append(TranslateMathFunction(methodName));
                TranslateArgumentList(invocation.ArgumentList);
                return;
            }
        }

        // Default: translate as regular function call
        TranslateExpression(invocation.Expression);
        TranslateArgumentList(invocation.ArgumentList);
    }

    private void TranslateContextApiCall(string methodName, ArgumentListSyntax arguments)
    {
        switch (methodName)
        {
            // Barriers
            case "SyncThreads":
                _output.Append("__syncthreads()");
                break;

            case "SyncGrid":
                _requiredHelpers.Add("cooperative_groups");
                _output.Append("cooperative_groups::this_grid().sync()");
                break;

            case "SyncWarp":
                _output.Append("__syncwarp()");
                break;

            case "NamedBarrier":
                // Translate named barrier to CUDA cooperative groups
                _requiredHelpers.Add("cooperative_groups");
                _output.Append("cooperative_groups::this_thread_block().sync()");
                break;

            // Temporal
            case "Now":
                _requiredHelpers.Add("hlc");
                _output.Append("hlc_now()");
                break;

            case "Tick":
                _requiredHelpers.Add("hlc");
                _output.Append("hlc_tick()");
                break;

            case "UpdateClock":
                _requiredHelpers.Add("hlc");
                _output.Append("hlc_update(");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            // Memory fences
            case "ThreadFence":
                _output.Append("__threadfence()");
                break;

            case "ThreadFenceBlock":
                _output.Append("__threadfence_block()");
                break;

            case "ThreadFenceSystem":
                _output.Append("__threadfence_system()");
                break;

            // K2K Messaging
            case "SendToKernel":
                TranslateK2KSend(arguments);
                break;

            case "TryReceiveFromKernel":
                TranslateK2KReceive(arguments);
                break;

            case "GetPendingMessageCount":
                TranslateK2KPendingCount(arguments);
                break;

            // Pub/Sub
            case "PublishToTopic":
                TranslatePubSubPublish(arguments);
                break;

            case "TryReceiveFromTopic":
                TranslatePubSubReceive(arguments);
                break;

            // Atomics
            case "AtomicAdd":
                _output.Append("atomicAdd(");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "AtomicCAS":
                _output.Append("atomicCAS(");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "AtomicExch":
                _output.Append("atomicExch(");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "AtomicMin":
                _output.Append("atomicMin(");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "AtomicMax":
                _output.Append("atomicMax(");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            // Warp primitives
            case "WarpShuffle":
                _output.Append("__shfl_sync(0xFFFFFFFF, ");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "WarpShuffleDown":
                _output.Append("__shfl_down_sync(0xFFFFFFFF, ");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "WarpReduce":
                TranslateWarpReduce(arguments);
                break;

            case "WarpBallot":
                _output.Append("__ballot_sync(0xFFFFFFFF, ");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "WarpAll":
                _output.Append("__all_sync(0xFFFFFFFF, ");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            case "WarpAny":
                _output.Append("__any_sync(0xFFFFFFFF, ");
                TranslateArgumentList(arguments, skipParens: true);
                _output.Append(")");
                break;

            default:
                _output.Append($"/* Unknown context API: {methodName} */");
                break;
        }
    }

    private void TranslateK2KSend(ArgumentListSyntax arguments)
    {
        _requiredHelpers.Add("k2k_messaging");

        // Generate: k2k_send(target_kernel_id, message_ptr, message_size)
        _output.Append("k2k_send(");

        var args = arguments.Arguments.ToArray();
        if (args.Length >= 2)
        {
            // First arg: target kernel ID (string)
            TranslateExpression(args[0].Expression);
            _output.Append(", ");

            // Second arg: message (needs serialization pointer)
            _output.Append("&");
            TranslateExpression(args[1].Expression);
            _output.Append(", sizeof(");
            TranslateExpression(args[1].Expression);
            _output.Append(")");
        }

        _output.Append(")");
    }

    private void TranslateK2KReceive(ArgumentListSyntax arguments)
    {
        _requiredHelpers.Add("k2k_messaging");

        // Generate: k2k_try_receive(source_kernel_id, message_ptr, message_size)
        _output.Append("k2k_try_receive(");

        var args = arguments.Arguments.ToArray();
        if (args.Length >= 2)
        {
            TranslateExpression(args[0].Expression);
            _output.Append(", &");
            TranslateExpression(args[1].Expression);
            _output.Append(", sizeof(");
            TranslateExpression(args[1].Expression);
            _output.Append(")");
        }

        _output.Append(")");
    }

    private void TranslateK2KPendingCount(ArgumentListSyntax arguments)
    {
        _requiredHelpers.Add("k2k_messaging");

        _output.Append("k2k_pending_count(");
        var args = arguments.Arguments.ToArray();
        if (args.Length >= 1)
        {
            TranslateExpression(args[0].Expression);
        }
        _output.Append(")");
    }

    private void TranslatePubSubPublish(ArgumentListSyntax arguments)
    {
        _requiredHelpers.Add("pubsub_messaging");

        _output.Append("pubsub_publish(");
        var args = arguments.Arguments.ToArray();
        if (args.Length >= 2)
        {
            TranslateExpression(args[0].Expression);
            _output.Append(", &");
            TranslateExpression(args[1].Expression);
            _output.Append(", sizeof(");
            TranslateExpression(args[1].Expression);
            _output.Append(")");
        }
        _output.Append(")");
    }

    private void TranslatePubSubReceive(ArgumentListSyntax arguments)
    {
        _requiredHelpers.Add("pubsub_messaging");

        _output.Append("pubsub_try_receive(");
        var args = arguments.Arguments.ToArray();
        if (args.Length >= 2)
        {
            TranslateExpression(args[0].Expression);
            _output.Append(", &");
            TranslateExpression(args[1].Expression);
            _output.Append(", sizeof(");
            TranslateExpression(args[1].Expression);
            _output.Append(")");
        }
        _output.Append(")");
    }

    private void TranslateWarpReduce(ArgumentListSyntax arguments)
    {
        _requiredHelpers.Add("warp_reduce");

        // Generate inline warp reduction
        var args = arguments.Arguments.ToArray();
        if (args.Length >= 2)
        {
#pragma warning disable CA1308 // Lowercase required for CUDA function naming convention
            var op = args[1].Expression.ToString().ToLowerInvariant();
#pragma warning restore CA1308

            _output.Append($"warp_reduce_{op}(");
            TranslateExpression(args[0].Expression);
            _output.Append(")");
        }
        else
        {
            _output.Append("/* Invalid WarpReduce call */");
        }
    }

    private void TranslateArgumentList(ArgumentListSyntax arguments, bool skipParens = false)
    {
        if (!skipParens)
        {
            _output.Append('(');
        }

        for (var i = 0; i < arguments.Arguments.Count; i++)
        {
            if (i > 0)
            {
                _output.Append(", ");
            }

            TranslateExpression(arguments.Arguments[i].Expression);
        }

        if (!skipParens)
        {
            _output.Append(')');
        }
    }

    private void TranslateMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var memberName = memberAccess.Name.Identifier.Text;
        var targetName = GetTargetName(memberAccess.Expression);

        // Check if accessing context properties (ThreadId, BlockId, etc.)
        if (IsContextParameter(targetName))
        {
            TranslateContextProperty(memberName);
            return;
        }

        // Handle array Length property
        if (memberName is "Length" or "Count")
        {
            _output.Append("length");
            return;
        }

        // Default member access
        TranslateExpression(memberAccess.Expression);
        _output.Append('.');
        _output.Append(memberName);
    }

    private void TranslateContextProperty(string propertyName)
    {
        switch (propertyName)
        {
            case "ThreadId":
                _output.Append("threadIdx");
                break;
            case "BlockId":
                _output.Append("blockIdx");
                break;
            case "BlockDim":
                _output.Append("blockDim");
                break;
            case "GridDim":
                _output.Append("gridDim");
                break;
            case "WarpId":
                _output.Append("(threadIdx.x / 32)");
                break;
            case "LaneId":
                _output.Append("(threadIdx.x % 32)");
                break;
            case "GlobalThreadId":
                _output.Append("(blockIdx.x * blockDim.x + threadIdx.x)");
                break;
            case "KernelId":
                _output.Append("kernel_id");
                break;
            default:
                _output.Append($"/* Unknown context property: {propertyName} */");
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

        if (literal.Token.Value is bool boolValue)
        {
            _output.Append(boolValue ? "true" : "false");
            return;
        }

        if (literal.Token.Text.EndsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            _output.Append(literal.Token.Text);
        }
        else if (literal.Token.Value is float or double)
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

        // Skip context parameter
        if (IsContextParameter(name))
        {
            return;
        }

        _output.Append(name);
    }

    private void TranslateElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        TranslateExpression(elementAccess.Expression);
        _output.Append('[');

        for (var i = 0; i < elementAccess.ArgumentList.Arguments.Count; i++)
        {
            if (i > 0)
            {
                _output.Append(", ");
            }

            TranslateExpression(elementAccess.ArgumentList.Arguments[i].Expression);
        }

        _output.Append(']');
    }

    private void TranslateAssignment(AssignmentExpressionSyntax assignment)
    {
        TranslateExpression(assignment.Left);
        _output.Append($" {GetOperatorString(assignment.OperatorToken)} ");
        TranslateExpression(assignment.Right);
    }

    private void TranslateForStatement(ForStatementSyntax forStmt)
    {
        WriteIndented("for (");

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

        if (forStmt.Condition != null)
        {
            TranslateExpression(forStmt.Condition);
        }
        _output.Append("; ");

        for (var i = 0; i < forStmt.Incrementors.Count; i++)
        {
            if (i > 0)
            {
                _output.Append(", ");
            }

            TranslateExpression(forStmt.Incrementors[i]);
        }

        _output.AppendLine(") {");
        _indentLevel++;
        TranslateStatement(forStmt.Statement);
        _indentLevel--;
        WriteIndented("}\n");
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
                TranslateStatement(ifStmt.Else.Statement);
            }
            else
            {
                _output.AppendLine("{");
                _indentLevel++;
                TranslateStatement(ifStmt.Else.Statement);
                _indentLevel--;
                WriteIndented("}\n");
            }
        }
        else
        {
            WriteIndented("}\n");
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

        WriteIndented("}\n");
    }

    private void TranslateDoWhileStatement(DoStatementSyntax doStmt)
    {
        WriteIndented("do {\n");
        _indentLevel++;
        TranslateStatement(doStmt.Statement);
        _indentLevel--;

        WriteIndented("} while (");
        TranslateExpression(doStmt.Condition);
        _output.AppendLine(");");
    }

    private void TranslateReturnStatement(ReturnStatementSyntax returnStmt)
    {
        WriteIndented("return");
        if (returnStmt.Expression != null)
        {
            _output.Append(' ');
            TranslateExpression(returnStmt.Expression);
        }
        _output.AppendLine(";");
    }

    private static string GetTargetName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => expression.ToString()
        };
    }

    private static bool IsContextParameter(string name)
    {
        return name is "ctx" or "context" or "kernelContext" or "ringContext";
    }

    private static string TranslateMathFunction(string methodName)
    {
        return methodName switch
        {
            "Sin" => "sinf",
            "Cos" => "cosf",
            "Tan" => "tanf",
            "Exp" => "expf",
            "Log" => "logf",
            "Sqrt" => "sqrtf",
            "Pow" => "powf",
            "Abs" => "fabsf",
            "Min" => "fminf",
            "Max" => "fmaxf",
            "Floor" => "floorf",
            "Ceil" => "ceilf",
            "Round" => "roundf",
#pragma warning disable CA1308 // Lowercase required for CUDA C function names
            _ => methodName.ToLowerInvariant()
#pragma warning restore CA1308
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
            SyntaxKind.LessThanToken => "<",
            SyntaxKind.GreaterThanToken => ">",
            SyntaxKind.LessThanEqualsToken => "<=",
            SyntaxKind.GreaterThanEqualsToken => ">=",
            SyntaxKind.EqualsEqualsToken => "==",
            SyntaxKind.ExclamationEqualsToken => "!=",
            SyntaxKind.AmpersandAmpersandToken => "&&",
            SyntaxKind.BarBarToken => "||",
            SyntaxKind.EqualsToken => "=",
            SyntaxKind.PlusEqualsToken => "+=",
            SyntaxKind.MinusEqualsToken => "-=",
            SyntaxKind.AsteriskEqualsToken => "*=",
            SyntaxKind.SlashEqualsToken => "/=",
            SyntaxKind.PlusPlusToken => "++",
            SyntaxKind.MinusMinusToken => "--",
            SyntaxKind.ExclamationToken => "!",
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
            "var" => "auto",
            _ => "float"
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
}

/// <summary>
/// Result of translating a ring kernel method to CUDA C.
/// </summary>
public sealed class RingKernelTranslationResult
{
    /// <summary>
    /// The generated CUDA C code.
    /// </summary>
    public string CudaCode { get; set; } = string.Empty;

    /// <summary>
    /// Helper modules that need to be included.
    /// </summary>
    public string[] RequiredHelpers { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether the translation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if translation failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
