// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend.CPU;

/// <summary>
/// Generates platform-agnostic SIMD code using System.Numerics.Vector.
/// </summary>
public class PlatformSimdCodeGenerator : CpuCodeGeneratorBase
{
    #region Constructor
    
    public PlatformSimdCodeGenerator(
        string methodName,
        IReadOnlyList<KernelParameter> parameters,
        MethodDeclarationSyntax methodSyntax,
        VectorizationInfo vectorizationInfo)
        : base(methodName, parameters, methodSyntax, vectorizationInfo)
    {
    }
    
    #endregion
    
    #region Public Methods
    
    public override void Generate(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "SIMD implementation using platform-agnostic vectors.",
            "Works on any platform with hardware vector support.");
        
        GenerateMethodSignature(sb, "ExecuteSimd", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateSimdMethodContent(sb));
    }
    
    #endregion
    
    #region Private Methods
    
    private void GenerateSimdMethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine($"            int vectorSize = Vector<float>.Count;");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();
        
        GenerateSimdProcessingLoop(sb);
        GenerateRemainderHandling(sb, "ExecuteScalar");
    }
    
    private void GenerateSimdProcessingLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Process vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        GenerateSimdOperations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
    }
    
    private void GenerateSimdOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Optimized SIMD vector processing");
        
        if (VectorizationInfo.IsArithmetic)
        {
            GenerateSimdArithmeticOperations(sb);
        }
        else if (VectorizationInfo.IsMemoryOperation)
        {
            GenerateSimdMemoryOperations(sb);
        }
        else
        {
            GenerateGenericSimdOperations(sb);
        }
    }
    
    private void GenerateSimdArithmeticOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Load vectors for arithmetic operation");
        _ = sb.AppendLine("                var vec1 = new Vector<float>(input1, i);");
        _ = sb.AppendLine("                var vec2 = new Vector<float>(input2, i);");
        _ = sb.AppendLine("                var result = Vector.Add(vec1, vec2);");
        _ = sb.AppendLine("                result.CopyTo(output, i);");
    }
    
    private void GenerateSimdMemoryOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Vectorized memory copy");
        _ = sb.AppendLine("                var vec = new Vector<float>(input, i);");
        _ = sb.AppendLine("                vec.CopyTo(output, i);");
    }
    
    private void GenerateGenericSimdOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Generic vector processing");
        _ = sb.AppendLine("                var vec = new Vector<float>(data, i);");
        _ = sb.AppendLine("                var processed = ProcessVector(vec);");
        _ = sb.AppendLine("                processed.CopyTo(output, i);");
    }
    
    #endregion
}