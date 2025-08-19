// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Backends.Metal.Kernels;


/// <summary>
/// Provides optimized kernel implementations for Metal.
/// </summary>
public static class MetalOptimizedKernels
{
/// <summary>
/// Creates a vector addition kernel optimized for Metal.
/// </summary>
public static KernelDefinition CreateVectorAddKernel()
{
    var code = @"
kernel void vectorAdd(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    constant uint& size [[buffer(3)]],
    uint gid [[thread_position_in_grid]]
) {
    if (gid < size) {
        result[gid] = a[gid] + b[gid];
    }
}";

    var kernelSource = new TextKernelSource(
        code: code,
        name: "vectorAdd",
        language: KernelLanguage.Metal,
        entryPoint: "vectorAdd",
        dependencies: []
    );

    var compilationOptions = new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Default,
        EnableDebugInfo = false
    };

    var definition = new KernelDefinition("vectorAdd", kernelSource, compilationOptions);

    if (definition.Metadata != null)
    {
        definition.Metadata["paramCount"] = 4;
        definition.Metadata["operation"] = "VectorAdd";
    }

    return definition;
}

/// <summary>
/// Creates a matrix multiplication kernel optimized for Metal.
/// </summary>
public static KernelDefinition CreateMatrixMultiplyKernel()
{
    var code = @"
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

kernel void matrixMultiply(
    device const float* matrixA [[buffer(0)]],
    device const float* matrixB [[buffer(1)]],
    device float* matrixC [[buffer(2)]],
    constant uint& M [[buffer(3)]],
    constant uint& N [[buffer(4)]],
    constant uint& K [[buffer(5)]],
    uint2 gid [[thread_position_in_grid]]
) {
    uint row = gid.y;
    uint col = gid.x;
    
    if (row >= M || col >= N) {
        return;
    }
    
    float sum = 0.0f;
    for (uint k = 0; k < K; ++k) {
        sum += matrixA[row * K + k] * matrixB[k * N + col];
    }
    
    matrixC[row * N + col] = sum;
}";

    var kernelSource = new TextKernelSource(
        code: code,
        name: "matrixMultiply",
        language: KernelLanguage.Metal,
        entryPoint: "matrixMultiply",
        dependencies: []
    );

    var compilationOptions = new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Default,
        EnableDebugInfo = false
    };

    var definition = new KernelDefinition("matrixMultiply", kernelSource, compilationOptions);

    if (definition.Metadata != null)
    {
        definition.Metadata["paramCount"] = 6;
        definition.Metadata["operation"] = "MatrixMultiply";
        definition.Metadata["requiresTiling"] = true;
    }

    return definition;
}

/// <summary>
/// Creates a parallel reduction kernel optimized for Metal.
/// </summary>
public static KernelDefinition CreateReductionKernel(ReductionOperation operation = ReductionOperation.Sum)
{
    var operationCode = operation switch
    {
        ReductionOperation.Sum => "sum += shared[tid + s];",
        ReductionOperation.Product => "sum *= shared[tid + s];",
        ReductionOperation.Min => "sum = min(sum, shared[tid + s]);",
        ReductionOperation.Max => "sum = max(sum, shared[tid + s]);",
        _ => "sum += shared[tid + s];"
    };

    var code = $@"
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

kernel void reduction{operation}(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    constant uint& size [[buffer(2)]],
    threadgroup float* shared [[threadgroup(0)]],
    uint tid [[thread_position_in_threadgroup]],
    uint gid [[thread_position_in_grid]],
    uint tgid [[threadgroup_position_in_grid]],
    uint tg_size [[threads_per_threadgroup]]
) {{
    // Load data into shared memory
    shared[tid] = (gid < size) ? input[gid] : 0;
    threadgroup_barrier(mem_flags::mem_threadgroup);
    
    // Perform reduction
    float sum = shared[tid];
    for (uint s = tg_size / 2; s > 0; s >>= 1) {{
        if (tid < s) {{
            {operationCode}
        }}
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }}
    
    // Write result
    if (tid == 0) {{
        output[tgid] = sum;
    }}
}}";

    var kernelName = $"reduction{operation}";
    var kernelSource = new TextKernelSource(
        code: code,
        name: kernelName,
        language: KernelLanguage.Metal,
        entryPoint: kernelName,
        dependencies: []
    );

    var compilationOptions = new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Default,
        EnableDebugInfo = false
    };

    var definition = new KernelDefinition(kernelName, kernelSource, compilationOptions);

    if (definition.Metadata != null)
    {
        definition.Metadata["operation"] = operation.ToString();
        definition.Metadata["requiresSharedMemory"] = true;
    }

    return definition;
}

/// <summary>
/// Creates a convolution kernel optimized for Metal (useful for neural networks).
/// </summary>
public static KernelDefinition CreateConvolution2DKernel()
{
    var code = @"
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

struct ConvParams {
    uint inputWidth;
    uint inputHeight;
    uint kernelWidth;
    uint kernelHeight;
    uint outputWidth;
    uint outputHeight;
    uint strideX;
    uint strideY;
    uint padX;
    uint padY;
    float bias;
};

kernel void convolution2D(
    device const float* input [[buffer(0)]],
    device const float* kernel [[buffer(1)]],
    device float* output [[buffer(2)]],
    constant ConvParams& params [[buffer(3)]],
    uint2 gid [[thread_position_in_grid]]
) {
    if (gid.x >= params.outputWidth || gid.y >= params.outputHeight) {
        return;
    }
    
    float sum = 0.0f;
    
    for (uint ky = 0; ky < params.kernelHeight; ++ky) {
        for (uint kx = 0; kx < params.kernelWidth; ++kx) {
            int inputY = gid.y * params.strideY + ky - params.padY;
            int inputX = gid.x * params.strideX + kx - params.padX;
            
            if (inputY >= 0 && inputY < params.inputHeight &&
                inputX >= 0 && inputX < params.inputWidth) {
                
                uint inputIdx = inputY * params.inputWidth + inputX;
                uint kernelIdx = ky * params.kernelWidth + kx;
                
                sum += input[inputIdx] * kernel[kernelIdx];
            }
        }
    }
    
    output[gid.y * params.outputWidth + gid.x] = sum + params.bias;
}";

    var kernelSource = new TextKernelSource(
        code: code,
        name: "convolution2D",
        language: KernelLanguage.Metal,
        entryPoint: "convolution2D",
        dependencies: []
    );

    var compilationOptions = new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Default,
        EnableDebugInfo = false
    };

    var definition = new KernelDefinition("convolution2D", kernelSource, compilationOptions);

    if (definition.Metadata != null)
    {
        definition.Metadata["operation"] = "Convolution2D";
        definition.Metadata["requiresStructParam"] = true;
    }

    return definition;
}

/// <summary>
/// Creates a neural network activation kernel.
/// </summary>
public static KernelDefinition CreateActivationKernel(ActivationType activation)
{
    var activationCode = activation switch
    {
        ActivationType.ReLU => "output[gid] = fmax(value, 0.0f);",
        ActivationType.Sigmoid => "output[gid] = 1.0f / (1.0f + exp(-value));",
        ActivationType.Tanh => "output[gid] = tanh(value);",
        ActivationType.LeakyReLU => "output[gid] = value > 0 ? value : alpha * value;",
        _ => throw new NotSupportedException($"Activation {activation} not supported")
    };

    var code = $@"
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

kernel void activation{activation}(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    constant float& alpha [[buffer(2)]],
    constant uint& size [[buffer(3)]],
    uint gid [[thread_position_in_grid]]
) {{
    if (gid >= size) return;
    
    float value = input[gid];
    {activationCode}
}}";

    var kernelName = $"activation{activation}";
    var kernelSource = new TextKernelSource(
        code: code,
        name: kernelName,
        language: KernelLanguage.Metal,
        entryPoint: kernelName,
        dependencies: []
    );

    var compilationOptions = new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Default,
        EnableDebugInfo = false
    };

    var definition = new KernelDefinition(kernelName, kernelSource, compilationOptions);

    if (definition.Metadata != null)
    {
        definition.Metadata["activation"] = activation.ToString();
    }

    return definition;
}

/// <summary>
/// Creates a simple element-wise operation kernel.
/// </summary>
public static KernelDefinition CreateElementWiseKernel(string operation, string operationCode)
{
    var code = $@"
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

kernel void {operation}(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    constant uint& size [[buffer(3)]],
    uint gid [[thread_position_in_grid]]
) {{
    if (gid < size) {{
        result[gid] = {operationCode};
    }}
}}";

    var kernelSource = new TextKernelSource(
        code: code,
        name: operation,
        language: KernelLanguage.Metal,
        entryPoint: operation,
        dependencies: []
    );

    var compilationOptions = new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Default,
        EnableDebugInfo = false
    };

    var definition = new KernelDefinition(operation, kernelSource, compilationOptions);

    if (definition.Metadata != null)
    {
        definition.Metadata["operation"] = operation;
    }

    return definition;
}
}

/// <summary>
/// Reduction operation types.
/// </summary>
public enum ReductionOperation
{
Sum,
Product,
Min,
Max,
Mean
}

/// <summary>
/// Neural network activation types.
/// </summary>
public enum ActivationType
{
ReLU,
Sigmoid,
Tanh,
LeakyReLU
}
