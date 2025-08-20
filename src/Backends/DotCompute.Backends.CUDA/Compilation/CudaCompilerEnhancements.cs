// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Enums;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA.Compilation
{

    /// <summary>
    /// Enhanced compilation features for modern CUDA architectures including RTX 2000 Ada Generation.
    /// </summary>
    public static class CudaCompilerEnhancements
    {
        /// <summary>
        /// Enhanced compilation options with RTX 2000 Ada Generation (8.9) optimizations.
        /// </summary>
        public static string[] BuildEnhancedCompilationOptions(CompilationOptions? options, int deviceId)
        {
            var optionsList = new List<string>();

            // Get target GPU architecture with Ada support
            var (major, minor) = GetEnhancedComputeCapability(deviceId);

            // Use the new interop extensions for modern architectures
            if (major >= 8 && minor >= 9) // RTX 2000 Ada Generation
            {
                optionsList.Add($"--gpu-architecture=compute_{major}{minor}");
                optionsList.Add($"--gpu-code=sm_{major}{minor}");

                // Ada-specific optimizations
                optionsList.Add("--use_fast_math");
                optionsList.Add("--extra-device-vectorization");
                optionsList.Add("--optimize-float-atomics");
                optionsList.Add("--fmad=true");
                optionsList.Add("--prec-div=false");
                optionsList.Add("--prec-sqrt=false");

                // Enable FP8 tensor operations for Ada generation
                optionsList.Add("-DCUDA_ADA_FP8_SUPPORT");
                optionsList.Add("-DCUDA_ENABLE_FP8_TENSOR_CORES");
            }
            else if (major >= 8) // Ampere optimizations
            {
                optionsList.Add($"--gpu-architecture=compute_{major}{minor}");
                optionsList.Add($"--gpu-code=sm_{major}{minor}");
                optionsList.Add("--use_fast_math");
                optionsList.Add("--extra-device-vectorization");
            }
            else
            {
                // Fallback for older architectures
                optionsList.Add($"--gpu-architecture=compute_{major}{minor}");
            }

            // Add optimization level with modern enhancements
            var optLevel = options?.OptimizationLevel ?? OptimizationLevel.Default;
            switch (optLevel)
            {
                case OptimizationLevel.None:
                    optionsList.Add("-O0");
                    break;
                case OptimizationLevel.Maximum or OptimizationLevel.Aggressive:
                    optionsList.Add("-O3");
                    optionsList.Add("--use_fast_math");
                    optionsList.Add("--fmad=true");
                    if (major >= 8) // Modern architectures
                    {
                        optionsList.Add("--extra-device-vectorization");
                        optionsList.Add("--optimize-float-atomics");
                    }
                    break;
                default:
                    optionsList.Add("-O2");
                    break;
            }

            // Debug information with modern support
            if (options?.EnableDebugInfo == true)
            {
                optionsList.Add("-g");
                optionsList.Add("-G");
                optionsList.Add("--device-debug");
                optionsList.Add("--generate-line-info");
            }
            else
            {
                optionsList.Add("--restrict");
                if (major >= 7) // Volta and newer
                {
                    optionsList.Add("--extra-device-vectorization");
                }
            }

            // Modern C++ standard support
            optionsList.Add("-std=c++17");

            if (major >= 8) // Ampere and newer support C++20 features
            {
                optionsList.Add("-DCUDA_ENABLE_CPP20_FEATURES");
            }

            // Architecture-specific defines
            optionsList.Add("-DCUDA_KERNEL_COMPILATION");
            optionsList.Add($"-DCUDA_ARCH_MAJOR={major}");
            optionsList.Add($"-DCUDA_ARCH_MINOR={minor}");

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.TensorCores))
            {
                optionsList.Add("-DCUDA_ENABLE_TENSOR_CORES");
            }

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.CooperativeGroups))
            {
                optionsList.Add("-DCUDA_ENABLE_COOPERATIVE_GROUPS");
            }

            // Add user-specified additional flags
            if (options?.AdditionalFlags != null)
            {
                optionsList.AddRange(options.AdditionalFlags);
            }

            return [.. optionsList];
        }

        /// <summary>
        /// Prepares enhanced CUDA source code with architecture-specific optimizations.
        /// </summary>
        internal static async Task<string> PrepareEnhancedCudaSourceAsync(
            KernelSource source,
            CompilationOptions? options,
            int deviceId)
        {
            await Task.Delay(1).ConfigureAwait(false);

            var builder = new StringBuilder();
            var (major, minor) = GetEnhancedComputeCapability(deviceId);

            // Add enhanced header with metadata
            _ = builder.AppendLine($"// Enhanced CUDA kernel: {source.Name}");
            _ = builder.AppendLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            _ = builder.AppendLine($"// Target: Compute Capability {major}.{minor}");
            _ = builder.AppendLine($"// Optimization: {options?.OptimizationLevel ?? OptimizationLevel.Default}");
            _ = builder.AppendLine();

            // Add essential CUDA headers
            _ = builder.AppendLine("#include <cuda_runtime.h>");
            _ = builder.AppendLine("#include <device_launch_parameters.h>");

            // Add modern architecture headers
            if (major >= 8) // Ampere and newer
            {
                _ = builder.AppendLine("#include <cuda_fp16.h>");
                _ = builder.AppendLine("#include <cuda_bf16.h>");
                _ = builder.AppendLine("#include <cooperative_groups.h>");
                _ = builder.AppendLine("#include <mma.h>"); // Matrix Multiply-Accumulate
            }

            if (major >= 8 && minor >= 9) // Ada generation
            {
                _ = builder.AppendLine("#include <cuda_fp8.h>"); // FP8 support for Ada
            }

            if (major >= 9) // Hopper
            {
                _ = builder.AppendLine("#include <cuda_pipeline.h>");
                _ = builder.AppendLine("#include <cooperative_groups/memcpy_async.h>");
            }

            // Mathematical libraries based on source analysis
            if (source.Code.Contains("sin", StringComparison.Ordinal) ||
                source.Code.Contains("cos", StringComparison.Ordinal) ||
                source.Code.Contains("exp", StringComparison.Ordinal))
            {
                _ = builder.AppendLine("#include <math_functions.h>");
            }

            _ = builder.AppendLine();

            // Add performance macros based on architecture
            _ = builder.AppendLine("// Architecture-specific performance macros");
            if (options?.OptimizationLevel == OptimizationLevel.Maximum)
            {
                _ = builder.AppendLine("#define FORCE_INLINE __forceinline__");
                _ = builder.AppendLine("#define RESTRICT __restrict__");
                if (major >= 8)
                {
                    _ = builder.AppendLine("#define CUDA_ENABLE_ASYNC_COPY");
                }
            }
            else
            {
                _ = builder.AppendLine("#define FORCE_INLINE inline");
                _ = builder.AppendLine("#define RESTRICT");
            }

            // Debug macros
            if (options?.EnableDebugInfo == true)
            {
                _ = builder.AppendLine("#define DEBUG_KERNEL 1");
                _ = builder.AppendLine("#define KERNEL_ASSERT(x) assert(x)");
            }
            else
            {
                _ = builder.AppendLine("#define DEBUG_KERNEL 0");
                _ = builder.AppendLine("#define KERNEL_ASSERT(x)");
            }

            _ = builder.AppendLine();

            // Add compute capability specific features
            _ = builder.AppendLine($"// Compute Capability {major}.{minor} Features");

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.TensorCores))
            {
                _ = builder.AppendLine("#define TENSOR_CORES_AVAILABLE 1");
            }

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.CooperativeGroups))
            {
                _ = builder.AppendLine("#define COOPERATIVE_GROUPS_AVAILABLE 1");
            }

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.AsyncCopy))
            {
                _ = builder.AppendLine("#define ASYNC_COPY_AVAILABLE 1");
            }

            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.FP8))
            {
                _ = builder.AppendLine("#define FP8_AVAILABLE 1");
            }

            _ = builder.AppendLine();

            // Add architecture-specific optimizations
            if (major >= 8) // Ampere and newer
            {
                _ = builder.AppendLine("#define AMPERE_OPTIMIZATIONS 1");
                _ = builder.AppendLine($"#define MAX_SHARED_MEM_PER_BLOCK {ComputeCapabilityExtensions.GetMaxSharedMemoryPerBlock(major, minor)}");
                _ = builder.AppendLine($"#define RECOMMENDED_BLOCK_SIZE {ComputeCapabilityExtensions.GetRecommendedBlockSize(major, minor)}");
            }

            if (major >= 8 && minor >= 9) // Ada generation specific
            {
                _ = builder.AppendLine("#define ADA_OPTIMIZATIONS 1");
                _ = builder.AppendLine("#define FP8_TENSOR_CORES_AVAILABLE 1");
            }

            if (major >= 9) // Hopper
            {
                _ = builder.AppendLine("#define HOPPER_OPTIMIZATIONS 1");
                _ = builder.AppendLine("#define CLUSTER_API_AVAILABLE 1");
            }

            _ = builder.AppendLine();

            // Add the kernel source code
            switch (source.Language)
            {
                case KernelLanguage.Cuda:
                    _ = builder.Append(source.Code);
                    break;

                case KernelLanguage.OpenCL:
                    // Enhanced OpenCL to CUDA conversion
                    var convertedCode = ConvertEnhancedOpenClToCuda(source.Code, major, minor);
                    _ = builder.Append(convertedCode);
                    break;

                default:
                    throw new NotSupportedException($"Kernel language '{source.Language}' is not supported");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Enhanced OpenCL to CUDA conversion with modern architecture support.
        /// </summary>
        private static string ConvertEnhancedOpenClToCuda(string openClCode, int major, int minor)
        {
            var cudaCode = openClCode;

            // Basic OpenCL to CUDA mapping
            cudaCode = cudaCode.Replace("__kernel", "__global__", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("__global", "__device__", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("__local", "__shared__", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("__constant", "__constant__", StringComparison.Ordinal);

            // Thread indexing
            cudaCode = cudaCode.Replace("get_global_id(0)", "blockIdx.x * blockDim.x + threadIdx.x", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_global_id(1)", "blockIdx.y * blockDim.y + threadIdx.y", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_global_id(2)", "blockIdx.z * blockDim.z + threadIdx.z", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_local_id(0)", "threadIdx.x", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_local_id(1)", "threadIdx.y", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_local_id(2)", "threadIdx.z", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_group_id(0)", "blockIdx.x", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_group_id(1)", "blockIdx.y", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("get_group_id(2)", "blockIdx.z", StringComparison.Ordinal);

            // Synchronization
            cudaCode = cudaCode.Replace("barrier(CLK_LOCAL_MEM_FENCE)", "__syncthreads()", StringComparison.Ordinal);
            cudaCode = cudaCode.Replace("barrier(CLK_GLOBAL_MEM_FENCE)", "__threadfence()", StringComparison.Ordinal);

            // Enhanced synchronization for modern architectures
            if (major >= 8)
            {
                cudaCode = cudaCode.Replace("mem_fence(CLK_LOCAL_MEM_FENCE)", "__threadfence_block()", StringComparison.Ordinal);
                cudaCode = cudaCode.Replace("mem_fence(CLK_GLOBAL_MEM_FENCE)", "__threadfence()", StringComparison.Ordinal);
            }

            // Cooperative groups for modern architectures
            if (ComputeCapabilityExtensions.SupportsFeature(major, minor, ComputeFeature.CooperativeGroups))
            {
                // Add cooperative groups namespace if not present
                if (!cudaCode.Contains("cooperative_groups", StringComparison.Ordinal))
                {
                    cudaCode = "using namespace cooperative_groups;\n" + cudaCode;
                }
            }

            return cudaCode;
        }

        /// <summary>
        /// Gets compute capability with enhanced detection for modern architectures.
        /// </summary>
        private static (int major, int minor) GetEnhancedComputeCapability(int deviceId)
        {
            try
            {
                var result = CudaRuntime.cudaGetDevice(out var currentDevice);
                if (result == CudaError.Success)
                {
                    var props = new CudaDeviceProperties();
                    result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
                    if (result == CudaError.Success)
                    {
                        return (props.Major, props.Minor);
                    }
                }
            }
            catch
            {
                // Fall through to default
            }

            // Enhanced fallback - detect RTX 2000 Ada if possible
            try
            {
                var supportedArchs = NvrtcInterop.GetSupportedArchitectures();
                if (supportedArchs.Contains(89)) // 8.9 for Ada
                {
                    return ComputeCapabilityExtensions.KnownComputeCapabilities.Ada_89;
                }
                if (supportedArchs.Contains(86)) // 8.6 for RTX 30 series
                {
                    return ComputeCapabilityExtensions.KnownComputeCapabilities.Ampere_86;
                }
                if (supportedArchs.Contains(80)) // 8.0 for A100
                {
                    return ComputeCapabilityExtensions.KnownComputeCapabilities.Ampere_80;
                }
            }
            catch
            {
                // Fall through to default
            }

            // Safe default for modern development
            return ComputeCapabilityExtensions.KnownComputeCapabilities.Ampere_80;
        }

        /// <summary>
        /// Compiles kernel using enhanced NVRTC with modern architecture support.
        /// </summary>
        public static async Task<byte[]> CompileWithEnhancedNvrtcAsync(
            string cudaSource,
            string kernelName,
            CompilationOptions? options,
            int deviceId,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            var stopwatch = Stopwatch.StartNew();
            var program = IntPtr.Zero;

            try
            {
                logger.LogDebug("Starting enhanced NVRTC compilation for: {KernelName}", kernelName);

                // Create NVRTC program using enhanced interop
                var result = NvrtcInterop.nvrtcCreateProgram(
                    out program,
                    cudaSource,
                    kernelName + ".cu",
                    0, // numHeaders
                    null, // headers
                    null  // includeNames
                );
                NvrtcInterop.CheckResult(result, "creating NVRTC program");

                // Build enhanced compilation options
                var compilationOptions = BuildEnhancedCompilationOptions(options, deviceId);

                logger.LogDebug("Enhanced NVRTC options: {Options}", string.Join(" ", compilationOptions));

                // Compile the program
                result = NvrtcInterop.nvrtcCompileProgram(
                    program,
                    compilationOptions.Length,
                    compilationOptions);

                // Get compilation log
                var compilerLog = NvrtcInterop.GetCompilationLog(program);
                if (!string.IsNullOrWhiteSpace(compilerLog))
                {
                    if (result == NvrtcResult.Success)
                    {
                        logger.LogInformation("NVRTC compilation log for {KernelName}: {Log}", kernelName, compilerLog);
                    }
                    else
                    {
                        logger.LogError("NVRTC compilation failed for {KernelName}: {Log}", kernelName, compilerLog);
                    }
                }

                // Check compilation result
                NvrtcInterop.CheckResult(result, $"compiling kernel '{kernelName}'");

                // Get compiled code (PTX or CUBIN)
                byte[] compiledCode;
                var (major, minor) = GetEnhancedComputeCapability(deviceId);

                // Use CUBIN for maximum performance on modern architectures
                if (ShouldUseCubinForArchitecture(major, minor, options))
                {
                    compiledCode = NvrtcInterop.GetCubinCode(program);
                    logger.LogInformation("Generated CUBIN binary ({Size} bytes) for kernel: {KernelName}",
                        compiledCode.Length, kernelName);
                }
                else
                {
                    compiledCode = NvrtcInterop.GetPtxCode(program);
                    logger.LogInformation("Generated PTX code ({Size} bytes) for kernel: {KernelName}",
                        compiledCode.Length, kernelName);
                }

                stopwatch.Stop();
                logger.LogInformation("Enhanced NVRTC compilation completed for {KernelName} in {ElapsedMs}ms",
                    kernelName, stopwatch.ElapsedMilliseconds);

                return compiledCode;
            }
            catch (Exception ex) when (ex is not NvrtcException)
            {
                logger.LogError(ex, "Enhanced NVRTC compilation failed for: {KernelName}", kernelName);
                throw new KernelCompilationException($"Enhanced NVRTC compilation failed for kernel '{kernelName}'", ex);
            }
            finally
            {
                if (program != IntPtr.Zero)
                {
                    try
                    {
                        _ = NvrtcInterop.nvrtcDestroyProgram(ref program);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to cleanup NVRTC program for: {KernelName}", kernelName);
                    }
                }
            }
        }

        /// <summary>
        /// Determines if CUBIN should be used for the given architecture and options.
        /// </summary>
        private static bool ShouldUseCubinForArchitecture(int major, int minor, CompilationOptions? options)
        {
            // Use CUBIN for maximum optimization and modern architectures
            if (options?.OptimizationLevel == OptimizationLevel.Maximum || options?.OptimizationLevel == OptimizationLevel.Aggressive)
            {
                // CUBIN is supported on compute capability 3.5 and above
                // Prefer it for modern architectures for better performance
                return major > 7 || (major >= 3 && minor >= 5);
            }

            // Use CUBIN by default for Ada generation and newer
            return major > 8 || (major == 8 && minor >= 9);
        }
    }
}
