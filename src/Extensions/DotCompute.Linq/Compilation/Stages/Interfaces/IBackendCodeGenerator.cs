using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.KernelGeneration;

// Namespace aliases
using LinqKernelParameter = DotCompute.Linq.Operators.Parameters.KernelParameter;
namespace DotCompute.Linq.Compilation.Stages.Interfaces;
/// <summary>
/// Interface for backend-specific code generators.
/// </summary>
internal interface IBackendCodeGenerator
{
    Task<string> GenerateKernelSourceAsync(CodeGenerationContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken);
    KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context);
}
