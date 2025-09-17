using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
using AbstractionsKernelParameter = DotCompute.Abstractions.Kernels.KernelParameter;
using OperatorsKernelParameter = DotCompute.Linq.Operators.Parameters.KernelParameter;
using DotCompute.Linq.Pipelines.Analysis;
using LinqKernelParameter = DotCompute.Linq.Operators.Parameters.KernelParameter;
using CompilationOperatorInfo = DotCompute.Linq.Compilation.Analysis.OperatorInfo;
using PipelineOperatorInfo = DotCompute.Linq.Pipelines.Analysis.OperatorInfo;

// Namespace aliases to resolve ambiguous references  
using PipelinesExpressionAnalysisResult = DotCompute.Linq.Pipelines.Analysis.ExpressionAnalysisResult;
using KernelGenerationExpressionAnalysisResult = DotCompute.Linq.KernelGeneration.ExpressionAnalysisResult;
using OperatorsGeneratedKernel = DotCompute.Linq.Operators.Generation.GeneratedKernel;
using KernelGenerationGeneratedKernel = DotCompute.Linq.KernelGeneration.GeneratedKernel;

namespace DotCompute.Linq.Compilation.Stages;

/// <summary>
/// Generates kernel source code from analyzed expression trees.
/// Supports multiple target backends (CPU SIMD, CUDA GPU).
/// </summary>
public sealed class KernelCodeGenerator
{
    private readonly ILogger<KernelCodeGenerator> _logger;
    private readonly Dictionary<BackendType, IBackendCodeGenerator> _backendGenerators;
    private readonly KernelNamingStrategy _namingStrategy;
    private readonly CodeGenerationMetrics _metrics;

    #region Helper Methods

    private static DotCompute.Linq.KernelGeneration.MemoryAccessPattern? ConvertMemoryAccessPattern(DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern pattern)
    {
        return pattern switch
        {
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Sequential => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Sequential,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Random => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Random,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Strided => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Strided,
            _ => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Sequential
        };
    }

    private DotCompute.Linq.Pipelines.Analysis.OperatorInfo ConvertToOperatorInfo(DotCompute.Linq.Compilation.Analysis.PipelineOperatorInfo pipelineOp)
    {
        // Return a default OperatorInfo since the types don't map directly
        // This is a temporary solution for compilation - a proper mapping should be implemented
        return new DotCompute.Linq.Pipelines.Analysis.OperatorInfo();
    }

    #endregion

    public KernelCodeGenerator(ILogger<KernelCodeGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backendGenerators = InitializeBackendGenerators();
        _namingStrategy = new KernelNamingStrategy();
        _metrics = new CodeGenerationMetrics();
    }

    /// <summary>
    /// Converts LinqKernelParameters to GeneratedKernelParameters.
    /// </summary>
    private static DotCompute.Linq.Operators.Generation.GeneratedKernelParameter[] ConvertToGeneratedKernelParameters(
        IReadOnlyList<LinqKernelParameter> parameters)
    {
        return parameters.Select((p, index) => new DotCompute.Linq.Operators.Generation.GeneratedKernelParameter
        {
            Name = p.Name,
            Type = p.Type,
            IsInput = p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Input || 
                     p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.InOut,
            IsOutput = p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Output || 
                      p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.InOut,
            SizeInBytes = p.Type.IsArray ? 0 : System.Runtime.InteropServices.Marshal.SizeOf(p.Type),
            ElementCount = p.Type.IsArray ? 1000 : 1, // Default array size
            ElementType = p.Type.IsArray ? p.Type.GetElementType() : null
        }).ToArray();
    }

    /// <summary>
    /// Converts metadata to dictionary format.
    /// </summary>
    private static Dictionary<string, object> ConvertToMetadataDictionary(
        DotCompute.Linq.KernelGeneration.KernelMetadata metadata)
    {
        return new Dictionary<string, object>
        {
            ["OptimizationLevel"] = metadata.OptimizationLevel.ToString(),
            ["Language"] = metadata.Language.ToString(),
            ["WorkGroupSize"] = metadata.WorkGroupSize ?? new int[] { 1, 1, 1 },
            ["SharedMemorySize"] = metadata.SharedMemorySize,
            ["SupportsVectorization"] = metadata.SupportsVectorization,
            ["UseFastMath"] = metadata.UseFastMath,
            ["Complexity"] = metadata.Complexity?.ToString() ?? "Moderate",
            ["MemoryPattern"] = metadata.MemoryPattern?.ToString() ?? "Sequential",
            ["IsDeterministic"] = metadata.IsDeterministic,
            ["IsThreadSafe"] = metadata.IsThreadSafe,
            ["Version"] = metadata.Version
        };
    }

    /// <summary>
    /// Generates kernel source code for a specific backend.
    /// </summary>
    public async Task<OperatorsGeneratedKernel> GenerateAsync(
        DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult,
        BackendType targetBackend,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = CodeGenerationActivity.Start(nameof(GenerateAsync));


        _logger.LogDebug("Generating kernel code for backend: {Backend}", targetBackend);


        try
        {
            var startTime = DateTimeOffset.UtcNow;


            if (!_backendGenerators.TryGetValue(targetBackend, out var generator))
            {
                throw new UnsupportedBackendException($"Backend {targetBackend} is not supported for code generation");
            }

            var kernelName = _namingStrategy.GenerateKernelName(analysisResult, targetBackend);
            var context = new CodeGenerationContext(
                analysisResult,
                targetBackend,
                kernelName,
                options ?? CompilationOptions.Default);

            // Generate kernel source code
            var sourceCode = await generator.GenerateKernelSourceAsync(context, cancellationToken);

            // Generate parameter definitions

            var parameters = await generator.GenerateParametersAsync(context, cancellationToken);

            // Generate entry point information

            var entryPoint = generator.GenerateEntryPoint(context);

            // Generate metadata

            var metadata = GenerateKernelMetadata(context, analysisResult);


            var generationTime = DateTimeOffset.UtcNow - startTime;
            _metrics.RecordGeneration(targetBackend, generationTime);


            var result = new OperatorsGeneratedKernel
            {
                Name = kernelName,
                Source = sourceCode,
                Parameters = ConvertToGeneratedKernelParameters(parameters),
                EntryPoint = entryPoint.FunctionName,
                TargetBackend = targetBackend.ToString(),
                Metadata = ConvertToMetadataDictionary(metadata)
            };


            _logger.LogDebug("Kernel generation completed for {Backend} in {Duration}ms: {LineCount} lines",
                targetBackend, generationTime.TotalMilliseconds, sourceCode.Split('\n').Length);


            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate kernel for backend: {Backend}", targetBackend);
            _metrics.RecordError(targetBackend);
            throw new CodeGenerationException($"Code generation failed for backend {targetBackend}", ex);
        }
    }

    /// <summary>
    /// Generates kernel code for multiple backends in parallel.
    /// </summary>
    public async Task<IReadOnlyDictionary<BackendType, OperatorsGeneratedKernel>> GenerateBatchAsync(
        DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult,
        IEnumerable<BackendType> targetBackends,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = targetBackends.Select(async backend =>
        {
            var kernel = await GenerateAsync(analysisResult, backend, options, cancellationToken);
            return new KeyValuePair<BackendType, OperatorsGeneratedKernel>(backend, kernel);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private KernelMetadata GenerateKernelMetadata(
        CodeGenerationContext context,
        DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
    {
        // Determine the kernel language based on target backend
        var kernelLanguage = context.TargetBackend switch
        {
            BackendType.CPU => DotCompute.Abstractions.Types.KernelLanguage.CSharp,
            BackendType.CUDA => DotCompute.Abstractions.Types.KernelLanguage.Cuda,
            BackendType.Metal => DotCompute.Abstractions.Types.KernelLanguage.Metal,
            BackendType.ROCm => DotCompute.Abstractions.Types.KernelLanguage.HIP,
            _ => DotCompute.Abstractions.Types.KernelLanguage.Auto
        };

        return new KernelMetadata(context.KernelName, kernelLanguage)
        {
            // Set properties for additional metadata
            MemoryPattern = ConvertMemoryAccessPattern(analysisResult.MemoryAccessPattern.PredominantPattern),
            Complexity = ComputationalComplexity.Moderate
        };
    }

    private ResourceUsageEstimate EstimateResourceUsage(
        CodeGenerationContext context,
        DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
    {
        var estimator = new ResourceUsageEstimator();
        return estimator.Estimate(context.TargetBackend, analysisResult);
    }

    private Dictionary<BackendType, IBackendCodeGenerator> InitializeBackendGenerators()
    {
        return new Dictionary<BackendType, IBackendCodeGenerator>
        {
            [BackendType.CPU] = new CpuSimdCodeGenerator(_logger),
            [BackendType.CUDA] = new CudaCodeGenerator(_logger),
            [BackendType.Metal] = new MetalCodeGenerator(_logger),
            [BackendType.ROCm] = new RocmCodeGenerator(_logger)
        };
    }

    public CodeGenerationStatistics GetStatistics()
    {
        return _metrics.GetStatistics();
    }
}

/// <summary>
/// Base interface for backend-specific code generators.
/// </summary>
internal interface IBackendCodeGenerator
{
    Task<string> GenerateKernelSourceAsync(CodeGenerationContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken);
    KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context);
}

/// <summary>
/// Generates SIMD-optimized C# code for CPU execution.
/// </summary>
internal class CpuSimdCodeGenerator : IBackendCodeGenerator
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, IOperatorCodeGenerator> _operatorGenerators;

    public CpuSimdCodeGenerator(ILogger logger)
    {
        _logger = logger;
        _operatorGenerators = InitializeOperatorGenerators();
    }

    public async Task<string> GenerateKernelSourceAsync(CodeGenerationContext context, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        // Generate using directives

        GenerateUsingDirectives(builder);

        // Generate namespace and class declaration

        GenerateClassDeclaration(builder, context);

        // Generate kernel method

        await GenerateKernelMethodAsync(builder, context, cancellationToken);

        // Close class and namespace

        builder.AppendLine("    }");
        builder.AppendLine("}");


        return builder.ToString();
    }

    public async Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken)
    {
        var parameters = new List<LinqKernelParameter>();

        // Add input arrays

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

        // Add output array

        var outputElementType = DetermineOutputElementType(context.AnalysisResult);
        parameters.Add(new LinqKernelParameter(
            "output",
            outputElementType.MakeArrayType(),
            DotCompute.Linq.Operators.Parameters.ParameterDirection.Output));

        // Add length parameter

        parameters.Add(new LinqKernelParameter(
            "length",
            typeof(int),
            DotCompute.Linq.Operators.Parameters.ParameterDirection.Input));


        await Task.CompletedTask; // For async consistency
        return parameters;
    }

    public KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context)
    {
        return new KernelEntryPoint(
            context.KernelName,
            "Execute",
            KernelExecutionModel.DataParallel);
    }

    private void GenerateUsingDirectives(StringBuilder builder)
    {
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Numerics;");
        builder.AppendLine("using System.Runtime.Intrinsics;");
        builder.AppendLine("using System.Runtime.Intrinsics.X86;");
        builder.AppendLine("using System.Runtime.CompilerServices;");
        builder.AppendLine("using DotCompute.Backends.CPU;");
        builder.AppendLine();
    }

    private void GenerateClassDeclaration(StringBuilder builder, CodeGenerationContext context)
    {
        builder.AppendLine("namespace DotCompute.Generated.Kernels");
        builder.AppendLine("{");
        builder.AppendLine($"    public static class {context.KernelName}");
        builder.AppendLine("    {");
    }

    private async Task GenerateKernelMethodAsync(StringBuilder builder, CodeGenerationContext context, CancellationToken cancellationToken)
    {
        var outputType = DetermineOutputElementType(context.AnalysisResult);

        // Method signature

        builder.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine($"        public static unsafe void Execute(");

        // Parameters

        var parameters = await GenerateParametersAsync(context, cancellationToken);
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            var paramDecl = param.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Input ? "in " : "";
            paramDecl += GetCSharpTypeName(param.Type) + " " + param.Name;


            if (i < parameters.Count - 1)
            {
                paramDecl += ",";
            }


            builder.AppendLine($"            {paramDecl}");
        }


        builder.AppendLine("        )");
        builder.AppendLine("        {");

        // Method body with SIMD optimization

        GenerateSimdKernelBody(builder, context, outputType);


        builder.AppendLine("        }");
    }

    private void GenerateSimdKernelBody(StringBuilder builder, CodeGenerationContext context, Type outputType)
    {
        builder.AppendLine("            // SIMD-optimized kernel implementation");
        builder.AppendLine("            var vectorSize = Vector<float>.Count;");
        builder.AppendLine("            var vectorLength = length - (length % vectorSize);");
        builder.AppendLine();

        // Generate vectorized loop

        builder.AppendLine("            // Vectorized processing");
        builder.AppendLine("            for (int i = 0; i < vectorLength; i += vectorSize)");
        builder.AppendLine("            {");


        GenerateVectorizedOperations(builder, context);


        builder.AppendLine("            }");
        builder.AppendLine();

        // Generate scalar remainder

        builder.AppendLine("            // Scalar remainder");
        builder.AppendLine("            for (int i = vectorLength; i < length; i++)");
        builder.AppendLine("            {");


        GenerateScalarOperations(builder, context);


        builder.AppendLine("            }");
    }

    private void GenerateVectorizedOperations(StringBuilder builder, CodeGenerationContext context)
    {
        foreach (var op in context.AnalysisResult.OperatorChain)
        {
            if (_operatorGenerators.TryGetValue(op.Name, out var generator))
            {
                var operatorInfo = ConvertToOperatorInfo(op);
                generator.GenerateVectorized(builder, operatorInfo, context);
            }
        }
    }

    private void GenerateScalarOperations(StringBuilder builder, CodeGenerationContext context)
    {
        foreach (var op in context.AnalysisResult.OperatorChain)
        {
            if (_operatorGenerators.TryGetValue(op.Name, out var generator))
            {
                var operatorInfo = ConvertToOperatorInfo(op);
                generator.GenerateScalar(builder, operatorInfo, context);
            }
        }
    }

    private Dictionary<string, IOperatorCodeGenerator> InitializeOperatorGenerators()
    {
        return new Dictionary<string, IOperatorCodeGenerator>
        {
            ["Addition"] = new ArithmeticOperatorGenerator(),
            ["Subtraction"] = new ArithmeticOperatorGenerator(),
            ["Multiplication"] = new ArithmeticOperatorGenerator(),
            ["Division"] = new ArithmeticOperatorGenerator(),
            ["Select"] = new SelectOperatorGenerator(),
            ["Where"] = new WhereOperatorGenerator(),
            ["Aggregate"] = new AggregateOperatorGenerator()
        };
    }

    private static bool IsArrayType(Type type) => type.IsArray;

    /// <summary>
    /// Converts LINQ kernel parameters to generated kernel parameters.
    /// </summary>
    private static GeneratedKernelParameter[] ConvertToGeneratedKernelParameters(IReadOnlyList<LinqKernelParameter> parameters)
    {
        return parameters.Select(p => new GeneratedKernelParameter
        {
            Name = p.Name,
            Type = p.Type,
            IsInput = p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Input || p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.InOut,
            IsOutput = p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Output || p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.InOut,
            SizeInBytes = EstimateParameterSize(p.Type),
            ElementCount = p.Type.IsArray ? 1 : 0,
            ElementType = p.Type.IsArray ? p.Type.GetElementType() : null
        }).ToArray();
    }

    /// <summary>
    /// Estimates the size of a parameter type in bytes.
    /// </summary>
    private static int EstimateParameterSize(Type type)
    {
        if (type == typeof(int) || type == typeof(float)) return 4;
        if (type == typeof(long) || type == typeof(double)) return 8;
        if (type == typeof(short)) return 2;
        if (type == typeof(byte) || type == typeof(bool)) return 1;
        if (type.IsArray) return 0; // Will be determined at runtime
        return IntPtr.Size; // Default to pointer size for reference types
    }

    /// <summary>
    /// Converts kernel metadata to dictionary.
    /// </summary>
    private static Dictionary<string, object> ConvertToMetadataDictionary(KernelMetadata metadata)
    {
        var result = new Dictionary<string, object>();
        
        if (metadata.Properties != null)
        {
            foreach (var kvp in metadata.Properties)
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        
        // Add core metadata properties
        result["GenerationTimestamp"] = metadata.GenerationTimestamp;
        result["OptimizationLevel"] = metadata.OptimizationLevel.ToString();
        result["CompilerVersion"] = metadata.CompilerVersion;
        
        return result;
    }

    private static Type DetermineOutputElementType(DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
    {
        // Simplified logic - in practice, this would analyze the expression tree
        return typeof(float);
    }


    private static string GetCSharpTypeName(Type type)
    {
        if (type == typeof(int))
        {
            return "int";
        }


        if (type == typeof(float))
        {
            return "float";
        }


        if (type == typeof(double))
        {
            return "double";
        }


        if (type == typeof(bool))
        {
            return "bool";
        }


        if (type.IsArray)
        {
            return GetCSharpTypeName(type.GetElementType()!) + "[]";
        }


        return type.Name;
    }
}

/// <summary>
/// Generates CUDA C code for GPU execution.
/// </summary>
internal class CudaCodeGenerator : IBackendCodeGenerator
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ICudaOperatorGenerator> _operatorGenerators;

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


        parameters.Add(new LinqKernelParameter(
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
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            var paramDecl = GetCudaParameterDeclaration(param);


            if (i < parameters.Count - 1)
            {
                paramDecl += ",";
            }


            builder.AppendLine($"    {paramDecl}");
        }


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
        builder.AppendLine();


        builder.AppendLine("    // Grid-stride loop for coalesced memory access");
        builder.AppendLine("    for (int i = idx; i < length; i += stride)");
        builder.AppendLine("    {");

        // Generate operation code

        foreach (var op in context.AnalysisResult.OperatorChain)
        {
            if (_operatorGenerators.TryGetValue(op.Name, out var generator))
            {
                var operatorInfo = ConvertToOperatorInfo(op);
                generator.Generate(builder, operatorInfo, context);
            }
        }


        builder.AppendLine("    }");
    }

    private Dictionary<string, ICudaOperatorGenerator> InitializeCudaOperatorGenerators()
    {
        return new Dictionary<string, ICudaOperatorGenerator>
        {
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
        {
            nameof(Single) => "float",
            nameof(Double) => "double",
            nameof(Int32) => "int",
            nameof(Int64) => "long long",
            nameof(Boolean) => "bool",
            _ => "float" // Default fallback
        };
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
    }

    private static bool IsArrayType(Type type) => type.IsArray;

    /// <summary>
    /// Converts PipelineOperatorInfo to OperatorInfo for compatibility.
    /// </summary>
    private static Pipelines.Analysis.OperatorInfo ConvertToOperatorInfo(Compilation.Analysis.PipelineOperatorInfo pipelineOp)
    {
        return new Pipelines.Analysis.OperatorInfo
        {
            OperatorType = DotCompute.Core.Analysis.UnifiedOperatorType.Transform, // Default mapping
            InputTypes = pipelineOp.InputTypes,
            OutputType = pipelineOp.OutputType,
            Name = pipelineOp.Name
        };
    }

    /// <summary>
    /// Converts memory access pattern types.
    /// </summary>
    private static DotCompute.Linq.KernelGeneration.MemoryAccessPattern? ConvertMemoryAccessPattern(DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern pattern)
    {
        return pattern switch
        {
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Sequential => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Sequential,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Random => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Random,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Strided => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Strided,
            _ => null
        };
    }

}

// Placeholder implementations for other backends
internal class MetalCodeGenerator : IBackendCodeGenerator
{
    private readonly ILogger _logger;


    public MetalCodeGenerator(ILogger logger) => _logger = logger;


    public Task<string> GenerateKernelSourceAsync(CodeGenerationContext context, CancellationToken cancellationToken)
        => throw new NotImplementedException("Metal backend code generation not yet implemented");


    public Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken)
        => throw new NotImplementedException("Metal backend code generation not yet implemented");


    public KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context)
        => throw new NotImplementedException("Metal backend code generation not yet implemented");
}

internal class RocmCodeGenerator : IBackendCodeGenerator
{
    private readonly ILogger _logger;


    public RocmCodeGenerator(ILogger logger) => _logger = logger;


    public Task<string> GenerateKernelSourceAsync(CodeGenerationContext context, CancellationToken cancellationToken)
        => throw new NotImplementedException("ROCm backend code generation not yet implemented");


    public Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken)
        => throw new NotImplementedException("ROCm backend code generation not yet implemented");


    public KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context)
        => throw new NotImplementedException("ROCm backend code generation not yet implemented");
}

/// <summary>
/// Context information for code generation.
/// </summary>
internal record CodeGenerationContext(
    DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult AnalysisResult,
    BackendType TargetBackend,
    string KernelName,
    CompilationOptions Options);

/// <summary>
/// Generates unique kernel names based on analysis results.
/// </summary>
internal class KernelNamingStrategy
{
    public string GenerateKernelName(DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult, BackendType backend)
    {
        var operatorChain = string.Join("_", analysisResult.OperatorChain.Take(3).Select(op => op.OperatorType));
        var hash = Math.Abs(analysisResult.OperationSignature.GetHashCode()) % 10000;
        return $"Kernel_{operatorChain}_{backend}_{hash:D4}";
    }
}

/// <summary>
/// Tracks code generation metrics.
/// </summary>
internal class CodeGenerationMetrics
{
    private readonly Dictionary<BackendType, List<TimeSpan>> _generationTimes = new();
    private readonly Dictionary<BackendType, int> _errorCounts = new();

    public void RecordGeneration(BackendType backend, TimeSpan duration)
    {
        if (!_generationTimes.ContainsKey(backend))
        {
            _generationTimes[backend] = new List<TimeSpan>();
        }


        _generationTimes[backend].Add(duration);
    }

    public void RecordError(BackendType backend)
    {
        _errorCounts.TryGetValue(backend, out var count);
        _errorCounts[backend] = count + 1;
    }

    public CodeGenerationStatistics GetStatistics()
    {
        var backendStats = new Dictionary<BackendType, BackendStatistics>();


        foreach (var (backend, times) in _generationTimes)
        {
            var avgTime = times.Count > 0 ? times.Average(t => t.TotalMilliseconds) : 0;
            var errorCount = _errorCounts.GetValueOrDefault(backend, 0);


            backendStats[backend] = new BackendStatistics(
                times.Count,
                errorCount,
                avgTime,
                times.Count > 0 ? times.Min() : TimeSpan.Zero,
                times.Count > 0 ? times.Max() : TimeSpan.Zero);
        }


        return new CodeGenerationStatistics(backendStats);
    }
}

/// <summary>
/// Statistics for code generation performance.
/// </summary>
public record CodeGenerationStatistics(
    IReadOnlyDictionary<BackendType, BackendStatistics> BackendStatistics);

public record BackendStatistics(
    int GenerationCount,
    int ErrorCount,
    double AverageTimeMs,
    TimeSpan MinTime,
    TimeSpan MaxTime);

/// <summary>
/// Exception thrown during code generation.
/// </summary>
public class CodeGenerationException : Exception
{
    public CodeGenerationException(string message) : base(message) { }
    public CodeGenerationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception for unsupported backend types.
/// </summary>
public class UnsupportedBackendException : Exception
{
    public UnsupportedBackendException(string message) : base(message) { }
}

/// <summary>
/// Activity tracking for code generation stages.
/// </summary>
internal static class CodeGenerationActivity
{
    public static IDisposable Start(string operationName)
    {
        return new NoOpDisposable();
    }


    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}

/// <summary>
/// Interfaces for operator-specific code generation.
/// </summary>
internal interface IOperatorCodeGenerator
{
    void GenerateVectorized(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context);
    void GenerateScalar(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context);
}

internal interface ICudaOperatorGenerator
{
    void Generate(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context);
}

// Placeholder operator generators
internal class ArithmeticOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Vectorized arithmetic operation");
    }

    public void GenerateScalar(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Scalar arithmetic operation");
    }
}

internal class SelectOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Vectorized select operation");
    }

    public void GenerateScalar(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Scalar select operation");
    }
}

internal class WhereOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Vectorized where operation");
    }

    public void GenerateScalar(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Scalar where operation");
    }
}

internal class AggregateOperatorGenerator : IOperatorCodeGenerator
{
    public void GenerateVectorized(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Vectorized aggregate operation");
    }

    public void GenerateScalar(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("                // Scalar aggregate operation");
    }
}

internal class CudaArithmeticOperatorGenerator : ICudaOperatorGenerator
{
    public void Generate(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("        // CUDA arithmetic operation");
    }
}

internal class CudaSelectOperatorGenerator : ICudaOperatorGenerator
{
    public void Generate(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("        // CUDA select operation");
    }
}

internal class CudaWhereOperatorGenerator : ICudaOperatorGenerator
{
    public void Generate(StringBuilder builder, PipelineOperatorInfo operatorInfo, CodeGenerationContext context)
    {
        builder.AppendLine("        // CUDA where operation");
    }
}

/// <summary>
/// Estimates resource usage for generated kernels.
/// </summary>
internal class ResourceUsageEstimator
{
    public ResourceUsageEstimate Estimate(BackendType backend, DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
    {
        return backend switch
        {
            BackendType.CPU => EstimateCpuUsage(analysisResult),
            BackendType.CUDA => EstimateGpuUsage(analysisResult),
            _ => new ResourceUsageEstimate(0, 0, 0, 0)
        };
    }

    private ResourceUsageEstimate EstimateCpuUsage(DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
    {
        var operatorCount = analysisResult.OperatorChain.Count;
        var memoryMB = operatorCount * 10; // Rough estimate
        var threadsNeeded = Environment.ProcessorCount;


        return new ResourceUsageEstimate(memoryMB, threadsNeeded, 0, 0);
    }

    private ResourceUsageEstimate EstimateGpuUsage(DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
    {
        var operatorCount = analysisResult.OperatorChain.Count;
        var memoryMB = operatorCount * 20; // GPU kernels typically use more memory
        var registersPerThread = operatorCount * 4;
        var sharedMemoryKB = Math.Min(operatorCount * 2, 48); // Max 48KB per block


        return new ResourceUsageEstimate(memoryMB, 0, registersPerThread, sharedMemoryKB);
    }

    /// <summary>
    /// Converts pipeline operator info to analysis operator info.
    /// </summary>
    private static DotCompute.Linq.Analysis.OperatorInfo ConvertToOperatorInfo(PipelineOperatorInfo pipelineInfo)
    {
        return new DotCompute.Linq.Analysis.OperatorInfo
        {
            OperatorType = ExpressionType.Call,
            ResultType = pipelineInfo.OutputType ?? typeof(object),
            OperandTypes = pipelineInfo.InputTypes?.ToArray() ?? Array.Empty<Type>()
        };
    }

    /// <summary>
    /// Converts compilation memory access pattern to kernel metadata memory access pattern.
    /// </summary>
    private static DotCompute.Linq.KernelGeneration.MemoryAccessPattern ConvertMemoryAccessPattern(
        DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern compilationPattern)
    {
        return compilationPattern switch
        {
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Sequential => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Sequential,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Random => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Random,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Strided => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Strided,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Coalesced => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Coalesced,
            DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern.Scatter => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Scattered,
            _ => DotCompute.Linq.KernelGeneration.MemoryAccessPattern.Sequential
        };
    }
}