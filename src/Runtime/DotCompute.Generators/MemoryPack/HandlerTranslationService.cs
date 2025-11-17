// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Text;
using DotCompute.Generators.Kernel;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.MemoryPack;

/// <summary>
/// Service for discovering and translating C# message handlers to CUDA C device functions.
/// </summary>
/// <remarks>
/// <para>
/// This service implements the C# to CUDA translation pipeline for ring kernel message handlers:
/// 1. Discovers handler classes by naming convention (e.g., VectorAddHandler for VectorAddRequest)
/// 2. Validates handler method signatures (ProcessMessage with correct parameters)
/// 3. Uses CSharpToCudaTranslator to convert C# method body to CUDA C
/// 4. Wraps translated code in __device__ function with proper signature
/// </para>
/// <para>
/// <b>Handler Convention:</b>
/// - Handler class name: {MessageType}Handler (e.g., VectorAddHandler)
/// - Handler method: static bool ProcessMessage(Span&lt;byte&gt; inputBuffer, int inputSize, Span&lt;byte&gt; outputBuffer, int outputSize)
/// - Returns: true on success, false on failure
/// </para>
/// <para>
/// <b>CUDA Output:</b>
/// <code>
/// __device__ bool process_vector_add_message(
///     unsigned char* input_buffer,
///     int32_t input_size,
///     unsigned char* output_buffer,
///     int32_t output_size)
/// {
///     // Translated C# code...
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class HandlerTranslationService
{
    /// <summary>
    /// Discovers and translates a message handler for the specified message type.
    /// </summary>
    /// <param name="messageTypeName">Simple name of the message type (e.g., "VectorAddRequest").</param>
    /// <param name="compilation">Compilation context for type discovery.</param>
    /// <returns>Translated CUDA device function, or null if no handler found.</returns>
    public static string? TranslateHandler(string messageTypeName, Compilation compilation)
    {
        if (string.IsNullOrWhiteSpace(messageTypeName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(messageTypeName));
        }

        if (compilation == null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        // Derive handler class name by convention
        // Remove "Request" or "Response" suffix if present, add "Handler"
        var baseName = messageTypeName
            .Replace("Request", "")
            .Replace("Response", "")
            .Replace("Message", "");
        var handlerClassName = $"{baseName}Handler";

        // Search for handler class in compilation
        var handlerSymbol = FindHandlerClass(compilation, handlerClassName);
        if (handlerSymbol == null)
        {
            return null; // No handler found - this is optional
        }

        // Find ProcessMessage method
        var processMessageMethod = FindProcessMessageMethod(handlerSymbol);
        if (processMessageMethod == null)
        {
            return null; // No valid ProcessMessage method
        }

        // Get syntax tree and semantic model
        var syntaxTree = processMessageMethod.Locations.FirstOrDefault()?.SourceTree;
        if (syntaxTree == null)
        {
            return null;
        }

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var methodSyntax = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "ProcessMessage");

        if (methodSyntax == null)
        {
            return null;
        }

        // Translate method body using CSharpToCudaTranslator
        var kernelInfo = new KernelMethodInfo
        {
            Name = "ProcessMessage",
            MethodDeclaration = methodSyntax,
            ReturnType = processMessageMethod.ReturnType.ToDisplayString(),
            ContainingType = handlerSymbol.ToDisplayString(),
            Namespace = handlerSymbol.ContainingNamespace.ToDisplayString()
        };

        var translator = new CSharpToCudaTranslator(semanticModel, kernelInfo);
        var translatedBody = translator.TranslateMethodBody();

        // Generate CUDA function wrapper
        var cudaFunctionName = $"process_{ToSnakeCase(baseName)}_message";
        return WrapInCudaDeviceFunction(cudaFunctionName, translatedBody);
    }

    /// <summary>
    /// Finds a handler class by name in the compilation.
    /// </summary>
    private static INamedTypeSymbol? FindHandlerClass(Compilation compilation, string className)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration != null)
            {
                if (semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol symbol && symbol.IsStatic)
                {
                    return symbol;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the ProcessMessage method in a handler class.
    /// </summary>
    private static IMethodSymbol? FindProcessMessageMethod(INamedTypeSymbol handlerClass)
    {
        var method = handlerClass.GetMembers("ProcessMessage")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.IsStatic &&
                m.Parameters.Length == 4 &&
                m.ReturnType.SpecialType == SpecialType.System_Boolean);

        return method;
    }

    /// <summary>
    /// Wraps translated C# code in a CUDA __device__ function.
    /// </summary>
    private static string WrapInCudaDeviceFunction(string functionName, string translatedBody)
    {
        var sb = new StringBuilder();

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Auto-translated message handler: {functionName}");
        sb.AppendLine("/// Original C# method translated by CSharpToCudaTranslator");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"__device__ bool {functionName}(");
        sb.AppendLine("    unsigned char* input_buffer,");
        sb.AppendLine("    int32_t input_size,");
        sb.AppendLine("    unsigned char* output_buffer,");
        sb.AppendLine("    int32_t output_size)");
        sb.AppendLine("{");

        // Add translated body with proper indentation
        foreach (var line in translatedBody.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                sb.Append("    ");
                sb.AppendLine(line.TrimEnd());
            }
            else
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        sb.AppendLine();

        return sb.ToString();
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

        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (i > 0 && char.IsUpper(input[i]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(input[i]));
        }

        return sb.ToString();
    }
}
