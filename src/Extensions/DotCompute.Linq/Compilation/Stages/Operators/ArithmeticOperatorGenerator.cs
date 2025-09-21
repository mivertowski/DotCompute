using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Compilation.Stages.Interfaces;
using OperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;

namespace DotCompute.Linq.Compilation.Stages.Operators;
/// <summary>
/// Generates code for arithmetic operations (add, subtract, multiply, divide).
/// </summary>
internal class ArithmeticOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
    {
        // Generate SIMD vectorized arithmetic operations
        builder.AppendLine("                // Vectorized arithmetic operation");
        builder.AppendLine("                var vector1 = new Vector<float>(input1, i);");
        builder.AppendLine("                var vector2 = new Vector<float>(input2, i);");
        builder.AppendLine("                var result = vector1 + vector2; // Placeholder for actual operation");
        builder.AppendLine("                result.CopyTo(output, i);");
    }
    public void GenerateScalar(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context)
        // Generate scalar arithmetic operations
        builder.AppendLine("                // Scalar arithmetic operation");
        builder.AppendLine("                output[i] = input1[i] + input2[i]; // Placeholder for actual operation");
}
