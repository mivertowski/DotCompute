using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Implementations.Plugins;

/// <summary>
/// Test implementation of IBackendPlugin for testing plugin functionality.
/// </summary>
public class TestBackendPlugin : IBackendPlugin
{
    private readonly string _pluginType;
    private readonly Stopwatch _uptime;
    private readonly Dictionary<string, object> _customMetrics;
    private PluginState _state;
    private PluginHealth _health;
    private ILogger<TestBackendPlugin>? _logger;
    private IServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;
    private long _requestCount;
    private long _errorCount;
    private double _totalResponseTime;
    private bool _disposed;

    public TestBackendPlugin(string pluginType = "TestBackend", PluginCapabilities capabilities = PluginCapabilities.ComputeBackend)
    {
        _pluginType = pluginType;
        Id = $"test.backend.{pluginType.ToLower()}";
        Name = $"Test {pluginType} Backend Plugin";
        Version = new Version(1, 0, 0);
        Description = $"Test implementation of {pluginType} backend plugin for testing";
        Author = "DotCompute Test Suite";
        Capabilities = capabilities;
        _state = PluginState.Unknown;
        _health = PluginHealth.Unknown;
        _uptime = new Stopwatch();
        _customMetrics = new Dictionary<string, object>();
    }

    public string Id { get; }
    public string Name { get; }
    public Version Version { get; }
    public string Description { get; }
    public string Author { get; }
    public PluginCapabilities Capabilities { get; }
    public PluginState State => _state;
    public PluginHealth Health => _health;

    public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
    public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
    public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        _configuration = configuration;
        
        // Register plugin-specific services
        services.AddSingleton(this);
        
        // Add logging if not already registered
        if (services.All(s => s.ServiceType != typeof(ILoggerFactory)))
        {
            services.AddLogging();
        }

        // Register plugin-specific implementations based on capabilities
        if (Capabilities.HasFlag(PluginCapabilities.ComputeBackend))
        {
            services.AddSingleton<IComputeService>(new TestComputeService());
        }

        if (Capabilities.HasFlag(PluginCapabilities.StorageProvider))
        {
            services.AddSingleton<IStorageService>(new TestStorageService());
        }

        if (Capabilities.HasFlag(PluginCapabilities.MetricsProvider))
        {
            services.AddSingleton<IMetricsService>(new TestMetricsService());
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
            
            _logger?.LogInformation("Plugin {PluginId} initialized successfully", Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize plugin {PluginId}", Id);
            ChangeState(PluginState.Failed, $"Initialization failed: {ex.Message}");
            ChangeHealth(PluginHealth.Critical, "Initialization failure");
            OnErrorOccurred(ex, "InitializeAsync");
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_state != PluginState.Initialized && _state != PluginState.Stopped)
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
            
            _logger?.LogInformation("Plugin {PluginId} started", Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start plugin {PluginId}", Id);
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
            
            _logger?.LogInformation("Plugin {PluginId} stopped", Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping plugin {PluginId}", Id);
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
        
        _logger?.LogInformation("Configuration changed for plugin {PluginId}", Id);
        
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
        Interlocked.Increment(ref _requestCount);
        _totalResponseTime += responseTimeMs;
        _customMetrics["LastRequestTime"] = DateTime.UtcNow;
    }

    public void SimulateError(Exception exception)
    {
        Interlocked.Increment(ref _errorCount);
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
        if (_state == newState) return;
        
        var oldState = _state;
        _state = newState;
        StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, newState, reason));
    }

    private void ChangeHealth(PluginHealth newHealth, string? reason = null)
    {
        if (_health == newHealth) return;
        
        var oldHealth = _health;
        _health = newHealth;
        HealthChanged?.Invoke(this, new PluginHealthChangedEventArgs(oldHealth, newHealth, reason));
    }

    private void OnErrorOccurred(Exception exception, string context)
    {
        ErrorOccurred?.Invoke(this, new PluginErrorEventArgs(exception, context));
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        if (_state == PluginState.Running)
        {
            StopAsync().Wait(1000);
        }
        
        ChangeState(PluginState.Unloaded, "Plugin disposed");
        _uptime.Stop();
    }
}

// Test service interfaces and implementations
public interface IComputeService
{
    Task<double> ComputeAsync(double[] data);
}

public class TestComputeService : IComputeService
{
    public async Task<double> ComputeAsync(double[] data)
    {
        await Task.Delay(1);
        return data.Sum();
    }
}

public interface IStorageService
{
    Task<bool> StoreAsync(string key, byte[] data);
    Task<byte[]?> RetrieveAsync(string key);
}

public class TestStorageService : IStorageService
{
    private readonly Dictionary<string, byte[]> _storage = new();

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
    void RecordMetric(string name, double value);
    double GetMetric(string name);
}

public class TestMetricsService : IMetricsService
{
    private readonly Dictionary<string, double> _metrics = new();

    public void RecordMetric(string name, double value)
    {
        _metrics[name] = value;
    }

    public double GetMetric(string name)
    {
        return _metrics.TryGetValue(name, out var value) ? value : 0;
    }
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