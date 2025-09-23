// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Coordinates the overall CUDA kernel compilation pipeline.
/// Orchestrates source preparation, validation, compilation target selection, and caching.
/// </summary>
internal sealed class CudaCompilationPipeline
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly CudaCompilationCache _cache;
    private readonly string _tempDirectory;

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

    public CudaCompilationPipeline(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotCompute", "Cache", "CUDA");
        _cache = new CudaCompilationCache(cacheDirectory, logger);
        
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DotCompute.CUDA", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Compiles a kernel definition through the complete compilation pipeline.
    /// Handles caching, validation, compilation target selection, and error recovery.
    /// </summary>
    /// <param name="definition">Kernel definition to compile.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compiled CUDA kernel ready for execution.</returns>
    public async Task<CudaCompiledKernel> CompileKernelAsync(
        KernelDefinition definition, 
        CompilationOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Starting compilation pipeline for kernel {KernelName}", definition.Name);

        try
        {
            // Phase 1: Check cache first
            var cacheKey = _cache.GenerateCacheKey(definition, options);
            if (_cache.TryGetCachedKernel(cacheKey, out var cachedKernel, out var metadata))
            {
                _logger.LogDebug("Using cached kernel {KernelName} (access count: {AccessCount})", 
                    definition.Name, metadata?.AccessCount ?? 0);
                return cachedKernel;
            }

            // Phase 2: Prepare source code
            var source = await PrepareKernelSourceAsync(definition, options).ConfigureAwait(false);

            // Phase 3: Validate source code
            var validationResult = CudaCompilerValidator.ValidateCudaSource(source.Code, source.Name, _logger);
            if (!validationResult.IsValid)
            {
                throw new KernelCompilationException($"CUDA source validation failed for '{source.Name}': {validationResult.ErrorMessage}");
            }

            // Log validation warnings
            if (validationResult.Warnings != null)
            {
                foreach (var warning in validationResult.Warnings)
                {
                    _logger.LogWarning("CUDA source warning for {KernelName}: {Warning}", source.Name, warning);
                }
            }

            // Phase 4: Determine compilation target (PTX vs CUBIN)
            var compilationTarget = DetermineCompilationTarget(options);
            _logger.LogDebug("Selected compilation target: {Target} for kernel {KernelName}", 
                compilationTarget, source.Name);

            // Phase 5: Compile kernel
            byte[] compiledCode;
            switch (compilationTarget)
            {
                case CompilationTarget.CUBIN:
                    compiledCode = await CubinCompiler.CompileToCubinAsync(source.Code, source.Name, options, _logger)
                        .ConfigureAwait(false);
                    break;
                
                case CompilationTarget.PTX:
                default:
                    compiledCode = await PTXCompiler.CompileToPtxAsync(source.Code, source.Name, options, _logger)
                        .ConfigureAwait(false);
                    break;
            }

            // Phase 6: Verify compiled code
            if (!CudaCompilerValidator.VerifyCompiledCode(compiledCode, source.Name, _logger))
            {
                _logger.LogWarning("Compiled code verification failed for kernel {KernelName}, proceeding anyway", source.Name);
            }

            // Phase 7: Create compiled kernel object
            var compiledKernel = new CudaCompiledKernel(
                _context,
                source.Name,
                source.EntryPoint,
                compiledCode,
                options,
                _logger);

            // Phase 8: Cache the result
            await _cache.CacheKernelAsync(cacheKey, compiledKernel, definition, options).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Successfully compiled kernel {KernelName} in {ElapsedMs}ms using {Target}", 
                definition.Name, stopwatch.ElapsedMilliseconds, compilationTarget);

            return compiledKernel;
        }
        catch (Exception ex) when (ex is not KernelCompilationException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Compilation pipeline failed for kernel {KernelName} after {ElapsedMs}ms", 
                definition.Name, stopwatch.ElapsedMilliseconds);
            throw new KernelCompilationException($"Compilation pipeline failed for kernel '{definition.Name}'", ex);
        }
    }

    /// <summary>
    /// Compiles multiple kernels in parallel through the compilation pipeline.
    /// Optimizes batch compilation with shared resources and parallel processing.
    /// </summary>
    /// <param name="definitions">Kernel definitions to compile.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of compiled kernels in the same order as input.</returns>
    public async Task<CudaCompiledKernel[]> CompileBatchAsync(
        KernelDefinition[] definitions, 
        CompilationOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        if (definitions.Length == 0)
        {
            return Array.Empty<CudaCompiledKernel>();
        }

        _logger.LogInformation("Starting batch compilation of {KernelCount} kernels", definitions.Length);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Compile kernels in parallel with controlled concurrency
            var maxConcurrency = Math.Min(Environment.ProcessorCount, definitions.Length);
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var compilationTasks = definitions.Select(async definition =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await CompileKernelAsync(definition, options, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            var results = await Task.WhenAll(compilationTasks).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Completed batch compilation of {KernelCount} kernels in {ElapsedMs}ms", 
                definitions.Length, stopwatch.ElapsedMilliseconds);

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Batch compilation failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Prepares CUDA source code from a kernel definition.
    /// Handles source transformation, macro injection, and optimization annotations.
    /// </summary>
    /// <param name="definition">Kernel definition containing source code.</param>
    /// <param name="options">Compilation options affecting source preparation.</param>
    /// <returns>Prepared kernel source ready for compilation.</returns>
    private static async Task<KernelSource> PrepareKernelSourceAsync(KernelDefinition definition, CompilationOptions? options)
    {
        var source = new KernelSource
        {
            Name = definition.Name,
            EntryPoint = definition.EntryPoint ?? "kernel_main",
            Code = definition.Code ?? string.Empty,
            Language = KernelLanguage.Cuda
        };

        // Transform source based on language
        var preparedCode = await PrepareCudaSourceCodeAsync(source, options).ConfigureAwait(false);
        source.Code = preparedCode;

        return source;
    }

    /// <summary>
    /// Prepares CUDA source code with headers, macros, and optimization annotations.
    /// </summary>
    /// <param name="source">Kernel source to prepare.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>Prepared CUDA source code.</returns>
    private static Task<string> PrepareCudaSourceCodeAsync(KernelSource source, CompilationOptions? options)
    {
        var builder = new StringBuilder();

        // Add header with compilation metadata
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Auto-generated CUDA kernel: {source.Name}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Optimization level: {options?.OptimizationLevel ?? OptimizationLevel.Default}");
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
        var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Target compute capability: {major}.{minor}");

        AddComputeCapabilityMacros(builder, major, minor);

        builder.AppendLine();

        // Add the kernel source code with extern "C" handling
        AddKernelSourceCode(builder, source);

        return Task.FromResult(builder.ToString());
    }

    /// <summary>
    /// Adds compute capability specific optimization macros.
    /// </summary>
    /// <param name="builder">StringBuilder to append macros to.</param>
    /// <param name="major">Major compute capability.</param>
    /// <param name="minor">Minor compute capability.</param>
    private static void AddComputeCapabilityMacros(StringBuilder builder, int major, int minor)
    {
        if (major >= 7) // Volta and newer
        {
            builder.AppendLine("#define VOLTA_OPTIMIZATIONS 1");
        }

        if (major >= 8) // Ampere and newer
        {
            builder.AppendLine("#define AMPERE_OPTIMIZATIONS 1");

            if (minor >= 9) // Ada Lovelace (8.9)
            {
                builder.AppendLine("#define ADA_OPTIMIZATIONS 1");
                builder.AppendLine("#define RTX_2000_OPTIMIZATIONS 1");
                builder.AppendLine("#define SHARED_MEM_SIZE_100KB 1");
                builder.AppendLine("#define FP8_TENSOR_CORES 1");
                builder.AppendLine("#define OPTIMAL_BLOCK_SIZE_512 1");
            }
        }
    }

    /// <summary>
    /// Adds kernel source code with proper extern "C" handling.
    /// </summary>
    /// <param name="builder">StringBuilder to append source to.</param>
    /// <param name="source">Kernel source information.</param>
    private static void AddKernelSourceCode(StringBuilder builder, KernelSource source)
    {
        switch (source.Language)
        {
            case KernelLanguage.Cuda:
                // Check if the code already has extern "C"
                if (!source.Code.Contains("extern \"C\""))
                {
                    // Add extern "C" to __global__ functions
                    var modifiedCode = System.Text.RegularExpressions.Regex.Replace(
                        source.Code,
                        @"(\s*)(__global__\s+void\s+)",
                        "$1extern \"C\" $2",
                        System.Text.RegularExpressions.RegexOptions.Multiline);
                    builder.Append(modifiedCode);
                }
                else
                {
                    builder.Append(source.Code);
                }
                break;

            case KernelLanguage.OpenCL:
                // Convert OpenCL to CUDA syntax
                var convertedCode = ConvertOpenClToCuda(source.Code);
                builder.Append(convertedCode);
                break;

            default:
                throw new NotSupportedException($"Kernel language '{source.Language}' is not supported by CUDA compiler");
        }
    }

    /// <summary>
    /// Converts OpenCL kernel code to CUDA syntax.
    /// </summary>
    /// <param name="openClCode">OpenCL source code.</param>
    /// <returns>Converted CUDA source code.</returns>
    private static string ConvertOpenClToCuda(string openClCode)
    {
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

    /// <summary>
    /// Determines the optimal compilation target (PTX vs CUBIN) based on options and device capabilities.
    /// </summary>
    /// <param name="options">Compilation options.</param>
    /// <returns>Selected compilation target.</returns>
    private CompilationTarget DetermineCompilationTarget(CompilationOptions? options)
    {
        try
        {
            // Use the centralized decision logic from CubinCompiler
            return CubinCompiler.ShouldUseCubin(options) ? CompilationTarget.CUBIN : CompilationTarget.PTX;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine optimal compilation target, defaulting to PTX");
            return CompilationTarget.PTX;
        }
    }

    /// <summary>
    /// Gets compilation pipeline statistics and performance metrics.
    /// </summary>
    /// <returns>Pipeline statistics including cache performance and compilation metrics.</returns>
    public Types.CacheStatistics GetPipelineStatistics()
    {
        return _cache.GetCacheStatistics();
    }

    /// <summary>
    /// Clears the compilation cache and cleans up temporary resources.
    /// </summary>
    public void ClearCache()
    {
        _cache.ClearCache();
    }

    /// <summary>
    /// Compilation target enumeration for pipeline decision making.
    /// </summary>
    private enum CompilationTarget
    {
        PTX,
        CUBIN
    }

    /// <summary>
    /// Disposes of pipeline resources including cache and temporary directories.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _cache?.Dispose();
            
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up compilation pipeline resources");
        }
    }
}
