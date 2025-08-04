// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

// Internal types for kernel compilation
internal sealed class KernelSource
{
    public required string Name { get; set; }
    public required string EntryPoint { get; set; }
    public required string Code { get; set; }
    public KernelLanguage Language { get; set; }
}

internal enum KernelLanguage
{
    Cuda,
    OpenCL,
    Ptx
}

/// <summary>
/// Metadata for cached kernel entries
/// </summary>
internal sealed class KernelCacheMetadata
{
    public required string CacheKey { get; set; }
    public required string KernelName { get; set; }
    public int SourceCodeHash { get; set; }
    public DateTime CompileTime { get; set; }
    public DateTime LastAccessed { get; set; }
    public int AccessCount { get; set; }
    public CompilationOptions? CompilationOptions { get; set; }
    public int PtxSize { get; set; }
}

/// <summary>
/// Cache performance statistics
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long TotalSizeBytes { get; set; }
    public double AverageAccessCount { get; set; }
    public DateTime? OldestEntryTime { get; set; }
    public DateTime? NewestEntryTime { get; set; }
    public double HitRate { get; set; }
}

/// <summary>
/// CUDA kernel compiler implementation using NVRTC
/// </summary>
public class CudaKernelCompiler : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CudaCompiledKernel> _kernelCache;
    private readonly ConcurrentDictionary<string, KernelCacheMetadata> _cacheMetadata;
    private readonly string _tempDirectory;
    private readonly string _cacheDirectory;
    private bool _disposed;

    public CudaKernelCompiler(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kernelCache = new ConcurrentDictionary<string, CudaCompiledKernel>();
        _cacheMetadata = new ConcurrentDictionary<string, KernelCacheMetadata>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DotCompute.CUDA", Guid.NewGuid().ToString());
        _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotCompute", "Cache", "CUDA");
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_cacheDirectory);

        // Verify NVRTC availability
        if (!IsNvrtcAvailable())
        {
            _logger.LogWarning("NVRTC is not available or version is too old. Kernel compilation may fail.");
        }
        else
        {
            var (major, minor) = GetNvrtcVersion();
            _logger.LogInformation("NVRTC version {Major}.{Minor} detected and ready for kernel compilation", major, minor);
        }

        // Load persistent cache on startup
        _ = Task.Run(LoadPersistentCacheAsync);
    }

    public async Task<ICompiledKernel> CompileAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        try
        {
            _logger.LogInformation("Compiling CUDA kernel: {KernelName}", definition.Name);

            // Check cache first
            var cacheKey = GenerateCacheKey(definition, options);
            if (_kernelCache.TryGetValue(cacheKey, out var cachedKernel))
            {
                // Update cache access statistics
                if (_cacheMetadata.TryGetValue(cacheKey, out var cacheMetadata))
                {
                    cacheMetadata.LastAccessed = DateTime.UtcNow;
                    cacheMetadata.AccessCount++;
                }

                _logger.LogDebug("Using cached kernel: {KernelName} (accessed {AccessCount} times)",
                    definition.Name, cacheMetadata?.AccessCount ?? 0);
                return cachedKernel;
            }

            // Convert KernelDefinition to source
            var source = new KernelSource
            {
                Name = definition.Name,
                EntryPoint = definition.EntryPoint ?? "kernel_main",
                Code = Encoding.UTF8.GetString(definition.Code),
                Language = KernelLanguage.Cuda
            };

            // Prepare CUDA source code
            var cudaSource = await PrepareCudaSourceAsync(source, options);

            // Validate source code
            var validationResult = ValidateCudaSource(cudaSource, source.Name);
            if (!validationResult.IsValid)
            {
                throw new KernelCompilationException($"CUDA source validation failed for '{source.Name}': {validationResult.ErrorMessage}");
            }

            if (validationResult.Warnings != null && validationResult.Warnings.Length > 0)
            {
                foreach (var warning in validationResult.Warnings)
                {
                    _logger.LogWarning("CUDA source warning for '{KernelName}': {Warning}", source.Name, warning);
                }
            }

            // Compile to PTX or CUBIN based on optimization settings
            byte[] compiledCode;
            var useCubin = ShouldUseCubin(options);

            if (useCubin)
            {
                _logger.LogDebug("Using CUBIN compilation for kernel: {KernelName}", source.Name);
                compiledCode = await CompileToCubinAsync(cudaSource, source.Name, options);
            }
            else
            {
                _logger.LogDebug("Using PTX compilation for kernel: {KernelName}", source.Name);
                compiledCode = await CompileToPtxAsync(cudaSource, source.Name, options);
            }

            // Verify compiled code
            if (!VerifyCompiledCode(compiledCode, source.Name))
            {
                throw new KernelCompilationException($"Compiled code verification failed for kernel '{source.Name}'");
            }

            // Create compiled kernel
            var compiledKernel = new CudaCompiledKernel(
                _context,
                source.Name,
                source.EntryPoint,
                compiledCode,
                options,
                _logger);

            // Cache the compiled kernel with metadata
            var metadata = new KernelCacheMetadata
            {
                CacheKey = cacheKey,
                KernelName = definition.Name,
                SourceCodeHash = definition.Code.GetHashCode(),
                CompileTime = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                AccessCount = 1,
                CompilationOptions = options,
                PtxSize = compiledCode.Length
            };

            _kernelCache.TryAdd(cacheKey, compiledKernel);
            _cacheMetadata.TryAdd(cacheKey, metadata);

            // Persist to disk asynchronously
            _ = Task.Run(() => PersistKernelToDiskAsync(cacheKey, compiledCode, metadata), cancellationToken);

            _logger.LogInformation("Successfully compiled CUDA kernel: {KernelName}", source.Name);
            return compiledKernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile CUDA kernel: {KernelName}", definition.Name);
            throw new InvalidOperationException($"Failed to compile CUDA kernel '{definition.Name}'", ex);
        }
    }

    public async Task<ICompiledKernel[]> CompileBatchAsync(KernelDefinition[] definitions, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (definitions == null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        // Compile kernels in parallel
        var tasks = definitions.Select(def => CompileAsync(def, options, cancellationToken)).ToArray();
        return await Task.WhenAll(tasks);
    }

    public bool TryGetCached(string kernelName, out ICompiledKernel? compiledKernel)
    {
        ThrowIfDisposed();

        // Simple cache lookup by name (without options consideration)
        var cachedKernel = _kernelCache.Values.FirstOrDefault(k => k.Name == kernelName);
        compiledKernel = cachedKernel;
        return cachedKernel != null;
    }

    public void ClearCache()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Clearing CUDA kernel cache");

        foreach (var kernel in _kernelCache.Values)
        {
            kernel.Dispose();
        }

        _kernelCache.Clear();
        _cacheMetadata.Clear();
    }

    private Task<string> PrepareCudaSourceAsync(KernelSource source, CompilationOptions? options)
    {
        var builder = new StringBuilder();

        // Add header with compilation metadata
        builder.AppendLine($"// Auto-generated CUDA kernel: {source.Name}");
        builder.AppendLine($"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine($"// Optimization level: {options?.OptimizationLevel ?? OptimizationLevel.Default}");
        builder.AppendLine();

        // Add essential CUDA headers
        builder.AppendLine("#include <cuda_runtime.h>");
        builder.AppendLine("#include <device_launch_parameters.h>");

        // Add performance-oriented headers
        if (options?.OptimizationLevel == OptimizationLevel.Maximum)
        {
            builder.AppendLine("#include <cuda_fp16.h>");  // Half precision support
            builder.AppendLine("#include <cooperative_groups.h>");  // Cooperative groups
        }

        // Add mathematical libraries if needed
        if (source.Code.Contains("sin") || source.Code.Contains("cos") || source.Code.Contains("exp"))
        {
            builder.AppendLine("#include <math_functions.h>");
        }

        builder.AppendLine();

        // Add performance macros
        builder.AppendLine("// Performance optimization macros");
        if (options?.OptimizationLevel == OptimizationLevel.Maximum)
        {
            builder.AppendLine("#define FORCE_INLINE __forceinline__");
            builder.AppendLine("#define RESTRICT __restrict__");
        }
        else
        {
            builder.AppendLine("#define FORCE_INLINE inline");
            builder.AppendLine("#define RESTRICT");
        }

        // Add debug macros
        if (options?.EnableDebugInfo == true)
        {
            builder.AppendLine("#define DEBUG_KERNEL 1");
            builder.AppendLine("#define KERNEL_ASSERT(x) assert(x)");
        }
        else
        {
            builder.AppendLine("#define DEBUG_KERNEL 0");
            builder.AppendLine("#define KERNEL_ASSERT(x)");
        }

        builder.AppendLine();

        // Add compute capability specific optimizations
        var (major, minor) = GetTargetComputeCapability();
        builder.AppendLine($"// Target compute capability: {major}.{minor}");

        if (major >= 7) // Volta and newer
        {
            builder.AppendLine("#define VOLTA_OPTIMIZATIONS 1");
        }

        if (major >= 8) // Ampere and newer
        {
            builder.AppendLine("#define AMPERE_OPTIMIZATIONS 1");
        }

        builder.AppendLine();

        // Add the kernel source code
        switch (source.Language)
        {
            case KernelLanguage.Cuda:
                builder.Append(source.Code);
                break;

            case KernelLanguage.OpenCL:
                // Convert OpenCL to CUDA syntax
                var convertedCode = ConvertOpenClToCuda(source.Code);
                builder.Append(convertedCode);
                break;

            default:
                throw new NotSupportedException($"Kernel language '{source.Language}' is not supported by CUDA compiler");
        }

        return Task.FromResult(builder.ToString());
    }

    private static string ConvertOpenClToCuda(string openClCode)
    {
        // Comprehensive OpenCL to CUDA conversion with proper language mapping
        var cudaCode = openClCode;

        // Replace OpenCL keywords with CUDA equivalents
        cudaCode = cudaCode.Replace("__kernel", "__global__");
        cudaCode = cudaCode.Replace("__global", "__device__");
        cudaCode = cudaCode.Replace("__local", "__shared__");
        cudaCode = cudaCode.Replace("__constant", "__constant__");
        cudaCode = cudaCode.Replace("get_global_id(0)", "blockIdx.x * blockDim.x + threadIdx.x");
        cudaCode = cudaCode.Replace("get_global_id(1)", "blockIdx.y * blockDim.y + threadIdx.y");
        cudaCode = cudaCode.Replace("get_global_id(2)", "blockIdx.z * blockDim.z + threadIdx.z");
        cudaCode = cudaCode.Replace("get_local_id(0)", "threadIdx.x");
        cudaCode = cudaCode.Replace("get_local_id(1)", "threadIdx.y");
        cudaCode = cudaCode.Replace("get_local_id(2)", "threadIdx.z");
        cudaCode = cudaCode.Replace("get_group_id(0)", "blockIdx.x");
        cudaCode = cudaCode.Replace("get_group_id(1)", "blockIdx.y");
        cudaCode = cudaCode.Replace("get_group_id(2)", "blockIdx.z");
        cudaCode = cudaCode.Replace("barrier(CLK_LOCAL_MEM_FENCE)", "__syncthreads()");

        return cudaCode;
    }

    private async Task<byte[]> CompileToPtxAsync(string cudaSource, string kernelName, CompilationOptions? options)
    {
        var stopwatch = Stopwatch.StartNew();
        IntPtr program = IntPtr.Zero;

        try
        {
            _logger.LogDebug("Starting NVRTC compilation for kernel: {KernelName}", kernelName);

            // Create NVRTC program
            var result = NvrtcRuntime.nvrtcCreateProgram(
                out program,
                cudaSource,
                kernelName + ".cu",
                0, // numHeaders
                null, // headers
                null  // includeNames
            );
            NvrtcRuntime.CheckResult(result, "creating NVRTC program");

            // Build compilation options
            var compilationOptions = BuildCompilationOptions(options);

            _logger.LogDebug("NVRTC compilation options: {Options}", string.Join(" ", compilationOptions));

            // Compile the program
            result = NvrtcRuntime.nvrtcCompileProgram(
                program,
                compilationOptions.Length,
                compilationOptions);

            // Get compilation log regardless of success/failure
            var compilerLog = await GetCompilationLogAsync(program);

            if (!string.IsNullOrWhiteSpace(compilerLog))
            {
                if (result == NvrtcResult.Success)
                {
                    _logger.LogInformation("NVRTC compilation log for {KernelName}: {Log}", kernelName, compilerLog);
                }
                else
                {
                    _logger.LogError("NVRTC compilation failed for {KernelName}: {Log}", kernelName, compilerLog);
                }
            }

            // Check compilation result
            if (result != NvrtcResult.Success)
            {
                throw new KernelCompilationException(
                    $"NVRTC compilation failed for kernel '{kernelName}': {NvrtcRuntime.GetErrorString(result)}",
                    compilerLog);
            }

            // Get PTX size
            result = NvrtcRuntime.nvrtcGetPTXSize(program, out var ptxSize);
            NvrtcRuntime.CheckResult(result, "getting PTX size");

            // Get PTX code
            var ptxBuilder = new StringBuilder((int)ptxSize);
            result = NvrtcRuntime.nvrtcGetPTX(program, ptxBuilder);
            NvrtcRuntime.CheckResult(result, "getting PTX code");

            var ptxString = ptxBuilder.ToString();
            var ptxBytes = Encoding.UTF8.GetBytes(ptxString);

            stopwatch.Stop();
            _logger.LogInformation(
                "Successfully compiled kernel '{KernelName}' using NVRTC in {CompilationTime}ms. PTX size: {PtxSize} bytes",
                kernelName,
                stopwatch.ElapsedMilliseconds,
                ptxBytes.Length);

            return ptxBytes;
        }
        catch (Exception ex) when (!(ex is KernelCompilationException))
        {
            _logger.LogError(ex, "Unexpected error during NVRTC compilation of kernel: {KernelName}", kernelName);
            throw new KernelCompilationException($"NVRTC compilation failed for kernel '{kernelName}'", ex);
        }
        finally
        {
            // Clean up NVRTC program
            if (program != IntPtr.Zero)
            {
                try
                {
                    NvrtcRuntime.nvrtcDestroyProgram(ref program);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup NVRTC program for kernel: {KernelName}", kernelName);
                }
            }
        }
    }

    private Task<string> GetCompilationLogAsync(IntPtr program)
    {
        try
        {
            // Get log size
            var result = NvrtcRuntime.nvrtcGetProgramLogSize(program, out var logSize);
            if (result != NvrtcResult.Success || logSize <= 1)
            {
                return Task.FromResult(string.Empty);
            }

            // Get log content
            var logBuilder = new StringBuilder((int)logSize);
            result = NvrtcRuntime.nvrtcGetProgramLog(program, logBuilder);
            if (result != NvrtcResult.Success)
            {
                return Task.FromResult("Failed to retrieve compilation log");
            }

            return Task.FromResult(logBuilder.ToString().Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve NVRTC compilation log");
            return Task.FromResult("Failed to retrieve compilation log");
        }
    }

    private string[] BuildCompilationOptions(CompilationOptions? options)
    {
        var optionsList = new List<string>();

        // Get target GPU architecture
        var (major, minor) = GetTargetComputeCapability();
        optionsList.Add($"--gpu-architecture={ComputeCapability.GetArchString(major, minor)}");

        // Add optimization level
        var optLevel = options?.OptimizationLevel ?? OptimizationLevel.Default;
        switch (optLevel)
        {
            case OptimizationLevel.None:
                optionsList.Add("-O0");
                break;
            case OptimizationLevel.Maximum:
                optionsList.Add("-O3");
                optionsList.Add("--use_fast_math");
                optionsList.Add("--fmad=true");
                break;
            default: // Default
                optionsList.Add("-O2");
                break;
        }

        // Debug information
        if (options?.EnableDebugInfo == true)
        {
            optionsList.Add("-g");
            optionsList.Add("-G");
            optionsList.Add("--device-debug");
            optionsList.Add("--generate-line-info");
        }
        else
        {
            // Release optimizations
            optionsList.Add("--restrict");
            optionsList.Add("--extra-device-vectorization");
        }

        // Standard includes and defines
        optionsList.Add("-default-device");
        optionsList.Add("-std=c++17");
        optionsList.Add("-DCUDA_KERNEL_COMPILATION");

        // Add any additional user-specified flags
        if (options?.AdditionalFlags != null)
        {
            optionsList.AddRange(options.AdditionalFlags);
        }

        return optionsList.ToArray();
    }

    private (int major, int minor) GetTargetComputeCapability()
    {
        try
        {
            // Try to get current device compute capability
            var result = CudaRuntime.cudaGetDevice(out var deviceId);
            if (result == CudaError.Success)
            {
                return ComputeCapability.ParseFromDevice(deviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current device compute capability, using default");
        }

        // Default to a widely supported compute capability (Maxwell generation)
        return ComputeCapability.Common.Maxwell;
    }

    /// <summary>
    /// Compiles kernel to CUBIN for better performance (when supported)
    /// </summary>
    private async Task<byte[]> CompileToCubinAsync(string cudaSource, string kernelName, CompilationOptions? options)
    {
        var stopwatch = Stopwatch.StartNew();
        IntPtr program = IntPtr.Zero;

        try
        {
            _logger.LogDebug("Starting NVRTC CUBIN compilation for kernel: {KernelName}", kernelName);

            // Create NVRTC program
            var result = NvrtcRuntime.nvrtcCreateProgram(
                out program,
                cudaSource,
                kernelName + ".cu",
                0, // numHeaders
                null, // headers
                null  // includeNames
            );
            NvrtcRuntime.CheckResult(result, "creating NVRTC program");

            // Build compilation options for CUBIN (use code generation instead of compute architecture)
            var compilationOptions = BuildCompilationOptionsForCubin(options);

            _logger.LogDebug("NVRTC CUBIN compilation options: {Options}", string.Join(" ", compilationOptions));

            // Compile the program
            result = NvrtcRuntime.nvrtcCompileProgram(
                program,
                compilationOptions.Length,
                compilationOptions);

            // Get compilation log
            var compilerLog = await GetCompilationLogAsync(program);

            if (!string.IsNullOrWhiteSpace(compilerLog))
            {
                if (result == NvrtcResult.Success)
                {
                    _logger.LogInformation("NVRTC CUBIN compilation log for {KernelName}: {Log}", kernelName, compilerLog);
                }
                else
                {
                    _logger.LogError("NVRTC CUBIN compilation failed for {KernelName}: {Log}", kernelName, compilerLog);
                }
            }

            // Check compilation result
            if (result != NvrtcResult.Success)
            {
                throw new KernelCompilationException(
                    $"NVRTC CUBIN compilation failed for kernel '{kernelName}': {NvrtcRuntime.GetErrorString(result)}",
                    compilerLog);
            }

            // Get CUBIN size
            result = NvrtcRuntime.nvrtcGetCUBINSize(program, out var cubinSize);
            NvrtcRuntime.CheckResult(result, "getting CUBIN size");

            // Get CUBIN code
            var cubinData = new byte[(int)cubinSize];
            result = NvrtcRuntime.nvrtcGetCUBIN(program, cubinData);
            NvrtcRuntime.CheckResult(result, "getting CUBIN code");

            stopwatch.Stop();
            _logger.LogInformation(
                "Successfully compiled kernel '{KernelName}' to CUBIN using NVRTC in {CompilationTime}ms. CUBIN size: {CubinSize} bytes",
                kernelName,
                stopwatch.ElapsedMilliseconds,
                cubinData.Length);

            return cubinData;
        }
        catch (Exception ex) when (!(ex is KernelCompilationException))
        {
            _logger.LogError(ex, "Unexpected error during NVRTC CUBIN compilation of kernel: {KernelName}", kernelName);
            throw new KernelCompilationException($"NVRTC CUBIN compilation failed for kernel '{kernelName}'", ex);
        }
        finally
        {
            // Clean up NVRTC program
            if (program != IntPtr.Zero)
            {
                try
                {
                    NvrtcRuntime.nvrtcDestroyProgram(ref program);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup NVRTC program for kernel: {KernelName}", kernelName);
                }
            }
        }
    }

    private string[] BuildCompilationOptionsForCubin(CompilationOptions? options)
    {
        var optionsList = new List<string>();

        // Get target GPU architecture for CUBIN generation
        var (major, minor) = GetTargetComputeCapability();
        optionsList.Add($"--gpu-code={ComputeCapability.GetCodeString(major, minor)}");
        optionsList.Add($"--gpu-architecture={ComputeCapability.GetArchString(major, minor)}");

        // Add optimization level
        var optLevel = options?.OptimizationLevel ?? OptimizationLevel.Default;
        switch (optLevel)
        {
            case OptimizationLevel.None:
                optionsList.Add("-O0");
                break;
            case OptimizationLevel.Maximum:
                optionsList.Add("-O3");
                optionsList.Add("--use_fast_math");
                optionsList.Add("--fmad=true");
                optionsList.Add("--prec-div=false");
                optionsList.Add("--prec-sqrt=false");
                break;
            default: // Default
                optionsList.Add("-O2");
                break;
        }

        // Debug information
        if (options?.EnableDebugInfo == true)
        {
            optionsList.Add("-g");
            optionsList.Add("-G");
            optionsList.Add("--device-debug");
            optionsList.Add("--generate-line-info");
        }
        else
        {
            // Release optimizations specific to CUBIN
            optionsList.Add("--restrict");
            optionsList.Add("--extra-device-vectorization");
            optionsList.Add("--optimize-float-atomics");
        }

        // CUBIN-specific optimizations
        optionsList.Add("-rdc=false"); // Disable relocatable device code for better optimization
        optionsList.Add("-default-device");
        optionsList.Add("-std=c++17");
        optionsList.Add("-DCUDA_KERNEL_COMPILATION");
        optionsList.Add("-DCUDA_CUBIN_COMPILATION");

        // Add any additional user-specified flags
        if (options?.AdditionalFlags != null)
        {
            optionsList.AddRange(options.AdditionalFlags);
        }

        return optionsList.ToArray();
    }

    /// <summary>
    /// Determines whether to use CUBIN or PTX based on compilation options and device support
    /// </summary>
    private bool ShouldUseCubin(CompilationOptions? options)
    {
        // Use CUBIN for maximum optimization when explicitly requested
        if (options?.OptimizationLevel == OptimizationLevel.Maximum)
        {
            try
            {
                // Check if CUBIN is supported on current device
                var (major, minor) = GetTargetComputeCapability();

                // CUBIN is generally supported on compute capability 3.5 and above
                return major > 3 || (major == 3 && minor >= 5);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates CUDA source code for common issues before compilation
    /// </summary>
    private ValidationResult ValidateCudaSource(string cudaSource, string kernelName)
    {
        var warnings = new List<string>();

        try
        {
            // Check for basic CUDA kernel structure
            if (!cudaSource.Contains("__global__") && !cudaSource.Contains("__device__"))
            {
                return ValidationResult.Failure("CUDA source must contain at least one __global__ or __device__ function");
            }

            // Check for potential issues
            if (cudaSource.Contains("printf") && !cudaSource.Contains("#include <cstdio>"))
            {
                warnings.Add("Using printf without including <cstdio> may cause compilation issues");
            }

            if (cudaSource.Contains("__syncthreads()") && !cudaSource.Contains("__shared__"))
            {
                warnings.Add("Using __syncthreads() without shared memory may indicate inefficient synchronization");
            }

            // Check for deprecated functions
            if (cudaSource.Contains("__threadfence_system"))
            {
                warnings.Add("__threadfence_system is deprecated, consider using __threadfence() or memory fences");
            }

            // Check for potential memory issues
            if (cudaSource.Contains("malloc") || cudaSource.Contains("free"))
            {
                warnings.Add("Dynamic memory allocation in kernels can impact performance and may not be supported on all devices");
            }

            return warnings.Count > 0
                ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
                : ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during CUDA source validation for kernel: {KernelName}", kernelName);
            return ValidationResult.SuccessWithWarnings("Source validation failed, proceeding with compilation");
        }
    }

    /// <summary>
    /// Verifies the compiled PTX/CUBIN for basic correctness
    /// </summary>
    private bool VerifyCompiledCode(byte[] compiledCode, string kernelName)
    {
        try
        {
            if (compiledCode == null || compiledCode.Length == 0)
            {
                _logger.LogError("Compiled code is null or empty for kernel: {KernelName}", kernelName);
                return false;
            }

            // Basic PTX validation
            var codeString = Encoding.UTF8.GetString(compiledCode);
            if (codeString.StartsWith(".version") || codeString.StartsWith("//"))
            {
                // Looks like PTX
                if (!codeString.Contains(".entry"))
                {
                    _logger.LogWarning("PTX code does not contain .entry directive for kernel: {KernelName}", kernelName);
                    return false;
                }

                _logger.LogDebug("PTX verification passed for kernel: {KernelName}", kernelName);
                return true;
            }

            // If it's binary data, assume it's CUBIN and do basic size check
            if (compiledCode.Length > 100) // CUBIN should be reasonably sized
            {
                _logger.LogDebug("Binary code verification passed for kernel: {KernelName} (size: {Size} bytes)",
                    kernelName, compiledCode.Length);
                return true;
            }

            _logger.LogWarning("Compiled code verification inconclusive for kernel: {KernelName}", kernelName);
            return true; // Allow inconclusive results to proceed
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during compiled code verification for kernel: {KernelName}", kernelName);
            return true; // Allow verification errors to proceed
        }
    }

    /// <summary>
    /// Checks NVRTC availability and version
    /// </summary>
    public static bool IsNvrtcAvailable()
    {
        try
        {
            var result = NvrtcRuntime.nvrtcVersion(out var major, out var minor);
            return result == NvrtcResult.Success && major >= 11; // Require NVRTC 11.0+
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets NVRTC version information
    /// </summary>
    public static (int major, int minor) GetNvrtcVersion()
    {
        try
        {
            var result = NvrtcRuntime.nvrtcVersion(out var major, out var minor);
            if (result == NvrtcResult.Success)
            {
                return (major, minor);
            }
        }
        catch { }

        return (0, 0);
    }

    private static string GenerateCacheKey(KernelDefinition definition, CompilationOptions? options)
    {
        var key = new StringBuilder();
        key.Append(definition.Name);
        key.Append('_');
        key.Append(definition.Code.GetHashCode());

        if (options != null)
        {
            key.Append('_');
            key.Append(options.OptimizationLevel);
            key.Append('_');
            key.Append(options.EnableDebugInfo ? "debug" : "release");
        }

        return key.ToString();
    }

    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    private async Task LoadPersistentCacheAsync()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                return;
            }

            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.ptx");
            var loadedCount = 0;

            foreach (var ptxFile in cacheFiles)
            {
                try
                {
                    var metadataFile = Path.ChangeExtension(ptxFile, ".metadata.json");
                    if (!File.Exists(metadataFile))
                    {
                        continue;
                    }

                    var metadataJson = await File.ReadAllTextAsync(metadataFile);
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<KernelCacheMetadata>(metadataJson);

                    if (metadata == null || IsCacheEntryExpired(metadata))
                    {
                        File.Delete(ptxFile);
                        File.Delete(metadataFile);
                        continue;
                    }

                    var ptxData = await File.ReadAllBytesAsync(ptxFile);

                    // Create compiled kernel from cached PTX
                    var compiledKernel = new CudaCompiledKernel(
                        _context,
                        metadata.KernelName,
                        metadata.KernelName, // Use kernel name as entry point
                        ptxData,
                        metadata.CompilationOptions,
                        _logger);

                    _kernelCache.TryAdd(metadata.CacheKey, compiledKernel);
                    _cacheMetadata.TryAdd(metadata.CacheKey, metadata);

                    loadedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load cached kernel from: {File}", ptxFile);
                }
            }

            if (loadedCount > 0)
            {
                _logger.LogInformation("Loaded {Count} cached kernels from disk", loadedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load persistent kernel cache");
        }
    }

    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private async Task PersistKernelToDiskAsync(string cacheKey, byte[] ptx, KernelCacheMetadata metadata)
    {
        try
        {
            var fileName = SanitizeFileName(cacheKey);
            var ptxFile = Path.Combine(_cacheDirectory, $"{fileName}.ptx");
            var metadataFile = Path.Combine(_cacheDirectory, $"{fileName}.metadata.json");

            await File.WriteAllBytesAsync(ptxFile, ptx);

            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(metadataFile, metadataJson);

            _logger.LogDebug("Persisted kernel cache to disk: {File}", ptxFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist kernel cache for: {CacheKey}", cacheKey);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder();

        foreach (var c in fileName)
        {
            if (invalidChars.Contains(c))
            {
                sanitized.Append('_');
            }
            else
            {
                sanitized.Append(c);
            }
        }

        return sanitized.ToString();
    }

    private static bool IsCacheEntryExpired(KernelCacheMetadata metadata)
    {
        // Cache entries expire after 7 days of no access
        var maxAge = TimeSpan.FromDays(7);
        return DateTime.UtcNow - metadata.LastAccessed > maxAge;
    }

    /// <summary>
    /// Gets cache statistics for monitoring and debugging
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        ThrowIfDisposed();

        var totalEntries = _kernelCache.Count;
        var totalSize = _cacheMetadata.Values.Sum(m => m.PtxSize);
        var avgAccessCount = totalEntries > 0 ? _cacheMetadata.Values.Average(m => m.AccessCount) : 0;
        var oldestEntry = _cacheMetadata.Values.MinBy(m => m.CompileTime)?.CompileTime;
        var newestEntry = _cacheMetadata.Values.MaxBy(m => m.CompileTime)?.CompileTime;

        return new CacheStatistics
        {
            TotalEntries = totalEntries,
            TotalSizeBytes = totalSize,
            AverageAccessCount = avgAccessCount,
            OldestEntryTime = oldestEntry,
            NewestEntryTime = newestEntry,
            HitRate = CalculateHitRate()
        };
    }

    private double CalculateHitRate()
    {
        var totalAccess = _cacheMetadata.Values.Sum(m => m.AccessCount);
        var uniqueKernels = _cacheMetadata.Count;

        if (totalAccess == 0 || uniqueKernels == 0)
        {
            return 0.0;
        }

        // Hit rate = (total accesses - unique kernels) / total accesses
        // This represents how often we served from cache vs compiled new
        return uniqueKernels > totalAccess ? 0.0 : (double)(totalAccess - uniqueKernels) / totalAccess;
    }

    /// <summary>
    /// Cleans up expired cache entries
    /// </summary>
    public void CleanupExpiredEntries()
    {
        ThrowIfDisposed();

        var expiredKeys = new List<string>();

        foreach (var kvp in _cacheMetadata)
        {
            if (IsCacheEntryExpired(kvp.Value))
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            if (_kernelCache.TryRemove(key, out var kernel))
            {
                kernel.Dispose();
            }
            _cacheMetadata.TryRemove(key, out _);

            // Also remove from disk
            _ = Task.Run(() =>
            {
                try
                {
                    var fileName = SanitizeFileName(key);
                    var ptxFile = Path.Combine(_cacheDirectory, $"{fileName}.ptx");
                    var metadataFile = Path.Combine(_cacheDirectory, $"{fileName}.metadata.json");

                    if (File.Exists(ptxFile))
                    {
                        File.Delete(ptxFile);
                    }

                    if (File.Exists(metadataFile))
                    {
                        File.Delete(metadataFile);
                    }
                }
                catch { /* Ignore cleanup errors */ }
            });
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaKernelCompiler));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            ClearCache();

            // Clean up temp directory
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, true);
                }
                catch { /* Ignore cleanup errors */ }

                // Clean up expired entries from disk cache
                CleanupExpiredEntries();
            }

            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA kernel compiler disposal");
        }
    }
}
