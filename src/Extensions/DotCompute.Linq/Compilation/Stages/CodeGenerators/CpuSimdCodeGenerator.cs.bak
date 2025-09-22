using System;
using System.Collections.Generic;
using System.Linq;
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
    public async Task<IReadOnlyList<LinqKernelParameter>> GenerateParametersAsync(CodeGenerationContext context, CancellationToken cancellationToken)
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
            "length",
            typeof(int),
            DotCompute.Linq.Operators.Parameters.ParameterDirection.Input));
        await Task.CompletedTask; // For async consistency
        return parameters;
    public KernelEntryPoint GenerateEntryPoint(CodeGenerationContext context)
        return new KernelEntryPoint(
            context.KernelName,
            "Execute",
            KernelExecutionModel.DataParallel);
    private void GenerateUsingDirectives(StringBuilder builder)
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Numerics;");
        builder.AppendLine("using System.Runtime.Intrinsics;");
        builder.AppendLine("using System.Runtime.Intrinsics.X86;");
        builder.AppendLine("using System.Runtime.CompilerServices;");
        builder.AppendLine("using DotCompute.Backends.CPU;");
        builder.AppendLine();
    private void GenerateClassDeclaration(StringBuilder builder, CodeGenerationContext context)
        builder.AppendLine("namespace DotCompute.Generated.Kernels");
        builder.AppendLine("{");
        builder.AppendLine($"    public static class {context.KernelName}");
        builder.AppendLine("    {");
    private async Task GenerateKernelMethodAsync(StringBuilder builder, CodeGenerationContext context, CancellationToken cancellationToken)
        var outputType = DetermineOutputElementType(context.AnalysisResult);
        // Method signature
        builder.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine($"        public static unsafe void Execute(");
        // Parameters
        var parameters = await GenerateParametersAsync(context, cancellationToken);
        for (var i = 0; i < parameters.Count; i++)
            var param = parameters[i];
            var paramDecl = param.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Input ? "in " : "";
            paramDecl += GetCSharpTypeName(param.Type) + " " + param.Name;
            if (i < parameters.Count - 1)
                paramDecl += ",";
            builder.AppendLine($"            {paramDecl}");
        builder.AppendLine("        )");
        builder.AppendLine("        {");
        // Method body with SIMD optimization
        GenerateSimdKernelBody(builder, context, outputType);
        builder.AppendLine("        }");
    private void GenerateSimdKernelBody(StringBuilder builder, CodeGenerationContext context, Type outputType)
        builder.AppendLine("            // SIMD-optimized kernel implementation");
        builder.AppendLine("            var vectorSize = Vector<float>.Count;");
        builder.AppendLine("            var vectorLength = length - (length % vectorSize);");
        // Generate vectorized loop
        builder.AppendLine("            // Vectorized processing");
        builder.AppendLine("            for (int i = 0; i < vectorLength; i += vectorSize)");
        builder.AppendLine("            {");
        GenerateVectorizedOperations(builder, context);
        builder.AppendLine("            }");
        // Generate scalar remainder
        builder.AppendLine("            // Scalar remainder");
        builder.AppendLine("            for (int i = vectorLength; i < length; i++)");
        GenerateScalarOperations(builder, context);
    private void GenerateVectorizedOperations(StringBuilder builder, CodeGenerationContext context)
        foreach (var op in context.AnalysisResult.OperatorChain)
            if (_operatorGenerators.TryGetValue(op.Name, out var generator))
                var operatorInfo = op.ConvertToOperatorInfo();
                generator.GenerateVectorized(builder, operatorInfo, context);
    private void GenerateScalarOperations(StringBuilder builder, CodeGenerationContext context)
                generator.GenerateScalar(builder, operatorInfo, context);
    private Dictionary<string, IOperatorCodeGenerator> InitializeOperatorGenerators()
        return new Dictionary<string, IOperatorCodeGenerator>
            ["Addition"] = new ArithmeticOperatorGenerator(),
            ["Subtraction"] = new ArithmeticOperatorGenerator(),
            ["Multiplication"] = new ArithmeticOperatorGenerator(),
            ["Division"] = new ArithmeticOperatorGenerator(),
            ["Select"] = new SelectOperatorGenerator(),
            ["Where"] = new WhereOperatorGenerator(),
            ["Aggregate"] = new AggregateOperatorGenerator()
        };
    private static bool IsArrayType(Type type) => type.IsArray;
    /// <summary>
    /// Converts LINQ kernel parameters to generated kernel parameters.
    /// </summary>
    private static GeneratedKernelParameter[] ConvertToGeneratedKernelParameters(IReadOnlyList<LinqKernelParameter> parameters)
        return parameters.Select(p => new GeneratedKernelParameter
            Name = p.Name,
            Type = p.Type,
            IsInput = p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Input || p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.InOut,
            IsOutput = p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.Output || p.Direction == DotCompute.Linq.Operators.Parameters.ParameterDirection.InOut,
            SizeInBytes = EstimateParameterSize(p.Type),
            ElementCount = p.Type.IsArray ? 1 : 0,
            ElementType = p.Type.IsArray ? p.Type.GetElementType() : null
        }).ToArray();
    /// Estimates the size of a parameter type in bytes.
    private static int EstimateParameterSize(Type type)
        if (type == typeof(int) || type == typeof(float))
            return 4;
        if (type == typeof(long) || type == typeof(double))
            return 8;
        if (type == typeof(short))
            return 2;
        if (type == typeof(byte) || type == typeof(bool))
            return 1;
        if (type.IsArray)
            return 0; // Will be determined at runtime
        return IntPtr.Size; // Default to pointer size for reference types
    /// Converts kernel metadata to dictionary.
    private static Dictionary<string, object> ConvertToMetadataDictionary(KernelMetadata metadata)
        var result = new Dictionary<string, object>();
        if (metadata.Properties != null)
            foreach (var kvp in metadata.Properties)
                result[kvp.Key] = kvp.Value;
        // Add core metadata properties
        result["GenerationTimestamp"] = metadata.GenerationTimestamp;
        result["OptimizationLevel"] = metadata.OptimizationLevel.ToString();
        result["CompilerVersion"] = metadata.CompilerVersion;
        return result;
    private static Type DetermineOutputElementType(DotCompute.Linq.Compilation.Analysis.ExpressionAnalysisResult analysisResult)
        // Simplified logic - in practice, this would analyze the expression tree
        return typeof(float);
    private static string GetCSharpTypeName(Type type)
        if (type == typeof(int))
            return "int";
        if (type == typeof(float))
            return "float";
        if (type == typeof(double))
            return "double";
        if (type == typeof(bool))
            return "bool";
            return GetCSharpTypeName(type.GetElementType()!) + "[]";
        return type.Name;
}
