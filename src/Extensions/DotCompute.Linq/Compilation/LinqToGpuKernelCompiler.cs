// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
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
        _ = sb.AppendLine("extern \"C\" __global__");
        _ = sb.AppendLine($"void select_kernel({GetCudaType<TSource>()}* input, {GetCudaType<TResult>()}* output, int count) {{");
        _ = sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    if (idx >= count) return;");
        _ = sb.AppendLine();

        // Generate selector body
        var selectorBody = CompileExpression(selector.Body, "input[idx]");
        _ = sb.AppendLine($"    output[idx] = {selectorBody};");
        _ = sb.AppendLine("}");

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
        _ = sb.AppendLine("extern \"C\" __global__");
        _ = sb.AppendLine($"void where_kernel({GetCudaType<T>()}* input, {GetCudaType<T>()}* output, int* output_count, int count) {{");
        _ = sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    if (idx >= count) return;");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"    {GetCudaType<T>()} value = input[idx];");

        // Generate predicate check
        var predicateBody = CompileExpression(predicate.Body, "value");
        _ = sb.AppendLine($"    if ({predicateBody}) {{");
        _ = sb.AppendLine("        int output_idx = atomicAdd(output_count, 1);");
        _ = sb.AppendLine("        output[output_idx] = value;");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

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
        _ = sb.AppendLine("extern \"C\" __global__");
        _ = sb.AppendLine($"void aggregate_kernel({GetCudaType<T>()}* input, {GetCudaType<T>()}* output, int count) {{");
        _ = sb.AppendLine("    extern __shared__ " + GetCudaType<T>() + " sdata[];");
        _ = sb.AppendLine("    ");
        _ = sb.AppendLine("    unsigned int tid = threadIdx.x;");
        _ = sb.AppendLine("    unsigned int i = blockIdx.x * (blockDim.x * 2) + tid;");
        _ = sb.AppendLine("    unsigned int gridSize = blockDim.x * 2 * gridDim.x;");
        _ = sb.AppendLine("    ");

        // Load and perform first reduction during load
        _ = sb.AppendLine($"    {GetCudaType<T>()} mySum = 0;");
        _ = sb.AppendLine("    while (i < count) {");

        if (operation == "sum")
        {
            _ = sb.AppendLine("        mySum += input[i];");
            _ = sb.AppendLine("        if (i + blockDim.x < count)");
            _ = sb.AppendLine("            mySum += input[i + blockDim.x];");
        }
        else if (operation == "max")
        {
            _ = sb.AppendLine("        mySum = max(mySum, input[i]);");
            _ = sb.AppendLine("        if (i + blockDim.x < count)");
            _ = sb.AppendLine("            mySum = max(mySum, input[i + blockDim.x]);");
        }
        else if (operation == "min")
        {
            _ = sb.AppendLine("        mySum = min(mySum, input[i]);");
            _ = sb.AppendLine("        if (i + blockDim.x < count)");
            _ = sb.AppendLine("            mySum = min(mySum, input[i + blockDim.x]);");
        }
        else
        {
            // Custom aggregator
            var aggregatorBody = CompileExpression(aggregator.Body, "mySum", "input[i]");
            _ = sb.AppendLine($"        mySum = {aggregatorBody};");
            _ = sb.AppendLine("        if (i + blockDim.x < count) {");
            _ = sb.AppendLine($"            mySum = {CompileExpression(aggregator.Body, "mySum", "input[i + blockDim.x]")};");
            _ = sb.AppendLine("        }");
        }

        _ = sb.AppendLine("        i += gridSize;");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("    ");
        _ = sb.AppendLine("    sdata[tid] = mySum;");
        _ = sb.AppendLine("    __syncthreads();");
        _ = sb.AppendLine("    ");

        // Parallel reduction in shared memory
        _ = sb.AppendLine("    // Parallel reduction in shared memory");
        _ = sb.AppendLine("    for (unsigned int s = blockDim.x / 2; s > 0; s >>= 1) {");
        _ = sb.AppendLine("        if (tid < s) {");

        if (operation == "sum")
        {
            _ = sb.AppendLine("            sdata[tid] += sdata[tid + s];");
        }
        else if (operation == "max")
        {
            _ = sb.AppendLine("            sdata[tid] = max(sdata[tid], sdata[tid + s]);");
        }
        else if (operation == "min")
        {
            _ = sb.AppendLine("            sdata[tid] = min(sdata[tid], sdata[tid + s]);");
        }
        else
        {
            _ = sb.AppendLine($"            sdata[tid] = {CompileExpression(aggregator.Body, "sdata[tid]", "sdata[tid + s]")};");
        }

        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        __syncthreads();");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("    ");
        _ = sb.AppendLine("    // Write result for this block to global memory");
        _ = sb.AppendLine("    if (tid == 0) output[blockIdx.x] = sdata[0];");
        _ = sb.AppendLine("}");

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
        _ = sb.AppendLine("extern \"C\" __global__");
        _ = sb.AppendLine($"void orderby_kernel({GetCudaType<T>()}* data, int count, int stage, int step) {{");
        _ = sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    ");
        _ = sb.AppendLine("    int partner = idx ^ step;");
        _ = sb.AppendLine("    ");
        _ = sb.AppendLine("    if (partner > idx && partner < count) {");
        _ = sb.AppendLine($"        {GetCudaType<T>()} left = data[idx];");
        _ = sb.AppendLine($"        {GetCudaType<T>()} right = data[partner];");
        _ = sb.AppendLine("        ");

        // Generate key extraction
        var leftKey = CompileExpression(keySelector.Body, "left");
        var rightKey = CompileExpression(keySelector.Body, "right");

        _ = sb.AppendLine($"        auto leftKey = {leftKey};");
        _ = sb.AppendLine($"        auto rightKey = {rightKey};");
        _ = sb.AppendLine("        ");
        _ = sb.AppendLine("        bool ascending = ((idx & (stage << 1)) == 0);");
        _ = sb.AppendLine("        bool swap = (leftKey > rightKey) == ascending;");
        _ = sb.AppendLine("        ");
        _ = sb.AppendLine("        if (swap) {");
        _ = sb.AppendLine("            data[idx] = right;");
        _ = sb.AppendLine("            data[partner] = left;");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateCudaKernel(Expression body, ParameterExpression parameter, Type elementType)
    {
        var sb = new StringBuilder();

        // Analyze expression to determine kernel type
        if (body is MethodCallExpression methodCall)
        {
            return methodCall.Method.Name switch
            {
                "Select" => GenerateSelectKernel(methodCall, elementType),
                "Where" => GenerateWhereKernel(methodCall, elementType),
                "Sum" or "Average" or "Min" or "Max" => GenerateReductionKernel(methodCall, elementType),
                "OrderBy" or "OrderByDescending" => GenerateSortKernel(methodCall, elementType),
                _ => throw new NotSupportedException($"LINQ method {methodCall.Method.Name} is not supported for GPU compilation"),
            };

        }

        throw new NotSupportedException("Expression type not supported for GPU compilation");
    }

    private string GenerateSelectKernel(MethodCallExpression methodCall, Type elementType)
    {
        var lambda = (LambdaExpression)((UnaryExpression)methodCall.Arguments[1]).Operand;
        var cudaType = GetCudaType(elementType);

        var sb = new StringBuilder();
        _ = sb.AppendLine($"extern \"C\" __global__ void kernel({cudaType}* input, {cudaType}* output, int count) {{");
        _ = sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    if (idx >= count) return;");
        _ = sb.AppendLine($"    output[idx] = {CompileExpression(lambda.Body, "input[idx]")};");
        _ = sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateWhereKernel(MethodCallExpression methodCall, Type elementType)
    {
        var lambda = (LambdaExpression)((UnaryExpression)methodCall.Arguments[1]).Operand;
        var cudaType = GetCudaType(elementType);

        var sb = new StringBuilder();
        _ = sb.AppendLine($"extern \"C\" __global__ void kernel({cudaType}* input, {cudaType}* output, int* count, int total) {{");
        _ = sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    if (idx >= total) return;");
        _ = sb.AppendLine($"    if ({CompileExpression(lambda.Body, "input[idx]")}) {{");
        _ = sb.AppendLine("        int pos = atomicAdd(count, 1);");
        _ = sb.AppendLine("        output[pos] = input[idx];");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

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
        _ = sb.AppendLine($"extern \"C\" __global__ void kernel({cudaType}* data, int count, int stage, int step) {{");
        _ = sb.AppendLine("    // Bitonic sort implementation");
        _ = sb.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    int partner = idx ^ step;");
        _ = sb.AppendLine("    if (partner > idx && partner < count) {");
        _ = sb.AppendLine($"        {cudaType} left = data[idx];");
        _ = sb.AppendLine($"        {cudaType} right = data[partner];");
        _ = sb.AppendLine("        bool ascending = ((idx & (stage << 1)) == 0);");
        _ = sb.AppendLine("        bool swap = (left > right) == ascending;");
        _ = sb.AppendLine("        if (swap) {");
        _ = sb.AppendLine("            data[idx] = right;");
        _ = sb.AppendLine("            data[partner] = left;");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

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
    public string EntryPoint => "LinqGeneratedKernel";
    public string[] Dependencies => Array.Empty<string>();

    public ValueTask<string> GetSourceAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_source);

    public ValueTask<byte[]> GetBinaryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Binary not available for string source");
}
