// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Extensions;
using DotCompute.Runtime.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Examples;

/// <summary>
/// Example demonstrating the Generator ↔ Runtime integration bridge.
/// This shows how generated kernel code can seamlessly use the runtime orchestrator.
/// </summary>
public static class IntegrationExample
{
    /// <summary>
    /// Example of setting up the DotCompute runtime with dependency injection.
    /// </summary>
    public static async Task<IServiceProvider> SetupRuntimeAsync()
    {
        // Create a host builder with DotCompute runtime services
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add DotCompute runtime services
                services.AddDotComputeRuntime(options =>
                {
                    options.EnableAutoDiscovery = true;
                    options.MaxAccelerators = 8;
                    options.EnableDebugLogging = true;
                });

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
            })
            .Build();

        // Initialize the runtime
        await host.Services.InitializeDotComputeRuntimeAsync();

        return host.Services;
    }

    /// <summary>
    /// Example of how generated kernel code would call the orchestrator.
    /// This simulates what the source generator would produce.
    /// </summary>
    public static async Task ExecuteExampleKernelAsync(IServiceProvider serviceProvider)
    {
        var orchestrator = serviceProvider.GetRequiredService<IComputeOrchestrator>();
        var logger = serviceProvider.GetRequiredService<ILogger<IntegrationExample>>();

        try
        {
            logger.LogInformation("Demonstrating kernel execution through orchestrator");

            // This simulates what generated kernel wrapper code would do:
            // 1. Call the orchestrator with kernel name and arguments
            // 2. Orchestrator automatically selects optimal backend
            // 3. Orchestrator handles compilation, caching, and execution

            // Example: Generated wrapper for VectorAdd kernel would call:
            // await orchestrator.ExecuteAsync<void>("MyNamespace.VectorAdd", inputA, inputB, result, length);

            // For this demo, we'll just validate the orchestrator can select accelerators
            var accelerators = await orchestrator.GetSupportedAcceleratorsAsync("DemoKernel");
            logger.LogInformation("Found {Count} accelerators that could support DemoKernel", accelerators.Count);

            if (accelerators.Count > 0)
            {
                var optimal = await orchestrator.GetOptimalAcceleratorAsync("DemoKernel");
                if (optimal != null)
                {
                    logger.LogInformation("Optimal accelerator selected: {AcceleratorType} - {Name}",

                        optimal.Info.DeviceType, optimal.Info.Name);
                }
            }

            logger.LogInformation("Integration example completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Integration example failed");
            throw;
        }
    }

    /// <summary>
    /// Complete example showing the full integration workflow.
    /// </summary>
    public static async Task RunFullExampleAsync()
    {
        var serviceProvider = await SetupRuntimeAsync();

        // Register a sample kernel to demonstrate discovery

        var discoveryService = serviceProvider.GetRequiredService<GeneratedKernelDiscoveryService>();
        var executionService = serviceProvider.GetRequiredService<KernelExecutionServiceSimplified>();

        // Manually register a demo kernel (normally this would be done by the generator)

        var demoKernel = new KernelRegistrationInfo
        {
            Name = "DemoKernel",
            FullName = "DotCompute.Runtime.Examples.DemoKernel",
            ContainingType = typeof(IntegrationExample),
            SupportedBackends = ["CPU", "CUDA"],
            VectorSize = 8,
            IsParallel = true
        };


        executionService.RegisterKernels([demoKernel]);

        // Now demonstrate kernel execution

        await ExecuteExampleKernelAsync(serviceProvider);
    }
}

/// <summary>
/// Example kernel class that demonstrates the [Kernel] attribute pattern.
/// This shows how users would define kernels that get processed by the generator.
/// </summary>
public static class ExampleKernels
{
    // Example of how a user would define a kernel:
    // [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA, VectorSize = 8)]
    // public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    // {
    //     int index = Kernel.ThreadId.X;
    //     if (index < result.Length)
    //         result[index] = a[index] + b[index];
    // }


    /// <summary>
    /// Demonstrates the usage pattern for the generated wrapper.
    /// </summary>
    public static async Task ExampleUsageAsync(IServiceProvider serviceProvider)
    {
        // In the generated wrapper, this would be available:
        // await VectorAddKernelExecutor.ExecuteAsync(inputA, inputB, output, length, serviceProvider);

        // The generated code would internally call:

        var orchestrator = serviceProvider.GetRequiredService<IComputeOrchestrator>();

        // This demonstrates the integration pattern:

        await orchestrator.ExecuteAsync<object>("MyNamespace.VectorAdd",

            /* inputA, inputB, output, length */);
    }
}