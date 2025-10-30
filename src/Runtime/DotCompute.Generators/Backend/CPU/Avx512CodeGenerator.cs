// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend.CPU;

/// <summary>
/// Generates AVX-512 specific optimized code for latest x86/x64 processors.
/// </summary>
public class Avx512CodeGenerator(
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
            "AVX-512 optimized implementation for latest x86/x64 processors.",
            "Uses 512-bit vector operations for maximum throughput.");


        GenerateMethodSignature(sb, "ExecuteAvx512", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateAvx512MethodContent(sb));
    }

    #endregion

    #region Private Methods


    private void GenerateAvx512MethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine($"            const int vectorSize = {Avx512VectorSize}; // 512-bit / 32-bit");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();


        GenerateAvx512ProcessingLoop(sb);
        GenerateRemainderHandling(sb, "ExecuteAvx2");
    }


    private void GenerateAvx512ProcessingLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Process AVX-512 vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        GenerateAvx512Operations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
    }


    private void GenerateAvx512Operations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // AVX-512 512-bit vector operations");
        _ = sb.AppendLine("                unsafe");
        _ = sb.AppendLine("                {");


        if (VectorizationInfo.IsArithmetic)
        {
            GenerateAvx512ArithmeticOperations(sb);
        }
        else if (VectorizationInfo.IsMemoryOperation)
        {
            GenerateAvx512MemoryOperations(sb);
        }
        else
        {
            GenerateAvx512GenericOperations(sb);
        }


        _ = sb.AppendLine("                }");
    }


    private static void GenerateAvx512ArithmeticOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX-512 arithmetic operations");
        _ = sb.AppendLine("                    fixed (float* pInput1 = &input1[i], pInput2 = &input2[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec1 = Avx512F.LoadVector512(pInput1);");
        _ = sb.AppendLine("                        var vec2 = Avx512F.LoadVector512(pInput2);");
        _ = sb.AppendLine("                        var result = Avx512F.Add(vec1, vec2);");
        _ = sb.AppendLine("                        Avx512F.Store(pOutput, result);");
        _ = sb.AppendLine("                    }");
    }


    private static void GenerateAvx512MemoryOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX-512 memory operations");
        _ = sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec = Avx512F.LoadVector512(pInput);");
        _ = sb.AppendLine("                        Avx512F.Store(pOutput, vec);");
        _ = sb.AppendLine("                    }");
    }


    private static void GenerateAvx512GenericOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX-512 generic operations");
        _ = sb.AppendLine("                    fixed (float* pData = &data[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec = Avx512F.LoadVector512(pData);");
        _ = sb.AppendLine("                        // Process vector with AVX-512 intrinsics");
        _ = sb.AppendLine("                        var processed = ProcessAvx512Vector(vec);");
        _ = sb.AppendLine("                        Avx512F.Store(pOutput, processed);");
        _ = sb.AppendLine("                    }");
    }


    #endregion
}
