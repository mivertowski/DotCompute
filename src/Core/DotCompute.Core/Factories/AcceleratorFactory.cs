// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Factories;
using Microsoft.Extensions.Logging;

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
        
        _logger.LogInformation("Creating {Backend} accelerator", backendName);
        
        if (!_backends.TryGetValue(backendName, out var factoryMethod))
        {
            throw new NotSupportedException($"Backend '{backendName}' is not registered. Available backends: {string.Join(", ", _backends.Keys)}");
        }
        
        configuration ??= GetDefaultConfiguration(backendName);
        
        try
        {
            var accelerator = await factoryMethod(configuration, _loggerFactory).ConfigureAwait(false);
            
            _logger.LogInformation("Successfully created {Backend} accelerator", backendName);
            
            // Apply post-creation configuration
            await ConfigureAcceleratorAsync(accelerator, configuration, cancellationToken).ConfigureAwait(false);
            
            return accelerator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create {Backend} accelerator", backendName);
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
                    using var testAccelerator = await CreateAsync(type, new AcceleratorConfiguration { EnableDebugMode = false }, cancellationToken).ConfigureAwait(false);
                    availableTypes.Add(type);
                }
            }
            catch
            {
                // Backend not available on this system
                _logger.LogDebug("Backend {Backend} is not available on this system", backend);
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
            _deviceDiscoverySemaphore.Release();
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
        
        _logger.LogInformation("Registered backend: {Backend}", backendName);
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
        
        var provider = serviceProvider.GetService(typeof(TProvider)) as TProvider;
        if (provider == null)
        {
            throw new InvalidOperationException($"Provider type {typeof(TProvider).Name} is not registered in the service provider");
        }
        
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
                var provider = Activator.CreateInstance(providerType) as IAcceleratorProvider;
                if (provider == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of {providerType.Name}");
                }
                
                return await provider.CreateAcceleratorAsync(type, config, cancellationToken).ConfigureAwait(false);
            });
        }
        
        _logger.LogInformation("Registered provider {ProviderType} for types: {Types}",
            providerType.Name, string.Join(", ", supportedTypes));
    }
    
    /// <inheritdoc/>
    public bool UnregisterProvider(Type providerType)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(providerType);
        
        // For simplicity, we'll remove all backends that were registered with this provider type
        // In production, you'd track which backends are associated with which provider
        _logger.LogInformation("Unregistering provider {ProviderType}", providerType.Name);
        
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
        
        _logger.LogInformation("Selecting best accelerator for workload profile");
        
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
        _logger.LogInformation("Selected {AcceleratorType} as best for workload (score: {Score})",
            bestType, scores[bestType]);
        
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
            
            // Dynamic loading to avoid compile-time dependency
            var cpuType = Type.GetType("DotCompute.Backends.CPU.Accelerators.CpuAccelerator, DotCompute.Backends.CPU");
            if (cpuType == null)
            {
                throw new InvalidOperationException("CPU backend assembly not found");
            }
            
            var logger = loggerFactory.CreateLogger(cpuType);
            var memoryManager = CreateMemoryManager(config, loggerFactory);
            var context = new AcceleratorContext();
            var info = new AcceleratorInfo(AcceleratorType.CPU, "CPU", "1.0", GetSystemMemory());
            
            var instance = Activator.CreateInstance(cpuType, info, memoryManager, context, logger) as IAccelerator;
            return instance ?? throw new InvalidOperationException("Failed to create CPU accelerator");
        });
        
        // Register CUDA backend (conditional)
        RegisterBackend("CUDA", async (config, loggerFactory) =>
        {
            await Task.Yield(); // Ensure async context
            
            var cudaType = Type.GetType("DotCompute.Backends.CUDA.CudaAccelerator, DotCompute.Backends.CUDA");
            if (cudaType == null)
            {
                throw new InvalidOperationException("CUDA backend assembly not found");
            }
            
            // TODO: Production - Implement proper CUDA backend initialization
            // Missing: Device enumeration, capability detection, context creation
            // Missing: Multi-GPU support, P2P capability detection
            throw new NotImplementedException("CUDA backend initialization not yet implemented");
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
    {
        // TODO: Production - Implement standard pooled memory manager
        // Missing: Pool size management, eviction policies, fragmentation handling
        return CreateDirectMemoryManager(config, loggerFactory); // Placeholder
    }

    private IUnifiedMemoryManager CreateUnifiedMemoryManager(AcceleratorConfiguration config, ILoggerFactory loggerFactory)
    {
        // TODO: Production - Implement unified memory manager for CUDA
        // Missing: cudaMallocManaged, prefetching, migration hints
        return CreateDirectMemoryManager(config, loggerFactory); // Placeholder
    }

    private IUnifiedMemoryManager CreateDirectMemoryManager(AcceleratorConfiguration config, ILoggerFactory loggerFactory)
    {
        // Direct memory manager without pooling
        var type = Type.GetType("DotCompute.Memory.Internal.SimpleInMemoryManager, DotCompute.Memory");
        if (type == null)
        {
            throw new InvalidOperationException("Memory manager type not found");
        }
        
        var instance = Activator.CreateInstance(type) as IUnifiedMemoryManager;
        return instance ?? throw new InvalidOperationException("Failed to create memory manager");
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
            _logger.LogDebug("Enabling profiling for accelerator");
            // Enable profiling through accelerator-specific APIs
        }
        
        if (configuration.EnableDebugMode)
        {
            _logger.LogDebug("Enabling debug mode for accelerator");
            // Enable debug mode through accelerator-specific APIs
        }
        
        await Task.CompletedTask;
    }

    private async ValueTask DiscoverDevicesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discovering available accelerator devices");
        
        // Discover CPU devices (always available)
        var cpuInfo = new AcceleratorInfo(
            AcceleratorType.CPU,
            Environment.ProcessorCount + " Core CPU",
            Environment.Version.ToString(),
            GetSystemMemory());
        
        _deviceCache.TryAdd("CPU_0", cpuInfo);
        
        // Discover other devices through backend-specific APIs
        // This would involve calling into CUDA, OpenCL, etc. APIs
        
        await Task.CompletedTask;
    }

    private static long GetSystemMemory()
    {
        // TODO: Production - Implement proper system memory detection
        // Missing: Platform-specific memory queries (Windows WMI, Linux /proc/meminfo)
        return GC.GetTotalMemory(false) * 10; // Rough estimate
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

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