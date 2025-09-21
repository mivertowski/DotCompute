using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Compilation.Stages.Interfaces;
using OperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;

namespace DotCompute.Linq.Compilation.Stages.Operators;
/// <summary>
/// Generates CUDA code for where operations.
/// </summary>
internal class CudaWhereOperatorGenerator : ICudaOperatorGenerator
{
    public void Generate(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("        // CUDA where operation");
        builder.AppendLine("        if (input[i] > 0) output[i] = input[i]; else output[i] = 0; // Placeholder condition");
    }
}
