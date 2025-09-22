using System.Text;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Pipelines.Analysis;

namespace DotCompute.Linq.Compilation.Stages.Interfaces;
{
/// <summary>
/// Interface for CUDA-specific operator code generation.
/// </summary>
internal interface ICudaOperatorGenerator
{
    void Generate(StringBuilder builder, OperatorInfo operatorInfo, CodeGenerationContext context);
}
