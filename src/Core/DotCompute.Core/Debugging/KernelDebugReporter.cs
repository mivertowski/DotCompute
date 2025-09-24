// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Handles report generation and metrics for kernel debugging operations.
/// Provides comprehensive reporting capabilities and result comparison.
/// </summary>
internal sealed class KernelDebugReporter : IDisposable
{
    private readonly ILogger<KernelDebugReporter> _logger;
    private DebugServiceOptions _options;
    private bool _disposed;

    public KernelDebugReporter(ILogger<KernelDebugReporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = new DebugServiceOptions();
    }

    public void Configure(DebugServiceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ResultComparisonReport> CompareResultsAsync(
        KernelExecutionResult result1,
        KernelExecutionResult result2,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(result1);
        ArgumentNullException.ThrowIfNull(result2);

        _logger.LogInformation("Comparing results between {backend1} and {backend2}",
            result1.BackendType, result2.BackendType);

        try
        {
            var report = new ResultComparisonReport
            {
                Result1 = result1,
                Result2 = result2,
                Tolerance = tolerance,
                ComparisonTime = DateTimeOffset.UtcNow
            };

            // Compare execution success
            if (result1.Success != result2.Success)
            {
                report.Issues.Add(new ComparisonIssue
                {
                    Severity = ComparisonSeverity.Error,
                    Category = "Execution Status",
                    Description = "One execution succeeded while the other failed",
                    Details = $"{result1.BackendType}: {(result1.Success ? "Success" : "Failed")} | " +
                             $"{result2.BackendType}: {(result2.Success ? "Success" : "Failed")}"
                });
            }

            // Compare execution times
            var timeDifference = Math.Abs((result1.ExecutionTime - result2.ExecutionTime).TotalMilliseconds);
            var averageTime = (result1.ExecutionTime.TotalMilliseconds + result2.ExecutionTime.TotalMilliseconds) / 2;

            if (averageTime > 0 && timeDifference / averageTime > 0.5) // >50% difference
            {
                report.Issues.Add(new ComparisonIssue
                {
                    Severity = ComparisonSeverity.Warning,
                    Category = "Performance",
                    Description = "Significant execution time difference detected",
                    Details = $"{result1.BackendType}: {result1.ExecutionTime.TotalMilliseconds:F2}ms | " +
                             $"{result2.BackendType}: {result2.ExecutionTime.TotalMilliseconds:F2}ms"
                });
            }

            // Compare memory usage
            if (result1.MemoryUsed > 0 && result2.MemoryUsed > 0)
            {
                var memoryDifference = Math.Abs(result1.MemoryUsed - result2.MemoryUsed);
                var averageMemory = (result1.MemoryUsed + result2.MemoryUsed) / 2.0;

                if (memoryDifference / averageMemory > 0.3) // >30% difference
                {
                    report.Issues.Add(new ComparisonIssue
                    {
                        Severity = ComparisonSeverity.Warning,
                        Category = "Memory Usage",
                        Description = "Significant memory usage difference detected",
                        Details = $"{result1.BackendType}: {result1.MemoryUsed:N0} bytes | " +
                                 $"{result2.BackendType}: {result2.MemoryUsed:N0} bytes"
                    });
                }
            }

            // Compare actual results
            if (result1.Success && result2.Success)
            {
                var resultComparison = CompareResultValues(result1.Result, result2.Result, tolerance);
                if (!resultComparison.IsMatch)
                {
                    report.Issues.Add(new ComparisonIssue
                    {
                        Severity = ComparisonSeverity.Error,
                        Category = "Result Values",
                        Description = "Result values differ between backends",
                        Details = $"Difference: {resultComparison.Difference:F6}, Tolerance: {tolerance:F6}"
                    });
                }

                // ResultsMatch is set in the report object creation
                // ResultDifference is a computed property from Differences list
            }
            else
            {
                // ResultsMatch is set in the report object creation
                // ResultDifference is a computed property from Differences list
            }

            // Return the report
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during result comparison");

            return new ResultComparisonReport
            {
                Result1 = result1,
                Result2 = result2,
                Tolerance = tolerance,
                ComparisonTime = DateTimeOffset.UtcNow,
                ResultsMatch = false,
                Issues = [new ComparisonIssue
                {
                    Severity = ComparisonSeverity.Error,
                    Category = "Comparison Error",
                    Description = "Failed to compare results",
                    Details = ex.Message
                }],
                Differences = []
            };
        }
    }

    public async Task<string> GenerateDetailedReportAsync(KernelValidationResult validationResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(validationResult);

        var report = new StringBuilder();

        // Header
        report.AppendLine("=== Kernel Debug Report ===");
        report.AppendLine($"Kernel: {validationResult.KernelName}");
        report.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        report.AppendLine($"Validation Status: {(validationResult.IsValid ? "PASSED" : "FAILED")}");
        report.AppendLine();

        // Executive Summary
        report.AppendLine("--- Executive Summary ---");
        if (validationResult.IsValid)
        {
            report.AppendLine("✓ All cross-backend validations passed successfully");
            report.AppendLine($"✓ Tested on {validationResult.BackendsTested.Length} backend(s): {string.Join(", ", validationResult.BackendsTested)}");
        }
        else
        {
            var errorCount = validationResult.Issues.Count(i => i.Severity == ValidationSeverity.Error);
            var warningCount = validationResult.Issues.Count(i => i.Severity == ValidationSeverity.Warning);
            report.AppendLine($"✗ Validation failed with {errorCount} error(s) and {warningCount} warning(s)");
        }
        report.AppendLine($"Execution Time: {validationResult.ExecutionTime.TotalMilliseconds:F2}ms");
        report.AppendLine();

        // Backend Results
        if (validationResult.Results?.Any() == true)
        {
            report.AppendLine("--- Backend Execution Results ---");
            foreach (var kvp in validationResult.Results)
            {
                if (kvp.Value is KernelExecutionResult result)
                {
                    report.AppendLine($"Backend: {result.BackendType}");
                    report.AppendLine($"  Status: {(result.Success ? "Success" : "Failed")}");
                    report.AppendLine($"  Execution Time: {result.ExecutionTime.TotalMilliseconds:F2}ms");
                    if (result.MemoryUsed > 0)
                    {
                        report.AppendLine($"  Memory Usage: {result.MemoryUsed:N0} bytes");
                    }
                    if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        report.AppendLine($"  Error: {result.ErrorMessage}");
                    }
                    report.AppendLine();
                }
            }
        }

        // Cross-Backend Comparisons
        if (validationResult.CrossBackendComparisons?.Any() == true)
        {
            report.AppendLine("--- Cross-Backend Comparisons ---");
            foreach (var comparison in validationResult.CrossBackendComparisons)
            {
                var status = comparison.IsMatch ? "✓ MATCH" : "✗ DIFFER";
                report.AppendLine($"{comparison.Backend1} vs {comparison.Backend2}: {status}");
                if (!comparison.IsMatch)
                {
                    report.AppendLine($"  Difference: {comparison.Difference:F6}");
                }
            }
            report.AppendLine();
        }

        // Issues and Recommendations
        if (validationResult.Issues.Any())
        {
            report.AppendLine("--- Issues Found ---");
            var groupedIssues = validationResult.Issues.GroupBy(i => i.Severity);
            foreach (var group in groupedIssues.OrderBy(g => g.Key))
            {
                report.AppendLine($"{group.Key}s:");
                foreach (var issue in group)
                {
                    report.AppendLine($"  • {issue.Message}");
                    if (!string.IsNullOrEmpty(issue.Details))
                    {
                        report.AppendLine($"    Details: {issue.Context ?? issue.Details}");
                    }
                }
                report.AppendLine();
            }
        }

        return report.ToString();
    }

    public async Task<string> ExportReportAsync(object report, ReportFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            return format switch
            {
                ReportFormat.Json => JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
                ReportFormat.Xml => await ExportToXmlAsync(report),
                ReportFormat.Csv => await ExportToCsvAsync(report),
                ReportFormat.PlainText => await ExportToTextAsync(report),
                _ => throw new ArgumentException($"Unsupported report format: {format}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report in format {format}", format);
            throw;
        }
    }

    public static async Task<IEnumerable<BackendInfo>> GetAvailableBackendsAsync()
    {
        await Task.CompletedTask;

        // This would typically query the actual system for available backends
        // For now, return a static list of common backends
        return new List<BackendInfo>
        {
            new() { Name = "CPU", IsAvailable = true, Version = "1.0", Capabilities = ["SIMD", "MultiThreading"] },
            new() { Name = "CUDA", IsAvailable = false, Version = "Unknown", Capabilities = [] } // Would check actual CUDA availability
        };
    }

    private static (bool IsMatch, float Difference) CompareResultValues(object? result1, object? result2, float tolerance)
    {
        if (result1 == null && result2 == null)
        {
            return (true, 0f);
        }


        if (result1 == null || result2 == null)
        {
            return (false, 1f);
        }


        if (result1.GetType() != result2.GetType())
        {

            return (false, 1.0f);
        }

        // Numeric comparison with tolerance

        if (result1 is float f1 && result2 is float f2)
        {
            var diff = Math.Abs(f1 - f2);
            return (diff <= tolerance, diff);
        }

        if (result1 is double d1 && result2 is double d2)
        {
            var diff = Math.Abs(d1 - d2);
            return (diff <= tolerance, (float)diff);
        }

        // Array comparison
        if (result1 is Array arr1 && result2 is Array arr2)
        {
            if (arr1.Length != arr2.Length)
            {

                return (false, 1.0f);
            }


            var totalDifference = 0f;
            var elementCount = 0;

            for (var i = 0; i < arr1.Length; i++)
            {
                var (isMatch, diff) = CompareResultValues(arr1.GetValue(i), arr2.GetValue(i), tolerance);
                totalDifference += diff;
                elementCount++;

                if (!isMatch && diff > tolerance)
                {

                    return (false, diff);
                }
            }

            var avgDifference = elementCount > 0 ? totalDifference / elementCount : 0f;
            return (avgDifference <= tolerance, avgDifference);
        }

        // Default comparison
        return (result1.Equals(result2), result1.Equals(result2) ? 0f : 0.5f);
    }

    private static string GenerateComparisonSummary(ResultComparisonReport report)
    {
        var summary = new StringBuilder();

        if (report.ResultsMatch)
        {
            summary.Append("Results match");
        }
        else
        {
            summary.Append("Results differ");
            if (report.ResultDifference < float.MaxValue)
            {
                summary.Append($" (difference: {report.ResultDifference:F6})");
            }
        }

        var errorCount = report.Issues.Count(i => i.Severity == ComparisonSeverity.Error);
        var warningCount = report.Issues.Count(i => i.Severity == ComparisonSeverity.Warning);

        if (errorCount > 0 || warningCount > 0)
        {
            summary.Append($" - {errorCount} error(s), {warningCount} warning(s)");
        }

        return summary.ToString();
    }

    private static async Task<string> ExportToXmlAsync(object report)
    {
        await Task.CompletedTask;

        // Simplified XML export - in practice, you'd use a proper XML serializer
        return $"<Report>{JsonSerializer.Serialize(report)}</Report>";
    }

    private static async Task<string> ExportToCsvAsync(object report)
    {
        await Task.CompletedTask;

        // Simplified CSV export - in practice, you'd extract specific properties
        if (report is KernelValidationResult validation)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Kernel,Backend,Success,ExecutionTimeMs,MemoryUsage");

            if (validation.Results != null)
            {
                foreach (var result in validation.Results)
                {
                    csv.AppendLine($"{result.KernelName},{result.BackendType},{result.Success}," +
                                 $"{result.ExecutionTime.TotalMilliseconds},{result.MemoryUsage}");
                }
            }

            return csv.ToString();
        }

        return "CSV export not supported for this report type";
    }

    private async Task<string> ExportToTextAsync(object report)
    {
        if (report is KernelValidationResult validation)
        {
            return await GenerateDetailedReportAsync(validation);
        }

        return report.ToString() ?? "No data available";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

// Supporting enums and classes
// ReportFormat moved to DotCompute.Abstractions.Debugging.ReportingTypes

// ComparisonSeverity and ComparisonIssue types have been moved to DotCompute.Abstractions.Debugging
