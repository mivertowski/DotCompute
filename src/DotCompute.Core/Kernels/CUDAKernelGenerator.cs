// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Core.Types;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Generates CUDA kernels from expressions and operations.
/// </summary>
public sealed class CUDAKernelGenerator : IKernelGenerator
{
    private readonly Dictionary<string, Func<Type[], Type, KernelGenerationContext, GeneratedKernel>> _operationGenerators;

    /// <summary>
    /// Initializes a new instance of the <see cref="CUDAKernelGenerator"/> class.
    /// </summary>
    public CUDAKernelGenerator()
    {
        _operationGenerators = new Dictionary<string, Func<Type[], Type, KernelGenerationContext, GeneratedKernel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["VectorAdd"] = GenerateVectorAddKernel,
            ["VectorSubtract"] = GenerateVectorSubtractKernel,
            ["MatrixMultiply"] = GenerateMatrixMultiplyKernel,
            ["Convolution"] = GenerateConvolutionKernel,
            // 1D Convolution variants
            ["DirectConvolution1D"] = GenerateDirectConvolution1D,
            ["StridedConvolution1D"] = GenerateStridedConvolution1D,
            ["DilatedConvolution1D"] = GenerateDilatedConvolution1D,
            // 2D Convolution variants
            ["DirectConvolution2D"] = GenerateDirectConvolution2D,
            ["WinogradConvolution2D"] = GenerateWinogradConvolution2D,
            ["Im2ColConvolution2D"] = GenerateIm2ColConvolution2D,
            ["FFTConvolution2D"] = GenerateFFTConvolution2D,
            ["DepthwiseConvolution2D"] = GenerateDepthwiseConvolution2D,
            ["TransposedConvolution2D"] = GenerateTransposedConvolution2D,
            ["ConvolveRows2D"] = GenerateConvolveRows2D,
            ["ConvolveColumns2D"] = GenerateConvolveColumns2D,
            ["BatchConvolution2D"] = GenerateBatchConvolution2D,
            // 3D Convolution
            ["Convolution3D"] = GenerateConvolution3D,
            // Existing operations
            ["FFT"] = GenerateFFTKernel,
            ["Reduce"] = GenerateReduceKernel,
            ["Map"] = GenerateMapKernel,
            ["Filter"] = GenerateFilterKernel
        };
    }

    /// <inheritdoc/>
    public AcceleratorType AcceleratorType => AcceleratorType.CUDA;

    /// <inheritdoc/>
    public GeneratedKernel GenerateKernel(Expression expression, KernelGenerationContext context)
    {
        var visitor = new CUDAExpressionVisitor(context);
        var kernelBody = visitor.Visit(expression);

        var parameters = visitor.GetParameters();
        var source = BuildKernelSource(visitor.KernelName, parameters, kernelBody, context);

        return new GeneratedKernel
        {
            Name = visitor.KernelName,
            Source = source,
            Language = KernelLanguage.CUDA,
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

        throw new NotSupportedException($"Operation '{operation}' is not supported by the CUDA kernel generator.");
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
            UnrollLoops = true, // CUDA supports #pragma unroll
            VectorWidth = GetOptimalVectorWidth(deviceInfo),
            UseFMA = true, // All modern NVIDIA GPUs support FMA
            PrefetchData = true, // Use __ldg for read-only data
            CacheConfig = CacheConfiguration.PreferL1,
            AdditionalHints = new Dictionary<string, object>
            {
                ["UseWarpShuffle"] = true,
                ["UseTensorCores"] = deviceInfo.Name.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                                    deviceInfo.Name.Contains("A100", StringComparison.OrdinalIgnoreCase) ||
                                    deviceInfo.Name.Contains("V100", StringComparison.OrdinalIgnoreCase),
                ["CooperativeGroups"] = true
            }
        };
    }

    private static GeneratedKernel GenerateVectorAddKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetCUDAType(elementType);
        var vectorWidth = context.UseVectorTypes ? GetOptimalVectorWidth(context.DeviceInfo) : 1;

        var source = new StringBuilder();
        source.AppendLine($"__global__ void vector_add(");
        source.AppendLine($"    const {typeStr}* __restrict__ a,");
        source.AppendLine($"    const {typeStr}* __restrict__ b,");
        source.AppendLine($"    {typeStr}* __restrict__ result,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        
        if (vectorWidth > 1)
        {
            source.AppendLine($"    int vec_idx = idx * {vectorWidth};");
            source.AppendLine($"    if (vec_idx + {vectorWidth} <= n) {{");
            source.AppendLine($"        {typeStr}{vectorWidth} va = reinterpret_cast<const {typeStr}{vectorWidth}*>(a)[idx];");
            source.AppendLine($"        {typeStr}{vectorWidth} vb = reinterpret_cast<const {typeStr}{vectorWidth}*>(b)[idx];");
            source.AppendLine($"        {typeStr}{vectorWidth} vresult;");
            source.AppendLine($"        #pragma unroll");
            source.AppendLine($"        for (int i = 0; i < {vectorWidth}; i++) {{");
            source.AppendLine($"            vresult[i] = va[i] + vb[i];");
            source.AppendLine($"        }}");
            source.AppendLine($"        reinterpret_cast<{typeStr}{vectorWidth}*>(result)[idx] = vresult;");
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
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "a", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "b", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "result", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            },
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
        var typeStr = GetCUDAType(elementType);
        var vectorWidth = context.UseVectorTypes ? GetOptimalVectorWidth(context.DeviceInfo) : 1;

        var source = new StringBuilder();
        source.AppendLine($"__global__ void vector_subtract(");
        source.AppendLine($"    const {typeStr}* __restrict__ a,");
        source.AppendLine($"    const {typeStr}* __restrict__ b,");
        source.AppendLine($"    {typeStr}* __restrict__ result,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        
        if (vectorWidth > 1)
        {
            source.AppendLine($"    int vec_idx = idx * {vectorWidth};");
            source.AppendLine($"    if (vec_idx + {vectorWidth} <= n) {{");
            source.AppendLine($"        {typeStr}{vectorWidth} va = reinterpret_cast<const {typeStr}{vectorWidth}*>(a)[idx];");
            source.AppendLine($"        {typeStr}{vectorWidth} vb = reinterpret_cast<const {typeStr}{vectorWidth}*>(b)[idx];");
            source.AppendLine($"        {typeStr}{vectorWidth} vresult;");
            source.AppendLine($"        #pragma unroll");
            source.AppendLine($"        for (int i = 0; i < {vectorWidth}; i++) {{");
            source.AppendLine($"            vresult[i] = va[i] - vb[i];");
            source.AppendLine($"        }}");
            source.AppendLine($"        reinterpret_cast<{typeStr}{vectorWidth}*>(result)[idx] = vresult;");
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
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "a", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "b", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "result", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            },
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
        var typeStr = GetCUDAType(elementType);
        var tileSize = context.WorkGroupDimensions?[0] ?? 16;

        var source = new StringBuilder();
        
        // Use Tensor Cores if available
        bool useTensorCores = context.Metadata?.ContainsKey("UseTensorCores") == true && 
                             (bool)context.Metadata["UseTensorCores"] &&
                             (elementType == typeof(float) || elementType == typeof(DotCompute.Core.Types.Half));

        if (useTensorCores)
        {
            source.AppendLine("#include <mma.h>");
            source.AppendLine("using namespace nvcuda;");
            source.AppendLine();
        }

        source.AppendLine($"#define TILE_SIZE {tileSize}");
        source.AppendLine();
        source.AppendLine($"__global__ void matrix_multiply(");
        source.AppendLine($"    const {typeStr}* __restrict__ A,");
        source.AppendLine($"    const {typeStr}* __restrict__ B,");
        source.AppendLine($"    {typeStr}* __restrict__ C,");
        source.AppendLine($"    const int M,");
        source.AppendLine($"    const int N,");
        source.AppendLine($"    const int K)");
        source.AppendLine("{");
        
        if (context.UseSharedMemory)
        {
            source.AppendLine($"    __shared__ {typeStr} As[TILE_SIZE][TILE_SIZE + 1]; // +1 to avoid bank conflicts");
            source.AppendLine($"    __shared__ {typeStr} Bs[TILE_SIZE][TILE_SIZE + 1];");
            source.AppendLine();
            source.AppendLine("    int bx = blockIdx.x;");
            source.AppendLine("    int by = blockIdx.y;");
            source.AppendLine("    int tx = threadIdx.x;");
            source.AppendLine("    int ty = threadIdx.y;");
            source.AppendLine();
            source.AppendLine("    int row = by * TILE_SIZE + ty;");
            source.AppendLine("    int col = bx * TILE_SIZE + tx;");
            source.AppendLine();
            source.AppendLine($"    {typeStr} sum = 0;");
            source.AppendLine();
            source.AppendLine("    #pragma unroll");
            source.AppendLine("    for (int t = 0; t < (K + TILE_SIZE - 1) / TILE_SIZE; t++) {");
            source.AppendLine("        if (row < M && t * TILE_SIZE + tx < K) {");
            source.AppendLine("            As[ty][tx] = __ldg(&A[row * K + t * TILE_SIZE + tx]);");
            source.AppendLine("        } else {");
            source.AppendLine("            As[ty][tx] = 0;");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        if (t * TILE_SIZE + ty < K && col < N) {");
            source.AppendLine("            Bs[ty][tx] = __ldg(&B[(t * TILE_SIZE + ty) * N + col]);");
            source.AppendLine("        } else {");
            source.AppendLine("            Bs[ty][tx] = 0;");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        __syncthreads();");
            source.AppendLine();
            source.AppendLine("        #pragma unroll");
            source.AppendLine("        for (int k = 0; k < TILE_SIZE; k++) {");
            source.AppendLine("            sum = fma(As[ty][k], Bs[k][tx], sum);");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        __syncthreads();");
            source.AppendLine("    }");
            source.AppendLine();
            source.AppendLine("    if (row < M && col < N) {");
            source.AppendLine("        C[row * N + col] = sum;");
            source.AppendLine("    }");
        }
        else
        {
            // Simple non-tiled version
            source.AppendLine("    int row = blockIdx.y * blockDim.y + threadIdx.y;");
            source.AppendLine("    int col = blockIdx.x * blockDim.x + threadIdx.x;");
            source.AppendLine();
            source.AppendLine("    if (row < M && col < N) {");
            source.AppendLine($"        {typeStr} sum = 0;");
            source.AppendLine("        for (int k = 0; k < K; k++) {");
            source.AppendLine("            sum = fma(__ldg(&A[row * K + k]), __ldg(&B[k * N + col]), sum);");
            source.AppendLine("        }");
            source.AppendLine("        C[row * N + col] = sum;");
            source.AppendLine("    }");
        }
        
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "matrix_multiply",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "A", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "B", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "C", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "M", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "N", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "K", Type = typeof(int), IsInput = true }
            },
            RequiredWorkGroupSize = new[] { tileSize, tileSize },
            SharedMemorySize = 2 * tileSize * (tileSize + 1) * GetTypeSize(elementType),
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["TileSize"] = tileSize,
                ["UseSharedMemory"] = context.UseSharedMemory,
                ["ElementType"] = typeStr,
                ["UseTensorCores"] = useTensorCores
            }
        };
    }

    private static GeneratedKernel GenerateConvolutionKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetCUDAType(elementType);

        var source = new StringBuilder();
        
        // Use texture memory for better cache locality
        source.AppendLine("// Texture memory would be declared globally");
        source.AppendLine("// texture<float, cudaTextureType2D, cudaReadModeElementType> texInput;");
        source.AppendLine("// texture<float, cudaTextureType2D, cudaReadModeElementType> texKernel;");
        source.AppendLine();
        
        source.AppendLine($"__global__ void convolution_2d(");
        source.AppendLine($"    const {typeStr}* __restrict__ input,");
        source.AppendLine($"    const {typeStr}* __restrict__ kernel,");
        source.AppendLine($"    {typeStr}* __restrict__ output,");
        source.AppendLine($"    const int input_width,");
        source.AppendLine($"    const int input_height,");
        source.AppendLine($"    const int kernel_width,");
        source.AppendLine($"    const int kernel_height,");
        source.AppendLine($"    const int pad_x,");
        source.AppendLine($"    const int pad_y)");
        source.AppendLine("{");
        source.AppendLine("    int out_x = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    int out_y = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine();
        source.AppendLine("    int output_width = input_width + 2 * pad_x - kernel_width + 1;");
        source.AppendLine("    int output_height = input_height + 2 * pad_y - kernel_height + 1;");
        source.AppendLine();
        
        // Use shared memory for kernel if it's small enough
        if (context.UseSharedMemory)
        {
            source.AppendLine($"    __shared__ {typeStr} shared_kernel[16][16]; // Assume max 16x16 kernel");
            source.AppendLine();
            source.AppendLine("    // Cooperatively load kernel into shared memory");
            source.AppendLine("    if (threadIdx.x < kernel_width && threadIdx.y < kernel_height) {");
            source.AppendLine("        shared_kernel[threadIdx.y][threadIdx.x] = kernel[threadIdx.y * kernel_width + threadIdx.x];");
            source.AppendLine("    }");
            source.AppendLine("    __syncthreads();");
            source.AppendLine();
        }
        
        source.AppendLine("    if (out_x < output_width && out_y < output_height) {");
        source.AppendLine($"        {typeStr} sum = 0;");
        source.AppendLine();
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("            #pragma unroll");
        source.AppendLine("            for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                int in_x = out_x - pad_x + kx;");
        source.AppendLine("                int in_y = out_y - pad_y + ky;");
        source.AppendLine();
        source.AppendLine("                if (in_x >= 0 && in_x < input_width && in_y >= 0 && in_y < input_height) {");
        source.AppendLine($"                    {typeStr} input_val = __ldg(&input[in_y * input_width + in_x]);");
        
        if (context.UseSharedMemory)
        {
            source.AppendLine($"                    {typeStr} kernel_val = shared_kernel[ky][kx];");
        }
        else
        {
            source.AppendLine($"                    {typeStr} kernel_val = __ldg(&kernel[ky * kernel_width + kx]);");
        }
        
        source.AppendLine("                    sum = fma(input_val, kernel_val, sum);");
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
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = inputTypes[1], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "pad_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "pad_y", Type = typeof(int), IsInput = true }
            },
            SharedMemorySize = context.UseSharedMemory ? 16 * 16 * GetTypeSize(elementType) : 0
        };
    }

    private static GeneratedKernel GenerateFFTKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        
        // Use cuFFT-compatible layout
        source.AppendLine("#include <cufft.h>");
        source.AppendLine();
        source.AppendLine("struct complex_t {");
        source.AppendLine("    float x, y;");
        source.AppendLine("    __device__ complex_t(float real = 0, float imag = 0) : x(real), y(imag) {}");
        source.AppendLine("    __device__ complex_t operator*(const complex_t& b) const {");
        source.AppendLine("        return complex_t(x * b.x - y * b.y, x * b.y + y * b.x);");
        source.AppendLine("    }");
        source.AppendLine("    __device__ complex_t operator+(const complex_t& b) const {");
        source.AppendLine("        return complex_t(x + b.x, y + b.y);");
        source.AppendLine("    }");
        source.AppendLine("    __device__ complex_t operator-(const complex_t& b) const {");
        source.AppendLine("        return complex_t(x - b.x, y - b.y);");
        source.AppendLine("    }");
        source.AppendLine("};");
        source.AppendLine();
        source.AppendLine("__device__ complex_t twiddle(int k, int N) {");
        source.AppendLine("    float angle = -2.0f * M_PI * k / N;");
        source.AppendLine("    float s, c;");
        source.AppendLine("    sincosf(angle, &s, &c);");
        source.AppendLine("    return complex_t(c, s);");
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("__global__ void fft_stockham(");
        source.AppendLine("    complex_t* input,");
        source.AppendLine("    complex_t* output,");
        source.AppendLine("    const int N,");
        source.AppendLine("    const int stride,");
        source.AppendLine("    const int offset)");
        source.AppendLine("{");
        source.AppendLine("    int tid = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    int block_size = N / stride;");
        source.AppendLine("    int block_id = tid / (block_size / 2);");
        source.AppendLine("    int thread_id = tid % (block_size / 2);");
        source.AppendLine();
        source.AppendLine("    if (tid < N / 2) {");
        source.AppendLine("        int in_offset = block_id * block_size + thread_id;");
        source.AppendLine("        int out_offset = block_id * block_size + 2 * thread_id;");
        source.AppendLine();
        source.AppendLine("        complex_t u = input[in_offset];");
        source.AppendLine("        complex_t v = input[in_offset + block_size / 2];");
        source.AppendLine();
        source.AppendLine("        complex_t w = twiddle(thread_id * stride, N);");
        source.AppendLine("        complex_t vw = v * w;");
        source.AppendLine();
        source.AppendLine("        output[out_offset] = u + vw;");
        source.AppendLine("        output[out_offset + 1] = u - vw;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "fft_stockham",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "N", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "offset", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateReduceKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetCUDAType(elementType);
        var workGroupSize = context.WorkGroupDimensions?[0] ?? 256;

        var source = new StringBuilder();
        
        // Use warp shuffle for efficiency
        source.AppendLine("#include <cooperative_groups.h>");
        source.AppendLine("using namespace cooperative_groups;");
        source.AppendLine();
        source.AppendLine($"#define WARP_SIZE 32");
        source.AppendLine($"#define BLOCK_SIZE {workGroupSize}");
        source.AppendLine();
        
        // Warp reduce function
        source.AppendLine($"__device__ {typeStr} warp_reduce_sum({typeStr} val) {{");
        source.AppendLine("    #pragma unroll");
        source.AppendLine("    for (int offset = WARP_SIZE / 2; offset > 0; offset /= 2) {");
        source.AppendLine("        val += __shfl_down_sync(0xffffffff, val, offset);");
        source.AppendLine("    }");
        source.AppendLine("    return val;");
        source.AppendLine("}");
        source.AppendLine();
        
        source.AppendLine($"__global__ void reduce_sum(");
        source.AppendLine($"    const {typeStr}* __restrict__ input,");
        source.AppendLine($"    {typeStr}* __restrict__ output,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine($"    __shared__ {typeStr} shared[BLOCK_SIZE / WARP_SIZE];");
        source.AppendLine();
        source.AppendLine("    int tid = threadIdx.x;");
        source.AppendLine("    int gid = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    int lane = tid % WARP_SIZE;");
        source.AppendLine("    int wid = tid / WARP_SIZE;");
        source.AppendLine();
        source.AppendLine($"    {typeStr} sum = 0;");
        source.AppendLine();
        source.AppendLine("    // Grid-stride loop");
        source.AppendLine("    for (int i = gid; i < n; i += blockDim.x * gridDim.x) {");
        source.AppendLine("        sum += input[i];");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    // Warp reduce");
        source.AppendLine("    sum = warp_reduce_sum(sum);");
        source.AppendLine();
        source.AppendLine("    // Write warp sum to shared memory");
        source.AppendLine("    if (lane == 0) {");
        source.AppendLine("        shared[wid] = sum;");
        source.AppendLine("    }");
        source.AppendLine("    __syncthreads();");
        source.AppendLine();
        source.AppendLine("    // Final reduce in first warp");
        source.AppendLine("    if (tid < BLOCK_SIZE / WARP_SIZE) {");
        source.AppendLine("        sum = shared[tid];");
        source.AppendLine("        sum = warp_reduce_sum(sum);");
        source.AppendLine("        if (tid == 0) {");
        source.AppendLine("            output[blockIdx.x] = sum;");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "reduce_sum",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            },
            RequiredWorkGroupSize = new[] { workGroupSize },
            SharedMemorySize = (workGroupSize / 32) * GetTypeSize(elementType),
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["UseWarpShuffle"] = true,
                ["BlockSize"] = workGroupSize
            }
        };
    }

    private static GeneratedKernel GenerateMapKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetCUDAType(elementType);

        var source = new StringBuilder();
        source.AppendLine($"__global__ void map_function(");
        source.AppendLine($"    const {typeStr}* __restrict__ input,");
        source.AppendLine($"    {typeStr}* __restrict__ output,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    if (idx < n) {");
        source.AppendLine($"        {typeStr} x = __ldg(&input[idx]);");
        source.AppendLine("        // TODO: Apply user-defined function");
        source.AppendLine("        output[idx] = x * x; // Example: square function");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "map_function",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateFilterKernel(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var elementType = GetElementType(inputTypes[0]);
        var typeStr = GetCUDAType(elementType);

        var source = new StringBuilder();
        source.AppendLine($"__global__ void filter_predicate(");
        source.AppendLine($"    const {typeStr}* __restrict__ input,");
        source.AppendLine($"    {typeStr}* __restrict__ output,");
        source.AppendLine($"    int* __restrict__ output_count,");
        source.AppendLine($"    const int n)");
        source.AppendLine("{");
        source.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    if (idx < n) {");
        source.AppendLine($"        {typeStr} x = __ldg(&input[idx]);");
        source.AppendLine("        // TODO: Apply user-defined predicate");
        source.AppendLine("        bool keep = x > 0; // Example: keep positive values");
        source.AppendLine("        if (keep) {");
        source.AppendLine("            int out_idx = atomicAdd(output_count, 1);");
        source.AppendLine("            output[out_idx] = x;");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "filter_predicate",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = inputTypes[0], IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = outputType, IsOutput = true },
                new KernelParameter { Name = "output_count", Type = typeof(int).MakeArrayType(), IsOutput = true },
                new KernelParameter { Name = "n", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static string BuildKernelSource(string name, KernelParameter[] parameters, string body, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        
        // Add includes
        source.AppendLine("#include <cuda_runtime.h>");
        source.AppendLine("#include <device_launch_parameters.h>");
        source.AppendLine();

        // Add precision support
        if (context.Precision == PrecisionMode.Half)
        {
            source.AppendLine("#include <cuda_fp16.h>");
            source.AppendLine("typedef half DotCompute.Core.Types.Half;");
        }

        source.AppendLine();
        source.AppendLine($"extern \"C\" __global__ void {name}(");
        
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var typeStr = GetCUDAType(param.Type);
            var pointer = param.Type.IsArray ? "*" : "";
            var readOnly = param.IsReadOnly ? "const " : "";
            var restrict = param.IsReadOnly ? " __restrict__" : "";
            
            source.Append($"    {readOnly}{typeStr}{pointer}{restrict} {param.Name}");
            
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
               type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || 
               type == typeof(sbyte) || type == typeof(DotCompute.Core.Types.Half);
    }

    private static string GetCUDAType(Type type)
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
            _ when type == typeof(uint) => "unsigned int",
            _ when type == typeof(long) => "long long",
            _ when type == typeof(ulong) => "unsigned long long",
            _ when type == typeof(short) => "short",
            _ when type == typeof(ushort) => "unsigned short",
            _ when type == typeof(byte) => "unsigned char",
            _ when type == typeof(sbyte) => "char",
            _ when type == typeof(bool) => "bool",
            _ when type == typeof(DotCompute.Core.Types.Half) => "half",
            _ => throw new NotSupportedException($"Type {type} is not supported in CUDA kernels.")
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
            _ when type == typeof(DotCompute.Core.Types.Half) => 2,
            _ => 4
        };
    }

    private static int GetOptimalVectorWidth(AcceleratorInfo deviceInfo)
    {
        // CUDA typically uses float4 for coalesced memory access
        return 4;
    }

    #region Convolution Kernel Generators

    private static GeneratedKernel GenerateDirectConvolution1D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        
        source.AppendLine("__global__ void direct_convolution_1d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_length,");
        source.AppendLine("    const int kernel_length,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int tid = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int output_length = input_length + kernel_length - 1;");
        source.AppendLine();
        source.AppendLine("    if (tid < output_length) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int k = 0; k < kernel_length; k++) {");
        source.AppendLine("            int in_idx = tid - k;");
        source.AppendLine("            if (in_idx >= 0 && in_idx < input_length) {");
        source.AppendLine("                sum = fma(__ldg(&input[in_idx]), __ldg(&kernel[k]), sum);");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        output[tid] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "direct_convolution_1d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateStridedConvolution1D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();

        source.AppendLine("__global__ void strided_convolution_1d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_length,");
        source.AppendLine("    const int kernel_length,");
        source.AppendLine("    const int stride,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int tid = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int output_length = (input_length - kernel_length + stride) / stride;");
        source.AppendLine("    if (tid < output_length) {");
        source.AppendLine("        const int input_start = tid * stride;");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int k = 0; k < kernel_length; k++) {");
        source.AppendLine("            int in_idx = input_start + k;");
        source.AppendLine("            if (in_idx < input_length) {");
        source.AppendLine("                sum = fma(__ldg(&input[in_idx]), __ldg(&kernel[k]), sum);");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        output[tid] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "strided_convolution_1d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateDilatedConvolution1D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();

        source.AppendLine("__global__ void dilated_convolution_1d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_length,");
        source.AppendLine("    const int kernel_length,");
        source.AppendLine("    const int dilation,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int tid = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int effective_kernel_size = (kernel_length - 1) * dilation + 1;");
        source.AppendLine("    const int output_length = input_length - effective_kernel_size + 1;");
        source.AppendLine("    if (tid < output_length) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int k = 0; k < kernel_length; k++) {");
        source.AppendLine("            int in_idx = tid + k * dilation;");
        source.AppendLine("            if (in_idx < input_length) {");
        source.AppendLine("                sum = fma(__ldg(&input[in_idx]), __ldg(&kernel[k]), sum);");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        output[tid] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "dilated_convolution_1d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "dilation", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateDirectConvolution2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        var tileSize = context.WorkGroupDimensions?[0] ?? 16;

        source.AppendLine($"#define TILE_SIZE {tileSize}");
        source.AppendLine("__global__ void direct_convolution_2d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_width,");
        source.AppendLine("    const int input_height,");
        source.AppendLine("    const int kernel_width,");
        source.AppendLine("    const int kernel_height,");
        source.AppendLine("    const int stride_x,");
        source.AppendLine("    const int stride_y,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int out_x = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int out_y = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine("    const int output_width = (input_width - kernel_width) / stride_x + 1;");
        source.AppendLine("    const int output_height = (input_height - kernel_height) / stride_y + 1;");
        source.AppendLine("    if (out_x < output_width && out_y < output_height) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("            #pragma unroll");
        source.AppendLine("            for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                const int in_x = out_x * stride_x + kx;");
        source.AppendLine("                const int in_y = out_y * stride_y + ky;");
        source.AppendLine("                if (in_x < input_width && in_y < input_height) {");
        source.AppendLine("                    const float input_val = __ldg(&input[in_y * input_width + in_x]);");
        source.AppendLine("                    const float kernel_val = __ldg(&kernel[ky * kernel_width + kx]);");
        source.AppendLine("                    sum = fma(input_val, kernel_val, sum);");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        output[out_y * output_width + out_x] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "direct_convolution_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            },
            RequiredWorkGroupSize = new[] { tileSize, tileSize }
        };
    }

    private static GeneratedKernel GenerateWinogradConvolution2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        // Simplified Winograd for 3x3 kernels
        var source = new StringBuilder();

        source.AppendLine("__global__ void winograd_convolution_2d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_width,");
        source.AppendLine("    const int input_height,");
        source.AppendLine("    const int kernel_width,");
        source.AppendLine("    const int kernel_height,");
        source.AppendLine("    const int stride_x,");
        source.AppendLine("    const int stride_y,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    // For now, fallback to direct convolution");
        source.AppendLine("    // Full Winograd implementation would require complex transformations");
        source.AppendLine("    const int out_x = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int out_y = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine("    const int output_width = (input_width - kernel_width) / stride_x + 1;");
        source.AppendLine("    const int output_height = (input_height - kernel_height) / stride_y + 1;");
        source.AppendLine("    if (out_x < output_width && out_y < output_height) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("            #pragma unroll");
        source.AppendLine("            for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                const int in_x = out_x * stride_x + kx;");
        source.AppendLine("                const int in_y = out_y * stride_y + ky;");
        source.AppendLine("                if (in_x < input_width && in_y < input_height) {");
        source.AppendLine("                    sum = fma(__ldg(&input[in_y * input_width + in_x]), __ldg(&kernel[ky * kernel_width + kx]), sum);");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        output[out_y * output_width + out_x] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "winograd_convolution_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateIm2ColConvolution2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();

        source.AppendLine("__global__ void im2col_convolution_2d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_width,");
        source.AppendLine("    const int input_height,");
        source.AppendLine("    const int kernel_width,");
        source.AppendLine("    const int kernel_height,");
        source.AppendLine("    const int stride_x,");
        source.AppendLine("    const int stride_y,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int tid = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int output_width = (input_width - kernel_width) / stride_x + 1;");
        source.AppendLine("    const int output_height = (input_height - kernel_height) / stride_y + 1;");
        source.AppendLine("    const int output_size = output_width * output_height;");
        source.AppendLine("    if (tid < output_size) {");
        source.AppendLine("        const int out_y = tid / output_width;");
        source.AppendLine("        const int out_x = tid % output_width;");
        source.AppendLine("        const int input_start_x = out_x * stride_x;");
        source.AppendLine("        const int input_start_y = out_y * stride_y;");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("            #pragma unroll");
        source.AppendLine("            for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                const int in_x = input_start_x + kx;");
        source.AppendLine("                const int in_y = input_start_y + ky;");
        source.AppendLine("                if (in_x < input_width && in_y < input_height) {");
        source.AppendLine("                    sum = fma(__ldg(&input[in_y * input_width + in_x]), __ldg(&kernel[ky * kernel_width + kx]), sum);");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        output[tid] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "im2col_convolution_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateFFTConvolution2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        // For now, use the existing 2D convolution as a fallback
        // In a full implementation, this would use cuFFT
        return GenerateDirectConvolution2D(inputTypes, outputType, context);
    }

    private static GeneratedKernel GenerateDepthwiseConvolution2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();

        source.AppendLine("__global__ void depthwise_convolution_2d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernels,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int channels,");
        source.AppendLine("    const int input_width,");
        source.AppendLine("    const int input_height,");
        source.AppendLine("    const int kernel_width,");
        source.AppendLine("    const int kernel_height,");
        source.AppendLine("    const int stride_x,");
        source.AppendLine("    const int stride_y,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int out_x = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int out_y = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine("    const int channel = blockIdx.z;");
        source.AppendLine("    const int output_width = (input_width - kernel_width) / stride_x + 1;");
        source.AppendLine("    const int output_height = (input_height - kernel_height) / stride_y + 1;");
        source.AppendLine("    if (out_x < output_width && out_y < output_height && channel < channels) {");
        source.AppendLine("        const int input_channel_offset = channel * input_height * input_width;");
        source.AppendLine("        const int kernel_channel_offset = channel * kernel_height * kernel_width;");
        source.AppendLine("        const int output_channel_offset = channel * output_height * output_width;");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("            #pragma unroll");
        source.AppendLine("            for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                const int in_x = out_x * stride_x + kx;");
        source.AppendLine("                const int in_y = out_y * stride_y + ky;");
        source.AppendLine("                if (in_x < input_width && in_y < input_height) {");
        source.AppendLine("                    const int input_idx = input_channel_offset + in_y * input_width + in_x;");
        source.AppendLine("                    const int kernel_idx = kernel_channel_offset + ky * kernel_width + kx;");
        source.AppendLine("                    sum = fma(__ldg(&input[input_idx]), __ldg(&kernels[kernel_idx]), sum);");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        const int output_idx = output_channel_offset + out_y * output_width + out_x;");
        source.AppendLine("        output[output_idx] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "depthwise_convolution_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernels", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "channels", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateTransposedConvolution2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();

        source.AppendLine("__global__ void transposed_convolution_2d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_width,");
        source.AppendLine("    const int input_height,");
        source.AppendLine("    const int kernel_width,");
        source.AppendLine("    const int kernel_height,");
        source.AppendLine("    const int stride_x,");
        source.AppendLine("    const int stride_y,");
        source.AppendLine("    const int output_padding_x,");
        source.AppendLine("    const int output_padding_y,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int out_x = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int out_y = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine("    const int output_width = (input_width - 1) * stride_x + kernel_width + output_padding_x;");
        source.AppendLine("    const int output_height = (input_height - 1) * stride_y + kernel_height + output_padding_y;");
        source.AppendLine("    if (out_x < output_width && out_y < output_height) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("            for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                const int in_x_base = out_x - kx;");
        source.AppendLine("                const int in_y_base = out_y - ky;");
        source.AppendLine("                if (in_x_base >= 0 && in_x_base % stride_x == 0 &&");
        source.AppendLine("                    in_y_base >= 0 && in_y_base % stride_y == 0) {");
        source.AppendLine("                    const int in_x = in_x_base / stride_x;");
        source.AppendLine("                    const int in_y = in_y_base / stride_y;");
        source.AppendLine("                    if (in_x < input_width && in_y < input_height) {");
        source.AppendLine("                        const int input_idx = in_y * input_width + in_x;");
        source.AppendLine("                        const int kernel_idx = ky * kernel_width + kx;");
        source.AppendLine("                        sum = fma(__ldg(&input[input_idx]), __ldg(&kernel[kernel_idx]), sum);");
        source.AppendLine("                    }");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        const int output_idx = out_y * output_width + out_x;");
        source.AppendLine("        output[output_idx] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "transposed_convolution_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "output_padding_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "output_padding_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateConvolveRows2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        
        source.AppendLine("__global__ void convolve_rows_2d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int width,");
        source.AppendLine("    const int height,");
        source.AppendLine("    const int kernel_length,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int row = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine("    const int col = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine();
        source.AppendLine("    if (row < height && col < width) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        const int row_offset = row * width;");
        source.AppendLine();
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int k = 0; k < kernel_length; k++) {");
        source.AppendLine("            int src_col = col - kernel_length / 2 + k;");
        source.AppendLine("            if (src_col >= 0 && src_col < width) {");
        source.AppendLine("                sum = fma(__ldg(&input[row_offset + src_col]), __ldg(&kernel[k]), sum);");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        output[row_offset + col] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "convolve_rows_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateConvolveColumns2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        
        source.AppendLine("__global__ void convolve_columns_2d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int width,");
        source.AppendLine("    const int height,");
        source.AppendLine("    const int kernel_length,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int row = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine("    const int col = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine();
        source.AppendLine("    if (row < height && col < width) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine();
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int k = 0; k < kernel_length; k++) {");
        source.AppendLine("            int src_row = row - kernel_length / 2 + k;");
        source.AppendLine("            if (src_row >= 0 && src_row < height) {");
        source.AppendLine("                int src_idx = src_row * width + col;");
        source.AppendLine("                sum = fma(__ldg(&input[src_idx]), __ldg(&kernel[k]), sum);");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        output[row * width + col] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "convolve_columns_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_length", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            }
        };
    }

    private static GeneratedKernel GenerateBatchConvolution2D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        var tileSize = context.WorkGroupDimensions?[0] ?? 16;
        
        source.AppendLine("// Batch 2D Convolution Kernel");
        source.AppendLine($"#define TILE_SIZE {tileSize}");
        source.AppendLine();
        source.AppendLine("__global__ void batch_convolution_2d(");
        source.AppendLine("    const float* __restrict__ batch_input,");
        source.AppendLine("    const float* __restrict__ kernels,");
        source.AppendLine("    float* __restrict__ batch_output,");
        source.AppendLine("    const int batch_size,");
        source.AppendLine("    const int input_channels,");
        source.AppendLine("    const int output_channels,");
        source.AppendLine("    const int input_width,");
        source.AppendLine("    const int input_height,");
        source.AppendLine("    const int kernel_width,");
        source.AppendLine("    const int kernel_height,");
        source.AppendLine("    const int stride_x,");
        source.AppendLine("    const int stride_y,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int tx = threadIdx.x;");
        source.AppendLine("    const int ty = threadIdx.y;");
        source.AppendLine("    const int bx = blockIdx.x;");
        source.AppendLine("    const int by = blockIdx.y;");
        source.AppendLine("    const int batch_idx = blockIdx.z;");
        source.AppendLine();
        source.AppendLine("    const int output_width = (input_width - kernel_width) / stride_x + 1;");
        source.AppendLine("    const int output_height = (input_height - kernel_height) / stride_y + 1;");
        source.AppendLine();
        source.AppendLine("    const int out_x = bx * blockDim.x + tx;");
        source.AppendLine("    const int out_y = by * blockDim.y + ty;");
        source.AppendLine();
        source.AppendLine("    if (out_x < output_width && out_y < output_height && batch_idx < batch_size) {");
        source.AppendLine();
        source.AppendLine("        // Process each output channel");
        source.AppendLine("        for (int out_ch = 0; out_ch < output_channels; out_ch++) {");
        source.AppendLine("            float sum = 0.0f;");
        source.AppendLine();
        source.AppendLine("            // Sum over all input channels");
        source.AppendLine("            for (int in_ch = 0; in_ch < input_channels; in_ch++) {");
        source.AppendLine("                const int input_channel_offset = batch_idx * input_channels * input_height * input_width +");
        source.AppendLine("                                                 in_ch * input_height * input_width;");
        source.AppendLine("                const int kernel_offset = out_ch * input_channels * kernel_height * kernel_width +");
        source.AppendLine("                                         in_ch * kernel_height * kernel_width;");
        source.AppendLine();
        source.AppendLine("                // Convolution over kernel");
        source.AppendLine("                #pragma unroll");
        source.AppendLine("                for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("                    #pragma unroll");
        source.AppendLine("                    for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                        const int in_x = out_x * stride_x + kx;");
        source.AppendLine("                        const int in_y = out_y * stride_y + ky;");
        source.AppendLine();
        source.AppendLine("                        if (in_x < input_width && in_y < input_height) {");
        source.AppendLine("                            const int input_idx = input_channel_offset + in_y * input_width + in_x;");
        source.AppendLine("                            const int kernel_idx = kernel_offset + ky * kernel_width + kx;");
        source.AppendLine();
        source.AppendLine("                            const float input_val = __ldg(&batch_input[input_idx]);");
        source.AppendLine("                            const float kernel_val = __ldg(&kernels[kernel_idx]);");
        source.AppendLine("                            sum = fma(input_val, kernel_val, sum);");
        source.AppendLine("                        }");
        source.AppendLine("                    }");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine();
        source.AppendLine("            // Write result");
        source.AppendLine("            const int output_offset = batch_idx * output_channels * output_height * output_width +");
        source.AppendLine("                                     out_ch * output_height * output_width;");
        source.AppendLine("            const int output_idx = output_offset + out_y * output_width + out_x;");
        source.AppendLine("            batch_output[output_idx] = sum;");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "batch_convolution_2d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "batch_input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernels", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "batch_output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "batch_size", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_channels", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "output_channels", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            },
            RequiredWorkGroupSize = new[] { tileSize, tileSize },
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["TileSize"] = tileSize,
                ["SupportsBatching"] = true
            }
        };
    }

    private static GeneratedKernel GenerateConvolution3D(Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        var source = new StringBuilder();
        var tileSize = context.WorkGroupDimensions?[0] ?? 8;

        source.AppendLine($"#define TILE_SIZE {tileSize}");
        source.AppendLine("__global__ void convolution_3d(");
        source.AppendLine("    const float* __restrict__ input,");
        source.AppendLine("    const float* __restrict__ kernel,");
        source.AppendLine("    float* __restrict__ output,");
        source.AppendLine("    const int input_width,");
        source.AppendLine("    const int input_height,");
        source.AppendLine("    const int input_depth,");
        source.AppendLine("    const int kernel_width,");
        source.AppendLine("    const int kernel_height,");
        source.AppendLine("    const int kernel_depth,");
        source.AppendLine("    const int stride_x,");
        source.AppendLine("    const int stride_y,");
        source.AppendLine("    const int stride_z,");
        source.AppendLine("    const int padding_mode)");
        source.AppendLine("{");
        source.AppendLine("    const int out_x = blockIdx.x * blockDim.x + threadIdx.x;");
        source.AppendLine("    const int out_y = blockIdx.y * blockDim.y + threadIdx.y;");
        source.AppendLine("    const int out_z = blockIdx.z * blockDim.z + threadIdx.z;");
        source.AppendLine("    const int output_width = (input_width - kernel_width) / stride_x + 1;");
        source.AppendLine("    const int output_height = (input_height - kernel_height) / stride_y + 1;");
        source.AppendLine("    const int output_depth = (input_depth - kernel_depth) / stride_z + 1;");
        source.AppendLine("    if (out_x < output_width && out_y < output_height && out_z < output_depth) {");
        source.AppendLine("        float sum = 0.0f;");
        source.AppendLine("        #pragma unroll");
        source.AppendLine("        for (int kz = 0; kz < kernel_depth; kz++) {");
        source.AppendLine("            #pragma unroll");
        source.AppendLine("            for (int ky = 0; ky < kernel_height; ky++) {");
        source.AppendLine("                #pragma unroll");
        source.AppendLine("                for (int kx = 0; kx < kernel_width; kx++) {");
        source.AppendLine("                    const int in_x = out_x * stride_x + kx;");
        source.AppendLine("                    const int in_y = out_y * stride_y + ky;");
        source.AppendLine("                    const int in_z = out_z * stride_z + kz;");
        source.AppendLine("                    if (in_x < input_width && in_y < input_height && in_z < input_depth) {");
        source.AppendLine("                        const int input_idx = in_z * input_height * input_width + in_y * input_width + in_x;");
        source.AppendLine("                        const int kernel_idx = kz * kernel_height * kernel_width + ky * kernel_width + kx;");
        source.AppendLine("                        sum = fma(__ldg(&input[input_idx]), __ldg(&kernel[kernel_idx]), sum);");
        source.AppendLine("                    }");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        const int output_idx = out_z * output_height * output_width + out_y * output_width + out_x;");
        source.AppendLine("        output[output_idx] = sum;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return new GeneratedKernel
        {
            Name = "convolution_3d",
            Source = source.ToString(),
            Language = KernelLanguage.CUDA,
            Parameters = new[]
            {
                new KernelParameter { Name = "input", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "kernel", Type = typeof(float[]), IsInput = true, IsReadOnly = true },
                new KernelParameter { Name = "output", Type = typeof(float[]), IsOutput = true },
                new KernelParameter { Name = "input_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "input_depth", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_width", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_height", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "kernel_depth", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_x", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_y", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "stride_z", Type = typeof(int), IsInput = true },
                new KernelParameter { Name = "padding_mode", Type = typeof(int), IsInput = true }
            },
            RequiredWorkGroupSize = new[] { tileSize, tileSize, tileSize }
        };
    }

    #endregion
}

/// <summary>
/// Expression visitor for CUDA kernel generation.
/// </summary>
internal sealed class CUDAExpressionVisitor : ExpressionVisitor
{
    private readonly KernelGenerationContext _context;
    private readonly StringBuilder _kernelBody;
    private readonly List<KernelParameter> _parameters;
    private int _parameterIndex;

    public CUDAExpressionVisitor(KernelGenerationContext context)
    {
        _context = context;
        _kernelBody = new StringBuilder();
        _parameters = new List<KernelParameter>();
        KernelName = "generated_kernel";
        OptimizationMetadata = new Dictionary<string, object>();
    }

    public string KernelName { get; set; }
    public int SharedMemorySize { get; set; }
    public Dictionary<string, object> OptimizationMetadata { get; }

    public KernelParameter[] GetParameters()
    {
        return _parameters.ToArray();
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
            if (node.Type == typeof(float))
            {
                _kernelBody.Append("f");
            }
        }
        
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;
        
        if (method.DeclaringType == typeof(Math) || method.DeclaringType == typeof(MathF))
        {
            var funcName = method.Name.ToLowerInvariant();
            if (funcName == "log") funcName = "logf";
            else if (funcName == "log10") funcName = "log10f";
            else if (!funcName.EndsWith("f")) funcName += "f";
            
            _kernelBody.Append($"{funcName}(");
            
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                if (i > 0) _kernelBody.Append(", ");
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