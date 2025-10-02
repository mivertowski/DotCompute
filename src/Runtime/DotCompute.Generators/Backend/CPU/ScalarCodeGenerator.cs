// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using DotCompute.Generators.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend.CPU;

/// <summary>
/// Generates scalar implementation for kernel execution.
/// Handles non-vectorizable code and remainder processing.
/// </summary>
public class ScalarCodeGenerator(
    string methodName,
    IReadOnlyList<KernelParameter> parameters,
    MethodDeclarationSyntax methodSyntax,
    VectorizationInfo vectorizationInfo) : CpuCodeGeneratorBase(methodName, parameters, methodSyntax, vectorizationInfo)
{

    #region Constructor

    #endregion

    #region Public Methods


    public override void Generate(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "Scalar implementation for compatibility and remainder handling.",
            "This implementation is used for small arrays and processors without SIMD support.");


        GenerateMethodSignature(sb, "ExecuteScalar", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateScalarMethodContent(sb));
    }

    #endregion

    #region Private Methods


    private void GenerateScalarMethodContent(StringBuilder sb)
    {
        // Generate parameter validation
        var validation = ParameterValidator.GenerateParameterValidation(Parameters);
        if (!string.IsNullOrEmpty(validation))
        {
            _ = sb.Append(CodeFormatter.Indent(validation, MethodBodyIndentLevel));
        }

        // Generate scalar processing loop

        var methodBody = MethodBodyExtractor.ExtractMethodBody(MethodSyntax);
        if (!string.IsNullOrEmpty(methodBody))
        {
            GenerateTransformedScalarLoop(sb, methodBody!);
        }
        else
        {
            GenerateDefaultScalarLoop(sb);
        }
    }


    private static void GenerateTransformedScalarLoop(StringBuilder sb, string methodBody)
    {
        _ = sb.AppendLine("            // Transformed scalar implementation:");
        var transformedBody = TransformMethodBodyForScalar(methodBody);
        _ = sb.AppendLine(transformedBody);
    }


    private void GenerateDefaultScalarLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Default scalar implementation based on operation type");
        _ = sb.AppendLine("            for (int i = start; i < end; i++)");
        _ = sb.AppendLine("            {");


        if (VectorizationInfo.IsArithmetic)
        {
            _ = sb.AppendLine("                // Perform arithmetic operation on elements");
            _ = sb.AppendLine("                output[i] = ProcessArithmetic(input1[i], input2[i]);");
        }
        else if (VectorizationInfo.IsMemoryOperation)
        {
            _ = sb.AppendLine("                // Perform memory operation");
            _ = sb.AppendLine("                output[i] = input[i];");
        }
        else
        {
            _ = sb.AppendLine("                // Generic element processing");
            _ = sb.AppendLine("                ProcessElement(i);");
        }


        _ = sb.AppendLine("            }");
    }


    private static string TransformMethodBodyForScalar(string methodBody)
    {
        if (string.IsNullOrEmpty(methodBody))
        {
            return string.Empty;
        }


        var transformedBody = methodBody
            .Replace("{", "")
            .Replace("}", "")
            .Trim();

        // Handle common patterns

        if (transformedBody.Contains("for") && transformedBody.Contains("++"))
        {
            // Already has loop structure - adapt for range
            return $"            for (int i = start; i < end; i++)\n            {{\n                {transformedBody.Replace("i++", "").Trim()}\n            }}";
        }
        else if (transformedBody.Contains("[") && transformedBody.Contains("]"))
        {
            // Array access pattern - adapt for indexed operation
            return $"            for (int i = start; i < end; i++)\n            {{\n                // Process element at index i\n                {transformedBody}\n            }}";
        }
        else
        {
            // Generic operation
            return $"            for (int i = start; i < end; i++)\n            {{\n                {transformedBody}\n            }}";
        }
    }


    #endregion
}