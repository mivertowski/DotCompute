// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Provides comprehensive statistics dashboard and reporting for Metal memory pools.
/// </summary>
public static class MetalPoolStatisticsDashboard
{
    /// <summary>
    /// Generates a comprehensive dashboard report for memory pool statistics.
    /// </summary>
    public static string GenerateDashboard(MemoryPoolManagerStatistics stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var dashboard = new StringBuilder();
        dashboard.AppendLine("╔════════════════════════════════════════════════════════════════════════╗");
        dashboard.AppendLine("║          Metal Memory Pool Performance Dashboard                      ║");
        dashboard.AppendLine("╚════════════════════════════════════════════════════════════════════════╝");
        dashboard.AppendLine();

        // Overall Statistics
        AppendSection(dashboard, "Overall Statistics", () =>
        {
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Total Allocations:        {stats.TotalAllocations,15:N0}");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Pool Hits:                {stats.PoolHits,15:N0}  ({stats.HitRate:P2})");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Pool Misses:              {stats.PoolMisses,15:N0}");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Allocation Reduction:     {stats.AllocationReductionPercentage,14:F1}%");
            dashboard.AppendLine();
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Total Bytes Allocated:    {FormatBytes(stats.TotalBytesAllocated),15}");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Current Pool Size:        {FormatBytes(stats.TotalBytesInPools),15}");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Peak Pool Size:           {FormatBytes(stats.PeakBytesInPools),15}");
        });

        // Performance Metrics
        AppendSection(dashboard, "Performance Metrics", () =>
        {
            var activePools = stats.PoolStatistics.Where(p => p.TotalAllocations > 0).ToList();
            var avgEfficiency = activePools.Count > 0 ? activePools.Average(p => p.EfficiencyScore) : 0;
            var avgHitRate = activePools.Count > 0 ? activePools.Average(p => p.HitRate) : 0;

            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Average Efficiency:       {avgEfficiency,14:F1}");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Average Hit Rate:         {avgHitRate,14:P2}");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Active Pool Classes:      {activePools.Count,15:N0}");

            // Estimate memory savings
            var theoreticalDirectAllocs = stats.TotalAllocations;
            var actualMetalAllocs = stats.PoolMisses;
            var savedAllocations = theoreticalDirectAllocs - actualMetalAllocs;
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Saved Allocations:        {savedAllocations,15:N0}");
        });

        // Pool Size Breakdown
        AppendSection(dashboard, "Pool Size Breakdown", () =>
        {
            dashboard.AppendLine("  ┌─────────────┬───────────┬──────────┬──────────┬────────────┬────────────┐");
            dashboard.AppendLine("  │  Pool Size  │   Allocs  │ Hit Rate │  Cached  │   Bytes    │ Efficiency │");
            dashboard.AppendLine("  ├─────────────┼───────────┼──────────┼──────────┼────────────┼────────────┤");

            foreach (var pool in stats.PoolStatistics.Where(p => p.TotalAllocations > 0))
            {
                dashboard.AppendLine(CultureInfo.InvariantCulture, $"  │ {FormatSize(pool.PoolSize),11} │ {pool.TotalAllocations,9:N0} │ {pool.HitRate,8:P1} │ {pool.AvailableBuffers,8} │ {FormatBytes(pool.BytesInPool),10} │ {pool.EfficiencyScore,9:F1} │");
            }

            dashboard.AppendLine("  └─────────────┴───────────┴──────────┴──────────┴────────────┴────────────┘");
        });

        // Efficiency Analysis
        AppendSection(dashboard, "Efficiency Analysis", () =>
        {
            var rating = GetEfficiencyRating(stats.AllocationReductionPercentage);
            var healthStatus = GetHealthStatus(stats);

            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Overall Rating:           {rating}");
            dashboard.AppendLine(CultureInfo.InvariantCulture, $"  Health Status:            {healthStatus}");
            dashboard.AppendLine();

            // Recommendations
            var recommendations = GenerateRecommendations(stats);
            if (recommendations.Length > 0)
            {
                dashboard.AppendLine("  Recommendations:");
                foreach (var rec in recommendations)
                {
                    dashboard.AppendLine(CultureInfo.InvariantCulture, $"    • {rec}");
                }
            }
            else
            {
                dashboard.AppendLine("  ✓ Pool is operating at optimal performance");
            }
        });

        // Memory Distribution Chart
        AppendSection(dashboard, "Memory Distribution", () =>
        {
            AppendMemoryDistributionChart(dashboard, stats);
        });

        dashboard.AppendLine("╔════════════════════════════════════════════════════════════════════════╗");
        dashboard.AppendLine("║  End of Dashboard                                                      ║");
        dashboard.AppendLine("╚════════════════════════════════════════════════════════════════════════╝");

        return dashboard.ToString();
    }

    /// <summary>
    /// Generates a compact summary report for logging.
    /// </summary>
    public static string GenerateCompactSummary(MemoryPoolManagerStatistics stats)
    {
        return string.Create(CultureInfo.InvariantCulture, $"Metal Pool Stats: {stats.TotalAllocations:N0} allocs, " +
               $"{stats.HitRate:P1} hit rate, {stats.AllocationReductionPercentage:F1}% reduction, " +
               $"Peak: {FormatBytes(stats.PeakBytesInPools)}, " +
               $"Health: {GetHealthStatus(stats)}");
    }

    private static void AppendSection(StringBuilder sb, string title, Action content)
    {
        var line = $"┌─ {title} " + new string('─', Math.Max(1, 72 - title.Length - 4));
        sb.AppendLine(line);
        content();
        sb.AppendLine();
    }

    private static void AppendMemoryDistributionChart(StringBuilder sb, MemoryPoolManagerStatistics stats)
    {
        var activePools = stats.PoolStatistics
            .Where(p => p.BytesInPool > 0)
            .OrderByDescending(p => p.BytesInPool)
            .Take(10)
            .ToList();

        if (activePools.Count == 0)
        {
            sb.AppendLine("  No active pools");
            return;
        }

        var maxBytes = activePools.Max(p => p.BytesInPool);
        const int barWidth = 50;

        foreach (var pool in activePools)
        {
            var barLength = (int)((double)pool.BytesInPool / maxBytes * barWidth);
            var bar = new string('█', barLength) + new string('░', barWidth - barLength);
            var percentage = (double)pool.BytesInPool / stats.TotalBytesInPools * 100;

            sb.AppendLine(CultureInfo.InvariantCulture, $"  {FormatSize(pool.PoolSize),11} │{bar}│ {percentage,5:F1}% ({FormatBytes(pool.BytesInPool)})");
        }
    }

    private static string GetEfficiencyRating(double allocationReduction)
    {
        return allocationReduction switch
        {
            >= 90 => "★★★★★ Excellent (90%+ reduction)",
            >= 80 => "★★★★☆ Very Good (80-90% reduction)",
            >= 70 => "★★★☆☆ Good (70-80% reduction)",
            >= 50 => "★★☆☆☆ Fair (50-70% reduction)",
            _ => "★☆☆☆☆ Poor (<50% reduction)"
        };
    }

    private static string GetHealthStatus(MemoryPoolManagerStatistics stats)
    {
        var hitRate = stats.HitRate;
        var reduction = stats.AllocationReductionPercentage;

        if (hitRate >= 0.9 && reduction >= 85)
        {
            return "✓ Healthy";
        }

        if (hitRate >= 0.7 && reduction >= 70)
        {
            return "⚠ Good";
        }

        if (hitRate >= 0.5 && reduction >= 50)
        {
            return "⚠ Fair";
        }

        return "✗ Needs Attention";
    }

    private static string[] GenerateRecommendations(MemoryPoolManagerStatistics stats)
    {
        var recommendations = new System.Collections.Generic.List<string>();

        if (stats.HitRate < 0.7)
        {
            recommendations.Add("Consider pre-allocating buffers for frequently used sizes");
        }

        if (stats.AllocationReductionPercentage < 70)
        {
            recommendations.Add("Pool hit rate is low - workload may have irregular allocation patterns");
        }

        var highFragmentationPools = stats.PoolStatistics
            .Where(p => p.FragmentationPercentage > 30 && p.TotalAllocations > 100)
            .ToList();

        if (highFragmentationPools.Count > 0)
        {
            recommendations.Add($"High fragmentation detected in {highFragmentationPools.Count} pool(s) - consider cleanup");
        }

        var lowEfficiencyPools = stats.PoolStatistics
            .Where(p => p.EfficiencyScore < 50 && p.TotalAllocations > 50)
            .ToList();

        if (lowEfficiencyPools.Count > 0)
        {
            recommendations.Add($"{lowEfficiencyPools.Count} pool(s) have low efficiency - review allocation patterns");
        }

        if (stats.TotalBytesInPools > stats.TotalBytesAllocated * 2)
        {
            recommendations.Add("Pool is holding significantly more memory than allocated - consider aggressive cleanup");
        }

        return recommendations.ToArray();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0)
        {
            return "0 B";
        }

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{size:F2} {sizes[order]}");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes} B");
        }

        if (bytes < 1024 * 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024} KB");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024 * 1024)} MB");
    }
}
