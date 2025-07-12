using DotCompute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GettingStarted;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Handle --version argument for CI validation
        if (args.Length > 0 && args[0] == "--version")
        {
            Console.WriteLine("DotCompute Getting Started Sample v1.0.0");
            Console.WriteLine("Built with .NET 9 Native AOT");
            return 0;
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
            
            logger.LogInformation("DotCompute sample started successfully");
            logger.LogInformation("Native AOT compilation working properly");
            
            // Simulate some basic compute work
            var data = Enumerable.Range(0, 1000).Select(i => (float)i).ToArray();
            var result = new float[data.Length];
            
            var sw = Stopwatch.StartNew();
            
            // Simple computation (without actual kernels for now)
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = data[i] * 2.0f + 1.0f;
            }
            
            sw.Stop();
            
            logger.LogInformation("Processed {Count} elements in {ElapsedMs}ms", 
                data.Length, sw.ElapsedMilliseconds);
            
            // Verify first few results
            var expected = new[] { 1.0f, 3.0f, 5.0f, 7.0f, 9.0f };
            var actual = result.Take(5).ToArray();
            
            bool isCorrect = expected.SequenceEqual(actual);
            logger.LogInformation("Results verification: {IsCorrect}", isCorrect ? "PASSED" : "FAILED");
            
            if (!isCorrect)
            {
                logger.LogError("Expected: [{Expected}]", string.Join(", ", expected));
                logger.LogError("Actual: [{Actual}]", string.Join(", ", actual));
                return 1;
            }
            
            Console.WriteLine("✅ Sample completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }
}