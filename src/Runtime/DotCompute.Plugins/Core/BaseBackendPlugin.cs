// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - Base backend plugin has dynamic logging requirements

namespace DotCompute.Plugins.Core;

/// <summary>
/// Abstract base class for backend plugins that provides common registration patterns.
/// Uses the template method pattern to allow backend-specific customization while
/// eliminating duplicate code across backend implementations.
/// </summary>
/// <typeparam name="TAccelerator">The type of accelerator this backend provides.</typeparam>
/// <typeparam name="TOptions">The type of backend-specific configuration options.</typeparam>
public abstract class BaseBackendPlugin<TAccelerator, TOptions> : BackendPluginBase
    where TAccelerator : class, IAccelerator
    where TOptions : class, new()
{
    /// <summary>
    /// Gets the name used for the named accelerator wrapper.
    /// Override this to customize the accelerator name.
    /// </summary>
    protected abstract string AcceleratorName { get; }

    /// <summary>
    /// Gets the configuration section name for backend options.
    /// Override this to customize the configuration section path.
    /// </summary>
    protected abstract string ConfigurationSectionName { get; }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        try
        {
            Logger?.LogDebug("Configuring services for {BackendName} backend plugin", AcceleratorName);

            // Configure backend-specific options using the template method pattern
            ConfigureBackendOptions(services, configuration);

            // Register the accelerator using backend-specific logic
            RegisterAccelerator(services, configuration);

            // Register the named accelerator wrapper
            RegisterNamedAccelerator(services);

            Logger?.LogDebug("Successfully configured services for {BackendName} backend plugin", AcceleratorName);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to configure services for {BackendName} backend plugin", AcceleratorName);
            throw;
        }
    }

    /// <summary>
    /// Configures backend-specific options. Override this method to provide
    /// custom configuration binding logic.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    protected virtual void ConfigureBackendOptions(IServiceCollection services, IConfiguration configuration)
    {
        // Default implementation binds options from the configuration section
        var sectionPath = $"{ConfigurationSectionName}:Options";
        
        Logger?.LogDebug("Binding configuration from section: {SectionPath}", sectionPath);
        
        services.Configure<TOptions>(options =>
            configuration.GetSection(sectionPath).Bind(options));
    }

    /// <summary>
    /// Registers the backend-specific accelerator. This method must be implemented
    /// by derived classes to provide their specific accelerator registration logic.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    protected abstract void RegisterAccelerator(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Registers the named accelerator wrapper that provides the common IAccelerator interface.
    /// Override this method if you need custom wrapper behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    protected virtual void RegisterNamedAccelerator(IServiceCollection services)
    {
        Logger?.LogDebug("Registering named accelerator wrapper for {BackendName}", AcceleratorName);

        services.AddSingleton<IAccelerator>(provider =>
        {
            try
            {
                var accelerator = provider.GetRequiredService<TAccelerator>();
                return CreateNamedAcceleratorWrapper(AcceleratorName, accelerator);
            }
            catch (Exception ex)
            {
                var logger = provider.GetService<ILogger<BaseBackendPlugin<TAccelerator, TOptions>>>();
                logger?.LogError(ex, "Failed to create named accelerator wrapper for {BackendName}", AcceleratorName);
                throw;
            }
        });
    }

    /// <summary>
    /// Creates a named accelerator wrapper. Override this method to provide
    /// custom wrapper implementation if needed.
    /// </summary>
    /// <param name="name">The name for the accelerator.</param>
    /// <param name="accelerator">The underlying accelerator instance.</param>
    /// <returns>A wrapped accelerator with the specified name.</returns>
    protected virtual IAccelerator CreateNamedAcceleratorWrapper(string name, TAccelerator accelerator)
    {
        return new NamedAcceleratorWrapper(name, accelerator);
    }

    /// <summary>
    /// Provides common lifecycle logging for initialization.
    /// Override OnInitializeAsync to add backend-specific initialization logic.
    /// </summary>
    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Initializing {BackendName} backend plugin", AcceleratorName);
        
        try
        {
            await OnBackendInitializeAsync(cancellationToken).ConfigureAwait(false);
            await base.OnInitializeAsync(cancellationToken).ConfigureAwait(false);
            
            Logger?.LogInformation("{BackendName} backend plugin initialized successfully", AcceleratorName);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to initialize {BackendName} backend plugin", AcceleratorName);
            throw;
        }
    }

    /// <summary>
    /// Provides common lifecycle logging for startup.
    /// Override OnStartAsync to add backend-specific startup logic.
    /// </summary>
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Starting {BackendName} backend plugin", AcceleratorName);
        
        try
        {
            await OnBackendStartAsync(cancellationToken).ConfigureAwait(false);
            await base.OnStartAsync(cancellationToken).ConfigureAwait(false);
            
            Logger?.LogInformation("{BackendName} backend plugin started successfully", AcceleratorName);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to start {BackendName} backend plugin", AcceleratorName);
            throw;
        }
    }

    /// <summary>
    /// Provides common lifecycle logging for shutdown.
    /// Override OnStopAsync to add backend-specific shutdown logic.
    /// </summary>
    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Stopping {BackendName} backend plugin", AcceleratorName);
        
        try
        {
            await OnBackendStopAsync(cancellationToken).ConfigureAwait(false);
            await base.OnStopAsync(cancellationToken).ConfigureAwait(false);
            
            Logger?.LogInformation("{BackendName} backend plugin stopped successfully", AcceleratorName);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to stop {BackendName} backend plugin", AcceleratorName);
            throw;
        }
    }

    /// <summary>
    /// Called during backend initialization. Override this method to add
    /// backend-specific initialization logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    protected virtual Task OnBackendInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called during backend startup. Override this method to add
    /// backend-specific startup logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    protected virtual Task OnBackendStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called during backend shutdown. Override this method to add
    /// backend-specific shutdown logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    protected virtual Task OnBackendStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Common wrapper to provide named accelerator support across all backends.
/// This consolidates the duplicate NamedAcceleratorWrapper implementations.
/// </summary>
public sealed class NamedAcceleratorWrapper : IAccelerator
{
    private readonly string _name;
    private readonly IAccelerator _accelerator;

    /// <summary>
    /// Initializes a new instance of the NamedAcceleratorWrapper class.
    /// </summary>
    /// <param name="name">The name for this accelerator.</param>
    /// <param name="accelerator">The underlying accelerator instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when name or accelerator is null.</exception>
    public NamedAcceleratorWrapper(string name, IAccelerator accelerator)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    /// <inheritdoc/>
    public string Name => _name;

    /// <inheritdoc/>
    public AcceleratorType Type => _accelerator.Type;

    /// <inheritdoc/>
    public AcceleratorInfo Info => _accelerator.Info;

    /// <inheritdoc/>
    public IMemoryManager Memory => _accelerator.Memory;

    /// <inheritdoc/>
    public AcceleratorContext Context => _accelerator.Context;

    /// <inheritdoc/>
    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = default,
        CancellationToken cancellationToken = default) => 
        _accelerator.CompileKernelAsync(definition, options, cancellationToken);

    /// <inheritdoc/>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => 
        _accelerator.SynchronizeAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _accelerator.DisposeAsync();
}

#pragma warning restore CA1848