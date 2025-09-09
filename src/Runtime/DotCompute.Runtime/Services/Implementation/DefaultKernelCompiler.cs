// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Runtime.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Implementation;

/// <summary>
/// Default kernel compiler implementation that delegates to backend-specific compilers.
/// </summary>
public class DefaultKernelCompiler : IKernelCompiler
{
    private readonly ILogger<DefaultKernelCompiler> _logger;
    private readonly ConcurrentDictionary<string, IKernelCompiler> _backendCompilers;

    public DefaultKernelCompiler(ILogger<DefaultKernelCompiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backendCompilers = new ConcurrentDictionary<string, IKernelCompiler>();
    }

    /// <summary>
    /// Registers a backend-specific compiler.
    /// </summary>
    public void RegisterBackendCompiler(string backendType, IKernelCompiler compiler)
    {
        _backendCompilers[backendType] = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _logger.LogInformation("Registered compiler for backend: {BackendType}", backendType);
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
            _logger.LogDebug("Using {BackendType} compiler for kernel {KernelName}", 
                backendType, kernelDefinition.Name);
            return await backendCompiler.CompileAsync(kernelDefinition, accelerator, cancellationToken);
        }

        // Fallback: Use accelerator's built-in compilation if available
        if (accelerator is IAcceleratorWithCompilation compilingAccelerator)
        {
            _logger.LogDebug("Using accelerator's built-in compiler for kernel {KernelName}", 
                kernelDefinition.Name);
            
            var compilationOptions = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Release,
                EnableDebugInfo = false,
                TargetArchitecture = accelerator.Info.DeviceType
            };

            return await compilingAccelerator.CompileKernelAsync(
                kernelDefinition, 
                compilationOptions, 
                cancellationToken);
        }

        throw new NotSupportedException(
            $"No compiler available for backend type: {backendType}. " +
            $"Register a backend-specific compiler or use an accelerator with built-in compilation support.");
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

        // Check if accelerator supports compilation
        return accelerator is IAcceleratorWithCompilation;
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
            OptimizationLevel = OptimizationLevel.Release,
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
                _logger.LogError(ex, "Failed to compile kernel {KernelName}", kernelDef.Name);
            }
        }

        return results;
    }
}

/// <summary>
/// Interface for accelerators that support kernel compilation.
/// </summary>
public interface IAcceleratorWithCompilation : IAccelerator
{
    /// <summary>
    /// Compiles a kernel definition into executable code.
    /// </summary>
    ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition kernelDefinition,
        CompilationOptions compilationOptions,
        CancellationToken cancellationToken = default);
}