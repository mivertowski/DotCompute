// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Text;
using DotCompute.Abstractions;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Generates OpenCL kernels from expressions and operations.
/// </summary>
public sealed class OpenCLKernelGenerator : IKernelGenerator
{
    private readonly Dictionary<string, Func<Type[], Type, KernelGenerationContext, GeneratedKernel>> _operationGenerators;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelGenerator"/> class.
    /// </summary>
    public OpenCLKernelGenerator()
    {
        _operationGenerators = new Dictionary<string, Func<Type[], Type, KernelGenerationContext, GeneratedKernel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["VectorAdd"] = GenerateVectorAddKernel,
            ["VectorSubtract"] = GenerateVectorSubtractKernel,
            ["MatrixMultiply"] = GenerateMatrixMultiplyKernel,
            ["Convolution"] = GenerateConvolutionKernel,
            ["FFT"] = GenerateFFTKernel,
            ["Reduce"] = GenerateReduceKernel,
            ["Map"] = GenerateMapKernel,
            ["Filter"] = GenerateFilterKernel
        };
    }

    /// <inheritdoc/>
    public AcceleratorType AcceleratorType => AcceleratorType.OpenCL;

    /// <inheritdoc/>
    public GeneratedKernel GenerateKernel(Expression expression, KernelGenerationContext context)
    {
        var visitor = new OpenCLExpressionVisitor(context);
        var kernelBody = visitor.Visit(expression);

        var parameters = visitor.GetParameters();
        var source = BuildKernelSource(visitor.KernelName, parameters, kernelBody, context);

        return new GeneratedKernel
        {
            Name = visitor.KernelName,
            Source = source,
            Language = KernelLanguage.OpenCL,
            Parameters = parameters,
            RequiredWorkGroupSize = context.WorkGroupDimensions,
            SharedMemorySize = visitor.SharedMemorySize,
            OptimizationMetadata = visitor.OptimizationMetadata
        };
    }

    /// <inheritdoc/>
    public GeneratedKernel GenerateOperationKernel(string operation, Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        if (_operationGenerators.TryGetValue(operation, out var generator))
        {
            return generator(inputTypes, outputType, context);
        }

        throw new NotSupportedException($"Operation '{operation}' is not supported by the OpenCL kernel generator.");
    }

    /// <inheritdoc/>
    public bool CanCompile(Expression expression)
    {
        return expression.NodeType switch
        {
            ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or ExpressionType.Divide => true,
            ExpressionType.Call => CanCompileMethodCall((MethodCallExpression)expression),
            ExpressionType.Lambda => CanCompile(((LambdaExpression)expression).Body),
            ExpressionType.Parameter or ExpressionType.Constant => true,
            ExpressionType.ArrayIndex or ExpressionType.ArrayLength => true,
            ExpressionType.Convert => CanCompileConvert((UnaryExpression)expression),
            _ => false
        };
    }

    /// <inheritdoc/>
    public KernelOptimizationHints GetOptimizationHints(KernelGenerationContext context)
    {
        var deviceInfo = context.DeviceInfo;
        
        return new KernelOptimizationHints
        {
            UnrollLoops = deviceInfo.MaxThreadsPerBlock >= 256,
            VectorWidth = GetOptimalVectorWidth(deviceInfo),
            UseFMA = deviceInfo.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || 
                     deviceInfo.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase),
            PrefetchData = deviceInfo.TotalMemory > 4L * 1024 * 1024 * 1024, // > 4GB
            CacheConfig = GetOptimalCacheConfig(deviceInfo),
            AdditionalHints = new Dictionary<string, object>
            {
                ["UseNativeFunctions"] = true,
                ["UseAsyncCopy"] = deviceInfo.DeviceType != "CPU"
            }
        };
    }

    private static GeneratedKernel GenerateVectorAddKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetOpenCLType(elementType);
        var vectorWidth = context.UseVectorTypes ? GetOptimalVectorWidth(context.DeviceInfo) : 1;

        var source = new StringBuilder();
        source.AppendLine($"__kernel void vector_add(");
        source.AppendLine($"    __global const {typeStr}* a,");
        source.AppendLine($"    __global const {typeStr}* b,");
        source.AppendLine($"    __global {typeStr}* result,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = get_global_id(0);");
        
        if (vectorWidth > 1)
        {
            source.AppendLine($"    int vec_idx = idx * {vectorWidth};");
            source.AppendLine($"    if (vec_idx + {vectorWidth} <= n) {{");
            source.AppendLine($"        {typeStr}{vectorWidth} va = vload{vectorWidth}(idx, a);");
            source.AppendLine($"        {typeStr}{vectorWidth} vb = vload{vectorWidth}(idx, b);");
            source.AppendLine($"        vstore{vectorWidth}(va + vb, idx, result);");
            source.AppendLine("    } else if (vec_idx < n) {");
            source.AppendLine("        for (int i = vec_idx; i < n; i++) {");
            source.AppendLine("            result[i] = a[i] + b[i];");
            source.AppendLine("        }");
            source.AppendLine("    }");
        }
        else
        {
            source.AppendLine("    if (idx < n) {");
            source.AppendLine("        result[idx] = a[idx] + b[idx];");
            source.AppendLine("    }");
        }
        
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "vector_add",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "a", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "b", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "result", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            ],
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["VectorWidth"] = vectorWidth,
                ["ElementType"] = typeStr
            }
        };
    }

    private static GeneratedKernel GenerateVectorSubtractKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetOpenCLType(elementType);
        var vectorWidth = context.UseVectorTypes ? GetOptimalVectorWidth(context.DeviceInfo) : 1;

        var source = new StringBuilder();
        source.AppendLine($"__kernel void vector_subtract(");
        source.AppendLine($"    __global const {typeStr}* a,");
        source.AppendLine($"    __global const {typeStr}* b,");
        source.AppendLine($"    __global {typeStr}* result,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = get_global_id(0);");
        
        if (vectorWidth > 1)
        {
            source.AppendLine($"    int vec_idx = idx * {vectorWidth};");
            source.AppendLine($"    if (vec_idx + {vectorWidth} <= n) {{");
            source.AppendLine($"        {typeStr}{vectorWidth} va = vload{vectorWidth}(idx, a);");
            source.AppendLine($"        {typeStr}{vectorWidth} vb = vload{vectorWidth}(idx, b);");
            source.AppendLine($"        vstore{vectorWidth}(va - vb, idx, result);");
            source.AppendLine("    } else if (vec_idx < n) {");
            source.AppendLine("        for (int i = vec_idx; i < n; i++) {");
            source.AppendLine("            result[i] = a[i] - b[i];");
            source.AppendLine("        }");
            source.AppendLine("    }");
        }
        else
        {
            source.AppendLine("    if (idx < n) {");
            source.AppendLine("        result[idx] = a[idx] - b[idx];");
            source.AppendLine("    }");
        }
        
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "vector_subtract",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "a", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "b", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "result", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            ],
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["VectorWidth"] = vectorWidth,
                ["ElementType"] = typeStr
            }
        };
    }

    private static GeneratedKernel GenerateMatrixMultiplyKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetOpenCLType(elementType);
        var tileSize = context.WorkGroupDimensions?[0] ?? 16;

        var source = new StringBuilder();
        
        // Tiled matrix multiplication with shared memory
        source.AppendLine($"#define TILE_SIZE {tileSize}");
        source.AppendLine();
        source.AppendLine($"__kernel void matrix_multiply(");
        source.AppendLine($"    __global const {typeStr}* A,");
        source.AppendLine($"    __global const {typeStr}* B,");
        source.AppendLine($"    __global {typeStr}* C,");
        source.AppendLine($"    const int M,");
        source.AppendLine($"    const int N,");
        source.AppendLine($"    const int K)");
        source.AppendLine("{");
        
        if (context.UseSharedMemory)
        {
            source.AppendLine($"    __local {typeStr} As[TILE_SIZE][TILE_SIZE];");
            source.AppendLine($"    __local {typeStr} Bs[TILE_SIZE][TILE_SIZE];");
            source.AppendLine();
            source.AppendLine("    int bx = get_group_id(0);");
            source.AppendLine("    int by = get_group_id(1);");
            source.AppendLine("    int tx = get_local_id(0);");
            source.AppendLine("    int ty = get_local_id(1);");
            source.AppendLine();
            source.AppendLine("    int row = by * TILE_SIZE + ty;");
            source.AppendLine("    int col = bx * TILE_SIZE + tx;");
            source.AppendLine();
            source.AppendLine($"    {typeStr} sum = 0;");
            source.AppendLine();
            source.AppendLine("    for (int t = 0; t < (K + TILE_SIZE - 1) / TILE_SIZE; t++) {");
            source.AppendLine("        if (row < M && t * TILE_SIZE + tx < K) {");
            source.AppendLine("            As[ty][tx] = A[row * K + t * TILE_SIZE + tx];");
            source.AppendLine("        } else {");
            source.AppendLine("            As[ty][tx] = 0;");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        if (t * TILE_SIZE + ty < K && col < N) {");
            source.AppendLine("            Bs[ty][tx] = B[(t * TILE_SIZE + ty) * N + col];");
            source.AppendLine("        } else {");
            source.AppendLine("            Bs[ty][tx] = 0;");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        barrier(CLK_LOCAL_MEM_FENCE);");
            source.AppendLine();
            source.AppendLine("        #pragma unroll");
            source.AppendLine("        for (int k = 0; k < TILE_SIZE; k++) {");
            
            if (context.Metadata?.ContainsKey("UseFMA") == true && (bool)context.Metadata["UseFMA"])
            {
                source.AppendLine("            sum = fma(As[ty][k], Bs[k][tx], sum);");
            }
            else
            {
                source.AppendLine("            sum += As[ty][k] * Bs[k][tx];");
            }
            
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        barrier(CLK_LOCAL_MEM_FENCE);");
            source.AppendLine("    }");
            source.AppendLine();
            source.AppendLine("    if (row < M && col < N) {");
            source.AppendLine("        C[row * N + col] = sum;");
            source.AppendLine("    }");
        }
        else
        {
            // Simple non-tiled version
            source.AppendLine("    int row = get_global_id(1);");
            source.AppendLine("    int col = get_global_id(0);");
            source.AppendLine();
            source.AppendLine("    if (row < M && col < N) {");
            source.AppendLine($"        {typeStr} sum = 0;");
            source.AppendLine("        for (int k = 0; k < K; k++) {");
            source.AppendLine("            sum += A[row * K + k] * B[k * N + col];");
            source.AppendLine("        }");
            source.AppendLine("        C[row * N + col] = sum;");
            source.AppendLine("    }");
        }
        
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "matrix_multiply",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "A", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "B", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "C", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "M", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "N", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "K", Type = typeof(int), IsInput = true }
            ],
            RequiredWorkGroupSize = [tileSize, tileSize],
            SharedMemorySize = 2 * tileSize * tileSize * GetTypeSize(elementType),
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["TileSize"] = tileSize,
                ["UseSharedMemory"] = context.UseSharedMemory,
                ["ElementType"] = typeStr
            }
        };
    }

    private static GeneratedKernel GenerateConvolutionKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetOpenCLType(elementType);

        var source = new StringBuilder();
        source.AppendLine($"__kernel void convolution_2d(");
        source.AppendLine($"    __global const {typeStr}* input,");
        source.AppendLine($"    __global const {typeStr}* kernel,");
        source.AppendLine($"    __global {typeStr}* output,");
        source.AppendLine($"    const int input_width,");
        source.AppendLine($"    const int input_height,");
        source.AppendLine($"    const int kernel_width,");
        source.AppendLine($"    const int kernel_height,");
        source.AppendLine($"    const int pad_x,");
        source.AppendLine($"    const int pad_y)");
        source.AppendLine("{");
        source.AppendLine("    int out_x = get_global_id(0);");
        source.AppendLine("    int out_y = get_global_id(1);");
        source.AppendLine();
        source.AppendLine("    int output_width = input_width + 2 * pad_x - kernel_width + 1;");
        source.AppendLine("    int output_height = input_height + 2 * pad_y - kernel_height + 1;");
        source.AppendLine();
        source.AppendLine("    if (out_x < output_width && out_y < output_height) {");
        source.AppendLine($"        {typeStr} sum = 0;");
        source.AppendLine();
        source.AppendLine("        for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("            for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                int in_x = out_x - pad_x + kx;");
        source.AppendLine("                int in_y = out_y - pad_y + ky;");
        source.AppendLine();
        source.AppendLine("                if (in_x >= 0 && in_x < input_width && in_y >= 0 && in_y < input_height) {");
        source.AppendLine($"                    {typeStr} input_val = input[in_y * input_width + in_x];");
        source.AppendLine($"                    {typeStr} kernel_val = kernel[ky * kernel_width + kx];");
        
        if (context.Metadata?.ContainsKey("UseFMA") == true && (bool)context.Metadata["UseFMA"])
        {
            source.AppendLine("                    sum = fma(input_val, kernel_val, sum);");
        }
        else
        {
            source.AppendLine("                    sum += input_val * kernel_val;");
        }
        
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        output[out_y * output_width + out_x] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "convolution_2d",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "pad_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "pad_y", Type = typeof(int), IsInput = true }
            ]
        };
    }

    private static GeneratedKernel GenerateFFTKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        
        // Stockham FFT implementation
        source.AppendLine("typedef float2 complex_t;");
        source.AppendLine();
        source.AppendLine("complex_t complex_mul(complex_t a, complex_t b) {");
        source.AppendLine("    return (complex_t)(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);");
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("complex_t twiddle(int k, int N) {");
        source.AppendLine("    float angle = -2.0f * M_PI_F * k / N;");
        source.AppendLine("    return (complex_t)(cos(angle), sin(angle));");
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("__kernel void fft_stockham(");
        source.AppendLine("    __global complex_t* input,");
        source.AppendLine("    __global complex_t* output,");
        source.AppendLine("    const int N,");
        source.AppendLine("    const int stride,");
        source.AppendLine("    const int offset)");
        source.AppendLine("{");
        source.AppendLine("    int tid = get_global_id(0);");
        source.AppendLine("    int block_size = N / stride;");
        source.AppendLine("    int block_id = tid / (block_size / 2);");
        source.AppendLine("    int thread_id = tid % (block_size / 2);");
        source.AppendLine();
        source.AppendLine("    int in_offset = block_id * block_size + thread_id;");
        source.AppendLine("    int out_offset = block_id * block_size + 2 * thread_id;");
        source.AppendLine();
        source.AppendLine("    complex_t u = input[in_offset];");
        source.AppendLine("    complex_t v = input[in_offset + block_size / 2];");
        source.AppendLine();
        source.AppendLine("    complex_t w = twiddle(thread_id * stride, N);");
        source.AppendLine("    complex_t vw = complex_mul(v, w);");
        source.AppendLine();
        source.AppendLine("    output[out_offset] = u + vw;");
        source.AppendLine("    output[out_offset + 1] = u - vw;");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "fft_stockham",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "N", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "offset", Type = typeof(int), IsInput = true }
            ]
        };
    }

    private static GeneratedKernel GenerateReduceKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetOpenCLType(elementType);
        var workGroupSize = context.WorkGroupDimensions?[0] ?? 256;

        var source = new StringBuilder();
        source.AppendLine($"#define WORK_GROUP_SIZE {workGroupSize}");
        source.AppendLine();
        source.AppendLine($"__kernel void reduce_sum(");
        source.AppendLine($"    __global const {typeStr}* input,");
        source.AppendLine($"    __global {typeStr}* output,");
        source.AppendLine($"    __local {typeStr}* shared,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int tid = get_local_id(0);");
        source.AppendLine("    int gid = get_global_id(0);");
        source.AppendLine("    int group_id = get_group_id(0);");
        source.AppendLine();
        source.AppendLine("    // Load data into shared memory");
        source.AppendLine($"    shared[tid] = (gid < n) ? input[gid] : 0;");
        source.AppendLine("    barrier(CLK_LOCAL_MEM_FENCE);");
        source.AppendLine();
        source.AppendLine("    // Reduce in shared memory");
        source.AppendLine("    for (int stride = WORK_GROUP_SIZE / 2; stride > 0; stride /= 2) {");
        source.AppendLine("        if (tid < stride) {");
        source.AppendLine("            shared[tid] += shared[tid + stride];");
        source.AppendLine("        }");
        source.AppendLine("        barrier(CLK_LOCAL_MEM_FENCE);");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    // Write result");
        source.AppendLine("    if (tid == 0) {");
        source.AppendLine("        output[group_id] = shared[0];");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "reduce_sum",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "shared", Type = elementType.MakeArrayType(), MemorySpace = MemorySpace.Shared },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            ],
            RequiredWorkGroupSize = [workGroupSize],
            SharedMemorySize = workGroupSize * GetTypeSize(elementType)
        };
    }

    private static GeneratedKernel GenerateMapKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetOpenCLType(elementType);

        var source = new StringBuilder();
        source.AppendLine($"__kernel void map_function(");
        source.AppendLine($"    __global const {typeStr}* input,");
        source.AppendLine($"    __global {typeStr}* output,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = get_global_id(0);");
        source.AppendLine("    if (idx < n) {");
        source.AppendLine($"        {typeStr} x = input[idx];");
        // Apply user-defined transformation through function dispatch
        source.AppendLine("        output[idx] = transform_element(x);");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "map_function",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            ]
        };
    }

    private static GeneratedKernel GenerateFilterKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetOpenCLType(elementType);

        var source = new StringBuilder();
        source.AppendLine($"__kernel void filter_predicate(");
        source.AppendLine($"    __global const {typeStr}* input,");
        source.AppendLine($"    __global {typeStr}* output,");
        source.AppendLine($"    __global int* output_count,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = get_global_id(0);");
        source.AppendLine("    if (idx < n) {");
        source.AppendLine($"        {typeStr} x = input[idx];");
        // Apply user-defined predicate through function dispatch
        source.AppendLine("        bool keep = predicate_function(x);");
        source.AppendLine("        if (keep) {");
        source.AppendLine("            int out_idx = atomic_inc(output_count);");
        source.AppendLine("            output[out_idx] = x;");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "filter_predicate",
            Source = source.ToString(),
            Language = KernelLanguage.OpenCL,
            Parameters =
            [
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "output_count", Type = typeof(int).MakeArrayType(), IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            ]
        };
    }

    private static string BuildKernelSource(string name, KernelParameter[] parameters, string body, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        
        // Add precision pragma if needed
        if (context.Precision == PrecisionMode.Double)
        {
            source.AppendLine("#pragma OPENCL EXTENSION cl_khr_fp64 : enable");
        }
        else if (context.Precision == PrecisionMode.Half)
        {
            source.AppendLine("#pragma OPENCL EXTENSION cl_khr_fp16 : enable");
        }

        source.AppendLine();
        source.AppendLine($"__kernel void {name}(");
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var qualifier = param.MemorySpace switch
            {
                MemorySpace.Global => "__global",
                MemorySpace.Shared => "__local",
                MemorySpace.Constant => "__constant",
                MemorySpace.Private => "__private",
                _ => "__global"
            };

            var readOnly = param.IsReadOnly ? "const " : "";
            var typeStr = GetOpenCLType(param.Type);
            
            source.Append($"    {qualifier} {readOnly}{typeStr}* {param.Name}");
            
            if (i < parameters.Length - 1)
            {
                source.AppendLine(",");
            }
        }
        
        source.AppendLine(")");
        source.AppendLine("{");
        source.AppendLine(body);
        source.AppendLine("}");

        return source.ToString();
    }

    private static bool CanCompileMethodCall(MethodCallExpression call)
    {
        var method = call.Method;
        
        // Check for supported LINQ methods
        if (method.DeclaringType == typeof(Enumerable) || method.DeclaringType == typeof(Queryable))
        {
            return method.Name switch
            {
                "Select" or "Where" or "Sum" or "Average" or "Min" or "Max" => true,
                "Count" or "Any" or "All" => true,
                _ => false
            };
        }

        // Check for supported math operations
        if (method.DeclaringType == typeof(Math) || method.DeclaringType == typeof(MathF))
        {
            return method.Name switch
            {
                "Abs" or "Sign" or "Min" or "Max" => true,
                "Sin" or "Cos" or "Tan" or "Asin" or "Acos" or "Atan" => true,
                "Exp" or "Log" or "Log10" or "Pow" or "Sqrt" => true,
                "Ceiling" or "Floor" or "Round" => true,
                _ => false
            };
        }

        return false;
    }

    private static bool CanCompileConvert(UnaryExpression convert)
    {
        var fromType = convert.Operand.Type;
        var toType = convert.Type;

        // Allow numeric conversions
        return IsNumericType(fromType) && IsNumericType(toType);
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(float) || type == typeof(double) ||
               type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
               type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte);
    }

    private static string GetOpenCLType(Type type)
    {
        if (type.IsArray)
        {
            type = type.GetElementType() ?? type;
        }

        return type switch
        {
            _ when type == typeof(float) => "float",
            _ when type == typeof(double) => "double",
            _ when type == typeof(int) => "int",
            _ when type == typeof(uint) => "uint",
            _ when type == typeof(long) => "long",
            _ when type == typeof(ulong) => "ulong",
            _ when type == typeof(short) => "short",
            _ when type == typeof(ushort) => "ushort",
            _ when type == typeof(byte) => "uchar",
            _ when type == typeof(sbyte) => "char",
            _ when type == typeof(bool) => "bool",
            _ => throw new NotSupportedException($"Type {type} is not supported in OpenCL kernels.")
        };
    }

    private static Type GetElementType(Type type)
    {
        return type.IsArray ? type.GetElementType() ?? typeof(float) : type;
    }

    private static int GetTypeSize(Type type)
    {
        return type switch
        {
            _ when type == typeof(float) => 4,
            _ when type == typeof(double) => 8,
            _ when type == typeof(int) => 4,
            _ when type == typeof(uint) => 4,
            _ when type == typeof(long) => 8,
            _ when type == typeof(ulong) => 8,
            _ when type == typeof(short) => 2,
            _ when type == typeof(ushort) => 2,
            _ when type == typeof(byte) => 1,
            _ when type == typeof(sbyte) => 1,
            _ => 4
        };
    }

    private static int GetOptimalVectorWidth(AcceleratorInfo deviceInfo)
    {
        // Use device hints or default based on device type
        if (deviceInfo.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return 4; // float4 is optimal for NVIDIA
        }
        else if (deviceInfo.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase))
        {
            return 4; // float4 for AMD as well
        }
        else if (deviceInfo.DeviceType == "CPU")
        {
            return 8; // AVX supports 8 floats
        }
        
        return 1; // Default to scalar
    }

    private static CacheConfiguration GetOptimalCacheConfig(AcceleratorInfo deviceInfo)
    {
        if (deviceInfo.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return CacheConfiguration.PreferL1;
        }
        else if (deviceInfo.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase))
        {
            return CacheConfiguration.PreferShared;
        }
        
        return CacheConfiguration.Default;
    }
}

/// <summary>
/// Expression visitor for OpenCL kernel generation.
/// </summary>
internal sealed class OpenCLExpressionVisitor : ExpressionVisitor
{
    private readonly KernelGenerationContext _context;
    private readonly StringBuilder _kernelBody;
    private readonly List<KernelParameter> _parameters;
    private int _parameterIndex;

    public OpenCLExpressionVisitor(KernelGenerationContext context)
    {
        _context = context;
        _kernelBody = new StringBuilder();
        _parameters = [];
        KernelName = "generated_kernel";
        OptimizationMetadata = [];
    }

    public string KernelName { get; set; }
    public int SharedMemorySize { get; set; }
    public Dictionary<string, object> OptimizationMetadata { get; }

    public KernelParameter[] GetParameters()
    {
        return [.. _parameters];
    }

    public new string Visit(Expression expression)
    {
        base.Visit(expression);
        return _kernelBody.ToString();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _kernelBody.Append("(");
        Visit(node.Left);
        
        var op = node.NodeType switch
        {
            ExpressionType.Add => " + ",
            ExpressionType.Subtract => " - ",
            ExpressionType.Multiply => " * ",
            ExpressionType.Divide => " / ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            ExpressionType.Equal => " == ",
            ExpressionType.NotEqual => " != ",
            _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported.")
        };
        
        _kernelBody.Append(op);
        Visit(node.Right);
        _kernelBody.Append(")");
        
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        var paramName = $"param{_parameterIndex++}";
        _parameters.Add(new KernelParameter
        {
            Name = paramName,
            Type = node.Type,
            IsInput = true,
            IsReadOnly = true
        });
        
        _kernelBody.Append(paramName);
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value == null)
        {
            _kernelBody.Append("0");
        }
        else
        {
            _kernelBody.Append(node.Value.ToString());
        }
        
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;
        
        if (method.DeclaringType == typeof(Math) || method.DeclaringType == typeof(MathF))
        {
            var funcName = method.Name.ToLowerInvariant();
            _kernelBody.Append($"{funcName}(");
            
            for (var i = 0; i < node.Arguments.Count; i++)
            {
                if (i > 0)
                {
                    _kernelBody.Append(", ");
                }

                Visit(node.Arguments[i]);
            }
            
            _kernelBody.Append(")");
        }
        else
        {
            throw new NotSupportedException($"Method {method.Name} is not supported in kernel generation.");
        }
        
        return node;
    }
}