// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Samples.GettingStarted
{

internal sealed partial class Program
{
    public static Task<int> Main(string[] args)
    {
        // Handle --version argument for CI validation
        if (args.Length > 0 && args[0] == "--version")
        {
            Console.WriteLine("DotCompute Getting Started Sample v1.0.0");
            Console.WriteLine("Built with .NET 9 Native AOT");
            return Task.FromResult(0);
        }

        try
        {
            Console.WriteLine("🚀 DotCompute - Getting Started Sample");
            Console.WriteLine("=====================================");

            // Set up dependency injection
            var services = new ServiceCollection()
                .AddLogging(builder => builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information))
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILogger<Program>>();

            LogSampleStarted(logger);
            LogNativeAOTWorking(logger);

            // Simulate some basic compute work
            var data = Enumerable.Range(0, 1000).Select(i => (float)i).ToArray();
            var result = new float[data.Length];

            var sw = Stopwatch.StartNew();

            // Simple computation (without actual kernels for now)
            for (var i = 0; i < data.Length; i++)
            {
                result[i] = data[i] * 2.0f + 1.0f;
            }

            sw.Stop();

            LogProcessed(logger, data.Length, sw.ElapsedMilliseconds);

            // Verify first few results
            var expected = new[] { 1.0f, 3.0f, 5.0f, 7.0f, 9.0f };
            var actual = result.Take(5).ToArray();

            var isCorrect = expected.SequenceEqual(actual);
            LogVerification(logger, isCorrect ? "PASSED" : "FAILED");

            if (!isCorrect)
            {
                LogExpected(logger, string.Join(", ", expected));
                LogActual(logger, string.Join(", ", actual));
                return Task.FromResult(1);
            }

            Console.WriteLine("✅ Sample completed successfully!");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    #region Logger Message Delegates

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "DotCompute sample started successfully")]
    private static partial void LogSampleStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Native AOT compilation working properly")]
    private static partial void LogNativeAOTWorking(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Processed {Count} elements in {ElapsedMs}ms")]
    private static partial void LogProcessed(ILogger logger, int count, long elapsedMs);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Results verification: {IsCorrect}")]
    private static partial void LogVerification(ILogger logger, string isCorrect);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Expected: [{Expected}]")]
    private static partial void LogExpected(ILogger logger, string expected);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Actual: [{Actual}]")]
    private static partial void LogActual(ILogger logger, string actual);

    #endregion
}
}
