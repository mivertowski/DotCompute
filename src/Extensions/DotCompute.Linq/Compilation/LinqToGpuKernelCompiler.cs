// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Compilation;


/// <summary>
/// Compiles LINQ expression trees into optimized GPU kernels with CPU fallback support.
/// </summary>
public sealed class LinqToGpuKernelCompiler
{
private readonly ILogger<LinqToGpuKernelCompiler> _logger;
private readonly Dictionary<ExpressionType, string> _cudaOperatorMap;
private readonly Dictionary<MethodInfo, string> _methodMap;

public LinqToGpuKernelCompiler(ILogger<LinqToGpuKernelCompiler> logger)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _cudaOperatorMap = InitializeOperatorMap();
    _methodMap = InitializeMethodMap();
}

/// <summary>
/// Compiles a LINQ expression tree into a CUDA kernel.
/// </summary>
public KernelDefinition CompileExpression<T>(Expression<Func<IEnumerable<T>, IEnumerable<T>>> expression)
    where T : unmanaged
{
    _logger.LogDebug("Compiling LINQ expression to CUDA kernel");

    var body = expression.Body;
    var parameter = expression.Parameters[0];
    
    var kernelCode = GenerateCudaKernel(body, parameter, typeof(T));
    
    return new KernelDefinition(
        $"linq_kernel_{Guid.NewGuid():N}",
        kernelCode
    );
}

/// <summary>
/// Compiles a Select operation into a CUDA kernel.
/// </summary>
public string CompileSelect<TSource, TResult>(Expression<Func<TSource, TResult>> selector)
    where TSource : unmanaged
    where TResult : unmanaged
{
    var sb = new StringBuilder();
    
    // Generate CUDA kernel header
    sb.AppendLine("extern \"C\" __global__");
    sb.AppendLine($"void select_kernel({GetCudaType<TSource>()}* input, {GetCudaType<TResult>()}* output, int count) {{");
    sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
    sb.AppendLine("    if (idx >= count) return;");
    sb.AppendLine();
    
    // Generate selector body
    var selectorBody = CompileExpression(selector.Body, "input[idx]");
    sb.AppendLine($"    output[idx] = {selectorBody};");
    sb.AppendLine("}");

    return sb.ToString();
}

/// <summary>
/// Compiles a Where operation into a CUDA kernel.
/// </summary>
public string CompileWhere<T>(Expression<Func<T, bool>> predicate)
    where T : unmanaged
{
    var sb = new StringBuilder();
    
    // Generate CUDA kernel with stream compaction
    sb.AppendLine("extern \"C\" __global__");
    sb.AppendLine($"void where_kernel({GetCudaType<T>()}* input, {GetCudaType<T>()}* output, int* output_count, int count) {{");
    sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
    sb.AppendLine("    if (idx >= count) return;");
    sb.AppendLine();
    sb.AppendLine($"    {GetCudaType<T>()} value = input[idx];");
    
    // Generate predicate check
    var predicateBody = CompileExpression(predicate.Body, "value");
    sb.AppendLine($"    if ({predicateBody}) {{");
    sb.AppendLine("        int output_idx = atomicAdd(output_count, 1);");
    sb.AppendLine("        output[output_idx] = value;");
    sb.AppendLine("    }");
    sb.AppendLine("}");

    return sb.ToString();
}

/// <summary>
/// Compiles an Aggregate operation into a CUDA kernel using parallel reduction.
/// </summary>
public string CompileAggregate<T>(Expression<Func<T, T, T>> aggregator, string operation = "custom")
    where T : unmanaged
{
    var sb = new StringBuilder();
    
    // Generate optimized parallel reduction kernel
    sb.AppendLine("extern \"C\" __global__");
    sb.AppendLine($"void aggregate_kernel({GetCudaType<T>()}* input, {GetCudaType<T>()}* output, int count) {{");
    sb.AppendLine("    extern __shared__ " + GetCudaType<T>() + " sdata[];");
    sb.AppendLine("    ");
    sb.AppendLine("    unsigned int tid = threadIdx.x;");
    sb.AppendLine("    unsigned int i = blockIdx.x * (blockDim.x * 2) + tid;");
    sb.AppendLine("    unsigned int gridSize = blockDim.x * 2 * gridDim.x;");
    sb.AppendLine("    ");
    
    // Load and perform first reduction during load
    sb.AppendLine($"    {GetCudaType<T>()} mySum = 0;");
    sb.AppendLine("    while (i < count) {");
    
    if (operation == "sum")
    {
        sb.AppendLine("        mySum += input[i];");
        sb.AppendLine("        if (i + blockDim.x < count)");
        sb.AppendLine("            mySum += input[i + blockDim.x];");
    }
    else if (operation == "max")
    {
        sb.AppendLine("        mySum = max(mySum, input[i]);");
        sb.AppendLine("        if (i + blockDim.x < count)");
        sb.AppendLine("            mySum = max(mySum, input[i + blockDim.x]);");
    }
    else if (operation == "min")
    {
        sb.AppendLine("        mySum = min(mySum, input[i]);");
        sb.AppendLine("        if (i + blockDim.x < count)");
        sb.AppendLine("            mySum = min(mySum, input[i + blockDim.x]);");
    }
    else
    {
        // Custom aggregator
        var aggregatorBody = CompileExpression(aggregator.Body, "mySum", "input[i]");
        sb.AppendLine($"        mySum = {aggregatorBody};");
        sb.AppendLine("        if (i + blockDim.x < count) {");
        sb.AppendLine($"            mySum = {CompileExpression(aggregator.Body, "mySum", "input[i + blockDim.x]")};");
        sb.AppendLine("        }");
    }
    
    sb.AppendLine("        i += gridSize;");
    sb.AppendLine("    }");
    sb.AppendLine("    ");
    sb.AppendLine("    sdata[tid] = mySum;");
    sb.AppendLine("    __syncthreads();");
    sb.AppendLine("    ");
    
    // Parallel reduction in shared memory
    sb.AppendLine("    // Parallel reduction in shared memory");
    sb.AppendLine("    for (unsigned int s = blockDim.x / 2; s > 0; s >>= 1) {");
    sb.AppendLine("        if (tid < s) {");
    
    if (operation == "sum")
        {
            sb.AppendLine("            sdata[tid] += sdata[tid + s];");
        }
        else if (operation == "max")
        {
            sb.AppendLine("            sdata[tid] = max(sdata[tid], sdata[tid + s]);");
        }
        else if (operation == "min")
        {
            sb.AppendLine("            sdata[tid] = min(sdata[tid], sdata[tid + s]);");
        }
        else
        {
            sb.AppendLine($"            sdata[tid] = {CompileExpression(aggregator.Body, "sdata[tid]", "sdata[tid + s]")};");
        }

        sb.AppendLine("        }");
    sb.AppendLine("        __syncthreads();");
    sb.AppendLine("    }");
    sb.AppendLine("    ");
    sb.AppendLine("    // Write result for this block to global memory");
    sb.AppendLine("    if (tid == 0) output[blockIdx.x] = sdata[0];");
    sb.AppendLine("}");

    return sb.ToString();
}

/// <summary>
/// Compiles a OrderBy operation using GPU bitonic sort.
/// </summary>
public string CompileOrderBy<T, TKey>(Expression<Func<T, TKey>> keySelector)
    where T : unmanaged
    where TKey : unmanaged, IComparable<TKey>
{
    var sb = new StringBuilder();
    
    // Generate bitonic sort kernel
    sb.AppendLine("extern \"C\" __global__");
    sb.AppendLine($"void orderby_kernel({GetCudaType<T>()}* data, int count, int stage, int step) {{");
    sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
    sb.AppendLine("    ");
    sb.AppendLine("    int partner = idx ^ step;");
    sb.AppendLine("    ");
    sb.AppendLine("    if (partner > idx && partner < count) {");
    sb.AppendLine($"        {GetCudaType<T>()} left = data[idx];");
    sb.AppendLine($"        {GetCudaType<T>()} right = data[partner];");
    sb.AppendLine("        ");
    
    // Generate key extraction
    var leftKey = CompileExpression(keySelector.Body, "left");
    var rightKey = CompileExpression(keySelector.Body, "right");
    
    sb.AppendLine($"        auto leftKey = {leftKey};");
    sb.AppendLine($"        auto rightKey = {rightKey};");
    sb.AppendLine("        ");
    sb.AppendLine("        bool ascending = ((idx & (stage << 1)) == 0);");
    sb.AppendLine("        bool swap = (leftKey > rightKey) == ascending;");
    sb.AppendLine("        ");
    sb.AppendLine("        if (swap) {");
    sb.AppendLine("            data[idx] = right;");
    sb.AppendLine("            data[partner] = left;");
    sb.AppendLine("        }");
    sb.AppendLine("    }");
    sb.AppendLine("}");

    return sb.ToString();
}

private string GenerateCudaKernel(Expression body, ParameterExpression parameter, Type elementType)
{
    var sb = new StringBuilder();
    
    // Analyze expression to determine kernel type
    if (body is MethodCallExpression methodCall)
    {
        switch (methodCall.Method.Name)
        {
            case "Select":
                return GenerateSelectKernel(methodCall, elementType);
            case "Where":
                return GenerateWhereKernel(methodCall, elementType);
            case "Sum":
            case "Average":
            case "Min":
            case "Max":
                return GenerateReductionKernel(methodCall, elementType);
            case "OrderBy":
            case "OrderByDescending":
                return GenerateSortKernel(methodCall, elementType);
            default:
                throw new NotSupportedException($"LINQ method {methodCall.Method.Name} is not supported for GPU compilation");
        }
    }
    
    throw new NotSupportedException("Expression type not supported for GPU compilation");
}

private string GenerateSelectKernel(MethodCallExpression methodCall, Type elementType)
{
    var lambda = (LambdaExpression)((UnaryExpression)methodCall.Arguments[1]).Operand;
    var cudaType = GetCudaType(elementType);
    
    var sb = new StringBuilder();
    sb.AppendLine($"extern \"C\" __global__ void kernel({cudaType}* input, {cudaType}* output, int count) {{");
    sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
    sb.AppendLine("    if (idx >= count) return;");
    sb.AppendLine($"    output[idx] = {CompileExpression(lambda.Body, "input[idx]")};");
    sb.AppendLine("}");
    
    return sb.ToString();
}

private string GenerateWhereKernel(MethodCallExpression methodCall, Type elementType)
{
    var lambda = (LambdaExpression)((UnaryExpression)methodCall.Arguments[1]).Operand;
    var cudaType = GetCudaType(elementType);
    
    var sb = new StringBuilder();
    sb.AppendLine($"extern \"C\" __global__ void kernel({cudaType}* input, {cudaType}* output, int* count, int total) {{");
    sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
    sb.AppendLine("    if (idx >= total) return;");
    sb.AppendLine($"    if ({CompileExpression(lambda.Body, "input[idx]")}) {{");
    sb.AppendLine("        int pos = atomicAdd(count, 1);");
    sb.AppendLine("        output[pos] = input[idx];");
    sb.AppendLine("    }");
    sb.AppendLine("}");
    
    return sb.ToString();
}

private string GenerateReductionKernel(MethodCallExpression methodCall, Type elementType)
{
    var operation = methodCall.Method.Name.ToLowerInvariant();
    var cudaType = GetCudaType(elementType);
    
    return CompileAggregate<float>(null!, operation);
}

private string GenerateSortKernel(MethodCallExpression methodCall, Type elementType)
{
    var lambda = methodCall.Arguments.Count > 1 
        ? (LambdaExpression)((UnaryExpression)methodCall.Arguments[1]).Operand 
        : null;
        
    var cudaType = GetCudaType(elementType);
    
    // Use bitonic sort for GPU
    var sb = new StringBuilder();
    sb.AppendLine($"extern \"C\" __global__ void kernel({cudaType}* data, int count, int stage, int step) {{");
    sb.AppendLine("    // Bitonic sort implementation");
    sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
    sb.AppendLine("    int partner = idx ^ step;");
    sb.AppendLine("    if (partner > idx && partner < count) {");
    sb.AppendLine($"        {cudaType} left = data[idx];");
    sb.AppendLine($"        {cudaType} right = data[partner];");
    sb.AppendLine("        bool ascending = ((idx & (stage << 1)) == 0);");
    sb.AppendLine("        bool swap = (left > right) == ascending;");
    sb.AppendLine("        if (swap) {");
    sb.AppendLine("            data[idx] = right;");
    sb.AppendLine("            data[partner] = left;");
    sb.AppendLine("        }");
    sb.AppendLine("    }");
    sb.AppendLine("}");
    
    return sb.ToString();
}

    private string CompileExpression(Expression expr, string input) => CompileExpression(expr, input, null);

    private string CompileExpression(Expression expr, string left, string? right)
{
    switch (expr)
    {
        case BinaryExpression binary:
            var leftExpr = CompileExpression(binary.Left, left, right);
            var rightExpr = CompileExpression(binary.Right, left, right);
            return _cudaOperatorMap.TryGetValue(binary.NodeType, out var op) 
                ? $"({leftExpr} {op} {rightExpr})"
                : throw new NotSupportedException($"Binary operator {binary.NodeType} not supported");

        case UnaryExpression unary:
            var operand = CompileExpression(unary.Operand, left, right);
            return unary.NodeType switch
            {
                ExpressionType.Negate => $"(-{operand})",
                ExpressionType.Not => $"(!{operand})",
                ExpressionType.Convert => operand, // Simplification
                _ => throw new NotSupportedException($"Unary operator {unary.NodeType} not supported")
            };

        case ParameterExpression:
            return left;

        case ConstantExpression constant:
            return constant.Value?.ToString() ?? "0";

        case MemberExpression member:
            var obj = CompileExpression(member.Expression!, left, right);
            return $"{obj}.{member.Member.Name.ToLowerInvariant()}";

        case MethodCallExpression methodCall:
            if (_methodMap.TryGetValue(methodCall.Method, out var cudaFunc))
            {
                var args = string.Join(", ", methodCall.Arguments.Select(a => CompileExpression(a, left, right)));
                return $"{cudaFunc}({args})";
            }
            throw new NotSupportedException($"Method {methodCall.Method.Name} not supported");

        default:
            throw new NotSupportedException($"Expression type {expr.NodeType} not supported");
    }
}

    private static string GetCudaType<T>() where T : unmanaged => GetCudaType(typeof(T));

    private static string GetCudaType(Type type)
{
    return type.Name switch
    {
        "Int32" => "int",
        "UInt32" => "unsigned int",
        "Int64" => "long long",
        "UInt64" => "unsigned long long",
        "Single" => "float",
        "Double" => "double",
        "Boolean" => "bool",
        "Byte" => "unsigned char",
        "SByte" => "char",
        "Int16" => "short",
        "UInt16" => "unsigned short",
        _ => throw new NotSupportedException($"Type {type.Name} not supported for CUDA")
    };
}

private static Dictionary<ExpressionType, string> InitializeOperatorMap()
{
    return new Dictionary<ExpressionType, string>
    {
        [ExpressionType.Add] = "+",
        [ExpressionType.Subtract] = "-",
        [ExpressionType.Multiply] = "*",
        [ExpressionType.Divide] = "/",
        [ExpressionType.Modulo] = "%",
        [ExpressionType.And] = "&",
        [ExpressionType.Or] = "|",
        [ExpressionType.ExclusiveOr] = "^",
        [ExpressionType.LeftShift] = "<<",
        [ExpressionType.RightShift] = ">>",
        [ExpressionType.AndAlso] = "&&",
        [ExpressionType.OrElse] = "||",
        [ExpressionType.Equal] = "==",
        [ExpressionType.NotEqual] = "!=",
        [ExpressionType.LessThan] = "<",
        [ExpressionType.LessThanOrEqual] = "<=",
        [ExpressionType.GreaterThan] = ">",
        [ExpressionType.GreaterThanOrEqual] = ">="
    };
}

private static Dictionary<MethodInfo, string> InitializeMethodMap()
{
    var dict = new Dictionary<MethodInfo, string>();
    
    // Math functions
    var mathType = typeof(Math);
    dict[mathType.GetMethod(nameof(Math.Abs), new[] { typeof(float) })!] = "fabsf";
    dict[mathType.GetMethod(nameof(Math.Abs), new[] { typeof(double) })!] = "fabs";
    dict[mathType.GetMethod(nameof(Math.Sqrt), new[] { typeof(double) })!] = "sqrt";
    dict[mathType.GetMethod(nameof(Math.Sin), new[] { typeof(double) })!] = "sin";
    dict[mathType.GetMethod(nameof(Math.Cos), new[] { typeof(double) })!] = "cos";
    dict[mathType.GetMethod(nameof(Math.Tan), new[] { typeof(double) })!] = "tan";
    dict[mathType.GetMethod(nameof(Math.Exp), new[] { typeof(double) })!] = "exp";
    dict[mathType.GetMethod(nameof(Math.Log), new[] { typeof(double) })!] = "log";
    dict[mathType.GetMethod(nameof(Math.Pow), new[] { typeof(double), typeof(double) })!] = "pow";
    dict[mathType.GetMethod(nameof(Math.Min), new[] { typeof(float), typeof(float) })!] = "fminf";
    dict[mathType.GetMethod(nameof(Math.Max), new[] { typeof(float), typeof(float) })!] = "fmaxf";
    
    return dict;
}
}

/// <summary>
/// Simple string-based kernel source for CUDA code.
/// </summary>
public sealed class StringKernelSource : IKernelSource
{
private readonly string _source;

public StringKernelSource(string source)
{
    _source = source ?? throw new ArgumentNullException(nameof(source));
}

public string Name => "LinqGeneratedKernel";
public KernelSourceType Type => KernelSourceType.CUDA;
public string Code => _source;
public KernelLanguage Language => KernelLanguage.Cuda;
public string EntryPoint => "kernel";
public string[] Dependencies => Array.Empty<string>();

    public ValueTask<string> GetSourceAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_source);

    public ValueTask<byte[]> GetBinaryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Binary not available for string source");
}
