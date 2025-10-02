// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend.CPU;

/// <summary>
/// Generates AVX2-specific optimized code for x86/x64 processors.
/// </summary>
public class Avx2CodeGenerator(
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
            "AVX2 optimized implementation for x86/x64 processors.",
            "Uses 256-bit vector operations for improved performance.");


        GenerateMethodSignature(sb, "ExecuteAvx2", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateAvx2MethodContent(sb));
    }

    #endregion

    #region Private Methods


    private void GenerateAvx2MethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine($"            const int vectorSize = {Avx2VectorSize}; // 256-bit / 32-bit");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();


        GenerateAvx2ProcessingLoop(sb);
        GenerateRemainderHandling(sb, "ExecuteScalar");
    }


    private void GenerateAvx2ProcessingLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Process AVX2 vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        GenerateAvx2Operations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
    }


    private void GenerateAvx2Operations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // AVX2 256-bit vector operations");
        _ = sb.AppendLine("                unsafe");
        _ = sb.AppendLine("                {");


        if (VectorizationInfo.IsArithmetic)
        {
            GenerateAvx2ArithmeticOperations(sb);
        }
        else if (VectorizationInfo.IsMemoryOperation)
        {
            GenerateAvx2MemoryOperations(sb);
        }
        else
        {
            GenerateAvx2GenericOperations(sb);
        }


        _ = sb.AppendLine("                }");
    }


    private static void GenerateAvx2ArithmeticOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX2 arithmetic operations");
        _ = sb.AppendLine("                    fixed (float* pInput1 = &input1[i], pInput2 = &input2[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec1 = Avx.LoadVector256(pInput1);");
        _ = sb.AppendLine("                        var vec2 = Avx.LoadVector256(pInput2);");
        _ = sb.AppendLine("                        var result = Avx.Add(vec1, vec2);");
        _ = sb.AppendLine("                        Avx.Store(pOutput, result);");
        _ = sb.AppendLine("                    }");
    }


    private static void GenerateAvx2MemoryOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX2 memory operations");
        _ = sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec = Avx.LoadVector256(pInput);");
        _ = sb.AppendLine("                        Avx.Store(pOutput, vec);");
        _ = sb.AppendLine("                    }");
    }


    private static void GenerateAvx2GenericOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX2 generic operations");
        _ = sb.AppendLine("                    fixed (float* pData = &data[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec = Avx.LoadVector256(pData);");
        _ = sb.AppendLine("                        // Process vector with AVX2 intrinsics");
        _ = sb.AppendLine("                        var processed = ProcessAvx2Vector(vec);");
        _ = sb.AppendLine("                        Avx.Store(pOutput, processed);");
        _ = sb.AppendLine("                    }");
    }


    #endregion
}