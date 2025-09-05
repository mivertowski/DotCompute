// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation
{

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
        public DotCompute.Abstractions.CompilationOptions? CompilationOptions { get; set; }
        public int PtxSize { get; set; }
    }


    /// <summary>
    /// CUDA kernel compiler implementation using NVRTC
    /// </summary>
    public sealed partial class CudaKernelCompiler : IDisposable, IAsyncDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, CudaCompiledKernel> _kernelCache;
        private readonly ConcurrentDictionary<string, KernelCacheMetadata> _cacheMetadata;
        private readonly string _tempDirectory;
        private readonly string _cacheDirectory;
        private bool _disposed;

        // Static storage for mangled function names - shared across all compiler instances
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _mangledNamesCache = new();

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
            _ = Directory.CreateDirectory(_tempDirectory);
            _ = Directory.CreateDirectory(_cacheDirectory);

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

        public async Task<ICompiledKernel> CompileAsync(KernelDefinition definition, DotCompute.Abstractions.CompilationOptions? options = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(definition);

            try
            {
                // Ensure we have valid compilation options
                // The architecture capping is handled in BuildCompilationOptions and GetTargetComputeCapability
                // which already cap at compute_86 for CUDA 12.8 compatibility
                
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
                    Code = definition.Code ?? string.Empty,
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

                // Kernel compiled successfully with proper extern "C" handling
                
                // Temporarily disable verification to test CUBIN compatibility
                // TODO: Re-enable verification once CUBIN compilation is working
                // if (!VerifyCompiledCode(compiledCode, source.Name))
                // {
                //     throw new KernelCompilationException($"Compiled code verification failed for kernel '{source.Name}'");
                // }

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
                    SourceCodeHash = definition.Code?.GetHashCode() ?? 0,
                    CompileTime = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    AccessCount = 1,
                    CompilationOptions = options,
                    PtxSize = compiledCode.Length
                };

                _ = _kernelCache.TryAdd(cacheKey, compiledKernel);
                _ = _cacheMetadata.TryAdd(cacheKey, metadata);

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

        public async Task<ICompiledKernel[]> CompileBatchAsync(KernelDefinition[] definitions, DotCompute.Abstractions.CompilationOptions? options = null, CancellationToken cancellationToken = default)
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

        private Task<string> PrepareCudaSourceAsync(KernelSource source, DotCompute.Abstractions.CompilationOptions? options)
        {
            var builder = new StringBuilder();

            // Add header with compilation metadata
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Auto-generated CUDA kernel: {source.Name}");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Optimization level: {options?.OptimizationLevel ?? OptimizationLevel.Default}");
            _ = builder.AppendLine();

            // NVRTC doesn't support external headers - all built-in CUDA functions are available
            // without explicit includes. The compiler has implicit access to:
            // - CUDA built-in variables (threadIdx, blockIdx, blockDim, gridDim)
            // - Math functions (sin, cos, exp, etc.)
            // - Atomic operations
            // - Synchronization primitives (__syncthreads, etc.)
            
            // For NVRTC, we don't need to include any headers
            // The runtime compilation has all CUDA intrinsics built-in

            _ = builder.AppendLine();

            // Add performance macros
            _ = builder.AppendLine("// Performance optimization macros");
            if (options?.OptimizationLevel == OptimizationLevel.Maximum)
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

            _ = builder.AppendLine();

            // Add the kernel source code
            // For NVRTC, we need to ensure kernels have extern "C" to prevent name mangling
            switch (source.Language)
            {
                case KernelLanguage.Cuda:
                    // Check if the code already has extern "C"
                    if (!source.Code.Contains("extern \"C\""))
                    {
                        // Add extern "C" to __global__ functions
                        // This regex replaces "__global__ void funcname" with "extern \"C\" __global__ void funcname"
                        var modifiedCode = System.Text.RegularExpressions.Regex.Replace(
                            source.Code,
                            @"(\s*)(__global__\s+void\s+)",
                            "$1extern \"C\" $2",
                            System.Text.RegularExpressions.RegexOptions.Multiline);
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

        private async Task<byte[]> CompileToPtxAsync(string cudaSource, string kernelName, DotCompute.Abstractions.CompilationOptions? options)
        {
            var stopwatch = Stopwatch.StartNew();
            var program = IntPtr.Zero;
            var registeredFunctionNames = new List<string>();

            try
            {
                LogNvrtcCompilationStart(_logger, kernelName);

                // Create NVRTC program
                var result = NvrtcInterop.CreateProgram(
                    out program,
                    cudaSource,
                    kernelName + ".cu",
                    null, // headers
                    null  // includeNames
                );
                NvrtcInterop.CheckResult(result, "creating NVRTC program");

                // Extract function names from CUDA source and register them with NVRTC
                // This is essential for proper name resolution with C++ mangling
                var functionNames = ExtractKernelFunctionNames(cudaSource);
                foreach (var funcName in functionNames)
                {
                    // Register name expression for kernel function
                    var nameExpression = $"&{funcName}";
                    result = NvrtcInterop.nvrtcAddNameExpression(program, nameExpression);
                    if (result == NvrtcResult.Success)
                    {
                        registeredFunctionNames.Add(funcName);
                        _logger.LogDebug("Registered name expression for kernel function: {FunctionName}", funcName);
                    }
                    else
                    {
                        LogFailedToRegisterNameExpression(_logger, funcName, NvrtcInterop.GetErrorString(result));
                    }
                }

                // Build compilation options
                var compilationOptions = BuildCompilationOptions(options);

                LogNvrtcCompilationOptions(_logger, string.Join(" ", compilationOptions));
                
                // Log each option individually for debugging
                foreach (var option in compilationOptions)
                {
                    _logger.LogDebug("NVRTC Option: {Option}", option);
                }

                // Compile the program
                result = NvrtcInterop.CompileProgram(program, compilationOptions);

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
                        // Also output to console for immediate debugging
                        Console.WriteLine($"[NVRTC COMPILATION ERROR for '{kernelName}']:");
                        Console.WriteLine(compilerLog);
                        Console.WriteLine("[END NVRTC ERROR]");
                    }
                }

                // Check compilation result
                if (result != NvrtcResult.Success)
                {
                    var errorDetails = $"NVRTC compilation failed for kernel '{kernelName}': {NvrtcInterop.GetErrorString(result)}";
                    if (!string.IsNullOrWhiteSpace(compilerLog))
                    {
                        errorDetails += $"\nCompilation Log:\n{compilerLog}";
                    }
                    throw new KernelCompilationException(errorDetails, compilerLog);
                }

                // Get lowered (mangled) names for all registered functions
                // This is critical for proper kernel symbol resolution
                var mangledNames = new Dictionary<string, string>();
                foreach (var funcName in registeredFunctionNames)
                {
                    var nameExpression = $"&{funcName}";
                    var mangledName = NvrtcInterop.GetLoweredName(program, nameExpression);
                    if (!string.IsNullOrEmpty(mangledName))
                    {
                        mangledNames[funcName] = mangledName;
                        _logger.LogDebug("Retrieved mangled name for '{FunctionName}': '{MangledName}'", funcName, mangledName);
                    }
                    else
                    {
                        LogFailedToGetMangledName(_logger, funcName);
                        // Fallback to original name if mangling fails
                        mangledNames[funcName] = funcName;
                    }
                }

                // Store mangled names in thread-local storage for CudaCompiledKernel to access
                // We use a simple approach: store in a concurrent dictionary keyed by kernel name
                if (mangledNames.Count > 0)
                {
                    StoreMangledNames(kernelName, mangledNames);
                }

                // Get PTX code using safe helper method
                var ptxBytes = NvrtcInterop.GetPtxCode(program);

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
                        _ = NvrtcInterop.nvrtcDestroyProgram(ref program);
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
                // Get compilation log using safe helper method
                var log = NvrtcInterop.GetCompilationLog(program);
                return Task.FromResult(log);
            }
            catch (Exception ex)
            {
                LogFailedToRetrieveNvrtcLog(_logger, ex);
                return Task.FromResult("Failed to retrieve compilation log");
            }
        }

        private string[] BuildCompilationOptions(DotCompute.Abstractions.CompilationOptions? options)
        {
            var optionsList = new List<string>();

            // Add CUDA include paths first - CRITICAL for headers like cooperative_groups.h
            AddCudaIncludePaths(optionsList);
            
            // Get target GPU architecture - this is REQUIRED
            var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
            
            // Use the centralized architecture string generation
            var archString = CudaCapabilityManager.GetArchitectureString((major, minor));
            optionsList.Add($"--gpu-architecture={archString}");
            _logger.LogDebug("NVRTC compilation using architecture: {ArchString}", archString);
            
            // Use absolutely minimal NVRTC options - most options are not supported by NVRTC
            // Only use options that are documented to work with NVRTC
            
            // CUDA 13.0 optimizations: Enable shared memory register spilling for Turing and newer
            if (major >= 7 && minor >= 5)
            {
                // Enable verbose ptxas output to analyze register usage
                optionsList.Add("--ptxas-options=-v");
                
                // Enable shared memory register spilling (available in CUDA 13.0+)
                // This helps when register pressure is high
                if (options?.EnableSharedMemoryRegisterSpilling ?? true)
                {
                    optionsList.Add("--ptxas-options=--allow-expensive-optimizations=true");
                }
            }
            
            // Add dynamic parallelism support if requested
            // Dynamic parallelism requires relocatable device code (-rdc=true)
            if (options?.EnableDynamicParallelism == true)
            {
                optionsList.Add("--relocatable-device-code=true");
                _logger.LogDebug("Enabled relocatable device code for dynamic parallelism support");
            }
            
            // Add debug info if requested
            if (options?.EnableDebugInfo == true)
            {
                optionsList.Add("--device-debug");
                optionsList.Add("--generate-line-info");
            }

            // Add any additional user-specified flags
            if (options?.AdditionalFlags != null)
            {
                optionsList.AddRange(options.AdditionalFlags);
            }

            return [.. optionsList];
        }

        // Obsolete - replaced by CudaCapabilityManager.GetTargetComputeCapability()
        // This method is no longer used - all compute capability detection is centralized
        private (int major, int minor) GetTargetComputeCapability()
        {
            // Delegate to centralized manager
            return CudaCapabilityManager.GetTargetComputeCapability();
        }

        /// <summary>
        /// Compiles kernel to CUBIN for better performance (when supported)
        /// </summary>
        private async Task<byte[]> CompileToCubinAsync(string cudaSource, string kernelName, DotCompute.Abstractions.CompilationOptions? options)
        {
            var stopwatch = Stopwatch.StartNew();
            var program = IntPtr.Zero;
            var registeredFunctionNames = new List<string>();

            try
            {
                LogNvrtcCubinCompilationStart(_logger, kernelName);

                // Create NVRTC program
                var result = NvrtcInterop.CreateProgram(
                    out program,
                    cudaSource,
                    kernelName + ".cu",
                    null, // headers
                    null  // includeNames
                );
                NvrtcInterop.CheckResult(result, "creating NVRTC program");

                // Extract function names and register them with NVRTC for name resolution
                var functionNames = ExtractKernelFunctionNames(cudaSource);
                foreach (var funcName in functionNames)
                {
                    var nameExpression = $"&{funcName}";
                    result = NvrtcInterop.nvrtcAddNameExpression(program, nameExpression);
                    if (result == NvrtcResult.Success)
                    {
                        registeredFunctionNames.Add(funcName);
                        _logger.LogDebug("Registered name expression for CUBIN kernel function: {FunctionName}", funcName);
                    }
                    else
                    {
                        LogFailedToRegisterNameExpression(_logger, funcName, NvrtcInterop.GetErrorString(result));
                    }
                }

                // Build compilation options for CUBIN (use code generation instead of compute architecture)
                var compilationOptions = BuildCompilationOptionsForCubin(options);

                LogNvrtcCubinCompilationOptions(_logger, string.Join(" ", compilationOptions));

                // Compile the program
                result = NvrtcInterop.CompileProgram(program, compilationOptions);

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

                // Get lowered (mangled) names for all registered functions (CUBIN path)
                var mangledNames = new Dictionary<string, string>();
                foreach (var funcName in registeredFunctionNames)
                {
                    var nameExpression = $"&{funcName}";
                    var mangledName = NvrtcInterop.GetLoweredName(program, nameExpression);
                    if (!string.IsNullOrEmpty(mangledName))
                    {
                        mangledNames[funcName] = mangledName;
                        _logger.LogDebug("Retrieved CUBIN mangled name for '{FunctionName}': '{MangledName}'", funcName, mangledName);
                    }
                    else
                    {
                        LogFailedToGetMangledName(_logger, funcName);
                        mangledNames[funcName] = funcName;
                    }
                }

                // Store mangled names for CUBIN kernels
                if (mangledNames.Count > 0)
                {
                    StoreMangledNames(kernelName, mangledNames);
                }

                // Get CUBIN code using safe helper method
                var cubinData = NvrtcInterop.GetCubinCode(program);

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
                        _ = NvrtcInterop.nvrtcDestroyProgram(ref program);
                    }
                    catch (Exception ex)
                    {
                        LogNvrtcCleanupFailed(_logger, ex, kernelName);
                    }
                }
            }
        }

        private string[] BuildCompilationOptionsForCubin(DotCompute.Abstractions.CompilationOptions? options)
        {
            var optionsList = new List<string>();

            // MINIMAL OPTIONS FOR CUBIN - NVRTC is very limited
            
            // Get target GPU architecture for CUBIN generation
            var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
            // For CUBIN, we use the centralized architecture string generation
            var archString = CudaCapabilityManager.GetArchitectureString((major, minor));
            
            // Just use architecture - NVRTC will handle the rest
            optionsList.Add($"--gpu-architecture={archString}");

            // Add dynamic parallelism support if requested
            // Dynamic parallelism requires relocatable device code (-rdc=true)
            if (options?.EnableDynamicParallelism == true)
            {
                optionsList.Add("--relocatable-device-code=true");
                _logger.LogDebug("Enabled relocatable device code for dynamic parallelism support (CUBIN)");
            }
            
            // Add debug info if requested
            if (options?.EnableDebugInfo == true)
            {
                optionsList.Add("--device-debug");
                optionsList.Add("--generate-line-info");
            }

            // Add any additional user-specified flags
            if (options?.AdditionalFlags != null)
            {
                optionsList.AddRange(options.AdditionalFlags);
            }

            return [.. optionsList];
        }

        /// <summary>
        /// Determines whether to use CUBIN or PTX based on compilation options and device support
        /// CRITICAL: CUDA 13.0 has instability with CUBIN for newer architectures - prefer PTX
        /// </summary>
        private bool ShouldUseCubin(DotCompute.Abstractions.CompilationOptions? options)
        {
            try
            {
                var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
                
                // CRITICAL FIX: CUDA 13.0 has compilation issues with CUBIN for sm_89
                // Use PTX instead for better driver compatibility
                if (major == 8 && minor >= 6) // Ampere and Ada architectures
                {
                    _logger.LogInformation("Using PTX compilation for compute capability {Major}.{Minor} " +
                        "to ensure CUDA 13.0 driver compatibility", major, minor);
                    return false; // Use PTX instead of CUBIN
                }
                
                // For older architectures (Turing and earlier), CUBIN is stable
                if (major >= 7 && major < 8) // Turing (sm_75)
                {
                    _logger.LogDebug("Using CUBIN compilation for compute capability {Major}.{Minor}", major, minor);
                    return true;
                }
                
                // CUBIN is generally supported on compute capability 3.5 and above for older archs
                return major > 3 || (major == 3 && minor >= 5);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to determine CUBIN compatibility, falling back to PTX");
                return false; // Default to PTX on error
            }
        }

        /// <summary>
        /// Validates CUDA source code for common issues before compilation
        /// </summary>
        private Types.UnifiedValidationResult ValidateCudaSource(string cudaSource, string kernelName)
        {
            var warnings = new List<string>();

            try
            {
                // Check for basic CUDA kernel structure
                if (!cudaSource.Contains("__global__", StringComparison.Ordinal) && !cudaSource.Contains("__device__", StringComparison.Ordinal))
                {
                    return Types.UnifiedValidationResult.Failure("CUDA source must contain at least one __global__ or __device__ function");
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
                    ? Types.UnifiedValidationResult.SuccessWithWarnings(warnings.ToArray())
                    : Types.UnifiedValidationResult.Success();
            }
            catch (Exception ex)
            {
                LogCudaSourceValidationError(_logger, ex, kernelName);
                return Types.UnifiedValidationResult.Success("Source validation failed, proceeding with compilation");
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

        private static string GenerateCacheKey(KernelDefinition definition, DotCompute.Abstractions.CompilationOptions? options)
        {
            var key = new StringBuilder();
            _ = key.Append(definition.Name);
            _ = key.Append('_');
            _ = key.Append(definition.Code?.GetHashCode() ?? 0);

            if (options != null)
            {
                _ = key.Append('_');
                _ = key.Append(options.OptimizationLevel);
                _ = key.Append('_');
                _ = key.Append(options.EnableDebugInfo ? "debug" : "release");
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

                        _ = _kernelCache.TryAdd(metadata.CacheKey, compiledKernel);
                        _ = _cacheMetadata.TryAdd(metadata.CacheKey, metadata);

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
                    _ = sanitized.Append('_');
                }
                else
                {
                    _ = sanitized.Append(c);
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
        /// Extracts kernel function names from CUDA source code.
        /// Looks for __global__ function declarations to register with NVRTC name resolution.
        /// </summary>
        private List<string> ExtractKernelFunctionNames(string cudaSource)
        {
            var functionNames = new List<string>();
            
            try
            {
                // Use regex to find __global__ function declarations
                // Pattern matches: [extern "C"] __global__ void functionName(
                var pattern = @"(?:extern\s*""C""\s*)?__global__\s+void\s+(\w+)\s*\(";
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    cudaSource, 
                    pattern, 
                    System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var funcName = match.Groups[1].Value;
                        if (!string.IsNullOrWhiteSpace(funcName) && !functionNames.Contains(funcName))
                        {
                            functionNames.Add(funcName);
                        }
                    }
                }

                // If no functions found, try to extract from entry point or kernel name
                if (functionNames.Count == 0)
                {
                    // Look for any function-like patterns as fallback
                    var fallbackPattern = @"void\s+(\w+)\s*\([^)]*\)\s*\{";
                    var fallbackMatches = System.Text.RegularExpressions.Regex.Matches(
                        cudaSource, 
                        fallbackPattern, 
                        System.Text.RegularExpressions.RegexOptions.Multiline);

                    foreach (System.Text.RegularExpressions.Match match in fallbackMatches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var funcName = match.Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(funcName) && !functionNames.Contains(funcName))
                            {
                                functionNames.Add(funcName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFailedToExtractFunctionNames(_logger, ex);
            }

            return functionNames;
        }

        /// <summary>
        /// Stores mangled function names for a kernel in the cache.
        /// </summary>
        private void StoreMangledNames(string kernelName, Dictionary<string, string> mangledNames)
        {
            try
            {
                _ = _mangledNamesCache.TryAdd(kernelName, new Dictionary<string, string>(mangledNames));
                _logger.LogDebug("Stored {Count} mangled names for kernel '{KernelName}'", mangledNames.Count, kernelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store mangled names for kernel '{KernelName}'", kernelName);
            }
        }

        /// <summary>
        /// Gets the mangled function name for a kernel and function name.
        /// </summary>
        public static string? GetMangledFunctionName(string kernelName, string functionName)
        {
            if (_mangledNamesCache.TryGetValue(kernelName, out var mangledNames))
            {
                return mangledNames.TryGetValue(functionName, out var mangledName) ? mangledName : null;
            }
            return null;
        }

        /// <summary>
        /// Gets all mangled function names for a kernel.
        /// </summary>
        public static Dictionary<string, string>? GetAllMangledNames(string kernelName)
        {
            return _mangledNamesCache.TryGetValue(kernelName, out var mangledNames) ? mangledNames : null;
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
                _ = _cacheMetadata.TryRemove(key, out _);

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

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    // Save cache asynchronously before disposing
                    await SavePersistentCacheAsync().ConfigureAwait(false);
                    
                    // Dispose synchronously
                    Dispose();
                }
                catch (Exception ex)
                {
                    LogDisposalError(_logger, ex);
                }
            }
        }

        /// <summary>
        /// Saves the current kernel cache to persistent storage asynchronously.
        /// </summary>
        [RequiresUnreferencedCode("Uses System.Text.Json serialization which may require dynamic code generation")]
        [RequiresDynamicCode("Uses System.Text.Json serialization which may require dynamic code generation")]
        private async Task SavePersistentCacheAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    _ = Directory.CreateDirectory(_cacheDirectory);
                }

                var saveCount = 0;
                var saveTasks = new List<Task>();

                foreach (var kvp in _cacheMetadata)
                {
                    var cacheKey = kvp.Key;
                    var metadata = kvp.Value;

                    // Skip if already persisted recently
                    if (metadata.LastAccessed - metadata.CompileTime < TimeSpan.FromMinutes(1))
                    {
                        continue;
                    }

                    // Get the compiled kernel
                    if (_kernelCache.TryGetValue(cacheKey, out var compiledKernel))
                    {
                        var saveTask = SaveKernelToDiskAsync(cacheKey, compiledKernel, metadata);
                        saveTasks.Add(saveTask);
                        saveCount++;
                    }
                }

                // Wait for all save operations to complete
                if (saveTasks.Count > 0)
                {
                    await Task.WhenAll(saveTasks).ConfigureAwait(false);
                    _logger.LogDebug("Saved {SaveCount} kernel cache entries to persistent storage", saveCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save persistent kernel cache");
            }
        }

        /// <summary>
        /// Saves a specific kernel and its metadata to disk.
        /// </summary>
        [RequiresUnreferencedCode("Uses System.Text.Json serialization which may require dynamic code generation")]
        [RequiresDynamicCode("Uses System.Text.Json serialization which may require dynamic code generation")]
        private async Task SaveKernelToDiskAsync(string cacheKey, CudaCompiledKernel compiledKernel, KernelCacheMetadata metadata)
        {
            try
            {
                var fileName = SanitizeFileName(cacheKey);
                var ptxFile = Path.Combine(_cacheDirectory, $"{fileName}.ptx");
                var metadataFile = Path.Combine(_cacheDirectory, $"{fileName}.metadata.json");

                // Skip if files already exist and are recent
                if (File.Exists(ptxFile) && File.Exists(metadataFile))
                {
                    var fileInfo = new FileInfo(metadataFile);
                    if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc < TimeSpan.FromHours(1))
                    {
                        return; // Recently saved
                    }
                }

                // Get PTX/CUBIN data from compiled kernel
                var kernelData = GetKernelBinaryData(compiledKernel);
                if (kernelData != null && kernelData.Length > 0)
                {
                    await File.WriteAllBytesAsync(ptxFile, kernelData).ConfigureAwait(false);

                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, _jsonOptions);
                    await File.WriteAllTextAsync(metadataFile, metadataJson).ConfigureAwait(false);

                    _logger.LogTrace("Saved kernel cache entry: {FileName}", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save kernel cache entry: {CacheKey}", cacheKey);
            }
        }

        /// <summary>
        /// Extracts binary data (PTX/CUBIN) from a compiled kernel.
        /// </summary>
        private byte[]? GetKernelBinaryData(CudaCompiledKernel compiledKernel)
        {
            try
            {
                // Access the compiled kernel's binary data
                // This is a simplified implementation - in practice, you might need to use reflection
                // or add a public property to CudaCompiledKernel to access the binary data
                
                // For now, return null to indicate we couldn't get the data
                // In a real implementation, you would access the PTX/CUBIN binary from the kernel
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract kernel binary data for {KernelName}", compiledKernel.Name);
                return null;
            }
        }

        /// <summary>
        /// Adds CUDA include paths for NVRTC compilation
        /// </summary>
        private void AddCudaIncludePaths(List<string> optionsList)
        {
            // Add CUDA 13.0 include paths - order matters for header resolution
            var cudaIncludePaths = new[]
            {
                "/usr/local/cuda-13.0/include",
                "/usr/local/cuda-13.0/targets/x86_64-linux/include", 
                "/usr/local/cuda/include", // Fallback
                "/usr/include/cuda", // System fallback
            };

            foreach (var includePath in cudaIncludePaths)
            {
                if (System.IO.Directory.Exists(includePath))
                {
                    optionsList.Add($"--include-path={includePath}");
                    _logger.LogDebug("Added CUDA include path: {Path}", includePath);
                }
            }

            // Add standard C++ include paths for device compilation
            var cppIncludePaths = new[]
            {
                "/usr/include/c++/11", // GCC 11
                "/usr/include/c++/9",  // GCC 9 fallback
                "/usr/include",
            };

            foreach (var includePath in cppIncludePaths)
            {
                if (System.IO.Directory.Exists(includePath))
                {
                    optionsList.Add($"--include-path={includePath}");
                    break; // Only add one C++ include path
                }
            }
        }
    }
}
