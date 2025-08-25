// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Runtime.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotCompute.Runtime.Factories;


/// <summary>
/// Default implementation of accelerator factory with comprehensive DI support
/// </summary>
public class DefaultAcceleratorFactory : IUnifiedAcceleratorFactory, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DefaultAcceleratorFactory> _logger;
    private readonly DotComputeRuntimeOptions _options;
    private readonly ConcurrentDictionary<AcceleratorType, Type> _providerTypes = new();
    private readonly ConcurrentDictionary<string, IServiceScope> _acceleratorScopes = new();
    private readonly ConcurrentDictionary<string, IAccelerator> _createdAccelerators = new();
    private bool _disposed;

    public DefaultAcceleratorFactory(
        IServiceProvider serviceProvider,
        IOptions<DotComputeRuntimeOptions> options,
        ILogger<DefaultAcceleratorFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register default providers
        RegisterDefaultProviders();
    }

    public async Task<IAccelerator> CreateAcceleratorAsync(AcceleratorInfo acceleratorInfo, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(acceleratorInfo);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultAcceleratorFactory));
        }

        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        try
        {
            _logger.LogDebug("Creating accelerator {AcceleratorId} of type {AcceleratorType}",
                acceleratorInfo.Id, acceleratorInfo.DeviceType);

            // Parse accelerator type
            if (!Enum.TryParse<AcceleratorType>(acceleratorInfo.DeviceType, true, out var acceleratorType))
            {
                throw new ArgumentException($"Unsupported accelerator type: {acceleratorInfo.DeviceType}");
            }

            // Check if we can create this type
            if (!CanCreateAccelerator(acceleratorType))
            {
                throw new NotSupportedException($"Accelerator type {acceleratorType} is not supported");
            }

            // Get or create provider
            var provider = await GetOrCreateProviderAsync(acceleratorType, serviceProvider);

            // Create accelerator through provider
            var accelerator = await provider.CreateAsync(acceleratorInfo);

            // Cache based on lifetime setting
            if (_options.AcceleratorLifetime == DotCompute.Runtime.Configuration.ServiceLifetime.Singleton)
            {
                _createdAccelerators[acceleratorInfo.Id] = accelerator;
            }

            // Validate if required
            AcceleratorValidationResult? validationResult = null;
            if (_options.ValidateCapabilities)
            {
                validationResult = await ValidateAcceleratorAsync(accelerator);
                if (!validationResult.IsValid)
                {
                    warnings.AddRange(validationResult.Errors);
                    warnings.AddRange(validationResult.Warnings);
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("Created accelerator {AcceleratorId} in {ElapsedMs}ms",
                acceleratorInfo.Id, stopwatch.ElapsedMilliseconds);

            return accelerator;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to create accelerator {AcceleratorId} after {ElapsedMs}ms",
                acceleratorInfo.Id, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<TProvider> CreateProviderAsync<TProvider>(IServiceProvider serviceProvider)
        where TProvider : class, IAcceleratorProvider
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultAcceleratorFactory));
        }

        _logger.LogDebug("Creating accelerator provider {ProviderType}", typeof(TProvider).Name);

        try
        {
            // Try to get from service provider first
            var provider = serviceProvider.GetService<TProvider>();
            if (provider != null)
            {
                await Task.CompletedTask; // Make method properly async
                return provider;
            }

            // Create manually with DI
            var constructors = typeof(TProvider).GetConstructors();
            var bestConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

            if (bestConstructor == null)
            {
                throw new InvalidOperationException($"No suitable constructor found for {typeof(TProvider).Name}");
            }

            var parameters = bestConstructor.GetParameters();
            var dependencies = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var dependency = serviceProvider.GetService(parameters[i].ParameterType);
                if (dependency == null && !IsOptionalParameter(parameters[i]))
                {
                    throw new InvalidOperationException(
                        $"Required dependency {parameters[i].ParameterType.Name} could not be resolved for {typeof(TProvider).Name}");
                }
                dependencies[i] = dependency!;
            }

            var instance = (TProvider)Activator.CreateInstance(typeof(TProvider), dependencies)!;

            _logger.LogDebug("Created accelerator provider {ProviderType}", typeof(TProvider).Name);
            return instance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create accelerator provider {ProviderType}", typeof(TProvider).Name);
            throw;
        }
    }

    public bool CanCreateAccelerator(AcceleratorType acceleratorType)
    {
        return _providerTypes.ContainsKey(acceleratorType) ||
               _serviceProvider.GetServices<IAcceleratorProvider>()
                   .Any(p => p.SupportedTypes.Contains(acceleratorType));
    }

    public IEnumerable<AcceleratorType> GetSupportedTypes()
    {
        var supportedTypes = new HashSet<AcceleratorType>(_providerTypes.Keys);

        // Add types from registered providers
        foreach (var provider in _serviceProvider.GetServices<IAcceleratorProvider>())
        {
            foreach (var type in provider.SupportedTypes)
            {
                _ = supportedTypes.Add(type);
            }
        }

        return supportedTypes;
    }

    public void RegisterProvider(Type providerType, params AcceleratorType[] supportedTypes)
    {
        ArgumentNullException.ThrowIfNull(providerType);
        ArgumentNullException.ThrowIfNull(supportedTypes);

        if (!typeof(IAcceleratorProvider).IsAssignableFrom(providerType))
        {
            throw new ArgumentException($"Provider type {providerType.Name} must implement IAcceleratorProvider");
        }

        foreach (var type in supportedTypes)
        {
            _providerTypes[type] = providerType;
            _logger.LogDebug("Registered provider {ProviderType} for accelerator type {AcceleratorType}",
                providerType.Name, type);
        }
    }

    public IServiceScope CreateAcceleratorScope(string acceleratorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceleratorId);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultAcceleratorFactory));
        }

        return _acceleratorScopes.GetOrAdd(acceleratorId, id =>
        {
            _logger.LogDebug("Creating service scope for accelerator {AcceleratorId}", id);
            return _serviceProvider.CreateScope();
        });
    }

    private void RegisterDefaultProviders()
        // Register CPU provider by default
        // _providerTypes[AcceleratorType.CPU] = typeof(DotCompute.Core.Accelerators.CpuAcceleratorProvider); // Commented out - type doesn't exist"


        => _logger.LogDebug("Registered default accelerator providers");

    private async Task<IAcceleratorProvider> GetOrCreateProviderAsync(AcceleratorType type, IServiceProvider serviceProvider)
    {
        // First try to get from registered providers
        var existingProvider = _serviceProvider.GetServices<IAcceleratorProvider>()
            .FirstOrDefault(p => p.SupportedTypes.Contains(type));

        if (existingProvider != null)
        {
            return existingProvider;
        }

        // Try to create from registered type
        if (_providerTypes.TryGetValue(type, out var providerType))
        {
            var provider = await CreateProviderAsync(providerType, serviceProvider);
            return (IAcceleratorProvider)provider;
        }

        throw new NotSupportedException($"No provider found for accelerator type {type}");
    }

    private Task<object> CreateProviderAsync(Type providerType, IServiceProvider serviceProvider)
    {
        var constructors = providerType.GetConstructors();
        var bestConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

        if (bestConstructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found for {providerType.Name}");
        }

        var parameters = bestConstructor.GetParameters();
        var dependencies = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var dependency = serviceProvider.GetService(parameters[i].ParameterType);
            if (dependency == null && !IsOptionalParameter(parameters[i]))
            {
                throw new InvalidOperationException(
                    $"Required dependency {parameters[i].ParameterType.Name} could not be resolved for {providerType.Name}");
            }
            dependencies[i] = dependency!;
        }

        var instance = Activator.CreateInstance(providerType, dependencies)!;
        return Task.FromResult(instance);
    }

    private async Task<AcceleratorValidationResult> ValidateAcceleratorAsync(IAccelerator accelerator)
    {
        try
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var performanceMetrics = new Dictionary<string, double>();
            var supportedFeatures = AcceleratorFeature.None;

            // Basic validation
            if (accelerator.Info == null)
            {
                errors.Add("Accelerator info is null");
            }

            if (accelerator.Memory == null)
            {
                errors.Add("Accelerator memory manager is null");
            }

            // Test basic functionality
            try
            {
                await accelerator.SynchronizeAsync();
                performanceMetrics["SyncLatency"] = 1.0; // Placeholder
            }
            catch (Exception ex)
            {
                warnings.Add($"Synchronization test failed: {ex.Message}");
            }

            // Detect supported features
            if (accelerator.Info != null)
            {
                if (accelerator.Info.IsUnifiedMemory)
                {
                    supportedFeatures |= AcceleratorFeature.UnifiedMemory;
                }

                if (accelerator.Info.TotalMemory > 0)
                {
                    performanceMetrics["TotalMemoryMB"] = accelerator.Info.TotalMemory / (1024.0 * 1024.0);
                }
            }

            return errors.Count == 0
                ? AcceleratorValidationResult.Success(supportedFeatures, performanceMetrics)
                : AcceleratorValidationResult.Failure(errors, warnings);
        }

        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating accelerator {AcceleratorId}", accelerator.Info?.Id);
            return AcceleratorValidationResult.Failure(new[] { $"Validation error: {ex.Message}" });
        }
    }

    private static bool IsOptionalParameter(System.Reflection.ParameterInfo parameter)
    {
        return parameter.HasDefaultValue ||
               // parameter.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.OptionalAttribute), false).Any() ||
               Nullable.GetUnderlyingType(parameter.ParameterType) != null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing DefaultAcceleratorFactory");

        // Dispose all accelerator scopes
        foreach (var scope in _acceleratorScopes.Values)
        {
            try
            {
                scope.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing accelerator scope");
            }
        }

        // Dispose cached accelerators if they are singletons
        if (_options.AcceleratorLifetime == DotCompute.Runtime.Configuration.ServiceLifetime.Singleton)
        {
            foreach (var accelerator in _createdAccelerators.Values)
            {
                try
                {
                    _ = accelerator.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing cached accelerator");
                }
            }
        }

        _acceleratorScopes.Clear();
        _createdAccelerators.Clear();
        _providerTypes.Clear();

        _disposed = true;
    }
}
