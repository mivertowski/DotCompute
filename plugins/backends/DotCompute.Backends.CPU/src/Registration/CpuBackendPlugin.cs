using System;
using System.Threading;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AbstractionsAcceleratorInfo = DotCompute.Abstractions.AcceleratorInfo;
using AbstractionsKernelDefinition = DotCompute.Abstractions.KernelDefinition;
using AbstractionsCompilationOptions = DotCompute.Abstractions.CompilationOptions;
using AbstractionsICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using AbstractionsIAccelerator = DotCompute.Abstractions.IAccelerator;
using AbstractionsIMemoryManager = DotCompute.Abstractions.IMemoryManager;
using CoreAcceleratorInfo = DotCompute.Core.AcceleratorInfo;
using CoreKernelDefinition = DotCompute.Core.KernelDefinition;
using CoreCompilationOptions = DotCompute.Core.CompilationOptions;
using CoreICompiledKernel = DotCompute.Core.ICompiledKernel;
using IAccelerator = DotCompute.Abstractions.IAccelerator;

namespace DotCompute.Backends.CPU.Registration;

/// <summary>
/// Plugin registration for the CPU backend.
/// </summary>
public static class CpuBackendPlugin
{
    /// <summary>
    /// Adds the CPU backend to the service collection.
    /// </summary>
    public static IServiceCollection AddCpuBackend(
        this IServiceCollection services,
        Action<CpuAcceleratorOptions>? configureAccelerator = null,
        Action<CpuThreadPoolOptions>? configureThreadPool = null)
    {
        // Register options
        if (configureAccelerator != null)
        {
            services.Configure(configureAccelerator);
        }
        else
        {
            services.Configure<CpuAcceleratorOptions>(options => { });
        }

        if (configureThreadPool != null)
        {
            services.Configure(configureThreadPool);
        }
        else
        {
            services.Configure<CpuThreadPoolOptions>(options => { });
        }

        // Register the CPU accelerator
        services.TryAddSingleton<CpuAccelerator>();
        
        // Register as IAccelerator with a factory that includes the backend name
        services.AddSingleton<IAccelerator>(provider =>
        {
            var accelerator = provider.GetRequiredService<CpuAccelerator>();
            return new NamedAcceleratorWrapper("cpu", accelerator);
        });

        return services;
    }

    /// <summary>
    /// Adds the CPU backend with default configuration.
    /// </summary>
    public static IServiceCollection AddCpuBackend(this IServiceCollection services)
    {
        return services.AddCpuBackend(null, null);
    }
}

/// <summary>
/// Wrapper to provide named accelerator support.
/// </summary>
internal sealed class NamedAcceleratorWrapper : AbstractionsIAccelerator
{
    private readonly string _name;
    private readonly AbstractionsIAccelerator _accelerator;

    public NamedAcceleratorWrapper(string name, AbstractionsIAccelerator accelerator)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    public string Name => _name;

    public AbstractionsAcceleratorInfo Info => _accelerator.Info;

    public AbstractionsIMemoryManager Memory => _accelerator.Memory;

    public ValueTask<AbstractionsICompiledKernel> CompileKernelAsync(
        AbstractionsKernelDefinition definition,
        AbstractionsCompilationOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        return _accelerator.CompileKernelAsync(definition, options, cancellationToken);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        return _accelerator.SynchronizeAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _accelerator.DisposeAsync();
    }
}