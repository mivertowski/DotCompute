// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotCompute.Cli.Commands;

/// <summary>
/// Device management commands for listing and querying accelerators.
/// </summary>
internal static class DeviceCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Creates the device command group.
    /// </summary>
    public static Command CreateDeviceCommand()
    {
        var deviceCommand = new Command("device", "Manage compute accelerators")
        {
            CreateListCommand(),
            CreateInfoCommand()
        };

        return deviceCommand;
    }

    private static Command CreateListCommand()
    {
        var jsonOption = new Option<bool>(
            aliases: ["--json", "-j"],
            description: "Output as JSON",
            getDefaultValue: () => false);

        var typeOption = new Option<string?>(
            aliases: ["--type", "-t"],
            description: "Filter by accelerator type (CPU, CUDA, OpenCL, Metal)");

        var command = new Command("list", "List available accelerators")
        {
            jsonOption,
            typeOption
        };

        command.SetHandler(async (bool json, string? type) =>
        {
            await ListDevicesAsync(json, type);
        }, jsonOption, typeOption);

        return command;
    }

    private static Command CreateInfoCommand()
    {
        var deviceIdOption = new Option<string?>(
            aliases: ["--device", "-d"],
            description: "Device ID to query (default: first available)");

        var jsonOption = new Option<bool>(
            aliases: ["--json", "-j"],
            description: "Output as JSON",
            getDefaultValue: () => false);

        var command = new Command("info", "Show detailed device information")
        {
            deviceIdOption,
            jsonOption
        };

        command.SetHandler(async (string? deviceId, bool json) =>
        {
            await ShowDeviceInfoAsync(deviceId, json);
        }, deviceIdOption, jsonOption);

        return command;
    }

    private static async Task ListDevicesAsync(bool json, string? typeFilter)
    {
        try
        {
            // Discover available accelerators
            var devices = await DiscoverDevicesAsync();

            // Filter by type if specified
            if (!string.IsNullOrEmpty(typeFilter))
            {
                devices = devices
                    .Where(d => d.Type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(devices, JsonOptions));
            }
            else
            {
                if (devices.Count == 0)
                {
                    Console.WriteLine("No accelerators found.");
                    return;
                }

                Console.WriteLine($"Found {devices.Count} accelerator(s):\n");
                Console.WriteLine($"{"ID",-15} {"Type",-10} {"Name",-35} {"Memory",-12} {"Status"}");
                Console.WriteLine(new string('-', 85));

                foreach (var device in devices)
                {
                    var memoryStr = device.MemoryBytes > 0
                        ? $"{device.MemoryBytes / (1024 * 1024 * 1024.0):F1} GB"
                        : "N/A";

                    Console.WriteLine($"{device.Id,-15} {device.Type,-10} {device.Name,-35} {memoryStr,-12} {device.Status}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error listing devices: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task ShowDeviceInfoAsync(string? deviceId, bool json)
    {
        try
        {
            var devices = await DiscoverDevicesAsync();

            var device = string.IsNullOrEmpty(deviceId)
                ? devices.FirstOrDefault()
                : devices.FirstOrDefault(d => d.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase));

            if (device == null)
            {
                Console.Error.WriteLine(string.IsNullOrEmpty(deviceId)
                    ? "No accelerators found."
                    : $"Device '{deviceId}' not found.");
                Environment.ExitCode = 1;
                return;
            }

            var info = await GetDetailedDeviceInfoAsync(device);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(info, JsonOptions));
            }
            else
            {
                Console.WriteLine($"Device: {info.Name}");
                Console.WriteLine($"{"=",-50}");
                Console.WriteLine($"  ID:              {info.Id}");
                Console.WriteLine($"  Type:            {info.Type}");
                Console.WriteLine($"  Vendor:          {info.Vendor}");
                Console.WriteLine($"  Status:          {info.Status}");
                Console.WriteLine();
                Console.WriteLine("Compute Capabilities:");
                Console.WriteLine($"  Compute Units:   {info.ComputeUnits}");
                Console.WriteLine($"  Clock Speed:     {info.ClockSpeedMHz} MHz");
                Console.WriteLine($"  Max Workgroup:   {info.MaxWorkgroupSize}");
                Console.WriteLine();
                Console.WriteLine("Memory:");
                Console.WriteLine($"  Total:           {info.MemoryBytes / (1024.0 * 1024 * 1024):F2} GB");
                Console.WriteLine($"  Available:       {info.AvailableMemoryBytes / (1024.0 * 1024 * 1024):F2} GB");
                Console.WriteLine($"  Type:            {info.MemoryType}");
                Console.WriteLine();

                if (!string.IsNullOrEmpty(info.ComputeCapability))
                {
                    Console.WriteLine("CUDA Info:");
                    Console.WriteLine($"  Compute Capability: {info.ComputeCapability}");
                }

                if (info.DriverVersion != null)
                {
                    Console.WriteLine($"  Driver Version:  {info.DriverVersion}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error getting device info: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task<List<DeviceSummary>> DiscoverDevicesAsync()
    {
        // Integration point: Replace with IAcceleratorDiscovery.DiscoverAsync()
        // when DotCompute runtime is available

        var devices = new List<DeviceSummary>();

        // CPU device - always available with real system info
        var gcInfo = GC.GetGCMemoryInfo();
        devices.Add(new DeviceSummary
        {
            Id = "CPU0",
            Type = "CPU",
            Name = GetCpuName(),
            MemoryBytes = (long)gcInfo.TotalAvailableMemoryBytes,
            Status = "Available"
        });

        // Check for CUDA devices using nvidia-smi if available
        var cudaDevice = await TryGetNvidiaGpuInfoAsync();
        if (cudaDevice != null)
        {
            devices.Add(cudaDevice);
        }
        else if (Environment.GetEnvironmentVariable("CUDA_PATH") != null ||
                 Directory.Exists("/usr/local/cuda"))
        {
            // CUDA installed but nvidia-smi not available
            devices.Add(new DeviceSummary
            {
                Id = "GPU0",
                Type = "CUDA",
                Name = "NVIDIA GPU (nvidia-smi unavailable)",
                MemoryBytes = 0,
                Status = "Detection Limited"
            });
        }

        return devices;
    }

    private static string GetCpuName()
    {
        // Try to get CPU name from environment or /proc/cpuinfo
        var procId = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        if (!string.IsNullOrEmpty(procId))
            return procId;

        // Linux: try /proc/cpuinfo
        if (File.Exists("/proc/cpuinfo"))
        {
            try
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                if (modelLine != null)
                {
                    var parts = modelLine.Split(':');
                    if (parts.Length > 1)
                        return parts[1].Trim();
                }
            }
            catch { /* Fall through to default */ }
        }

        return $"System CPU ({Environment.ProcessorCount} cores)";
    }

    private static async Task<DeviceSummary?> TryGetNvidiaGpuInfoAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,memory.total --format=csv,noheader,nounits",
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
            if (parts.Length >= 2)
            {
                var name = parts[0].Trim();
                var memoryMb = long.TryParse(parts[1].Trim(), out var mb) ? mb : 0;

                return new DeviceSummary
                {
                    Id = "GPU0",
                    Type = "CUDA",
                    Name = name,
                    MemoryBytes = memoryMb * 1024 * 1024,
                    Status = "Available"
                };
            }
        }
        catch { /* nvidia-smi not available or failed */ }

        return null;
    }

    private static async Task<DeviceInfo> GetDetailedDeviceInfoAsync(DeviceSummary summary)
    {
        // Integration point: Replace with IAccelerator.Info when DotCompute runtime is available

        if (summary.Type == "CUDA")
        {
            var gpuInfo = await TryGetDetailedNvidiaInfoAsync();
            if (gpuInfo != null)
                return gpuInfo with { Id = summary.Id, Status = summary.Status };
        }

        // CPU or fallback info
        var gcInfo = GC.GetGCMemoryInfo();
        return new DeviceInfo
        {
            Id = summary.Id,
            Type = summary.Type,
            Name = summary.Name,
            Vendor = summary.Type == "CPU" ? GetCpuVendor() : "NVIDIA",
            Status = summary.Status,
            ComputeUnits = Environment.ProcessorCount,
            ClockSpeedMHz = 0, // Not easily available cross-platform
            MaxWorkgroupSize = summary.Type == "CPU" ? Environment.ProcessorCount * 2 : 1024,
            MemoryBytes = summary.MemoryBytes > 0 ? summary.MemoryBytes : (long)gcInfo.TotalAvailableMemoryBytes,
            AvailableMemoryBytes = summary.MemoryBytes > 0 ? summary.MemoryBytes : (long)gcInfo.TotalAvailableMemoryBytes,
            MemoryType = summary.Type == "CPU" ? "System RAM" : "GPU Memory",
            ComputeCapability = null,
            DriverVersion = null
        };
    }

    private static string GetCpuVendor()
    {
        // Try to extract vendor from processor identifier
        var procId = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "";
        if (procId.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            return "Intel";
        if (procId.Contains("AMD", StringComparison.OrdinalIgnoreCase))
            return "AMD";
        if (procId.Contains("ARM", StringComparison.OrdinalIgnoreCase))
            return "ARM";

        // Linux: check /proc/cpuinfo for vendor_id
        if (File.Exists("/proc/cpuinfo"))
        {
            try
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                var vendorLine = lines.FirstOrDefault(l => l.StartsWith("vendor_id", StringComparison.OrdinalIgnoreCase));
                if (vendorLine != null)
                {
                    var parts = vendorLine.Split(':');
                    if (parts.Length > 1)
                    {
                        var vendor = parts[1].Trim();
                        if (vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                            return "Intel";
                        if (vendor.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                            vendor.Contains("AuthenticAMD", StringComparison.OrdinalIgnoreCase))
                            return "AMD";
                        return vendor;
                    }
                }
            }
            catch { /* Fall through */ }
        }

        return "Unknown";
    }

    private static async Task<DeviceInfo?> TryGetDetailedNvidiaInfoAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,memory.total,memory.free,compute_cap,driver_version,clocks.sm --format=csv,noheader,nounits",
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
                var name = parts[0].Trim();
                var totalMemMb = long.TryParse(parts[1].Trim(), out var tmb) ? tmb : 0;
                var freeMemMb = long.TryParse(parts[2].Trim(), out var fmb) ? fmb : 0;
                var computeCap = parts[3].Trim();
                var driverVer = parts[4].Trim();
                var clockMhz = parts.Length > 5 && int.TryParse(parts[5].Trim(), out var clk) ? clk : 0;

                return new DeviceInfo
                {
                    Id = "GPU0",
                    Type = "CUDA",
                    Name = name,
                    Vendor = "NVIDIA",
                    Status = "Available",
                    ComputeUnits = 0, // Would need separate query
                    ClockSpeedMHz = clockMhz,
                    MaxWorkgroupSize = 1024,
                    MemoryBytes = totalMemMb * 1024 * 1024,
                    AvailableMemoryBytes = freeMemMb * 1024 * 1024,
                    MemoryType = "GDDR/HBM",
                    ComputeCapability = computeCap,
                    DriverVersion = driverVer
                };
            }
        }
        catch { /* nvidia-smi not available or failed */ }

        return null;
    }
}

/// <summary>
/// Summary information for a compute device.
/// </summary>
internal sealed class DeviceSummary
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public long MemoryBytes { get; set; }
    public required string Status { get; set; }
}

/// <summary>
/// Detailed information for a compute device.
/// </summary>
internal sealed class DeviceInfo
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public required string Vendor { get; set; }
    public required string Status { get; set; }
    public int ComputeUnits { get; set; }
    public int ClockSpeedMHz { get; set; }
    public int MaxWorkgroupSize { get; set; }
    public long MemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public required string MemoryType { get; set; }
    public string? ComputeCapability { get; set; }
    public string? DriverVersion { get; set; }
}
