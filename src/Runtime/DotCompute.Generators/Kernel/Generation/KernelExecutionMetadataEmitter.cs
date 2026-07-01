// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Produces the executable metadata the runtime consumes for a <c>[Kernel]</c> method:
/// dimensionality, real CUDA-C source, and a CPU invoker delegate body. This is a focused,
/// syntax-only translator for the <c>[Kernel]</c> subset (local int decls, KernelContext
/// intrinsics, span indexing, binary/assignment expressions, and bounds-check <c>if</c>s).
/// It intentionally does NOT reuse the Ring-Kernel <see cref="CSharpToCudaTranslator"/>
/// (which snake_cases identifiers and has different intrinsic semantics).
/// </summary>
/// <remarks>
/// Unhandled constructs cause a graceful fallback: CUDA generation returns <c>null</c>
/// rather than emitting broken device code, while the CPU invoker keeps the body verbatim
/// (it stays valid C#) and only substitutes the KernelContext intrinsics.
/// </remarks>
internal static class KernelExecutionMetadataEmitter
{
    /// <summary>
    /// Detects kernel dimensionality (1, 2, or 3) from KernelContext intrinsic usage in the body.
    /// Uses any <c>.Z</c> axis =&gt; 3, else any <c>.Y</c> axis =&gt; 2, else 1.
    /// </summary>
    public static int DetectDimensions(KernelMethodInfo method)
    {
        var body = GetBody(method);
        if (body is null)
        {
            return 1;
        }

        var usesY = false;
        foreach (var memberAccess in body.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (!TryClassifyIntrinsic(memberAccess, out _, out var axis))
            {
                continue;
            }

            if (axis == "z")
            {
                return 3;
            }

            if (axis == "y")
            {
                usesY = true;
            }
        }

        return usesY ? 2 : 1;
    }

    /// <summary>
    /// Generates the <c>extern "C" __global__</c> CUDA-C source for the kernel, or
    /// <c>(null, null)</c> if the body uses constructs outside the supported subset.
    /// </summary>
    public static (string? Source, string? EntryPoint, bool NeedsLength) GenerateCuda(KernelMethodInfo method)
    {
        var body = GetBody(method);
        if (body is null)
        {
            return (null, null, false);
        }

        try
        {
            // A CUDA pointer has no `.Length`. If the body uses a buffer's `.Length` (the common
            // elementwise `if (i < c.Length)` bound), emit an implicit trailing `__length` kernel
            // parameter; each `.Length` translates to `__length`, and the runtime supplies the work
            // size for it (KernelExecutionService, guarded by CudaNeedsLength).
            var needsLength = UsesBufferLength(body);

            var signature = BuildCudaSignature(method);
            if (needsLength)
            {
                signature += ", const unsigned int __length";
            }

            var builder = new StringBuilder();
            _ = builder.Append("extern \"C\" __global__ void ").Append(method.Name).Append('(').Append(signature).Append(") {").Append('\n');
            foreach (var statement in body.Statements)
            {
                TranslateStatementToCuda(statement, builder, 1);
            }
            _ = builder.Append('}');

            return (builder.ToString(), method.Name, needsLength);
        }
#pragma warning disable CA1031 // Do not catch general exception types - generator must never crash the build
        catch (Exception)
#pragma warning restore CA1031
        {
            // Graceful fallback: a kernel that uses unsupported constructs (e.g. Math.* calls)
            // still registers for CPU; it simply has no auto-generated CUDA source.
            return (null, null, false);
        }
    }

    /// <summary>True if the body reads any <c>&lt;identifier&gt;.Length</c> (treated as a buffer length).</summary>
    private static bool UsesBufferLength(BlockSyntax body)
        => body.DescendantNodes()
               .OfType<MemberAccessExpressionSyntax>()
               .Any(m => m.Name.Identifier.Text == "Length" && m.Expression is IdentifierNameSyntax);

    /// <summary>
    /// Generates the <c>&lt;sanitized&gt;CpuInvoker</c> class that reconstructs typed spans/scalars
    /// from <c>object[] args</c> and runs the kernel body over the flat work range [start, end).
    /// </summary>
    public static string GenerateCpuInvokerClass(KernelMethodInfo method, int dimensions, string invokerClassName)
    {
        var builder = new StringBuilder();
        _ = builder.AppendLine($"    /// <summary>CPU invoker for {method.ContainingType}.{method.Name} (flat work-range execution).</summary>");
        _ = builder.AppendLine($"    public static class {invokerClassName}");
        _ = builder.AppendLine("    {");
        _ = builder.AppendLine("        public static void Invoke(object[] args, int start, int end)");
        _ = builder.AppendLine("        {");

        var body = GetBody(method);
        if (body is null)
        {
            _ = builder.AppendLine($"            throw new System.NotSupportedException(\"No translatable body for kernel {method.Name}.\");");
            _ = builder.AppendLine("        }");
            _ = builder.AppendLine("    }");
            return builder.ToString();
        }

        // 1. Reconstruct each parameter (buffer => typed span, scalar => cast) from args[i].
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            if (param.IsBuffer)
            {
                var element = GetElementCSharpType(param.Type);
                var spanType = IsReadOnlyBuffer(param) ? "System.ReadOnlySpan" : "System.Span";
                _ = builder.AppendLine($"            var {param.Name} = new {spanType}<{element}>(({element}[])args[{i}]);");
            }
            else
            {
                _ = builder.AppendLine($"            var {param.Name} = ({param.Type})args[{i}];");
            }
        }

        // 2. Flat work-range loop with N-D coordinate reconstruction from trailing integer extents.
        _ = builder.AppendLine("            for (int __i = start; __i < end; __i++)");
        _ = builder.AppendLine("            {");
        EmitExtentReconstruction(builder, method, dimensions);

        // 3. Body with KernelContext intrinsics substituted and early returns mapped to continue.
        var rewritten = (BlockSyntax)new CpuInvokerBodyRewriter().Visit(body);
        foreach (var statement in rewritten.Statements)
        {
            foreach (var line in statement.ToString().Split('\n'))
            {
                _ = builder.Append("                ").AppendLine(line.TrimEnd('\r'));
            }
        }

        _ = builder.AppendLine("            }");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");
        return builder.ToString();
    }

    /// <summary>
    /// Builds a deterministic, collision-free CPU-invoker class name for a kernel.
    /// </summary>
    public static string GetInvokerClassName(KernelMethodInfo method)
        => $"{SanitizeIdentifier(method.ContainingType)}_{method.Name}CpuInvoker";

    /// <summary>
    /// Maps a parameter's element type (buffer element or scalar) to a C# keyword string
    /// (e.g. <c>float</c>, <c>int</c>) used as the registry's <c>ElementType</c>.
    /// </summary>
    public static string GetElementTypeName(ParameterInfo param)
        => param.IsBuffer ? GetElementCSharpType(param.Type) : NormalizeCSharpType(param.Type);

    /// <summary>
    /// Determines registry read-only status: true only for <c>ReadOnlySpan&lt;T&gt;</c> buffers.
    /// </summary>
    /// <remarks>
    /// The upstream <see cref="ParameterInfo.IsReadOnly"/> flag is derived in part from
    /// <c>ITypeSymbol.IsReadOnly</c>, which is <c>true</c> for every readonly struct
    /// (<c>Span&lt;T&gt;</c>, <c>int</c>, <c>float</c>) and therefore cannot distinguish a
    /// writable <c>Span&lt;T&gt;</c> from a <c>ReadOnlySpan&lt;T&gt;</c>. We recompute it
    /// from the declared type name for correctness.
    /// </remarks>
    public static bool GetIsReadOnly(ParameterInfo param) => param.IsBuffer && IsReadOnlyBuffer(param);

    private static bool IsReadOnlyBuffer(ParameterInfo param)
        => param.Type.IndexOf("ReadOnlySpan", StringComparison.Ordinal) >= 0;

    // -------------------------------------------------------------------------------------
    // Extent reconstruction
    // -------------------------------------------------------------------------------------

    private static void EmitExtentReconstruction(StringBuilder builder, KernelMethodInfo method, int dimensions)
    {
        const string indent = "                ";

        // Indices (into args) of integer scalar parameters, in declaration order.
        var intArgs = new List<int>();
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            if (!param.IsBuffer && IsIntegerType(NormalizeCSharpType(param.Type)))
            {
                intArgs.Add(i);
            }
        }

        var last = intArgs.Count - 1;

        if (dimensions >= 3 && intArgs.Count >= 3)
        {
            _ = builder.AppendLine($"{indent}int __xext = (int)args[{intArgs[last]}];");
            _ = builder.AppendLine($"{indent}int __yext = (int)args[{intArgs[last - 1]}];");
            _ = builder.AppendLine($"{indent}int __zext = (int)args[{intArgs[last - 2]}];");
            _ = builder.AppendLine($"{indent}int __x = __i % __xext;");
            _ = builder.AppendLine($"{indent}int __y = (__i / __xext) % __yext;");
            _ = builder.AppendLine($"{indent}int __z = __i / (__xext * __yext);");
        }
        else if (dimensions >= 2 && intArgs.Count >= 2)
        {
            _ = builder.AppendLine($"{indent}int __xext = (int)args[{intArgs[last]}];");
            _ = builder.AppendLine($"{indent}int __yext = (int)args[{intArgs[last - 1]}];");
            _ = builder.AppendLine($"{indent}int __zext = 1;");
            _ = builder.AppendLine($"{indent}int __x = __i % __xext;");
            _ = builder.AppendLine($"{indent}int __y = __i / __xext;");
            _ = builder.AppendLine($"{indent}int __z = 0;");
        }
        else
        {
            var xext = intArgs.Count > 0 ? $"(int)args[{intArgs[last]}]" : "(end - start)";
            _ = builder.AppendLine($"{indent}int __xext = {xext};");
            _ = builder.AppendLine($"{indent}int __yext = 1;");
            _ = builder.AppendLine($"{indent}int __zext = 1;");
            _ = builder.AppendLine($"{indent}int __x = __i;");
            _ = builder.AppendLine($"{indent}int __y = 0;");
            _ = builder.AppendLine($"{indent}int __z = 0;");
        }
    }

    // -------------------------------------------------------------------------------------
    // CUDA signature + statement/expression translation
    // -------------------------------------------------------------------------------------

    private static string BuildCudaSignature(KernelMethodInfo method)
    {
        var parts = new List<string>();
        foreach (var param in method.Parameters)
        {
            if (param.IsBuffer)
            {
                var element = MapCudaType(GetElementCSharpType(param.Type));
                var qualifier = IsReadOnlyBuffer(param) ? "const " : string.Empty;
                parts.Add($"{qualifier}{element}* {param.Name}");
            }
            else
            {
                parts.Add($"{MapCudaType(NormalizeCSharpType(param.Type))} {param.Name}");
            }
        }

        return string.Join(", ", parts);
    }

    private static void TranslateStatementToCuda(StatementSyntax statement, StringBuilder builder, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);

        switch (statement)
        {
            case LocalDeclarationStatementSyntax local:
                {
                    var cudaType = MapCudaType(NormalizeCSharpType(local.Declaration.Type.ToString()));
                    foreach (var declarator in local.Declaration.Variables)
                    {
                        if (declarator.Initializer is null)
                        {
                            _ = builder.Append(indent).Append(cudaType).Append(' ').Append(declarator.Identifier.Text).Append(';').Append('\n');
                        }
                        else
                        {
                            var value = TranslateExpressionToCuda(declarator.Initializer.Value);
                            _ = builder.Append(indent).Append(cudaType).Append(' ').Append(declarator.Identifier.Text).Append(" = ").Append(value).Append(';').Append('\n');
                        }
                    }

                    break;
                }

            case ExpressionStatementSyntax expr:
                _ = builder.Append(indent).Append(TranslateExpressionToCuda(expr.Expression)).Append(';').Append('\n');
                break;

            case BlockSyntax block:
                _ = builder.Append(indent).Append('{').Append('\n');
                foreach (var inner in block.Statements)
                {
                    TranslateStatementToCuda(inner, builder, indentLevel + 1);
                }
                _ = builder.Append(indent).Append('}').Append('\n');
                break;

            case IfStatementSyntax ifStatement:
                {
                    var condition = TranslateExpressionToCuda(ifStatement.Condition);
                    _ = builder.Append(indent).Append("if (").Append(condition).Append(')').Append('\n');
                    EmitBranch(ifStatement.Statement, builder, indentLevel);

                    if (ifStatement.Else is not null)
                    {
                        _ = builder.Append(indent).Append("else").Append('\n');
                        EmitBranch(ifStatement.Else.Statement, builder, indentLevel);
                    }

                    break;
                }

            case ReturnStatementSyntax returnStatement when returnStatement.Expression is null:
                _ = builder.Append(indent).Append("return;").Append('\n');
                break;

            default:
                throw new NotSupportedException($"Unsupported statement kind: {statement.Kind()}");
        }
    }

    private static void EmitBranch(StatementSyntax branch, StringBuilder builder, int indentLevel)
    {
        // Block branches keep their own indent level; single statements are indented one deeper.
        TranslateStatementToCuda(branch, builder, branch is BlockSyntax ? indentLevel : indentLevel + 1);
    }

    private static string TranslateExpressionToCuda(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax paren:
                return $"({TranslateExpressionToCuda(paren.Expression)})";

            case BinaryExpressionSyntax binary:
                return $"{TranslateExpressionToCuda(binary.Left)} {binary.OperatorToken.Text} {TranslateExpressionToCuda(binary.Right)}";

            case AssignmentExpressionSyntax assignment:
                return $"{TranslateExpressionToCuda(assignment.Left)} {assignment.OperatorToken.Text} {TranslateExpressionToCuda(assignment.Right)}";

            case PrefixUnaryExpressionSyntax prefix:
                return $"{prefix.OperatorToken.Text}{TranslateExpressionToCuda(prefix.Operand)}";

            case PostfixUnaryExpressionSyntax postfix:
                return $"{TranslateExpressionToCuda(postfix.Operand)}{postfix.OperatorToken.Text}";

            case LiteralExpressionSyntax literal:
                return literal.Token.Text;

            case IdentifierNameSyntax identifier:
                return identifier.Identifier.Text;

            case ElementAccessExpressionSyntax elementAccess:
                {
                    var indices = elementAccess.ArgumentList.Arguments
                        .Select(a => TranslateExpressionToCuda(a.Expression));
                    return $"{TranslateExpressionToCuda(elementAccess.Expression)}[{string.Join(", ", indices)}]";
                }

            case CastExpressionSyntax cast:
                return $"({MapCudaType(NormalizeCSharpType(cast.Type.ToString()))}){TranslateExpressionToCuda(cast.Expression)}";

            case MemberAccessExpressionSyntax memberAccess when TryClassifyIntrinsic(memberAccess, out var intrinsic, out var axis):
                return $"{intrinsic}.{axis}";

            // A buffer's `.Length` maps to the implicit `__length` kernel parameter (a CUDA pointer
            // carries no length). GenerateCuda adds `__length` to the signature and flags the
            // kernel so the runtime supplies the work size for it.
            case MemberAccessExpressionSyntax lengthAccess when lengthAccess.Name.Identifier.Text == "Length" && lengthAccess.Expression is IdentifierNameSyntax:
                return "__length";

            default:
                throw new NotSupportedException($"Unsupported expression kind: {expression.Kind()}");
        }
    }

    // -------------------------------------------------------------------------------------
    // KernelContext intrinsic classification (shared by CUDA + CPU + dimension detection)
    // -------------------------------------------------------------------------------------

    /// <summary>
    /// Classifies a member access as a KernelContext intrinsic of the form
    /// <c>&lt;root&gt;.{ThreadId|BlockId|BlockDim|GridDim}.{X|Y|Z}</c>. The root identifier
    /// (KernelContext, Kernel, ...) is ignored so both spellings are accepted.
    /// </summary>
    /// <param name="node">The outer member access (the <c>.X/.Y/.Z</c> access).</param>
    /// <param name="cudaIntrinsic">The CUDA intrinsic name (threadIdx/blockIdx/blockDim/gridDim).</param>
    /// <param name="axis">The lower-cased axis (x/y/z).</param>
    private static bool TryClassifyIntrinsic(MemberAccessExpressionSyntax node, out string cudaIntrinsic, out string axis)
    {
        cudaIntrinsic = string.Empty;
        axis = string.Empty;

        var axisName = node.Name.Identifier.Text;
        if (axisName is not ("X" or "Y" or "Z"))
        {
            return false;
        }

        if (node.Expression is not MemberAccessExpressionSyntax inner)
        {
            return false;
        }

        cudaIntrinsic = inner.Name.Identifier.Text switch
        {
            "ThreadId" => "threadIdx",
            "BlockId" => "blockIdx",
            "BlockDim" => "blockDim",
            "GridDim" => "gridDim",
            _ => string.Empty
        };

        if (cudaIntrinsic.Length == 0)
        {
            return false;
        }

        axis = axisName == "X" ? "x" : axisName == "Y" ? "y" : "z";
        return true;
    }

    /// <summary>
    /// Rewrites a kernel body for CPU execution: KernelContext intrinsics become local
    /// coordinate/extent variables, and top-level <c>return;</c> early-outs become <c>continue;</c>.
    /// </summary>
    private sealed class CpuInvokerBodyRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (!TryClassifyIntrinsic(node, out var intrinsic, out var axis))
            {
                return base.VisitMemberAccessExpression(node);
            }

            var replacement = intrinsic switch
            {
                // ThreadId.* -> __x/__y/__z
                "threadIdx" => Identifier("__" + axis),
                // BlockDim.* -> __xext/__yext/__zext
                "blockDim" => Identifier("__" + axis + "ext"),
                // BlockId.* -> 0 (single logical block on CPU)
                "blockIdx" => Number(0),
                // GridDim.* -> 1
                _ => Number(1)
            };

            return replacement.WithTriviaFrom(node);
        }

        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression is not null)
            {
                return base.VisitReturnStatement(node);
            }

            return SyntaxFactory.ContinueStatement().WithTriviaFrom(node);
        }

        private static ExpressionSyntax Identifier(string name) => SyntaxFactory.IdentifierName(name);

        private static ExpressionSyntax Number(int value)
            => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
    }

    // -------------------------------------------------------------------------------------
    // Type helpers
    // -------------------------------------------------------------------------------------

    private static BlockSyntax? GetBody(KernelMethodInfo method) => method.MethodDeclaration?.Body;

    /// <summary>Extracts the element type of a buffer parameter's display string (Span&lt;T&gt;, T[], ...).</summary>
    private static string GetElementCSharpType(string typeString)
    {
        var open = typeString.IndexOf('<');
        var close = typeString.LastIndexOf('>');
        if (open >= 0 && close > open)
        {
            return NormalizeCSharpType(typeString.Substring(open + 1, close - open - 1).Trim());
        }

        if (typeString.EndsWith("[]", StringComparison.Ordinal))
        {
            return NormalizeCSharpType(typeString.Substring(0, typeString.Length - 2).Trim());
        }

        return NormalizeCSharpType(typeString);
    }

    /// <summary>Normalizes a fully-qualified BCL type name to its C# keyword (System.Single -&gt; float).</summary>
    private static string NormalizeCSharpType(string typeString)
    {
        var trimmed = typeString.Trim();
        return trimmed switch
        {
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Boolean" => "bool",
            "System.Char" => "char",
            _ => trimmed
        };
    }

    private static bool IsIntegerType(string csType)
        => csType is "int" or "uint" or "long" or "ulong" or "short" or "ushort" or "byte" or "sbyte";

    private static string MapCudaType(string csType)
    {
        return csType switch
        {
            "float" => "float",
            "double" => "double",
            "int" => "int",
            "uint" => "unsigned int",
            "long" => "long long",
            "ulong" => "unsigned long long",
            "short" => "short",
            "ushort" => "unsigned short",
            "byte" => "unsigned char",
            "sbyte" => "signed char",
            "bool" => "bool",
            _ => throw new NotSupportedException($"Unsupported CUDA scalar type: {csType}")
        };
    }

    private static string SanitizeIdentifier(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            _ = sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return sb.ToString();
    }
}
