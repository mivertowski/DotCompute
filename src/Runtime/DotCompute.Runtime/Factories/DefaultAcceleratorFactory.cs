// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Factories;
using DotCompute.Abstractions.Validation;
using DotCompute.Runtime.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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

    public async ValueTask<IAccelerator> CreateAsync(AcceleratorInfo acceleratorInfo, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceleratorInfo);
        serviceProvider ??= _serviceProvider;

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultAcceleratorFactory));
        }

        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        try
        {
            _logger.LogDebugMessage($"Creating accelerator {acceleratorInfo.Id} of type {acceleratorInfo.DeviceType}");

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
            var provider = await GetOrCreateProviderAsync(acceleratorType, serviceProvider, cancellationToken);

            // Create accelerator through provider
            var accelerator = await provider.CreateAsync(acceleratorInfo);

            // Cache based on lifetime setting
            if (_options.AcceleratorLifetime == Configuration.ServiceLifetime.Singleton)
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
                    warnings.AddRange(validationResult.Errors.Select(e => e.Message));
                    warnings.AddRange(validationResult.Warnings.Select(w => w.Message));
                }
            }

            stopwatch.Stop();
            _logger.LogInfoMessage($"Created accelerator {acceleratorInfo.Id} in {stopwatch.ElapsedMilliseconds}ms");

            return accelerator;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorMessage(ex, $"Failed to create accelerator {acceleratorInfo.Id} after {stopwatch.ElapsedMilliseconds}ms");
            throw;
        }
    }

    public async ValueTask<TProvider> CreateProviderAsync<TProvider>(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        where TProvider : class, IAcceleratorProvider
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultAcceleratorFactory));
        }

        _logger.LogDebugMessage($"Creating accelerator provider {typeof(TProvider).Name}");

        try
        {
            // Try to get from service provider first
            var provider = serviceProvider.GetService<TProvider>();
            if (provider != null)
            {
                await Task.CompletedTask; // Make method properly async
                return provider;
            }

            // For AOT compatibility, try to use ActivatorUtilities for DI-based creation
            try
            {
                var instance = ActivatorUtilities.CreateInstance<TProvider>(serviceProvider);
                return instance;
            }
            catch (InvalidOperationException)
            {
                // Fall back to parameterless constructor if available
                if (TryCreateInstanceWithParameterlessConstructor<TProvider>(out var fallbackInstance))
                {
                    return fallbackInstance;
                }
                throw new InvalidOperationException(
                    $"Cannot create instance of {typeof(TProvider).Name}. " +
                    "Ensure it has a parameterless constructor or all dependencies are registered.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create accelerator provider {typeof(TProvider).Name}");
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

    public async ValueTask<IAccelerator> CreateAsync(AcceleratorType type, AcceleratorConfiguration? configuration = null, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default)
    {
        serviceProvider ??= _serviceProvider;

        // Create a mock AcceleratorInfo from the type

        var acceleratorInfo = new AcceleratorInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{type} Accelerator",
            DeviceType = type.ToString(),
            DeviceIndex = configuration?.DeviceIndex ?? 0,
            IsUnifiedMemory = configuration?.MemoryStrategy == MemoryAllocationStrategy.Unified
        };


        return await CreateAsync(acceleratorInfo, serviceProvider, cancellationToken);
    }

    public async ValueTask<IAccelerator> CreateAsync(string backendName, AcceleratorConfiguration? configuration = null, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AcceleratorType>(backendName, true, out var type))
        {
            throw new ArgumentException($"Unknown backend name: {backendName}", nameof(backendName));
        }


        return await CreateAsync(type, configuration, serviceProvider, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<AcceleratorType>> GetAvailableTypesAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return GetSupportedTypes().ToList();
    }

    public async ValueTask<IReadOnlyList<AcceleratorInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var devices = new List<AcceleratorInfo>();


        foreach (var type in GetSupportedTypes())
        {
            // Create a mock device info for each supported type
            devices.Add(new AcceleratorInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{type} Device",
                DeviceType = type.ToString(),
                DeviceIndex = 0,
                IsUnifiedMemory = false
            });
        }


        return devices;
    }

    public bool UnregisterProvider(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);


        var keysToRemove = _providerTypes.Where(kvp => kvp.Value == providerType).Select(kvp => kvp.Key).ToList();


        foreach (var key in keysToRemove)
        {
            _ = _providerTypes.TryRemove(key, out _);
        }


        return keysToRemove.Count > 0;
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
            _logger.LogDebugMessage($"Registered provider {providerType.Name} for accelerator type {type}");
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
            _logger.LogDebugMessage("Creating service scope for accelerator {id}");
            return _serviceProvider.CreateScope();
        });
    }

    private void RegisterDefaultProviders()
        // Register CPU provider by default
        // _providerTypes[AcceleratorType.CPU] = typeof(DotCompute.Core.Accelerators.CpuAcceleratorProvider); // Commented out - type doesn't exist"





        => _logger.LogDebugMessage("Registered default accelerator providers");

    private async Task<IAcceleratorProvider> GetOrCreateProviderAsync(AcceleratorType type, IServiceProvider serviceProvider, CancellationToken cancellationToken)
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
            var provider = await CreateProviderAsync(providerType, serviceProvider, cancellationToken);
            return (IAcceleratorProvider)provider;
        }

        throw new NotSupportedException($"No provider found for accelerator type {type}");
    }

    [RequiresUnreferencedCode("Creating provider instances requires runtime type information")]
    private static ValueTask<object> CreateProviderAsync(Type providerType, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        // For AOT compatibility, try using ActivatorUtilities first
        try
        {
            var instance = ActivatorUtilities.CreateInstance(serviceProvider, providerType);
            return ValueTask.FromResult(instance);
        }
        catch (InvalidOperationException)
        {
            // Fall back to parameterless constructor if available
            var constructor = providerType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                var instance = constructor.Invoke(null);
                return ValueTask.FromResult(instance);
            }


            throw new InvalidOperationException(
                $"Cannot create instance of {providerType.Name}. " +
                "Ensure it has a parameterless constructor or all dependencies are registered.");
        }
    }

    private async Task<AcceleratorValidationResult> ValidateAcceleratorAsync(IAccelerator accelerator)
    {
        try
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var performanceMetrics = new Dictionary<string, double>();
            var supportedFeatures = AcceleratorFeature.None;
            var SupportedFeatures = new List<string>();

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
                    SupportedFeatures.Add("UnifiedMemory");
                }

                if (accelerator.Info.TotalMemory > 0)
                {
                    performanceMetrics["TotalMemoryMB"] = accelerator.Info.TotalMemory / (1024.0 * 1024.0);
                }
            }

            return errors.Count == 0
                ? AcceleratorValidationResult.Success(AcceleratorType.Auto, 0, SupportedFeatures.ToArray(),

                    new AcceleratorPerformanceMetrics
                    {
                        MemoryBandwidthGBps = performanceMetrics.GetValueOrDefault("MemoryBandwidth", 0.0),
                        ComputeCapabilityScore = performanceMetrics.GetValueOrDefault("ComputeCapability", 1.0),
                        InitializationTimeMs = performanceMetrics.GetValueOrDefault("SyncLatency", 0.0),
                        DeviceMemoryBytes = (long)performanceMetrics.GetValueOrDefault("TotalMemoryMB", 0.0) * 1024 * 1024,
                        SupportsUnifiedMemory = supportedFeatures.HasFlag(AcceleratorFeature.UnifiedMemory)
                    })
                : AcceleratorValidationResult.Failure(errors, warnings);
        }

        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating accelerator {AcceleratorId}", accelerator.Info?.Id);
            return AcceleratorValidationResult.Failure(new[] { $"Validation error: {ex.Message}" });
        }
    }

    /// <summary>
    /// AOT-safe method to create instances with parameterless constructors
    /// </summary>
    private static bool TryCreateInstanceWithParameterlessConstructor<T>([NotNullWhen(true)] out T? instance)
        where T : class
    {
        try
        {
            var constructor = typeof(T).GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                instance = (T)constructor.Invoke(null);
                return true;
            }
        }
        catch
        {
            // Ignore exceptions during fallback creation
        }


        instance = null;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebugMessage("Disposing DefaultAcceleratorFactory");

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
        if (_options.AcceleratorLifetime == Configuration.ServiceLifetime.Singleton)
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
