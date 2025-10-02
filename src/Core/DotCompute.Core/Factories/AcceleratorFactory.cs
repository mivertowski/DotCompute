// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Factories;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Factories;

/// <summary>
/// Default implementation of the accelerator factory.
/// Provides centralized accelerator creation with configuration management and caching.
/// </summary>
public sealed class AcceleratorFactory : IUnifiedAcceleratorFactory, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AcceleratorFactory> _logger;
    private readonly ConcurrentDictionary<string, Func<AcceleratorConfiguration, ILoggerFactory, ValueTask<IAccelerator>>> _backends;
    private readonly ConcurrentDictionary<string, AcceleratorInfo> _deviceCache;
    private readonly SemaphoreSlim _deviceDiscoverySemaphore;
    private volatile bool _disposed;


    public AcceleratorFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);


        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AcceleratorFactory>();
        _backends = new ConcurrentDictionary<string, Func<AcceleratorConfiguration, ILoggerFactory, ValueTask<IAccelerator>>>(StringComparer.OrdinalIgnoreCase);
        _deviceCache = new ConcurrentDictionary<string, AcceleratorInfo>();
        _deviceDiscoverySemaphore = new SemaphoreSlim(1, 1);
        RegisterDefaultBackends();
    }

    /// <inheritdoc/>
    public async ValueTask<IAccelerator> CreateAsync(
        AcceleratorType type,
        AcceleratorConfiguration? configuration = null,
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var backendName = type.ToString();
        return await CreateAsync(backendName, configuration, serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IAccelerator> CreateAsync(
        string backendName,
        AcceleratorConfiguration? configuration = null,
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(backendName);


        _logger.LogInfoMessage("Creating {backendName} accelerator");


        if (!_backends.TryGetValue(backendName, out var factoryMethod))
        {
            throw new NotSupportedException($"Backend '{backendName}' is not registered. Available backends: {string.Join(", ", _backends.Keys)}");
        }


        configuration ??= GetDefaultConfiguration(backendName);


        try
        {
            var accelerator = await factoryMethod(configuration, _loggerFactory).ConfigureAwait(false);


            _logger.LogInfoMessage("Successfully created {backendName} accelerator");

            // Apply post-creation configuration

            await ConfigureAcceleratorAsync(accelerator, configuration, cancellationToken).ConfigureAwait(false);


            return accelerator;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create {backendName} accelerator");
            throw new InvalidOperationException($"Failed to create {backendName} accelerator", ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AcceleratorType>> GetAvailableTypesAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        var availableTypes = new List<AcceleratorType>();


        foreach (var backend in _backends.Keys)
        {
            try
            {
                if (Enum.TryParse<AcceleratorType>(backend, ignoreCase: true, out var type))
                {
                    // Try to create a test instance to verify availability
                    var testAccelerator = await CreateAsync(type, new AcceleratorConfiguration { EnableDebugMode = false }, serviceProvider: null, cancellationToken).ConfigureAwait(false);
                    if (testAccelerator is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else if (testAccelerator is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    availableTypes.Add(type);
                }
            }
            catch
            {
                // Backend not available on this system
                _logger.LogDebugMessage("Backend {backend} is not available on this system");
            }
        }


        return availableTypes;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AcceleratorInfo>> GetAvailableDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        await _deviceDiscoverySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_deviceCache.IsEmpty)
            {
                await DiscoverDevicesAsync(cancellationToken).ConfigureAwait(false);
            }


            return _deviceCache.Values.ToList();
        }
        finally
        {
            _ = _deviceDiscoverySemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public void RegisterBackend(
        string backendName,
        Func<AcceleratorConfiguration, ILoggerFactory, ValueTask<IAccelerator>> factoryMethod)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(backendName);
        ArgumentNullException.ThrowIfNull(factoryMethod);


        if (!_backends.TryAdd(backendName, factoryMethod))
        {
            throw new InvalidOperationException($"Backend '{backendName}' is already registered");
        }


        _logger.LogInfoMessage("Registered backend: {backendName}");
    }

    /// <inheritdoc/>
    public async ValueTask<IAccelerator> CreateAsync(
        AcceleratorInfo acceleratorInfo,
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(acceleratorInfo);


        var configuration = new AcceleratorConfiguration
        {
            DeviceIndex = 0,
            CustomProperties = new Dictionary<string, object>
            {
                ["DeviceInfo"] = acceleratorInfo
            }
        };


        return await CreateAsync(acceleratorInfo.Type, configuration, serviceProvider, cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc/>
    public async ValueTask<TProvider> CreateProviderAsync<TProvider>(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)

        where TProvider : class, IAcceleratorProvider
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(serviceProvider);


        var provider = serviceProvider.GetService(typeof(TProvider)) as TProvider ?? throw new InvalidOperationException($"Provider type {typeof(TProvider).Name} is not registered in the service provider");
        return await ValueTask.FromResult(provider);
    }


    /// <inheritdoc/>
    public bool CanCreateAccelerator(AcceleratorType acceleratorType)
    {
        ThrowIfDisposed();
        var backendName = acceleratorType.ToString();
        return _backends.ContainsKey(backendName);
    }


    /// <inheritdoc/>
    public void RegisterProvider(Type providerType, params AcceleratorType[] supportedTypes)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(providerType);
        ArgumentNullException.ThrowIfNull(supportedTypes);


        if (!typeof(IAcceleratorProvider).IsAssignableFrom(providerType))
        {
            throw new ArgumentException($"Type {providerType.Name} must implement IAcceleratorProvider", nameof(providerType));
        }


        foreach (var type in supportedTypes)
        {
            var backendName = type.ToString();
            RegisterBackend(backendName, async (config, loggerFactory) =>
            {
                var provider = Activator.CreateInstance(providerType) as IAcceleratorProvider ?? throw new InvalidOperationException($"Failed to create instance of {providerType.Name}");

                // Provider should create the accelerator based on type and config
                // The actual CreateAsync method signature may vary

                if (provider is IAccelerator accelerator)
                {
                    return accelerator;
                }

                // Try to find a Create method on the provider

                var createMethod = provider.GetType().GetMethod("CreateAccelerator")

                    ?? provider.GetType().GetMethod("Create");


                if (createMethod != null)
                {
                    var result = createMethod.Invoke(provider, [type, config]);
                    if (result is IAccelerator acc)
                    {
                        return acc;
                    }
                }


                await Task.CompletedTask; // Ensure async method has await
                throw new NotSupportedException($"Provider {providerType.Name} does not have a suitable creation method");
            });
        }


        _logger.LogInfoMessage($"Registered provider {providerType.Name} for types: {string.Join(", ", supportedTypes)}");
    }


    /// <inheritdoc/>
    public bool UnregisterProvider(Type providerType)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(providerType);

        // For simplicity, we'll remove all backends that were registered with this provider type
        // In production, you'd track which backends are associated with which provider - TODO

        _logger.LogInfoMessage("Unregistering provider {providerType.Name}");

        // Since we don't track provider associations, we can't fully implement this
        // Return false to indicate no providers were unregistered

        return false;
    }


    /// <inheritdoc/>
    public async ValueTask<IAccelerator> CreateBestForWorkloadAsync(
        WorkloadProfile workloadProfile,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(workloadProfile);


        _logger.LogInfoMessage("Selecting best accelerator for workload profile");


        var availableTypes = await GetAvailableTypesAsync(cancellationToken).ConfigureAwait(false);


        if (availableTypes.Count == 0)
        {
            throw new InvalidOperationException("No accelerators available on this system");
        }

        // Score each available accelerator type

        var scores = new Dictionary<AcceleratorType, int>();
        foreach (var type in availableTypes)
        {
            scores[type] = CalculateWorkloadScore(type, workloadProfile);
        }

        // Select the best scoring accelerator

        var bestType = scores.OrderByDescending(kvp => kvp.Value).First().Key;
        _logger.LogInfoMessage($"Selected {bestType} as best for workload (score: {scores[bestType]})");

        // Create optimized configuration for the workload

        var configuration = CreateOptimizedConfiguration(bestType, workloadProfile);


        return await CreateAsync(bestType, configuration, null, cancellationToken).ConfigureAwait(false);
    }

    private void RegisterDefaultBackends()
    {
        // Register CPU backend (always available)
        RegisterBackend("CPU", async (config, loggerFactory) =>
        {
            await Task.Yield(); // Ensure async context

            // For AOT compatibility, use factory method instead of dynamic type loading
            // This should be replaced with proper DI registration in production TODO

            throw new NotSupportedException(
                "Dynamic backend loading is not supported in AOT scenarios. " +
                "Use static backend registration via dependency injection instead.");
        });

        // Register CUDA backend (conditional)

        RegisterBackend("CUDA", async (config, loggerFactory) =>
        {
            await Task.Yield(); // Ensure async context

            // For AOT compatibility, backends should be registered statically TODO

            throw new NotSupportedException(
                "Dynamic backend loading is not supported in AOT scenarios. " +
                "Use static backend registration via dependency injection instead. " +
                "For CUDA backend, register CudaAccelerator through DI container.");
        });

        // Additional backends can be registered here

    }

    private IUnifiedMemoryManager CreateMemoryManager(AcceleratorConfiguration config, ILoggerFactory loggerFactory)
    {
        return config.MemoryStrategy switch
        {
            MemoryAllocationStrategy.OptimizedPooled => CreateOptimizedPooledMemoryManager(config, loggerFactory),
            MemoryAllocationStrategy.Pooled => CreatePooledMemoryManager(config, loggerFactory),
            MemoryAllocationStrategy.Unified => CreateUnifiedMemoryManager(config, loggerFactory),
            _ => CreateDirectMemoryManager(config, loggerFactory)
        };
    }

    private IUnifiedMemoryManager CreateOptimizedPooledMemoryManager(AcceleratorConfiguration config, ILoggerFactory loggerFactory)
    {
        // Use our new optimized memory pool
        var baseManager = CreateDirectMemoryManager(config, loggerFactory);
        // TODO: Production - Wrap with OptimizedMemoryPool implementation
        // Missing: Bucket-based pooling, lock-free allocation, automatic sizing
        return baseManager; // Placeholder - would wrap with OptimizedMemoryPool
    }

    private IUnifiedMemoryManager CreatePooledMemoryManager(AcceleratorConfiguration config, ILoggerFactory loggerFactory)
        // TODO: Production - Implement standard pooled memory manager
        // Missing: Pool size management, eviction policies, fragmentation handling


        => CreateDirectMemoryManager(config, loggerFactory); // Placeholder

    private IUnifiedMemoryManager CreateUnifiedMemoryManager(AcceleratorConfiguration config, ILoggerFactory loggerFactory)
        // TODO: Production - Implement unified memory manager for CUDA
        // Missing: cudaMallocManaged, prefetching, migration hints


        => CreateDirectMemoryManager(config, loggerFactory); // Placeholder

    [RequiresUnreferencedCode("Memory manager creation uses dynamic type loading")]
    private IUnifiedMemoryManager CreateDirectMemoryManager(AcceleratorConfiguration config, ILoggerFactory loggerFactory)
    {
        // For AOT compatibility, use factory method instead of dynamic type loading
        // This should be replaced with proper DI registration in production
        throw new NotSupportedException(
            "Dynamic memory manager creation is not supported in AOT scenarios. " +
            "Use dependency injection to register memory managers statically.");
    }

    private static AcceleratorConfiguration GetDefaultConfiguration(string backendName)
    {
        return backendName.ToUpperInvariant() switch
        {
            "CPU" => new AcceleratorConfiguration
            {
                MemoryStrategy = MemoryAllocationStrategy.OptimizedPooled,
                EnableNumaOptimization = true,
                PerformanceProfile = PerformanceProfile.Balanced
            },
            "CUDA" => new AcceleratorConfiguration
            {
                MemoryStrategy = MemoryAllocationStrategy.Unified,
                PerformanceProfile = PerformanceProfile.MaxPerformance
            },
            _ => new AcceleratorConfiguration()
        };
    }

    private static int CalculateWorkloadScore(AcceleratorType type, WorkloadProfile profile)
    {
        var score = 0;


        switch (type)
        {
            case AcceleratorType.CPU:
                score += profile.RequiresRealTime ? 10 : 0;
                score += !profile.IsComputeIntensive ? 5 : 0;
                score += profile.ExpectedParallelism < 16 ? 5 : 0;
                break;


            case AcceleratorType.CUDA:
                score += profile.IsComputeIntensive ? 10 : 0;
                score += profile.UsesMachineLearning ? 10 : 0;
                score += profile.ExpectedParallelism > 100 ? 10 : 0;
                score += profile.IsMemoryIntensive ? -5 : 0;
                break;


            case AcceleratorType.OpenCL:
                score += profile.IsComputeIntensive ? 8 : 0;
                score += profile.ExpectedParallelism > 50 ? 8 : 0;
                break;
        }

        // Apply preferred backend bonus

        if (profile.PreferredBackends.Contains(type.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            score += 20;
        }


        return score;
    }

    private static AcceleratorConfiguration CreateOptimizedConfiguration(
        AcceleratorType type,
        WorkloadProfile profile)
    {
        var config = GetDefaultConfiguration(type.ToString());

        // Optimize based on workload characteristics

        if (profile.RequiresRealTime)
        {
            config.PerformanceProfile = PerformanceProfile.LowLatency;
        }
        else if (profile.IsComputeIntensive)
        {
            config.PerformanceProfile = PerformanceProfile.MaxPerformance;
        }


        if (profile.IsMemoryIntensive)
        {
            config.MemoryStrategy = MemoryAllocationStrategy.OptimizedPooled;
        }


        return config;
    }

    private async ValueTask ConfigureAcceleratorAsync(
        IAccelerator accelerator,
        AcceleratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        // Apply post-creation configuration
        if (configuration.EnableProfiling)
        {
            _logger.LogDebugMessage("Enabling profiling for accelerator");
            // Enable profiling through accelerator-specific APIs
        }


        if (configuration.EnableDebugMode)
        {
            _logger.LogDebugMessage("Enabling debug mode for accelerator");
            // Enable debug mode through accelerator-specific APIs
        }


        await Task.CompletedTask;
    }

    private async ValueTask DiscoverDevicesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfoMessage("Discovering available accelerator devices");

        // Discover CPU devices (always available)

        var cpuInfo = new AcceleratorInfo(
            AcceleratorType.CPU,
            Environment.ProcessorCount + " Core CPU",
            Environment.Version.ToString(),
            GetSystemMemory());

        _ = _deviceCache.TryAdd("CPU_0", cpuInfo);

        // Discover other devices through backend-specific APIs
        // This would involve calling into CUDA, OpenCL, etc. APIs - TODO


        await Task.CompletedTask;
    }

    private static long GetSystemMemory()
        // TODO: Production - Implement proper system memory detection
        // Missing: Platform-specific memory queries (Windows WMI, Linux /proc/meminfo)


        => GC.GetTotalMemory(false) * 10; // Rough estimate

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _deviceDiscoverySemaphore?.Dispose();
            _backends.Clear();
            _deviceCache.Clear();
        }
    }
}
