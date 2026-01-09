// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Ports;
using Microsoft.Extensions.Logging;

// Resolve ambiguous type references
using KernelCompilationOptions = DotCompute.Abstractions.Ports.KernelCompilationOptions;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Backends.Metal.Adapters;

/// <summary>
/// Metal adapter implementing the kernel compilation port.
/// Bridges the hexagonal architecture port interface to the Metal backend implementation.
/// </summary>
/// <remarks>
/// This adapter wraps Metal's shader compilation to provide a standardized
/// compilation interface for the application core.
/// </remarks>
public sealed class MetalKernelCompilationAdapter : IKernelCompilationPort, IDisposable
{
    private readonly ILogger<MetalKernelCompilationAdapter> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalKernelCompilationAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MetalKernelCompilationAdapter(ILogger<MetalKernelCompilationAdapter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public KernelCompilationCapabilities Capabilities { get; } = new()
    {
        SupportedLanguages = [KernelLanguage.Msl, KernelLanguage.CSharp],
        SupportsRuntimeCompilation = true,
        SupportsBinaryCaching = true,
        MaxKernelParameters = 31 // Metal limit
    };

    /// <inheritdoc />
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelSource source,
        KernelCompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Validate source language
        if (source.Language != KernelLanguage.Msl && source.Language != KernelLanguage.CSharp)
        {
            throw new NotSupportedException($"Metal backend does not support {source.Language} source language");
        }

        // Convert C# to MSL if needed
        var mslSource = source.Language == KernelLanguage.CSharp
            ? await TranslateCSharpToMslAsync(source.Code, cancellationToken)
            : source.Code;

        // Create compiled kernel wrapper
        var compiledKernel = new MetalCompiledKernel(
            source.EntryPoint ?? "kernel_main",
            mslSource,
            options.GenerateDebugInfo);

        return compiledKernel;
    }

    /// <inheritdoc />
    public ValueTask<KernelValidationResult> ValidateAsync(
        KernelSource source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var errors = new List<KernelDiagnostic>();
        var warnings = new List<KernelDiagnostic>();

        if (string.IsNullOrWhiteSpace(source.Code))
        {
            errors.Add(new KernelDiagnostic(
                "MTL001",
                "Kernel source code is empty",
                DiagnosticSeverity.Error));
        }

        if (!Capabilities.SupportedLanguages.Contains(source.Language))
        {
            errors.Add(new KernelDiagnostic(
                "MTL002",
                $"Language '{source.Language}' is not supported by Metal backend",
                DiagnosticSeverity.Error));
        }

        // MSL-specific validation
        if (source.Language == KernelLanguage.Msl)
        {
            if (!source.Code.Contains("kernel ") && !source.Code.Contains("[[kernel]]"))
            {
                warnings.Add(new KernelDiagnostic(
                    "MTL003",
                    "MSL source should contain a kernel function with [[kernel]] attribute",
                    DiagnosticSeverity.Warning));
            }

            if (source.Code.Contains("#include <metal_stdlib>") == false)
            {
                warnings.Add(new KernelDiagnostic(
                    "MTL004",
                    "Consider including metal_stdlib for common Metal functions",
                    DiagnosticSeverity.Info));
            }
        }

        var result = errors.Count > 0
            ? new KernelValidationResult { IsValid = false, Errors = errors, Warnings = warnings }
            : new KernelValidationResult { IsValid = true, Warnings = warnings };

        return ValueTask.FromResult(result);
    }

    private Task<string> TranslateCSharpToMslAsync(string csharpCode, CancellationToken cancellationToken)
    {
        // TODO: Integrate with MetalKernelTranslator
        // For now, return the code wrapped in basic MSL structure
        var msl = $@"#include <metal_stdlib>
using namespace metal;

// Translated from C# source
{csharpCode}
";
        return Task.FromResult(msl);
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
            _disposed = true;
        }
    }
}

/// <summary>
/// Metal compiled kernel representation.
/// </summary>
internal sealed class MetalCompiledKernel : ICompiledKernel
{
    public MetalCompiledKernel(string name, string mslSource, bool hasDebugInfo)
    {
        Id = Guid.NewGuid();
        Name = name;
        MslSource = mslSource;
        HasDebugInfo = hasDebugInfo;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    public string MslSource { get; }
    public bool HasDebugInfo { get; }

    /// <inheritdoc />
    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        // Metal kernel execution would be implemented here
        // For now, this is a placeholder for the compilation adapter
        throw new NotImplementedException("Metal kernel execution is not yet implemented in this adapter.");
    }

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
