using DotCompute.Abstractions;
using DotCompute.Core;
using DotCompute.Memory;
using DotCompute.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DotCompute.Integration.Tests.Fixtures;

public class IntegrationTestFixture : IDisposable
{
    public IHost Host { get; }
    public IServiceProvider Services => Host.Services;
    public IUnifiedMemoryManager MemoryManager { get; }
    public ILogger<IntegrationTestFixture> Logger { get; }

    public IntegrationTestFixture()
    {
        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register core services
                services.AddSingleton<IUnifiedMemoryManager, UnifiedMemoryManager>();

                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
            });

        Host = hostBuilder.Build();
        Host.Start();

        MemoryManager = Services.GetRequiredService<IUnifiedMemoryManager>();
        Logger = Services.GetRequiredService<ILogger<IntegrationTestFixture>>();
    }

    public bool IsCudaAvailable => false; // For now, focus on CPU testing

    public async Task<UnifiedBuffer<T>> CreateBufferAsync<T>(T[] data)
        where T : unmanaged
    {
        var buffer = await MemoryManager.CreateUnifiedBufferAsync<T>(data.Length);
        await buffer.WriteAsync(data);
        return buffer;
    }

    public async Task<UnifiedBuffer<T>> CreateBufferAsync<T>(int length)
        where T : unmanaged
    {
        return await MemoryManager.CreateUnifiedBufferAsync<T>(length);
    }

    // Mock kernel compilation and execution for testing
    public async Task<MockCompiledKernel> CompileKernelAsync(string kernelCode)
    {
        await Task.Delay(1); // Simulate compilation time
        return new MockCompiledKernel(kernelCode);
    }

    public async Task ExecuteKernelAsync(MockCompiledKernel kernel, params object[] arguments)
    {
        await Task.Delay(1); // Simulate execution time
        Logger.LogInformation($"Mock executing kernel: {kernel.Name}");
    }

    public void Dispose()
    {
        MemoryManager?.Dispose();
        Host?.Dispose();
    }
}

// Mock implementations for testing
public class MockCompiledKernel
{
    public string Name { get; }
    public string SourceCode { get; }

    public MockCompiledKernel(string sourceCode)
    {
        SourceCode = sourceCode;
        Name = ExtractKernelName(sourceCode);
    }

    private static string ExtractKernelName(string sourceCode)
    {
        // Simple kernel name extraction for testing
        var lines = sourceCode.Split('\n');
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("kernel") && line.Contains("void"))
            {
                var parts = line.Split(new[] { ' ', '(' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (parts[i] == "void")
                    {
                        return parts[i + 1];
                    }
                }
            }
        }
        return "unknown_kernel";
    }
}

public class IntegrationTestCollection : ICollectionDefinition<IntegrationTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}