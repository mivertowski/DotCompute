// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Core
{
    /// <summary>
    /// Base implementation for backend plugins.
    /// </summary>
    public abstract class BackendPluginBase : IBackendPlugin
    {
        private readonly object _stateLock = new();
        private PluginState _state = PluginState.Unknown;
        private PluginHealth _health = PluginHealth.Unknown;
        private readonly PluginMetrics _metrics = new();
        private DateTime _startTime;
        private bool _disposed;

        protected ILogger? Logger { get; private set; }
        protected IServiceProvider? ServiceProvider { get; private set; }
        protected IConfiguration? Configuration { get; private set; }

        /// <inheritdoc/>
        public abstract string Id { get; }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract Version Version { get; }

        /// <inheritdoc/>
        public abstract string Description { get; }

        /// <inheritdoc/>
        public abstract string Author { get; }

        /// <inheritdoc/>
        public abstract PluginCapabilities Capabilities { get; }

        /// <inheritdoc/>
        public PluginState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
            protected set
            {
                lock (_stateLock)
                {
                    if (_state != value)
                    {
                        var oldState = _state;
                        _state = value;
                        OnStateChanged(oldState, value);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public PluginHealth Health
        {
            get => _health;
            protected set
            {
                if (_health != value)
                {
                    var oldHealth = _health;
                    _health = value;
                    OnHealthChanged(oldHealth, value);
                }
            }
        }

        /// <inheritdoc/>
        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;

        /// <inheritdoc/>
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;

        /// <inheritdoc/>
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;

        /// <inheritdoc/>
        public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Base implementation - derived classes can override
            services.AddSingleton(this);
        }

        /// <inheritdoc/>
        public virtual async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (State != PluginState.Unknown && State != PluginState.Loaded)
            {
                throw new InvalidOperationException($"Cannot initialize plugin in state {State}");
            }

            State = PluginState.Initializing;
            
            try
            {
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                Logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(GetType());
                
                Logger?.LogInformation("Initializing plugin {PluginId}", Id);
                
                await OnInitializeAsync(cancellationToken);
                
                State = PluginState.Initialized;
                Health = PluginHealth.Healthy;
                
                Logger?.LogInformation("Plugin {PluginId} initialized successfully", Id);
            }
            catch (Exception ex)
            {
                State = PluginState.Failed;
                Health = PluginHealth.Critical;
                Logger?.LogError(ex, "Failed to initialize plugin {PluginId}", Id);
                OnError(ex, "Initialization");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (State != PluginState.Initialized && State != PluginState.Stopped)
            {
                throw new InvalidOperationException($"Cannot start plugin in state {State}");
            }

            State = PluginState.Starting;
            
            try
            {
                Logger?.LogInformation("Starting plugin {PluginId}", Id);
                
                _startTime = DateTime.UtcNow;
                await OnStartAsync(cancellationToken);
                
                State = PluginState.Running;
                
                Logger?.LogInformation("Plugin {PluginId} started successfully", Id);
            }
            catch (Exception ex)
            {
                State = PluginState.Failed;
                Health = PluginHealth.Critical;
                Logger?.LogError(ex, "Failed to start plugin {PluginId}", Id);
                OnError(ex, "Start");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual async Task StopAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (State != PluginState.Running)
            {
                Logger?.LogWarning("Plugin {PluginId} is not running (state: {State})", Id, State);
                return;
            }

            State = PluginState.Stopping;
            
            try
            {
                Logger?.LogInformation("Stopping plugin {PluginId}", Id);
                
                await OnStopAsync(cancellationToken);
                
                State = PluginState.Stopped;
                
                Logger?.LogInformation("Plugin {PluginId} stopped successfully", Id);
            }
            catch (Exception ex)
            {
                State = PluginState.Failed;
                Logger?.LogError(ex, "Failed to stop plugin {PluginId}", Id);
                OnError(ex, "Stop");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual PluginValidationResult Validate()
        {
            var result = new PluginValidationResult { IsValid = true };

            // Basic validation
            if (string.IsNullOrEmpty(Id))
            {
                result.IsValid = false;
                result.Errors.Add("Plugin ID is required");
            }

            if (string.IsNullOrEmpty(Name))
            {
                result.IsValid = false;
                result.Errors.Add("Plugin name is required");
            }

            if (Version == null)
            {
                result.IsValid = false;
                result.Errors.Add("Plugin version is required");
            }

            // Allow derived classes to add custom validation
            OnValidate(result);

            return result;
        }

        /// <inheritdoc/>
        public virtual string GetConfigurationSchema()
        {
            // Base implementation returns empty schema
            return "{}";
        }

        /// <inheritdoc/>
        public virtual async Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            Logger?.LogInformation("Configuration changed for plugin {PluginId}", Id);
            
            await OnConfigurationUpdatedAsync(configuration, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual PluginMetrics GetMetrics()
        {
            lock (_metrics)
            {
                _metrics.Timestamp = DateTime.UtcNow;
                _metrics.Uptime = State == PluginState.Running ? DateTime.UtcNow - _startTime : TimeSpan.Zero;
                
                // Allow derived classes to add custom metrics
                OnUpdateMetrics(_metrics);
                
                return new PluginMetrics
                {
                    Timestamp = _metrics.Timestamp,
                    Uptime = _metrics.Uptime,
                    RequestCount = _metrics.RequestCount,
                    ErrorCount = _metrics.ErrorCount,
                    AverageResponseTime = _metrics.AverageResponseTime,
                    MemoryUsage = _metrics.MemoryUsage,
                    CpuUsage = _metrics.CpuUsage,
                    CustomMetrics = new Dictionary<string, object>(_metrics.CustomMetrics)
                };
            }
        }

        /// <summary>
        /// Called when the plugin needs to be initialized.
        /// </summary>
        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            // Override in derived classes
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the plugin needs to be started.
        /// </summary>
        protected virtual Task OnStartAsync(CancellationToken cancellationToken)
        {
            // Override in derived classes
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the plugin needs to be stopped.
        /// </summary>
        protected virtual Task OnStopAsync(CancellationToken cancellationToken)
        {
            // Override in derived classes
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when plugin validation is performed.
        /// </summary>
        protected virtual void OnValidate(PluginValidationResult result)
        {
            // Override in derived classes to add custom validation
        }

        /// <summary>
        /// Called when configuration is updated.
        /// </summary>
        protected virtual Task OnConfigurationUpdatedAsync(IConfiguration configuration, CancellationToken cancellationToken)
        {
            // Override in derived classes
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when metrics need to be updated.
        /// </summary>
        protected virtual void OnUpdateMetrics(PluginMetrics metrics)
        {
            // Override in derived classes to add custom metrics
        }

        /// <summary>
        /// Records a successful request.
        /// </summary>
        protected void RecordRequest(double responseTime = 0)
        {
            lock (_metrics)
            {
                _metrics.RequestCount++;
                if (responseTime > 0)
                {
                    // Simple moving average
                    _metrics.AverageResponseTime = (_metrics.AverageResponseTime * (_metrics.RequestCount - 1) + responseTime) / _metrics.RequestCount;
                }
            }
        }

        /// <summary>
        /// Records an error.
        /// </summary>
        protected void RecordError()
        {
            lock (_metrics)
            {
                _metrics.ErrorCount++;
            }
        }

        /// <summary>
        /// Updates memory usage metric.
        /// </summary>
        protected void UpdateMemoryUsage(long bytes)
        {
            lock (_metrics)
            {
                _metrics.MemoryUsage = bytes;
            }
        }

        /// <summary>
        /// Updates CPU usage metric.
        /// </summary>
        protected void UpdateCpuUsage(double percentage)
        {
            lock (_metrics)
            {
                _metrics.CpuUsage = Math.Max(0, Math.Min(100, percentage));
            }
        }

        /// <summary>
        /// Sets a custom metric value.
        /// </summary>
        protected void SetCustomMetric(string name, object value)
        {
            lock (_metrics)
            {
                _metrics.CustomMetrics[name] = value;
            }
        }

        /// <summary>
        /// Raises the StateChanged event.
        /// </summary>
        private void OnStateChanged(PluginState oldState, PluginState newState)
        {
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, newState));
        }

        /// <summary>
        /// Raises the HealthChanged event.
        /// </summary>
        private void OnHealthChanged(PluginHealth oldHealth, PluginHealth newHealth)
        {
            HealthChanged?.Invoke(this, new PluginHealthChangedEventArgs(oldHealth, newHealth));
        }

        /// <summary>
        /// Raises the ErrorOccurred event.
        /// </summary>
        protected void OnError(Exception exception, string context)
        {
            RecordError();
            ErrorOccurred?.Invoke(this, new PluginErrorEventArgs(exception, context));
        }

        /// <summary>
        /// Throws if the plugin has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by the plugin.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Stop the plugin if it's running
                    if (State == PluginState.Running)
                    {
                        try
                        {
                            StopAsync().GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "Error stopping plugin {PluginId} during dispose", Id);
                        }
                    }

                    State = PluginState.Unloaded;
                }

                _disposed = true;
            }
        }
    }
}