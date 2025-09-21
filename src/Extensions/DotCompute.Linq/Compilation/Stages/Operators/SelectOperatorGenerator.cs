using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Compilation.Stages.Interfaces;
using OperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;

namespace DotCompute.Linq.Compilation.Stages.Operators;
/// <summary>
/// Generates code for select/map operations.
/// </summary>
internal class SelectOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
    {
        // Generate SIMD vectorized select operations
        builder.AppendLine("                // Vectorized select operation");
        builder.AppendLine("                var inputVector = new Vector<float>(input, i);");
        builder.AppendLine("                var resultVector = Vector.Multiply(inputVector, 2.0f); // Placeholder transformation");
        builder.AppendLine("                resultVector.CopyTo(output, i);");
    }
    public void GenerateScalar(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
        // Generate scalar select operations
        builder.AppendLine("                // Scalar select operation");
        builder.AppendLine("                output[i] = input[i] * 2.0f; // Placeholder transformation");
}
