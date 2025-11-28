// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.CommandLine;
using DotCompute.Generators.Cli.Commands;

namespace DotCompute.Generators.Cli;

/// <summary>
/// CLI tool for DotCompute code generation tasks.
/// This tool runs in a separate process to avoid assembly version conflicts
/// with consuming projects that reference Microsoft.CodeAnalysis.
/// </summary>
internal sealed class Program
{
    /// <summary>
    /// Exit codes for the CLI tool.
    /// </summary>
    private static class ExitCodes
    {
        public const int Success = 0;
        public const int Error = 1;
        public const int NoMessagesFound = 2;
    }

    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DotCompute Code Generation CLI Tool")
        {
            GenerateCudaSerializationCommand.Create()
        };

        return await rootCommand.InvokeAsync(args);
    }
}
