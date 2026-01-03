// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotCompute.Cli.Commands;

/// <summary>
/// Health monitoring commands for accelerator diagnostics.
/// </summary>
internal static class HealthCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Creates the health command group.
    /// </summary>
    public static Command CreateHealthCommand()
    {
        var healthCommand = new Command("health", "Monitor accelerator health")
        {
            CreateCheckCommand(),
            CreateWatchCommand()
        };

        return healthCommand;
    }

    private static Command CreateCheckCommand()
    {
        var deviceIdOption = new Option<string?>(
            aliases: ["--device", "-d"],
            description: "Device ID to check (default: all devices)");

        var jsonOption = new Option<bool>(
            aliases: ["--json", "-j"],
            description: "Output as JSON",
            getDefaultValue: () => false);

        var command = new Command("check", "Check accelerator health status")
        {
            deviceIdOption,
            jsonOption
        };

        command.SetHandler(async (string? deviceId, bool json) =>
        {
            await CheckHealthAsync(deviceId, json);
        }, deviceIdOption, jsonOption);

        return command;
    }

    private static Command CreateWatchCommand()
    {
        var deviceIdOption = new Option<string?>(
            aliases: ["--device", "-d"],
            description: "Device ID to watch (default: all devices)");

        var intervalOption = new Option<int>(
            aliases: ["--interval", "-i"],
            description: "Update interval in milliseconds",
            getDefaultValue: () => 1000);

        var command = new Command("watch", "Continuously monitor accelerator health")
        {
            deviceIdOption,
            intervalOption
        };

        command.SetHandler(async (string? deviceId, int interval) =>
        {
            await WatchHealthAsync(deviceId, interval);
        }, deviceIdOption, intervalOption);

        return command;
    }

    private static async Task CheckHealthAsync(string? deviceId, bool json)
    {
        try
        {
            var healthReports = await GetHealthReportsAsync(deviceId);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(healthReports, JsonOptions));
            }
            else
            {
                if (healthReports.Count == 0)
                {
                    Console.WriteLine("No devices to check.");
                    return;
                }

                foreach (var report in healthReports)
                {
                    PrintHealthReport(report);
                    Console.WriteLine();
                }

                // Summary
                var healthy = healthReports.Count(r => r.Status == HealthStatus.Healthy);
                var degraded = healthReports.Count(r => r.Status == HealthStatus.Degraded);
                var unhealthy = healthReports.Count(r => r.Status == HealthStatus.Unhealthy);

                Console.WriteLine($"Summary: {healthy} healthy, {degraded} degraded, {unhealthy} unhealthy");
            }

            // Set exit code based on health
            if (healthReports.Any(r => r.Status == HealthStatus.Unhealthy))
            {
                Environment.ExitCode = 2;
            }
            else if (healthReports.Any(r => r.Status == HealthStatus.Degraded))
            {
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error checking health: {ex.Message}");
            Environment.ExitCode = 3;
        }
    }

    private static async Task WatchHealthAsync(string? deviceId, int intervalMs)
    {
        Console.WriteLine("Monitoring accelerator health. Press Ctrl+C to stop.\n");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Console.Clear();
                Console.WriteLine($"Health Monitor - {DateTime.Now:HH:mm:ss}\n");

                var healthReports = await GetHealthReportsAsync(deviceId);

                foreach (var report in healthReports)
                {
                    PrintHealthReportCompact(report);
                }

                await Task.Delay(intervalMs, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nMonitoring stopped.");
        }
    }

    private static void PrintHealthReport(HealthReport report)
    {
        var statusColor = report.Status switch
        {
            HealthStatus.Healthy => ConsoleColor.Green,
            HealthStatus.Degraded => ConsoleColor.Yellow,
            HealthStatus.Unhealthy => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };

        Console.Write($"Device: {report.DeviceId} - ");
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = statusColor;
        Console.WriteLine(report.Status);
        Console.ForegroundColor = originalColor;

        Console.WriteLine($"  Type:              {report.DeviceType}");
        Console.WriteLine($"  Temperature:       {report.TemperatureCelsius:F1}°C");
        Console.WriteLine($"  Power Usage:       {report.PowerWatts:F1}W");
        Console.WriteLine($"  Utilization:       {report.UtilizationPercent:F1}%");
        Console.WriteLine($"  Memory Used:       {report.MemoryUsedPercent:F1}%");

        if (report.IsThrottling)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Throttling:      {report.ThrottleReason}");
            Console.ForegroundColor = originalColor;
        }

        if (report.ErrorCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ⚠ Errors:          {report.ErrorCount}");
            Console.ForegroundColor = originalColor;
        }

        Console.WriteLine($"  Last Check:        {report.Timestamp:HH:mm:ss}");
    }

    private static void PrintHealthReportCompact(HealthReport report)
    {
        var statusSymbol = report.Status switch
        {
            HealthStatus.Healthy => "✓",
            HealthStatus.Degraded => "⚠",
            HealthStatus.Unhealthy => "✗",
            _ => "?"
        };

        var statusColor = report.Status switch
        {
            HealthStatus.Healthy => ConsoleColor.Green,
            HealthStatus.Degraded => ConsoleColor.Yellow,
            HealthStatus.Unhealthy => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = statusColor;
        Console.Write($"[{statusSymbol}] ");
        Console.ForegroundColor = originalColor;

        Console.WriteLine($"{report.DeviceId,-10} Temp: {report.TemperatureCelsius,5:F1}°C  " +
            $"Power: {report.PowerWatts,5:F1}W  " +
            $"Util: {report.UtilizationPercent,5:F1}%  " +
            $"Mem: {report.MemoryUsedPercent,5:F1}%");
    }

    private static Task<List<HealthReport>> GetHealthReportsAsync(string? deviceId)
    {
        // TODO: Integrate with actual DotCompute IAccelerator.GetHealthSnapshotAsync()
        // For scaffold, return mock data demonstrating the structure

        var reports = new List<HealthReport>
        {
            new()
            {
                DeviceId = "CPU0",
                DeviceType = "CPU",
                Status = HealthStatus.Healthy,
                TemperatureCelsius = 45.0,
                PowerWatts = 65.0,
                UtilizationPercent = 25.0,
                MemoryUsedPercent = 42.0,
                IsThrottling = false,
                ThrottleReason = null,
                ErrorCount = 0,
                Timestamp = DateTime.UtcNow
            }
        };

        // Add GPU if likely present
        if (Environment.GetEnvironmentVariable("CUDA_PATH") != null ||
            Directory.Exists("/usr/local/cuda"))
        {
            reports.Add(new HealthReport
            {
                DeviceId = "GPU0",
                DeviceType = "CUDA",
                Status = HealthStatus.Healthy,
                TemperatureCelsius = 55.0,
                PowerWatts = 120.0,
                UtilizationPercent = 15.0,
                MemoryUsedPercent = 30.0,
                IsThrottling = false,
                ThrottleReason = null,
                ErrorCount = 0,
                Timestamp = DateTime.UtcNow
            });
        }

        // Filter by device ID if specified
        if (!string.IsNullOrEmpty(deviceId))
        {
            reports = reports
                .Where(r => r.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Task.FromResult(reports);
    }
}

/// <summary>
/// Health status enumeration.
/// </summary>
internal enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Health report for a compute device.
/// </summary>
internal sealed class HealthReport
{
    public required string DeviceId { get; set; }
    public required string DeviceType { get; set; }
    public HealthStatus Status { get; set; }
    public double TemperatureCelsius { get; set; }
    public double PowerWatts { get; set; }
    public double UtilizationPercent { get; set; }
    public double MemoryUsedPercent { get; set; }
    public bool IsThrottling { get; set; }
    public string? ThrottleReason { get; set; }
    public int ErrorCount { get; set; }
    public DateTime Timestamp { get; set; }
}
