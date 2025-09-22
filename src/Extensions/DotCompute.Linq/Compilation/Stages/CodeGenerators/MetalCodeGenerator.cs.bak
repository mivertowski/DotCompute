using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators.Parameters;

// Namespace aliases
using LinqKernelParameter = DotCompute.Linq.Operators.Parameters.KernelParameter;
namespace DotCompute.Linq.Compilation.Stages.CodeGenerators;
/// <summary>
/// Placeholder implementation for Metal backend code generation.
/// </summary>
internal class MetalCodeGenerator : IBackendCodeGenerator
{
    private readonly ILogger _logger;
    public MetalCodeGenerator(ILogger logger) => _logger = logger;
    public Task<string> GenerateKernelSourceAsync(CodeGenerationContext context, CancellationToken cancellationToken)
        => throw new NotImplementedException("Metal backend code generation not yet implemented");
    public Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken)
    public KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context)
}
