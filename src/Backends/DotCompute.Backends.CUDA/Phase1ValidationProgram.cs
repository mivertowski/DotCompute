// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Simple program to validate Phase 1 CUDA backend implementation.
/// </summary>
public static class Phase1ValidationProgram
{
    /// <summary>
    /// Entry point for Phase 1 validation.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static int Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("CudaPhase1Validation");

        Console.WriteLine("=== CUDA Phase 1 Implementation Validation ===");
        Console.WriteLine();

        try
        {
            var result = CudaPhase1Validator.ValidatePhase1Implementation(logger);
            
            Console.WriteLine();
            Console.WriteLine("=== Validation Results ===");
            Console.WriteLine(result.GetSummary());

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Validation failed with exception: {ex.Message}");
            logger.LogError(ex, "Phase 1 validation failed");
            return 2;
        }
    }
}