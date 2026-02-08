// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DotCompute.Generators.MSBuild;

/// <summary>
/// MSBuild task that generates CUDA serialization code for message types at build time.
/// </summary>
/// <remarks>
/// <para>
/// This task runs before compilation to generate .cu files for all message types
/// implementing IRingKernelMessage with [MemoryPackable] attribute.
/// </para>
/// <para>
/// <b>Version 0.5.1+:</b> This task now invokes an external CLI tool to avoid
/// Microsoft.CodeAnalysis version conflicts with consuming projects. The CLI
/// tool runs in a separate process with its own bundled dependencies.
/// </para>
/// <para>
/// <b>MSBuild Integration:</b>
/// - Runs during BeforeCompile target
/// - Invokes DotCompute.Generators.Cli for code generation
/// - Writes .cu files to $(IntermediateOutputPath)/Generated/CUDA
/// - Includes generated files in CUDA compilation inputs
/// </para>
/// </remarks>
public sealed class GenerateCudaSerializationTask : Task
{
    /// <summary>
    /// Gets or sets the C# source files to analyze for message types.
    /// </summary>
    [Required]
    public ITaskItem[]? SourceFiles { get; set; }

    /// <summary>
    /// Gets or sets the output directory for generated CUDA files.
    /// </summary>
    [Required]
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets reference assemblies needed for compilation.
    /// </summary>
    public ITaskItem[]? References { get; set; }

    /// <summary>
    /// Gets or sets the path to the DotCompute.Generators.Cli tool.
    /// This is set by the MSBuild targets file based on the platform.
    /// </summary>
    [Required]
    public string? CliToolPath { get; set; }

    /// <summary>
    /// Gets or sets whether to enable verbose logging.
    /// </summary>
    public bool VerboseLogging { get; set; }

    /// <summary>
    /// Gets the generated CUDA files (output parameter).
    /// </summary>
    [Output]
    public ITaskItem[]? GeneratedFiles { get; private set; }

    /// <summary>
    /// Executes the task to generate CUDA serialization code.
    /// </summary>
    /// <returns>True if successful; otherwise, false.</returns>
    public override bool Execute()
    {
        try
        {
            if (SourceFiles == null || SourceFiles.Length == 0)
            {
                Log.LogMessage(MessageImportance.Low, "No source files provided for CUDA code generation.");
                GeneratedFiles = Array.Empty<ITaskItem>();
                return true;
            }

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                Log.LogError("OutputDirectory parameter is required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(CliToolPath))
            {
                Log.LogError("CliToolPath parameter is required. Ensure DotCompute.Generators is properly installed.");
                return false;
            }

            if (!File.Exists(CliToolPath))
            {
                Log.LogError($"CLI tool not found at: {CliToolPath}");
                return false;
            }

            Log.LogMessage(MessageImportance.High, $"Generating CUDA serialization code for {SourceFiles.Length} source files...");

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(OutputDirectory);

            // Build command arguments
            var arguments = BuildCliArguments();

            // Execute CLI tool
            var result = ExecuteCliTool(arguments);

            if (result == null)
            {
                Log.LogError("Failed to execute CLI tool - no output received.");
                return false;
            }

            if (!result.Success)
            {
                Log.LogError($"CUDA code generation failed: {result.Message}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Log.LogError(result.Error);
                }
                return false;
            }

            // Convert file paths to TaskItems
            var generatedFiles = new List<ITaskItem>();
            foreach (var filePath in result.Files)
            {
                generatedFiles.Add(new TaskItem(filePath));
            }

            GeneratedFiles = generatedFiles.ToArray();
            Log.LogMessage(MessageImportance.High, $"Successfully generated {GeneratedFiles.Length} CUDA serialization files.");

            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private string BuildCliArguments()
    {
        var args = new List<string>
        {
            "generate-cuda-serialization",
            "--output",
            $"\"{OutputDirectory}\""
        };

        // Add source files
        args.Add("--source-files");
        foreach (var sourceFile in SourceFiles!)
        {
            args.Add($"\"{sourceFile.ItemSpec}\"");
        }

        // Add references
        if (References != null && References.Length > 0)
        {
            args.Add("--references");
            foreach (var reference in References)
            {
                if (File.Exists(reference.ItemSpec))
                {
                    args.Add($"\"{reference.ItemSpec}\"");
                }
            }
        }

        // Add verbose flag
        if (VerboseLogging)
        {
            args.Add("--verbose");
        }

        return string.Join(" ", args);
    }

    private CliToolResult? ExecuteCliTool(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = CliToolPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (VerboseLogging)
        {
            Log.LogMessage(MessageImportance.Normal, $"Executing: {CliToolPath} {arguments}");
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Log.LogError("Failed to start CLI tool process.");
            return null;
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Log stderr (verbose/debug output from CLI)
        if (!string.IsNullOrWhiteSpace(stderr) && VerboseLogging)
        {
            foreach (var line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Log.LogMessage(MessageImportance.Normal, $"[CLI] {line.Trim()}");
            }
        }

        // Parse JSON output from stdout
        if (string.IsNullOrWhiteSpace(stdout))
        {
            Log.LogError($"CLI tool returned no output. Exit code: {process.ExitCode}");
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<CliToolResult>(stdout);
            return result;
        }
        catch (JsonException ex)
        {
            Log.LogError($"Failed to parse CLI output: {ex.Message}");
            Log.LogError($"Raw output: {stdout}");
            return null;
        }
    }

    /// <summary>
    /// Result from the CLI tool, deserialized from JSON.
    /// </summary>
    private sealed class CliToolResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string[] Files { get; set; } = Array.Empty<string>();
    }
}
#endif
