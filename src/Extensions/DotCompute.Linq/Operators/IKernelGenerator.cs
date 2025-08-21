// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
namespace DotCompute.Linq.Operators;


/// <summary>
/// Interface for generating kernels from expressions and operation types.
/// </summary>
public interface IKernelGenerator
{
    /// <summary>
    /// Determines if an expression can be compiled to a kernel.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <returns>True if the expression can be compiled; otherwise, false.</returns>
    public bool CanCompile(Expression expression);

    /// <summary>
    /// Generates a kernel from an expression.
    /// </summary>
    /// <param name="expression">The expression to compile.</param>
    /// <param name="context">The generation context.</param>
    /// <returns>A generated kernel.</returns>
    public GeneratedKernel GenerateKernel(Expression expression, KernelGenerationContext context);

    /// <summary>
    /// Generates a kernel for a specific operation type.
    /// </summary>
    /// <param name="operationType">The type of operation (Map, Filter, Reduce, etc.).</param>
    /// <param name="inputTypes">The input parameter types.</param>
    /// <param name="outputType">The output type.</param>
    /// <param name="context">The generation context.</param>
    /// <returns>A generated kernel.</returns>
    public GeneratedKernel GenerateOperationKernel(string operationType, Type[] inputTypes, Type outputType, KernelGenerationContext context);
}

/// <summary>
/// CUDA kernel generator implementation.
/// </summary>
internal class CUDAKernelGenerator : IKernelGenerator
{
    public bool CanCompile(Expression expression)
        // Basic check for supported expressions

        => expression.NodeType is ExpressionType.Call or ExpressionType.Lambda;

    public GeneratedKernel GenerateKernel(Expression expression, KernelGenerationContext context)
    {
        return new GeneratedKernel
        {
            Name = $"cuda_kernel_{Guid.NewGuid():N}",
            Source = GenerateCudaSource(expression, context),
            Language = Core.Kernels.KernelLanguage.CUDA,
            Parameters = ExtractParameters(expression),
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["Generated"] = DateTime.UtcNow,
                ["ExpressionType"] = expression.NodeType.ToString()
            }
        };
    }

    public GeneratedKernel GenerateOperationKernel(string operationType, Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        return new GeneratedKernel
        {
            Name = $"cuda_{operationType.ToLowerInvariant()}_kernel_{Guid.NewGuid():N}",
            Source = GenerateCudaOperationSource(operationType, inputTypes, outputType, context),
            Language = Core.Kernels.KernelLanguage.CUDA,
            Parameters = GenerateParameters(inputTypes, outputType),
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["OperationType"] = operationType,
                ["Generated"] = DateTime.UtcNow
            }
        };
    }

    private static string GenerateCudaSource(Expression expression, KernelGenerationContext context) => """
           __global__ void generated_kernel(float* input, float* output, int size) {
               int idx = blockIdx.x * blockDim.x + threadIdx.x;
               if (idx < size) {
                   // Placeholder CUDA kernel implementation
                   output[idx] = input[idx] * 2.0f;
               }
           }
           """;

    private static string GenerateCudaOperationSource(string operationType, Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        return operationType switch
        {
            "Map" => """
                 __global__ void map_kernel(float* input, float* output, int size) {
                     int idx = blockIdx.x * blockDim.x + threadIdx.x;
                     if (idx < size) {
                         output[idx] = input[idx] * 2.0f; // Example map operation
                     }
                 }
                 """,
            "Filter" => """
                    __global__ void filter_kernel(float* input, int* output, int size) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < size) {
                            output[idx] = (input[idx] > 0.0f) ? 1 : 0; // Example filter
                        }
                    }
                    """,
            _ => """
             __global__ void generic_kernel(float* input, float* output, int size) {
                 int idx = blockIdx.x * blockDim.x + threadIdx.x;
                 if (idx < size) {
                     output[idx] = input[idx];
                 }
             }
             """
        };
    }

    private static GeneratedKernelParameter[] ExtractParameters(Expression expression)
    {
        return [
            new() { Name = "input", Type = typeof(float[]), IsInput = true },
        new() { Name = "output", Type = typeof(float[]), IsOutput = true },
        new() { Name = "size", Type = typeof(int), IsInput = true }
        ];
    }

    private static GeneratedKernelParameter[] GenerateParameters(Type[] inputTypes, Type outputType)
    {
        var parameters = new List<GeneratedKernelParameter>();

        for (var i = 0; i < inputTypes.Length; i++)
        {
            parameters.Add(new GeneratedKernelParameter
            {
                Name = $"input_{i}",
                Type = inputTypes[i],
                IsInput = true
            });
        }

        parameters.Add(new GeneratedKernelParameter
        {
            Name = "output",
            Type = outputType,
            IsOutput = true
        });

        parameters.Add(new GeneratedKernelParameter
        {
            Name = "size",
            Type = typeof(int),
            IsInput = true
        });

        return [.. parameters];
    }
}

/// <summary>
/// OpenCL kernel generator implementation.
/// </summary>
internal class OpenCLKernelGenerator : IKernelGenerator
{
    public bool CanCompile(Expression expression) => expression.NodeType is ExpressionType.Call or ExpressionType.Lambda;

    public GeneratedKernel GenerateKernel(Expression expression, KernelGenerationContext context)
    {
        return new GeneratedKernel
        {
            Name = $"opencl_kernel_{Guid.NewGuid():N}",
            Source = GenerateOpenCLSource(expression, context),
            Language = Core.Kernels.KernelLanguage.OpenCL,
            Parameters = ExtractParameters(expression),
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["Generated"] = DateTime.UtcNow,
                ["ExpressionType"] = expression.NodeType.ToString()
            }
        };
    }

    public GeneratedKernel GenerateOperationKernel(string operationType, Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        return new GeneratedKernel
        {
            Name = $"opencl_{operationType.ToLowerInvariant()}_kernel_{Guid.NewGuid():N}",
            Source = GenerateOpenCLOperationSource(operationType, inputTypes, outputType, context),
            Language = Core.Kernels.KernelLanguage.OpenCL,
            Parameters = GenerateParameters(inputTypes, outputType),
            OptimizationMetadata = new Dictionary<string, object>
            {
                ["OperationType"] = operationType,
                ["Generated"] = DateTime.UtcNow
            }
        };
    }

    private static string GenerateOpenCLSource(Expression expression, KernelGenerationContext context) => """
           __kernel void generated_kernel(__global float* input, __global float* output, int size) {
               int idx = get_global_id(0);
               if (idx < size) {
                   // Placeholder OpenCL kernel implementation
                   output[idx] = input[idx] * 2.0f;
               }
           }
           """;

    private static string GenerateOpenCLOperationSource(string operationType, Type[] inputTypes, Type outputType, KernelGenerationContext context)
    {
        return operationType switch
        {
            "Map" => """
                 __kernel void map_kernel(__global float* input, __global float* output, int size) {
                     int idx = get_global_id(0);
                     if (idx < size) {
                         output[idx] = input[idx] * 2.0f; // Example map operation
                     }
                 }
                 """,
            "Filter" => """
                    __kernel void filter_kernel(__global float* input, __global int* output, int size) {
                        int idx = get_global_id(0);
                        if (idx < size) {
                            output[idx] = (input[idx] > 0.0f) ? 1 : 0; // Example filter
                        }
                    }
                    """,
            _ => """
             __kernel void generic_kernel(__global float* input, __global float* output, int size) {
                 int idx = get_global_id(0);
                 if (idx < size) {
                     output[idx] = input[idx];
                 }
             }
             """
        };
    }

    private static GeneratedKernelParameter[] ExtractParameters(Expression expression)
    {
        return [
            new() { Name = "input", Type = typeof(float[]), IsInput = true },
        new() { Name = "output", Type = typeof(float[]), IsOutput = true },
        new() { Name = "size", Type = typeof(int), IsInput = true }
        ];
    }

    private static GeneratedKernelParameter[] GenerateParameters(Type[] inputTypes, Type outputType)
    {
        var parameters = new List<GeneratedKernelParameter>();

        for (var i = 0; i < inputTypes.Length; i++)
        {
            parameters.Add(new GeneratedKernelParameter
            {
                Name = $"input_{i}",
                Type = inputTypes[i],
                IsInput = true
            });
        }

        parameters.Add(new GeneratedKernelParameter
        {
            Name = "output",
            Type = outputType,
            IsOutput = true
        });

        parameters.Add(new GeneratedKernelParameter
        {
            Name = "size",
            Type = typeof(int),
            IsInput = true
        });

        return [.. parameters];
    }
}
