// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.CommandLine;
using DotCompute.Cli.Commands;

namespace DotCompute.Cli;

/// <summary>
/// DotCompute CLI tool - Compute accelerator management and diagnostics.
/// </summary>
/// <remarks>
/// Provides commands for:
/// - Device discovery and information
/// - Health monitoring and diagnostics
/// - Performance benchmarking
/// - Kernel compilation and management
/// </remarks>
internal static class Program
{
    /// <summary>
    /// CLI entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for errors.</returns>
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DotCompute - Universal Compute Accelerator Toolkit")
        {
            Description = "Manage and diagnose GPU and CPU compute accelerators"
        };

        // Add command groups
        rootCommand.AddCommand(DeviceCommands.CreateDeviceCommand());
        rootCommand.AddCommand(HealthCommands.CreateHealthCommand());

        // Global options
        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Enable verbose output",
            getDefaultValue: () => false);

        var jsonOption = new Option<bool>(
            aliases: ["--json", "-j"],
            description: "Output as JSON",
            getDefaultValue: () => false);

        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(jsonOption);

        return await rootCommand.InvokeAsync(args);
    }
}
