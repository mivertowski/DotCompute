// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Runtime.DependencyInjection.Metrics;

/// <summary>
/// Default plugin metrics implementation.
/// </summary>
internal sealed class PluginMetrics : IPluginMetrics
{
    private readonly ConcurrentDictionary<string, PluginMetric> _metrics = new();

    public void RecordActivation(Type pluginType)
    {
        var typeName = pluginType.FullName ?? pluginType.Name;
        _ = _metrics.AddOrUpdate(typeName,
            new PluginMetric { ActivationCount = 1 },
            (_, existing) => existing with { ActivationCount = existing.ActivationCount + 1 });
    }

    public void RecordExecutionTime(Type pluginType, TimeSpan executionTime)
    {
        var typeName = pluginType.FullName ?? pluginType.Name;
        _ = _metrics.AddOrUpdate(typeName,
            new PluginMetric { TotalExecutionTime = executionTime },
            (_, existing) => existing with
            {
                TotalExecutionTime = existing.TotalExecutionTime + executionTime,
                ExecutionCount = existing.ExecutionCount + 1
            });
    }

    public PluginMetricsData GetMetrics()
    {
        return new PluginMetricsData
        {
            PluginMetrics = _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CollectionTime = DateTime.UtcNow
        };
    }
}