// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend.CPU;

/// <summary>
/// Generates the main execution strategy that selects the best implementation at runtime.
/// </summary>
public class ExecutionStrategyGenerator(
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
        GenerateMainExecuteMethod(sb);
        GenerateConvenienceOverload(sb);
    }

    #endregion

    #region Private Methods


    private void GenerateMainExecuteMethod(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "Main execution method that selects the best implementation.",
            "Automatically chooses between scalar, SIMD, AVX2, and AVX-512 based on hardware support and data size.");


        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        public static void Execute(");
        _ = sb.Append(string.Join(", ", Parameters.Select(p => p.Declaration)));
        _ = sb.AppendLine(", int start, int end)");
        _ = sb.AppendLine("        {");


        GenerateImplementationSelection(sb);


        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }


    private void GenerateImplementationSelection(StringBuilder sb)
    {
        _ = sb.AppendLine("            int length = end - start;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Select best implementation based on hardware support and data size");

        // Small arrays - use scalar

        _ = sb.AppendLine($"            if (length < {SmallArrayThreshold})");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Small arrays - use scalar for better cache efficiency");
        GenerateMethodCall(sb, "ExecuteScalar", includeRange: true);
        _ = sb.AppendLine("            }");


        if (VectorizationInfo.IsVectorizable)
        {
            // AVX-512 path
            _ = sb.AppendLine($"            else if (Avx512F.IsSupported && length >= {MinAvx512Size})");
            _ = sb.AppendLine("            {");
            _ = sb.AppendLine("                // Use AVX-512 for maximum throughput on supported hardware");
            GenerateMethodCall(sb, "ExecuteAvx512", includeRange: true);
            _ = sb.AppendLine("            }");

            // AVX2 path

            _ = sb.AppendLine($"            else if (Avx2.IsSupported && length >= {MinVectorSize})");
            _ = sb.AppendLine("            {");
            _ = sb.AppendLine("                // Use AVX2 for good performance on modern x86/x64");
            GenerateMethodCall(sb, "ExecuteAvx2", includeRange: true);
            _ = sb.AppendLine("            }");

            // Generic SIMD path

            _ = sb.AppendLine("            else if (Vector.IsHardwareAccelerated)");
            _ = sb.AppendLine("            {");
            _ = sb.AppendLine("                // Use platform-agnostic SIMD");
            GenerateMethodCall(sb, "ExecuteSimd", includeRange: true);
            _ = sb.AppendLine("            }");
        }

        // Fallback to scalar

        _ = sb.AppendLine("            else");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Fallback to scalar implementation");
        GenerateMethodCall(sb, "ExecuteScalar", includeRange: true);
        _ = sb.AppendLine("            }");
    }


    private void GenerateConvenienceOverload(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "Convenience overload for full array processing.",
            "Processes the entire array from index 0 to length.");


        _ = sb.Append($"        public static void Execute(");
        _ = sb.Append(string.Join(", ", Parameters.Select(p => p.Declaration)));
        _ = sb.AppendLine(", int length)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            Execute(");
        _ = sb.Append(string.Join(", ", Parameters.Select(p => p.Name)));
        _ = sb.AppendLine(", 0, length);");
        _ = sb.AppendLine("        }");
    }


    private void GenerateMethodCall(StringBuilder sb, string methodName, bool includeRange)
    {
        _ = sb.AppendLine($"                {methodName}(");
        _ = sb.Append(string.Join(", ", Parameters.Select(p => p.Name)));


        if (includeRange)
        {
            _ = sb.AppendLine(", start, end);");
        }
        else
        {
            _ = sb.AppendLine(");");
        }
    }


    #endregion
}
