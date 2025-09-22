using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Compilation.Stages.Interfaces;
using OperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;

namespace DotCompute.Linq.Compilation.Stages.Operators;
/// <summary>
/// Generates code for where/filter operations.
/// </summary>
internal class WhereOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
    {
        // Generate SIMD vectorized where operations
        builder.AppendLine("                // Vectorized where operation");
        builder.AppendLine("                var inputVector = new Vector<float>(input, i);");
        builder.AppendLine("                var conditionVector = Vector.GreaterThan(inputVector, Vector<float>.Zero);");
        builder.AppendLine("                var resultVector = Vector.ConditionalSelect(conditionVector, inputVector, Vector<float>.Zero);");
        builder.AppendLine("                resultVector.CopyTo(output, i);");
    }
    public void GenerateScalar(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
        // Generate scalar where operations
        builder.AppendLine("                // Scalar where operation");
        builder.AppendLine("                output[i] = input[i] > 0 ? input[i] : 0; // Placeholder condition");
}
