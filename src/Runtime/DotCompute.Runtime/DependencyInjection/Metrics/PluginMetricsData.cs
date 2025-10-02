// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Metrics;

/// <summary>
/// Plugin metrics data.
/// </summary>
public sealed class PluginMetricsData
{
    /// <summary>
    /// Gets or sets the plugin metrics by type name.
    /// </summary>
    public Dictionary<string, PluginMetric> PluginMetrics { get; } = [];

    /// <summary>
    /// Gets or sets the metrics collection time.
    /// </summary>
    public DateTime CollectionTime { get; set; }
}