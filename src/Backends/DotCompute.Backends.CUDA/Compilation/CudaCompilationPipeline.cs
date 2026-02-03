// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Timing;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Coordinates the overall CUDA kernel compilation pipeline.
/// Orchestrates source preparation, validation, compilation target selection, and caching.
/// </summary>
internal sealed partial class CudaCompilationPipeline : IDisposable
{
    [GeneratedRegex(@"(\s*)(__global__\s+void\s+)", RegexOptions.Multiline)]
    private static partial Regex GlobalFunctionPattern();
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly CudaCompilationCache _cache;
    private readonly string _tempDirectory;
    private CudaTimingProvider? _timingProvider;
    private IFenceInjectionService? _fenceInjectionService;

    // Internal types for kernel compilation
    internal sealed class KernelSource
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public required string Name { get; set; }
        /// <summary>
        /// Gets or sets the entry point.
        /// </summary>
        /// <value>The entry point.</value>
        public required string EntryPoint { get; set; }
        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        /// <value>The code.</value>
        public required string Code { get; set; }
        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        /// <value>The language.</value>
        public KernelLanguage Language { get; set; }
    }
    /// <summary>
    /// An kernel language enumeration.
    /// </summary>

    public enum KernelLanguage
    {
        Cuda,
        OpenCL,
        Ptx
    }
    /// <summary>
    /// Initializes a new instance of the CudaCompilationPipeline class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

    public CudaCompilationPipeline(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));


        var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotCompute", "Cache", "CUDA");
        _cache = new CudaCompilationCache(cacheDirectory, logger);


        _tempDirectory = Path.Combine(Path.GetTempPath(), "DotCompute.CUDA", Guid.NewGuid().ToString());
        _ = Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Sets the timing provider for timestamp injection support.
    /// </summary>
    /// <param name="timingProvider">The timing provider instance, or null to disable injection.</param>
    /// <remarks>
    /// When a timing provider is set and timestamp injection is enabled via
    /// <see cref="CudaTimingProvider.EnableTimestampInjection"/>, kernels will
    /// automatically have timestamp recording code injected at their entry point.
    /// </remarks>
    public void SetTimingProvider(CudaTimingProvider? timingProvider)
    {
        _timingProvider = timingProvider;
    }

    /// <summary>
    /// Sets the fence injection service for memory ordering support.
    /// </summary>
    /// <param name="fenceService">The fence injection service instance, or null to disable fence injection.</param>
    /// <remarks>
    /// <para>
    /// When a fence injection service is set and has pending fence requests,
    /// kernels will automatically have memory fence instructions injected at
    /// the specified locations (entry, exit, after writes, before reads).
    /// </para>
    /// <para>
    /// The fence requests are cleared after successful injection to prevent
    /// duplicate injection in subsequent compilations.
    /// </para>
    /// <para>
    /// <strong>PTX Fence Instructions:</strong>
    /// <list type="bullet">
    /// <item><description><c>bar.sync 0;</c> - Thread-block scope</description></item>
    /// <item><description><c>membar.gl;</c> - Device/global scope</description></item>
    /// <item><description><c>membar.sys;</c> - System-wide scope</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void SetFenceInjectionService(IFenceInjectionService? fenceService)
    {
        _fenceInjectionService = fenceService;
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
        LogPipelineStart(_logger, definition.Name);

        try
        {
            // Phase 1: Check cache first
            var cacheKey = CudaCompilationCache.GenerateCacheKey(definition, options);
            if (_cache.TryGetCachedKernel(cacheKey, out var cachedKernel, out var metadata))
            {
                LogCachedKernel(_logger, definition.Name, metadata?.AccessCount ?? 0);
                return cachedKernel!; // Non-null when TryGetCachedKernel returns true
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
                    LogSourceWarning(_logger, source.Name, warning);
                }
            }

            // Phase 4: Determine compilation target (PTX vs CUBIN)
            var compilationTarget = DetermineCompilationTarget(options);
            LogCompilationTarget(_logger, compilationTarget, source.Name);

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

            // Phase 5.5: Inject timestamp recording if enabled
            if (_timingProvider?.IsTimestampInjectionEnabled == true && compilationTarget == CompilationTarget.PTX)
            {
                compiledCode = TimestampInjector.InjectTimestampIntoPtx(compiledCode, source.Name, _logger);
            }

            // Phase 5.6: Inject memory fences if service is configured with pending requests
            if (_fenceInjectionService?.PendingFenceCount > 0 && compilationTarget == CompilationTarget.PTX)
            {
                compiledCode = FenceInjector.InjectFencesIntoPtx(compiledCode, source.Name, _fenceInjectionService, _logger);
            }

            // Phase 6: Verify compiled code
            if (!CudaCompilerValidator.VerifyCompiledCode(compiledCode, source.Name, _logger))
            {
                LogVerificationFailed(_logger, source.Name);
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
            LogCompilationSuccess(_logger, definition.Name, stopwatch.ElapsedMilliseconds, compilationTarget);

            return compiledKernel;
        }
        catch (Exception ex) when (ex is not KernelCompilationException)
        {
            stopwatch.Stop();
            LogCompilationFailure(_logger, ex, definition.Name, stopwatch.ElapsedMilliseconds);
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
            return [];
        }

        LogBatchStart(_logger, definitions.Length);
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
                    _ = semaphore.Release();
                }
            }).ToArray();

            var results = await Task.WhenAll(compilationTasks).ConfigureAwait(false);

            stopwatch.Stop();
            LogBatchSuccess(_logger, definitions.Length, stopwatch.ElapsedMilliseconds);

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogBatchFailure(_logger, ex, stopwatch.ElapsedMilliseconds);
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
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Auto-generated CUDA kernel: {source.Name}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Optimization level: {options?.OptimizationLevel ?? OptimizationLevel.Default}");
        _ = builder.AppendLine();

        // Add performance macros
        _ = builder.AppendLine("// Performance optimization macros");
        if (options?.OptimizationLevel == OptimizationLevel.O3)
        {
            _ = builder.AppendLine("#define FORCE_INLINE __forceinline__");
            _ = builder.AppendLine("#define RESTRICT __restrict__");
        }
        else
        {
            _ = builder.AppendLine("#define FORCE_INLINE inline");
            _ = builder.AppendLine("#define RESTRICT");
        }

        // Add debug macros
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

        // Add compute capability specific optimizations
        var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Target compute capability: {major}.{minor}");

        AddComputeCapabilityMacros(builder, major, minor);

        _ = builder.AppendLine();

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
            _ = builder.AppendLine("#define VOLTA_OPTIMIZATIONS 1");
        }

        if (major >= 8) // Ampere and newer
        {
            _ = builder.AppendLine("#define AMPERE_OPTIMIZATIONS 1");

            if (minor >= 9) // Ada Lovelace (8.9)
            {
                _ = builder.AppendLine("#define ADA_OPTIMIZATIONS 1");
                _ = builder.AppendLine("#define RTX_2000_OPTIMIZATIONS 1");
                _ = builder.AppendLine("#define SHARED_MEM_SIZE_100KB 1");
                _ = builder.AppendLine("#define FP8_TENSOR_CORES 1");
                _ = builder.AppendLine("#define OPTIMAL_BLOCK_SIZE_512 1");
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
                if (!source.Code.Contains("extern \"C\"", StringComparison.OrdinalIgnoreCase))
                {
                    // Add extern "C" to __global__ functions
                    var modifiedCode = GlobalFunctionPattern().Replace(
                        source.Code,
                        "$1extern \"C\" $2");
                    _ = builder.Append(modifiedCode);
                }
                else
                {
                    _ = builder.Append(source.Code);
                }
                break;

            case KernelLanguage.OpenCL:
                // Convert OpenCL to CUDA syntax
                var convertedCode = ConvertOpenClToCuda(source.Code);
                _ = builder.Append(convertedCode);
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
            LogTargetSelectionFailed(_logger, ex);
            return CompilationTarget.PTX;
        }
    }

    /// <summary>
    /// Gets compilation pipeline statistics and performance metrics.
    /// </summary>
    /// <returns>Pipeline statistics including cache performance and compilation metrics.</returns>
    public CacheStatistics GetPipelineStatistics() => _cache.GetCacheStatistics();

    /// <summary>
    /// Clears the compilation cache and cleans up temporary resources.
    /// </summary>
    public void ClearCache() => _cache.ClearCache();
    /// <summary>
    /// An compilation target enumeration.
    /// </summary>

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
            LogCleanupFailed(_logger, ex);
        }
    }
}
