// <copyright file="CudaKernelTemplate.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.KernelGeneration.Templates;

/// <summary>
/// CUDA kernel template implementation that generates optimized CUDA C++ kernel source code.
/// Supports various CUDA-specific optimizations including shared memory, texture memory, and compute capability targeting.
/// </summary>
public sealed class CudaKernelTemplate : IKernelTemplate
{
    private readonly ILogger<CudaKernelTemplate> _logger;
    private static readonly HashSet<string> s_supportedOperations = new()
    {
        "map", "reduce", "filter", "scan", "sort", "matmul", "conv2d", "elementwise",
        "transpose", "gemv", "gemm", "fft", "reduction", "broadcast", "gather", "scatter"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaKernelTemplate"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public CudaKernelTemplate(ILogger<CudaKernelTemplate> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public BackendType TargetBackend => BackendType.CUDA;

    /// <inheritdoc/>
    public KernelLanguage Language => KernelLanguage.CUDA;

    /// <inheritdoc/>
    public string Name => "CudaKernelTemplate";

    /// <inheritdoc/>
    public string Version => "1.0.0";

    /// <inheritdoc/>
    public IReadOnlySet<string> SupportedOperations => s_supportedOperations;

    /// <inheritdoc/>
    public async Task<KernelGenerationResult> GenerateAsync(
        KernelMetadata metadata,
        KernelEntryPoint entryPoint,
        KernelGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating CUDA kernel: {KernelName}", metadata.Name);

        var validation = Validate(metadata, entryPoint);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid kernel configuration: {string.Join(", ", validation.Errors)}");
        }

        var sourceBuilder = new StringBuilder();
        var compilationFlags = new List<string>();
        var headers = new List<string> { "cuda_runtime.h", "device_launch_parameters.h" };

        // Add standard headers
        sourceBuilder.AppendLine("#include <cuda_runtime.h>");
        sourceBuilder.AppendLine("#include <device_launch_parameters.h>");
        sourceBuilder.AppendLine("#include <cstdint>");
        sourceBuilder.AppendLine();

        // Add CUDA-specific optimizations based on metadata
        AddCompileTimeOptimizations(sourceBuilder, metadata, options);

        // Generate kernel function signature
        GenerateKernelSignature(sourceBuilder, entryPoint, metadata);

        // Generate kernel body based on operation type
        await GenerateKernelBodyAsync(sourceBuilder, metadata, entryPoint, cancellationToken);

        // Add compilation flags based on compute capability and optimization level
        AddCompilationFlags(compilationFlags, metadata, options);

        var result = new KernelGenerationResult(sourceBuilder.ToString(), entryPoint.FunctionName)
        {
            EstimatedResourceUsage = EstimateResourceUsage(metadata, entryPoint, 1024 * 1024) // Default 1MB estimation
        };

        result.Headers.AddRange(headers);
        result.CompilationFlags.AddRange(compilationFlags);

        if (validation.Warnings.Count > 0)
        {
            result.Warnings = new List<string>(validation.Warnings);
        }

        _logger.LogDebug("Generated CUDA kernel {KernelName} with {Lines} lines of code",
            metadata.Name, sourceBuilder.ToString().Split('\n').Length);

        return result;
    }

    /// <inheritdoc/>
    public KernelTemplateValidationResult Validate(KernelMetadata metadata, KernelEntryPoint entryPoint)
    {
        var result = new KernelTemplateValidationResult(true);

        // Validate CUDA-specific requirements
        if (metadata.Language != KernelLanguage.CUDA && metadata.Language != KernelLanguage.Auto)
        {
            result.AddError($"Language {metadata.Language} is not supported by CUDA template");
        }

        // Validate compute capability
        if (!string.IsNullOrEmpty(metadata.MinComputeCapability))
        {
            if (!IsValidComputeCapability(metadata.MinComputeCapability))
            {
                result.AddError($"Invalid compute capability: {metadata.MinComputeCapability}");
            }
        }

        // Validate register usage
        if (metadata.MaxRegistersPerThread.HasValue && metadata.MaxRegistersPerThread > 255)
        {
            result.AddError("Maximum registers per thread cannot exceed 255 for CUDA kernels");
        }

        // Validate shared memory usage
        if (metadata.SharedMemorySize > 49152) // 48KB for most GPUs
        {
            result.AddWarning($"Shared memory usage {metadata.SharedMemorySize} bytes may exceed hardware limits");
        }

        // Validate parameter types
        foreach (var param in entryPoint.Parameters)
        {
            if (!IsSupportedCudaType(param.Type))
            {
                result.AddError($"Parameter type {param.Type.Name} is not supported in CUDA kernels");
            }
        }

        // Validate work group size
        if (metadata.WorkGroupSize != null)
        {
            var totalThreads = metadata.WorkGroupSize.Aggregate(1, (a, b) => a * b);
            if (totalThreads > 1024)
            {
                result.AddError("Total work group size cannot exceed 1024 threads for CUDA kernels");
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public ResourceUsageEstimate EstimateResourceUsage(
        KernelMetadata metadata,
        KernelEntryPoint entryPoint,
        long dataSize)
    {
        var estimate = new ResourceUsageEstimate
        {
            InputDataSize = dataSize,
            HardwareProfile = "CUDA GPU"
        };

        // Estimate memory usage
        estimate.GlobalMemoryBytes = EstimateGlobalMemoryUsage(entryPoint, dataSize);
        estimate.SharedMemoryBytes = metadata.SharedMemorySize;
        estimate.ConstantMemoryBytes = metadata.ConstantMemorySize;
        estimate.RegistersPerThread = metadata.MaxRegistersPerThread ?? EstimateRegisterUsage(metadata);

        estimate.TotalMemoryBytes = estimate.GlobalMemoryBytes + estimate.SharedMemoryBytes + estimate.ConstantMemoryBytes;

        // Estimate compute requirements
        estimate.EstimatedFlops = EstimateFloatingPointOperations(metadata, dataSize);
        estimate.EstimatedIntOps = EstimateIntegerOperations(metadata, dataSize);

        // Estimate execution time (simplified model)
        var computeIntensity = estimate.EstimatedFlops / Math.Max(estimate.GlobalMemoryBytes, 1.0);
        estimate.ComputeIntensity = computeIntensity;

        // Memory bandwidth estimation (assuming PCIe 4.0 x16)
        estimate.MemoryBandwidthGBps = Math.Min(estimate.GlobalMemoryBytes / 1e9, 64.0); // Max ~64 GB/s

        // Execution time estimation (very rough)
        var memoryTime = estimate.GlobalMemoryBytes / (estimate.MemoryBandwidthGBps * 1e9);
        var computeTime = estimate.EstimatedFlops / 10e12; // Assume 10 TFLOPS peak
        estimate.EstimatedExecutionTimeMs = Math.Max(memoryTime, computeTime) * 1000;

        // Occupancy estimation
        estimate.ExpectedOccupancy = EstimateOccupancy(metadata);

        // Power estimation (rough approximation)
        estimate.EstimatedPowerWatts = 150.0 + (estimate.ExpectedOccupancy / 100.0 * 100.0); // Base + utilization

        estimate.ConfidenceLevel = 0.7; // Medium confidence for estimates

        return estimate;
    }

    /// <inheritdoc/>
    public IEnumerable<KernelOptimizationSuggestion> GetOptimizationSuggestions(
        KernelMetadata metadata,
        KernelEntryPoint entryPoint)
    {
        var suggestions = new List<KernelOptimizationSuggestion>();

        // Work group size optimization
        if (metadata.WorkGroupSize == null || metadata.WorkGroupSize.Length == 0)
        {
            suggestions.Add(new KernelOptimizationSuggestion(
                OptimizationType.WorkGroupSize,
                "Consider specifying optimal work group size based on your hardware (typically 256 or 512 for modern GPUs)",
                PerformanceImpact.Medium));
        }

        // Shared memory optimization
        if (metadata.SharedMemorySize == 0 && HasPotentialForSharedMemory(entryPoint))
        {
            suggestions.Add(new KernelOptimizationSuggestion(
                OptimizationType.SharedMemory,
                "Consider using shared memory for frequently accessed data to reduce global memory bandwidth",
                PerformanceImpact.High));
        }

        // Register optimization
        if (metadata.MaxRegistersPerThread == null || metadata.MaxRegistersPerThread > 32)
        {
            suggestions.Add(new KernelOptimizationSuggestion(
                OptimizationType.RegisterUsage,
                "Consider limiting register usage to 32 per thread for better occupancy",
                PerformanceImpact.Medium));
        }

        // Memory access pattern optimization
        if (HasNonCoalescedMemoryAccess(entryPoint))
        {
            suggestions.Add(new KernelOptimizationSuggestion(
                OptimizationType.MemoryAccess,
                "Optimize memory access patterns for coalescing to improve bandwidth utilization",
                PerformanceImpact.High));
        }

        // Vectorization optimization
        if (CanBenefitFromVectorization(entryPoint))
        {
            suggestions.Add(new KernelOptimizationSuggestion(
                OptimizationType.Vectorization,
                "Use vector types (float2, float4) for better memory throughput",
                PerformanceImpact.Medium));
        }

        return suggestions;
    }

    /// <inheritdoc/>
    public bool SupportsOperation(string operationType)
    {
        return s_supportedOperations.Contains(operationType?.ToLowerInvariant() ?? string.Empty);
    }

    private static void AddCompileTimeOptimizations(StringBuilder sourceBuilder, KernelMetadata metadata, KernelGenerationOptions? options)
    {
        // Add preprocessor definitions for optimization
        if (metadata.UseFastMath)
        {
            sourceBuilder.AppendLine("#define __FAST_MATH__");
        }

        if (options?.MaxRegistersPerThread.HasValue == true)
        {
            sourceBuilder.AppendLine($"#define MAX_REGISTERS {options.MaxRegistersPerThread}");
        }

        if (!string.IsNullOrEmpty(metadata.MinComputeCapability))
        {
            var cc = metadata.MinComputeCapability.Replace(".", "");
            sourceBuilder.AppendLine($"#if __CUDA_ARCH__ >= {cc}");
            sourceBuilder.AppendLine("#define ARCH_OPTIMIZED");
            sourceBuilder.AppendLine("#endif");
        }

        sourceBuilder.AppendLine();
    }

    private static void GenerateKernelSignature(StringBuilder sourceBuilder, KernelEntryPoint entryPoint, KernelMetadata metadata)
    {
        sourceBuilder.Append("__global__ ");

        // Add launch bounds if specified
        if (metadata.WorkGroupSize != null && metadata.WorkGroupSize.Length > 0)
        {
            var blockSize = metadata.WorkGroupSize.Aggregate(1, (a, b) => a * b);
            sourceBuilder.Append($"__launch_bounds__({blockSize}) ");
        }

        sourceBuilder.Append($"void {entryPoint.FunctionName}(");

        var paramStrings = new List<string>();
        foreach (var param in entryPoint.Parameters)
        {
            var paramString = GenerateCudaParameterString(param);
            paramStrings.Add(paramString);
        }

        sourceBuilder.Append(string.Join(", ", paramStrings));
        sourceBuilder.AppendLine(")");
        sourceBuilder.AppendLine("{");
    }

    private static async Task GenerateKernelBodyAsync(
        StringBuilder sourceBuilder,
        KernelMetadata metadata,
        KernelEntryPoint entryPoint,
        CancellationToken cancellationToken)
    {
        // Generate thread indexing
        sourceBuilder.AppendLine("    // Thread indexing");
        sourceBuilder.AppendLine("    const int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        sourceBuilder.AppendLine("    const int stride = blockDim.x * gridDim.x;");
        sourceBuilder.AppendLine();

        // Add bounds checking
        var sizeParam = entryPoint.Parameters.FirstOrDefault(p => p.Name.Contains("size") || p.Name.Contains("length"));
        if (sizeParam != null)
        {
            sourceBuilder.AppendLine($"    if (idx >= {sizeParam.Name}) return;");
            sourceBuilder.AppendLine();
        }

        // Generate operation-specific code based on hints
        var operationType = metadata.GetCompilationHint<string>("operation_type") ?? "elementwise";
        
        await Task.Run(() =>
        {
            switch (operationType.ToLowerInvariant())
            {
                case "map":
                case "elementwise":
                    GenerateElementwiseOperation(sourceBuilder, entryPoint);
                    break;
                case "reduce":
                case "reduction":
                    GenerateReductionOperation(sourceBuilder, entryPoint, metadata);
                    break;
                case "matmul":
                case "gemm":
                    GenerateMatrixMultiplyOperation(sourceBuilder, entryPoint, metadata);
                    break;
                default:
                    GenerateGenericOperation(sourceBuilder, entryPoint);
                    break;
            }
        }, cancellationToken);

        sourceBuilder.AppendLine("}");
    }

    private static void GenerateElementwiseOperation(StringBuilder sourceBuilder, KernelEntryPoint entryPoint)
    {
        sourceBuilder.AppendLine("    // Elementwise operation");
        var inputParams = entryPoint.GetInputParameters().ToArray();
        var outputParams = entryPoint.GetOutputParameters().ToArray();

        if (inputParams.Length >= 1 && outputParams.Length >= 1)
        {
            var input = inputParams[0];
            var output = outputParams[0];
            
            sourceBuilder.AppendLine($"    for (int i = idx; i < size; i += stride) {{");
            sourceBuilder.AppendLine($"        {output.Name}[i] = {input.Name}[i]; // TODO: Add actual operation");
            sourceBuilder.AppendLine("    }");
        }
    }

    private static void GenerateReductionOperation(StringBuilder sourceBuilder, KernelEntryPoint entryPoint, KernelMetadata metadata)
    {
        sourceBuilder.AppendLine("    // Reduction operation with shared memory");
        sourceBuilder.AppendLine($"    __shared__ float sdata[{Math.Max(metadata.SharedMemorySize / sizeof(float), 256)}];");
        sourceBuilder.AppendLine();
        
        var inputParam = entryPoint.GetInputParameters().FirstOrDefault();
        if (inputParam != null)
        {
            sourceBuilder.AppendLine("    // Load data into shared memory");
            sourceBuilder.AppendLine($"    sdata[threadIdx.x] = (idx < size) ? {inputParam.Name}[idx] : 0.0f;");
            sourceBuilder.AppendLine("    __syncthreads();");
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("    // Perform reduction in shared memory");
            sourceBuilder.AppendLine("    for (int s = blockDim.x / 2; s > 0; s >>= 1) {");
            sourceBuilder.AppendLine("        if (threadIdx.x < s) {");
            sourceBuilder.AppendLine("            sdata[threadIdx.x] += sdata[threadIdx.x + s];");
            sourceBuilder.AppendLine("        }");
            sourceBuilder.AppendLine("        __syncthreads();");
            sourceBuilder.AppendLine("    }");
        }
    }

    private static void GenerateMatrixMultiplyOperation(StringBuilder sourceBuilder, KernelEntryPoint entryPoint, KernelMetadata metadata)
    {
        sourceBuilder.AppendLine("    // Matrix multiplication with shared memory");
        sourceBuilder.AppendLine("    const int row = blockIdx.y * blockDim.y + threadIdx.y;");
        sourceBuilder.AppendLine("    const int col = blockIdx.x * blockDim.x + threadIdx.x;");
        sourceBuilder.AppendLine();
        
        if (metadata.SharedMemorySize > 0)
        {
            sourceBuilder.AppendLine("    __shared__ float As[16][16];");
            sourceBuilder.AppendLine("    __shared__ float Bs[16][16];");
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("    // Implement tiled matrix multiplication");
            sourceBuilder.AppendLine("    // TODO: Add complete matrix multiplication implementation");
        }
    }

    private static void GenerateGenericOperation(StringBuilder sourceBuilder, KernelEntryPoint entryPoint)
    {
        sourceBuilder.AppendLine("    // Generic kernel operation");
        sourceBuilder.AppendLine("    // TODO: Implement specific operation logic");
    }

    private static string GenerateCudaParameterString(KernelParameter param)
    {
        var typeString = GetCudaTypeString(param.Type);
        var qualifier = param.Direction switch
        {
            ParameterDirection.In => "const ",
            ParameterDirection.Out => "",
            ParameterDirection.InOut => "",
            _ => ""
        };

        if (param.Type.IsArray || param.Type.IsPointer)
        {
            return $"{qualifier}{typeString}* {param.Name}";
        }

        return $"{qualifier}{typeString} {param.Name}";
    }

    private static string GetCudaTypeString(Type type)
    {
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long long";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "unsigned char";
        if (type == typeof(short)) return "short";
        if (type.IsArray) return GetCudaTypeString(type.GetElementType()!);
        
        return type.Name.ToLowerInvariant();
    }

    private static void AddCompilationFlags(List<string> flags, KernelMetadata metadata, KernelGenerationOptions? options)
    {
        // Add optimization level
        flags.Add($"-O{(int)metadata.OptimizationLevel}");

        // Add compute capability
        var cc = options?.TargetComputeCapability ?? metadata.MinComputeCapability ?? "6.0";
        flags.Add($"-arch=sm_{cc.Replace(".", "")}");

        // Add register limit
        if (metadata.MaxRegistersPerThread.HasValue)
        {
            flags.Add($"--maxrregcount={metadata.MaxRegistersPerThread}");
        }

        // Add debug info if requested
        if (options?.GenerateDebugInfo == true)
        {
            flags.Add("-G");
            flags.Add("-lineinfo");
        }

        // Add fast math if enabled
        if (metadata.UseFastMath)
        {
            flags.Add("--use_fast_math");
        }
    }

    private static bool IsValidComputeCapability(string cc)
    {
        if (string.IsNullOrEmpty(cc)) return false;
        
        var parts = cc.Split('.');
        if (parts.Length != 2) return false;
        
        return int.TryParse(parts[0], out var major) && 
               int.TryParse(parts[1], out var minor) &&
               major >= 3 && major <= 9;
    }

    private static bool IsSupportedCudaType(Type type)
    {
        var supportedTypes = new[]
        {
            typeof(float), typeof(double), typeof(int), typeof(long), typeof(short), typeof(byte),
            typeof(bool), typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
        };

        if (supportedTypes.Contains(type)) return true;
        if (type.IsArray && supportedTypes.Contains(type.GetElementType())) return true;
        if (type.IsPointer && supportedTypes.Contains(type.GetElementType())) return true;

        return false;
    }

    private static long EstimateGlobalMemoryUsage(KernelEntryPoint entryPoint, long dataSize)
    {
        long totalMemory = 0;

        foreach (var param in entryPoint.Parameters)
        {
            if (param.Type.IsArray || param.Type.IsPointer)
            {
                var elementSize = GetTypeSize(param.Type.GetElementType() ?? param.Type);
                totalMemory += dataSize * elementSize;
            }
            else
            {
                totalMemory += GetTypeSize(param.Type);
            }
        }

        return totalMemory;
    }

    private static int GetTypeSize(Type type)
    {
        if (type == typeof(float)) return 4;
        if (type == typeof(double)) return 8;
        if (type == typeof(int)) return 4;
        if (type == typeof(long)) return 8;
        if (type == typeof(short)) return 2;
        if (type == typeof(byte)) return 1;
        if (type == typeof(bool)) return 1;
        
        return 4; // Default size
    }

    private static double EstimateFloatingPointOperations(KernelMetadata metadata, long dataSize)
    {
        var operationType = metadata.GetCompilationHint<string>("operation_type") ?? "elementwise";
        
        return operationType.ToLowerInvariant() switch
        {
            "map" or "elementwise" => dataSize * 1.0, // 1 operation per element
            "reduce" or "reduction" => dataSize * Math.Log2(dataSize), // Log reduction
            "matmul" or "gemm" => Math.Pow(dataSize, 1.5), // Assume square matrices
            _ => dataSize * 2.0 // Default estimate
        };
    }

    private static double EstimateIntegerOperations(KernelMetadata metadata, long dataSize)
    {
        // Typically fewer integer operations than floating-point in compute kernels
        return EstimateFloatingPointOperations(metadata, dataSize) * 0.1;
    }

    private static int EstimateRegisterUsage(KernelMetadata metadata)
    {
        var operationType = metadata.GetCompilationHint<string>("operation_type") ?? "elementwise";
        
        return operationType.ToLowerInvariant() switch
        {
            "map" or "elementwise" => 16,
            "reduce" or "reduction" => 32,
            "matmul" or "gemm" => 48,
            _ => 24
        };
    }

    private static double EstimateOccupancy(KernelMetadata metadata)
    {
        var baseOccupancy = 75.0; // Base occupancy percentage
        
        // Reduce occupancy based on register usage
        if (metadata.MaxRegistersPerThread.HasValue)
        {
            var regPenalty = Math.Max(0, (metadata.MaxRegistersPerThread.Value - 32) * 2);
            baseOccupancy -= regPenalty;
        }

        // Reduce occupancy based on shared memory usage
        if (metadata.SharedMemorySize > 16384) // 16KB
        {
            var memPenalty = (metadata.SharedMemorySize - 16384) / 1024.0 * 5;
            baseOccupancy -= memPenalty;
        }

        return Math.Max(10.0, Math.Min(100.0, baseOccupancy));
    }

    private static bool HasPotentialForSharedMemory(KernelEntryPoint entryPoint)
    {
        // Simple heuristic: if there are multiple input parameters, shared memory might help
        return entryPoint.GetInputParameters().Count() > 1;
    }

    private static bool HasNonCoalescedMemoryAccess(KernelEntryPoint entryPoint)
    {
        // Simple heuristic: assume non-coalesced access if there are complex parameter patterns
        return entryPoint.Parameters.Count > 3;
    }

    private static bool CanBenefitFromVectorization(KernelEntryPoint entryPoint)
    {
        // Check if parameters use float types that could benefit from vectorization
        return entryPoint.Parameters.Any(p => p.Type == typeof(float) || p.Type == typeof(float[]));
    }
}