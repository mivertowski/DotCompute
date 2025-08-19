using System.Diagnostics;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Implementations.Plugins;


/// <summary>
/// Test implementation of IBackendPlugin for testing plugin functionality.
/// </summary>
public sealed class TestBackendPlugin(string pluginType = "TestBackend", PluginCapabilities capabilities = PluginCapabilities.ComputeBackend) : IBackendPlugin
{
    private readonly string _pluginType = pluginType;
    private readonly Stopwatch _uptime = new();
    private readonly Dictionary<string, object> _customMetrics = [];
    private PluginState _state = PluginState.Unknown;
    private PluginHealth _health = PluginHealth.Unknown;
    private ILogger<TestBackendPlugin>? _logger;
    private IServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;
    private long _requestCount;
    private long _errorCount;
    private double _totalResponseTime;
    private bool _disposed;

    // Logger message delegates for performance
    private static readonly Action<ILogger, string, Exception?> _logPluginInitialized =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, "PluginInitialized"), "Plugin {PluginId} initialized successfully");

    private static readonly Action<ILogger, string, Exception?> _logPluginStarted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "PluginStarted"), "Plugin {PluginId} started");

    private static readonly Action<ILogger, string, Exception?> _logPluginStopped =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "PluginStopped"), "Plugin {PluginId} stopped");

    private static readonly Action<ILogger, string, Exception?> _logConfigurationChanged =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, "ConfigurationChanged"), "Configuration changed for plugin {PluginId}");

    private static readonly Action<ILogger, string, Exception?> _logPluginInitializationFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(101, "PluginInitializationFailed"), "Failed to initialize plugin {PluginId}");

    private static readonly Action<ILogger, string, Exception?> _logPluginStartFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(102, "PluginStartFailed"), "Failed to start plugin {PluginId}");

    private static readonly Action<ILogger, string, Exception?> _logPluginStopError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(103, "PluginStopError"), "Error stopping plugin {PluginId}");

    public string Id { get; } = $"test.backend.{pluginType.ToUpperInvariant()}";
    public string Name { get; } = $"Test {pluginType} Backend Plugin";
    public Version Version { get; } = new Version(1, 0, 0);
    public string Description { get; } = $"Test implementation of {pluginType} backend plugin for testing";
    public string Author { get; } = "DotCompute Test Suite";
    public PluginCapabilities Capabilities { get; } = capabilities;
    public PluginState State => _state;
    public PluginHealth Health => _health;

    public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
    public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
    public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        _configuration = configuration;

        // Register plugin-specific services
        _ = services.AddSingleton(this);

        // Add logging if not already registered
        if (services.All(s => s.ServiceType != typeof(ILoggerFactory)))
        {
            _ = services.AddLogging();
        }

        // Register plugin-specific implementations based on capabilities
        if (Capabilities.HasFlag(PluginCapabilities.ComputeBackend))
        {
            _ = services.AddSingleton<IComputeService>(new TestComputeService());
        }

        if (Capabilities.HasFlag(PluginCapabilities.StorageProvider))
        {
            _ = services.AddSingleton<IStorageService>(new TestStorageService());
        }

        if (Capabilities.HasFlag(PluginCapabilities.MetricsProvider))
        {
            _ = services.AddSingleton<IMetricsService>(new TestMetricsService());
        }

        ChangeState(PluginState.Loaded, "Services configured");
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<TestBackendPlugin>>();

        ChangeState(PluginState.Initializing, "Starting initialization");

        try
        {
            // Simulate initialization work
            await Task.Delay(10, cancellationToken);

            // Initialize plugin resources
            _customMetrics["InitializationTime"] = DateTime.UtcNow;
            _customMetrics["PluginType"] = _pluginType;

            // Validate configuration
            var validationResult = Validate();
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Plugin validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            ChangeState(PluginState.Initialized, "Initialization complete");
            ChangeHealth(PluginHealth.Healthy, "Plugin initialized successfully");

            if (_logger != null)
                _logPluginInitialized(_logger, Id, null);
        }
        catch (Exception ex)
        {
            if (_logger != null)
                _logPluginInitializationFailed(_logger, Id, ex);
            ChangeState(PluginState.Failed, $"Initialization failed: {ex.Message}");
            ChangeHealth(PluginHealth.Critical, "Initialization failure");
            OnErrorOccurred(ex, "InitializeAsync");
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_state is not PluginState.Initialized and not PluginState.Stopped)
        {
            throw new InvalidOperationException($"Cannot start plugin in state: {_state}");
        }

        ChangeState(PluginState.Starting, "Starting plugin");

        try
        {
            // Simulate startup work
            await Task.Delay(5, cancellationToken);

            _uptime.Restart();
            _requestCount = 0;
            _errorCount = 0;
            _totalResponseTime = 0;

            ChangeState(PluginState.Running, "Plugin started successfully");
            ChangeHealth(PluginHealth.Healthy, "Plugin is running");

            if (_logger != null)
                _logPluginStarted(_logger, Id, null);
        }
        catch (Exception ex)
        {
            if (_logger != null)
                _logPluginStartFailed(_logger, Id, ex);
            ChangeState(PluginState.Failed, $"Start failed: {ex.Message}");
            ChangeHealth(PluginHealth.Critical, "Start failure");
            OnErrorOccurred(ex, "StartAsync");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_state != PluginState.Running)
        {
            return;
        }

        ChangeState(PluginState.Stopping, "Stopping plugin");

        try
        {
            // Simulate cleanup work
            await Task.Delay(5, cancellationToken);

            _uptime.Stop();

            ChangeState(PluginState.Stopped, "Plugin stopped successfully");
            ChangeHealth(PluginHealth.Unknown, "Plugin is stopped");

            if (_logger != null)
                _logPluginStopped(_logger, Id, null);
        }
        catch (Exception ex)
        {
            if (_logger != null)
                _logPluginStopError(_logger, Id, ex);
            OnErrorOccurred(ex, "StopAsync");
            throw;
        }
    }

    public PluginValidationResult Validate()
    {
        var result = new PluginValidationResult { IsValid = true };

        // Check required configuration
        if (_configuration != null)
        {
            var requiredSettings = new[] { "ConnectionString", "MaxConnections", "Timeout" };
            foreach (var setting in requiredSettings)
            {
                var value = _configuration[$"Plugins:{Id}:{setting}"];
                if (string.IsNullOrEmpty(value))
                {
                    result.Warnings.Add($"Optional configuration '{setting}' not set");
                }
            }
        }

        // Check system requirements
        if (Environment.ProcessorCount < 2)
        {
            result.Warnings.Add("Low processor count may impact performance");
        }

        if (Environment.WorkingSet < 1024 * 1024 * 100) // 100MB
        {
            result.Warnings.Add("Low memory may impact performance");
        }

        // Add metadata
        result.Metadata["ValidationTime"] = DateTime.UtcNow;
        result.Metadata["PluginType"] = _pluginType;
        result.Metadata["Capabilities"] = Capabilities.ToString();

        return result;
    }

    public string GetConfigurationSchema()
    {
        return @"{
            ""$schema"": ""http://json-schema.org/draft-07/schema#"",
            ""type"": ""object"",
            ""properties"": {
                ""ConnectionString"": {
                    ""type"": ""string"",
                    ""description"": ""Connection string for the backend""
                },
                ""MaxConnections"": {
                    ""type"": ""integer"",
                    ""minimum"": 1,
                    ""maximum"": 1000,
                    ""default"": 10
                },
                ""Timeout"": {
                    ""type"": ""integer"",
                    ""description"": ""Timeout in milliseconds"",
                    ""minimum"": 100,
                    ""default"": 5000
                },
                ""EnableCaching"": {
                    ""type"": ""boolean"",
                    ""default"": true
                },
                ""CacheSize"": {
                    ""type"": ""integer"",
                    ""minimum"": 0,
                    ""default"": 100
                }
            }
        }";
    }

    public async Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _configuration = configuration;

        if (_logger != null)
            _logConfigurationChanged(_logger, Id, null);

        // Simulate configuration reload
        await Task.Delay(1, cancellationToken);

        // Re-validate with new configuration
        var validationResult = Validate();
        if (!validationResult.IsValid)
        {
            ChangeHealth(PluginHealth.Degraded, "Configuration validation failed");
        }
        else
        {
            ChangeHealth(PluginHealth.Healthy, "Configuration updated successfully");
        }
    }

    public PluginMetrics GetMetrics()
    {
        var metrics = new PluginMetrics
        {
            Timestamp = DateTime.UtcNow,
            Uptime = _uptime.Elapsed,
            RequestCount = _requestCount,
            ErrorCount = _errorCount,
            AverageResponseTime = _requestCount > 0 ? _totalResponseTime / _requestCount : 0,
            MemoryUsage = GC.GetTotalMemory(false),
            CpuUsage = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount
        };

        // Add custom metrics
        foreach (var kvp in _customMetrics)
        {
            metrics.CustomMetrics[kvp.Key] = kvp.Value;
        }

        return metrics;
    }

    public void SimulateRequest(double responseTimeMs)
    {
        _ = Interlocked.Increment(ref _requestCount);
        _totalResponseTime += responseTimeMs;
        _customMetrics["LastRequestTime"] = DateTime.UtcNow;
    }

    public void SimulateError(Exception exception)
    {
        _ = Interlocked.Increment(ref _errorCount);
        OnErrorOccurred(exception, "SimulatedError");

        // Degrade health after multiple errors
        if (_errorCount > 5 && _health == PluginHealth.Healthy)
        {
            ChangeHealth(PluginHealth.Degraded, "Multiple errors detected");
        }

        if (_errorCount > 10 && _health == PluginHealth.Degraded)
        {
            ChangeHealth(PluginHealth.Unhealthy, "Too many errors");
        }
    }

    private void ChangeState(PluginState newState, string? reason = null)
    {
        if (_state == newState)
            return;

        var oldState = _state;
        _state = newState;
        StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, newState, reason));
    }

    private void ChangeHealth(PluginHealth newHealth, string? reason = null)
    {
        if (_health == newHealth)
            return;

        var oldHealth = _health;
        _health = newHealth;
        HealthChanged?.Invoke(this, new PluginHealthChangedEventArgs(oldHealth, newHealth, reason));
    }

    private void OnErrorOccurred(Exception exception, string context) => ErrorOccurred?.Invoke(this, new PluginErrorEventArgs(exception, context));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_state == PluginState.Running)
        {
            _ = StopAsync().Wait(1000);
        }

        ChangeState(PluginState.Unloaded, "Plugin disposed");
        _uptime.Stop();
        GC.SuppressFinalize(this);
    }
}

// Test service interfaces and implementations
public interface IComputeService
{
    public Task<double> ComputeAsync(double[] data);
}

public sealed class TestComputeService : IComputeService
{
    public async Task<double> ComputeAsync(double[] data)
    {
        await Task.Delay(1);
        return data.Sum();
    }
}

public interface IStorageService
{
    public Task<bool> StoreAsync(string key, byte[] data);
    public Task<byte[]?> RetrieveAsync(string key);
}

public sealed class TestStorageService : IStorageService
{
    private readonly Dictionary<string, byte[]> _storage = [];

    public async Task<bool> StoreAsync(string key, byte[] data)
    {
        await Task.Delay(1);
        _storage[key] = data;
        return true;
    }

    public async Task<byte[]?> RetrieveAsync(string key)
    {
        await Task.Delay(1);
        return _storage.TryGetValue(key, out var data) ? data : null;
    }
}

public interface IMetricsService
{
    public void RecordMetric(string name, double value);
    public double GetMetric(string name);
}

public sealed class TestMetricsService : IMetricsService
{
    private readonly Dictionary<string, double> _metrics = [];

    public void RecordMetric(string name, double value) => _metrics[name] = value;

    public double GetMetric(string name) => _metrics.TryGetValue(name, out var value) ? value : 0;
}

// Extension methods for LINQ operations
public static class EnumerableExtensions
{
    public static bool All<T>(this IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        foreach (var service in services)
        {
            if (!predicate(service))
                return false;
        }
        return true;
    }
}
