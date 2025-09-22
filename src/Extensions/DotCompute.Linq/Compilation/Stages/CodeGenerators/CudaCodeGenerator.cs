using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Types;
using DotCompute.Linq.Compilation.Analysis;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.KernelGeneration;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Pipelines.Bridge;

// Namespace aliases to resolve ambiguous references
using LinqKernelParameter = DotCompute.Linq.Operators.Parameters.KernelParameter;
namespace DotCompute.Linq.Compilation.Stages.CodeGenerators;
{
/// <summary>
/// Generates CUDA C code for GPU execution.
/// </summary>
internal class CudaCodeGenerator : IBackendCodeGenerator
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ICudaOperatorGenerator> _operatorGenerators;
    }
    public CudaCodeGenerator(ILogger logger)
    {
        _logger = logger;
        _operatorGenerators = InitializeCudaOperatorGenerators();
    }
    public async Task<string> GenerateKernelSourceAsync(CodeGenerationContext context, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        // Generate CUDA headers
        GenerateCudaHeaders(builder);
        // Generate kernel function
        await GenerateCudaKernelAsync(builder, context, cancellationToken);
        return builder.ToString();
    }
    public async Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken)
    {
        // Similar to CPU version but with device memory considerations
        var parameters = new List<LinqKernelParameter>();
        foreach (var inputType in context.AnalysisResult.TypeUsage.Keys)
        {
            if (IsArrayType(inputType))
            {
                parameters.Add(new LinqKernelParameter(
                    $"input_{inputType.GetElementType()?.Name?.ToLowerInvariant()}",
                    inputType,
                    DotCompute.Linq.Operators.Parameters.ParameterDirection.Input));
            }
        }
        var outputElementType = DetermineOutputElementType(context.AnalysisResult);
        parameters.Add(new LinqKernelParameter(
            "output",
            outputElementType.MakeArrayType(),
            DotCompute.Linq.Operators.Parameters.ParameterDirection.Output));
            "length",
            typeof(int),
            DotCompute.Linq.Operators.Parameters.ParameterDirection.Input));
        await Task.CompletedTask;
        return parameters;
    }
    public KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context)
    {
        return new KernelEntryPoint(
            context.KernelName,
            context.KernelName, // CUDA kernels use the same name
            KernelExecutionModel.GPU);
    }
    private void GenerateCudaHeaders(StringBuilder builder)
    {
        builder.AppendLine("#include <cuda_runtime.h>");
        builder.AppendLine("#include <device_launch_parameters.h>");
        builder.AppendLine("#include <cub/cub.cuh>");
        builder.AppendLine();
    }
    private async Task GenerateCudaKernelAsync(StringBuilder builder, CodeGenerationContext context, CancellationToken cancellationToken)
    {
        var outputType = DetermineOutputElementType(context.AnalysisResult);
        var cudaType = GetCudaTypeName(outputType);
        // Kernel signature
        builder.AppendLine($"__global__ void {context.KernelName}(");
        var parameters = await GenerateParametersAsync(context, cancellationToken);
        for (var i = 0; i < parameters.Count; i++)
            var param = parameters[i];
            var paramDecl = GetCudaParameterDeclaration(param);
            if (i < parameters.Count - 1)
                paramDecl += ",";
            builder.AppendLine($"    {paramDecl}");
        builder.AppendLine(")");
        builder.AppendLine("{");
        // Kernel body
        GenerateCudaKernelBody(builder, context);
        builder.AppendLine("}");
    }
    private void GenerateCudaKernelBody(StringBuilder builder, CodeGenerationContext context)
    {
        builder.AppendLine("    // Calculate thread index");
        builder.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        builder.AppendLine("    int stride = blockDim.x * gridDim.x;");
        builder.AppendLine("    // Grid-stride loop for coalesced memory access");
        builder.AppendLine("    for (int i = idx; i < length; i += stride)");
        builder.AppendLine("    {");
        // Generate operation code
        foreach (var op in context.AnalysisResult.OperatorChain)
            if (_operatorGenerators.TryGetValue(op.Name, out var generator))
                var operatorInfo = op.ConvertToOperatorInfo();
                generator.Generate(builder, operatorInfo, context);
        builder.AppendLine("    }");
    }
    private Dictionary<string, ICudaOperatorGenerator> InitializeCudaOperatorGenerators()
    {
        return new Dictionary<string, ICudaOperatorGenerator>
            ["Addition"] = new CudaArithmeticOperatorGenerator(),
            ["Subtraction"] = new CudaArithmeticOperatorGenerator(),
            ["Multiplication"] = new CudaArithmeticOperatorGenerator(),
            ["Division"] = new CudaArithmeticOperatorGenerator(),
            ["Select"] = new CudaSelectOperatorGenerator(),
            ["Where"] = new CudaWhereOperatorGenerator()
        };
    }
    private static string GetCudaTypeName(Type type)
    {
        return type.Name switch
            nameof(Single) => "float",
            nameof(Double) => "double",
            nameof(Int32) => "int",
            nameof(Int64) => "long long",
            nameof(Boolean) => "bool",
            _ => "float" // Default fallback
    }
    private static string GetCudaParameterDeclaration(LinqKernelParameter param)
    {
        var typeName = GetCudaTypeName(param.Type.IsArray ? param.Type.GetElementType()! : param.Type);
        var pointer = param.Type.IsArray ? "*" : "";
        var constModifier = param.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Input && param.Type.IsArray ? "const " : "";
        return $"{constModifier}{typeName}{pointer} {param.Name}";
    }
    private static Type DetermineOutputElementType(DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
    {
        return typeof(float); // Simplified
    private static bool IsArrayType(Type type) => type.IsArray;
    /// <summary>
    /// Converts PipelineOperatorInfo to OperatorInfo for compatibility.
    /// </summary>
    private static Pipelines.Analysis.OperatorInfo ConvertToOperatorInfo(Compilation.Analysis.PipelineOperatorInfo pipelineOp)
        return new Pipelines.Analysis.OperatorInfo
            OperatorType = DotCompute.Core.Analysis.UnifiedOperatorType.Transform, // Default mapping
            InputTypes = pipelineOp.InputTypes,
            OutputType = pipelineOp.OutputType,
            Name = pipelineOp.Name
    /// Converts memory access pattern types.
    private static DotCompute.Linq.KernelGeneration.MemoryAccessPattern? ConvertMemoryAccessPattern(DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern pattern)
        return pattern switch
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Sequential => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Sequential,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Random => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Random,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Strided => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Strided,
            _ => null
}
