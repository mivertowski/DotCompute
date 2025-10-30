// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend.CPU;

/// <summary>
/// Generates parallel implementation using task parallelism for multi-core execution.
/// </summary>
public class ParallelCodeGenerator(
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
            "Parallel implementation using task parallelism.",
            "Automatically partitions work across available CPU cores.");


        GenerateMethodSignature(sb, "ExecuteParallel", false, includeRange: false, includeLength: true);
        GenerateMethodBody(sb, () => GenerateParallelMethodContent(sb));
    }

    #endregion

    #region Private Methods


    private void GenerateParallelMethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine("            int processorCount = Environment.ProcessorCount;");
        _ = sb.AppendLine($"            int chunkSize = Math.Max({DefaultChunkSize}, length / processorCount);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            Parallel.ForEach(Partitioner.Create(0, length, chunkSize), range =>");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                Execute(");
        _ = sb.Append(string.Join(", ", Parameters.Select(p => p.Name)));
        _ = sb.AppendLine(", range.Item1, range.Item2);");
        _ = sb.AppendLine("            });");
    }


    #endregion
}
