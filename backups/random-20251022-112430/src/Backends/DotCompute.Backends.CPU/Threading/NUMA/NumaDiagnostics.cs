// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Diagnostic tools and system analysis for NUMA configurations.
/// </summary>
public static class NumaDiagnostics
{
    /// <summary>
    /// Performs a comprehensive NUMA system analysis.
    /// </summary>
    /// <returns>Detailed diagnostic report.</returns>
    public static NumaDiagnosticReport AnalyzeSystem()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var topology = NumaTopologyDetector.Topology;
            var capabilities = NumaPlatformDetector.DetectCapabilities();

            var issues = new List<DiagnosticIssue>();
            var recommendations = new List<string>();
            var warnings = new List<string>();

            // Analyze topology
            AnalyzeTopology(topology, issues, recommendations, warnings);

            // Analyze platform capabilities
            AnalyzePlatformCapabilities(capabilities, issues, recommendations, warnings);

            // Analyze performance characteristics
            AnalyzePerformanceCharacteristics(topology, issues, recommendations);

            // Analyze configuration
            AnalyzeConfiguration(topology, capabilities, issues, recommendations, warnings);

            var analysisTime = DateTime.UtcNow - startTime;
            var overallHealth = CalculateOverallHealth(issues);

            return new NumaDiagnosticReport
            {
                Timestamp = DateTime.UtcNow,
                Topology = topology,
                Capabilities = capabilities,
                Issues = issues,
                Recommendations = recommendations,
                Warnings = warnings,
                OverallHealth = overallHealth,
                AnalysisTime = analysisTime,
                SystemInfo = GatherSystemInfo()
            };
        }
        catch (Exception ex)
        {
            return new NumaDiagnosticReport
            {
                Timestamp = DateTime.UtcNow,
                Topology = null,
                Capabilities = null,
                Issues = new List<DiagnosticIssue>
                {
                    new()
                    {
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.System,
                        Title = "Diagnostic Analysis Failed",
                        Description = ex.Message,
                        Impact = "Cannot perform NUMA analysis",
                        Recommendation = "Check system configuration and permissions"
                    }
                },
                Recommendations = new List<string>(),
                Warnings = new List<string> { "Diagnostic analysis failed - system may have NUMA issues" },
                OverallHealth = SystemHealth.Critical,
                AnalysisTime = DateTime.UtcNow - startTime,
                SystemInfo = null
            };
        }
    }

    /// <summary>
    /// Validates NUMA configuration and settings.
    /// </summary>
    /// <returns>Validation results.</returns>
    public static ValidationResult ValidateConfiguration()
    {
        var issues = new List<ValidationIssue>();

        try
        {
            var topology = NumaTopologyDetector.Topology;
            var capabilities = NumaPlatformDetector.DetectCapabilities();

            // Validate topology consistency
            ValidateTopologyConsistency(topology, issues);

            // Validate platform support
            ValidatePlatformSupport(capabilities, issues);

            // Validate memory configuration
            ValidateMemoryConfiguration(topology, issues);

            // Validate CPU configuration
            ValidateCpuConfiguration(topology, issues);

            var severity = issues.Count == 0 ? ValidationSeverity.Info :
                          issues.Any(i => i.Severity == ValidationSeverity.Error) ? ValidationSeverity.Error :
                          issues.Any(i => i.Severity == ValidationSeverity.Warning) ? ValidationSeverity.Warning :
                          ValidationSeverity.Info;

            return new ValidationResult
            {
                OverallResult = severity,
                Issues = issues,
                TestedComponents = new[] { "Topology", "Platform", "Memory", "CPU" },
                ValidationTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                OverallResult = ValidationSeverity.Error,
                Issues = new List<ValidationIssue>
                {
                    new()
                    {
                        Component = "System",
                        Severity = ValidationSeverity.Error,
                        Message = $"Validation failed: {ex.Message}",
                        Details = ex.ToString()
                    }
                },
                TestedComponents = Array.Empty<string>(),
                ValidationTime = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Runs performance benchmarks for NUMA operations.
    /// </summary>
    /// <param name="duration">Benchmark duration.</param>
    /// <returns>Benchmark results.</returns>
    public static BenchmarkResult RunPerformanceBenchmark(TimeSpan? duration = null)
    {
        duration ??= TimeSpan.FromSeconds(10);
        var topology = NumaTopologyDetector.Topology;

        var results = new Dictionary<string, BenchmarkMetric>();
        var startTime = DateTime.UtcNow;

        try
        {
            // Memory allocation benchmark
            var memoryResult = BenchmarkMemoryAllocation(topology, duration.Value);
            results["MemoryAllocation"] = memoryResult;

            // CPU affinity benchmark
            var affinityResult = BenchmarkCpuAffinity(topology, duration.Value);
            results["CpuAffinity"] = affinityResult;

            // Cross-node communication benchmark
            var communicationResult = BenchmarkCrossNodeCommunication(topology, duration.Value);
            results["CrossNodeCommunication"] = communicationResult;

            var overallScore = results.Values.Average(m => m.Score);

            return new BenchmarkResult
            {
                OverallScore = overallScore,
                Metrics = results,
                Topology = topology,
                BenchmarkTime = DateTime.UtcNow - startTime,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new BenchmarkResult
            {
                OverallScore = 0.0,
                Metrics = results,
                Topology = topology,
                BenchmarkTime = DateTime.UtcNow - startTime,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates a detailed diagnostic report in text format.
    /// </summary>
    /// <param name="report">Diagnostic report to format.</param>
    /// <returns>Formatted text report.</returns>
    public static string GenerateTextReport(NumaDiagnosticReport report)
    {
        var sb = new StringBuilder();

        _ = sb.AppendLine("NUMA System Diagnostic Report");
        _ = sb.AppendLine("============================");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Generated: {report.Timestamp:yyyy-MM-dd HH:mm:ss}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Analysis Time: {report.AnalysisTime.TotalMilliseconds:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Overall Health: {report.OverallHealth}");
        _ = sb.AppendLine();

        // System Information
        if (report.SystemInfo != null)
        {
            _ = sb.AppendLine("System Information:");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Platform: {report.SystemInfo.Platform}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  OS Version: {report.SystemInfo.OsVersion}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Total Memory: {report.SystemInfo.TotalMemoryGB:F2} GB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  CPU Count: {report.SystemInfo.ProcessorCount}");
            _ = sb.AppendLine();
        }

        // Topology Information
        if (report.Topology != null)
        {
            _ = sb.AppendLine("NUMA Topology:");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Node Count: {report.Topology.NodeCount}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Is NUMA System: {report.Topology.IsNumaSystem}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Total Processors: {report.Topology.ProcessorCount}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Cache Line Size: {report.Topology.CacheLineSize} bytes");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Page Size: {report.Topology.PageSize} bytes");

            foreach (var node in report.Topology.Nodes)
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Node {node.NodeId}:");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    Processors: {node.ProcessorCount}");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    Memory: {node.MemorySize / (1024 * 1024 * 1024.0):F2} GB");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    CPU List: {string.Join(",", node.CpuList.Take(8))}{(node.CpuList.Count() > 8 ? "..." : "")}");
            }
            _ = sb.AppendLine();
        }

        // Platform Capabilities
        if (report.Capabilities != null)
        {
            _ = sb.AppendLine("Platform Capabilities:");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Native NUMA Support: {report.Capabilities.HasNativeNumaSupport}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Memory Binding: {report.Capabilities.SupportsMemoryBinding}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Affinity Control: {report.Capabilities.SupportsAffinityControl}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Huge Pages: {report.Capabilities.SupportsHugePages}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Detection Method: {report.Capabilities.PreferredDetectionMethod}");
            _ = sb.AppendLine();
        }

        // Issues
        if (report.Issues.Count > 0)
        {
            _ = sb.AppendLine("Issues Found:");
            foreach (var issue in report.Issues.OrderByDescending(i => i.Severity))
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  [{issue.Severity}] {issue.Title}");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    Category: {issue.Category}");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    Description: {issue.Description}");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    Impact: {issue.Impact}");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"    Recommendation: {issue.Recommendation}");
                _ = sb.AppendLine();
            }
        }

        // Recommendations
        if (report.Recommendations.Count > 0)
        {
            _ = sb.AppendLine("Recommendations:");
            foreach (var recommendation in report.Recommendations)
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  • {recommendation}");
            }
            _ = sb.AppendLine();
        }

        // Warnings
        if (report.Warnings.Count > 0)
        {
            _ = sb.AppendLine("Warnings:");
            foreach (var warning in report.Warnings)
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  ⚠ {warning}");
            }
        }

        return sb.ToString();
    }

    private static void AnalyzeTopology(NumaTopology topology, List<DiagnosticIssue> issues, List<string> recommendations, List<string> warnings)
    {
        // Check for balanced topology
        if (topology.IsNumaSystem)
        {
            var loadBalance = topology.GetLoadBalanceInfo();
            if (!loadBalance.IsBalanced)
            {
                issues.Add(new DiagnosticIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = IssueCategory.Topology,
                    Title = "Unbalanced NUMA Topology",
                    Description = $"Processor distribution variance: {loadBalance.ProcessorDistributionVariance:F2}",
                    Impact = "May lead to suboptimal performance",
                    Recommendation = "Consider workload distribution strategies"
                });
            }

            // Check memory sizes
            var nodesWithoutMemory = topology.Nodes.Count(n => n.MemorySize == 0);
            if (nodesWithoutMemory > 0)
            {
                warnings.Add($"{nodesWithoutMemory} nodes have unknown memory sizes");
            }
        }
        else
        {
            recommendations.Add("System is not NUMA-enabled - NUMA optimizations will have limited effect");
        }
    }

    private static void AnalyzePlatformCapabilities(NumaPlatformCapabilities capabilities, List<DiagnosticIssue> issues, List<string> recommendations, List<string> warnings)
    {
        if (!capabilities.HasNativeNumaSupport)
        {
            issues.Add(new DiagnosticIssue
            {
                Severity = IssueSeverity.Info,
                Category = IssueCategory.Platform,
                Title = "No Native NUMA Support",
                Description = "Platform does not provide native NUMA APIs",
                Impact = "Limited NUMA optimization capabilities",
                Recommendation = "Use software-based NUMA awareness strategies"
            });
        }

        if (!capabilities.SupportsMemoryBinding)
        {
            warnings.Add("Memory binding not supported - memory allocations may not be optimal");
        }

        if (!capabilities.SupportsAffinityControl)
        {
            issues.Add(new DiagnosticIssue
            {
                Severity = IssueSeverity.Warning,
                Category = IssueCategory.Platform,
                Title = "No Affinity Control",
                Description = "Platform does not support thread affinity control",
                Impact = "Cannot bind threads to specific processors",
                Recommendation = "Consider alternative scheduling strategies"
            });
        }
    }

    private static void AnalyzePerformanceCharacteristics(NumaTopology topology, List<DiagnosticIssue> issues, List<string> recommendations)
    {
        if (topology.IsNumaSystem && topology.DistanceMatrix != null)
        {
            // Analyze distance matrix for performance implications
            var maxDistance = 0;
            var minDistance = int.MaxValue;

            for (var i = 0; i < topology.NodeCount; i++)
            {
                for (var j = 0; j < topology.NodeCount; j++)
                {
                    if (i != j)
                    {
                        var distance = topology.GetDistance(i, j);
                        maxDistance = Math.Max(maxDistance, distance);
                        minDistance = Math.Min(minDistance, distance);
                    }
                }
            }

            var distanceRatio = maxDistance > 0 ? (double)minDistance / maxDistance : 1.0;
            if (distanceRatio < 0.5)
            {
                issues.Add(new DiagnosticIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = IssueCategory.Performance,
                    Title = "High NUMA Distance Variance",
                    Description = $"Distance ratio: {distanceRatio:F2} (min: {minDistance}, max: {maxDistance})",
                    Impact = "Some node pairs have significantly higher latency",
                    Recommendation = "Optimize data placement to minimize cross-node access"
                });
            }
        }
    }

    private static void AnalyzeConfiguration(NumaTopology topology, NumaPlatformCapabilities capabilities, List<DiagnosticIssue> issues, List<string> recommendations, List<string> warnings)
    {
        // Check for optimal configuration
        if (topology.IsNumaSystem && capabilities.IsNumaCapable)
        {
            recommendations.Add("System is NUMA-capable - consider enabling NUMA optimizations");

            if (capabilities.SupportsHugePages)
            {
                recommendations.Add("Huge pages are supported - consider using them for large memory allocations");
            }
        }

        // Check cache line alignment
        if (topology.CacheLineSize != NumaSizes.CacheLineSize)
        {
            warnings.Add($"Non-standard cache line size detected: {topology.CacheLineSize} bytes");
        }
    }

    private static SystemHealth CalculateOverallHealth(IReadOnlyList<DiagnosticIssue> issues)
    {
        if (issues.Any(i => i.Severity == IssueSeverity.Critical))
        {

            return SystemHealth.Critical;
        }


        if (issues.Any(i => i.Severity == IssueSeverity.Error))
        {

            return SystemHealth.Poor;
        }


        if (issues.Any(i => i.Severity == IssueSeverity.Warning))
        {

            return SystemHealth.Fair;
        }


        return issues.Any(i => i.Severity == IssueSeverity.Info) ? SystemHealth.Good : SystemHealth.Excellent;
    }

    private static SystemInformation GatherSystemInfo()
    {
        var totalMemory = GC.GetTotalMemory(false);

        return new SystemInformation
        {
            Platform = Environment.OSVersion.Platform.ToString(),
            OsVersion = Environment.OSVersion.VersionString,
            TotalMemoryGB = totalMemory / (1024.0 * 1024.0 * 1024.0),
            ProcessorCount = Environment.ProcessorCount,
            ClrVersion = Environment.Version.ToString(),
            Is64Bit = Environment.Is64BitProcess
        };
    }

    private static void ValidateTopologyConsistency(NumaTopology topology, List<ValidationIssue> issues)
    {
        // Validate node count consistency
        if (topology.Nodes.Count != topology.NodeCount)
        {
            issues.Add(new ValidationIssue
            {
                Component = "Topology",
                Message = "Node count mismatch",
                Details = $"Reported {topology.NodeCount} nodes but found {topology.Nodes.Count}",
                Severity = ValidationSeverity.Error
            });
        }

        // Validate processor count consistency
        var totalProcessors = topology.Nodes.Sum(n => n.ProcessorCount);
        if (totalProcessors != topology.ProcessorCount)
        {
            issues.Add(new ValidationIssue
            {
                Component = "Topology",
                Message = "Processor count mismatch",
                Details = $"Sum of node processors ({totalProcessors}) doesn't match total ({topology.ProcessorCount})",
                Severity = ValidationSeverity.Warning
            });
        }
    }

    private static void ValidatePlatformSupport(NumaPlatformCapabilities capabilities, List<ValidationIssue> issues)
    {
        if (capabilities.MaxNodes <= 0)
        {
            issues.Add(new ValidationIssue
            {
                Component = "Platform",
                Message = "Invalid maximum node count",
                Details = $"MaxNodes is {capabilities.MaxNodes}",
                Severity = ValidationSeverity.Error
            });
        }
    }

    private static void ValidateMemoryConfiguration(NumaTopology topology, List<ValidationIssue> issues)
    {
        foreach (var node in topology.Nodes)
        {
            if (node.MemorySize < 0)
            {
                issues.Add(new ValidationIssue
                {
                    Component = "Memory",
                    Message = $"Invalid memory size for node {node.NodeId}",
                    Details = $"Memory size is {node.MemorySize}",
                    Severity = ValidationSeverity.Error
                });
            }
        }
    }

    private static void ValidateCpuConfiguration(NumaTopology topology, List<ValidationIssue> issues)
    {
        foreach (var node in topology.Nodes)
        {
            if (node.ProcessorCount <= 0)
            {
                issues.Add(new ValidationIssue
                {
                    Component = "CPU",
                    Message = $"Invalid processor count for node {node.NodeId}",
                    Details = $"Processor count is {node.ProcessorCount}",
                    Severity = ValidationSeverity.Error
                });
            }

            var maskCount = CpuUtilities.CountSetBits(node.ProcessorMask);
            if (maskCount != node.ProcessorCount)
            {
                issues.Add(new ValidationIssue
                {
                    Component = "CPU",
                    Message = $"Processor mask/count mismatch for node {node.NodeId}",
                    Details = $"Mask has {maskCount} bits but count is {node.ProcessorCount}",
                    Severity = ValidationSeverity.Warning
                });
            }
        }
    }

    private static BenchmarkMetric BenchmarkMemoryAllocation(NumaTopology topology, TimeSpan duration)
    {
        var endTime = DateTime.UtcNow + duration;
        var allocations = 0;
        var totalTime = TimeSpan.Zero;

        while (DateTime.UtcNow < endTime)
        {
            var start = DateTime.UtcNow;

            // Simple allocation test
            var memory = new byte[1024 * 1024]; // 1MB allocation
            memory[0] = 1; // Touch the memory

            var elapsed = DateTime.UtcNow - start;
            totalTime += elapsed;
            allocations++;
        }

        var avgLatency = totalTime.TotalMicroseconds / allocations;
        var throughput = allocations / duration.TotalSeconds;

        return new BenchmarkMetric
        {
            Name = "Memory Allocation",
            Score = Math.Min(100.0, 10000.0 / avgLatency), // Lower latency = higher score
            LatencyMicroseconds = avgLatency,
            ThroughputOpsPerSecond = throughput,
            Details = $"{allocations} allocations in {duration.TotalSeconds:F2}s"
        };
    }

    private static BenchmarkMetric BenchmarkCpuAffinity(NumaTopology topology, TimeSpan duration)
    {
        if (!topology.IsNumaSystem)
        {
            return new BenchmarkMetric
            {
                Name = "CPU Affinity",
                Score = 50.0,
                LatencyMicroseconds = 0,
                ThroughputOpsPerSecond = 0,
                Details = "Not applicable - not a NUMA system"
            };
        }

        var operations = 0;
        var totalTime = TimeSpan.Zero;
        var endTime = DateTime.UtcNow + duration;

        using var affinityManager = new NumaAffinityManager(topology);

        while (DateTime.UtcNow < endTime)
        {
            var start = DateTime.UtcNow;

            // Test affinity setting
            var nodeId = operations % topology.NodeCount;
            _ = affinityManager.SetThreadAffinity(0, nodeId);

            var elapsed = DateTime.UtcNow - start;
            totalTime += elapsed;
            operations++;
        }

        var avgLatency = totalTime.TotalMicroseconds / operations;
        var throughput = operations / duration.TotalSeconds;

        return new BenchmarkMetric
        {
            Name = "CPU Affinity",
            Score = Math.Min(100.0, 1000.0 / avgLatency),
            LatencyMicroseconds = avgLatency,
            ThroughputOpsPerSecond = throughput,
            Details = $"{operations} affinity operations in {duration.TotalSeconds:F2}s"
        };
    }

    private static BenchmarkMetric BenchmarkCrossNodeCommunication(NumaTopology topology, TimeSpan duration)
    {
        // Simplified cross-node communication benchmark
        var operations = 0;
        var endTime = DateTime.UtcNow + duration;

        while (DateTime.UtcNow < endTime)
        {
            // Simulate cross-node data access
            var data = new int[1000];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = i;
            }

            operations++;
        }

        var throughput = operations / duration.TotalSeconds;

        return new BenchmarkMetric
        {
            Name = "Cross-Node Communication",
            Score = Math.Min(100.0, throughput / 10.0),
            LatencyMicroseconds = 0,
            ThroughputOpsPerSecond = throughput,
            Details = $"{operations} operations in {duration.TotalSeconds:F2}s"
        };
    }
}
/// <summary>
/// A class that represents numa diagnostic report.
/// </summary>

// Supporting data structures for diagnostics
public sealed record NumaDiagnosticReport
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public required DateTime Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the topology.
    /// </summary>
    /// <value>The topology.</value>
    public required NumaTopology? Topology { get; init; }
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    /// <value>The capabilities.</value>
    public required NumaPlatformCapabilities? Capabilities { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public required IReadOnlyList<DiagnosticIssue> Issues { get; init; }
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public required IReadOnlyList<string> Recommendations { get; init; }
    /// <summary>
    /// Gets or sets the warnings.
    /// </summary>
    /// <value>The warnings.</value>
    public required IReadOnlyList<string> Warnings { get; init; }
    /// <summary>
    /// Gets or sets the overall health.
    /// </summary>
    /// <value>The overall health.</value>
    public required SystemHealth OverallHealth { get; init; }
    /// <summary>
    /// Gets or sets the analysis time.
    /// </summary>
    /// <value>The analysis time.</value>
    public required TimeSpan AnalysisTime { get; init; }
    /// <summary>
    /// Gets or sets the system info.
    /// </summary>
    /// <value>The system info.</value>
    public required SystemInformation? SystemInfo { get; init; }
}
/// <summary>
/// A class that represents diagnostic issue.
/// </summary>

public sealed record DiagnosticIssue
{
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public required IssueSeverity Severity { get; init; }
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    /// <value>The category.</value>
    public required IssueCategory Category { get; init; }
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    /// <value>The title.</value>
    public required string Title { get; init; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public required string Description { get; init; }
    /// <summary>
    /// Gets or sets the impact.
    /// </summary>
    /// <value>The impact.</value>
    public required string Impact { get; init; }
    /// <summary>
    /// Gets or sets the recommendation.
    /// </summary>
    /// <value>The recommendation.</value>
    public required string Recommendation { get; init; }
}
/// <summary>
/// A class that represents validation result.
/// </summary>

public sealed record ValidationResult
{
    /// <summary>
    /// Gets or sets the overall result.
    /// </summary>
    /// <value>The overall result.</value>
    public required ValidationSeverity OverallResult { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    /// <summary>
    /// Gets or sets the tested components.
    /// </summary>
    /// <value>The tested components.</value>
    public required IReadOnlyList<string> TestedComponents { get; init; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public required DateTime ValidationTime { get; init; }
}
/// <summary>
/// A class that represents validation issue.
/// </summary>

public sealed record ValidationIssue
{
    /// <summary>
    /// Gets or sets the component.
    /// </summary>
    /// <value>The component.</value>
    public required string Component { get; init; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public required ValidationSeverity Severity { get; init; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public required string Message { get; init; }
    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    /// <value>The details.</value>
    public required string Details { get; init; }
}
/// <summary>
/// A class that represents benchmark result.
/// </summary>

public sealed record BenchmarkResult
{
    /// <summary>
    /// Gets or sets the overall score.
    /// </summary>
    /// <value>The overall score.</value>
    public required double OverallScore { get; init; }
    /// <summary>
    /// Gets or sets the metrics.
    /// </summary>
    /// <value>The metrics.</value>
    public required Dictionary<string, BenchmarkMetric> Metrics { get; init; }
    /// <summary>
    /// Gets or sets the topology.
    /// </summary>
    /// <value>The topology.</value>
    public required NumaTopology Topology { get; init; }
    /// <summary>
    /// Gets or sets the benchmark time.
    /// </summary>
    /// <value>The benchmark time.</value>
    public required TimeSpan BenchmarkTime { get; init; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public required bool Success { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; init; }
}
/// <summary>
/// A class that represents benchmark metric.
/// </summary>

public sealed record BenchmarkMetric
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the score.
    /// </summary>
    /// <value>The score.</value>
    public required double Score { get; init; }
    /// <summary>
    /// Gets or sets the latency microseconds.
    /// </summary>
    /// <value>The latency microseconds.</value>
    public required double LatencyMicroseconds { get; init; }
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public required double ThroughputOpsPerSecond { get; init; }
    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    /// <value>The details.</value>
    public required string Details { get; init; }
}
/// <summary>
/// A class that represents system information.
/// </summary>

public sealed record SystemInformation
{
    /// <summary>
    /// Gets or sets the platform.
    /// </summary>
    /// <value>The platform.</value>
    public required string Platform { get; init; }
    /// <summary>
    /// Gets or sets the os version.
    /// </summary>
    /// <value>The os version.</value>
    public required string OsVersion { get; init; }
    /// <summary>
    /// Gets or sets the total memory g b.
    /// </summary>
    /// <value>The total memory g b.</value>
    public required double TotalMemoryGB { get; init; }
    /// <summary>
    /// Gets or sets the processor count.
    /// </summary>
    /// <value>The processor count.</value>
    public required int ProcessorCount { get; init; }
    /// <summary>
    /// Gets or sets the clr version.
    /// </summary>
    /// <value>The clr version.</value>
    public required string ClrVersion { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether 64 bit.
    /// </summary>
    /// <value>The is64 bit.</value>
    public required bool Is64Bit { get; init; }
}
/// <summary>
/// An issue severity enumeration.
/// </summary>

public enum IssueSeverity { Info, Warning, Error, Critical }
/// <summary>
/// An issue category enumeration.
/// </summary>
public enum IssueCategory { System, Topology, Platform, Performance, Configuration, Memory, CPU }
/// <summary>
/// An system health enumeration.
/// </summary>
public enum SystemHealth { Critical, Poor, Fair, Good, Excellent }