using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Advanced;
using DotCompute.Backends.CUDA.Analysis;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.DeviceManagement;
using DotCompute.Backends.CUDA.ErrorHandling;
using DotCompute.Backends.CUDA.Execution;
using DotCompute.Backends.CUDA.Graphs;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Models;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Optimization;
using DotCompute.Backends.CUDA.Profiling;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Core.System;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Factory
{
    /// <summary>
    /// Production-grade CUDA accelerator factory with comprehensive feature integration,
    /// dependency injection support, and intelligent resource management.
    /// </summary>
    public sealed class CudaAcceleratorFactory : IBackendFactory, IDisposable
    {
        private readonly ILogger<CudaAcceleratorFactory> _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly List<ProductionCudaAccelerator> _createdAccelerators;
        private readonly CudaDeviceManager _deviceManager;
        private readonly SystemInfoManager _systemInfoManager;
        private bool _disposed;

        public string Name => "CUDA Production";
        public string Description => "Production-Grade NVIDIA CUDA GPU Backend with Advanced Features";
        public Version Version => new(2, 0, 0);

        public CudaAcceleratorFactory(
            ILogger<CudaAcceleratorFactory>? logger = null,
            IServiceProvider? serviceProvider = null)
        {
            _logger = logger ?? new NullLogger<CudaAcceleratorFactory>();
            _serviceProvider = serviceProvider;
            _loggerFactory = serviceProvider?.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            _createdAccelerators = [];

            // Initialize core managers

            var deviceManagerLogger = _serviceProvider?.GetService<ILogger<CudaDeviceManager>>()

                ?? new NullLogger<CudaDeviceManager>();
            _deviceManager = new CudaDeviceManager(deviceManagerLogger);


            var systemInfoLogger = _serviceProvider?.GetService<ILogger<SystemInfoManager>>()

                ?? new NullLogger<SystemInfoManager>();
            _systemInfoManager = new SystemInfoManager(systemInfoLogger);


            _logger.LogInformation("Production CUDA Accelerator Factory initialized");
        }

        /// <summary>
        /// Creates a fully configured production accelerator with all features.
        /// </summary>
        public ProductionCudaAccelerator CreateProductionAccelerator(
            int deviceId,
            ProductionConfiguration? config = null)
        {
            config ??= ProductionConfiguration.Default;


            _logger.LogInformation(
                "Creating production CUDA accelerator for device {DeviceId} with config: {@Config}",
                deviceId, config);

            // Create accelerator with dependency injection
            var accelerator = _serviceProvider != null
                ? ActivatorUtilities.CreateInstance<ProductionCudaAccelerator>(_serviceProvider, deviceId, config)
                : CreateAcceleratorManually(deviceId, config);

            // Initialize all production features
            InitializeProductionFeatures(accelerator, config);


            _createdAccelerators.Add(accelerator);


            _logger.LogInformation(
                "Production accelerator created for device {DeviceId} with {FeatureCount} features enabled",
                deviceId, accelerator.EnabledFeatures.Count);


            return accelerator;
        }

        /// <summary>
        /// Checks if CUDA is available with detailed diagnostics.
        /// </summary>
        public bool IsAvailable()
        {
            try
            {
                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);


                if (result != CudaError.Success)
                {
                    _logger.LogWarning("CUDA runtime error: {Error}", CudaRuntime.GetErrorString(result));
                    return false;
                }

                if (deviceCount == 0)
                {
                    _logger.LogWarning("No CUDA devices found");
                    return false;
                }

                // Check driver version
                if (CudaRuntime.cudaDriverGetVersion(out var driverVersion) == CudaError.Success)
                {
                    _logger.LogInformation("CUDA Driver Version: {Version}", FormatCudaVersion(driverVersion));
                }

                // Check runtime version
                if (CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion) == CudaError.Success)
                {
                    _logger.LogInformation("CUDA Runtime Version: {Version}", FormatCudaVersion(runtimeVersion));
                }

                _logger.LogInformation("CUDA is available with {DeviceCount} device(s)", deviceCount);
                return true;
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError(ex, "CUDA runtime library not found");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking CUDA availability");
                return false;
            }
        }

        /// <summary>
        /// Creates all available accelerators with production configuration.
        /// </summary>
        public IEnumerable<IAccelerator> CreateAccelerators()
        {
            if (!IsAvailable())
            {
                _logger.LogWarning("CUDA not available, no accelerators created");
                yield break;
            }

            var devices = _deviceManager.Devices;
            _logger.LogInformation("Creating accelerators for {DeviceCount} CUDA devices", devices.Count);

            var accelerators = new List<ProductionCudaAccelerator>();

            // Create all accelerators first, collecting successes

            foreach (var device in devices)
            {
                ProductionCudaAccelerator? accelerator = null;
                try
                {
                    // Determine configuration based on device capabilities
                    var config = DetermineOptimalConfiguration(device);


                    accelerator = CreateProductionAccelerator(device.DeviceId, config);


                    _logger.LogInformation(
                        "Created production accelerator for {DeviceName} (CC {ComputeCapability})",
                        device.Name, $"{device.Major}.{device.Minor}");


                    accelerators.Add(accelerator);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,

                        "Failed to create accelerator for device {DeviceId}: {DeviceName}",
                        device.DeviceId, device.Name);


                    accelerator?.Dispose();
                }
            }

            // Now yield all successfully created accelerators

            foreach (var accelerator in accelerators)
            {
                yield return accelerator;
            }
        }

        /// <summary>
        /// Creates the default accelerator with auto-selected best device.
        /// </summary>
        public IAccelerator? CreateDefaultAccelerator()
        {
            if (!IsAvailable())
            {
                _logger.LogWarning("CUDA not available, cannot create default accelerator");
                return null;
            }

            try
            {
                // Select best device based on criteria
                var criteria = new DeviceSelectionCriteria
                {
                    PreferTensorCores = true,
                    MinComputeCapability = 60, // Pascal or newer
                    PreferLargestMemory = true
                };


                var bestDevice = _deviceManager.SelectBestDevice(criteria);


                _logger.LogInformation("Selected device {DeviceId} as default", bestDevice);


                var config = ProductionConfiguration.HighPerformance;
                return CreateProductionAccelerator(bestDevice, config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default accelerator");
                throw; // Re-throw to see the actual error
            }
        }

        /// <summary>
        /// Gets comprehensive backend capabilities.
        /// </summary>
        public BackendCapabilities GetCapabilities()
        {
            var capabilities = new BackendCapabilities
            {
                SupportsFloat16 = true,
                SupportsFloat32 = true,
                SupportsFloat64 = true,
                SupportsInt8 = true,
                SupportsInt16 = true,
                SupportsInt32 = true,
                SupportsInt64 = true,
                SupportsAsyncExecution = true,
                SupportsMultiDevice = true,
                SupportsUnifiedMemory = CheckUnifiedMemorySupport(),
                MaxDevices = _deviceManager.DeviceCount,
                SupportedFeatures = GetSupportedFeatures()
            };

            // Add system info
            var systemInfo = _systemInfoManager.GetMemoryInfo();
            capabilities.MaxMemory = systemInfo.TotalPhysicalMemory;


            return capabilities;
        }

        /// <summary>
        /// Creates accelerator manually without DI.
        /// </summary>
        private ProductionCudaAccelerator CreateAcceleratorManually(
            int deviceId,
            ProductionConfiguration config)
        {
            // Create CUDA context for the device
            CudaContext? context = null;
            try
            {
                context = new CudaContext(deviceId);

                // Create loggers using the logger factory
                var acceleratorLogger = _loggerFactory.CreateLogger<ProductionCudaAccelerator>();
                var streamLogger = _loggerFactory.CreateLogger<CudaStreamManagerProduction>();
                var memoryLogger = _loggerFactory.CreateLogger<CudaMemoryManager>();
                var errorLogger = _loggerFactory.CreateLogger<CudaErrorHandler>();
                var unifiedLogger = _loggerFactory.CreateLogger<CudaMemoryManager>();
                var tensorLogger = _loggerFactory.CreateLogger<CudaTensorCoreManagerProduction>();
                var kernelCacheLogger = _loggerFactory.CreateLogger<CudaKernelCache>();
                var graphLogger = _loggerFactory.CreateLogger<CudaGraphOptimizationManager>();
                var profilerLogger = _loggerFactory.CreateLogger<CudaPerformanceProfiler>();
                var occupancyLogger = _loggerFactory.CreateLogger<CudaOccupancyCalculator>();
                var coalescingLogger = _loggerFactory.CreateLogger<CudaMemoryCoalescingAnalyzer>();

                // Create managers with all required parameters
                var streamManager = new CudaStreamManagerProduction(context, streamLogger);
                var memoryManager = new CudaMemoryManager(context, memoryLogger);
                var errorHandler = new CudaErrorHandler(errorLogger);
                var unifiedMemoryManager = new CudaMemoryManager(context, null, unifiedLogger);
                var tensorCoreManager = new CudaTensorCoreManagerProduction(context, _deviceManager, tensorLogger);
                var kernelCache = new CudaKernelCache(kernelCacheLogger);
                var graphOptimizer = new CudaGraphOptimizationManager(graphLogger);
                var profiler = new CudaPerformanceProfiler(profilerLogger);
                var occupancyCalculator = new CudaOccupancyCalculator(occupancyLogger);
                var coalescingAnalyzer = new CudaMemoryCoalescingAnalyzer(coalescingLogger);

                return new ProductionCudaAccelerator(
                    deviceId,
                    config,
                    acceleratorLogger,
                    streamManager,
                    memoryManager,
                    errorHandler,
                    unifiedMemoryManager,
                    tensorCoreManager,
                    kernelCache,
                    graphOptimizer,
                    profiler,
                    occupancyCalculator,
                    coalescingAnalyzer,
                    _deviceManager,
                    _systemInfoManager);
            }
            catch
            {
                // Clean up context on error
                context?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Initializes all production features on the accelerator.
        /// </summary>
        private void InitializeProductionFeatures(
            ProductionCudaAccelerator accelerator,
            ProductionConfiguration config)
        {
            var enabledFeatures = new List<string>();

            // Initialize stream management
            if (config.EnableStreamManagement)
            {
                enabledFeatures.Add("Stream Management");
            }

            // Initialize memory features
            if (config.EnableAsyncMemory)
            {
                enabledFeatures.Add("Async Memory");
            }

            if (config.EnableUnifiedMemory)
            {
                enabledFeatures.Add("Unified Memory");
            }

            // Initialize optimization features
            if (config.EnableGraphOptimization)
            {
                enabledFeatures.Add("Graph Optimization");
            }

            if (config.EnableKernelCaching)
            {
                enabledFeatures.Add("Kernel Caching");
            }

            // Initialize tensor cores if available
            var device = _deviceManager.GetDevice(accelerator.DeviceId);
            if (device.ComputeCapabilityMajor >= 7 && config.EnableTensorCores)
            {
                enabledFeatures.Add("Tensor Cores");
            }

            // Initialize profiling if requested
            if (config.EnableProfiling)
            {
                accelerator.Profiler.StartProfilingAsync().Wait();
                enabledFeatures.Add("Performance Profiling");
            }

            // Enable P2P if multiple GPUs
            if (_deviceManager.DeviceCount > 1 && config.EnableP2P)
            {
                EnableP2PSupport(accelerator.DeviceId);
                enabledFeatures.Add("P2P Memory Access");
            }

            accelerator.EnabledFeatures = enabledFeatures;


            _logger.LogInformation(
                "Initialized {FeatureCount} production features for device {DeviceId}: {Features}",
                enabledFeatures.Count,
                accelerator.DeviceId,
                string.Join(", ", enabledFeatures));
        }

        /// <summary>
        /// Determines optimal configuration based on device capabilities.
        /// </summary>
        private ProductionConfiguration DetermineOptimalConfiguration(CudaDeviceInfo device)
        {
            // Start with default configuration
            _ = ProductionConfiguration.Default;
            ProductionConfiguration? config;

            // Adjust based on compute capability
            if (device.Major >= 7) // Volta and newer
            {
                config = ProductionConfiguration.HighPerformance;
                config.EnableTensorCores = true;
                config.EnableGraphOptimization = true;
            }
            else if (device.Major >= 6) // Pascal
            {
                config = ProductionConfiguration.Balanced;
                config.EnableUnifiedMemory = true;
            }
            else // Older architectures
            {
                config = ProductionConfiguration.Compatible;
                config.EnableTensorCores = false;
            }

            // Adjust based on memory
            if (device.TotalGlobalMemory > 8L * 1024 * 1024 * 1024) // > 8GB
            {
                config.EnableKernelCaching = true;
                config.KernelCacheSize = 512 * 1024 * 1024; // 512MB cache
            }

            // Enable P2P if NVLink available
            if (device.SupportsNVLink)
            {
                config.EnableP2P = true;
            }

            _logger.LogDebug(
                "Determined configuration for {DeviceName}: TensorCores={TC}, Graphs={Graph}, P2P={P2P}",
                device.Name,
                config.EnableTensorCores,
                config.EnableGraphOptimization,
                config.EnableP2P);

            return config;
        }

        /// <summary>
        /// Enables P2P memory access between devices.
        /// </summary>
        private void EnableP2PSupport(int deviceId)
        {
            try
            {
                for (var peer = 0; peer < _deviceManager.DeviceCount; peer++)
                {
                    if (peer != deviceId && _deviceManager.CanAccessPeer(deviceId, peer))
                    {
                        _deviceManager.EnablePeerAccess(deviceId, peer);
                        _logger.LogInformation(
                            "Enabled P2P access from device {Source} to device {Target}",
                            deviceId, peer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enable P2P for device {DeviceId}", deviceId);
            }
        }

        /// <summary>
        /// Checks if unified memory is supported.
        /// </summary>
        private bool CheckUnifiedMemorySupport()
        {
            try
            {
                if (!IsAvailable())
                {
                    return false;
                }


                var devices = _deviceManager.Devices;
                return devices.Any(d => d.Major >= 3); // Kepler or newer
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets list of supported features.
        /// </summary>
        private List<string> GetSupportedFeatures()
        {
            var features = new List<string>
            {
                "Stream Management",
                "Async Memory Operations",
                "Error Recovery",
                "Memory Pooling",
                "CUDA Graphs",
                "Kernel Caching",
                "Performance Profiling",
                "Occupancy Calculation",
                "Memory Coalescing Analysis"
            };

            // Check for advanced features
            var devices = _deviceManager.Devices.ToList();
            if (devices.Any(d => d.Major >= 3))
            {
                features.Add("Unified Memory");
                features.Add("Dynamic Parallelism");
            }


            if (devices.Any(d => d.TensorCoreCount > 0))
            {
                features.Add("Tensor Cores");
                features.Add("Mixed Precision");
            }


            if (devices.Any(d => d.SupportsNVLink))
            {
                features.Add("NVLink");
                features.Add("P2P Memory Access");
            }


            if (devices.Count() > 1)
            {
                features.Add("Multi-GPU");
            }

            return features;
        }

        /// <summary>
        /// Formats CUDA version number.
        /// </summary>
        private static string FormatCudaVersion(int version)
        {
            var major = version / 1000;
            var minor = (version % 1000) / 10;
            return $"{major}.{minor}";
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _logger.LogInformation("Disposing Production CUDA Factory");

            // Dispose all created accelerators
            foreach (var accelerator in _createdAccelerators)
            {
                try
                {
                    accelerator.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing accelerator for device {DeviceId}",

                        accelerator.DeviceId);
                }
            }


            _createdAccelerators.Clear();

            // Dispose managers
            _deviceManager?.Dispose();
            if (_systemInfoManager is IDisposable disposableSystemInfo)
            {
                disposableSystemInfo.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Production configuration for CUDA accelerators.
        /// </summary>
        public class ProductionConfiguration
        {
            public bool EnableStreamManagement { get; set; } = true;
            public bool EnableAsyncMemory { get; set; } = true;
            public bool EnableUnifiedMemory { get; set; } = true;
            public bool EnableTensorCores { get; set; } = true;
            public bool EnableGraphOptimization { get; set; } = true;
            public bool EnableKernelCaching { get; set; } = true;
            public bool EnableP2P { get; set; }

            public bool EnableProfiling { get; set; }

            public bool EnableErrorRecovery { get; set; } = true;


            public int StreamPoolSize { get; set; } = 4;
            public int MaxStreams { get; set; } = 32;
            public string KernelCacheDirectory { get; set; } = ".cuda_cache";
            public long KernelCacheSize { get; set; } = 256 * 1024 * 1024; // 256MB


            public static ProductionConfiguration Default => new();


            public static ProductionConfiguration HighPerformance => new()
            {
                EnableStreamManagement = true,
                EnableAsyncMemory = true,
                EnableUnifiedMemory = true,
                EnableTensorCores = true,
                EnableGraphOptimization = true,
                EnableKernelCaching = true,
                EnableP2P = true,
                StreamPoolSize = 8,
                MaxStreams = 64
            };


            public static ProductionConfiguration Balanced => new()
            {
                EnableStreamManagement = true,
                EnableAsyncMemory = true,
                EnableUnifiedMemory = true,
                EnableTensorCores = true,
                EnableGraphOptimization = false,
                EnableKernelCaching = true,
                StreamPoolSize = 4,
                MaxStreams = 32
            };


            public static ProductionConfiguration Compatible => new()
            {
                EnableStreamManagement = true,
                EnableAsyncMemory = false,
                EnableUnifiedMemory = false,
                EnableTensorCores = false,
                EnableGraphOptimization = false,
                EnableKernelCaching = false,
                StreamPoolSize = 2,
                MaxStreams = 16
            };
        }

        /// <summary>
        /// Production CUDA accelerator with all advanced features.
        /// </summary>
        public class ProductionCudaAccelerator : IAccelerator, IDisposable, IAsyncDisposable
        {
            private readonly CudaAccelerator _baseAccelerator;
            private readonly Lazy<IUnifiedMemoryManager> _memoryAdapter;


            public CudaStreamManagerProduction StreamManager { get; }
            public CudaMemoryManager AsyncMemoryManager { get; }
            public CudaErrorHandler ErrorHandler { get; }
            public CudaMemoryManager UnifiedMemoryManager { get; }
            public CudaTensorCoreManagerProduction TensorCoreManager { get; }
            public CudaKernelCache KernelCache { get; }
            public CudaGraphOptimizationManager GraphOptimizer { get; }
            public CudaPerformanceProfiler Profiler { get; }
            public CudaOccupancyCalculator OccupancyCalculator { get; }
            public CudaMemoryCoalescingAnalyzer CoalescingAnalyzer { get; }
            public CudaDeviceManager DeviceManager { get; }
            public SystemInfoManager SystemInfoManager { get; }
            public ProductionConfiguration Configuration { get; }
            public List<string> EnabledFeatures { get; set; } = [];

            public ProductionCudaAccelerator(
                int deviceId,
                ProductionConfiguration config,
                ILogger<ProductionCudaAccelerator> logger,
                CudaStreamManagerProduction streamManager,
                CudaMemoryManager asyncMemoryManager,
                CudaErrorHandler errorHandler,
                CudaMemoryManager unifiedMemoryManager,
                CudaTensorCoreManagerProduction tensorCoreManager,
                CudaKernelCache kernelCache,
                CudaGraphOptimizationManager graphOptimizer,
                CudaPerformanceProfiler profiler,
                CudaOccupancyCalculator occupancyCalculator,
                CudaMemoryCoalescingAnalyzer coalescingAnalyzer,
                CudaDeviceManager deviceManager,
                SystemInfoManager systemInfoManager)
            {
                Configuration = config ?? throw new ArgumentNullException(nameof(config));
                StreamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
                AsyncMemoryManager = asyncMemoryManager ?? throw new ArgumentNullException(nameof(asyncMemoryManager));
                ErrorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
                UnifiedMemoryManager = unifiedMemoryManager ?? throw new ArgumentNullException(nameof(unifiedMemoryManager));
                TensorCoreManager = tensorCoreManager ?? throw new ArgumentNullException(nameof(tensorCoreManager));
                KernelCache = kernelCache ?? throw new ArgumentNullException(nameof(kernelCache));
                GraphOptimizer = graphOptimizer ?? throw new ArgumentNullException(nameof(graphOptimizer));
                Profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
                OccupancyCalculator = occupancyCalculator ?? throw new ArgumentNullException(nameof(occupancyCalculator));
                CoalescingAnalyzer = coalescingAnalyzer ?? throw new ArgumentNullException(nameof(coalescingAnalyzer));
                DeviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
                SystemInfoManager = systemInfoManager ?? throw new ArgumentNullException(nameof(systemInfoManager));
                DeviceId = deviceId;

                // Create base accelerator for delegation
                // Use NullLogger to avoid type conversion issues

                _baseAccelerator = new CudaAccelerator(deviceId, NullLogger<CudaAccelerator>.Instance);

                // Initialize memory adapter with lazy loading to avoid multiple instances

                _memoryAdapter = new Lazy<IUnifiedMemoryManager>(() => new Memory.CudaAsyncMemoryManagerAdapter(UnifiedMemoryManager));
            }

            // IAccelerator interface implementation
            public AcceleratorType Type => AcceleratorType.CUDA;
            public AcceleratorInfo Info => new()

            {
                Id = $"cuda_{DeviceId}",
                Name = $"CUDA Device {DeviceId}",
                DeviceType = AcceleratorType.CUDA.ToString(),
                Vendor = "NVIDIA",
                DriverVersion = "12.0", // Default version
                MaxComputeUnits = 108, // Default SM count
                MaxWorkGroupSize = 1024,
                GlobalMemorySize = (long)UnifiedMemoryManager.TotalAvailableMemory,
                LocalMemorySize = 49152, // 48KB shared memory per block
                SupportsFloat64 = true,
                SupportsInt64 = true
            };
            public IUnifiedMemoryManager Memory => _memoryAdapter.Value;
            public AcceleratorContext Context { get; private set; }

            public async ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
                => await _baseAccelerator.CompileKernelAsync(definition, options, cancellationToken);

            public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => await StreamManager.SynchronizeAsync(cancellationToken);

            public int DeviceId { get; private set; }

            public void Dispose()
            {
                // Dispose managers in reverse order
                Profiler?.Dispose();
                GraphOptimizer?.Dispose();
                KernelCache?.Dispose();
                TensorCoreManager?.Dispose();
                UnifiedMemoryManager?.Dispose();
                ErrorHandler?.Dispose();
                AsyncMemoryManager?.Dispose();
                StreamManager?.Dispose();

                // Dispose base accelerator asynchronously

                if (_baseAccelerator != null)
                {
                    _baseAccelerator.DisposeAsync().AsTask().Wait();
                }
            }

            public async ValueTask DisposeAsync()
            {
                // Dispose async-capable managers first
                if (_baseAccelerator != null)
                {
                    await _baseAccelerator.DisposeAsync().ConfigureAwait(false);
                }

                // Dispose remaining managers that only support sync disposal

                Profiler?.Dispose();
                GraphOptimizer?.Dispose();
                KernelCache?.Dispose();
                TensorCoreManager?.Dispose();
                UnifiedMemoryManager?.Dispose();
                ErrorHandler?.Dispose();

                // Handle async disposal for memory and stream managers if they support it

                if (AsyncMemoryManager is IDisposable syncMemoryManager)
                {
                    syncMemoryManager.Dispose();
                }
                else
                {
                    AsyncMemoryManager?.Dispose();
                }


                StreamManager?.Dispose();


                GC.SuppressFinalize(this);
            }
        }
    }
}
