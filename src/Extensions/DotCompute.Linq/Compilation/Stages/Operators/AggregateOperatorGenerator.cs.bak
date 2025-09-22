using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Compilation.Stages.Interfaces;
using OperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;

namespace DotCompute.Linq.Compilation.Stages.Operators;
/// <summary>
/// Generates code for aggregate operations (sum, min, max, average).
/// </summary>
internal class AggregateOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
    {
        // Generate SIMD vectorized aggregate operations
        builder.AppendLine("                // Vectorized aggregate operation");
        builder.AppendLine("                var inputVector = new Vector<float>(input, i);");
        builder.AppendLine("                accumulator = Vector.Add(accumulator, inputVector);");
    }
    public void GenerateScalar(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
        // Generate scalar aggregate operations
        builder.AppendLine("                // Scalar aggregate operation");
        builder.AppendLine("                accumulator += input[i];");
}
