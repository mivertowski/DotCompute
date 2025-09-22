using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Compilation.Stages.Interfaces;
using OperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;

namespace DotCompute.Linq.Compilation.Stages.Operators;
/// <summary>
/// Generates CUDA code for select operations.
/// </summary>
internal class CudaSelectOperatorGenerator : ICudaOperatorGenerator
{
    public void Generate(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("        // CUDA select operation");
        builder.AppendLine("        output[i] = input[i] * 2.0f; // Placeholder transformation");
    }
}
