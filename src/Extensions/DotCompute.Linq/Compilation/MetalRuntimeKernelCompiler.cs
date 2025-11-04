// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.Metal;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Optimization;
using Microsoft.Extensions.Logging;
using LinqKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Compiles Metal Shading Language (MSL) kernel source code for Apple Silicon and macOS GPUs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 6 Task 1: Option A Implementation - Production Grade</b>
/// </para>
/// <para>
/// This compiler integrates MetalKernelGenerator (generates MSL source) with
/// MetalAccelerator (compiles and manages Metal kernels) for Apple GPU acceleration.
/// </para>
/// <para>
/// <b>Architecture:</b>
/// </para>
/// <list type="number">
/// <item><description>Receive MSL source from MetalKernelGenerator</description></item>
/// <item><description>Use MetalAccelerator.CompileKernelAsync for Metal compilation</description></item>
/// <item><description>Return ICompiledKernel for execution in RuntimeExecutor</description></item>
/// </list>
/// <para>
/// <b>Supported Platforms:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Apple Silicon (M1, M2, M3, M4) - Unified memory architecture</description></item>
/// <item><description>macOS Intel with discrete AMD GPUs</description></item>
/// <item><description>iOS/iPadOS devices (A-series chips)</description></item>
/// </list>
/// </remarks>
public sealed class MetalRuntimeKernelCompiler : IGpuKernelCompiler, IDisposable
{
    private readonly ILogger<MetalRuntimeKernelCompiler> _logger;
    private readonly MetalAccelerator _accelerator;
    private readonly MetalKernelGenerator _kernelGenerator;
    private readonly Dictionary<string, object> _compiledKernels = new(); // Store actual backend kernel
    private readonly SemaphoreSlim _compilationLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalRuntimeKernelCompiler"/> class.
    /// </summary>
    /// <param name="accelerator">The Metal accelerator for compilation and execution.</param>
    /// <param name="kernelGenerator">Optional Metal kernel generator (uses default if not provided).</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public MetalRuntimeKernelCompiler(
        MetalAccelerator accelerator,
        MetalKernelGenerator? kernelGenerator = null,
        ILogger<MetalRuntimeKernelCompiler>? logger = null)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _kernelGenerator = kernelGenerator ?? new MetalKernelGenerator();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalRuntimeKernelCompiler>.Instance;

        _logger.LogInformation("MetalRuntimeKernelCompiler initialized for device: {DeviceName}",
            _accelerator.Info.Name);
    }

    /// <inheritdoc/>
    public ComputeBackend TargetBackend => ComputeBackend.Metal;

    /// <inheritdoc/>
    public async Task<CompiledKernel?> CompileAsync(
        string sourceCode,
        TypeMetadata metadata,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Generate cache key
        var cacheKey = GenerateCacheKey(sourceCode, options);

        // Check cache first
        if (_compiledKernels.TryGetValue(cacheKey, out var cachedKernel))
        {
            _logger.LogDebug("Using cached compiled Metal kernel (hash: {CacheKey})", cacheKey);
            var linqKernel = new MetalKernelAdapter(cachedKernel, _accelerator);
            return CreateCompiledKernelDto(linqKernel, metadata);
        }

        // Serialize compilation to prevent race conditions
        await _compilationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            if (_compiledKernels.TryGetValue(cacheKey, out cachedKernel))
            {
                var linqKernelCached = new MetalKernelAdapter(cachedKernel, _accelerator);
                return CreateCompiledKernelDto(linqKernelCached, metadata);
            }

            _logger.LogInformation("Compiling Metal kernel ({SourceLength} bytes, {InputType} → {ResultType})",
                sourceCode.Length, metadata.InputType.Name, metadata.ResultType.Name);

            // Extract kernel name from source
            var kernelName = ExtractKernelName(sourceCode);

            // Create kernel definition
            var kernelDef = new KernelDefinition(
                name: kernelName,
                source: sourceCode,
                entryPoint: kernelName);

            // Compile using Metal backend infrastructure
            var backendKernel = await _accelerator.CompileKernelAsync(kernelDef, options, cancellationToken);

            if (backendKernel == null)
            {
                _logger.LogWarning("Metal kernel compilation failed, returning null for CPU fallback");
                return null;
            }

            // Cache the compiled kernel (store the actual backend type)
            _compiledKernels[cacheKey] = backendKernel;

            _logger.LogInformation("Successfully compiled Metal kernel: {KernelName} (cached: {CacheKey})",
                kernelName, cacheKey);

            // Wrap backend kernel in LINQ adapter
            var linqKernelFinal = new MetalKernelAdapter(backendKernel, _accelerator);
            return CreateCompiledKernelDto(linqKernelFinal, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metal compilation failed, triggering CPU fallback");
            return null; // Null triggers graceful CPU fallback
        }
        finally
        {
            _compilationLock.Release();
        }
    }

    /// <summary>
    /// Compiles an OperationGraph directly to a Metal kernel.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for the kernel.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compiled kernel DTO, or null on failure.</returns>
    public async Task<CompiledKernel?> CompileFromGraphAsync(
        OperationGraph graph,
        TypeMetadata metadata,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            // Generate Metal Shading Language source code
            var metalSource = _kernelGenerator.GenerateMetalKernel(graph, metadata);

            _logger.LogDebug("Generated MSL source ({Length} bytes) from OperationGraph with {Operations} operations",
                metalSource.Length, graph.Operations.Count);

            // Compile the generated source
            return await CompileAsync(metalSource, metadata, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile OperationGraph to Metal kernel");
            return null;
        }
    }

    /// <inheritdoc/>
    public bool IsAvailable()
    {
        try
        {
            var info = _accelerator.Info;
            var isReady = _accelerator.IsAvailable;

            _logger.LogDebug("Metal availability: {Available} (Device: {DeviceName})",
                isReady, info.Name);

            return isReady;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metal not available");
            return false;
        }
    }

    /// <inheritdoc/>
    public string GetDiagnostics()
    {
        try
        {
            var info = _accelerator.Info;

            return $"Metal Runtime Kernel Compiler (Production)\n" +
                   $"  Device: {info.Name}\n" +
                   $"  Vendor: {info.Vendor}\n" +
                   $"  Memory: {info.TotalMemory / (1024.0 * 1024.0 * 1024.0):F2} GB\n" +
                   $"  Unified Memory: {(info.IsUnifiedMemory ? "Yes" : "No")}\n" +
                   $"  Cached Kernels: {_compiledKernels.Count}\n" +
                   $"  Status: Production-ready (Option A implementation)";
        }
        catch (Exception ex)
        {
            return $"Metal diagnostics unavailable: {ex.Message}";
        }
    }

    #region Helper Methods

    /// <summary>
    /// Extracts the kernel entry point name from MSL source code.
    /// </summary>
    private static string ExtractKernelName(string source)
    {
        // Look for "kernel void KernelName(" pattern
        var kernelIndex = source.IndexOf("kernel", StringComparison.Ordinal);
        if (kernelIndex < 0) return "Execute"; // Default fallback

        var voidIndex = source.IndexOf("void", kernelIndex, StringComparison.Ordinal);
        if (voidIndex < 0) return "Execute";

        var openParenIndex = source.IndexOf('(', voidIndex);
        if (openParenIndex < 0) return "Execute";

        // Extract name between 'void' and '('
        var nameStart = voidIndex + 4; // "void".Length
        var nameSegment = source.Substring(nameStart, openParenIndex - nameStart).Trim();

        return string.IsNullOrWhiteSpace(nameSegment) ? "Execute" : nameSegment;
    }

    /// <summary>
    /// Generates a cache key for compiled kernels.
    /// </summary>
    private static string GenerateCacheKey(string source, CompilationOptions options)
    {
        var hashInput = $"{source}:{options.OptimizationLevel}:{options.GenerateDebugInfo}";
        return $"metal_{hashInput.GetHashCode():X}";
    }

    /// <summary>
    /// Creates a CompiledKernel DTO from LinqKernel adapter.
    /// This bridges the Metal backend representation with the LINQ representation via the adapter.
    /// </summary>
    private CompiledKernel CreateCompiledKernelDto(LinqKernel linqKernel, TypeMetadata metadata)
    {
        return new CompiledKernel
        {
            Backend = ComputeBackend.Metal,
            Metadata = metadata,
            EntryPoint = linqKernel.Name,
            CompiledAt = DateTimeOffset.UtcNow,
            // Store the LinqKernel adapter for later execution
            __InternalKernelReference = linqKernel
        };
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _compilationLock.Wait();
        try
        {
            // Dispose all cached kernels
            foreach (var kernel in _compiledKernels.Values)
            {
                (kernel as IDisposable)?.Dispose();
            }
            _compiledKernels.Clear();

            _logger.LogInformation("MetalRuntimeKernelCompiler disposed ({Count} cached kernels cleaned up)",
                _compiledKernels.Count);
        }
        finally
        {
            _compilationLock.Release();
            _compilationLock.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Adapter that wraps a Metal backend compiled kernel and implements the LINQ ICompiledKernel interface.
/// This bridges the impedance mismatch between backend abstractions and LINQ abstractions.
/// </summary>
internal sealed class MetalKernelAdapter : LinqKernel
{
    private readonly dynamic _backendKernel; // Use dynamic to avoid interface resolution issues
    private readonly MetalAccelerator _accelerator;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalKernelAdapter"/> class.
    /// </summary>
    /// <param name="backendKernel">The backend compiled kernel to wrap.</param>
    /// <param name="accelerator">The Metal accelerator for execution.</param>
    public MetalKernelAdapter(object backendKernel, MetalAccelerator accelerator)
    {
        _backendKernel = backendKernel ?? throw new ArgumentNullException(nameof(backendKernel));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    /// <inheritdoc/>
    public string Name => _backendKernel.Name;

    /// <inheritdoc/>
    public bool IsReady => true; // Backend kernel exists, so it's ready

    /// <inheritdoc/>
    public string BackendType => "Metal";

    /// <inheritdoc/>
    public async Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Delegate to backend kernel's ExecuteAsync
        await _backendKernel.ExecuteAsync(parameters, cancellationToken);
    }

    /// <inheritdoc/>
    public object GetMetadata()
    {
        return new
        {
            KernelName = (string)_backendKernel.Name,
            Backend = "Metal",
            Accelerator = _accelerator.Info.Name,
            UnifiedMemory = _accelerator.Info.IsUnifiedMemory
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        (_backendKernel as IDisposable)?.Dispose();
        _disposed = true;
    }
}
