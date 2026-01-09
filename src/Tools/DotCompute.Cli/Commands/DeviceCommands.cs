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

    private static Task<List<DeviceSummary>> DiscoverDevicesAsync()
    {
        // TODO: Integrate with actual DotCompute backend discovery
        // For scaffold, return mock data demonstrating the structure

        var devices = new List<DeviceSummary>
        {
            new()
            {
                Id = "CPU0",
                Type = "CPU",
                Name = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "System CPU",
                MemoryBytes = 0, // CPU uses system memory
                Status = "Available"
            }
        };

        // Check for CUDA devices (mock for scaffold)
        if (Environment.GetEnvironmentVariable("CUDA_PATH") != null ||
            Directory.Exists("/usr/local/cuda"))
        {
            devices.Add(new DeviceSummary
            {
                Id = "GPU0",
                Type = "CUDA",
                Name = "NVIDIA GPU (detection pending)",
                MemoryBytes = 0,
                Status = "Detection Required"
            });
        }

        return Task.FromResult(devices);
    }

    private static Task<DeviceInfo> GetDetailedDeviceInfoAsync(DeviceSummary summary)
    {
        // TODO: Integrate with actual DotCompute IAccelerator.Info
        return Task.FromResult(new DeviceInfo
        {
            Id = summary.Id,
            Type = summary.Type,
            Name = summary.Name,
            Vendor = summary.Type == "CPU" ? "System" : "NVIDIA",
            Status = summary.Status,
            ComputeUnits = Environment.ProcessorCount,
            ClockSpeedMHz = 0,
            MaxWorkgroupSize = 1024,
            MemoryBytes = summary.MemoryBytes,
            AvailableMemoryBytes = summary.MemoryBytes,
            MemoryType = summary.Type == "CPU" ? "System RAM" : "GDDR6",
            ComputeCapability = summary.Type == "CUDA" ? "8.9" : null,
            DriverVersion = null
        });
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
