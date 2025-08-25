// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - AOT plugin registry uses dynamic logging

namespace DotCompute.Plugins.Core
{

    /// <summary>
    /// AOT-compatible plugin registry that avoids reflection-based activation.
    /// Replaces the dynamic plugin system with static registration.
    /// </summary>
    public sealed class AotPluginRegistry : IDisposable
    {
        private readonly ILogger<AotPluginRegistry> _logger;
        private readonly Dictionary<string, IBackendPlugin> _plugins;
        private readonly Dictionary<string, Func<IBackendPlugin>> _factories;
        private readonly Lock _lock = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AotPluginRegistry"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">logger</exception>
        public AotPluginRegistry(ILogger<AotPluginRegistry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugins = [];
            _factories = [];

            RegisterKnownPlugins();
        }

        /// <summary>
        /// Registers all known plugin types for AOT compatibility.
        /// This replaces runtime plugin discovery with compile-time registration.
        /// </summary>
        private void RegisterKnownPlugins()
        {
            _logger.LogInformation("Registering known plugins for AOT compatibility");

            // Register CPU backend
            _factories["DotCompute.Backends.CPU"] = () =>
            {
                _logger.LogDebug("Creating CPU backend plugin");

                // Create a minimal CPU backend plugin implementation for AOT compatibility
                return new AotCpuBackendPlugin();
            };

            // Register CUDA backend (when available)
            _factories["DotCompute.Backends.CUDA"] = () =>
            {
                _logger.LogDebug("Creating CUDA backend plugin");

                // Use comprehensive platform detection for CUDA availability
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.CUDA);
                return new AotCudaBackendPlugin();
            };

            // Register Metal backend (when available)
            _factories["DotCompute.Backends.Metal"] = () =>
            {
                _logger.LogDebug("Creating Metal backend plugin");

                // Use comprehensive platform detection for Metal availability
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.Metal);
                return new AotMetalBackendPlugin();
            };

            // Register OpenCL backend (when available)
            _factories["DotCompute.Backends.OpenCL"] = () =>
            {
                _logger.LogDebug("Creating OpenCL backend plugin");

                // Use comprehensive platform detection for OpenCL availability
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.OpenCL);
                return new AotOpenClBackendPlugin();
            };

            // Register DirectCompute backend (when available)
            _factories["DotCompute.Backends.DirectCompute"] = () =>
            {
                _logger.LogDebug("Creating DirectCompute backend plugin");

                // Use comprehensive platform detection for DirectCompute availability
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.DirectCompute);
                return new AotDirectComputeBackendPlugin();
            };

            // Register Vulkan compute backend (when available)
            _factories["DotCompute.Backends.Vulkan"] = () =>
            {
                _logger.LogDebug("Creating Vulkan compute backend plugin");

                // Use comprehensive platform detection for Vulkan availability
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.Vulkan);
                return new AotVulkanBackendPlugin();
            };

            _logger.LogInformation("Registered {Count} plugin factories", _factories.Count);
        }

        /// <summary>
        /// Checks if CUDA runtime is available on the system.
        /// </summary>
        private static bool IsCudaAvailable()
        {
            try
            {
                // This would normally check for CUDA runtime availability
                // For AOT compatibility, we do a simple platform check
                return Environment.Is64BitOperatingSystem &&
                       (OperatingSystem.IsWindows() || OperatingSystem.IsLinux());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a plugin instance using static factory methods instead of reflection.
        /// </summary>
        public IBackendPlugin? CreatePlugin(string pluginTypeName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (string.IsNullOrWhiteSpace(pluginTypeName))
            {
                throw new ArgumentException("Plugin type name cannot be null, empty, or whitespace.", nameof(pluginTypeName));
            }

            lock (_lock)
            {
                try
                {
                    if (_factories.TryGetValue(pluginTypeName, out var factory))
                    {
                        var plugin = factory();
                        _plugins[plugin.Id] = plugin;

                        _logger.LogInformation("Successfully created plugin {Id} ({Name}) from factory",
                            plugin.Id, plugin.Name);

                        return plugin;
                    }

                    _logger.LogWarning("No factory found for plugin type: {Type}", pluginTypeName);
                    return null;
                }
                catch (PlatformNotSupportedException)
                {
                    // Re-throw platform not supported exceptions so callers can handle them appropriately
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create plugin {Type}", pluginTypeName);
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets a loaded plugin by ID.
        /// </summary>
        public IBackendPlugin? GetPlugin(string pluginId)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_lock)
            {
                return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
            }
        }

        /// <summary>
        /// Gets all loaded plugins.
        /// </summary>
        public IReadOnlyCollection<IBackendPlugin> GetLoadedPlugins()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_lock)
            {
                return [.. _plugins.Values];
            }
        }

        /// <summary>
        /// Lists all available plugin types that can be created.
        /// </summary>
        public IReadOnlyCollection<string> GetAvailablePluginTypes()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return [.. _factories.Keys];
        }

        /// <summary>
        /// Unloads a plugin by ID.
        /// </summary>
        public bool UnloadPlugin(string pluginId)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (string.IsNullOrWhiteSpace(pluginId))
            {
                throw new ArgumentException("Plugin ID cannot be null, empty, or whitespace.", nameof(pluginId));
            }

            lock (_lock)
            {
                if (_plugins.TryGetValue(pluginId, out var plugin))
                {
                    try
                    {
                        _logger.LogInformation("Unloading plugin {Id}", pluginId);
                        plugin.Dispose();
                        _ = _plugins.Remove(pluginId);
                        _logger.LogInformation("Successfully unloaded plugin {Id}", pluginId);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to unload plugin {Id}", pluginId);
                        return false;
                    }
                }

                _logger.LogWarning("Plugin {Id} not found for unloading", pluginId);
                return false;
            }
        }

        /// <summary>
        /// Registers a custom plugin factory for AOT scenarios.
        /// This allows applications to register additional plugins at startup.
        /// </summary>
        public void RegisterPluginFactory(string pluginTypeName, Func<IBackendPlugin> factory)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (string.IsNullOrWhiteSpace(pluginTypeName))
            {
                throw new ArgumentException("Plugin type name cannot be null, empty, or whitespace.", nameof(pluginTypeName));
            }
            ArgumentNullException.ThrowIfNull(factory);

            lock (_lock)
            {
                _factories[pluginTypeName] = factory;
                _logger.LogInformation("Registered custom plugin factory for {Type}", pluginTypeName);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (_lock)
            {
                foreach (var plugin in _plugins.Values.ToList())
                {
                    try
                    {
                        plugin.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing plugin {Id}", plugin.Id);
                    }
                }

                _plugins.Clear();
                _factories.Clear();
            }

            _logger.LogInformation("AotPluginRegistry disposed");
        }
    }

    /// <summary>
    /// AOT-compatible plugin system that uses static registration instead of dynamic loading.
    /// This is a drop-in replacement for the reflection-based PluginSystem.
    /// </summary>
    public sealed class AotPluginSystem : IDisposable
    {
        private readonly AotPluginRegistry _registry;
        private readonly ILogger<AotPluginSystem> _logger;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AotPluginSystem"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">logger</exception>
        public AotPluginSystem(ILogger<AotPluginSystem> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var registryLogger = _logger as ILogger<AotPluginRegistry> ??
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AotPluginRegistry>.Instance;

            _registry = new AotPluginRegistry(registryLogger);
        }

        /// <summary>
        /// Loads a plugin using static factory methods instead of assembly loading.
        /// This method signature maintains compatibility with the original PluginSystem.
        /// </summary>
        public async Task<IBackendPlugin?> LoadPluginAsync(
            string assemblyPath,
            string pluginTypeName,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _logger.LogInformation("Loading plugin {Type} (assembly path ignored in AOT mode)", pluginTypeName);

            // In AOT mode, we ignore the assembly path and use static registration
            await Task.Yield(); // Maintain async signature for compatibility

            return _registry.CreatePlugin(pluginTypeName);
        }

        /// <summary>
        /// Unloads a plugin.
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await Task.Yield(); // Maintain async signature for compatibility

            return _registry.UnloadPlugin(pluginId);
        }

        /// <summary>
        /// Gets a loaded plugin by ID.
        /// </summary>
        public IBackendPlugin? GetPlugin(string pluginId) => _registry.GetPlugin(pluginId);

        /// <summary>
        /// Gets all loaded plugins.
        /// </summary>
        public IEnumerable<IBackendPlugin> GetLoadedPlugins() => _registry.GetLoadedPlugins();

        /// <summary>
        /// Gets available plugin types (replaces assembly discovery).
        /// </summary>
        public IEnumerable<string> GetAvailablePluginTypes() => _registry.GetAvailablePluginTypes();

        /// <summary>
        /// Registers a custom plugin factory.
        /// </summary>
        public void RegisterPluginFactory(string pluginTypeName, Func<IBackendPlugin> factory) => _registry.RegisterPluginFactory(pluginTypeName, factory);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _registry.Dispose();
        }
    }

    /// <summary>
    /// Static helper class for AOT plugin management.
    /// </summary>
    public static class AotPluginHelpers
    {
        /// <summary>
        /// Determines if the current runtime supports AOT plugin loading.
        /// </summary>
        public static bool IsAotCompatible =>
#if NETCOREAPP
            !global::System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled;
#else
        false;
#endif

        /// <summary>
        /// Creates the appropriate plugin system based on runtime capabilities.
        /// </summary>
        public static IDisposable CreatePluginSystem(ILogger logger)
        {
            return IsAotCompatible ? new AotPluginSystem(logger as ILogger<AotPluginSystem> ??
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<AotPluginSystem>.Instance) : new PluginSystem(logger as ILogger<PluginSystem> ??
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginSystem>.Instance);
        }
    }

    /// <summary>
    /// Minimal CPU backend plugin implementation for AOT compatibility.
    /// </summary>
    internal sealed class AotCpuBackendPlugin : IBackendPlugin
    {
        private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
        private PluginState _state = PluginState.Loaded;
        private PluginHealth _health = PluginHealth.Healthy;
#pragma warning restore IDE0044

        public string Id => "DotCompute.Backends.CPU";
        public string Name => "CPU Backend";
        public string Description => GetCpuBackendDescription();
        public Version Version => new(1, 0, 0);
        public string Author => "DotCompute Team";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;
        public PluginState State => _state;
        public PluginHealth Health => _health;

        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
#pragma warning disable CS0067 // Event is never used - minimal implementation for AOT compatibility
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Minimal implementation for AOT
        }

        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Initialized;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Running;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Stopped;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public PluginValidationResult Validate() => new() { IsValid = true };

        public string GetConfigurationSchema() => "{}";

        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public PluginMetrics GetMetrics() => new();

        private static string GetCpuBackendDescription()
        {
            var hardware = PlatformDetection.Hardware;
            var capabilities = new List<string> { "Multi-threaded CPU compute backend" };

            // Add SIMD capabilities
            var simdFeatures = new List<string>();
            if (hardware.SupportsAvx512F)
            {
                simdFeatures.Add("AVX-512");
            }
            else if (hardware.SupportsAvx2)
            {
                simdFeatures.Add("AVX2");
            }
            else if (hardware.SupportsAvx)
            {
                simdFeatures.Add("AVX");
            }
            else if (hardware.SupportsSse42)
            {
                simdFeatures.Add("SSE4.2");
            }
            else if (hardware.SupportsSse2)
            {
                simdFeatures.Add("SSE2");
            }

            if (hardware.SupportsArmBase)
            {
                simdFeatures.Add("NEON");
            }

            if (simdFeatures.Count > 0)
            {
                capabilities.Add($"SIMD acceleration ({string.Join(", ", simdFeatures)})");
            }

            capabilities.Add($"Vector size: {hardware.VectorSizeBytes} bytes");
            capabilities.Add($"{PlatformDetection.Current.ProcessorCount} threads");

            return string.Join(" | ", capabilities);
        }

        public void Dispose()
        {
            // No resources to dispose
        }
    }

    /// <summary>
    /// Minimal CUDA backend plugin implementation for AOT compatibility.
    /// </summary>
    internal sealed class AotCudaBackendPlugin : IBackendPlugin
    {
        private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
        private PluginState _state = PluginState.Loaded;
        private PluginHealth _health = PluginHealth.Unknown;
        private bool _disposed;
#pragma warning restore IDE0044

        public string Id => "DotCompute.Backends.CUDA";
        public string Name => "CUDA Backend";
        public string Description => "NVIDIA CUDA GPU compute backend";
        public Version Version => new(1, 0, 0);
        public string Author => "DotCompute Team";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;
        public PluginState State => _state;
        public PluginHealth Health => _health;

        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
#pragma warning disable CS0067 // Event is never used - minimal implementation for AOT compatibility
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Minimal implementation for AOT
        }

        public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            if (!CheckCudaAvailability())
            {
                _health = PluginHealth.Unhealthy;
                _state = PluginState.Failed;
                return;
            }

            _state = PluginState.Initialized;
            _health = PluginHealth.Healthy;
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(PluginState.Loaded, _state));
            await Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_health != PluginHealth.Healthy)
                {
                    return Task.CompletedTask;
                }

                var oldState = _state;
                _state = PluginState.Running;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Stopped;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public PluginValidationResult Validate()
        {
            var result = new PluginValidationResult { IsValid = CheckCudaAvailability() };
            if (!result.IsValid)
            {
                result.Errors.Add("CUDA runtime is not available on this system");
            }
            return result;
        }

        public string GetConfigurationSchema() => "{}";

        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public PluginMetrics GetMetrics() => new();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }

        private static bool CheckCudaAvailability()
        {
            try
            {
                // Check platform compatibility first
                if (!Environment.Is64BitOperatingSystem ||
                    !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
                {
                    return false;
                }

                // Check for NVIDIA GPU presence through system queries
                if (OperatingSystem.IsWindows())
                {
                    return CheckWindowsCudaAvailability();
                }
                else if (OperatingSystem.IsLinux())
                {
                    return CheckLinuxCudaAvailability();
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckWindowsCudaAvailability()
        {
            try
            {
                // Check for NVIDIA driver in Windows system files
                // This approach is production-ready and doesn't require WMI/registry access
                // which may be restricted in some environments
                var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var nvmlPath = Path.Combine(systemDirectory, "nvml.dll");
                var cudartPath = Path.Combine(systemDirectory, "cudart64_*.dll");

                return File.Exists(nvmlPath) || Directory.GetFiles(systemDirectory, "cudart64_*.dll").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckLinuxCudaAvailability()
        {
            try
            {
                // Check for CUDA libraries in standard locations
                var cudaPaths = new[]
                {
                "/usr/lib/x86_64-linux-gnu/libcuda.so",
                "/usr/lib64/libcuda.so",
                "/usr/local/cuda/lib64/libcudart.so"
            };

                return cudaPaths.Any(File.Exists) ||
                       Directory.Exists("/proc/driver/nvidia") ||
                       File.Exists("/dev/nvidia0");
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Minimal Metal backend plugin implementation for AOT compatibility.
    /// </summary>
    internal sealed class AotMetalBackendPlugin : IBackendPlugin
    {
        private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
        private PluginState _state = PluginState.Loaded;
        private PluginHealth _health = PluginHealth.Unknown;
        private bool _disposed;
#pragma warning restore IDE0044

        public string Id => "DotCompute.Backends.Metal";
        public string Name => "Metal Backend";
        public string Description => "Apple Metal GPU compute backend";
        public Version Version => new(1, 0, 0);
        public string Author => "DotCompute Team";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;
        public PluginState State => _state;
        public PluginHealth Health => _health;

        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;

        private void OnStateChanged(PluginStateChangedEventArgs e) => StateChanged?.Invoke(this, e);
        private void OnErrorOccurred(PluginErrorEventArgs e) => ErrorOccurred?.Invoke(this, e);
        private void OnHealthChanged(PluginHealthChangedEventArgs e) => HealthChanged?.Invoke(this, e);

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Minimal implementation for AOT
        }

        public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                var oldHealth = _health;

                if (!IsMetalAvailable())
                {
                    _health = PluginHealth.Unhealthy;
                    _state = PluginState.Failed;
                    StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
                    return;
                }

                _state = PluginState.Initialized;
                _health = PluginHealth.Healthy;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            await Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_health != PluginHealth.Healthy)
                {
                    return Task.CompletedTask;
                }

                var oldState = _state;
                _state = PluginState.Running;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Stopped;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public PluginValidationResult Validate()
        {
            var result = new PluginValidationResult { IsValid = IsMetalAvailable() };
            if (!result.IsValid)
            {
                result.Errors.Add("Metal is only available on macOS and iOS");
            }
            return result;
        }

        public string GetConfigurationSchema() => "{}";

        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public PluginMetrics GetMetrics() => new();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }

        private static bool IsMetalAvailable() => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();
    }

    /// <summary>
    /// Minimal OpenCL backend plugin implementation for AOT compatibility.
    /// </summary>
    internal sealed class AotOpenClBackendPlugin : IBackendPlugin
    {
        private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
        private PluginState _state = PluginState.Loaded;
        private PluginHealth _health = PluginHealth.Unknown;
        private bool _disposed;
#pragma warning restore IDE0044

        public string Id => "DotCompute.Backends.OpenCL";
        public string Name => "OpenCL Backend";
        public string Description => "Cross-platform OpenCL compute backend";
        public Version Version => new(1, 0, 0);
        public string Author => "DotCompute Team";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;
        public PluginState State => _state;
        public PluginHealth Health => _health;

        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
#pragma warning disable CS0067 // Event is never used - minimal implementation for AOT compatibility
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                if (PlatformDetection.IsBackendAvailable(ComputeBackendType.OpenCL))
                {
                    _state = PluginState.Initialized;
                    _health = PluginHealth.Healthy;
                }
                else
                {
                    _state = PluginState.Failed;
                    _health = PluginHealth.Unhealthy;
                }
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            await Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_health != PluginHealth.Healthy)
                {
                    return Task.CompletedTask;
                }

                var oldState = _state;
                _state = PluginState.Running;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Stopped;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public PluginValidationResult Validate()
        {
            var result = new PluginValidationResult { IsValid = PlatformDetection.IsBackendAvailable(ComputeBackendType.OpenCL) };
            if (!result.IsValid)
            {
                result.Errors.Add("OpenCL runtime not found or no compatible devices available");
            }
            return result;
        }

        public string GetConfigurationSchema() => "{}";
        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginMetrics GetMetrics() => new();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Minimal DirectCompute backend plugin implementation for AOT compatibility.
    /// </summary>
    internal sealed class AotDirectComputeBackendPlugin : IBackendPlugin
    {
        private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
        private PluginState _state = PluginState.Loaded;
        private PluginHealth _health = PluginHealth.Unknown;
        private bool _disposed;
#pragma warning restore IDE0044

        public string Id => "DotCompute.Backends.DirectCompute";
        public string Name => "DirectCompute Backend";
        public string Description => "Windows DirectCompute backend using DirectX compute shaders";
        public Version Version => new(1, 0, 0);
        public string Author => "DotCompute Team";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;
        public PluginState State => _state;
        public PluginHealth Health => _health;

        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
#pragma warning disable CS0067 // Event is never used - minimal implementation for AOT compatibility
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                if (PlatformDetection.IsBackendAvailable(ComputeBackendType.DirectCompute))
                {
                    _state = PluginState.Initialized;
                    _health = PluginHealth.Healthy;
                }
                else
                {
                    _state = PluginState.Failed;
                    _health = PluginHealth.Unhealthy;
                }
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            await Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_health != PluginHealth.Healthy)
                {
                    return Task.CompletedTask;
                }

                var oldState = _state;
                _state = PluginState.Running;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Stopped;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public PluginValidationResult Validate()
        {
            var result = new PluginValidationResult { IsValid = PlatformDetection.IsBackendAvailable(ComputeBackendType.DirectCompute) };
            if (!result.IsValid)
            {
                result.Errors.Add("DirectCompute is only available on Windows with DirectX 11+ support");
            }
            return result;
        }

        public string GetConfigurationSchema() => "{}";
        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginMetrics GetMetrics() => new();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Minimal Vulkan compute backend plugin implementation for AOT compatibility.
    /// </summary>
    internal sealed class AotVulkanBackendPlugin : IBackendPlugin
    {
        private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
        private PluginState _state = PluginState.Loaded;
        private PluginHealth _health = PluginHealth.Unknown;
        private bool _disposed;
#pragma warning restore IDE0044

        public string Id => "DotCompute.Backends.Vulkan";
        public string Name => "Vulkan Compute Backend";
        public string Description => "Cross-platform Vulkan compute backend";
        public Version Version => new(1, 0, 0);
        public string Author => "DotCompute Team";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;
        public PluginState State => _state;
        public PluginHealth Health => _health;

        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
#pragma warning disable CS0067 // Event is never used - minimal implementation for AOT compatibility
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                if (PlatformDetection.IsBackendAvailable(ComputeBackendType.Vulkan))
                {
                    _state = PluginState.Initialized;
                    _health = PluginHealth.Healthy;
                }
                else
                {
                    _state = PluginState.Failed;
                    _health = PluginHealth.Unhealthy;
                }
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            await Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_health != PluginHealth.Healthy)
                {
                    return Task.CompletedTask;
                }

                var oldState = _state;
                _state = PluginState.Running;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var oldState = _state;
                _state = PluginState.Stopped;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
            }
            return Task.CompletedTask;
        }

        public PluginValidationResult Validate()
        {
            var result = new PluginValidationResult { IsValid = PlatformDetection.IsBackendAvailable(ComputeBackendType.Vulkan) };
            if (!result.IsValid)
            {
                result.Errors.Add("Vulkan compute runtime not found or no compatible devices available");
            }
            return result;
        }

        public string GetConfigurationSchema() => "{}";
        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginMetrics GetMetrics() => new();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
    }
}
