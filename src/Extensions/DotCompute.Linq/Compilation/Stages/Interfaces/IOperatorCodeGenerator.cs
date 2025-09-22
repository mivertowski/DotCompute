using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Pipelines.Analysis;

namespace DotCompute.Linq.Compilation.Stages.Interfaces;
{
/// <summary>
/// Interface for operator-specific code generation.
/// </summary>
internal interface IOperatorCodeGenerator
{
    void GenerateVectorized(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context);
    void GenerateScalar(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context);
}
