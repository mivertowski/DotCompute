// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Extensions and utilities for CudaCompiledKernel with modern architecture support.
/// </summary>
public static class CudaCompiledKernelExtensions
{
    /// <summary>
    /// Creates an enhanced compiled kernel with optimizations for modern architectures.
    /// </summary>
    public static CudaCompiledKernel CreateEnhanced(
        CudaContext context,
        string name,
        string entryPoint,
        byte[] compiledCode,
        CompilationOptions? options,
        ILogger logger,
        CompilationMetadata? metadata = null)
    {
        return new CudaCompiledKernel(context, name, entryPoint, compiledCode, options, logger);
    }

    /// <summary>
    /// Gets optimal launch configuration for Ada generation and newer architectures.
    /// </summary>
    public static CudaLaunchConfig GetOptimalLaunchConfigForModernGPU(
        this CudaCompiledKernel kernel,
        int totalElements,
        int deviceId)
    {
        try
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            
            if (result == CudaError.Success)
            {
                var (major, minor) = (props.Major, props.Minor);
                
                // Enhanced configuration for modern architectures
                if (major >= 8 && minor >= 9) // Ada generation
                {
                    return GetAdaOptimizedConfig(totalElements, props);
                }
                else if (major >= 8) // Ampere
                {
                    return GetAmpereOptimizedConfig(totalElements, props);
                }
                else if (major >= 7) // Volta/Turing
                {
                    return GetVoltaOptimizedConfig(totalElements, props);
                }
            }
        }
        catch
        {
            // Fall back to standard configuration
        }

        // Default fallback
        return kernel.GetOptimalLaunchConfig(totalElements);
    }

    /// <summary>
    /// Gets Ada generation optimized launch configuration.
    /// </summary>
    private static CudaLaunchConfig GetAdaOptimizedConfig(int totalElements, CudaDeviceProperties props)
    {
        // Ada generation (RTX 2000 series) optimizations
        var warpSize = props.WarpSize;
        var maxThreadsPerBlock = props.MaxThreadsPerBlock;
        var multiprocessorCount = props.MultiProcessorCount;
        
        // For Ada, we can use larger blocks more efficiently
        var optimalBlockSize = Math.Min(512, maxThreadsPerBlock); // Ada handles 512-thread blocks well
        optimalBlockSize = (optimalBlockSize / warpSize) * warpSize; // Align to warp size
        
        // Calculate grid size
        var gridSize = (totalElements + optimalBlockSize - 1) / optimalBlockSize;
        
        // Ada has excellent occupancy, so we can use more aggressive configurations
        return new CudaLaunchConfig(
            (uint)gridSize, 1, 1,
            (uint)optimalBlockSize, 1, 1,
            0 // Shared memory will be allocated dynamically if needed
        );
    }

    /// <summary>
    /// Gets Ampere generation optimized launch configuration.
    /// </summary>
    private static CudaLaunchConfig GetAmpereOptimizedConfig(int totalElements, CudaDeviceProperties props)
    {
        var warpSize = props.WarpSize;
        var maxThreadsPerBlock = props.MaxThreadsPerBlock;
        
        // Ampere works well with 256-512 thread blocks
        var optimalBlockSize = Math.Min(256, maxThreadsPerBlock);
        optimalBlockSize = (optimalBlockSize / warpSize) * warpSize;
        
        var gridSize = (totalElements + optimalBlockSize - 1) / optimalBlockSize;
        
        return new CudaLaunchConfig(
            (uint)gridSize, 1, 1,
            (uint)optimalBlockSize, 1, 1
        );
    }

    /// <summary>
    /// Gets Volta/Turing optimized launch configuration.
    /// </summary>
    private static CudaLaunchConfig GetVoltaOptimizedConfig(int totalElements, CudaDeviceProperties props)
    {
        var warpSize = props.WarpSize;
        var maxThreadsPerBlock = props.MaxThreadsPerBlock;
        
        // Volta/Turing prefer moderate block sizes
        var optimalBlockSize = Math.Min(256, maxThreadsPerBlock);
        optimalBlockSize = (optimalBlockSize / warpSize) * warpSize;
        
        var gridSize = (totalElements + optimalBlockSize - 1) / optimalBlockSize;
        
        return new CudaLaunchConfig(
            (uint)gridSize, 1, 1,
            (uint)optimalBlockSize, 1, 1
        );
    }

    /// <summary>
    /// Validates launch configuration against modern GPU limits.
    /// </summary>
    public static bool ValidateEnhancedLaunchConfig(CudaLaunchConfig config, int deviceId)
    {
        try
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            
            if (result != CudaError.Success)
            {
                return false;
            }

            // Enhanced validation for modern architectures
            var blockSize = config.BlockX * config.BlockY * config.BlockZ;
            
            // Check block size limits
            if (blockSize > props.MaxThreadsPerBlock)
            {
                return false;
            }

            // Check individual dimension limits
            if (config.BlockX > props.MaxThreadsDimX ||
                config.BlockY > props.MaxThreadsDimY ||
                config.BlockZ > props.MaxThreadsDimZ)
            {
                return false;
            }

            // Check grid size limits
            if (config.GridX > props.MaxGridSizeX ||
                config.GridY > props.MaxGridSizeY ||
                config.GridZ > props.MaxGridSizeZ)
            {
                return false;
            }

            // Enhanced shared memory validation for modern architectures
            var maxSharedMemory = ComputeCapabilityExtensions.GetMaxSharedMemoryPerBlock(props.Major, props.Minor);
            if (config.SharedMemoryBytes > maxSharedMemory)
            {
                return false;
            }

            // Additional validation for modern architectures
            if (props.Major >= 8) // Ampere and newer
            {
                // Check occupancy considerations
                var warpsPerBlock = (blockSize + props.WarpSize - 1) / props.WarpSize;
                var maxWarpsPerSM = props.MaxThreadsPerMultiProcessor / props.WarpSize;
                
                // Ensure we don't exceed warp limits per SM
                if (warpsPerBlock > maxWarpsPerSM)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false; // Conservative approach on error
        }
    }

    /// <summary>
    /// Gets recommended shared memory configuration for the kernel and architecture.
    /// </summary>
    public static uint GetRecommendedSharedMemory(string kernelName, int deviceId, int blockSize)
    {
        try
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            
            if (result != CudaError.Success)
            {
                return 0;
            }

            var maxSharedMem = ComputeCapabilityExtensions.GetMaxSharedMemoryPerBlock(props.Major, props.Minor);
            
            // Conservative recommendation: use 25% of available shared memory
            // This leaves room for compiler optimizations and register spilling
            return (uint)(maxSharedMem * 0.25);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets architecture-specific performance hints for kernel optimization.
    /// </summary>
    public static string[] GetPerformanceHints(int deviceId)
    {
        var hints = new List<string>();
        
        try
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            
            if (result != CudaError.Success)
            {
                return [.. hints];
            }

            var (major, minor) = (props.Major, props.Minor);

            // General hints
            hints.Add($"Target architecture: Compute Capability {major}.{minor}");
            hints.Add($"Recommended block size: {ComputeCapabilityExtensions.GetRecommendedBlockSize(major, minor)}");
            hints.Add($"Max shared memory per block: {ComputeCapabilityExtensions.GetMaxSharedMemoryPerBlock(major, minor)} bytes");

            // Architecture-specific hints
            if (major >= 8 && minor >= 9) // Ada generation
            {
                hints.Add("Ada generation detected: Consider using FP8 operations for AI workloads");
                hints.Add("Enhanced tensor core utilization available");
                hints.Add("Improved memory bandwidth - optimize for memory-bound kernels");
            }
            else if (major >= 8) // Ampere
            {
                hints.Add("Ampere architecture: Excellent for mixed precision workloads");
                hints.Add("Use cooperative groups for advanced synchronization");
                hints.Add("Consider async copy operations for data movement");
            }
            else if (major >= 7) // Volta/Turing
            {
                hints.Add("Volta/Turing architecture: Optimize for tensor core usage");
                hints.Add("Use mixed precision (FP16) operations when possible");
            }
            else if (major >= 6) // Pascal
            {
                hints.Add("Pascal architecture: Focus on memory coalescing");
                hints.Add("Use unified memory for easier memory management");
            }

            // Feature-specific hints
            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.TensorCores))
            {
                hints.Add("Tensor cores available: Use WMMA or cuBLAS for matrix operations");
            }

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.CooperativeGroups))
            {
                hints.Add("Cooperative groups supported: Use for advanced thread cooperation");
            }

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.AsyncCopy))
            {
                hints.Add("Async copy supported: Use for overlapping computation and data transfer");
            }
        }
        catch
        {
            hints.Add("Unable to detect device properties");
        }

        return [.. hints];
    }
}

/// <summary>
/// Enhanced launch configuration with architecture-aware optimizations.
/// </summary>
public static class CudaLaunchConfigExtensions
{
    /// <summary>
    /// Creates an architecture-optimized 1D launch configuration.
    /// </summary>
    public static CudaLaunchConfig CreateOptimized1D(int totalElements, int deviceId)
    {
        try
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            
            if (result == CudaError.Success)
            {
                var blockSize = ComputeCapabilityExtensions.GetRecommendedBlockSize(props.Major, props.Minor);
                return CudaLaunchConfig.Create1D(totalElements, blockSize);
            }
        }
        catch
        {
            // Fall back to default
        }

        return CudaLaunchConfig.Create1D(totalElements);
    }

    /// <summary>
    /// Creates an architecture-optimized 2D launch configuration.
    /// </summary>
    public static CudaLaunchConfig CreateOptimized2D(int width, int height, int deviceId)
    {
        try
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            
            if (result == CudaError.Success && props.Major >= 8)
            {
                // Modern architectures can handle larger 2D blocks efficiently
                return CudaLaunchConfig.Create2D(width, height, 32, 16);
            }
        }
        catch
        {
            // Fall back to default
        }

        return CudaLaunchConfig.Create2D(width, height);
    }
}