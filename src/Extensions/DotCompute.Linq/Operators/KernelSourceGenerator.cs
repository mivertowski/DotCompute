// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.Operators.Types;
using CoreKernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;
namespace DotCompute.Linq.Operators;
{
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
    /// Generates source code for a specific operation type.
    /// <param name="operationType">The operation type.</param>
    /// <param name="definition">The kernel definition.</param>
    /// <returns>Generated source code.</returns>
    public string GenerateSource(string operationType, CoreKernelDefinition definition)
        {
        if (_templates.TryGetValue(operationType, out var template))
        {
            return template.GenerateCode(definition, _targetAcceleratorType);
        }
        // Fallback to generic template
        return GenerateGenericKernel(definition);
    /// Generates source code from an expression tree.
    /// <param name="expression">The expression to generate from.</param>
    /// <param name="context">The generation context.</param>
    public string GenerateFromExpression(Expression expression, KernelGenerationContext context)
        var sourceBuilder = new StringBuilder();
        // Generate kernel signature
        GenerateKernelSignature(sourceBuilder, expression, context);
        // Generate kernel body
        GenerateKernelBody(sourceBuilder, expression, context);
        return sourceBuilder.ToString();
    private void InitializeTemplates()
        _templates["Map"] = new MapCodeTemplate();
        _templates["Filter"] = new FilterCodeTemplate();
        _templates["Reduce"] = new ReduceCodeTemplate();
        _templates["Sort"] = new SortCodeTemplate();
        _templates["Custom"] = new CustomCodeTemplate();
    private string GenerateGenericKernel(CoreKernelDefinition definition)
        _ = sourceBuilder.AppendLine($"// Generated kernel: {definition.Name}");
        _ = sourceBuilder.AppendLine($"// Target: {_targetAcceleratorType}");
        _ = sourceBuilder.AppendLine();
        // Generate based on target accelerator
        switch (_targetAcceleratorType)
            case AcceleratorType.CUDA:
                GenerateCudaKernel(sourceBuilder, definition);
                break;
            case AcceleratorType.OpenCL:
                GenerateOpenCLKernel(sourceBuilder, definition);
            case AcceleratorType.Metal:
                GenerateMetalKernel(sourceBuilder, definition);
            case AcceleratorType.CPU:
                GenerateCSharpKernel(sourceBuilder, definition);
            default:
    private void GenerateKernelSignature(StringBuilder sourceBuilder, Expression expression, KernelGenerationContext context)
        var kernelName = $"generated_kernel_{expression.NodeType.ToString().ToLowerInvariant()}";
        _ = _targetAcceleratorType switch
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
    private void GenerateKernelBody(StringBuilder sourceBuilder, Expression expression, KernelGenerationContext context)
        _ = sourceBuilder.AppendLine("    if (idx < size) {");
        // Generate expression-specific code
        GenerateExpressionCode(sourceBuilder, expression, context);
        _ = sourceBuilder.AppendLine("}");
    private static void GenerateExpressionCode(StringBuilder sourceBuilder, Expression expression, KernelGenerationContext context)
        {
        switch (expression.NodeType)
            case ExpressionType.Call:
                if (expression is MethodCallExpression methodCall)
                {
                    GenerateMethodCallCode(sourceBuilder, methodCall, context);
                }
            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
            case ExpressionType.Divide:
                if (expression is BinaryExpression binaryExpr)
                    GenerateBinaryOperationCode(sourceBuilder, binaryExpr, context);
                _ = sourceBuilder.AppendLine("        // TODO: Implement expression generation");
                _ = sourceBuilder.AppendLine("        output[idx] = input[idx];");
    private static void GenerateMethodCallCode(StringBuilder sourceBuilder, MethodCallExpression methodCall, KernelGenerationContext context)
        switch (methodCall.Method.Name)
            case "Select":
                _ = sourceBuilder.AppendLine("        // Map operation");
                _ = sourceBuilder.AppendLine("        output[idx] = transform(input[idx]);");
            case "Where":
                _ = sourceBuilder.AppendLine("        // Filter operation");
                _ = sourceBuilder.AppendLine("        if (predicate(input[idx])) {");
                _ = sourceBuilder.AppendLine("            output[idx] = input[idx];");
                _ = sourceBuilder.AppendLine("        }");
                _ = sourceBuilder.AppendLine($"        // Method call: {methodCall.Method.Name}");
    private static void GenerateBinaryOperationCode(StringBuilder sourceBuilder, BinaryExpression binaryExpr, KernelGenerationContext context)
        var operatorSymbol = binaryExpr.NodeType switch
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            _ => "?"
        _ = sourceBuilder.AppendLine($"        // Binary operation: {binaryExpr.NodeType}");
        _ = sourceBuilder.AppendLine($"        output[idx] = left[idx] {operatorSymbol} right[idx];");
    private static void GenerateCudaKernel(StringBuilder sourceBuilder, CoreKernelDefinition definition)
        _ = sourceBuilder.AppendLine($"__global__ void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "");
        _ = sourceBuilder.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sourceBuilder.AppendLine("    // CUDA kernel implementation");
    private static void GenerateOpenCLKernel(StringBuilder sourceBuilder, CoreKernelDefinition definition)
        _ = sourceBuilder.AppendLine($"__kernel void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "__global ");
        _ = sourceBuilder.AppendLine("    int idx = get_global_id(0);");
        _ = sourceBuilder.AppendLine("    // OpenCL kernel implementation");
    private static void GenerateMetalKernel(StringBuilder sourceBuilder, CoreKernelDefinition definition)
        _ = sourceBuilder.AppendLine($"kernel void {definition.Name}(");
        GenerateParameters(sourceBuilder, definition, "device ");
        _ = sourceBuilder.AppendLine("    uint idx = thread_position_in_grid;");
        _ = sourceBuilder.AppendLine("    // Metal kernel implementation");
    private static void GenerateVulkanKernel(StringBuilder sourceBuilder, CoreKernelDefinition definition)
        _ = sourceBuilder.AppendLine("#version 450");
        _ = sourceBuilder.AppendLine("layout(local_size_x = 256) in;");
        _ = sourceBuilder.AppendLine("void main() {");
        _ = sourceBuilder.AppendLine("    uint idx = gl_GlobalInvocationID.x;");
        _ = sourceBuilder.AppendLine("    // Vulkan compute shader implementation");
    private static void GenerateCSharpKernel(StringBuilder sourceBuilder, CoreKernelDefinition definition)
        _ = sourceBuilder.AppendLine($"public void {definition.Name}(");
        _ = sourceBuilder.AppendLine("    int idx = GetGlobalThreadId();");
        _ = sourceBuilder.AppendLine("    // C# kernel implementation");
    private static void GenerateParameters(StringBuilder sourceBuilder, CoreKernelDefinition definition, string qualifier)
        // Extract parameters from metadata
        if (!definition.Metadata.TryGetValue("Parameters", out var paramsObj) || paramsObj is not Parameters.KernelParameter[] parameters)
            return;
        for (var i = 0; i < parameters.Length; i++)
            var param = parameters[i];
            var typeStr = GetTypeString(param.Type);
            _ = sourceBuilder.Append("    ");
            if (!string.IsNullOrEmpty(qualifier))
            {
                _ = sourceBuilder.Append(qualifier);
            }
            _ = sourceBuilder.Append(typeStr);
            _ = sourceBuilder.Append("* ");
            _ = sourceBuilder.Append(param.Name);
            if (i < parameters.Length - 1)
                _ = sourceBuilder.Append(",");
            _ = sourceBuilder.AppendLine();
    private static string GetTypeString(Type type)
        return Type.GetTypeCode(type) switch
            TypeCode.Int32 => "int",
            TypeCode.Single => "float",
            TypeCode.Double => "double",
            TypeCode.Boolean => "bool",
            TypeCode.Byte => "uchar",
            TypeCode.UInt32 => "uint",
            _ => "void"
}
/// Interface for code generation templates.
public interface ICodeTemplate
    {
    /// Generates code for a kernel definition.
    /// <param name="acceleratorType">The target accelerator type.</param>
    /// <returns>Generated code.</returns>
    public string GenerateCode(CoreKernelDefinition definition, AcceleratorType acceleratorType);
/// Template for map operations.
public class MapCodeTemplate : ICodeTemplate
    {
    public string GenerateCode(CoreKernelDefinition definition, AcceleratorType acceleratorType)
        _ = sourceBuilder.AppendLine($"// Map operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized map template");
/// Template for filter operations.
public class FilterCodeTemplate : ICodeTemplate
    {
        _ = sourceBuilder.AppendLine($"// Filter operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized filter template");
/// Template for reduce operations.
public class ReduceCodeTemplate : ICodeTemplate
    {
        _ = sourceBuilder.AppendLine($"// Reduce operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized reduce template");
/// Template for sort operations.
public class SortCodeTemplate : ICodeTemplate
    {
        _ = sourceBuilder.AppendLine($"// Sort operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement specialized sort template");
/// Template for custom operations.
public class CustomCodeTemplate : ICodeTemplate
    {
        _ = sourceBuilder.AppendLine($"// Custom operation template for {acceleratorType}");
        _ = sourceBuilder.AppendLine("// TODO: Implement custom operation logic");
