// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Factories;
using DotCompute.Abstractions.Validation;
using DotCompute.Backends.CUDA.DeviceManagement;
using DotCompute.Backends.OpenCL.DeviceManagement;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotCompute.Backends.Metal.Native;

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
    /// <summary>
    /// Initializes a new instance of the DefaultAcceleratorFactory class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>

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
    /// <summary>
    /// Creates a new async.
    /// </summary>
    /// <param name="acceleratorInfo">The accelerator info.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created async.</returns>

    public async ValueTask<IAccelerator> CreateAsync(AcceleratorInfo acceleratorInfo, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceleratorInfo);
        serviceProvider ??= _serviceProvider;

        ObjectDisposedException.ThrowIf(_disposed, this);

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
            var accelerator = await provider.CreateAsync(acceleratorInfo, cancellationToken);

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
    /// <summary>
    /// Creates a new provider async.
    /// </summary>
    /// <typeparam name="TProvider">The TProvider type parameter.</typeparam>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created provider async.</returns>

    public async ValueTask<TProvider> CreateProviderAsync<TProvider>(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        where TProvider : class, IAcceleratorProvider
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        ObjectDisposedException.ThrowIf(_disposed, this);

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
    /// <summary>
    /// Determines whether create accelerator.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

    public bool CanCreateAccelerator(AcceleratorType acceleratorType)
    {
        return _providerTypes.ContainsKey(acceleratorType) ||
               _serviceProvider.GetServices<IAcceleratorProvider>()
                   .Any(p => p.SupportedTypes.Contains(acceleratorType));
    }
    /// <summary>
    /// Gets the supported types.
    /// </summary>
    /// <returns>The supported types.</returns>

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
    /// <summary>
    /// Creates a new async.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created async.</returns>

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
    /// <summary>
    /// Creates a new async.
    /// </summary>
    /// <param name="backendName">The backend name.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created async.</returns>

    public async ValueTask<IAccelerator> CreateAsync(string backendName, AcceleratorConfiguration? configuration = null, IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AcceleratorType>(backendName, true, out var type))
        {
            throw new ArgumentException($"Unknown backend name: {backendName}", nameof(backendName));
        }


        return await CreateAsync(type, configuration, serviceProvider, cancellationToken);
    }
    /// <summary>
    /// Gets the available types async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available types async.</returns>

    public async ValueTask<IReadOnlyList<AcceleratorType>> GetAvailableTypesAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return GetSupportedTypes().ToList();
    }
    /// <summary>
    /// Gets the available devices async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available devices async.</returns>

    public async ValueTask<IReadOnlyList<AcceleratorInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Keep async signature for future extensibility
        var devices = new List<AcceleratorInfo>();

        // 1. Enumerate CUDA devices
        try
        {
            var cudaLogger = _serviceProvider.GetService<ILogger<CudaDeviceManager>>();
            if (cudaLogger != null)
            {
                var cudaManager = new CudaDeviceManager(cudaLogger);
                foreach (var cudaDevice in cudaManager.Devices)
                {
                    devices.Add(MapCudaDeviceToAcceleratorInfo(cudaDevice));
                }
                _logger.LogDebugMessage($"Enumerated {cudaManager.Devices.Count} CUDA device(s)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebugMessage($"CUDA devices not available: {ex.Message}");
        }

        // 2. Enumerate OpenCL devices
        try
        {
            var openclLogger = _serviceProvider.GetService<ILogger<OpenCLDeviceManager>>();
            if (openclLogger != null)
            {
                var openclManager = new OpenCLDeviceManager(openclLogger);
                foreach (var openclDevice in openclManager.AllDevices)
                {
                    devices.Add(MapOpenCLDeviceToAcceleratorInfo(openclDevice));
                }
                _logger.LogDebugMessage($"Enumerated {openclManager.AllDevices.Count()} OpenCL device(s)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebugMessage($"OpenCL devices not available: {ex.Message}");
        }

        // 3. Enumerate Metal devices (macOS only)
        _logger.LogInfoMessage($"Checking Metal availability - macOS detected: {OperatingSystem.IsMacOS()}");
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                _logger.LogInfoMessage("Calling MetalNative.GetDeviceCount()...");
                var metalCount = MetalNative.GetDeviceCount();
                _logger.LogInfoMessage($"MetalNative.GetDeviceCount() returned: {metalCount}");

                for (int i = 0; i < metalCount; i++)
                {
                    _logger.LogDebugMessage($"Mapping Metal device {i}...");
                    devices.Add(MapMetalDeviceToAcceleratorInfo(i));
                }
                _logger.LogDebugMessage($"Successfully enumerated {metalCount} Metal device(s)");
            }
            catch (Exception ex)
            {
                _logger.LogInfoMessage($"Metal devices not available - Exception: {ex.GetType().Name}: {ex.Message}");
                _logger.LogInfoMessage($"Metal stack trace: {ex.StackTrace}");
            }
        }
        else
        {
            _logger.LogDebugMessage("Skipping Metal enumeration - not running on macOS");
        }

        // 4. Always add CPU device (always available)
        devices.Add(CreateCpuDeviceInfo());
        _logger.LogDebugMessage("Added CPU device");

        _logger.LogInfoMessage($"Total devices discovered: {devices.Count}");
        return devices;
    }
    /// <summary>
    /// Determines unregister provider.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <returns>The result of the operation.</returns>

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
    /// <summary>
    /// Performs register provider.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <param name="supportedTypes">The supported types.</param>

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
    /// <summary>
    /// Creates a new accelerator scope.
    /// </summary>
    /// <param name="acceleratorId">The accelerator identifier.</param>
    /// <returns>The created accelerator scope.</returns>

    public IServiceScope CreateAcceleratorScope(string acceleratorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceleratorId);

        ObjectDisposedException.ThrowIf(_disposed, this);

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
            return AcceleratorValidationResult.Failure([$"Validation error: {ex.Message}"]);
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
#pragma warning disable IL2090 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' - Factory requires runtime type instantiation
            var constructor = typeof(T).GetConstructor(Type.EmptyTypes);
#pragma warning restore IL2090
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

    #region Device Mapping Methods

    /// <summary>
    /// Maps CUDA device information to unified AcceleratorInfo.
    /// </summary>
    private static AcceleratorInfo MapCudaDeviceToAcceleratorInfo(DotCompute.Backends.CUDA.Models.CudaDeviceInfo cudaDevice)
    {
        return new AcceleratorInfo
        {
            Id = $"cuda_{cudaDevice.DeviceId}",
            Name = cudaDevice.Name,
            DeviceType = AcceleratorType.CUDA.ToString(),
            Vendor = "NVIDIA",
            DriverVersion = cudaDevice.ComputeCapability,
            ComputeCapability = new Version(cudaDevice.ComputeCapabilityMajor, cudaDevice.ComputeCapabilityMinor),
            TotalMemory = cudaDevice.TotalMemory,
            AvailableMemory = cudaDevice.TotalMemory, // Approximate
            MaxSharedMemoryPerBlock = cudaDevice.SharedMemoryPerBlock,
            MaxMemoryAllocationSize = cudaDevice.TotalMemory,
            LocalMemorySize = cudaDevice.SharedMemoryPerBlock,
            IsUnifiedMemory = cudaDevice.ManagedMemory,
            ComputeUnits = cudaDevice.MultiProcessorCount,
            MaxClockFrequency = cudaDevice.ClockRate / 1000, // Convert kHz to MHz
            MaxThreadsPerBlock = cudaDevice.MaxThreadsPerBlock,
            DeviceIndex = cudaDevice.DeviceId,
            MaxComputeUnits = cudaDevice.MultiProcessorCount,
            GlobalMemorySize = cudaDevice.TotalMemory,
            SupportsFloat64 = cudaDevice.ComputeCapabilityMajor >= 6, // CC 6.0+ has good FP64 support
            SupportsInt64 = true,
            Architecture = cudaDevice.Architecture,
            WarpSize = cudaDevice.WarpSize,
            Capabilities = new Dictionary<string, object>
            {
                ["WarpSize"] = cudaDevice.WarpSize,
                ["Architecture"] = cudaDevice.Architecture,
                ["MaxBlockDimX"] = cudaDevice.MaxBlockDimX,
                ["MaxBlockDimY"] = cudaDevice.MaxBlockDimY,
                ["MaxBlockDimZ"] = cudaDevice.MaxBlockDimZ,
                ["MaxGridDimX"] = cudaDevice.MaxGridDimX,
                ["MaxGridDimY"] = cudaDevice.MaxGridDimY,
                ["MaxGridDimZ"] = cudaDevice.MaxGridDimZ,
                ["MemoryBandwidth"] = cudaDevice.MemoryBandwidth,
                ["L2CacheSize"] = cudaDevice.L2CacheSize,
                ["TensorCoreCount"] = cudaDevice.TensorCoreCount,
                ["SupportsNVLink"] = cudaDevice.SupportsNVLink,
                ["PciInfo"] = cudaDevice.PciInfo
            }
        };
    }

    /// <summary>
    /// Maps OpenCL device information to unified AcceleratorInfo.
    /// </summary>
    private static AcceleratorInfo MapOpenCLDeviceToAcceleratorInfo(DotCompute.Backends.OpenCL.Models.OpenCLDeviceInfo openclDevice)
    {
        return new AcceleratorInfo
        {
            Id = $"opencl_{openclDevice.DeviceId.Handle:X}",
            Name = openclDevice.Name,
            DeviceType = AcceleratorType.OpenCL.ToString(),
            Vendor = openclDevice.Vendor,
            DriverVersion = openclDevice.DriverVersion,
            ComputeCapability = ParseOpenCLVersion(openclDevice.OpenCLVersion),
            TotalMemory = (long)openclDevice.GlobalMemorySize,
            AvailableMemory = (long)openclDevice.GlobalMemorySize, // Approximate
            MaxSharedMemoryPerBlock = (long)openclDevice.LocalMemorySize,
            MaxMemoryAllocationSize = (long)openclDevice.MaxMemoryAllocationSize,
            LocalMemorySize = (long)openclDevice.LocalMemorySize,
            IsUnifiedMemory = openclDevice.Type.HasFlag(DotCompute.Backends.OpenCL.Types.Native.DeviceType.CPU),
            ComputeUnits = (int)openclDevice.MaxComputeUnits,
            MaxClockFrequency = (int)openclDevice.MaxClockFrequency,
            MaxThreadsPerBlock = (int)openclDevice.MaxWorkGroupSize,
            DeviceIndex = 0, // OpenCL doesn't have a simple device index
            MaxComputeUnits = (int)openclDevice.MaxComputeUnits,
            GlobalMemorySize = (long)openclDevice.GlobalMemorySize,
            SupportsFloat64 = openclDevice.SupportsDoublePrecision,
            SupportsInt64 = true,
            Capabilities = new Dictionary<string, object>
            {
                ["MaxWorkItemDimensions"] = openclDevice.MaxWorkItemDimensions,
                ["MaxWorkItemSizes"] = openclDevice.MaxWorkItemSizes,
                ["ImageSupport"] = openclDevice.ImageSupport,
                ["MaxImage2DWidth"] = openclDevice.MaxImage2DWidth,
                ["MaxImage2DHeight"] = openclDevice.MaxImage2DHeight,
                ["MaxImage3DWidth"] = openclDevice.MaxImage3DWidth,
                ["MaxImage3DHeight"] = openclDevice.MaxImage3DHeight,
                ["MaxImage3DDepth"] = openclDevice.MaxImage3DDepth,
                ["Extensions"] = openclDevice.Extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ["EstimatedGFlops"] = openclDevice.EstimatedGFlops
            }
        };
    }

    /// <summary>
    /// Maps Metal device information to unified AcceleratorInfo.
    /// </summary>
    private static AcceleratorInfo MapMetalDeviceToAcceleratorInfo(int deviceIndex)
    {
        var devicePtr = MetalNative.CreateDeviceAtIndex(deviceIndex);
        if (devicePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to create Metal device at index {deviceIndex}");
        }

        try
        {
            var metalInfo = MetalNative.GetDeviceInfo(devicePtr);
            var deviceName = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(metalInfo.Name) ?? $"Metal Device {deviceIndex}";

            return new AcceleratorInfo
            {
                Id = $"metal_{metalInfo.RegistryID}",
                Name = deviceName,
                DeviceType = AcceleratorType.Metal.ToString(),
                Vendor = "Apple",
                DriverVersion = "Metal 3.0", // Placeholder
                TotalMemory = (long)metalInfo.RecommendedMaxWorkingSetSize,
                AvailableMemory = (long)metalInfo.RecommendedMaxWorkingSetSize,
                MaxSharedMemoryPerBlock = 32768, // Metal typical shared memory
                MaxMemoryAllocationSize = (long)metalInfo.MaxBufferLength,
                LocalMemorySize = 32768,
                IsUnifiedMemory = metalInfo.HasUnifiedMemory,
                ComputeUnits = 16, // Approximate for Apple Silicon
                MaxClockFrequency = 1000, // Approximate
                MaxThreadsPerBlock = (int)metalInfo.MaxThreadsPerThreadgroup,
                DeviceIndex = deviceIndex,
                MaxComputeUnits = 16,
                GlobalMemorySize = (long)metalInfo.RecommendedMaxWorkingSetSize,
                SupportsFloat64 = false, // Metal typically uses FP32
                SupportsInt64 = true,
                Capabilities = new Dictionary<string, object>
                {
                    ["MaxThreadsPerThreadgroup"] = metalInfo.MaxThreadsPerThreadgroup,
                    ["MaxThreadgroupSize"] = metalInfo.MaxThreadgroupSize,
                    ["MaxBufferLength"] = metalInfo.MaxBufferLength,
                    ["RecommendedMaxWorkingSetSize"] = metalInfo.RecommendedMaxWorkingSetSize,
                    ["HasUnifiedMemory"] = metalInfo.HasUnifiedMemory,
                    ["IsLowPower"] = metalInfo.IsLowPower,
                    ["IsRemovable"] = metalInfo.IsRemovable,
                    ["RegistryID"] = metalInfo.RegistryID,
                    ["Location"] = metalInfo.Location.ToString()
                }
            };
        }
        finally
        {
            MetalNative.ReleaseDevice(devicePtr);
        }
    }

    /// <summary>
    /// Creates AcceleratorInfo for the CPU backend.
    /// </summary>
    private static AcceleratorInfo CreateCpuDeviceInfo()
    {
        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var processorCount = Environment.ProcessorCount;

        return new AcceleratorInfo
        {
            Id = "cpu_0",
            Name = $"CPU ({processorCount} cores)",
            DeviceType = AcceleratorType.CPU.ToString(),
            Vendor = "System",
            DriverVersion = Environment.OSVersion.Version.ToString(),
            TotalMemory = totalMemory,
            AvailableMemory = totalMemory,
            MaxSharedMemoryPerBlock = totalMemory / 4,
            MaxMemoryAllocationSize = totalMemory,
            LocalMemorySize = totalMemory / 8,
            IsUnifiedMemory = true, // CPU always has unified memory
            ComputeUnits = processorCount,
            MaxClockFrequency = 3000, // Approximate, varies by CPU
            MaxThreadsPerBlock = processorCount, // Limited by thread count
            DeviceIndex = 0,
            MaxComputeUnits = processorCount,
            GlobalMemorySize = totalMemory,
            SupportsFloat64 = true, // CPUs support full double precision
            SupportsInt64 = true,
            ComputeCapability = new Version(1, 0),
            Capabilities = new Dictionary<string, object>
            {
                ["ProcessorCount"] = processorCount,
                ["OSVersion"] = Environment.OSVersion.VersionString,
                ["Is64BitProcess"] = Environment.Is64BitProcess,
                ["PageSize"] = Environment.SystemPageSize
            }
        };
    }

    /// <summary>
    /// Parses OpenCL version string to Version object.
    /// </summary>
    private static Version ParseOpenCLVersion(string versionString)
    {
        // OpenCL version format: "OpenCL 2.1 ..."
        try
        {
            var parts = versionString.Split(' ');
            if (parts.Length >= 2 && Version.TryParse(parts[1], out var version))
            {
                return version;
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return new Version(1, 0); // Default fallback
    }

    #endregion

    /// <summary>
    /// Performs dispose.
    /// </summary>

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
        GC.SuppressFinalize(this);
    }
}
