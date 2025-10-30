// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.Metal;

/// <summary>
/// Extension methods for configuring Metal telemetry and monitoring
/// </summary>
public static class MetalTelemetryExtensions
{
    /// <summary>
    /// Adds Metal telemetry services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddMetalTelemetry(
        this IServiceCollection services,
        Action<MetalTelemetryOptions>? configureOptions = null)
    {
        // Configure telemetry options
        if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        // Register core telemetry services
        _ = services.AddSingleton<MetalTelemetryManager>();
        _ = services.AddSingleton<MetalPerformanceCounters>();
        _ = services.AddSingleton<MetalHealthMonitor>();
        _ = services.AddSingleton<MetalProductionLogger>();
        _ = services.AddSingleton<MetalMetricsExporter>();
        _ = services.AddSingleton<MetalAlertsManager>();

        // Register hosted service for background telemetry tasks
        _ = services.AddHostedService<MetalTelemetryHostedService>();

        return services;
    }

    /// <summary>
    /// Adds Metal telemetry services with configuration from appsettings
    /// </summary>
    public static IServiceCollection AddMetalTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "MetalTelemetry")
    {
        // Manual configuration binding for AOT compatibility
        _ = services.Configure<MetalTelemetryOptions>(options =>
        {
            var section = configuration.GetSection(sectionName);

            // Manually bind primitive properties
            if (TimeSpan.TryParse(section["ReportingInterval"], out var reportingInterval))
            {
                options.ReportingInterval = reportingInterval;
            }

            if (TimeSpan.TryParse(section["CleanupInterval"], out var cleanupInterval))
            {
                options.CleanupInterval = cleanupInterval;
            }

            if (TimeSpan.TryParse(section["MetricsRetentionPeriod"], out var retentionPeriod))
            {
                options.MetricsRetentionPeriod = retentionPeriod;
            }

            if (bool.TryParse(section["AutoExportMetrics"], out var autoExport))
            {
                options.AutoExportMetrics = autoExport;
            }

            if (double.TryParse(section["SlowOperationThresholdMs"], out var slowOpThreshold))
            {
                options.SlowOperationThresholdMs = slowOpThreshold;
            }

            if (double.TryParse(section["HighGpuUtilizationThreshold"], out var gpuThreshold))
            {
                options.HighGpuUtilizationThreshold = gpuThreshold;
            }

            if (double.TryParse(section["HighMemoryUtilizationThreshold"], out var memThreshold))
            {
                options.HighMemoryUtilizationThreshold = memThreshold;
            }

            if (double.TryParse(section["HighResourceUtilizationThreshold"], out var resourceThreshold))
            {
                options.HighResourceUtilizationThreshold = resourceThreshold;
            }
        });

        return services.AddMetalTelemetry();
    }

    /// <summary>
    /// Configures Prometheus metrics export
    /// </summary>
    public static MetalTelemetryOptions UsePrometheusExport(
        this MetalTelemetryOptions options,
        string endpoint,
        Dictionary<string, string>? headers = null)
    {
        options.ExportOptions.Exporters.Add(new ExporterConfiguration
        {
            Name = "Prometheus",
            Type = ExporterType.Prometheus,
            Endpoint = endpoint,
            Headers = headers,
            Enabled = true
        });

        return options;
    }

    /// <summary>
    /// Configures Application Insights export
    /// </summary>
    public static MetalTelemetryOptions UseApplicationInsights(
        this MetalTelemetryOptions options,
        string endpoint,
        string instrumentationKey)
    {
        options.ExportOptions.Exporters.Add(new ExporterConfiguration
        {
            Name = "ApplicationInsights",
            Type = ExporterType.ApplicationInsights,
            Endpoint = endpoint,
            Headers = new Dictionary<string, string>
            {
                ["instrumentationKey"] = instrumentationKey
            },
            Enabled = true
        });

        return options;
    }

    /// <summary>
    /// Configures DataDog export
    /// </summary>
    public static MetalTelemetryOptions UseDataDog(
        this MetalTelemetryOptions options,
        string endpoint,
        string apiKey)
    {
        options.ExportOptions.Exporters.Add(new ExporterConfiguration
        {
            Name = "DataDog",
            Type = ExporterType.DataDog,
            Endpoint = endpoint,
            Headers = new Dictionary<string, string>
            {
                ["DD-API-KEY"] = apiKey
            },
            Enabled = true
        });

        return options;
    }

    /// <summary>
    /// Configures Grafana Cloud export
    /// </summary>
    public static MetalTelemetryOptions UseGrafanaCloud(
        this MetalTelemetryOptions options,
        string endpoint,
        string apiKey)
    {
        options.ExportOptions.Exporters.Add(new ExporterConfiguration
        {
            Name = "GrafanaCloud",
            Type = ExporterType.Grafana,
            Endpoint = endpoint,
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiKey}"
            },
            Enabled = true
        });

        return options;
    }

    /// <summary>
    /// Configures OpenTelemetry export
    /// </summary>
    public static MetalTelemetryOptions UseOpenTelemetry(
        this MetalTelemetryOptions options,
        string endpoint,
        Dictionary<string, string>? headers = null)
    {
        options.ExportOptions.Exporters.Add(new ExporterConfiguration
        {
            Name = "OpenTelemetry",
            Type = ExporterType.OpenTelemetry,
            Endpoint = endpoint,
            Headers = headers,
            Enabled = true
        });

        return options;
    }

    /// <summary>
    /// Enables alert notifications
    /// </summary>
    public static MetalTelemetryOptions EnableAlerts(
        this MetalTelemetryOptions options,
        params string[] notificationEndpoints)
    {
        options.AlertsOptions.EnableNotifications = true;
        foreach (var endpoint in notificationEndpoints)
        {
            options.AlertsOptions.NotificationEndpoints.Add(endpoint);
        }

        return options;
    }

    /// <summary>
    /// Configures performance thresholds
    /// </summary>
    public static MetalTelemetryOptions SetPerformanceThresholds(
        this MetalTelemetryOptions options,
        double slowOperationMs = 100.0,
        double highGpuUtilization = 85.0,
        double highMemoryUtilization = 80.0)
    {
        options.SlowOperationThresholdMs = slowOperationMs;
        options.HighGpuUtilizationThreshold = highGpuUtilization;
        options.HighMemoryUtilizationThreshold = highMemoryUtilization;

        options.AlertsOptions.SlowOperationThresholdMs = slowOperationMs;
        options.AlertsOptions.HighGpuUtilizationThreshold = highGpuUtilization;
        options.AlertsOptions.HighMemoryUtilizationThreshold = highMemoryUtilization;

        return options;
    }

    /// <summary>
    /// Configures telemetry intervals
    /// </summary>
    public static MetalTelemetryOptions SetIntervals(
        this MetalTelemetryOptions options,
        TimeSpan? reportingInterval = null,
        TimeSpan? cleanupInterval = null,
        TimeSpan? exportInterval = null)
    {
        if (reportingInterval.HasValue)
        {
            options.ReportingInterval = reportingInterval.Value;
        }

        if (cleanupInterval.HasValue)
        {
            options.CleanupInterval = cleanupInterval.Value;
        }

        if (exportInterval.HasValue)
        {
            options.ExportOptions.AutoExportInterval = exportInterval.Value;
        }


        return options;
    }

    /// <summary>
    /// Enables production logging features
    /// </summary>
    public static MetalTelemetryOptions EnableProductionLogging(
        this MetalTelemetryOptions options,
        bool useJsonFormat = true,
        bool enableCorrelationTracking = true,
        params string[] externalEndpoints)
    {
        options.LoggingOptions.UseJsonFormat = useJsonFormat;
        options.LoggingOptions.EnableCorrelationTracking = enableCorrelationTracking;
        options.LoggingOptions.EnablePerformanceLogging = true;


        if (externalEndpoints.Length > 0)
        {
            options.LoggingOptions.ExternalLogEndpoints = [.. externalEndpoints];
        }

        return options;
    }
}

/// <summary>
/// Hosted service for managing Metal telemetry background tasks
/// </summary>
internal sealed class MetalTelemetryHostedService(
    MetalTelemetryManager telemetryManager,
    IOptions<MetalTelemetryOptions> options,
    ILogger<MetalTelemetryHostedService> logger) : BackgroundService
{
    private MetalTelemetryManager? _telemetryManager = telemetryManager;
    private readonly MetalTelemetryOptions _options = options.Value;
    private readonly ILogger<MetalTelemetryHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metal telemetry hosted service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_options.ReportingInterval, stoppingToken);

                try
                {
                    if (_telemetryManager is null)
                    {
                        break;
                    }

                    // Generate periodic health report
                    var report = _telemetryManager.GenerateProductionReport();


                    _logger.LogInformation("Telemetry health check - Operations: {Operations}, Errors: {Errors}, Health: {Health}",
                        report.Snapshot.TotalOperations, report.Snapshot.TotalErrors, report.Snapshot.HealthStatus);

                    // Export metrics if configured
                    if (_options.AutoExportMetrics)
                    {
                        await _telemetryManager.ExportMetricsAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during telemetry background task");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in telemetry hosted service");
        }
        finally
        {
            _logger.LogInformation("Metal telemetry hosted service stopped");
        }
    }

    public override void Dispose()
    {
        _telemetryManager?.Dispose();
        _telemetryManager = null;
        base.Dispose();
    }
}

/// <summary>
/// Builder for configuring Metal telemetry
/// </summary>
public sealed class MetalTelemetryBuilder
{
    private readonly IServiceCollection _services;
    private readonly MetalTelemetryOptions _options = new();

    internal MetalTelemetryBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds Prometheus metrics export
    /// </summary>
    public MetalTelemetryBuilder AddPrometheus(string endpoint, Dictionary<string, string>? headers = null)
    {
        _ = _options.UsePrometheusExport(endpoint, headers);
        return this;
    }

    /// <summary>
    /// Adds Application Insights export
    /// </summary>
    public MetalTelemetryBuilder AddApplicationInsights(string endpoint, string instrumentationKey)
    {
        _ = _options.UseApplicationInsights(endpoint, instrumentationKey);
        return this;
    }

    /// <summary>
    /// Adds DataDog export
    /// </summary>
    public MetalTelemetryBuilder AddDataDog(string endpoint, string apiKey)
    {
        _ = _options.UseDataDog(endpoint, apiKey);
        return this;
    }

    /// <summary>
    /// Sets performance thresholds
    /// </summary>
    public MetalTelemetryBuilder WithPerformanceThresholds(
        double slowOperationMs = 100.0,
        double highGpuUtilization = 85.0,
        double highMemoryUtilization = 80.0)
    {
        _ = _options.SetPerformanceThresholds(slowOperationMs, highGpuUtilization, highMemoryUtilization);
        return this;
    }

    /// <summary>
    /// Enables alerts
    /// </summary>
    public MetalTelemetryBuilder EnableAlerts(params string[] notificationEndpoints)
    {
        _ = _options.EnableAlerts(notificationEndpoints);
        return this;
    }

    /// <summary>
    /// Builds the telemetry configuration
    /// </summary>
    public IServiceCollection Build()
    {
        _ = _services.Configure<MetalTelemetryOptions>(opts =>
        {
            opts.ReportingInterval = _options.ReportingInterval;
            opts.CleanupInterval = _options.CleanupInterval;
            opts.MetricsRetentionPeriod = _options.MetricsRetentionPeriod;
            opts.AutoExportMetrics = _options.AutoExportMetrics;
            opts.SlowOperationThresholdMs = _options.SlowOperationThresholdMs;
            opts.HighGpuUtilizationThreshold = _options.HighGpuUtilizationThreshold;
            opts.HighMemoryUtilizationThreshold = _options.HighMemoryUtilizationThreshold;
            opts.HighResourceUtilizationThreshold = _options.HighResourceUtilizationThreshold;
            opts.PerformanceCountersOptions = _options.PerformanceCountersOptions;
            opts.HealthMonitorOptions = _options.HealthMonitorOptions;
            opts.LoggingOptions = _options.LoggingOptions;
            opts.ExportOptions = _options.ExportOptions;
            opts.AlertsOptions = _options.AlertsOptions;
        });

        return _services.AddMetalTelemetry();
    }
}

/// <summary>
/// Extension for fluent telemetry configuration
/// </summary>
public static class ServiceCollectionTelemetryExtensions
{
    /// <summary>
    /// Starts building Metal telemetry configuration
    /// </summary>
    public static MetalTelemetryBuilder AddMetalTelemetryBuilder(this IServiceCollection services) => new(services);
}
