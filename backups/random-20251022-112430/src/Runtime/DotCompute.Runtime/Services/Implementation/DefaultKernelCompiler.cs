// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Implementation;

/// <summary>
/// Default kernel compiler implementation that delegates to backend-specific compilers.
/// Implements the unified kernel compiler interface.
/// </summary>
public class DefaultKernelCompiler(ILogger<DefaultKernelCompiler> logger) : IUnifiedKernelCompiler
{
    private readonly ILogger<DefaultKernelCompiler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, IUnifiedKernelCompiler> _backendCompilers = new();

    /// <inheritdoc />
    public string Name => "Default Kernel Compiler";

    /// <inheritdoc />
    public IReadOnlyList<KernelLanguage> SupportedSourceTypes => new KernelLanguage[]
    {
        KernelLanguage.CSharp,
        KernelLanguage.Cuda,
        KernelLanguage.OpenCL,
        KernelLanguage.HLSL,
        KernelLanguage.Metal
    };

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> Capabilities => new Dictionary<string, object>
    {
        { "SupportsMultipleBackends", true },
        { "SupportsBatchCompilation", true },
        { "SupportsOptimization", true },
        { "Version", "1.0.0" }
    };

    /// <summary>
    /// Registers a backend-specific compiler.
    /// </summary>
    public void RegisterBackendCompiler(string backendType, IUnifiedKernelCompiler compiler)
    {
        _backendCompilers[backendType] = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _logger.CompilerRegistered(backendType);
    }

    /// <inheritdoc />
    public async Task<ICompiledKernel> CompileAsync(
        KernelDefinition kernelDefinition,

        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        var backendType = accelerator.Info.DeviceType.ToUpperInvariant();

        // Try to get backend-specific compiler
        if (_backendCompilers.TryGetValue(backendType, out var backendCompiler))
        {
            _logger.CompilerSelected(backendType, kernelDefinition.Name);
            return await backendCompiler.CompileAsync(kernelDefinition, accelerator, cancellationToken);
        }

        // Fallback: Use accelerator's built-in compilation
        _logger.CompilerSelected("built-in", kernelDefinition.Name);


        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType
        };

        return await accelerator.CompileKernelAsync(
            kernelDefinition,
            compilationOptions,

            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CanCompileAsync(KernelDefinition kernelDefinition, IAccelerator accelerator)
    {
        var backendType = accelerator.Info.DeviceType.ToUpperInvariant();

        // Check if we have a backend-specific compiler

        if (_backendCompilers.TryGetValue(backendType, out var backendCompiler))
        {
            return await backendCompiler.CanCompileAsync(kernelDefinition, accelerator);
        }

        // All accelerators support compilation through the IAccelerator interface
        return true;
    }

    /// <inheritdoc />
    public CompilationOptions GetSupportedOptions(IAccelerator accelerator)
    {
        var backendType = accelerator.Info.DeviceType.ToUpperInvariant();


        if (_backendCompilers.TryGetValue(backendType, out var backendCompiler))
        {
            return backendCompiler.GetSupportedOptions(accelerator);
        }

        // Return default options
        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType
        };
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, ICompiledKernel>> BatchCompileAsync(
        IEnumerable<KernelDefinition> kernelDefinitions,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, ICompiledKernel>();
        var backendType = accelerator.Info.DeviceType.ToUpperInvariant();

        // Try backend-specific batch compilation
        if (_backendCompilers.TryGetValue(backendType, out var backendCompiler))
        {
            return await backendCompiler.BatchCompileAsync(kernelDefinitions, accelerator, cancellationToken);
        }

        // Fallback to sequential compilation
        foreach (var kernelDef in kernelDefinitions)
        {
            try
            {
                var compiled = await CompileAsync(kernelDef, accelerator, cancellationToken);
                results[kernelDef.Name] = compiled;
            }
            catch (Exception ex)
            {
                _logger.KernelCompilationFailed(kernelDef.Name, ex.Message, ex);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition source,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
        // Use a dummy accelerator for the legacy method call
        // This is a design issue that should be addressed in the future

        => throw new NotSupportedException("This method requires an accelerator. Use CompileAsync(KernelDefinition, IAccelerator, CancellationToken) instead.");

    /// <inheritdoc />
    public UnifiedValidationResult Validate(KernelDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new UnifiedValidationResult
        {
            Context = $"Validation of kernel '{source.Name}'"
        };

        // Basic validation
        if (string.IsNullOrEmpty(source.Name))
        {
            result.AddError("Kernel name cannot be null or empty", "DC001");
        }

        if (string.IsNullOrEmpty(source.Source))
        {
            result.AddError("Kernel source cannot be null or empty", "DC002");
        }

        // Note: KernelDefinition structure may vary
        // if (source.Parameters == null)
        //     result.AddWarning("Kernel parameters are null", "DC003");


        if (result.IsValid)
        {
            result.AddInfo("Kernel validation passed", "DC000");
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<UnifiedValidationResult> ValidateAsync(
        KernelDefinition source,
        CancellationToken cancellationToken = default)
        // For now, delegate to synchronous validation
        // In the future, this could perform more expensive async validation

        => await Task.FromResult(Validate(source));

    /// <inheritdoc />
    public async ValueTask<ICompiledKernel> OptimizeAsync(
        ICompiledKernel kernel,
        OptimizationLevel level,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        // For now, return the kernel as-is
        // Individual backend compilers can override this for specific optimizations
        _logger.LogWarning("Optimization not implemented in default compiler for level {Level}", level);
        return await Task.FromResult(kernel);
    }
}