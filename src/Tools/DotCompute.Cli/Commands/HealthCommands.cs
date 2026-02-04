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

    private static async Task<List<HealthReport>> GetHealthReportsAsync(string? deviceId)
    {
        // Integration point: Replace with IAccelerator.GetHealthSnapshotAsync()
        // when DotCompute runtime is available

        var reports = new List<HealthReport>();

        // CPU health - use actual memory usage
        var gcInfo = GC.GetGCMemoryInfo();
        var memUsedPercent = gcInfo.TotalAvailableMemoryBytes > 0
            ? 100.0 * (gcInfo.TotalAvailableMemoryBytes - gcInfo.HighMemoryLoadThresholdBytes) / gcInfo.TotalAvailableMemoryBytes
            : 0;

        reports.Add(new HealthReport
        {
            DeviceId = "CPU0",
            DeviceType = "CPU",
            Status = memUsedPercent > 90 ? HealthStatus.Degraded : HealthStatus.Healthy,
            TemperatureCelsius = 0, // Not easily available cross-platform
            PowerWatts = 0, // Not easily available cross-platform
            UtilizationPercent = 0, // Would need platform-specific code
            MemoryUsedPercent = Math.Max(0, Math.Min(100, memUsedPercent)),
            IsThrottling = false,
            ThrottleReason = null,
            ErrorCount = 0,
            Timestamp = DateTime.UtcNow
        });

        // Check for NVIDIA GPU health using nvidia-smi
        var gpuHealth = await TryGetNvidiaHealthAsync();
        if (gpuHealth != null)
        {
            reports.Add(gpuHealth);
        }
        else if (Environment.GetEnvironmentVariable("CUDA_PATH") != null ||
                 Directory.Exists("/usr/local/cuda"))
        {
            // CUDA installed but nvidia-smi not available
            reports.Add(new HealthReport
            {
                DeviceId = "GPU0",
                DeviceType = "CUDA",
                Status = HealthStatus.Unknown,
                TemperatureCelsius = 0,
                PowerWatts = 0,
                UtilizationPercent = 0,
                MemoryUsedPercent = 0,
                IsThrottling = false,
                ThrottleReason = "nvidia-smi unavailable",
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

        return reports;
    }

    private static async Task<HealthReport?> TryGetNvidiaHealthAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=temperature.gpu,power.draw,utilization.gpu,memory.used,memory.total,clocks_throttle_reasons.active --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            var parts = output.Trim().Split(',');
            if (parts.Length >= 5)
            {
                var temp = double.TryParse(parts[0].Trim(), out var t) ? t : 0;
                var power = double.TryParse(parts[1].Trim(), out var p) ? p : 0;
                var util = double.TryParse(parts[2].Trim(), out var u) ? u : 0;
                var memUsed = long.TryParse(parts[3].Trim(), out var mu) ? mu : 0;
                var memTotal = long.TryParse(parts[4].Trim(), out var mt) ? mt : 1;
                var throttleReason = parts.Length > 5 ? parts[5].Trim() : null;

                var memUsedPct = 100.0 * memUsed / memTotal;
                var isThrottling = !string.IsNullOrEmpty(throttleReason) &&
                                   !throttleReason.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                                   !throttleReason.Equals("0x0", StringComparison.OrdinalIgnoreCase);

                // Determine health status
                var status = HealthStatus.Healthy;
                if (temp > 85 || memUsedPct > 95)
                    status = HealthStatus.Unhealthy;
                else if (temp > 75 || memUsedPct > 90 || isThrottling)
                    status = HealthStatus.Degraded;

                return new HealthReport
                {
                    DeviceId = "GPU0",
                    DeviceType = "CUDA",
                    Status = status,
                    TemperatureCelsius = temp,
                    PowerWatts = power,
                    UtilizationPercent = util,
                    MemoryUsedPercent = memUsedPct,
                    IsThrottling = isThrottling,
                    ThrottleReason = isThrottling ? throttleReason : null,
                    ErrorCount = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        catch { /* nvidia-smi not available or failed */ }

        return null;
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
