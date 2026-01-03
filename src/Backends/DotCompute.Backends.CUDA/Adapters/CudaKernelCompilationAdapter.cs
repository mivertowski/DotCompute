// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Ports;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Adapters;

/// <summary>
/// CUDA adapter implementing the kernel compilation port.
/// Bridges the hexagonal architecture port interface to the CUDA backend implementation.
/// </summary>
/// <remarks>
/// This adapter wraps the <see cref="CudaKernelCompiler"/> to provide a
/// standardized compilation interface for the application core.
/// </remarks>
public sealed class CudaKernelCompilationAdapter : IKernelCompilationPort, IDisposable, IAsyncDisposable
{
    private readonly CudaKernelCompiler _compiler;
    private readonly CudaContext _context;
    private readonly ILogger<CudaKernelCompilationAdapter> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaKernelCompilationAdapter"/> class.
    /// </summary>
    /// <param name="context">The CUDA context.</param>
    /// <param name="logger">The logger.</param>
    [RequiresUnreferencedCode("Uses CUDA runtime compilation")]
    [RequiresDynamicCode("Uses runtime code generation for CUDA kernels")]
    public CudaKernelCompilationAdapter(
        CudaContext context,
        ILogger<CudaKernelCompilationAdapter> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compiler = new CudaKernelCompiler(context, logger);
    }

    /// <inheritdoc />
    public KernelCompilationCapabilities Capabilities { get; } = new()
    {
        SupportedLanguages = [KernelLanguage.Cuda, KernelLanguage.Ptx, KernelLanguage.CSharp],
        SupportsRuntimeCompilation = true,
        SupportsBinaryCaching = true,
        MaxKernelParameters = 256
    };

    /// <inheritdoc />
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelSource source,
        KernelCompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Convert port types to CUDA types
        var definition = ConvertToKernelDefinition(source);
        var cudaOptions = ConvertToCompilationOptions(options);

        var compiled = await _compiler.CompileAsync(definition, cudaOptions, cancellationToken);

        return compiled;
    }

    /// <inheritdoc />
    public ValueTask<KernelValidationResult> ValidateAsync(
        KernelSource source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Basic validation - check language support and source presence
        var errors = new List<KernelDiagnostic>();

        if (string.IsNullOrWhiteSpace(source.Code))
        {
            errors.Add(new KernelDiagnostic(
                "CUDA001",
                "Kernel source code is empty",
                DiagnosticSeverity.Error));
        }

        if (!Capabilities.SupportedLanguages.Contains(source.Language))
        {
            errors.Add(new KernelDiagnostic(
                "CUDA002",
                $"Language '{source.Language}' is not supported by CUDA backend",
                DiagnosticSeverity.Error));
        }

        if (source.Language == KernelLanguage.Cuda && !source.Code.Contains("__global__"))
        {
            errors.Add(new KernelDiagnostic(
                "CUDA003",
                "CUDA kernel source should contain at least one __global__ function",
                DiagnosticSeverity.Warning));
        }

        var result = errors.Count > 0
            ? new KernelValidationResult { IsValid = false, Errors = errors }
            : KernelValidationResult.Success();

        return ValueTask.FromResult(result);
    }

    private static KernelDefinition ConvertToKernelDefinition(KernelSource source)
    {
        var name = source.EntryPoint ?? "kernel";
        var code = source.Code;

        // Map kernel language to CUDA source type
        var sourceType = source.Language switch
        {
            KernelLanguage.Cuda => KernelSourceType.CudaC,
            KernelLanguage.Ptx => KernelSourceType.PTX,
            KernelLanguage.CSharp => KernelSourceType.CSharp,
            _ => KernelSourceType.CudaC
        };

        return new KernelDefinition
        {
            Name = name,
            Source = code,
            SourceType = sourceType,
            EntryPoint = source.EntryPoint ?? name,
            Parameters = [] // Will be determined at runtime
        };
    }

    private static CompilationOptions ConvertToCompilationOptions(KernelCompilationOptions options)
    {
        var cudaOptions = new CompilationOptions
        {
            GenerateDebugInfo = options.GenerateDebugInfo,
            OptimizationLevel = options.OptimizationLevel switch
            {
                OptimizationLevel.None => Abstractions.Kernels.OptimizationLevel.None,
                OptimizationLevel.Basic => Abstractions.Kernels.OptimizationLevel.Basic,
                OptimizationLevel.Default => Abstractions.Kernels.OptimizationLevel.Default,
                OptimizationLevel.Aggressive => Abstractions.Kernels.OptimizationLevel.Aggressive,
                OptimizationLevel.Maximum => Abstractions.Kernels.OptimizationLevel.Maximum,
                _ => Abstractions.Kernels.OptimizationLevel.Default
            }
        };

        // Add target compute capability if specified
        if (!string.IsNullOrEmpty(options.TargetCapability))
        {
            cudaOptions.TargetArchitecture = options.TargetCapability;
        }

        // Add additional compiler flags
        foreach (var flag in options.AdditionalFlags)
        {
            cudaOptions.AdditionalCompilerFlags.Add(flag);
        }

        return cudaOptions;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _compiler.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _compiler.DisposeAsync();
            _disposed = true;
        }
    }
}
