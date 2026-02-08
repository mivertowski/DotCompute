// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Translates unified ring kernel methods (RingKernelContext + TRequest) to CUDA device functions.
/// </summary>
/// <remarks>
/// <para>
/// This translator handles the unified ring kernel API where methods have the signature:
/// <code>
/// public static void ProcessMessage(RingKernelContext ctx, TRequest request)
/// </code>
/// </para>
/// <para>
/// The translation process:
/// <list type="number">
/// <item><description>Parse C# method body using Roslyn</description></item>
/// <item><description>Generate deserialization from msg_buffer to typed request struct</description></item>
/// <item><description>Translate C# operations to CUDA equivalents</description></item>
/// <item><description>Generate serialization from response to output_buffer</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Supported C# to CUDA mappings:</b>
/// <list type="bullet">
/// <item><description><c>ctx.SyncThreads()</c> → <c>__syncthreads()</c></description></item>
/// <item><description><c>ctx.ThreadFence()</c> → <c>__threadfence()</c></description></item>
/// <item><description><c>ctx.EnqueueOutput(response)</c> → serialize and set output_size</description></item>
/// <item><description>Property access (request.X) → struct field access</description></item>
/// <item><description>Local variable declarations → CUDA variable declarations</description></item>
/// <item><description>Arithmetic operations → CUDA arithmetic</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class RingKernelHandlerTranslator
{
    private readonly ILogger<RingKernelHandlerTranslator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RingKernelHandlerTranslator"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic messages.</param>
    public RingKernelHandlerTranslator(ILogger<RingKernelHandlerTranslator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Translates a unified ring kernel method to a CUDA device function.
    /// </summary>
    /// <param name="kernel">The discovered ring kernel metadata.</param>
    /// <param name="compilation">The Roslyn compilation containing the source.</param>
    /// <returns>The translated CUDA handler function, or null if translation fails.</returns>
    public string? TranslateKernelHandler(DiscoveredRingKernel kernel, Microsoft.CodeAnalysis.Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(compilation);

        if (!kernel.HasInlineHandler)
        {
            _logger.LogDebug("Kernel {KernelId} does not use unified API (no RingKernelContext parameter)", kernel.KernelId);
            return null;
        }

        try
        {
            // Find the kernel method in the syntax trees
            var methodSyntax = FindKernelMethod(compilation, kernel);
            if (methodSyntax == null)
            {
                _logger.LogWarning("Could not find method {MethodName} in compilation for kernel {KernelId}",
                    kernel.Method.Name, kernel.KernelId);
                return null;
            }

            // Extract request and response type names
            var requestTypeName = ExtractSimpleTypeName(kernel.InputMessageTypeName);
            var responseTypeName = DeriveResponseTypeName(requestTypeName);

            if (string.IsNullOrEmpty(requestTypeName))
            {
                _logger.LogWarning("Could not determine request type for kernel {KernelId}", kernel.KernelId);
                return null;
            }

            _logger.LogDebug("Translating kernel {KernelId}: Request={RequestType}, Response={ResponseType}",
                kernel.KernelId, requestTypeName, responseTypeName);

            // Generate the CUDA handler function
            var cudaCode = GenerateCudaHandler(kernel, methodSyntax, requestTypeName, responseTypeName);

            if (!string.IsNullOrEmpty(cudaCode))
            {
                _logger.LogInformation(
                    "Successfully translated ring kernel handler for {KernelId} ({Length} bytes of CUDA)",
                    kernel.KernelId, cudaCode.Length);
            }

            return cudaCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception translating ring kernel handler for {KernelId}", kernel.KernelId);
            return null;
        }
    }

    /// <summary>
    /// Finds the kernel method in the compilation's syntax trees.
    /// </summary>
    private static MethodDeclarationSyntax? FindKernelMethod(Microsoft.CodeAnalysis.Compilation compilation, DiscoveredRingKernel kernel)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == kernel.Method.Name);

            foreach (var method in methods)
            {
                // Check if this method is in the right class
                var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDecl != null && classDecl.Identifier.Text == kernel.ContainingType.Name)
                {
                    return method;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the simple type name from a fully qualified name.
    /// </summary>
    private static string? ExtractSimpleTypeName(string? fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName))
        {
            return null;
        }

        var span = fullTypeName.AsSpan();
        var lastDot = span.LastIndexOf('.');
        return lastDot >= 0 ? span[(lastDot + 1)..].ToString() : fullTypeName;
    }

    /// <summary>
    /// Derives the response type name from the request type name.
    /// </summary>
    private static string DeriveResponseTypeName(string? requestTypeName)
    {
        if (string.IsNullOrEmpty(requestTypeName))
        {
            return "Response";
        }

        // VectorAddProcessorRingRequest → VectorAddProcessorRingResponse
        if (requestTypeName.EndsWith("Request", StringComparison.Ordinal))
        {
            return requestTypeName[..^7] + "Response";
        }

        return requestTypeName + "Response";
    }

    /// <summary>
    /// Generates the CUDA handler function body (not the full function signature).
    /// </summary>
    /// <remarks>
    /// The stub generator creates the function signature. This method generates only
    /// the body content to be inserted into that function:
    /// <list type="bullet">
    /// <item><description>Input validation</description></item>
    /// <item><description>Request deserialization</description></item>
    /// <item><description>Translated business logic</description></item>
    /// <item><description>Response serialization</description></item>
    /// </list>
    /// </remarks>
    private string GenerateCudaHandler(
        DiscoveredRingKernel kernel,
        MethodDeclarationSyntax methodSyntax,
        string requestTypeName,
        string responseTypeName)
    {
        var builder = new StringBuilder();

        // Indent constant - stub generator expects 4-space indentation for function body
        const string Indent = "    ";

        // Comment block indicating this is auto-translated code
        builder.AppendLine($"{Indent}// Auto-translated from C# method: {kernel.ContainingType.Name}.{kernel.Method.Name}");
        builder.AppendLine($"{Indent}// Request type: {requestTypeName}, Response type: {responseTypeName}");
        builder.AppendLine();

        // Input validation
        builder.AppendLine($"{Indent}// Validate input");
        builder.AppendLine($"{Indent}printf(\"[HANDLER] Starting handler, msg_buffer=%p, msg_size=%d\\n\", msg_buffer, msg_size);");
        builder.AppendLine($"{Indent}if (msg_buffer == nullptr || msg_size <= 0) {{");
        builder.AppendLine($"{Indent}    printf(\"[HANDLER] FAIL: Invalid input\\n\");");
        builder.AppendLine($"{Indent}    return false;");
        builder.AppendLine($"{Indent}}}");
        builder.AppendLine();

        // Deserialize request
        // NOTE: Struct names match CudaMemoryPackSerializerGenerator convention (snake_case without _t suffix)
        var requestStructName = ToSnakeCase(requestTypeName);
        builder.AppendLine($"{Indent}// Deserialize request from input buffer");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{Indent}{requestStructName} request;");
        builder.AppendLine($"{Indent}printf(\"[HANDLER] Deserializing request...\\n\");");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{Indent}if (!deserialize_{requestStructName}(msg_buffer, msg_size, &request)) {{");
        builder.AppendLine($"{Indent}    printf(\"[HANDLER] FAIL: Deserialization failed\\n\");");
        builder.AppendLine($"{Indent}    return false;");
        builder.AppendLine($"{Indent}}}");
        builder.AppendLine($"{Indent}printf(\"[HANDLER] Deserialized successfully\\n\");");
        builder.AppendLine();

        // Initialize response
        var responseStructName = ToSnakeCase(responseTypeName);
        builder.AppendLine($"{Indent}// Initialize response");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{Indent}{responseStructName} response;");
        builder.AppendLine($"{Indent}memset(&response, 0, sizeof(response));");
        builder.AppendLine($"{Indent}printf(\"[HANDLER] Response initialized, executing business logic...\\n\");");
        builder.AppendLine();

        // Translate the method body
        var translatedBody = TranslateMethodBody(methodSyntax, requestTypeName, responseTypeName);
        builder.AppendLine($"{Indent}// Translated business logic from C# method");
        // Add indentation to each line of the translated body
        foreach (var line in translatedBody.Split('\n', StringSplitOptions.None))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine($"{Indent}{line.TrimEnd('\r')}");
            }
            else
            {
                builder.AppendLine();
            }
        }

        // Serialize response
        builder.AppendLine($"{Indent}printf(\"[HANDLER] Business logic complete, serializing response...\\n\");");
        builder.AppendLine($"{Indent}// Serialize response to output buffer");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{Indent}int output_size = serialize_{responseStructName}(&response, output_buffer, {kernel.MaxOutputMessageSizeBytes});");
        builder.AppendLine($"{Indent}printf(\"[HANDLER] Serialized, output_size=%d\\n\", output_size);");
        builder.AppendLine($"{Indent}if (output_size <= 0) {{");
        builder.AppendLine($"{Indent}    printf(\"[HANDLER] FAIL: Serialization returned %d\\n\", output_size);");
        builder.AppendLine($"{Indent}    return false;");
        builder.AppendLine($"{Indent}}}");
        builder.AppendLine();
        builder.AppendLine($"{Indent}// Set output size");
        builder.AppendLine($"{Indent}if (output_size_ptr != nullptr) {{");
        builder.AppendLine($"{Indent}    *output_size_ptr = output_size;");
        builder.AppendLine($"{Indent}}}");
        builder.AppendLine($"{Indent}printf(\"[HANDLER] SUCCESS! Returning true\\n\");");
        builder.AppendLine();
        builder.AppendLine($"{Indent}return true;");

        return builder.ToString();
    }

    /// <summary>
    /// Translates the C# method body to CUDA code.
    /// </summary>
    private string TranslateMethodBody(
        MethodDeclarationSyntax methodSyntax,
        string requestTypeName,
        string responseTypeName)
    {
        var body = methodSyntax.Body;
        if (body == null)
        {
            _logger.LogWarning("Method has no body (expression-bodied?)");
            return "    // Method body translation failed\n    return false;";
        }

        var builder = new StringBuilder();

        foreach (var statement in body.Statements)
        {
            var translatedStatement = TranslateStatement(statement, requestTypeName, responseTypeName);
            if (!string.IsNullOrEmpty(translatedStatement))
            {
                builder.AppendLine(translatedStatement);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Translates a single C# statement to CUDA.
    /// </summary>
    private string TranslateStatement(
        StatementSyntax statement,
        string requestTypeName,
        string responseTypeName)
    {
        return statement switch
        {
            LocalDeclarationStatementSyntax localDecl => TranslateLocalDeclaration(localDecl, requestTypeName),
            ExpressionStatementSyntax exprStmt => TranslateExpressionStatement(exprStmt, requestTypeName, responseTypeName),
            IfStatementSyntax ifStmt => TranslateIfStatement(ifStmt, requestTypeName, responseTypeName),
            BlockSyntax block => TranslateBlock(block, requestTypeName, responseTypeName),
            _ => $"    // Unsupported statement: {statement.Kind()}"
        };
    }

    /// <summary>
    /// Translates a local variable declaration.
    /// </summary>
    private static string TranslateLocalDeclaration(LocalDeclarationStatementSyntax localDecl, string requestTypeName)
    {
        var declaration = localDecl.Declaration;
        var cudaType = MapCSharpTypeToCuda(declaration.Type.ToString());
        var builder = new StringBuilder();

        foreach (var variable in declaration.Variables)
        {
            var varName = variable.Identifier.Text;

            // Special handling for 'response' variable - it's already declared at the top of the handler
            // So we only need to emit the property assignments from the object initializer, not a new declaration
            if (string.Equals(varName, "response", StringComparison.Ordinal) &&
                variable.Initializer?.Value is ObjectCreationExpressionSyntax objectCreation)
            {
                // Emit property assignments from object initializer
                if (objectCreation.Initializer != null)
                {
                    foreach (var expr in objectCreation.Initializer.Expressions)
                    {
                        if (expr is AssignmentExpressionSyntax assignment)
                        {
                            var originalFieldName = assignment.Left.ToString();
                            var fieldName = ToSnakeCase(originalFieldName);

                            // Handle Guid fields (arrays in CUDA - use memcpy)
                            if (originalFieldName.Equals("MessageId", StringComparison.Ordinal))
                            {
                                if (assignment.Right is MemberAccessExpressionSyntax rightMember)
                                {
                                    var rightFieldName = ToSnakeCase(rightMember.Name.ToString());
                                    var rightObjName = rightMember.Expression.ToString();
                                    builder.AppendLine(CultureInfo.InvariantCulture, $"    memcpy(response.{fieldName}, {rightObjName}.{rightFieldName}, 16);");
                                }
                                else
                                {
                                    var fieldValue = TranslateExpression(assignment.Right, requestTypeName);
                                    builder.AppendLine(CultureInfo.InvariantCulture, $"    memcpy(response.{fieldName}, {fieldValue}, 16);");
                                }
                                continue;
                            }

                            // Handle Nullable<Guid> fields (nested structs in CUDA - use memcpy)
                            if (originalFieldName.Equals("CorrelationId", StringComparison.Ordinal))
                            {
                                if (assignment.Right is MemberAccessExpressionSyntax rightMember)
                                {
                                    var rightFieldName = ToSnakeCase(rightMember.Name.ToString());
                                    var rightObjName = rightMember.Expression.ToString();
                                    builder.AppendLine(CultureInfo.InvariantCulture, $"    memcpy(&response.{fieldName}, &{rightObjName}.{rightFieldName}, sizeof(response.{fieldName}));");
                                }
                                else
                                {
                                    var fieldValue = TranslateExpression(assignment.Right, requestTypeName);
                                    builder.AppendLine(CultureInfo.InvariantCulture, $"    memcpy(&response.{fieldName}, &({fieldValue}), sizeof(response.{fieldName}));");
                                }
                                continue;
                            }

                            // Standard field assignment
                            var value = TranslateExpression(assignment.Right, requestTypeName);
                            builder.AppendLine(CultureInfo.InvariantCulture, $"    response.{fieldName} = {value};");
                        }
                    }
                }
                continue;
            }

            if (variable.Initializer != null)
            {
                var initValue = TranslateExpression(variable.Initializer.Value, requestTypeName);
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {cudaType} {varName} = {initValue};");
            }
            else
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {cudaType} {varName};");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Translates an expression statement.
    /// </summary>
    private static string TranslateExpressionStatement(
        ExpressionStatementSyntax exprStmt,
        string requestTypeName,
        string responseTypeName)
    {
        var expr = exprStmt.Expression;

        // Handle ctx.SyncThreads() → __syncthreads()
        if (expr is InvocationExpressionSyntax invocation)
        {
            var invokeText = invocation.ToString();

            if (invokeText.Contains("SyncThreads", StringComparison.Ordinal))
            {
                return "    __syncthreads();";
            }

            if (invokeText.Contains("ThreadFence", StringComparison.Ordinal))
            {
                return "    __threadfence();";
            }

            // Handle ctx.EnqueueOutput(response) - this is handled by the response serialization
            if (invokeText.Contains("EnqueueOutput", StringComparison.Ordinal))
            {
                return "    // EnqueueOutput handled by response serialization";
            }
        }

        // Handle assignment expressions
        if (expr is AssignmentExpressionSyntax assignment)
        {
            return TranslateAssignment(assignment, requestTypeName, responseTypeName);
        }

        return $"    // Expression: {expr}";
    }

    /// <summary>
    /// Translates an assignment expression.
    /// </summary>
    private static string TranslateAssignment(
        AssignmentExpressionSyntax assignment,
        string requestTypeName,
        string responseTypeName)
    {
        var left = TranslateExpression(assignment.Left, requestTypeName);
        var right = TranslateExpression(assignment.Right, requestTypeName);

        // Check if assigning to response fields
        if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
        {
            var objectName = memberAccess.Expression.ToString();
            if (objectName == "response" || objectName.EndsWith("response", StringComparison.Ordinal))
            {
                var fieldName = ToSnakeCase(memberAccess.Name.ToString());
                var originalFieldName = memberAccess.Name.ToString();

                // Handle Guid fields (arrays in CUDA - use memcpy)
                // These are serialized as uint8_t field_name[16] in CUDA
                if (originalFieldName.Equals("MessageId", StringComparison.Ordinal))
                {
                    // Check if right side is also a field access (e.g., request.MessageId)
                    if (assignment.Right is MemberAccessExpressionSyntax rightMember)
                    {
                        var rightFieldName = ToSnakeCase(rightMember.Name.ToString());
                        var rightObjName = rightMember.Expression.ToString();
                        return $"    memcpy(response.{fieldName}, {rightObjName}.{rightFieldName}, 16);";
                    }
                    // Otherwise, use memcpy with value
                    return $"    memcpy(response.{fieldName}, {right}, 16);";
                }

                // Handle Nullable<Guid> fields (nested structs in CUDA - use memcpy)
                // These are serialized as struct { uint8_t has_value; uint8_t value[16]; } field_name
                if (originalFieldName.Equals("CorrelationId", StringComparison.Ordinal))
                {
                    // Check if right side is also a field access (e.g., request.CorrelationId)
                    if (assignment.Right is MemberAccessExpressionSyntax rightMember)
                    {
                        var rightFieldName = ToSnakeCase(rightMember.Name.ToString());
                        var rightObjName = rightMember.Expression.ToString();
                        return $"    memcpy(&response.{fieldName}, &{rightObjName}.{rightFieldName}, sizeof(response.{fieldName}));";
                    }
                    // Otherwise, use memcpy with address of value
                    return $"    memcpy(&response.{fieldName}, &({right}), sizeof(response.{fieldName}));";
                }

                return $"    response.{fieldName} = {right};";
            }
        }

        return $"    {left} = {right};";
    }

    /// <summary>
    /// Translates an if statement.
    /// </summary>
    private string TranslateIfStatement(
        IfStatementSyntax ifStmt,
        string requestTypeName,
        string responseTypeName)
    {
        var builder = new StringBuilder();
        var condition = TranslateExpression(ifStmt.Condition, requestTypeName);

        builder.AppendLine(CultureInfo.InvariantCulture, $"    if ({condition}) {{");

        // Translate the true branch
        if (ifStmt.Statement is BlockSyntax trueBlock)
        {
            foreach (var stmt in trueBlock.Statements)
            {
                var translated = TranslateStatement(stmt, requestTypeName, responseTypeName);
                // Add extra indentation
                builder.AppendLine("    " + translated.TrimStart());
            }
        }
        else
        {
            var translated = TranslateStatement(ifStmt.Statement, requestTypeName, responseTypeName);
            builder.AppendLine("    " + translated.TrimStart());
        }

        builder.Append("    }");

        // Handle else clause
        if (ifStmt.Else != null)
        {
            builder.AppendLine();
            builder.AppendLine("    else {");

            if (ifStmt.Else.Statement is BlockSyntax elseBlock)
            {
                foreach (var stmt in elseBlock.Statements)
                {
                    var translated = TranslateStatement(stmt, requestTypeName, responseTypeName);
                    builder.AppendLine("    " + translated.TrimStart());
                }
            }
            else if (ifStmt.Else.Statement is IfStatementSyntax elseIfStmt)
            {
                // else if chain
                var elseIfTranslated = TranslateIfStatement(elseIfStmt, requestTypeName, responseTypeName);
                builder.Append(elseIfTranslated);
            }
            else
            {
                var translated = TranslateStatement(ifStmt.Else.Statement, requestTypeName, responseTypeName);
                builder.AppendLine("    " + translated.TrimStart());
            }

            builder.Append("    }");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Translates a block statement.
    /// </summary>
    private string TranslateBlock(
        BlockSyntax block,
        string requestTypeName,
        string responseTypeName)
    {
        var builder = new StringBuilder();
        builder.AppendLine("    {");

        foreach (var stmt in block.Statements)
        {
            var translated = TranslateStatement(stmt, requestTypeName, responseTypeName);
            builder.AppendLine("    " + translated.TrimStart());
        }

        builder.Append("    }");
        return builder.ToString();
    }

    /// <summary>
    /// Translates a C# expression to CUDA.
    /// </summary>
    private static string TranslateExpression(ExpressionSyntax expr, string requestTypeName)
    {
        return expr switch
        {
            MemberAccessExpressionSyntax memberAccess => TranslateMemberAccess(memberAccess),
            BinaryExpressionSyntax binary => TranslateBinaryExpression(binary, requestTypeName),
            LiteralExpressionSyntax literal => TranslateLiteral(literal),
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            ParenthesizedExpressionSyntax paren => $"({TranslateExpression(paren.Expression, requestTypeName)})",
            ConditionalExpressionSyntax conditional => TranslateConditional(conditional, requestTypeName),
            PrefixUnaryExpressionSyntax prefix => TranslatePrefixUnary(prefix, requestTypeName),
            CastExpressionSyntax cast => TranslateCast(cast, requestTypeName),
            ObjectCreationExpressionSyntax objectCreation => TranslateObjectCreation(objectCreation),
            _ => expr.ToString()
        };
    }

    /// <summary>
    /// Translates member access (request.A0 → request.a0).
    /// </summary>
    private static string TranslateMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var objectName = memberAccess.Expression.ToString();
        var memberName = memberAccess.Name.ToString();

        // Handle request.Property → request.property (snake_case for CUDA structs)
        if (objectName == "request" || objectName.EndsWith("request", StringComparison.OrdinalIgnoreCase))
        {
            return $"request.{ToSnakeCase(memberName)}";
        }

        // Handle response.Property → response.property
        if (objectName == "response" || objectName.EndsWith("response", StringComparison.OrdinalIgnoreCase))
        {
            return $"response.{ToSnakeCase(memberName)}";
        }

        return $"{objectName}.{ToSnakeCase(memberName)}";
    }

    /// <summary>
    /// Translates a binary expression.
    /// </summary>
    private static string TranslateBinaryExpression(BinaryExpressionSyntax binary, string requestTypeName)
    {
        var left = TranslateExpression(binary.Left, requestTypeName);
        var right = TranslateExpression(binary.Right, requestTypeName);
        var op = binary.OperatorToken.Text;

        return $"{left} {op} {right}";
    }

    /// <summary>
    /// Translates a literal expression.
    /// </summary>
    private static string TranslateLiteral(LiteralExpressionSyntax literal)
    {
        var text = literal.Token.Text;

        // Handle float literals (0.0f → 0.0f)
        if (literal.Kind() == SyntaxKind.NumericLiteralExpression)
        {
            return text;
        }

        return text;
    }

    /// <summary>
    /// Translates a conditional expression (ternary operator).
    /// </summary>
    private static string TranslateConditional(ConditionalExpressionSyntax conditional, string requestTypeName)
    {
        var condition = TranslateExpression(conditional.Condition, requestTypeName);
        var whenTrue = TranslateExpression(conditional.WhenTrue, requestTypeName);
        var whenFalse = TranslateExpression(conditional.WhenFalse, requestTypeName);

        return $"({condition}) ? {whenTrue} : {whenFalse}";
    }

    /// <summary>
    /// Translates a prefix unary expression.
    /// </summary>
    private static string TranslatePrefixUnary(PrefixUnaryExpressionSyntax prefix, string requestTypeName)
    {
        var operand = TranslateExpression(prefix.Operand, requestTypeName);
        var op = prefix.OperatorToken.Text;

        return $"{op}{operand}";
    }

    /// <summary>
    /// Translates a cast expression.
    /// </summary>
    private static string TranslateCast(CastExpressionSyntax cast, string requestTypeName)
    {
        var type = MapCSharpTypeToCuda(cast.Type.ToString());
        var expr = TranslateExpression(cast.Expression, requestTypeName);

        return $"({type}){expr}";
    }

    /// <summary>
    /// Translates an object creation expression (new T { ... }).
    /// </summary>
    private static string TranslateObjectCreation(ObjectCreationExpressionSyntax objectCreation)
    {
        // For response creation, we just return a placeholder
        // The actual initialization is handled by property assignments
        return "/* response initialized above */";
    }

    /// <summary>
    /// Maps C# types to CUDA types.
    /// </summary>
    private static string MapCSharpTypeToCuda(string csharpType)
    {
        return csharpType switch
        {
            "int" => "int32_t",
            "uint" => "uint32_t",
            "long" => "int64_t",
            "ulong" => "uint64_t",
            "short" => "int16_t",
            "ushort" => "uint16_t",
            "byte" => "uint8_t",
            "sbyte" => "int8_t",
            "float" => "float",
            "double" => "double",
            "bool" => "bool",
            "var" => "auto",
            _ => csharpType
        };
    }

    /// <summary>
    /// Converts PascalCase to snake_case.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                // Don't add underscore if previous char was also uppercase (acronym)
                // Or if previous char was already underscore
                if (!char.IsUpper(input[i - 1]) && input[i - 1] != '_')
                {
                    builder.Append('_');
                }
            }
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
