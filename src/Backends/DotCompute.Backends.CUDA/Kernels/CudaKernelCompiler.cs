// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Types;
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

    public async Task<ICompiledKernel> CompileAsync(KernelDefinition definition, DotCompute.Abstractions.CompilationOptions? options = null, CancellationToken cancellationToken = default)
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

    public async Task<ICompiledKernel[]> CompileBatchAsync(KernelDefinition[] definitions, DotCompute.Abstractions.CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definitions);

        var results = await _pipeline.CompileBatchAsync(definitions, options, cancellationToken).ConfigureAwait(false);
        return results.Cast<ICompiledKernel>().ToArray();
    }

    public bool TryGetCached(string kernelName, out ICompiledKernel? compiledKernel)
    {
        ThrowIfDisposed();

        // Delegate to pipeline cache
        compiledKernel = null;
        return false; // Simplified implementation - cache lookup is handled internally by pipeline
    }

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
    public static string? GetMangledFunctionName(string kernelName, string functionName)
    {
        return PTXCompiler.GetMangledNames(kernelName)?.GetValueOrDefault(functionName);
    }

    /// <summary>
    /// Gets all mangled function names for a kernel.
    /// </summary>
    public static Dictionary<string, string>? GetAllMangledNames(string kernelName)
    {
        return PTXCompiler.GetMangledNames(kernelName);
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