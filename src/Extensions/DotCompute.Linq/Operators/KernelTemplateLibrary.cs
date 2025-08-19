// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using System.Text;

namespace DotCompute.Linq.Operators;


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
    _templates = new Dictionary<string, IKernelTemplate>();
    InitializeTemplates();
}

/// <summary>
/// Gets a kernel template by name.
/// </summary>
/// <param name="templateName">The template name.</param>
/// <returns>The kernel template.</returns>
public IKernelTemplate GetTemplate(string templateName)
{
    if (_templates.TryGetValue(templateName, out var template))
    {
        return template;
    }
    
    throw new InvalidOperationException($"Kernel template '{templateName}' not found");
}

/// <summary>
/// Gets all available template names.
/// </summary>
/// <returns>The template names.</returns>
public IReadOnlyList<string> GetTemplateNames()
{
    return _templates.Keys.ToArray();
}

private void InitializeTemplates()
{
    _templates["MapOperation"] = new MapKernelTemplate();
    _templates["FilterOperation"] = new FilterKernelTemplate();
    _templates["ReduceOperation"] = new ReduceKernelTemplate();
    _templates["SortOperation"] = new SortKernelTemplate();
}
}

/// <summary>
/// Interface for kernel templates that generate code.
/// </summary>
public interface IKernelTemplate
{
/// <summary>
/// Generates a kernel from a definition.
/// </summary>
/// <param name="definition">The kernel definition.</param>
/// <param name="accelerator">The target accelerator.</param>
/// <returns>A generated kernel.</returns>
GeneratedKernel Generate(KernelDefinition definition, IAccelerator accelerator);
}

/// <summary>
/// Template for map (Select) operations.
/// </summary>
public class MapKernelTemplate : IKernelTemplate
{
/// <inheritdoc/>
public GeneratedKernel Generate(KernelDefinition definition, IAccelerator accelerator)
{
    var sourceBuilder = new StringBuilder();
    
    // Generate kernel based on accelerator type
    switch (accelerator.Type)
    {
        case AcceleratorType.CUDA:
            GenerateCudaMapKernel(sourceBuilder, definition);
            break;
        case AcceleratorType.OpenCL:
            GenerateOpenCLMapKernel(sourceBuilder, definition);
            break;
        default:
            GenerateGenericMapKernel(sourceBuilder, definition);
            break;
    }
    
    return new GeneratedKernel
    {
        Name = definition.Name,
        Source = sourceBuilder.ToString(),
        Language = ConvertLanguage(definition.Language),
        Parameters = definition.Parameters.Select(ConvertParameter).ToArray(),
        SharedMemorySize = 0,
        OptimizationMetadata = definition.Metadata
    };
}

private static void GenerateCudaMapKernel(StringBuilder source, KernelDefinition definition)
{
    source.AppendLine("__global__ void " + definition.Name + "(");
    
    // Generate parameters
    var parameters = definition.Parameters;
    for (int i = 0; i < parameters.Count; i++)
    {
        var param = parameters[i];
        source.Append("    ");
        source.Append(GetCudaType(param.Type));
        source.Append("* ");
        source.Append(param.Name);
        if (i < parameters.Count - 1) source.Append(",");
        source.AppendLine();
    }
    
    source.AppendLine(") {");
    source.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
    source.AppendLine("    int size = gridDim.x * blockDim.x;");
    source.AppendLine("    ");
    source.AppendLine("    if (idx < size) {");
    source.AppendLine("        // Map operation logic here");
    source.AppendLine("        output[idx] = transform(input_0[idx]);");
    source.AppendLine("    }");
    source.AppendLine("}");
}

private static void GenerateOpenCLMapKernel(StringBuilder source, KernelDefinition definition)
{
    source.AppendLine("__kernel void " + definition.Name + "(");
    
    // Generate parameters
    var parameters = definition.Parameters;
    for (int i = 0; i < parameters.Count; i++)
    {
        var param = parameters[i];
        source.Append("    __global ");
        source.Append(GetOpenCLType(param.Type));
        source.Append("* ");
        source.Append(param.Name);
        if (i < parameters.Count - 1) source.Append(",");
        source.AppendLine();
    }
    
    source.AppendLine(") {");
    source.AppendLine("    int idx = get_global_id(0);");
    source.AppendLine("    int size = get_global_size(0);");
    source.AppendLine("    ");
    source.AppendLine("    if (idx < size) {");
    source.AppendLine("        // Map operation logic here");
    source.AppendLine("        output[idx] = transform(input_0[idx]);");
    source.AppendLine("    }");
    source.AppendLine("}");
}

private static void GenerateGenericMapKernel(StringBuilder source, KernelDefinition definition)
{
    source.AppendLine("// Generic map kernel template");
    source.AppendLine("public void " + definition.Name + "(");
    
    // Generate parameters
    var parameters = definition.Parameters;
    for (int i = 0; i < parameters.Count; i++)
    {
        var param = parameters[i];
        source.Append("    ");
        source.Append(GetCSharpType(param.Type));
        source.Append(" ");
        source.Append(param.Name);
        if (i < parameters.Count - 1) source.Append(",");
        source.AppendLine();
    }
    
    source.AppendLine(") {");
    source.AppendLine("    var threadId = GetThreadId();");
    source.AppendLine("    var totalThreads = GetTotalThreads();");
    source.AppendLine("    ");
    source.AppendLine("    for (int i = threadId; i < size; i += totalThreads) {");
    source.AppendLine("        output[i] = Transform(input_0[i]);");
    source.AppendLine("    }");
    source.AppendLine("}");
}

private static string GetCudaType(Type type)
{
    return Type.GetTypeCode(type) switch
    {
        TypeCode.Int32 => "int",
        TypeCode.Single => "float",
        TypeCode.Double => "double",
        TypeCode.Boolean => "bool",
        _ => "void"
    };
}

private static string GetOpenCLType(Type type)
{
    return Type.GetTypeCode(type) switch
    {
        TypeCode.Int32 => "int",
        TypeCode.Single => "float",
        TypeCode.Double => "double",
        TypeCode.Boolean => "bool",
        _ => "void"
    };
}

private static string GetCSharpType(Type type)
{
    return type.Name switch
    {
        "Int32" => "int",
        "Single" => "float",
        "Double" => "double",
        "Boolean" => "bool",
        _ => type.Name
    };
}

private static Core.Kernels.KernelLanguage ConvertLanguage(KernelLanguage language)
{
    return language switch
    {
        KernelLanguage.CSharp => Core.Kernels.KernelLanguage.CSharp,
        KernelLanguage.CUDA => Core.Kernels.KernelLanguage.CUDA,
        KernelLanguage.OpenCL => Core.Kernels.KernelLanguage.OpenCL,
        KernelLanguage.Metal => Core.Kernels.KernelLanguage.Metal,
        KernelLanguage.SPIRV => Core.Kernels.KernelLanguage.OpenCL, // SPIRV not available, fallback to OpenCL
        _ => Core.Kernels.KernelLanguage.CSharp
    };
}

private static GeneratedKernelParameter ConvertParameter(KernelParameter param)
{
    return new GeneratedKernelParameter
    {
        Name = param.Name,
        Type = param.Type,
        IsInput = param.Direction == ParameterDirection.In || param.Direction == ParameterDirection.InOut,
        IsOutput = param.Direction == ParameterDirection.Out || param.Direction == ParameterDirection.InOut
    };
}
}

/// <summary>
/// Template for filter (Where) operations.
/// </summary>
public class FilterKernelTemplate : IKernelTemplate
{
/// <inheritdoc/>
public GeneratedKernel Generate(KernelDefinition definition, IAccelerator accelerator)
{
    var sourceBuilder = new StringBuilder();
    
    sourceBuilder.AppendLine("// Filter kernel template");
    sourceBuilder.AppendLine($"// Generated for {accelerator.Type}");
    sourceBuilder.AppendLine("// TODO: Implement filter kernel generation");
    
    return new GeneratedKernel
    {
        Name = definition.Name,
        Source = sourceBuilder.ToString(),
        Language = Core.Kernels.KernelLanguage.CSharp,
        Parameters = definition.Parameters.Select(ConvertParameter).ToArray(),
        OptimizationMetadata = definition.Metadata
    };
}

private static GeneratedKernelParameter ConvertParameter(KernelParameter param)
{
    return new GeneratedKernelParameter
    {
        Name = param.Name,
        Type = param.Type,
        IsInput = param.Direction == ParameterDirection.In || param.Direction == ParameterDirection.InOut,
        IsOutput = param.Direction == ParameterDirection.Out || param.Direction == ParameterDirection.InOut
    };
}
}

/// <summary>
/// Template for reduce operations.
/// </summary>
public class ReduceKernelTemplate : IKernelTemplate
{
/// <inheritdoc/>
public GeneratedKernel Generate(KernelDefinition definition, IAccelerator accelerator)
{
    var sourceBuilder = new StringBuilder();
    
    sourceBuilder.AppendLine("// Reduce kernel template");
    sourceBuilder.AppendLine($"// Generated for {accelerator.Type}");
    sourceBuilder.AppendLine("// TODO: Implement reduce kernel generation");
    
    return new GeneratedKernel
    {
        Name = definition.Name,
        Source = sourceBuilder.ToString(),
        Language = Core.Kernels.KernelLanguage.CSharp,
        Parameters = definition.Parameters.Select(ConvertParameter).ToArray(),
        OptimizationMetadata = definition.Metadata
    };
}

private static GeneratedKernelParameter ConvertParameter(KernelParameter param)
{
    return new GeneratedKernelParameter
    {
        Name = param.Name,
        Type = param.Type,
        IsInput = param.Direction == ParameterDirection.In || param.Direction == ParameterDirection.InOut,
        IsOutput = param.Direction == ParameterDirection.Out || param.Direction == ParameterDirection.InOut
    };
}
}

/// <summary>
/// Template for sort operations.
/// </summary>
public class SortKernelTemplate : IKernelTemplate
{
/// <inheritdoc/>
public GeneratedKernel Generate(KernelDefinition definition, IAccelerator accelerator)
{
    var sourceBuilder = new StringBuilder();
    
    sourceBuilder.AppendLine("// Sort kernel template");
    sourceBuilder.AppendLine($"// Generated for {accelerator.Type}");
    sourceBuilder.AppendLine("// TODO: Implement sort kernel generation");
    
    return new GeneratedKernel
    {
        Name = definition.Name,
        Source = sourceBuilder.ToString(),
        Language = Core.Kernels.KernelLanguage.CSharp,
        Parameters = definition.Parameters.Select(ConvertParameter).ToArray(),
        OptimizationMetadata = definition.Metadata
    };
}

private static GeneratedKernelParameter ConvertParameter(KernelParameter param)
{
    return new GeneratedKernelParameter
    {
        Name = param.Name,
        Type = param.Type,
        IsInput = param.Direction == ParameterDirection.In || param.Direction == ParameterDirection.InOut,
        IsOutput = param.Direction == ParameterDirection.Out || param.Direction == ParameterDirection.InOut
    };
}
}
