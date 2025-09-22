// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.Operators.Types;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Operators.Adapters;
using System.Text;
using CoreKernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;
using LinqKernelParameter = DotCompute.Linq.Operators.Parameters.KernelParameter;
// Namespace aliases to resolve ambiguous references
using OperatorsGeneratedKernel = DotCompute.Linq.Operators.Generation.GeneratedKernel;
using KernelGenerationGeneratedKernel = DotCompute.Linq.KernelGeneration.GeneratedKernel;
namespace DotCompute.Linq.Operators;
{
/// <summary>
/// Library of kernel templates for common operations.
/// </summary>
public class KernelTemplateLibrary
{
    private readonly Dictionary<string, IKernelTemplate> _templates;
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelTemplateLibrary"/> class.
    /// </summary>
    public KernelTemplateLibrary()
    {
        _templates = [];
        InitializeTemplates();
    }
    /// Gets a kernel template by name.
    /// <param name="templateName">The template name.</param>
    /// <returns>The kernel template.</returns>
    public IKernelTemplate GetTemplate(string templateName)
        {
        if (_templates.TryGetValue(templateName, out var template))
        {
            return template;
        }
        throw new InvalidOperationException($"Kernel template '{templateName}' not found");
    /// Gets all available template names.
    /// <returns>The template names.</returns>
    public IReadOnlyList<string> GetTemplateNames() => _templates.Keys.ToArray();
    private void InitializeTemplates()
        _templates["MapOperation"] = new MapKernelTemplate();
        _templates["FilterOperation"] = new FilterKernelTemplate();
        _templates["ReduceOperation"] = new ReduceKernelTemplate();
        _templates["SortOperation"] = new SortKernelTemplate();
}
/// Interface for kernel templates that generate code.
public interface IKernelTemplate
    {
    /// Generates a kernel from a definition.
    /// <param name="definition">The kernel definition.</param>
    /// <param name="accelerator">The target accelerator.</param>
    /// <returns>A generated kernel.</returns>
    public GeneratedKernel Generate(CoreKernelDefinition definition, IAccelerator accelerator);
/// Template for map (Select) operations.
public class MapKernelTemplate : IKernelTemplate
    {
    /// <inheritdoc/>
    public GeneratedKernel Generate(CoreKernelDefinition definition, IAccelerator accelerator)
        {
        var sourceBuilder = new StringBuilder();
        // Generate kernel based on accelerator type
        switch (accelerator.Type)
            case AcceleratorType.CUDA:
                GenerateCudaMapKernel(sourceBuilder, definition);
                break;
            case AcceleratorType.OpenCL:
                GenerateOpenCLMapKernel(sourceBuilder, definition);
            default:
                GenerateGenericMapKernel(sourceBuilder, definition);
        // Use adapter to safely extract LINQ-specific data
        var language = ConvertLanguage(KernelDefinitionAdapter.ExtractLanguage(definition));
        var parameters = KernelDefinitionAdapter.ExtractParameters(definition).Select(ConvertParameter).ToArray();
        return new OperatorsGeneratedKernel
            Name = definition.Name,
            Source = sourceBuilder.ToString(),
            Language = language,
            Parameters = parameters,
            SharedMemorySize = 0,
            OptimizationMetadata = definition.Metadata
        };
    private static void GenerateCudaMapKernel(StringBuilder source, CoreKernelDefinition definition)
        {
        _ = source.AppendLine("__global__ void " + definition.Name + "(");
        // Generate parameters
        var parameters = KernelDefinitionAdapter.ExtractParameters(definition);
        for (var i = 0; i < parameters.Length; i++)
            var param = parameters[i];
            _ = source.Append("    ");
            _ = source.Append(GetCudaType(param.Type));
            _ = source.Append("* ");
            _ = source.Append(param.Name);
            if (i < parameters.Length - 1)
            {
                _ = source.Append(",");
            }
            _ = source.AppendLine();
        _ = source.AppendLine(") {");
        _ = source.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = source.AppendLine("    int size = gridDim.x * blockDim.x;");
        _ = source.AppendLine("    ");
        _ = source.AppendLine("    if (idx < size) {");
        _ = source.AppendLine("        // Map operation logic here");
        _ = source.AppendLine("        output[idx] = transform(input_0[idx]);");
        _ = source.AppendLine("    }");
        _ = source.AppendLine("}");
    private static void GenerateOpenCLMapKernel(StringBuilder source, CoreKernelDefinition definition)
        _ = source.AppendLine("__kernel void " + definition.Name + "(");
            _ = source.Append("    __global ");
            _ = source.Append(GetOpenCLType(param.Type));
        _ = source.AppendLine("    int idx = get_global_id(0);");
        _ = source.AppendLine("    int size = get_global_size(0);");
    private static void GenerateGenericMapKernel(StringBuilder source, CoreKernelDefinition definition)
        _ = source.AppendLine("// Generic map kernel template");
        _ = source.AppendLine("public void " + definition.Name + "(");
            _ = source.Append(GetCSharpType(param.Type));
            _ = source.Append(" ");
        _ = source.AppendLine("    var threadId = GetThreadId();");
        _ = source.AppendLine("    var totalThreads = GetTotalThreads();");
        _ = source.AppendLine("    for (int i = threadId; i < size; i += totalThreads) {");
        _ = source.AppendLine("        output[i] = Transform(input_0[i]);");
    private static string GetCudaType(Type type)
        return Type.GetTypeCode(type) switch
            TypeCode.Int32 => "int",
            TypeCode.Single => "float",
            TypeCode.Double => "double",
            TypeCode.Boolean => "bool",
            _ => "void"
    private static string GetOpenCLType(Type type)
    private static string GetCSharpType(Type type)
        return type.Name switch
            "Int32" => "int",
            "Single" => "float",
            "Double" => "double",
            "Boolean" => "bool",
            _ => type.Name
    private static DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage ConvertLanguage(DotCompute.Abstractions.Kernels.Types.KernelLanguage language)
        return language switch
            DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp => DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp,
            DotCompute.Abstractions.Kernels.Types.KernelLanguage.CUDA => DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.CUDA,
            DotCompute.Abstractions.Kernels.Types.KernelLanguage.OpenCL => DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.OpenCL,
            DotCompute.Abstractions.Kernels.Types.KernelLanguage.Metal => DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.Metal,
            DotCompute.Abstractions.Kernels.Types.KernelLanguage.SPIRV => DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.SPIRV,
            _ => DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp
    private static GeneratedKernelParameter ConvertParameter(LinqKernelParameter param)
        return new GeneratedKernelParameter
            Name = param.Name,
            Type = param.Type,
            IsInput = param.Direction is Parameters.ParameterDirection.In or Parameters.ParameterDirection.InOut,
            IsOutput = param.Direction is Parameters.ParameterDirection.Out or Parameters.ParameterDirection.InOut
/// Template for filter (Where) operations.
public class FilterKernelTemplate : IKernelTemplate
    {
        _ = sourceBuilder.AppendLine("// Filter kernel template");
        _ = sourceBuilder.AppendLine($"// Generated for {accelerator.Type}");
        _ = sourceBuilder.AppendLine("// TODO: Implement filter kernel generation");
            Language = DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp,
            Parameters = [.. KernelDefinitionAdapter.ExtractParameters(definition).Select(ConvertParameter)],
/// Template for reduce operations.
public class ReduceKernelTemplate : IKernelTemplate
    {
        _ = sourceBuilder.AppendLine("// Reduce kernel template");
        _ = sourceBuilder.AppendLine("// TODO: Implement reduce kernel generation");
/// Template for sort operations.
public class SortKernelTemplate : IKernelTemplate
    {
        _ = sourceBuilder.AppendLine("// Sort kernel template");
        _ = sourceBuilder.AppendLine("// TODO: Implement sort kernel generation");
