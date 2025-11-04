// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Optimization;
using Microsoft.Extensions.Logging;
using LinqKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Compiles CUDA C kernel source code using NVRTC (NVIDIA Runtime Compilation).
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 6 Task 1: Option A Implementation - Production Grade</b>
/// </para>
/// <para>
/// This compiler integrates CudaKernelGenerator (generates CUDA C source) with
/// CudaAccelerator (compiles and manages CUDA kernels) to provide seamless GPU acceleration.
/// </para>
/// <para>
/// <b>Architecture:</b>
/// </para>
/// <list type="number">
/// <item><description>Receive CUDA C source from CudaKernelGenerator</description></item>
/// <item><description>Use CudaAccelerator.CompileKernelAsync for NVRTC compilation</description></item>
/// <item><description>Return ICompiledKernel for execution in RuntimeExecutor</description></item>
/// </list>
/// <para>
/// <b>Benefits of Option A:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Reuses existing CUDA backend infrastructure (no duplication)</description></item>
/// <item><description>Clean separation of concerns (LINQ → Generator → Compiler → Executor)</description></item>
/// <item><description>Consistent with other backends (OpenCL, Metal follow same pattern)</description></item>
/// <item><description>Production-ready error handling and fallback to CPU</description></item>
/// </list>
/// </remarks>
public sealed class CudaRuntimeKernelCompiler : IGpuKernelCompiler, IDisposable
{
    private readonly ILogger<CudaRuntimeKernelCompiler> _logger;
    private readonly CudaAccelerator _accelerator;
    private readonly CudaKernelGenerator _kernelGenerator;
    private readonly Dictionary<string, object> _compiledKernels = new(); // Store actual backend kernel
    private readonly SemaphoreSlim _compilationLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaRuntimeKernelCompiler"/> class.
    /// </summary>
    /// <param name="accelerator">The CUDA accelerator for compilation and execution.</param>
    /// <param name="kernelGenerator">Optional CUDA kernel generator (uses default if not provided).</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public CudaRuntimeKernelCompiler(
        CudaAccelerator accelerator,
        CudaKernelGenerator? kernelGenerator = null,
        ILogger<CudaRuntimeKernelCompiler>? logger = null)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _kernelGenerator = kernelGenerator ?? new CudaKernelGenerator();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CudaRuntimeKernelCompiler>.Instance;

        _logger.LogInformation("CudaRuntimeKernelCompiler initialized for device: {DeviceName}, CC {ComputeCapability}",
            _accelerator.Info.Name, _accelerator.Info.ComputeCapability);
    }

    /// <inheritdoc/>
    public ComputeBackend TargetBackend => ComputeBackend.Cuda;

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
            _logger.LogDebug("Using cached compiled CUDA kernel (hash: {CacheKey})", cacheKey);
            var linqKernel = new CudaKernelAdapter(cachedKernel, _accelerator);
            return CreateCompiledKernelDto(linqKernel, metadata);
        }

        // Serialize compilation to prevent race conditions
        await _compilationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            if (_compiledKernels.TryGetValue(cacheKey, out cachedKernel))
            {
                var linqKernelCached = new CudaKernelAdapter(cachedKernel, _accelerator);
                return CreateCompiledKernelDto(linqKernelCached, metadata);
            }

            _logger.LogInformation("Compiling CUDA kernel ({SourceLength} bytes, {InputType} → {ResultType})",
                sourceCode.Length, metadata.InputType.Name, metadata.ResultType.Name);

            // Extract kernel name from source
            var kernelName = ExtractKernelName(sourceCode);

            // Create kernel definition
            var kernelDef = new KernelDefinition(
                name: kernelName,
                source: sourceCode,
                entryPoint: kernelName);

            // Compile using CUDA backend infrastructure (NVRTC)
            var backendKernel = await _accelerator.CompileKernelAsync(kernelDef, options, cancellationToken);

            if (backendKernel == null)
            {
                _logger.LogWarning("CUDA kernel compilation failed, returning null for CPU fallback");
                return null;
            }

            // Cache the compiled kernel (store the actual backend type)
            _compiledKernels[cacheKey] = backendKernel;

            _logger.LogInformation("Successfully compiled CUDA kernel: {KernelName} (cached: {CacheKey})",
                kernelName, cacheKey);

            // Wrap backend kernel in LINQ adapter
            var linqKernel = new CudaKernelAdapter(backendKernel, _accelerator);
            return CreateCompiledKernelDto(linqKernel, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CUDA compilation failed, triggering CPU fallback");
            return null; // Null triggers graceful CPU fallback
        }
        finally
        {
            _compilationLock.Release();
        }
    }

    /// <summary>
    /// Compiles an OperationGraph directly to a CUDA kernel.
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
            // Generate CUDA C source code
            var cudaSource = _kernelGenerator.GenerateCudaKernel(graph, metadata);

            _logger.LogDebug("Generated CUDA C source ({Length} bytes) from OperationGraph with {Operations} operations",
                cudaSource.Length, graph.Operations.Count);

            // Compile the generated source
            return await CompileAsync(cudaSource, metadata, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile OperationGraph to CUDA kernel");
            return null;
        }
    }

    /// <inheritdoc/>
    public bool IsAvailable()
    {
        try
        {
            var info = _accelerator.Info;
            // Require at least CC 5.0 (Maxwell) - check Major version
            var isReady = info.ComputeCapability != null && info.ComputeCapability.Major >= 5;

            _logger.LogDebug("CUDA availability: {Available} (Device: {DeviceName}, CC: {ComputeCapability})",
                isReady, info.Name, info.ComputeCapability);

            return isReady;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CUDA not available");
            return false;
        }
    }

    /// <inheritdoc/>
    public string GetDiagnostics()
    {
        try
        {
            var info = _accelerator.Info;
            var computeCapability = CudaCapabilityManager.GetTargetComputeCapability();

            return $"CUDA Runtime Kernel Compiler (Production)\n" +
                   $"  Device: {info.Name}\n" +
                   $"  Compute Capability: {info.ComputeCapability}\n" +
                   $"  Target CC: {computeCapability}\n" +
                   $"  Memory: {info.TotalMemory / (1024.0 * 1024.0 * 1024.0):F2} GB\n" +
                   $"  CUDA Driver: {info.DriverVersion}\n" +
                   $"  Cached Kernels: {_compiledKernels.Count}\n" +
                   $"  Status: Production-ready (Option A implementation)";
        }
        catch (Exception ex)
        {
            return $"CUDA diagnostics unavailable: {ex.Message}";
        }
    }

    #region Helper Methods

    /// <summary>
    /// Extracts the kernel entry point name from CUDA C source code.
    /// </summary>
    private static string ExtractKernelName(string source)
    {
        // Look for "__global__ void KernelName(" pattern
        var globalIndex = source.IndexOf("__global__", StringComparison.Ordinal);
        if (globalIndex < 0) return "Execute"; // Default fallback

        var voidIndex = source.IndexOf("void", globalIndex, StringComparison.Ordinal);
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
        return $"cuda_{hashInput.GetHashCode():X}";
    }

    /// <summary>
    /// Creates a CompiledKernel DTO from LinqKernel adapter.
    /// This bridges the CUDA backend representation (BackendKernel) with
    /// the LINQ representation (CompiledKernel DTO) via the adapter.
    /// </summary>
    private CompiledKernel CreateCompiledKernelDto(LinqKernel linqKernel, TypeMetadata metadata)
    {
        return new CompiledKernel
        {
            Backend = ComputeBackend.Cuda,
            Metadata = metadata,
            EntryPoint = linqKernel.Name,
            CompiledAt = DateTimeOffset.UtcNow,
            // Store the LinqKernel adapter for later execution
            // This is the bridge between LINQ and CUDA backend
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

            _logger.LogInformation("CudaRuntimeKernelCompiler disposed ({Count} cached kernels cleaned up)",
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
/// Adapter that wraps a CUDA backend compiled kernel and implements the LINQ ICompiledKernel interface.
/// This bridges the impedance mismatch between backend abstractions and LINQ abstractions.
/// </summary>
internal sealed class CudaKernelAdapter : LinqKernel
{
    private readonly dynamic _backendKernel; // Use dynamic to avoid interface resolution issues
    private readonly CudaAccelerator _accelerator;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaKernelAdapter"/> class.
    /// </summary>
    /// <param name="backendKernel">The backend compiled kernel to wrap.</param>
    /// <param name="accelerator">The CUDA accelerator for execution.</param>
    public CudaKernelAdapter(object backendKernel, CudaAccelerator accelerator)
    {
        _backendKernel = backendKernel ?? throw new ArgumentNullException(nameof(backendKernel));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    /// <inheritdoc/>
    public string Name => _backendKernel.Name;

    /// <inheritdoc/>
    public bool IsReady => true; // Backend kernel exists, so it's ready

    /// <inheritdoc/>
    public string BackendType => "CUDA";

    /// <inheritdoc/>
    public async Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Delegate to backend kernel's ExecuteAsync
        // The backend kernel has: ExecuteAsync(KernelArguments args, CancellationToken ct)
        await _backendKernel.ExecuteAsync(parameters, cancellationToken);
    }

    /// <inheritdoc/>
    public object GetMetadata()
    {
        return new
        {
            KernelName = (string)_backendKernel.Name,
            Backend = "CUDA",
            Accelerator = _accelerator.Info.Name,
            ComputeCapability = _accelerator.Info.ComputeCapability
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
