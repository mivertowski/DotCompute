// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using global::System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Types;
using ValidationResult = DotCompute.Abstractions.Validation.UnifiedValidationResult;

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Interfaces.Kernels;
using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;
using KernelLanguage = DotCompute.Abstractions.Kernels.KernelLanguage;
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Metal backend has dynamic logging requirements

namespace DotCompute.Backends.Metal.Kernels;


/// <summary>
/// Compiles kernels to Metal Shading Language and creates compute pipeline states.
/// </summary>
public sealed class MetalKernelCompiler : IUnifiedKernelCompiler, IDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly ILogger _logger;
    private readonly MetalCommandBufferPool? _commandBufferPool;
    private readonly MetalKernelCache _kernelCache;
    private readonly SemaphoreSlim _compilationSemaphore = new(1, 1);
    private int _disposed;
    
    public MetalKernelCompiler(
        IntPtr device, 
        IntPtr commandQueue, 
        ILogger logger, 
        MetalCommandBufferPool? commandBufferPool = null,
        MetalKernelCache? kernelCache = null)
    {
        _device = device;
        _commandQueue = commandQueue;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commandBufferPool = commandBufferPool;
        
        // Create or use provided kernel cache
        if (kernelCache != null)
        {
            _kernelCache = kernelCache;
        }
        else
        {
            var cacheLogger = logger is ILogger<MetalKernelCache> cacheTypedLogger 
                ? cacheTypedLogger 
                : new LoggerFactory().CreateLogger<MetalKernelCache>();
                
            _kernelCache = new MetalKernelCache(
                cacheLogger,
                maxCacheSize: 500,
                defaultTtl: TimeSpan.FromHours(2),
                persistentCachePath: Path.Combine(Path.GetTempPath(), "DotCompute", "MetalCache"));
        }
    }

    /// <inheritdoc/>
    public string Name => "Metal Shader Compiler";



    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays - Required by IUnifiedKernelCompiler interface
    public IReadOnlyList<KernelLanguage> SupportedSourceTypes { get; } = new List<KernelLanguage>
    {
        KernelLanguage.Metal,
        KernelLanguage.Binary
    }.AsReadOnly();
#pragma warning restore CA1819

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Capabilities { get; } = new Dictionary<string, object>
    {
        ["SupportsAsync"] = true,
        ["SupportsOptimization"] = true,
        ["SupportsCaching"] = true,
        ["SupportsValidation"] = true,
        ["SupportedLanguageVersions"] = new[] { "Metal 2.0", "Metal 2.1", "Metal 2.2", "Metal 2.3", "Metal 2.4" }
    }.AsReadOnly();

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        var validation = Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Kernel validation failed: {validation.ErrorMessage}");
        }

        // Check the cache first
        if (_kernelCache.TryGetKernel(definition, options, out var cachedLibrary, out var cachedFunction, out var cachedPipelineState))
        {
            _logger.LogDebug("Cache hit for kernel '{Name}' - using cached pipeline state", definition.Name);
            
            // Get metadata from cached pipeline state
            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(cachedPipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidthTuple(cachedPipelineState);
            
            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = 0, // No compilation time for cached kernel
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { "Kernel loaded from cache" }
            };
            
            return new MetalCompiledKernel(
                definition,
                cachedPipelineState,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger,
                _commandBufferPool);
        }

        await _compilationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Generate or extract Metal code
            var metalCode = ExtractMetalCode(definition);

            // Compile Metal code
            _logger.LogDebug("Compiling kernel '{Name}' from source", definition.Name);
            var library = await CompileMetalCodeAsync(metalCode, definition.Name, options, cancellationToken).ConfigureAwait(false);
            
            // Create function and pipeline state
            var function = MetalNative.GetFunction(library, definition.EntryPoint ?? definition.Name);
            if (function == IntPtr.Zero)
            {
                MetalNative.ReleaseLibrary(library);
                throw new InvalidOperationException($"Failed to get function '{definition.EntryPoint ?? definition.Name}' from Metal library");
            }
            
            var pipelineState = MetalNative.CreateComputePipelineState(_device, function);
            if (pipelineState == IntPtr.Zero)
            {
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                throw new InvalidOperationException($"Failed to create compute pipeline state for kernel '{definition.Name}'");
            }
            
            stopwatch.Stop();
            var compilationTimeMs = (long)stopwatch.Elapsed.TotalMilliseconds;
            
            // Add to cache
            byte[]? binaryData = null;
            try
            {
                // Try to get binary data for persistent caching
                var dataSize = MetalNative.GetLibraryDataSize(library);
                if (dataSize > 0)
                {
                    binaryData = new byte[dataSize];
                    var handle = GCHandle.Alloc(binaryData, GCHandleType.Pinned);
                    try
                    {
                        MetalNative.GetLibraryData(library, handle.AddrOfPinnedObject(), dataSize);
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract binary data for kernel '{Name}'", definition.Name);
            }
            
            _kernelCache.AddKernel(definition, options, library, function, pipelineState, binaryData, compilationTimeMs);
            _logger.LogInformation("Compiled and cached kernel '{Name}' in {Time}ms", definition.Name, compilationTimeMs);
            
            // Get metadata
            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(pipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidthTuple(pipelineState);
            
            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = compilationTimeMs,
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { $"Max threads per threadgroup: {maxThreadsPerThreadgroup}" }
            };
            
            // Release function reference (pipeline state retains it)
            MetalNative.ReleaseFunction(function);
            
            return new MetalCompiledKernel(
                definition,
                pipelineState,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger,
                _commandBufferPool);
        }
        finally
        {
            _ = _compilationSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public ValidationResult Validate(KernelDefinition definition)
    {
        if (definition == null)
        {
            return ValidationResult.Failure("Kernel definition cannot be null");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return ValidationResult.Failure("Kernel name cannot be empty");
        }

        if (definition.Code == null || definition.Code.Length == 0)
        {
            return ValidationResult.Failure("Kernel code cannot be empty");
        }

        // Check if this looks like Metal code or binary
        var codeString = definition.Code ?? string.Empty;
        if (!codeString.Contains("kernel", StringComparison.Ordinal) && !codeString.Contains("metal", StringComparison.Ordinal) && !IsBinaryCode(Encoding.UTF8.GetBytes(codeString)))
        {
            return ValidationResult.Failure("Code does not appear to be valid Metal shader language or compiled binary");
        }

        return ValidationResult.Success();
    }

    private static string ExtractMetalCode(KernelDefinition definition)
    {
        // If it's binary code, we'll need to handle it differently
        if (definition.Code != null && IsBinaryCode(System.Text.Encoding.UTF8.GetBytes(definition.Code)))
        {
            throw new NotSupportedException("Pre-compiled Metal binaries are not yet supported");
        }

        // Get the source code
        var code = definition.Code ?? string.Empty;
        
        // Check if this is already MSL code (generated from C# or handwritten)
        if (code.Contains("#include <metal_stdlib>", StringComparison.Ordinal) || 
            code.Contains("kernel void", StringComparison.Ordinal))
        {
            // Already Metal code, return as-is
            return code;
        }
        
        // Check if the language is specified as Metal
        if (definition.Language == KernelLanguage.Metal)
        {
            // If Metal is specified but headers are missing, add them
            if (!code.Contains("#include <metal_stdlib>", StringComparison.Ordinal))
            {
                var sb = new StringBuilder();
                _ = sb.AppendLine("#include <metal_stdlib>");
                _ = sb.AppendLine("#include <metal_compute>");
                _ = sb.AppendLine("using namespace metal;");
                _ = sb.AppendLine();
                _ = sb.Append(code);
                return sb.ToString();
            }
            return code;
        }
        
        // If it's OpenCL C or unspecified, we would need translation
        // For now, throw an error indicating MSL is required
        if (code.Contains("__kernel", StringComparison.Ordinal) || 
            code.Contains("__global", StringComparison.Ordinal) ||
            definition.Language == KernelLanguage.OpenCL)
        {
            throw new NotSupportedException(
                "OpenCL C to Metal Shading Language translation is not implemented. " +
                "Please provide Metal Shading Language code directly or use the [Kernel] attribute " +
                "to generate Metal kernels from C# code.");
        }

        // Try to add Metal headers if the code looks like it might be Metal
        var sb2 = new StringBuilder();
        _ = sb2.AppendLine("#include <metal_stdlib>");
        _ = sb2.AppendLine("#include <metal_compute>");
        _ = sb2.AppendLine("using namespace metal;");
        _ = sb2.AppendLine();
        _ = sb2.Append(code);
        return sb2.ToString();
    }

    private static bool IsBinaryCode(byte[] code)
    {
        // Check for common binary signatures
        if (code.Length < 4)
        {
            return false;
        }

        // Metal library magic number
        return code[0] == 0x4D && code[1] == 0x54 && code[2] == 0x4C && code[3] == 0x42;
    }

    private async Task<IntPtr> CompileMetalCodeAsync(string code, string kernelName, CompilationOptions options, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            // Create compile options
            var compileOptions = MetalNative.CreateCompileOptions();

            // Configure optimization settings
            ConfigureOptimizationOptions(compileOptions, options, kernelName);

            // Set language version based on system capabilities
            var languageVersion = GetOptimalLanguageVersion();
            MetalNative.SetCompileOptionsLanguageVersion(compileOptions, languageVersion);

            try
            {
                // Compile the code
                var error = IntPtr.Zero;
                var library = MetalNative.CompileLibrary(_device, code, compileOptions, ref error);

                if (library == IntPtr.Zero)
                {
                    var errorMessage = error != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(error)) ?? "Unknown error"
                        : "Failed to compile Metal library";

                    if (error != IntPtr.Zero)
                    {
                        MetalNative.ReleaseError(error);
                    }

                    throw new InvalidOperationException($"Metal compilation failed: {errorMessage}");
                }

                _logger.LogDebug("Compiler kernel compiled: {Name}", kernelName);
                return library;
            }
            finally
            {
                MetalNative.ReleaseCompileOptions(compileOptions);
            }
        }, cancellationToken).ConfigureAwait(false);
    }


    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Dispose the kernel cache (logs statistics)
        _kernelCache?.Dispose();

        _compilationSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ConfigureOptimizationOptions(IntPtr compileOptions, CompilationOptions options, string kernelName)
    {
        // Configure fast math based on optimization level
        var enableFastMath = options.OptimizationLevel >= OptimizationLevel.Default;
        if (options.FastMath || enableFastMath)
        {
            MetalNative.SetCompileOptionsFastMath(compileOptions, true);
            _logger.LogDebug("Fast math optimization enabled for kernel: {Name}", kernelName);
        }

        // Additional optimization hints could be set here based on the options
        if (options.OptimizationLevel == OptimizationLevel.Maximum)
        {
            _logger.LogDebug("Maximum optimization requested for kernel: {Name}", kernelName);
            // Metal doesn't expose many additional optimization flags through the public API
            // but we can log this for debugging purposes
        }
    }

    private static MetalLanguageVersion GetOptimalLanguageVersion()
    {
        // Determine the best Metal language version based on system capabilities
        try
        {
            var osVersion = Environment.OSVersion.Version;

            // macOS 13.0+ supports Metal 3.1
            if (osVersion.Major >= 13 || (osVersion.Major == 13 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal31;
            }

            // macOS 12.0+ supports Metal 3.0
            if (osVersion.Major >= 12 || (osVersion.Major == 12 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal30;
            }

            // macOS 11.0+ supports Metal 2.4
            if (osVersion.Major >= 11 || (osVersion.Major == 11 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal24;
            }

            // macOS 10.15+ supports Metal 2.3
            if (osVersion.Major >= 10 && osVersion.Minor >= 15)
            {
                return MetalLanguageVersion.Metal23;
            }

            // macOS 10.14+ supports Metal 2.1
            if (osVersion.Major >= 10 && osVersion.Minor >= 14)
            {
                return MetalLanguageVersion.Metal21;
            }

            // macOS 10.13+ supports Metal 2.0
            if (osVersion.Major >= 10 && osVersion.Minor >= 13)
            {
                return MetalLanguageVersion.Metal20;
            }

            // Fallback to Metal 1.2 for older systems
            return MetalLanguageVersion.Metal12;
        }
        catch
        {
            // If we can't determine OS version, use a safe default
            return MetalLanguageVersion.Metal20;
        }
    }


    /// <inheritdoc/>
    public ValueTask<ValidationResult> ValidateAsync(KernelDefinition kernel, CancellationToken cancellationToken = default) => ValueTask.FromResult(Validate(kernel));

    /// <inheritdoc/>
    public ValueTask<ICompiledKernel> OptimizeAsync(ICompiledKernel kernel, OptimizationLevel level, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Metal-specific optimizations
        // For now, return the kernel as-is since Metal optimization happens at compile time
        return ValueTask.FromResult(kernel);
    }
}
