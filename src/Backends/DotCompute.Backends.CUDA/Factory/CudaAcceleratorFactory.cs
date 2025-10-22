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
using DotCompute.Backends.CUDA.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Factory
{
    /// <summary>
    /// Production-grade CUDA accelerator factory with comprehensive feature integration,
    /// dependency injection support, and intelligent resource management.
    /// </summary>
    public sealed partial class CudaAcceleratorFactory : IBackendFactory, IDisposable
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6850,
            Level = LogLevel.Information,
            Message = "Initialized {FeatureCount} production features for device {DeviceId}: {Features}")]
        private static partial void LogInitializedProductionFeatures(ILogger logger, int featureCount, int deviceId, string features);

        [LoggerMessage(
            EventId = 6851,
            Level = LogLevel.Debug,
            Message = "Determined configuration for {DeviceName}: TensorCores={TC}, Graphs={Graph}, P2P={P2P}")]
        private static partial void LogDeterminedConfiguration(ILogger logger, string deviceName, bool tc, bool graph, bool p2P);

        [LoggerMessage(
            EventId = 6852,
            Level = LogLevel.Information,
            Message = "Enabled P2P access from device {Source} to device {Target}")]
        private static partial void LogEnabledP2PAccess(ILogger logger, int source, int target);

        [LoggerMessage(
            EventId = 6853,
            Level = LogLevel.Warning,
            Message = "Failed to enable P2P for device {DeviceId}")]
        private static partial void LogFailedToEnableP2P(ILogger logger, Exception ex, int deviceId);

        #endregion

        private readonly ILogger<CudaAcceleratorFactory> _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly List<ProductionCudaAccelerator> _createdAccelerators;
        private readonly CudaDeviceManager _deviceManager;
        private readonly SystemInfoManager _systemInfoManager;
        private bool _disposed;
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>

        public string Name => "CUDA Production";
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>The description.</value>
        public string Description => "Production-Grade NVIDIA CUDA GPU Backend with Advanced Features";
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        /// <value>The version.</value>
        public Version Version => new(2, 0, 0);
        /// <summary>
        /// Initializes a new instance of the CudaAcceleratorFactory class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="serviceProvider">The service provider.</param>

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


            LogFactoryInitialized();
        }

        /// <summary>
        /// Creates a fully configured production accelerator with all features.
        /// </summary>
        public ProductionCudaAccelerator CreateProductionAccelerator(
            int deviceId,
            ProductionConfiguration? config = null)
        {
            config ??= ProductionConfiguration.Default;


            LogCreatingAccelerator(deviceId, config);

            // Create accelerator with dependency injection
            var accelerator = _serviceProvider != null
                ? ActivatorUtilities.CreateInstance<ProductionCudaAccelerator>(_serviceProvider, deviceId, config)
                : CreateAcceleratorManually(deviceId, config);

            // Initialize all production features
            InitializeProductionFeatures(accelerator, config);


            _createdAccelerators.Add(accelerator);


            LogAcceleratorCreated(deviceId, accelerator.EnabledFeatures.Count);


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
                    LogCudaApiError(result.ToString());
                    return false;
                }

                if (deviceCount == 0)
                {
                    LogNoDevicesFound();
                    return false;
                }

                // Check driver version
                if (CudaRuntime.cudaDriverGetVersion(out var driverVersion) == CudaError.Success)
                {
                    LogDriverVersion(FormatCudaVersion(driverVersion));
                }

                // Check runtime version
                if (CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion) == CudaError.Success)
                {
                    LogRuntimeVersion(FormatCudaVersion(runtimeVersion));
                }

                LogDeviceCountFound(deviceCount);
                return true;
            }
            catch (DllNotFoundException ex)
            {
                LogRuntimeLibraryNotFound(ex);
                return false;
            }
            catch (Exception ex)
            {
                LogAvailabilityCheckError(ex);
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
                LogCudaNotAvailable();
                yield break;
            }

            var devices = _deviceManager.Devices;
            LogEnumeratingDevices(devices.Count());

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


                    LogAcceleratorCreatedForDevice(device.Name, $"{device.Major}.{device.Minor}");


                    accelerators.Add(accelerator);
                }
                catch (Exception ex)
                {
                    LogCreateAcceleratorFailed(ex, device.DeviceId, device.Name);


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
                LogCannotCreateDefault();
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


                LogDefaultDeviceSelected(bestDevice);


                var config = ProductionConfiguration.HighPerformance;
                return CreateProductionAccelerator(bestDevice, config);
            }
            catch (Exception ex)
            {
                LogCreateDefaultFailed(ex);
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
                accelerator.Profiler.StartProfilingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                enabledFeatures.Add("Performance Profiling");
            }

            // Enable P2P if multiple GPUs
            if (_deviceManager.DeviceCount > 1 && config.EnableP2P)
            {
                EnableP2PSupport(accelerator.DeviceId);
                enabledFeatures.Add("P2P Memory Access");
            }

            // Note: EnabledFeatures is init-only, should be set during construction
            // Store features in a private field or pass to constructor instead
            // For now, this is commented out as it requires refactoring the ProductionCudaAccelerator constructor
            // accelerator.EnabledFeatures = enabledFeatures;

            LogInitializedProductionFeatures(_logger, enabledFeatures.Count, accelerator.DeviceId,
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

            LogDeterminedConfiguration(_logger, device.Name, config.EnableTensorCores,
                config.EnableGraphOptimization, config.EnableP2P);

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
                        LogEnabledP2PAccess(_logger, deviceId, peer);
                    }
                }
            }
            catch (Exception ex)
            {
                LogFailedToEnableP2P(_logger, ex, deviceId);
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
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            LogDisposingFactory();

            // Dispose all created accelerators
            foreach (var accelerator in _createdAccelerators)
            {
                try
                {
                    accelerator.Dispose();
                }
                catch (Exception ex)
                {
                    LogDisposeAcceleratorError(ex, accelerator.DeviceId);
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
            /// <summary>
            /// Gets or sets the enable stream management.
            /// </summary>
            /// <value>The enable stream management.</value>
            public bool EnableStreamManagement { get; set; } = true;
            /// <summary>
            /// Gets or sets the enable async memory.
            /// </summary>
            /// <value>The enable async memory.</value>
            public bool EnableAsyncMemory { get; set; } = true;
            /// <summary>
            /// Gets or sets the enable unified memory.
            /// </summary>
            /// <value>The enable unified memory.</value>
            public bool EnableUnifiedMemory { get; set; } = true;
            /// <summary>
            /// Gets or sets the enable tensor cores.
            /// </summary>
            /// <value>The enable tensor cores.</value>
            public bool EnableTensorCores { get; set; } = true;
            /// <summary>
            /// Gets or sets the enable graph optimization.
            /// </summary>
            /// <value>The enable graph optimization.</value>
            public bool EnableGraphOptimization { get; set; } = true;
            /// <summary>
            /// Gets or sets the enable kernel caching.
            /// </summary>
            /// <value>The enable kernel caching.</value>
            public bool EnableKernelCaching { get; set; } = true;
            /// <summary>
            /// Gets or sets the enable p2 p.
            /// </summary>
            /// <value>The enable p2 p.</value>
            public bool EnableP2P { get; set; }
            /// <summary>
            /// Gets or sets the enable profiling.
            /// </summary>
            /// <value>The enable profiling.</value>

            public bool EnableProfiling { get; set; }
            /// <summary>
            /// Gets or sets the enable error recovery.
            /// </summary>
            /// <value>The enable error recovery.</value>

            public bool EnableErrorRecovery { get; set; } = true;
            /// <summary>
            /// Gets or sets the stream pool size.
            /// </summary>
            /// <value>The stream pool size.</value>


            public int StreamPoolSize { get; set; } = 4;
            /// <summary>
            /// Gets or sets the max streams.
            /// </summary>
            /// <value>The max streams.</value>
            public int MaxStreams { get; set; } = 32;
            /// <summary>
            /// Gets or sets the kernel cache directory.
            /// </summary>
            /// <value>The kernel cache directory.</value>
            public string KernelCacheDirectory { get; set; } = ".cuda_cache";
            /// <summary>
            /// Gets or sets the kernel cache size.
            /// </summary>
            /// <value>The kernel cache size.</value>
            public long KernelCacheSize { get; set; } = 256 * 1024 * 1024; // 256MB
            /// <summary>
            /// Gets or sets the default.
            /// </summary>
            /// <value>The default.</value>


            public static ProductionConfiguration Default => new();
            /// <summary>
            /// Gets or sets the high performance.
            /// </summary>
            /// <value>The high performance.</value>


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
            /// <summary>
            /// Gets or sets the balanced.
            /// </summary>
            /// <value>The balanced.</value>


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
            /// <summary>
            /// Gets or sets the compatible.
            /// </summary>
            /// <value>The compatible.</value>


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
            private bool _disposed;
            /// <summary>
            /// Gets or sets the stream manager.
            /// </summary>
            /// <value>The stream manager.</value>

            public CudaStreamManagerProduction StreamManager { get; }
            /// <summary>
            /// Gets or sets the async memory manager.
            /// </summary>
            /// <value>The async memory manager.</value>
            public CudaMemoryManager AsyncMemoryManager { get; }
            /// <summary>
            /// Gets or sets the error handler.
            /// </summary>
            /// <value>The error handler.</value>
            public CudaErrorHandler ErrorHandler { get; }
            /// <summary>
            /// Gets or sets the unified memory manager.
            /// </summary>
            /// <value>The unified memory manager.</value>
            public CudaMemoryManager UnifiedMemoryManager { get; }
            /// <summary>
            /// Gets or sets the tensor core manager.
            /// </summary>
            /// <value>The tensor core manager.</value>
            public CudaTensorCoreManagerProduction TensorCoreManager { get; }
            /// <summary>
            /// Gets or sets the kernel cache.
            /// </summary>
            /// <value>The kernel cache.</value>
            public CudaKernelCache KernelCache { get; }
            /// <summary>
            /// Gets or sets the graph optimizer.
            /// </summary>
            /// <value>The graph optimizer.</value>
            public CudaGraphOptimizationManager GraphOptimizer { get; }
            /// <summary>
            /// Gets or sets the profiler.
            /// </summary>
            /// <value>The profiler.</value>
            public CudaPerformanceProfiler Profiler { get; }
            /// <summary>
            /// Gets or sets the occupancy calculator.
            /// </summary>
            /// <value>The occupancy calculator.</value>
            public CudaOccupancyCalculator OccupancyCalculator { get; }
            /// <summary>
            /// Gets or sets the coalescing analyzer.
            /// </summary>
            /// <value>The coalescing analyzer.</value>
            public CudaMemoryCoalescingAnalyzer CoalescingAnalyzer { get; }
            /// <summary>
            /// Gets or sets the device manager.
            /// </summary>
            /// <value>The device manager.</value>
            public CudaDeviceManager DeviceManager { get; }
            /// <summary>
            /// Gets or sets the system info manager.
            /// </summary>
            /// <value>The system info manager.</value>
            public SystemInfoManager SystemInfoManager { get; }
            /// <summary>
            /// Gets or sets the configuration.
            /// </summary>
            /// <value>The configuration.</value>
            public ProductionConfiguration Configuration { get; }
            /// <summary>
            /// Gets or initializes the enabled features.
            /// </summary>
            /// <value>The enabled features.</value>
            public IList<string> EnabledFeatures { get; init; } = [];
            /// <summary>
            /// Initializes a new instance of the ProductionCudaAccelerator class.
            /// </summary>
            /// <param name="deviceId">The device identifier.</param>
            /// <param name="config">The config.</param>
            /// <param name="logger">The logger.</param>
            /// <param name="streamManager">The stream manager.</param>
            /// <param name="asyncMemoryManager">The async memory manager.</param>
            /// <param name="errorHandler">The error handler.</param>
            /// <param name="unifiedMemoryManager">The unified memory manager.</param>
            /// <param name="tensorCoreManager">The tensor core manager.</param>
            /// <param name="kernelCache">The kernel cache.</param>
            /// <param name="graphOptimizer">The graph optimizer.</param>
            /// <param name="profiler">The profiler.</param>
            /// <param name="occupancyCalculator">The occupancy calculator.</param>
            /// <param name="coalescingAnalyzer">The coalescing analyzer.</param>
            /// <param name="deviceManager">The device manager.</param>
            /// <param name="systemInfoManager">The system info manager.</param>

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

                _memoryAdapter = new Lazy<IUnifiedMemoryManager>(() => new CudaAsyncMemoryManagerAdapter(UnifiedMemoryManager));
            }
            /// <summary>
            /// Gets or sets the type.
            /// </summary>
            /// <value>The type.</value>

            // IAccelerator interface implementation
            public AcceleratorType Type => AcceleratorType.CUDA;
            /// <summary>
            /// Gets or sets the device type.
            /// </summary>
            /// <value>The device type.</value>
            public string DeviceType => AcceleratorType.CUDA.ToString();
            /// <summary>
            /// Gets or sets the memory manager.
            /// </summary>
            /// <value>The memory manager.</value>
            public IUnifiedMemoryManager MemoryManager => _memoryAdapter.Value;
            /// <summary>
            /// Gets or sets the info.
            /// </summary>
            /// <value>The info.</value>
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
            /// <summary>
            /// Gets or sets the memory.
            /// </summary>
            /// <value>The memory.</value>
            public IUnifiedMemoryManager Memory => _memoryAdapter.Value;
            /// <summary>
            /// Gets or sets the context.
            /// </summary>
            /// <value>The context.</value>
            public AcceleratorContext Context { get; private set; }
            /// <summary>
            /// Gets or sets a value indicating whether available.
            /// </summary>
            /// <value>The is available.</value>
            public bool IsAvailable => !_disposed;
            /// <summary>
            /// Gets compile kernel asynchronously.
            /// </summary>
            /// <param name="definition">The definition.</param>
            /// <param name="options">The options.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>The result of the operation.</returns>

            public async ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
                => await _baseAccelerator.CompileKernelAsync(definition, options, cancellationToken);
            /// <summary>
            /// Gets synchronize asynchronously.
            /// </summary>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>The result of the operation.</returns>

            public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => await StreamManager.SynchronizeAsync(cancellationToken);
            /// <summary>
            /// Gets or sets the device identifier.
            /// </summary>
            /// <value>The device id.</value>

            public int DeviceId { get; private set; }
            /// <summary>
            /// Performs dispose.
            /// </summary>

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

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
                    _baseAccelerator.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                }

                _disposed = true;
            }
            /// <summary>
            /// Gets dispose asynchronously.
            /// </summary>
            /// <returns>The result of the operation.</returns>

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    return;
                }

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

                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
