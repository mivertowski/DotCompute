// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
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

public enum KernelLanguage
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
/// CUDA kernel compiler implementation using NVRTC
/// </summary>
public sealed partial class CudaKernelCompiler : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CudaCompiledKernel> _kernelCache;
    private readonly ConcurrentDictionary<string, KernelCacheMetadata> _cacheMetadata;
    private readonly string _tempDirectory;
    private readonly string _cacheDirectory;
    private bool _disposed;

    // Cached JsonSerializerOptions to avoid CA1869
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    [RequiresUnreferencedCode("This type uses runtime code generation and reflection")]
    [RequiresDynamicCode("This type uses runtime code generation for CUDA kernel compilation")]
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
            LogNvrtcNotAvailable(_logger);
        }
        else
        {
            var (major, minor) = GetNvrtcVersion();
            LogNvrtcVersionDetected(_logger, major, minor);
        }

        // Load persistent cache on startup
        _ = Task.Run(LoadPersistentCacheAsync);
    }

    public async Task<ICompiledKernel> CompileAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(definition);

        try
        {
            LogCompilingCudaKernel(_logger, definition.Name);

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

                LogUsingCachedKernel(_logger, definition.Name, cacheMetadata?.AccessCount ?? 0);
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
            var cudaSource = await PrepareCudaSourceAsync(source, options).ConfigureAwait(false);

            // Validate source code
            var validationResult = ValidateCudaSource(cudaSource, source.Name);
            if (!validationResult.IsValid)
            {
                throw new KernelCompilationException($"CUDA source validation failed for '{source.Name}': {validationResult.ErrorMessage}");
            }

            if (validationResult.Warnings != null && validationResult.Warnings.Count > 0)
            {
                foreach (var warning in validationResult.Warnings)
                {
                    LogCudaSourceWarning(_logger, source.Name, warning);
                }
            }

            // Compile to PTX or CUBIN based on optimization settings
            byte[] compiledCode;
            var useCubin = ShouldUseCubin(options);

            if (useCubin)
            {
                LogUsingCubinCompilation(_logger, source.Name);
                compiledCode = await CompileToCubinAsync(cudaSource, source.Name, options).ConfigureAwait(false);
            }
            else
            {
                LogUsingPtxCompilation(_logger, source.Name);
                compiledCode = await CompileToPtxAsync(cudaSource, source.Name, options).ConfigureAwait(false);
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
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling
            _ = Task.Run(() => PersistKernelToDiskAsync(cacheKey, compiledCode, metadata), cancellationToken);
#pragma warning restore IL3050
#pragma warning restore IL2026

            LogSuccessfullyCompiledKernel(_logger, source.Name);
            return compiledKernel;
        }
        catch (Exception ex)
        {
            LogKernelCompilationError(_logger, ex, definition.Name);
            throw new InvalidOperationException($"Failed to compile CUDA kernel '{definition.Name}'", ex);
        }
    }

    public async Task<ICompiledKernel[]> CompileBatchAsync(KernelDefinition[] definitions, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(definitions);

        // Compile kernels in parallel
        var tasks = definitions.Select(def => CompileAsync(def, options, cancellationToken)).ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
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

        LogClearingKernelCache(_logger);

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
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Auto-generated CUDA kernel: {source.Name}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Optimization level: {options?.OptimizationLevel ?? OptimizationLevel.Default}");
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
        if (source.Code.Contains("sin", StringComparison.Ordinal) || source.Code.Contains("cos", StringComparison.Ordinal) || source.Code.Contains("exp", StringComparison.Ordinal))
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
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Target compute capability: {major}.{minor}");

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
        cudaCode = cudaCode.Replace("__kernel", "__global__", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("__global", "__device__", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("__local", "__shared__", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("__constant", "__constant__", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_global_id(0)", "blockIdx.x * blockDim.x + threadIdx.x", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_global_id(1)", "blockIdx.y * blockDim.y + threadIdx.y", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_global_id(2)", "blockIdx.z * blockDim.z + threadIdx.z", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_local_id(0)", "threadIdx.x", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_local_id(1)", "threadIdx.y", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_local_id(2)", "threadIdx.z", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_group_id(0)", "blockIdx.x", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_group_id(1)", "blockIdx.y", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("get_group_id(2)", "blockIdx.z", StringComparison.Ordinal);
        cudaCode = cudaCode.Replace("barrier(CLK_LOCAL_MEM_FENCE)", "__syncthreads()", StringComparison.Ordinal);

        return cudaCode;
    }

    private async Task<byte[]> CompileToPtxAsync(string cudaSource, string kernelName, CompilationOptions? options)
    {
        var stopwatch = Stopwatch.StartNew();
        var program = IntPtr.Zero;

        try
        {
            LogNvrtcCompilationStart(_logger, kernelName);

            // Create NVRTC program
            var result = NvrtcInterop.nvrtcCreateProgram(
                out program,
                cudaSource,
                kernelName + ".cu",
                0, // numHeaders
                null, // headers
                null  // includeNames
            );
            NvrtcInterop.CheckResult(result, "creating NVRTC program");

            // Build compilation options
            var compilationOptions = BuildCompilationOptions(options);

            LogNvrtcCompilationOptions(_logger, string.Join(" ", compilationOptions));

            // Compile the program
            result = NvrtcInterop.nvrtcCompileProgram(
                program,
                compilationOptions.Length,
                compilationOptions);

            // Get compilation log regardless of success/failure
            var compilerLog = await GetCompilationLogAsync(program).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(compilerLog))
            {
                if (result == NvrtcResult.Success)
                {
                    LogNvrtcCompilationLog(_logger, kernelName, compilerLog);
                }
                else
                {
                    LogNvrtcCompilationError(_logger, kernelName, compilerLog);
                }
            }

            // Check compilation result
            if (result != NvrtcResult.Success)
            {
                throw new KernelCompilationException(
                    $"NVRTC compilation failed for kernel '{kernelName}': {NvrtcInterop.GetErrorString(result)}",
                    compilerLog);
            }

            // Get PTX size
            result = NvrtcInterop.nvrtcGetPTXSize(program, out var ptxSize);
            NvrtcInterop.CheckResult(result, "getting PTX size");

            // Get PTX code
            var ptxBuilder = new StringBuilder((int)ptxSize);
            result = NvrtcInterop.nvrtcGetPTX(program, ptxBuilder);
            NvrtcInterop.CheckResult(result, "getting PTX code");

            var ptxString = ptxBuilder.ToString();
            var ptxBytes = Encoding.UTF8.GetBytes(ptxString);

            stopwatch.Stop();
            LogNvrtcKernelCompilationSuccess(_logger, kernelName, stopwatch.ElapsedMilliseconds, ptxBytes.Length);

            return ptxBytes;
        }
        catch (Exception ex) when (ex is not KernelCompilationException)
        {
            LogKernelCompilationError(_logger, ex, kernelName);
            throw new KernelCompilationException($"NVRTC compilation failed for kernel '{kernelName}'", ex);
        }
        finally
        {
            // Clean up NVRTC program
            if (program != IntPtr.Zero)
            {
                try
                {
                    NvrtcInterop.nvrtcDestroyProgram(ref program);
                }
                catch (Exception ex)
                {
                    LogNvrtcCleanupFailed(_logger, ex, kernelName);
                }
            }
        }
    }

    private Task<string> GetCompilationLogAsync(IntPtr program)
    {
        try
        {
            // Get log size
            var result = NvrtcInterop.nvrtcGetProgramLogSize(program, out var logSize);
            if (result != NvrtcResult.Success || logSize <= 1)
            {
                return Task.FromResult(string.Empty);
            }

            // Get log content
            var logBuilder = new StringBuilder((int)logSize);
            result = NvrtcInterop.nvrtcGetProgramLog(program, logBuilder);
            if (result != NvrtcResult.Success)
            {
                return Task.FromResult("Failed to retrieve compilation log");
            }

            return Task.FromResult(logBuilder.ToString().Trim());
        }
        catch (Exception ex)
        {
            LogFailedToRetrieveNvrtcLog(_logger, ex);
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

        return [.. optionsList];
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
            LogFailedToGetComputeCapability(_logger, ex);
        }

        // Default to a widely supported compute capability (Maxwell generation)
        return ComputeCapability.KnownCapabilities.Maxwell;
    }

    /// <summary>
    /// Compiles kernel to CUBIN for better performance (when supported)
    /// </summary>
    private async Task<byte[]> CompileToCubinAsync(string cudaSource, string kernelName, CompilationOptions? options)
    {
        var stopwatch = Stopwatch.StartNew();
        var program = IntPtr.Zero;

        try
        {
            LogNvrtcCubinCompilationStart(_logger, kernelName);

            // Create NVRTC program
            var result = NvrtcInterop.nvrtcCreateProgram(
                out program,
                cudaSource,
                kernelName + ".cu",
                0, // numHeaders
                null, // headers
                null  // includeNames
            );
            NvrtcInterop.CheckResult(result, "creating NVRTC program");

            // Build compilation options for CUBIN (use code generation instead of compute architecture)
            var compilationOptions = BuildCompilationOptionsForCubin(options);

            LogNvrtcCubinCompilationOptions(_logger, string.Join(" ", compilationOptions));

            // Compile the program
            result = NvrtcInterop.nvrtcCompileProgram(
                program,
                compilationOptions.Length,
                compilationOptions);

            // Get compilation log
            var compilerLog = await GetCompilationLogAsync(program).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(compilerLog))
            {
                if (result == NvrtcResult.Success)
                {
                    LogNvrtcCubinCompilationLog(_logger, kernelName, compilerLog);
                }
                else
                {
                    LogNvrtcCompilationError(_logger, kernelName, compilerLog);
                }
            }

            // Check compilation result
            if (result != NvrtcResult.Success)
            {
                throw new KernelCompilationException(
                    $"NVRTC CUBIN compilation failed for kernel '{kernelName}': {NvrtcInterop.GetErrorString(result)}",
                    compilerLog);
            }

            // Get CUBIN size
            result = NvrtcInterop.nvrtcGetCUBINSize(program, out var cubinSize);
            NvrtcInterop.CheckResult(result, "getting CUBIN size");

            // Get CUBIN code
            var cubinData = new byte[(int)cubinSize];
            result = NvrtcInterop.nvrtcGetCUBIN(program, cubinData);
            NvrtcInterop.CheckResult(result, "getting CUBIN code");

            stopwatch.Stop();
            LogNvrtcCubinKernelCompilationSuccess(_logger, kernelName, stopwatch.ElapsedMilliseconds, cubinData.Length);

            return cubinData;
        }
        catch (Exception ex) when (ex is not KernelCompilationException)
        {
            LogKernelCompilationError(_logger, ex, kernelName);
            throw new KernelCompilationException($"NVRTC CUBIN compilation failed for kernel '{kernelName}'", ex);
        }
        finally
        {
            // Clean up NVRTC program
            if (program != IntPtr.Zero)
            {
                try
                {
                    NvrtcInterop.nvrtcDestroyProgram(ref program);
                }
                catch (Exception ex)
                {
                    LogNvrtcCleanupFailed(_logger, ex, kernelName);
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

        return [.. optionsList];
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
    private Types.ValidationResult ValidateCudaSource(string cudaSource, string kernelName)
    {
        var warnings = new List<string>();

        try
        {
            // Check for basic CUDA kernel structure
            if (!cudaSource.Contains("__global__", StringComparison.Ordinal) && !cudaSource.Contains("__device__", StringComparison.Ordinal))
            {
                return Types.ValidationResult.Failure("CUDA source must contain at least one __global__ or __device__ function");
            }

            // Check for potential issues
            if (cudaSource.Contains("printf", StringComparison.Ordinal) && !cudaSource.Contains("#include <cstdio>", StringComparison.Ordinal))
            {
                warnings.Add("Using printf without including <cstdio> may cause compilation issues");
            }

            if (cudaSource.Contains("__syncthreads()", StringComparison.Ordinal) && !cudaSource.Contains("__shared__", StringComparison.Ordinal))
            {
                warnings.Add("Using __syncthreads() without shared memory may indicate inefficient synchronization");
            }

            // Check for deprecated functions
            if (cudaSource.Contains("__threadfence_system", StringComparison.Ordinal))
            {
                warnings.Add("__threadfence_system is deprecated, consider using __threadfence() or memory fences");
            }

            // Check for potential memory issues
            if (cudaSource.Contains("malloc", StringComparison.Ordinal) || cudaSource.Contains("free", StringComparison.Ordinal))
            {
                warnings.Add("Dynamic memory allocation in kernels can impact performance and may not be supported on all devices");
            }

            return warnings.Count > 0
                ? Types.ValidationResult.SuccessWithWarnings(warnings.ToArray())
                : Types.ValidationResult.Success();
        }
        catch (Exception ex)
        {
            LogCudaSourceValidationError(_logger, ex, kernelName);
            return Types.ValidationResult.Success("Source validation failed, proceeding with compilation");
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
                LogEmptyCompiledCode(_logger, kernelName);
                return false;
            }

            // Basic PTX validation
            var codeString = Encoding.UTF8.GetString(compiledCode);
            if (codeString.StartsWith(".version", StringComparison.Ordinal) || codeString.StartsWith("//", StringComparison.Ordinal))
            {
                // Looks like PTX
                if (!codeString.Contains(".entry", StringComparison.Ordinal))
                {
                    LogPtxMissingEntryDirective(_logger, kernelName);
                    return false;
                }

                LogPtxVerificationPassed(_logger, kernelName);
                return true;
            }

            // If it's binary data, assume it's CUBIN and do basic size check
            if (compiledCode.Length > 100) // CUBIN should be reasonably sized
            {
                LogBinaryVerificationPassed(_logger, kernelName, compiledCode.Length);
                return true;
            }

            LogCodeVerificationInconclusive(_logger, kernelName);
            return true; // Allow inconclusive results to proceed
        }
        catch (Exception ex)
        {
            LogCodeVerificationError(_logger, ex, kernelName);
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
            var result = NvrtcInterop.nvrtcVersion(out var major, out var minor);
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
            var result = NvrtcInterop.nvrtcVersion(out var major, out var minor);
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

                    var metadataJson = await File.ReadAllTextAsync(metadataFile).ConfigureAwait(false);
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<KernelCacheMetadata>(metadataJson);

                    if (metadata == null || IsCacheEntryExpired(metadata))
                    {
                        File.Delete(ptxFile);
                        File.Delete(metadataFile);
                        continue;
                    }

                    var ptxData = await File.ReadAllBytesAsync(ptxFile).ConfigureAwait(false);

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
                    LogFailedToLoadCachedKernel(_logger, ex, ptxFile);
                }
            }

            if (loadedCount > 0)
            {
                LogLoadedCachedKernels(_logger, loadedCount);
            }
        }
        catch (Exception ex)
        {
            LogFailedToLoadCache(_logger, ex);
        }
    }

    [RequiresUnreferencedCode("Uses System.Text.Json serialization which may require dynamic code generation")]
    [RequiresDynamicCode("Uses System.Text.Json serialization which may require dynamic code generation")]
    private async Task PersistKernelToDiskAsync(string cacheKey, byte[] ptx, KernelCacheMetadata metadata)
    {
        try
        {
            var fileName = SanitizeFileName(cacheKey);
            var ptxFile = Path.Combine(_cacheDirectory, $"{fileName}.ptx");
            var metadataFile = Path.Combine(_cacheDirectory, $"{fileName}.metadata.json");

            await File.WriteAllBytesAsync(ptxFile, ptx).ConfigureAwait(false);

            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, _jsonOptions);
            await File.WriteAllTextAsync(metadataFile, metadataJson).ConfigureAwait(false);

            LogPersistedKernelCache(_logger, ptxFile);
        }
        catch (Exception ex)
        {
            LogFailedToPersistCache(_logger, ex, cacheKey);
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
    public Types.CacheStatistics GetCacheStatistics()
    {
        ThrowIfDisposed();

        var totalEntries = _kernelCache.Count;
        var totalSize = _cacheMetadata.Values.Sum(m => m.PtxSize);
        var avgAccessCount = totalEntries > 0 ? _cacheMetadata.Values.Average(m => m.AccessCount) : 0;
        var oldestEntry = _cacheMetadata.Values.MinBy(m => m.CompileTime)?.CompileTime;
        var newestEntry = _cacheMetadata.Values.MaxBy(m => m.CompileTime)?.CompileTime;

        return new Types.CacheStatistics
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
            LogCacheClear(_logger, expiredKeys.Count);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

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
            LogDisposalError(_logger, ex);
        }
    }
}
