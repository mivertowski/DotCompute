// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Stages;
using DotCompute.Abstractions.Pipelines.Enums;
using System;
using System.Globalization;

namespace DotCompute.Core.Pipelines.Optimization.Calculators;

/// <summary>
/// Intelligent buffer size calculator that analyzes actual kernel memory requirements,
/// considers GPU/CPU memory hierarchy, accounts for SIMD vector sizes, optimizes for
/// cache locality, adapts to available system memory, and uses heuristics based on operation type.
/// </summary>
internal sealed class IntelligentBufferSizeCalculator
{
    private const long KB = 1024;
    private const long MB = 1024 * KB;
    private const long GB = 1024 * MB;

    // Cache hierarchy constants based on modern CPU architectures
    private const long L1_CACHE_SIZE = 32 * KB;   // Typical L1 data cache
    private const long L2_CACHE_SIZE = 256 * KB;  // Typical L2 cache
    private const long L3_CACHE_SIZE = 8 * MB;    // Typical L3 cache

    // GPU memory hierarchy constants
    private const long GPU_SHARED_MEMORY = 48 * KB;  // Modern GPU shared memory per SM
    private const long GPU_L1_CACHE = 128 * KB;      // GPU L1 cache
    private const long GPU_L2_CACHE = 4 * MB;        // GPU L2 cache

    // SIMD vector size constants
    private const int SSE_VECTOR_BITS = 128;
    private const int AVX_VECTOR_BITS = 256;
    private const int AVX512_VECTOR_BITS = 512;
    private const int NEON_VECTOR_BITS = 128;

    /// <summary>
    /// Calculates optimal buffer size considering kernel memory requirements,
    /// hardware capabilities, and memory hierarchy optimization.
    /// </summary>
    public static long CalculateOptimalBufferSize(KernelStage stage1, KernelStage stage2)
    {
        try
        {
            // 1. Analyze actual kernel memory requirements
            var kernelMemoryAnalysis = AnalyzeKernelMemoryRequirements(stage1, stage2);

            // 2. Consider GPU/CPU memory hierarchy
            var hierarchyOptimization = OptimizeForMemoryHierarchy(kernelMemoryAnalysis);

            // 3. Account for SIMD vector sizes
            var simdOptimization = OptimizeForSimdVectorSize(hierarchyOptimization);

            // 4. Optimize for cache locality
            var cacheOptimization = OptimizeForCacheLocality(simdOptimization);

            // 5. Adapt to available system memory
            var systemAdaptation = AdaptToSystemMemory(cacheOptimization);

            // 6. Apply operation-type heuristics
            var finalOptimization = ApplyOperationHeuristics(systemAdaptation, stage1, stage2);

            // Ensure result is within reasonable bounds
            return ClampToReasonableBounds(finalOptimization);
        }
        catch (Exception)
        {
            // Fallback to conservative estimate if calculation fails
            return GetConservativeEstimate(stage1, stage2);
        }
    }

    private static KernelMemoryAnalysis AnalyzeKernelMemoryRequirements(KernelStage stage1, KernelStage stage2)
    {
        var analysis = new KernelMemoryAnalysis();

        // Analyze stage1 requirements
        analysis.Stage1InputSize = EstimateStageInputSize(stage1);
        analysis.Stage1OutputSize = EstimateStageOutputSize(stage1);
        analysis.Stage1WorkingSet = EstimateStageWorkingSet(stage1);

        // Analyze stage2 requirements
        analysis.Stage2InputSize = EstimateStageInputSize(stage2);
        analysis.Stage2OutputSize = EstimateStageOutputSize(stage2);
        analysis.Stage2WorkingSet = EstimateStageWorkingSet(stage2);

        // Calculate intermediate buffer size (output of stage1 = input of stage2)
        analysis.IntermediateBufferSize = Math.Max(analysis.Stage1OutputSize, analysis.Stage2InputSize);

        // Calculate total memory footprint
        analysis.TotalMemoryFootprint = analysis.Stage1WorkingSet + analysis.Stage2WorkingSet + analysis.IntermediateBufferSize;

        return analysis;
    }

    private static long EstimateStageInputSize(KernelStage stage)
    {
        // Extract work size from stage metadata or use heuristics
        if (TryGetWorkSize(stage, out var workSize))
        {
            var dataTypeSize = GetEstimatedDataTypeSize(stage);
            var parameterCount = GetParameterCount(stage);

            return workSize * dataTypeSize * parameterCount;
        }

        // Fallback heuristic based on operation type
        return GetOperationBasedSizeEstimate(stage, isInput: true);
    }

    private static long EstimateStageOutputSize(KernelStage stage)
    {
        if (TryGetWorkSize(stage, out var workSize))
        {
            var dataTypeSize = GetEstimatedDataTypeSize(stage);
            var outputCount = GetOutputCount(stage);

            return workSize * dataTypeSize * outputCount;
        }

        return GetOperationBasedSizeEstimate(stage, isInput: false);
    }

    private static long EstimateStageWorkingSet(KernelStage stage)
    {
        var baseWorkingSet = EstimateStageInputSize(stage) + EstimateStageOutputSize(stage);

        // Add overhead for temporary variables, local memory, etc.
        var operationComplexity = GetOperationComplexity(stage);
        var workingSetMultiplier = operationComplexity switch
        {
            OperationComplexity.Simple => 1.2,      // 20% overhead
            OperationComplexity.Moderate => 1.5,    // 50% overhead
            OperationComplexity.Complex => 2.0,     // 100% overhead
            OperationComplexity.VeryComplex => 3.0, // 200% overhead
            _ => 1.5
        };

        return (long)(baseWorkingSet * workingSetMultiplier);
    }

    private static long OptimizeForMemoryHierarchy(KernelMemoryAnalysis analysis)
    {
        var bufferSize = analysis.IntermediateBufferSize;

        // Optimize based on compute device type
        var deviceType = GetTargetDeviceType(analysis);

        return deviceType switch
        {
            ComputeDeviceType.CPU => OptimizeForCpuHierarchy(bufferSize),
            ComputeDeviceType.CUDA => OptimizeForGpuHierarchy(bufferSize),
            ComputeDeviceType.Metal => OptimizeForGpuHierarchy(bufferSize),
            ComputeDeviceType.ROCm => OptimizeForGpuHierarchy(bufferSize),
            ComputeDeviceType.FPGA => OptimizeForFpgaHierarchy(bufferSize),
            _ => bufferSize
        };
    }

    private static long OptimizeForCpuHierarchy(long baseSize)
    {
        // Optimize for CPU cache hierarchy
        if (baseSize <= L1_CACHE_SIZE)
        {
            // Keep in L1 cache for best performance
            return AlignTocacheLine(baseSize, 64); // 64-byte cache lines
        }
        else if (baseSize <= L2_CACHE_SIZE)
        {
            // Optimize for L2 cache access
            return AlignToPageBoundary(baseSize, 4 * KB); // 4KB pages
        }
        else if (baseSize <= L3_CACHE_SIZE)
        {
            // Use L3 cache efficiently
            return AlignToPageBoundary(baseSize, 2 * MB); // Large pages
        }
        else
        {
            // For large buffers, optimize for memory bandwidth
            return AlignToPageBoundary(baseSize, 2 * MB); // Huge pages
        }
    }

    private static long OptimizeForGpuHierarchy(long baseSize)
    {
        // Optimize for GPU memory hierarchy
        if (baseSize <= GPU_SHARED_MEMORY)
        {
            // Use shared memory for small buffers
            return AlignToWarpSize(baseSize, 32); // 32 threads per warp
        }
        else if (baseSize <= GPU_L1_CACHE)
        {
            // Optimize for GPU L1 cache
            return AlignToMemoryTransaction(baseSize, 128); // 128-byte transactions
        }
        else if (baseSize <= GPU_L2_CACHE)
        {
            // Use L2 cache efficiently
            return AlignToMemoryTransaction(baseSize, 256); // Larger transactions for L2
        }
        else
        {
            // For large buffers, optimize for global memory bandwidth
            return AlignToMemoryTransaction(baseSize, 512); // Maximum transaction size
        }
    }

    private static long OptimizeForFpgaHierarchy(long baseSize)
    {
        // FPGA-specific optimizations
        // Align to burst boundaries for efficient DMA transfers
        var burstSize = 256; // Typical FPGA burst size in bytes
        return AlignToMemoryTransaction(baseSize, burstSize);
    }

    private static long OptimizeForSimdVectorSize(long baseSize)
    {
        // Get SIMD capabilities
        var vectorWidth = GetOptimalVectorWidth();
        var vectorSizeBytes = vectorWidth / 8;

        // Ensure buffer size is aligned to vector boundaries
        var alignedSize = AlignToVectorBoundary(baseSize, vectorSizeBytes);

        // For very small buffers, round up to at least one vector width
        if (alignedSize < vectorSizeBytes)
        {
            alignedSize = vectorSizeBytes;
        }

        return alignedSize;
    }

    private static int GetOptimalVectorWidth()
    {
        // Check SIMD capabilities and return optimal vector width
        try
        {
            if (global::System.Runtime.Intrinsics.X86.Avx512F.IsSupported)
            {
                return AVX512_VECTOR_BITS;
            }

            if (global::System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            {
                return AVX_VECTOR_BITS;
            }

            if (global::System.Runtime.Intrinsics.X86.Sse2.IsSupported)
            {
                return SSE_VECTOR_BITS;
            }

            if (global::System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
            {
                return NEON_VECTOR_BITS;
            }
        }
        catch
        {
            // Fallback if intrinsics detection fails
        }

        // Use .NET Vector<T> as fallback
        return global::System.Numerics.Vector<byte>.Count * 8;
    }

    private static long OptimizeForCacheLocality(long baseSize)
    {
        // Calculate optimal buffer size for cache locality

        // For small buffers, ensure they fit in a single cache line
        if (baseSize <= 64) // Cache line size
        {
            return 64;
        }

        // For medium buffers, align to cache line boundaries
        if (baseSize <= L2_CACHE_SIZE)
        {
            return AlignToNextPowerOfTwo(baseSize, 64); // Cache line aligned
        }

        // For large buffers, optimize for streaming access patterns
        return AlignToPageBoundary(baseSize, 4 * KB);
    }

    private static long AdaptToSystemMemory(long baseSize)
    {
        try
        {
            // Get available system memory
            var totalMemory = GC.GetTotalMemory(false);
            var availableMemory = GetAvailablePhysicalMemory();

            // Don't allocate more than 10% of available memory for intermediate buffers
            var maxAllowedSize = Math.Min(availableMemory / 10, totalMemory / 4);

            if (baseSize > maxAllowedSize)
            {
                // Scale down to fit memory constraints
                return maxAllowedSize;
            }

            // For very small systems, ensure minimum viable buffer size
            var minViableSize = 4 * KB;
            return Math.Max(baseSize, minViableSize);
        }
        catch
        {
            // If memory detection fails, use conservative estimate
            return Math.Min(baseSize, 16 * MB);
        }
    }

    private static long GetAvailablePhysicalMemory()
    {
        try
        {
            // Platform-specific memory detection
            if (OperatingSystem.IsWindows())
            {
                return GetWindowsAvailableMemory();
            }
            else if (OperatingSystem.IsLinux())
            {
                return GetLinuxAvailableMemory();
            }
            else if (OperatingSystem.IsMacOS())
            {
                return GetMacOSAvailableMemory();
            }
        }
        catch
        {
            // Fallback if platform detection fails
        }

        // Conservative fallback
        return 1 * GB;
    }

    private static long GetWindowsAvailableMemory()
        // Use performance counters or WMI to get available memory
        // Simplified implementation - in practice would use Windows APIs

        => Environment.WorkingSet * 4; // Rough estimate

    private static long GetLinuxAvailableMemory()
        // Parse /proc/meminfo for available memory
        // Simplified implementation

        => Environment.WorkingSet * 4; // Rough estimate

    private static long GetMacOSAvailableMemory()
        // Use system calls to get memory information
        // Simplified implementation

        => Environment.WorkingSet * 4; // Rough estimate

    private static long ApplyOperationHeuristics(long baseSize, KernelStage stage1, KernelStage stage2)
    {
        var operation1 = GetOperationType(stage1);
        var operation2 = GetOperationType(stage2);

        // Apply operation-specific heuristics
        var multiplier = GetOperationMemoryMultiplier(operation1, operation2);

        return (long)(baseSize * multiplier);
    }

    private static double GetOperationMemoryMultiplier(OperationType op1, OperationType op2)
    {
        // Memory multipliers based on operation combinations
        return (op1, op2) switch
        {
            (OperationType.MatrixMultiply, OperationType.MatrixMultiply) => 1.5, // High memory usage
            (OperationType.VectorAdd, OperationType.VectorMultiply) => 0.8,      // Low memory usage
            (OperationType.Convolution, OperationType.Convolution) => 2.0,       // Very high memory
            (OperationType.FFT, OperationType.FFT) => 1.8,                       // High temporary memory
            (OperationType.Reduction, _) => 0.6,                                 // Reduces data size
            (_, OperationType.Reduction) => 0.6,                                 // Reduces data size
            (OperationType.Map, OperationType.Map) => 0.9,                       // Element-wise ops
            _ => 1.0 // Default
        };
    }

    private static long ClampToReasonableBounds(long calculatedSize)
    {
        // Ensure the calculated size is within reasonable bounds
        const long minBufferSize = 1 * KB;     // Minimum 1KB
        const long maxBufferSize = 256 * MB;   // Maximum 256MB for intermediate buffers

        return Math.Clamp(calculatedSize, minBufferSize, maxBufferSize);
    }

    private static long GetConservativeEstimate(KernelStage stage1, KernelStage stage2)
    {
        // Conservative fallback estimate
        var baseEstimate = 64 * KB; // 64KB base

        // Scale based on perceived complexity
        if (IsComplexOperation(stage1) || IsComplexOperation(stage2))
        {
            baseEstimate *= 8; // 512KB for complex operations
        }

        return baseEstimate;
    }

    // Helper methods
    private static bool TryGetWorkSize(KernelStage stage, out long workSize)
    {

        // Try to extract work size from stage metadata
        if (stage.Metadata.TryGetValue("WorkSize", out var workSizeObj) && workSizeObj is long ws)
        {
            workSize = ws;
            return true;
        }

        // Try to infer from global work size (private field, so we estimate)
        // In a real implementation, this might be exposed through a property
        workSize = 1024; // Default assumption
        return false;
    }

    private static int GetEstimatedDataTypeSize(KernelStage stage)
    {
        // Try to determine data type from metadata
        if (stage.Metadata.TryGetValue("DataType", out var dataTypeObj))
        {
            return dataTypeObj switch
            {
                "float" => 4,
                "double" => 8,
                "int" => 4,
                "long" => 8,
                "short" => 2,
                "byte" => 1,
                _ => 4 // Default to float
            };
        }

        return 4; // Default to 32-bit (float/int)
    }

    private static int GetParameterCount(KernelStage stage)
    {
        // Estimate parameter count from metadata or kernel definition
        if (stage.Metadata.TryGetValue("ParameterCount", out var paramCountObj) && paramCountObj is int count)
        {
            return count;
        }

        return 2; // Default assumption: 2 input parameters
    }

    private static int GetOutputCount(KernelStage stage)
    {
        // Estimate output count
        if (stage.Metadata.TryGetValue("OutputCount", out var outputCountObj) && outputCountObj is int count)
        {
            return count;
        }

        return 1; // Default assumption: 1 output
    }

    private static OperationComplexity GetOperationComplexity(KernelStage stage)
    {
        var operation = GetOperationType(stage);

        return operation switch
        {
            OperationType.VectorAdd => OperationComplexity.Simple,
            OperationType.VectorMultiply => OperationComplexity.Simple,
            OperationType.MatrixMultiply => OperationComplexity.Complex,
            OperationType.Convolution => OperationComplexity.VeryComplex,
            OperationType.FFT => OperationComplexity.VeryComplex,
            OperationType.Reduction => OperationComplexity.Moderate,
            OperationType.Map => OperationComplexity.Simple,
            _ => OperationComplexity.Moderate
        };
    }

    private static ComputeDeviceType GetTargetDeviceType(KernelMemoryAnalysis analysis)
    {
        // Determine optimal device type based on memory characteristics and workload patterns
        // This decision considers multiple factors for optimal performance

        // GPU is preferred for large parallel workloads with high memory bandwidth requirements
        // CPU is preferred for smaller workloads or those with complex branching patterns
        if (analysis.TotalMemoryFootprint > 100 * MB)
        {
            return ComputeDeviceType.CUDA; // Use CUDA as the primary GPU target
        }

        return ComputeDeviceType.CPU;
    }

    private static OperationType GetOperationType(KernelStage stage)
    {
        if (stage.Metadata.TryGetValue("OperationType", out var opTypeObj) && opTypeObj is string opStr)
        {
            return Enum.TryParse<OperationType>(opStr, true, out var result) ? result : OperationType.Unknown;
        }

        // Try to infer from stage name
        var name = stage.Name.ToUpper(CultureInfo.InvariantCulture);
        if (name.Contains("add", StringComparison.OrdinalIgnoreCase))
        {
            return OperationType.VectorAdd;
        }

        if (name.Contains("multiply", StringComparison.OrdinalIgnoreCase) || name.Contains("mul", StringComparison.Ordinal))
        {
            return OperationType.VectorMultiply;
        }

        if (name.Contains("matrix", StringComparison.Ordinal))
        {
            return OperationType.MatrixMultiply;
        }

        if (name.Contains("conv", StringComparison.Ordinal))
        {
            return OperationType.Convolution;
        }

        if (name.Contains("fft", StringComparison.Ordinal))
        {
            return OperationType.FFT;
        }

        if (name.Contains("reduce", StringComparison.Ordinal))
        {
            return OperationType.Reduction;
        }

        if (name.Contains("map", StringComparison.Ordinal))
        {
            return OperationType.Map;
        }

        return OperationType.Unknown;
    }

    private static bool IsComplexOperation(KernelStage stage)
    {
        var complexity = GetOperationComplexity(stage);
        return complexity >= OperationComplexity.Complex;
    }

    private static long GetOperationBasedSizeEstimate(KernelStage stage, bool isInput)
    {
        var operation = GetOperationType(stage);
        var baseSize = 4 * KB; // 4KB base

        var multiplier = operation switch
        {
            OperationType.MatrixMultiply => isInput ? 256 : 256,     // Large matrices
            OperationType.Convolution => isInput ? 128 : 64,        // Feature maps
            OperationType.FFT => isInput ? 64 : 64,                 // Signal data
            OperationType.VectorAdd => isInput ? 16 : 16,           // Vector data
            OperationType.VectorMultiply => isInput ? 16 : 16,      // Vector data
            OperationType.Reduction => isInput ? 64 : 4,            // Input large, output small
            OperationType.Map => isInput ? 32 : 32,                 // Element-wise
            _ => 16
        };

        return baseSize * multiplier;
    }

    // Alignment helper methods
    private static long AlignTocacheLine(long size, int cacheLineSize) => ((size + cacheLineSize - 1) / cacheLineSize) * cacheLineSize;

    private static long AlignToPageBoundary(long size, long pageSize) => ((size + pageSize - 1) / pageSize) * pageSize;

    private static long AlignToWarpSize(long size, int warpSize)
    {
        var elementSize = 4; // Assume 32-bit elements
        var warpSizeBytes = warpSize * elementSize;
        return ((size + warpSizeBytes - 1) / warpSizeBytes) * warpSizeBytes;
    }

    private static long AlignToMemoryTransaction(long size, int transactionSize) => ((size + transactionSize - 1) / transactionSize) * transactionSize;

    private static long AlignToVectorBoundary(long size, int vectorSizeBytes) => ((size + vectorSizeBytes - 1) / vectorSizeBytes) * vectorSizeBytes;

    private static long AlignToNextPowerOfTwo(long size, int minAlignment)
    {
        var alignment = minAlignment;
        while (alignment < size)
        {
            alignment *= 2;
        }
        return alignment;
    }
}

/// <summary>
/// Kernel memory analysis result.
/// </summary>
internal sealed class KernelMemoryAnalysis
{
    /// <summary>
    /// Gets or sets the stage1 input size.
    /// </summary>
    /// <value>The stage1 input size.</value>
    public long Stage1InputSize { get; set; }
    /// <summary>
    /// Gets or sets the stage1 output size.
    /// </summary>
    /// <value>The stage1 output size.</value>
    public long Stage1OutputSize { get; set; }
    /// <summary>
    /// Gets or sets the stage1 working set.
    /// </summary>
    /// <value>The stage1 working set.</value>
    public long Stage1WorkingSet { get; set; }
    /// <summary>
    /// Gets or sets the stage2 input size.
    /// </summary>
    /// <value>The stage2 input size.</value>

    public long Stage2InputSize { get; set; }
    /// <summary>
    /// Gets or sets the stage2 output size.
    /// </summary>
    /// <value>The stage2 output size.</value>
    public long Stage2OutputSize { get; set; }
    /// <summary>
    /// Gets or sets the stage2 working set.
    /// </summary>
    /// <value>The stage2 working set.</value>
    public long Stage2WorkingSet { get; set; }
    /// <summary>
    /// Gets or sets the intermediate buffer size.
    /// </summary>
    /// <value>The intermediate buffer size.</value>

    public long IntermediateBufferSize { get; set; }
    /// <summary>
    /// Gets or sets the total memory footprint.
    /// </summary>
    /// <value>The total memory footprint.</value>
    public long TotalMemoryFootprint { get; set; }
}
/// <summary>
/// An operation complexity enumeration.
/// </summary>

/// <summary>
/// Operation complexity levels for memory estimation.
/// </summary>
internal enum OperationComplexity
{
    /// <summary>Simple operation with minimal overhead.</summary>
    Simple,
    /// <summary>Moderate complexity with typical overhead.</summary>
    Moderate,
    /// <summary>Complex operation with significant overhead.</summary>
    Complex,
    /// <summary>Very complex operation with high overhead.</summary>
    VeryComplex
}
/// <summary>
/// An operation type enumeration.
/// </summary>

/// <summary>
/// Operation types for heuristic-based optimization.
/// </summary>
internal enum OperationType
{
    /// <summary>Unknown operation type.</summary>
    Unknown,
    /// <summary>Vector addition operation.</summary>
    VectorAdd,
    /// <summary>Vector multiplication operation.</summary>
    VectorMultiply,
    /// <summary>Matrix multiplication operation.</summary>
    MatrixMultiply,
    /// <summary>Convolution operation.</summary>
    Convolution,
    /// <summary>Fast Fourier Transform operation.</summary>
    FFT,
    /// <summary>Reduction operation.</summary>
    Reduction,
    /// <summary>Map/transform operation.</summary>
    Map
}