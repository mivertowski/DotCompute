// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Text;
using DotCompute.Abstractions;

namespace DotCompute.Linq.Operators;

/// <summary>
/// Generates kernel source code for different accelerator types.
/// </summary>
public class KernelSourceGenerator
{
    private readonly AcceleratorType _targetAcceleratorType;
    private readonly Dictionary<string, ICodeTemplate> _templates;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelSourceGenerator"/> class.
    /// </summary>
    /// <param name="targetAcceleratorType">The target accelerator type.</param>
    public KernelSourceGenerator(AcceleratorType targetAcceleratorType)
    {
        _targetAcceleratorType = targetAcceleratorType;
        _templates = new Dictionary<string, ICodeTemplate>();
        InitializeTemplates();
    }

    /// <summary>
    /// Generates source code for a specific operation type.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="definition">The kernel definition.</param>
    /// <returns>Generated source code.</returns>
    public string GenerateSource(string operationType, KernelDefinition definition)
    {
        if (_templates.TryGetValue(operationType, out var template))
        {
            return template.GenerateCode(definition, _targetAcceleratorType);
        }

        // Fallback to generic template
        return GenerateGenericKernel(definition);
    }

    /// <summary>
    /// Generates source code from an expression tree.
    /// </summary>
    /// <param name="expression">The expression to generate from.</param>
    /// <param name="context">The generation context.</param>
    /// <returns>Generated source code.</returns>
    public string GenerateFromExpression(Expression expression, KernelGenerationContext context)
    {
        var sourceBuilder = new StringBuilder();
        
        // Generate kernel signature
        GenerateKernelSignature(sourceBuilder, expression, context);
        
        // Generate kernel body
        GenerateKernelBody(sourceBuilder, expression, context);
        
        return sourceBuilder.ToString();
    }

    private void InitializeTemplates()
    {
        _templates["Map"] = new MapCodeTemplate();
        _templates["Filter"] = new FilterCodeTemplate();
        _templates["Reduce"] = new ReduceCodeTemplate();
        _templates["Sort"] = new SortCodeTemplate();
        _templates["Custom"] = new CustomCodeTemplate();
    }

    private string GenerateGenericKernel(KernelDefinition definition)
    {
        var sourceBuilder = new StringBuilder();
        
        sourceBuilder.AppendLine($"// Generated kernel: {definition.Name}");
        sourceBuilder.AppendLine($"// Target: {_targetAcceleratorType}");
        sourceBuilder.AppendLine();
        
        // Generate based on target accelerator
        switch (_targetAcceleratorType)
        {
            case AcceleratorType.CUDA:
                GenerateCudaKernel(sourceBuilder, definition);
                break;
            case AcceleratorType.OpenCL:
                GenerateOpenCLKernel(sourceBuilder, definition);
                break;
            case AcceleratorType.Metal:
                GenerateMetalKernel(sourceBuilder, definition);
                break;
            case AcceleratorType.Vulkan:
                GenerateVulkanKernel(sourceBuilder, definition);
                break;
            default:
                GenerateCSharpKernel(sourceBuilder, definition);
                break;
        }
        
        return sourceBuilder.ToString();
    }

    private void GenerateKernelSignature(StringBuilder sourceBuilder, Expression expression, KernelGenerationContext context)
    {
        var kernelName = $"generated_kernel_{expression.NodeType.ToString().ToLowerInvariant()}";
        
        switch (_targetAcceleratorType)
        {
            case AcceleratorType.CUDA:
                sourceBuilder.AppendLine($"__global__ void {kernelName}(");
                break;
            case AcceleratorType.OpenCL:
                sourceBuilder.AppendLine($"__kernel void {kernelName}(");
                break;
            case AcceleratorType.Metal:
                sourceBuilder.AppendLine($"kernel void {kernelName}(");
                break;
            default:
                sourceBuilder.AppendLine($"public void {kernelName}(");
                break;
        }
        
        // Generate parameters (simplified)
        sourceBuilder.AppendLine("    global float* input,");
        sourceBuilder.AppendLine("    global float* output,");
        sourceBuilder.AppendLine("    int size");
        sourceBuilder.AppendLine(") {");
    }

    private void GenerateKernelBody(StringBuilder sourceBuilder, Expression expression, KernelGenerationContext context)
    {
        // Generate thread indexing
        switch (_targetAcceleratorType)
        {
            case AcceleratorType.CUDA:
                sourceBuilder.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
                break;
            case AcceleratorType.OpenCL:
                sourceBuilder.AppendLine("    int idx = get_global_id(0);");
                break;
            case AcceleratorType.Metal:
                sourceBuilder.AppendLine("    int idx = thread_position_in_grid;");
                break;
            default:
                sourceBuilder.AppendLine("    int idx = GetGlobalThreadId();");
                break;
        }
        
        sourceBuilder.AppendLine("    ");
        sourceBuilder.AppendLine("    if (idx < size) {");
        
        // Generate expression-specific code
        GenerateExpressionCode(sourceBuilder, expression, context);
        
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine("}");
    }

    private static void GenerateExpressionCode(StringBuilder sourceBuilder, Expression expression, KernelGenerationContext context)
    {
        switch (expression.NodeType)
        {
            case ExpressionType.Call:
                if (expression is MethodCallExpression methodCall)
                {
                    GenerateMethodCallCode(sourceBuilder, methodCall, context);
                }
                break;
            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
            case ExpressionType.Divide:
                if (expression is BinaryExpression binaryExpr)
                {
                    GenerateBinaryOperationCode(sourceBuilder, binaryExpr, context);
                }
                break;
            default:
                sourceBuilder.AppendLine("        // TODO: Implement expression generation");
                sourceBuilder.AppendLine("        output[idx] = input[idx];");
                break;
        }
    }

    private static void GenerateMethodCallCode(StringBuilder sourceBuilder, MethodCallExpression methodCall, KernelGenerationContext context)
    {
        switch (methodCall.Method.Name)
        {
            case "Select":
                sourceBuilder.AppendLine("        // Map operation");
                sourceBuilder.AppendLine("        output[idx] = transform(input[idx]);");
                break;
            case "Where":
                sourceBuilder.AppendLine("        // Filter operation");
                sourceBuilder.AppendLine("        if (predicate(input[idx])) {");
                sourceBuilder.AppendLine("            output[idx] = input[idx];");
                sourceBuilder.AppendLine("        }");
                break;
            default:
                sourceBuilder.AppendLine($"        // Method call: {methodCall.Method.Name}");
                sourceBuilder.AppendLine("        output[idx] = input[idx];");
                break;
        }
    }

    private static void GenerateBinaryOperationCode(StringBuilder sourceBuilder, BinaryExpression binaryExpr, KernelGenerationContext context)
    {
        var operatorSymbol = binaryExpr.NodeType switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            _ => "?"
        };
        
        sourceBuilder.AppendLine($"        // Binary operation: {binaryExpr.NodeType}");
        sourceBuilder.AppendLine($"        output[idx] = left[idx] {operatorSymbol} right[idx];");
    }

    private void GenerateCudaKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        sourceBuilder.AppendLine($"__global__ void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "");
        sourceBuilder.AppendLine(") {");
        sourceBuilder.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        sourceBuilder.AppendLine("    // CUDA kernel implementation");
        sourceBuilder.AppendLine("}");
    }

    private void GenerateOpenCLKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        sourceBuilder.AppendLine($"__kernel void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "__global ");
        sourceBuilder.AppendLine(") {");
        sourceBuilder.AppendLine("    int idx = get_global_id(0);");
        sourceBuilder.AppendLine("    // OpenCL kernel implementation");
        sourceBuilder.AppendLine("}");
    }

    private void GenerateMetalKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        sourceBuilder.AppendLine($"kernel void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "device ");
        sourceBuilder.AppendLine(") {");
        sourceBuilder.AppendLine("    uint idx = thread_position_in_grid;");
        sourceBuilder.AppendLine("    // Metal kernel implementation");
        sourceBuilder.AppendLine("}");
    }

    private static void GenerateVulkanKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        sourceBuilder.AppendLine("#version 450");
        sourceBuilder.AppendLine("layout(local_size_x = 256) in;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("void main() {");
        sourceBuilder.AppendLine("    uint idx = gl_GlobalInvocationID.x;");
        sourceBuilder.AppendLine("    // Vulkan compute shader implementation");
        sourceBuilder.AppendLine("}");
    }

    private void GenerateCSharpKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        sourceBuilder.AppendLine($"public void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "");
        sourceBuilder.AppendLine(") {");
        sourceBuilder.AppendLine("    int idx = GetGlobalThreadId();");
        sourceBuilder.AppendLine("    // C# kernel implementation");
        sourceBuilder.AppendLine("}");
    }

    private static void GenerateParameters(StringBuilder sourceBuilder, KernelDefinition definition, string qualifier)
    {
        for (int i = 0; i < definition.Parameters.Length; i++)
        {
            var param = definition.Parameters[i];
            var typeStr = GetTypeString(param.Type);
            
            sourceBuilder.Append("    ");
            if (!string.IsNullOrEmpty(qualifier))
            {
                sourceBuilder.Append(qualifier);
            }
            sourceBuilder.Append(typeStr);
            sourceBuilder.Append("* ");
            sourceBuilder.Append(param.Name);
            
            if (i < definition.Parameters.Length - 1)
            {
                sourceBuilder.Append(",");
            }
            
            sourceBuilder.AppendLine();
        }
    }

    private static string GetTypeString(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Int32 => "int",
            TypeCode.Single => "float",
            TypeCode.Double => "double",
            TypeCode.Boolean => "bool",
            TypeCode.Byte => "uchar",
            TypeCode.UInt32 => "uint",
            _ => "void"
        };
    }
}

/// <summary>
/// Interface for code generation templates.
/// </summary>
public interface ICodeTemplate
{
    /// <summary>
    /// Generates code for a kernel definition.
    /// </summary>
    /// <param name="definition">The kernel definition.</param>
    /// <param name="acceleratorType">The target accelerator type.</param>
    /// <returns>Generated code.</returns>
    string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType);
}

/// <summary>
/// Template for map operations.
/// </summary>
public class MapCodeTemplate : ICodeTemplate
{
    public string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($"// Map operation template for {acceleratorType}");
        sourceBuilder.AppendLine("// TODO: Implement specialized map template");
        return sourceBuilder.ToString();
    }
}

/// <summary>
/// Template for filter operations.
/// </summary>
public class FilterCodeTemplate : ICodeTemplate
{
    public string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($"// Filter operation template for {acceleratorType}");
        sourceBuilder.AppendLine("// TODO: Implement specialized filter template");
        return sourceBuilder.ToString();
    }
}

/// <summary>
/// Template for reduce operations.
/// </summary>
public class ReduceCodeTemplate : ICodeTemplate
{
    public string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($"// Reduce operation template for {acceleratorType}");
        sourceBuilder.AppendLine("// TODO: Implement specialized reduce template");
        return sourceBuilder.ToString();
    }
}

/// <summary>
/// Template for sort operations.
/// </summary>
public class SortCodeTemplate : ICodeTemplate
{
    public string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($"// Sort operation template for {acceleratorType}");
        sourceBuilder.AppendLine("// TODO: Implement specialized sort template");
        return sourceBuilder.ToString();
    }
}

/// <summary>
/// Template for custom operations.
/// </summary>
public class CustomCodeTemplate : ICodeTemplate
{
    public string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($"// Custom operation template for {acceleratorType}");
        sourceBuilder.AppendLine("// TODO: Implement custom operation logic");
        return sourceBuilder.ToString();
    }
}