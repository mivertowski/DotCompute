// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Timing;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// CUDA kernel compiler implementation using NVRTC.
/// Now delegates to specialized compilation pipeline for improved maintainability.
/// </summary>
public sealed partial class CudaKernelCompiler : IDisposable, IAsyncDisposable
{
    private readonly CudaCompilationPipeline _pipeline;
    private readonly ILogger _logger;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaKernelCompiler class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

    [RequiresUnreferencedCode("This type uses runtime code generation and reflection")]
    [RequiresDynamicCode("This type uses runtime code generation for CUDA kernel compilation")]
    public CudaKernelCompiler(CudaContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _pipeline = new CudaCompilationPipeline(context, logger);

        // Verify NVRTC availability
        if (!PTXCompiler.IsNvrtcAvailable())
        {
            LogNvrtcNotAvailable(_logger);
        }
        else
        {
            var (major, minor) = PTXCompiler.GetNvrtcVersion();
            LogNvrtcVersionDetected(_logger, major, minor);
        }
    }
    /// <summary>
    /// Gets compile kernel asynchronously.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default) => await CompileAsync(definition, options, cancellationToken);
    /// <summary>
    /// Gets compile asynchronously.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<ICompiledKernel> CompileAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definition);

        try
        {
            LogCompilingCudaKernel(_logger, definition.Name);

            var compiledKernel = await _pipeline.CompileKernelAsync(definition, options, cancellationToken).ConfigureAwait(false);

            LogSuccessfullyCompiledKernel(_logger, definition.Name);
            return compiledKernel;
        }
        catch (Exception ex)
        {
            LogKernelCompilationError(_logger, ex, definition.Name);
            throw new InvalidOperationException($"Failed to compile CUDA kernel '{definition.Name}'", ex);
        }
    }
    /// <summary>
    /// Gets compile batch asynchronously.
    /// </summary>
    /// <param name="definitions">The definitions.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<ICompiledKernel[]> CompileBatchAsync(KernelDefinition[] definitions, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definitions);

        var results = await _pipeline.CompileBatchAsync(definitions, options, cancellationToken).ConfigureAwait(false);
        return [.. results.Cast<ICompiledKernel>()];
    }
    /// <summary>
    /// Returns true if able to get cached, otherwise false.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="compiledKernel">The compiled kernel.</param>
    /// <returns>true if the operation succeeded; otherwise, false.</returns>

    public bool TryGetCached(string kernelName, out ICompiledKernel? compiledKernel)
    {
        ThrowIfDisposed();

        // Delegate to pipeline cache
        compiledKernel = null;
        return false; // Simplified implementation - cache lookup is handled internally by pipeline
    }
    /// <summary>
    /// Performs clear cache.
    /// </summary>

    public void ClearCache()
    {
        ThrowIfDisposed();
        LogClearingKernelCache(_logger);
        _pipeline.ClearCache();
    }

    /// <summary>
    /// Gets cache statistics for monitoring and debugging
    /// </summary>
    public Types.CacheStatistics GetCacheStatistics()
    {
        ThrowIfDisposed();
        return _pipeline.GetPipelineStatistics();
    }

    /// <summary>
    /// Gets the mangled function name for a kernel and function name.
    /// </summary>
    public static string? GetMangledFunctionName(string kernelName, string functionName) => PTXCompiler.GetMangledNames(kernelName)?.GetValueOrDefault(functionName);

    /// <summary>
    /// Gets all mangled function names for a kernel.
    /// </summary>
    public static Dictionary<string, string>? GetAllMangledNames(string kernelName) => PTXCompiler.GetMangledNames(kernelName);

    /// <summary>
    /// Sets the timing provider for automatic timestamp injection support.
    /// </summary>
    /// <param name="timingProvider">The timing provider instance, or null to disable injection.</param>
    /// <remarks>
    /// <para>
    /// When a timing provider is set and timestamp injection is enabled via
    /// <see cref="CudaTimingProvider.EnableTimestampInjection"/>, all subsequently compiled
    /// kernels will have timestamp recording code automatically injected at their entry point.
    /// </para>
    /// <para>
    /// This allows for automatic kernel profiling without manually modifying kernel code.
    /// The injected code records a GPU timestamp (via %%globaltimer on CC 6.0+) in parameter
    /// slot 0, shifting all user parameters by one position.
    /// </para>
    /// </remarks>
    public void SetTimingProvider(CudaTimingProvider? timingProvider)
    {
        ThrowIfDisposed();
        _pipeline.SetTimingProvider(timingProvider);
    }

    /// <summary>
    /// Sets the fence injection service for automatic memory fence injection support.
    /// </summary>
    /// <param name="fenceService">The fence injection service instance, or null to disable fence injection.</param>
    /// <remarks>
    /// <para>
    /// When a fence injection service is set (typically the <see cref="DotCompute.Backends.CUDA.Memory.CudaMemoryOrderingProvider"/>)
    /// and has pending fence requests, all subsequently compiled kernels will have memory fence
    /// instructions automatically injected at the specified locations.
    /// </para>
    /// <para>
    /// <strong>Fence Locations:</strong>
    /// <list type="bullet">
    /// <item><description>AtEntry: Fence at kernel entry point</description></item>
    /// <item><description>AtExit: Fence before ret instructions</description></item>
    /// <item><description>AfterWrites: Fence after store operations</description></item>
    /// <item><description>BeforeReads: Fence before load operations</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>PTX Instructions Injected:</strong>
    /// <list type="bullet">
    /// <item><description>ThreadBlock: <c>bar.sync 0;</c></description></item>
    /// <item><description>Device: <c>membar.gl;</c></description></item>
    /// <item><description>System: <c>membar.sys;</c></description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void SetFenceInjectionService(IFenceInjectionService? fenceService)
    {
        ThrowIfDisposed();
        _pipeline.SetFenceInjectionService(fenceService);
    }

    /// <summary>
    /// Checks NVRTC availability and version
    /// </summary>
    public static bool IsNvrtcAvailable() => PTXCompiler.IsNvrtcAvailable();

    /// <summary>
    /// Gets NVRTC version information
    /// </summary>
    public static (int major, int minor) GetNvrtcVersion() => PTXCompiler.GetNvrtcVersion();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _pipeline?.Dispose();
            _disposed = true;
        }
        catch (Exception ex)
        {
            LogDisposalError(_logger, ex);
        }
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                Dispose();
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposalError(_logger, ex);
            }
        }
    }
}
