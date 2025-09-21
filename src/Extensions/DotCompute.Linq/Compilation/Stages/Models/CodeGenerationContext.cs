using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Types;
using DotCompute.Linq.Compilation.Analysis;

namespace DotCompute.Linq.Compilation.Stages.Models;
/// <summary>
/// Context information for code generation.
/// </summary>
internal record CodeGenerationContext(
    ExpressionAnalysisResult AnalysisResult,
    BackendType TargetBackend,
    string KernelName,
    CompilationOptions Options);
