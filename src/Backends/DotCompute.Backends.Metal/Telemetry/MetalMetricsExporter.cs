// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Exports Metal metrics to various monitoring and observability systems
/// </summary>
public sealed partial class MetalMetricsExporter : IDisposable
{
    private readonly ILogger<MetalMetricsExporter> _logger;
    private readonly MetalExportOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Timer? _exportTimer;
    private volatile bool _disposed;

    public MetalMetricsExporter(
        ILogger<MetalMetricsExporter> logger,
        MetalExportOptions options)
    {
        _logger = logger;
        _options = options;
        _httpClient = new HttpClient();


        ConfigureHttpClient();

        if (_options.AutoExportInterval > TimeSpan.Zero)
        {
            _exportTimer = new Timer(AutoExportMetrics, null, _options.AutoExportInterval, _options.AutoExportInterval);
        }

        LogExporterInitialized(_logger, _options.Exporters.Count, _options.AutoExportInterval > TimeSpan.Zero);
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6100,
        Level = LogLevel.Information,
        Message = "Metal metrics exporter initialized with {ExporterCount} configured exporters, auto-export: {AutoExport}")]
    private static partial void LogExporterInitialized(ILogger logger, int exporterCount, bool autoExport);

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Error,
        Message = "Failed to initiate export to {ExporterType}: {ExporterName}")]
    private static partial void LogExportInitiationFailed(ILogger logger, Exception ex, ExporterType exporterType, string exporterName);

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Debug,
        Message = "Successfully exported metrics to {Count} monitoring systems")]
    private static partial void LogMetricsExportedSuccessfully(ILogger logger, int count);

    [LoggerMessage(
        EventId = 6103,
        Level = LogLevel.Warning,
        Message = "Metrics export timed out after {Timeout}ms")]
    private static partial void LogExportTimeout(ILogger logger, double timeout);

    [LoggerMessage(
        EventId = 6104,
        Level = LogLevel.Error,
        Message = "Error during metrics export")]
    private static partial void LogMetricsExportError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6105,
        Level = LogLevel.Trace,
        Message = "Successfully exported metrics to Prometheus: {Endpoint}")]
    private static partial void LogPrometheusExportSuccess(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 6106,
        Level = LogLevel.Warning,
        Message = "Failed to export to Prometheus: {StatusCode} {ReasonPhrase}")]
    private static partial void LogPrometheusExportFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string? reasonPhrase);

    [LoggerMessage(
        EventId = 6107,
        Level = LogLevel.Error,
        Message = "Error exporting to Prometheus: {Endpoint}")]
    private static partial void LogPrometheusExportError(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(
        EventId = 6108,
        Level = LogLevel.Trace,
        Message = "Successfully exported metrics to OpenTelemetry: {Endpoint}")]
    private static partial void LogOpenTelemetryExportSuccess(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 6109,
        Level = LogLevel.Warning,
        Message = "Failed to export to OpenTelemetry: {StatusCode} {ReasonPhrase}")]
    private static partial void LogOpenTelemetryExportFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string? reasonPhrase);

    [LoggerMessage(
        EventId = 6110,
        Level = LogLevel.Error,
        Message = "Error exporting to OpenTelemetry: {Endpoint}")]
    private static partial void LogOpenTelemetryExportError(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(
        EventId = 6111,
        Level = LogLevel.Trace,
        Message = "Successfully exported metrics to Application Insights: {Endpoint}")]
    private static partial void LogApplicationInsightsExportSuccess(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 6112,
        Level = LogLevel.Warning,
        Message = "Failed to export to Application Insights: {StatusCode} {ReasonPhrase}")]
    private static partial void LogApplicationInsightsExportFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string? reasonPhrase);

    [LoggerMessage(
        EventId = 6113,
        Level = LogLevel.Error,
        Message = "Error exporting to Application Insights: {Endpoint}")]
    private static partial void LogApplicationInsightsExportError(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(
        EventId = 6114,
        Level = LogLevel.Trace,
        Message = "Successfully exported metrics to DataDog: {Endpoint}")]
    private static partial void LogDataDogExportSuccess(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 6115,
        Level = LogLevel.Warning,
        Message = "Failed to export to DataDog: {StatusCode} {ReasonPhrase}")]
    private static partial void LogDataDogExportFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string? reasonPhrase);

    [LoggerMessage(
        EventId = 6116,
        Level = LogLevel.Error,
        Message = "Error exporting to DataDog: {Endpoint}")]
    private static partial void LogDataDogExportError(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(
        EventId = 6117,
        Level = LogLevel.Trace,
        Message = "Successfully exported metrics to Grafana: {Endpoint}")]
    private static partial void LogGrafanaExportSuccess(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 6118,
        Level = LogLevel.Warning,
        Message = "Failed to export to Grafana: {StatusCode} {ReasonPhrase}")]
    private static partial void LogGrafanaExportFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string? reasonPhrase);

    [LoggerMessage(
        EventId = 6119,
        Level = LogLevel.Error,
        Message = "Error exporting to Grafana: {Endpoint}")]
    private static partial void LogGrafanaExportError(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(
        EventId = 6120,
        Level = LogLevel.Trace,
        Message = "Successfully exported metrics to custom endpoint: {Endpoint}")]
    private static partial void LogCustomEndpointExportSuccess(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 6121,
        Level = LogLevel.Warning,
        Message = "Failed to export to custom endpoint: {StatusCode} {ReasonPhrase}")]
    private static partial void LogCustomEndpointExportFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string? reasonPhrase);

    [LoggerMessage(
        EventId = 6122,
        Level = LogLevel.Error,
        Message = "Error exporting to custom endpoint: {Endpoint}")]
    private static partial void LogCustomEndpointExportError(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(
        EventId = 6123,
        Level = LogLevel.Warning,
        Message = "Failed to add header {HeaderKey} for exporter {ExporterName}")]
    private static partial void LogHeaderAddFailed(ILogger logger, Exception ex, string headerKey, string exporterName);

    [LoggerMessage(
        EventId = 6124,
        Level = LogLevel.Trace,
        Message = "Auto-export timer triggered")]
    private static partial void LogAutoExportTriggered(ILogger logger);

    [LoggerMessage(
        EventId = 6125,
        Level = LogLevel.Error,
        Message = "Error during auto-export")]
    private static partial void LogAutoExportError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6126,
        Level = LogLevel.Debug,
        Message = "Metal metrics exporter disposed")]
    private static partial void LogExporterDisposed(ILogger logger);

    #endregion

    /// <summary>
    /// Exports telemetry snapshot to configured monitoring systems
    /// </summary>
    public async Task ExportAsync(MetalTelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }


        var exportTasks = new List<Task>();

        foreach (var exporter in _options.Exporters)
        {
            try
            {
                var exportTask = exporter.Type switch
                {
                    ExporterType.Prometheus => ExportToPrometheusAsync(exporter, snapshot, cancellationToken),
                    ExporterType.OpenTelemetry => ExportToOpenTelemetryAsync(exporter, snapshot, cancellationToken),
                    ExporterType.ApplicationInsights => ExportToApplicationInsightsAsync(exporter, snapshot, cancellationToken),
                    ExporterType.DataDog => ExportToDataDogAsync(exporter, snapshot, cancellationToken),
                    ExporterType.Grafana => ExportToGrafanaAsync(exporter, snapshot, cancellationToken),
                    ExporterType.Custom => ExportToCustomEndpointAsync(exporter, snapshot, cancellationToken),
                    _ => Task.CompletedTask
                };

                exportTasks.Add(exportTask);
            }
            catch (Exception ex)
            {
                LogExportInitiationFailed(_logger, ex, exporter.Type, exporter.Name);
            }
        }

        // Wait for all exports to complete or timeout
        if (exportTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(exportTasks).WaitAsync(_options.ExportTimeout, cancellationToken);
                LogMetricsExportedSuccessfully(_logger, exportTasks.Count);
            }
            catch (TimeoutException)
            {
                LogExportTimeout(_logger, _options.ExportTimeout.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                LogMetricsExportError(_logger, ex);
            }
        }
    }

    /// <summary>
    /// Gets metrics in a format suitable for external systems
    /// </summary>
    public Dictionary<string, object> GetExportableMetrics()
    {
        if (_disposed)
        {
            return [];
        }


        var exportableMetrics = new Dictionary<string, object>
        {
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["exporter_version"] = "1.0.0",
            ["system_info"] = new Dictionary<string, object>
            {
                ["machine_name"] = Environment.MachineName,
                ["os_version"] = Environment.OSVersion.VersionString,
                ["processor_count"] = Environment.ProcessorCount,
                ["working_set"] = Environment.WorkingSet,
                ["process_id"] = Environment.ProcessId
            }
        };

        return exportableMetrics;
    }

    private async Task ExportToPrometheusAsync(ExporterConfiguration exporter, MetalTelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var prometheusFormat = ConvertToPrometheusFormat(snapshot);


            var content = new StringContent(prometheusFormat, Encoding.UTF8, "text/plain");


            var response = await _httpClient.PostAsync(exporter.Endpoint, content, cancellationToken);


            if (response.IsSuccessStatusCode)
            {
                LogPrometheusExportSuccess(_logger, exporter.Endpoint);
            }
            else
            {
                LogPrometheusExportFailed(_logger, response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            LogPrometheusExportError(_logger, ex, exporter.Endpoint);
            throw;
        }
    }

    private async Task ExportToOpenTelemetryAsync(ExporterConfiguration exporter, MetalTelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var otlpData = ConvertToOTLPFormat(snapshot);


            var json = JsonSerializer.Serialize(otlpData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");


            var response = await _httpClient.PostAsync(exporter.Endpoint, content, cancellationToken);


            if (response.IsSuccessStatusCode)
            {
                LogOpenTelemetryExportSuccess(_logger, exporter.Endpoint);
            }
            else
            {
                LogOpenTelemetryExportFailed(_logger, response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            LogOpenTelemetryExportError(_logger, ex, exporter.Endpoint);
            throw;
        }
    }

    private async Task ExportToApplicationInsightsAsync(ExporterConfiguration exporter, MetalTelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var appInsightsData = ConvertToApplicationInsightsFormat(snapshot, exporter);


            var json = JsonSerializer.Serialize(appInsightsData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add instrumentation key header

            if (exporter.Headers?.ContainsKey("instrumentationKey") == true)
            {
                _httpClient.DefaultRequestHeaders.Add("instrumentationKey", exporter.Headers["instrumentationKey"]);
            }


            var response = await _httpClient.PostAsync(exporter.Endpoint, content, cancellationToken);


            if (response.IsSuccessStatusCode)
            {
                LogApplicationInsightsExportSuccess(_logger, exporter.Endpoint);
            }
            else
            {
                LogApplicationInsightsExportFailed(_logger, response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            LogApplicationInsightsExportError(_logger, ex, exporter.Endpoint);
            throw;
        }
    }

    private async Task ExportToDataDogAsync(ExporterConfiguration exporter, MetalTelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var dataDogMetrics = ConvertToDataDogFormat(snapshot);


            var json = JsonSerializer.Serialize(dataDogMetrics);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add API key header

            if (exporter.Headers?.ContainsKey("DD-API-KEY") == true)
            {
                content.Headers.Add("DD-API-KEY", exporter.Headers["DD-API-KEY"]);
            }


            var response = await _httpClient.PostAsync(exporter.Endpoint, content, cancellationToken);


            if (response.IsSuccessStatusCode)
            {
                LogDataDogExportSuccess(_logger, exporter.Endpoint);
            }
            else
            {
                LogDataDogExportFailed(_logger, response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            LogDataDogExportError(_logger, ex, exporter.Endpoint);
            throw;
        }
    }

    private async Task ExportToGrafanaAsync(ExporterConfiguration exporter, MetalTelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var grafanaData = ConvertToGrafanaFormat(snapshot);


            var json = JsonSerializer.Serialize(grafanaData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");


            var response = await _httpClient.PostAsync(exporter.Endpoint, content, cancellationToken);


            if (response.IsSuccessStatusCode)
            {
                LogGrafanaExportSuccess(_logger, exporter.Endpoint);
            }
            else
            {
                LogGrafanaExportFailed(_logger, response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            LogGrafanaExportError(_logger, ex, exporter.Endpoint);
            throw;
        }
    }

    private async Task ExportToCustomEndpointAsync(ExporterConfiguration exporter, MetalTelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var customData = ConvertToCustomFormat(snapshot, exporter);


            var json = JsonSerializer.Serialize(customData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");


            var response = await _httpClient.PostAsync(exporter.Endpoint, content, cancellationToken);


            if (response.IsSuccessStatusCode)
            {
                LogCustomEndpointExportSuccess(_logger, exporter.Endpoint);
            }
            else
            {
                LogCustomEndpointExportFailed(_logger, response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            LogCustomEndpointExportError(_logger, ex, exporter.Endpoint);
            throw;
        }
    }

    private static string ConvertToPrometheusFormat(MetalTelemetrySnapshot snapshot)
    {
        var sb = new StringBuilder();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // System metrics
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP metal_operations_total Total number of Metal operations");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE metal_operations_total counter");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"metal_operations_total {snapshot.TotalOperations} {timestamp}");

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP metal_errors_total Total number of Metal errors");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE metal_errors_total counter");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"metal_errors_total {snapshot.TotalErrors} {timestamp}");

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP metal_error_rate Current error rate");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE metal_error_rate gauge");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"metal_error_rate {snapshot.ErrorRate:F6} {timestamp}");

        // Operation metrics
        foreach (var operation in snapshot.OperationMetrics)
        {
            var safeName = SanitizePrometheusName(operation.Key);


            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP metal_operation_duration_ms_{safeName} Average duration of {operation.Key} operations");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE metal_operation_duration_ms_{safeName} gauge");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"metal_operation_duration_ms_{safeName} {operation.Value.AverageExecutionTime.TotalMilliseconds:F2} {timestamp}");


            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP metal_operation_count_{safeName} Number of {operation.Key} operations");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE metal_operation_count_{safeName} counter");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"metal_operation_count_{safeName} {operation.Value.TotalExecutions} {timestamp}");
        }

        // Resource metrics
        foreach (var resource in snapshot.ResourceMetrics)
        {
            var safeName = SanitizePrometheusName(resource.Key);


            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP metal_resource_utilization_{safeName} Utilization percentage for {resource.Key}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE metal_resource_utilization_{safeName} gauge");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"metal_resource_utilization_{safeName} {resource.Value.UtilizationPercentage:F2} {timestamp}");


            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP metal_resource_usage_{safeName} Current usage for {resource.Key}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE metal_resource_usage_{safeName} gauge");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"metal_resource_usage_{safeName} {resource.Value.CurrentUsage} {timestamp}");
        }

        return sb.ToString();
    }

    private static object ConvertToOTLPFormat(MetalTelemetrySnapshot snapshot)
    {
        var resourceMetrics = new
        {
            resource = new
            {
                attributes = new[]
                {
                    new { key = "service.name", value = new { stringValue = "dotcompute-metal" } },
                    new { key = "service.version", value = new { stringValue = "1.0.0" } },
                    new { key = "host.name", value = new { stringValue = Environment.MachineName } }
                }
            },
            scopeMetrics = new[]
            {
                new
                {
                    scope = new
                    {
                        name = "DotCompute.Backends.Metal",
                        version = "1.0.0"
                    },
                    metrics = CreateOTLPMetrics(snapshot)
                }
            }
        };

        return new { resourceMetrics = new[] { resourceMetrics } };
    }

    private static object[] CreateOTLPMetrics(MetalTelemetrySnapshot snapshot)
    {
        var metrics = new List<object>
        {
            // Add system-level metrics
            new
            {
                name = "metal_operations_total",
                unit = "1",
                sum = new
                {
                    dataPoints = new[]
                {
                    new
                    {
                        timeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
                        asInt = snapshot.TotalOperations,
                        attributes = new object[] { }
                    }
                },
                    aggregationTemporality = 2, // Cumulative
                    isMonotonic = true
                }
            },
            new
            {
                name = "metal_error_rate",
                unit = "1",
                gauge = new
                {
                    dataPoints = new[]
                {
                    new
                    {
                        timeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
                        asDouble = snapshot.ErrorRate,
                        attributes = new object[] { }
                    }
                }
                }
            }
        };

        return [.. metrics];
    }

    private static object ConvertToApplicationInsightsFormat(MetalTelemetrySnapshot snapshot, ExporterConfiguration exporter)
    {
        var telemetryItems = new List<object>
        {
            // Custom metrics
            new
            {
                name = "Microsoft.ApplicationInsights.Metric",
                time = DateTimeOffset.UtcNow,
                iKey = exporter.Headers?.GetValueOrDefault("instrumentationKey"),
                data = new
                {
                    baseType = "MetricData",
                    baseData = new
                    {
                        metrics = new object[]
                    {
                        new
                        {
                            name = "Metal.Operations.Total",
                            value = snapshot.TotalOperations,
                            kind = 1 // Measurement
                        },
                        new
                        {
                            name = "Metal.Error.Rate",
                            value = snapshot.ErrorRate,
                            kind = 1
                        }
                    },
                        properties = new Dictionary<string, string>
                        {
                            ["backend"] = "Metal",
                            ["version"] = "1.0.0"
                        }
                    }
                }
            }
        };

        return new { items = telemetryItems };
    }

    private static object ConvertToDataDogFormat(MetalTelemetrySnapshot snapshot)
    {
        var series = new List<object>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        series.Add(new
        {
            metric = "dotcompute.metal.operations.total",
            points = new[] { new[] { timestamp, snapshot.TotalOperations } },
            type = "count",
            tags = new[] { "backend:metal", "service:dotcompute" }
        });

        series.Add(new
        {
            metric = "dotcompute.metal.error.rate",
            points = new[] { new[] { timestamp, snapshot.ErrorRate } },
            type = "gauge",
            tags = new[] { "backend:metal", "service:dotcompute" }
        });

        // Operation-specific metrics
        foreach (var operation in snapshot.OperationMetrics)
        {
            series.Add(new
            {
                metric = $"dotcompute.metal.operation.duration",
                points = new[] { new[] { timestamp, operation.Value.AverageExecutionTime.TotalMilliseconds } },
                type = "gauge",
                tags = new[] { $"operation:{operation.Key}", "backend:metal", "service:dotcompute" }
            });
        }

        return new { series };
    }

    private static object ConvertToGrafanaFormat(MetalTelemetrySnapshot snapshot)
    {
        var dataPoints = new List<object>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        dataPoints.Add(new
        {
            name = "metal_operations_total",
            value = snapshot.TotalOperations,
            timestamp,
            tags = new Dictionary<string, string> { ["backend"] = "metal" }
        });

        dataPoints.Add(new
        {
            name = "metal_error_rate",
            value = snapshot.ErrorRate,
            timestamp,
            tags = new Dictionary<string, string> { ["backend"] = "metal" }
        });

        return new { dataPoints };
    }

    private static object ConvertToCustomFormat(MetalTelemetrySnapshot snapshot, ExporterConfiguration exporter)
    {
        // Default JSON format for custom endpoints
        return new
        {
            timestamp = DateTimeOffset.UtcNow,
            service = "DotCompute.Backends.Metal",
            version = "1.0.0",
            metrics = new
            {
                operations_total = snapshot.TotalOperations,
                errors_total = snapshot.TotalErrors,
                error_rate = snapshot.ErrorRate,
                health_status = snapshot.HealthStatus.ToString(),
                system_info = snapshot.SystemInfo
            },
            operation_metrics = snapshot.OperationMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    total_executions = kvp.Value.TotalExecutions,
                    successful_executions = kvp.Value.SuccessfulExecutions,
                    average_duration_ms = kvp.Value.AverageExecutionTime.TotalMilliseconds,
                    success_rate = kvp.Value.SuccessRate
                }),
            resource_metrics = snapshot.ResourceMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    current_usage = kvp.Value.CurrentUsage,
                    peak_usage = kvp.Value.PeakUsage,
                    limit = kvp.Value.Limit,
                    utilization_percentage = kvp.Value.UtilizationPercentage
                })
        };
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = _options.ExportTimeout;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DotCompute.Metal.MetricsExporter/1.0");

        // Add common headers

        foreach (var exporter in _options.Exporters)
        {
            if (exporter.Headers != null)
            {
                foreach (var header in exporter.Headers)
                {
                    try
                    {
                        _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                    catch (Exception ex)
                    {
                        LogHeaderAddFailed(_logger, ex, header.Key, exporter.Name);
                    }
                }
            }
        }
    }

    private void AutoExportMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Auto-export would need access to current telemetry snapshot
            // This is a placeholder for the auto-export functionality
            LogAutoExportTriggered(_logger);
        }
        catch (Exception ex)
        {
            LogAutoExportError(_logger, ex);
        }
    }

    private static string SanitizePrometheusName(string name)
    {
        // Replace invalid characters with underscores for Prometheus compatibility
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                _ = sb.Append(c);
            }
            else
            {
                _ = sb.Append('_');
            }
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            _exportTimer?.Dispose();
            _httpClient?.Dispose();

            LogExporterDisposed(_logger);
        }
    }
}
