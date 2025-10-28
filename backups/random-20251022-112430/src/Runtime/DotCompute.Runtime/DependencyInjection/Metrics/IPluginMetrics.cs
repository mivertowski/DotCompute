// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Metrics;

/// <summary>
/// Plugin metrics interface.
/// </summary>
public interface IPluginMetrics
{
    /// <summary>
    /// Records plugin activation.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    public void RecordActivation(Type pluginType);

    /// <summary>
    /// Records plugin execution time.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <param name="executionTime">The execution time.</param>
    public void RecordExecutionTime(Type pluginType, TimeSpan executionTime);

    /// <summary>
    /// Gets plugin metrics.
    /// </summary>
    /// <returns>Plugin metrics data.</returns>
    public PluginMetricsData GetMetrics();
}