// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.OpenCL;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Optimization;
using Microsoft.Extensions.Logging;
using LinqKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Compiles OpenCL C kernel source code for cross-platform GPU execution.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 6 Task 1: Option A Implementation - Production Grade</b>
/// </para>
/// <para>
/// This compiler integrates OpenCLKernelGenerator (generates OpenCL C source) with
/// OpenCLAccelerator (compiles and manages OpenCL kernels) for cross-platform GPU acceleration.
/// </para>
/// <para>
/// <b>Architecture:</b>
/// </para>
/// <list type="number">
/// <item><description>Receive OpenCL C source from OpenCLKernelGenerator</description></item>
/// <item><description>Use OpenCLAccelerator.CompileKernelAsync for OpenCL compilation</description></item>
/// <item><description>Return ICompiledKernel for execution in RuntimeExecutor</description></item>
/// </list>
/// <para>
/// <b>Supported Platforms:</b>
/// </para>
/// <list type="bullet">
/// <item><description>NVIDIA GPUs (via OpenCL)</description></item>
/// <item><description>AMD GPUs (Radeon, RDNA architectures)</description></item>
/// <item><description>Intel GPUs (integrated and discrete)</description></item>
/// <item><description>ARM Mali GPUs (mobile and embedded)</description></item>
/// <item><description>Qualcomm Adreno GPUs (mobile)</description></item>
/// </list>
/// </remarks>
public sealed class OpenCLRuntimeKernelCompiler : IGpuKernelCompiler, IDisposable
{
    private readonly ILogger<OpenCLRuntimeKernelCompiler> _logger;
    private readonly OpenCLAccelerator _accelerator;
    private readonly OpenCLKernelGenerator _kernelGenerator;
    private readonly Dictionary<string, object> _compiledKernels = new(); // Store actual backend kernel
    private readonly SemaphoreSlim _compilationLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLRuntimeKernelCompiler"/> class.
    /// </summary>
    /// <param name="accelerator">The OpenCL accelerator for compilation and execution.</param>
    /// <param name="kernelGenerator">Optional OpenCL kernel generator (uses default if not provided).</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public OpenCLRuntimeKernelCompiler(
        OpenCLAccelerator accelerator,
        OpenCLKernelGenerator? kernelGenerator = null,
        ILogger<OpenCLRuntimeKernelCompiler>? logger = null)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _kernelGenerator = kernelGenerator ?? new OpenCLKernelGenerator();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenCLRuntimeKernelCompiler>.Instance;

        _logger.LogInformation("OpenCLRuntimeKernelCompiler initialized for device: {DeviceName}, Vendor: {Vendor}",
            _accelerator.Info.Name, _accelerator.Info.Vendor);
    }

    /// <inheritdoc/>
    public ComputeBackend TargetBackend => ComputeBackend.OpenCL;

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
            _logger.LogDebug("Using cached compiled OpenCL kernel (hash: {CacheKey})", cacheKey);
            var linqKernel = new OpenCLKernelAdapter(cachedKernel, _accelerator);
            return CreateCompiledKernelDto(linqKernel, metadata);
        }

        // Serialize compilation to prevent race conditions
        await _compilationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            if (_compiledKernels.TryGetValue(cacheKey, out cachedKernel))
            {
                var linqKernel = new OpenCLKernelAdapter(cachedKernel, _accelerator);
                return CreateCompiledKernelDto(linqKernel, metadata);
            }

            _logger.LogInformation("Compiling OpenCL kernel ({SourceLength} bytes, {InputType} → {ResultType})",
                sourceCode.Length, metadata.InputType.Name, metadata.ResultType.Name);

            // Extract kernel name from source
            var kernelName = ExtractKernelName(sourceCode);

            // Create kernel definition
            var kernelDef = new KernelDefinition(
                name: kernelName,
                source: sourceCode,
                entryPoint: kernelName);

            // Compile using OpenCL backend infrastructure
            var backendKernel = await _accelerator.CompileKernelAsync(kernelDef, options, cancellationToken);

            if (backendKernel == null)
            {
                _logger.LogWarning("OpenCL kernel compilation failed, returning null for CPU fallback");
                return null;
            }

            // Cache the compiled kernel (store the actual backend type)
            _compiledKernels[cacheKey] = backendKernel;

            _logger.LogInformation("Successfully compiled OpenCL kernel: {KernelName} (cached: {CacheKey})",
                kernelName, cacheKey);

            // Wrap backend kernel in LINQ adapter
            var linqKernelFinal = new OpenCLKernelAdapter(backendKernel, _accelerator);
            return CreateCompiledKernelDto(linqKernelFinal, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenCL compilation failed, triggering CPU fallback");
            return null; // Null triggers graceful CPU fallback
        }
        finally
        {
            _compilationLock.Release();
        }
    }

    /// <summary>
    /// Compiles an OperationGraph directly to an OpenCL kernel.
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
            // Generate OpenCL C source code
            var openclSource = _kernelGenerator.GenerateOpenCLKernel(graph, metadata);

            _logger.LogDebug("Generated OpenCL C source ({Length} bytes) from OperationGraph with {Operations} operations",
                openclSource.Length, graph.Operations.Count);

            // Compile the generated source
            return await CompileAsync(openclSource, metadata, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile OperationGraph to OpenCL kernel");
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

            _logger.LogDebug("OpenCL availability: {Available} (Device: {DeviceName}, Vendor: {Vendor})",
                isReady, info.Name, info.Vendor);

            return isReady;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenCL not available");
            return false;
        }
    }

    /// <inheritdoc/>
    public string GetDiagnostics()
    {
        try
        {
            var info = _accelerator.Info;

            return $"OpenCL Runtime Kernel Compiler (Production)\n" +
                   $"  Device: {info.Name}\n" +
                   $"  Vendor: {info.Vendor}\n" +
                   $"  Driver Version: {info.DriverVersion}\n" +
                   $"  Memory: {info.TotalMemory / (1024.0 * 1024.0 * 1024.0):F2} GB\n" +
                   $"  Cached Kernels: {_compiledKernels.Count}\n" +
                   $"  Status: Production-ready (Option A implementation)";
        }
        catch (Exception ex)
        {
            return $"OpenCL diagnostics unavailable: {ex.Message}";
        }
    }

    #region Helper Methods

    /// <summary>
    /// Extracts the kernel entry point name from OpenCL C source code.
    /// </summary>
    private static string ExtractKernelName(string source)
    {
        // Look for "__kernel void KernelName(" pattern
        var kernelIndex = source.IndexOf("__kernel", StringComparison.Ordinal);
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
        return $"opencl_{hashInput.GetHashCode():X}";
    }

    /// <summary>
    /// Creates a CompiledKernel DTO from LinqKernel adapter.
    /// This bridges the OpenCL backend representation with the LINQ representation via the adapter.
    /// </summary>
    private CompiledKernel CreateCompiledKernelDto(LinqKernel linqKernel, TypeMetadata metadata)
    {
        return new CompiledKernel
        {
            Backend = ComputeBackend.OpenCL,
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

            _logger.LogInformation("OpenCLRuntimeKernelCompiler disposed ({Count} cached kernels cleaned up)",
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
/// Adapter that wraps an OpenCL backend compiled kernel and implements the LINQ ICompiledKernel interface.
/// This bridges the impedance mismatch between backend abstractions and LINQ abstractions.
/// </summary>
internal sealed class OpenCLKernelAdapter : LinqKernel
{
    private readonly dynamic _backendKernel; // Use dynamic to avoid interface resolution issues
    private readonly OpenCLAccelerator _accelerator;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelAdapter"/> class.
    /// </summary>
    /// <param name="backendKernel">The backend compiled kernel to wrap.</param>
    /// <param name="accelerator">The OpenCL accelerator for execution.</param>
    public OpenCLKernelAdapter(object backendKernel, OpenCLAccelerator accelerator)
    {
        _backendKernel = backendKernel ?? throw new ArgumentNullException(nameof(backendKernel));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    /// <inheritdoc/>
    public string Name => _backendKernel.Name;

    /// <inheritdoc/>
    public bool IsReady => true; // Backend kernel exists, so it's ready

    /// <inheritdoc/>
    public string BackendType => "OpenCL";

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
            Backend = "OpenCL",
            Accelerator = _accelerator.Info.Name,
            Vendor = _accelerator.Info.Vendor,
            DriverVersion = _accelerator.Info.DriverVersion
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
