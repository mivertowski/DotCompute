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
        _templates = [];
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

        _ = sourceBuilder.AppendLine($"// Generated kernel: {definition.Name}");
        _ = sourceBuilder.AppendLine($"// Target: {_targetAcceleratorType}");
        _ = sourceBuilder.AppendLine();

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
            case AcceleratorType.CPU:
                GenerateCSharpKernel(sourceBuilder, definition);
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

        _ = _targetAcceleratorType switch
        {
            AcceleratorType.CUDA => sourceBuilder.AppendLine($"__global__ void {kernelName}("),
            AcceleratorType.OpenCL => sourceBuilder.AppendLine($"__kernel void {kernelName}("),
            AcceleratorType.Metal => sourceBuilder.AppendLine($"kernel void {kernelName}("),
            _ => sourceBuilder.AppendLine($"public void {kernelName}("),
        };

        // Generate parameters (simplified)

        _ = sourceBuilder.AppendLine("    global float* input,");
        _ = sourceBuilder.AppendLine("    global float* output,");
        _ = sourceBuilder.AppendLine("    int size");
        _ = sourceBuilder.AppendLine(") {");
    }

    private void GenerateKernelBody(StringBuilder sourceBuilder, Expression expression, KernelGenerationContext context)
    {
        // Generate thread indexing
        _ = _targetAcceleratorType switch
        {
            AcceleratorType.CUDA => sourceBuilder.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;"),
            AcceleratorType.OpenCL => sourceBuilder.AppendLine("    int idx = get_global_id(0);"),
            AcceleratorType.Metal => sourceBuilder.AppendLine("    int idx = thread_position_in_grid;"),
            _ => sourceBuilder.AppendLine("    int idx = GetGlobalThreadId();"),
        };
        _ = sourceBuilder.AppendLine("    ");
        _ = sourceBuilder.AppendLine("    if (idx < size) {");

        // Generate expression-specific code
        GenerateExpressionCode(sourceBuilder, expression, context);

        _ = sourceBuilder.AppendLine("    }");
        _ = sourceBuilder.AppendLine("}");
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
                _ = sourceBuilder.AppendLine("        // TODO: Implement expression generation");
                _ = sourceBuilder.AppendLine("        output[idx] = input[idx];");
                break;
        }
    }

    private static void GenerateMethodCallCode(StringBuilder sourceBuilder, MethodCallExpression methodCall, KernelGenerationContext context)
    {
        switch (methodCall.Method.Name)
        {
            case "Select":
                _ = sourceBuilder.AppendLine("        // Map operation");
                _ = sourceBuilder.AppendLine("        output[idx] = transform(input[idx]);");
                break;
            case "Where":
                _ = sourceBuilder.AppendLine("        // Filter operation");
                _ = sourceBuilder.AppendLine("        if (predicate(input[idx])) {");
                _ = sourceBuilder.AppendLine("            output[idx] = input[idx];");
                _ = sourceBuilder.AppendLine("        }");
                break;
            default:
                _ = sourceBuilder.AppendLine($"        // Method call: {methodCall.Method.Name}");
                _ = sourceBuilder.AppendLine("        output[idx] = input[idx];");
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

        _ = sourceBuilder.AppendLine($"        // Binary operation: {binaryExpr.NodeType}");
        _ = sourceBuilder.AppendLine($"        output[idx] = left[idx] {operatorSymbol} right[idx];");
    }

    private static void GenerateCudaKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        _ = sourceBuilder.AppendLine($"__global__ void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "");
        _ = sourceBuilder.AppendLine(") {");
        _ = sourceBuilder.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sourceBuilder.AppendLine("    // CUDA kernel implementation");
        _ = sourceBuilder.AppendLine("}");
    }

    private static void GenerateOpenCLKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        _ = sourceBuilder.AppendLine($"__kernel void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "__global ");
        _ = sourceBuilder.AppendLine(") {");
        _ = sourceBuilder.AppendLine("    int idx = get_global_id(0);");
        _ = sourceBuilder.AppendLine("    // OpenCL kernel implementation");
        _ = sourceBuilder.AppendLine("}");
    }

    private static void GenerateMetalKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        _ = sourceBuilder.AppendLine($"kernel void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "device ");
        _ = sourceBuilder.AppendLine(") {");
        _ = sourceBuilder.AppendLine("    uint idx = thread_position_in_grid;");
        _ = sourceBuilder.AppendLine("    // Metal kernel implementation");
        _ = sourceBuilder.AppendLine("}");
    }

    private static void GenerateVulkanKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        _ = sourceBuilder.AppendLine("#version 450");
        _ = sourceBuilder.AppendLine("layout(local_size_x = 256) in;");
        _ = sourceBuilder.AppendLine();
        _ = sourceBuilder.AppendLine("void main() {");
        _ = sourceBuilder.AppendLine("    uint idx = gl_GlobalInvocationID.x;");
        _ = sourceBuilder.AppendLine("    // Vulkan compute shader implementation");
        _ = sourceBuilder.AppendLine("}");
    }

    private static void GenerateCSharpKernel(StringBuilder sourceBuilder, KernelDefinition definition)
    {
        _ = sourceBuilder.AppendLine($"public void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "");
        _ = sourceBuilder.AppendLine(") {");
        _ = sourceBuilder.AppendLine("    int idx = GetGlobalThreadId();");
        _ = sourceBuilder.AppendLine("    // C# kernel implementation");
        _ = sourceBuilder.AppendLine("}");
    }

    private static void GenerateParameters(StringBuilder sourceBuilder, KernelDefinition definition, string qualifier)
    {
        for (var i = 0; i < definition.Parameters.Count; i++)
        {
            var param = definition.Parameters[i];
            var typeStr = GetTypeString(param.Type);

            _ = sourceBuilder.Append("    ");
            if (!string.IsNullOrEmpty(qualifier))
            {
                _ = sourceBuilder.Append(qualifier);
            }
            _ = sourceBuilder.Append(typeStr);
            _ = sourceBuilder.Append("* ");
            _ = sourceBuilder.Append(param.Name);

            if (i < definition.Parameters.Count - 1)
            {
                _ = sourceBuilder.Append(",");
            }

            _ = sourceBuilder.AppendLine();
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
    public string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType);
}

/// <summary>
/// Template for map operations.
/// </summary>
public class MapCodeTemplate : ICodeTemplate
{
    public string GenerateCode(KernelDefinition definition, AcceleratorType acceleratorType)
    {
        var sourceBuilder = new StringBuilder();
        _ = sourceBuilder.AppendLine($"// Map operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized map template");
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
        _ = sourceBuilder.AppendLine($"// Filter operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized filter template");
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
        _ = sourceBuilder.AppendLine($"// Reduce operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized reduce template");
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
        _ = sourceBuilder.AppendLine($"// Sort operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized sort template");
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
        _ = sourceBuilder.AppendLine($"// Custom operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement custom operation logic");
        return sourceBuilder.ToString();
    }
}
