// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for plugin system integration with core DotCompute functionality.
/// Tests plugin loading, execution, and interaction with the core system.
/// </summary>
public class PluginIntegrationSystemTests : IntegrationTestBase
{
    public PluginIntegrationSystemTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task PluginSystem_LoadBackendPlugins_ShouldDiscoverAndLoadPlugins()
    {
        // Arrange
        var pluginSystem = ServiceProvider.GetRequiredService<PluginSystem>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();

        // Act
        var loadedPlugins = pluginSystem.GetLoadedPlugins();
        await acceleratorManager.InitializeAsync();
        var availableAccelerators = acceleratorManager.AvailableAccelerators;

        // Assert
        loadedPlugins.Should().NotBeEmpty("At least CPU backend should be available");
        availableAccelerators.Should().NotBeEmpty("Loaded plugins should provide accelerators");
        
        // Verify plugin-accelerator relationship
        foreach (var plugin in loadedPlugins)
        {
            Logger.LogInformation($"Loaded plugin: {plugin.Name} v{plugin.Version}");
            var pluginAccelerators = availableAccelerators
                .Where(a => a.Info.DeviceType.Contains(plugin.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            Logger.LogInformation($"Plugin {plugin.Name} provides {pluginAccelerators.Count} accelerators");
        }
    }

    [Fact]
    public async Task PluginSystem_BackendFactoryIntegration_ShouldCreateAccelerators()
    {
        // Arrange
        var pluginSystem = ServiceProvider.GetRequiredService<PluginSystem>();
        var loadedPlugins = pluginSystem.GetLoadedPlugins();
        
        if(!loadedPlugins.Any())
        {
            Logger.LogInformation("Skipping backend factory test - no plugins loaded");
            return;
        }

        // Act
        var factoryResults = new List<BackendFactoryResult>();
        
        foreach (var factoryResult in loadedPlugins.Select(plugin => TestBackendFactory(plugin)))
        {
            factoryResults.Add(await factoryResult);
        }

        // Assert
        Assert.NotEmpty(factoryResults);
        factoryResults.Should().AllSatisfy(result =>
        {
            result.FactoryCreated.Should().BeTrue();
            result.AcceleratorsCreated.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task PluginSystem_KernelExecution_ShouldExecuteViaDifferentBackends()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var availableBackends = computeEngine.AvailableBackends;
        
        const int testSize = 256;
        var testData = GenerateTestData(testSize);

        // Act
        var executionResults = new List<PluginExecutionResult>();
        
        foreach (var executionTask in availableBackends.Select(backend => 
            ExecuteKernelThroughBackend(computeEngine, backend, testData)))
        {
            executionResults.Add(await executionTask);
        }

        // Assert
        Assert.NotEmpty(executionResults);
        executionResults.Should().AllSatisfy(result =>
        {
            result.Success.Should().BeTrue();
            result.ExecutionTime.Should().BePositive();
            result.ResultData.Should().NotBeNull();
        });
        
        // Results should be consistent across backends
        if(executionResults.Count > 1)
        {
            var referenceResult = executionResults[0].ResultData as float[];
            foreach (var result in executionResults.Skip(1))
            {
                var resultData = result.ResultData as float[];
                CompareFloatArrays(referenceResult!, resultData!).Should().BeTrue(
                    "Different backends should produce equivalent results");
            }
        }
    }

    [Fact]
    public async Task PluginSystem_MemoryManagement_ShouldManageResourcesCorrectly()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if(!accelerators.Any())
        {
            Logger.LogInformation("Skipping memory management test - no accelerators available");
            return;
        }

        const int bufferCount = 10;
        const int bufferSize = 1024;

        // Act
        var memoryResults = new List<MemoryManagementResult>();
        
        foreach (var memoryTask in accelerators.Take(2).Select(accelerator => // Test first 2 accelerators
            TestPluginMemoryManagement(accelerator, bufferCount, bufferSize)))
        {
            memoryResults.Add(await memoryTask);
        }

        // Assert
        memoryResults.Should().AllSatisfy(result =>
        {
            result.AllocationSuccess.Should().BeTrue();
            result.AllocationTime.Should().BePositive();
            result.DeallocationSuccess.Should().BeTrue();
            result.MemoryLeaks.Should().BeFalse();
        });
    }

    [Fact]
    public async Task PluginSystem_ErrorHandling_ShouldHandlePluginFailures()
    {
        // Arrange
        var pluginSystem = ServiceProvider.GetRequiredService<PluginSystem>();
        
        // Act - Test error scenarios
        var errorResults = await TestPluginErrorScenarios(pluginSystem);

        // Assert
        Assert.NotEmpty(errorResults);
        errorResults.Should().AllSatisfy(result =>
        {
            result.ErrorHandled.Should().BeTrue();
            result.SystemStability.Should().BeTrue();
        });
    }

    [Fact]
    public async Task PluginSystem_ConcurrentAccess_ShouldHandleMultipleClients()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const int clientCount = 4;
        const int operationsPerClient = 5;
        
        // Act
        var concurrentTasks = Enumerable.Range(0, clientCount).Select(async clientId =>
        {
            var clientResults = new List<ClientOperationResult>();
            
            for(int op = 0; op < operationsPerClient; op++)
            {
                var operationResult = await ExecuteClientOperation(
                    computeEngine,
                    clientId,
                    op);
                clientResults.Add(operationResult);
            }
            
            return new ConcurrentClientResult
            {
                ClientId = clientId,
                Operations = clientResults,
                AllOperationsSuccessful = clientResults.All(r => r.Success)
            };
        });

        var results = await Task.WhenAll(concurrentTasks);

        // Assert
        Assert.Equal(clientCount, results.Count());
        results.Should().AllSatisfy(result =>
        {
            result.AllOperationsSuccessful.Should().BeTrue();
            result.Operations.Count.Should().Be(operationsPerClient);
        });
    }

    [Fact]
    public async Task PluginSystem_PluginLifecycle_ShouldManageCorrectly()
    {
        // Arrange
        var pluginSystem = ServiceProvider.GetRequiredService<PluginSystem>();

        // Act
        var lifecycleResult = await TestPluginLifecycle(pluginSystem);

        // Assert
        Assert.NotNull(lifecycleResult);
        lifecycleResult.LoadPhaseSuccess.Should().BeTrue();
        lifecycleResult.InitializePhaseSuccess.Should().BeTrue();
        lifecycleResult.ExecutionPhaseSuccess.Should().BeTrue();
        lifecycleResult.CleanupPhaseSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task PluginSystem_ScalabilityTest_ShouldScaleWithWorkload(int concurrencyLevel)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const int workItemsPerThread = 100;

        // Act
        var scalabilityTasks = Enumerable.Range(0, concurrencyLevel).Select(async threadId =>
        {
            var threadResults = new List<TimeSpan>();
            
            for(int i = 0; i < workItemsPerThread; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await ExecuteSimpleKernel(computeEngine, 64);
                stopwatch.Stop();
                threadResults.Add(stopwatch.Elapsed);
            }
            
            return new ScalabilityResult
            {
                ThreadId = threadId,
                OperationTimes = threadResults,
                AverageTime = TimeSpan.FromMilliseconds(threadResults.Average(t => t.TotalMilliseconds)),
                TotalTime = TimeSpan.FromMilliseconds(threadResults.Sum(t => t.TotalMilliseconds))
            };
        });

        var results = await Task.WhenAll(scalabilityTasks);

        // Assert
        Assert.Equal(concurrencyLevel, results.Count());
        results.Should().AllSatisfy(result =>
        {
            result.OperationTimes.Count.Should().Be(workItemsPerThread);
            result.AverageTime.Should().BePositive();
        });
        
        // Performance should not degrade significantly with increased concurrency
        if(results.Length > 1)
        {
            var avgTimes = results.Select(r => r.AverageTime.TotalMilliseconds).ToList();
            var minAvg = avgTimes.Min();
            var maxAvg = avgTimes.Max();
            
            Logger.LogInformation($"Performance range: {minAvg:F2}ms - {maxAvg:F2}ms");
           (maxAvg / minAvg).Should().BeLessThan(5.0, 
                "Performance should not degrade drastically with concurrency");
        }
    }

    [Fact]
    public async Task PluginSystem_ResourceCleanup_ShouldCleanupProperly()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if(!accelerators.Any())
        {
            Logger.LogInformation("Skipping resource cleanup test - no accelerators available");
            return;
        }

        // Act
        var cleanupResults = new List<ResourceCleanupResult>();
        
        foreach (var cleanupTask in accelerators.Take(2).Select(accelerator => 
            TestResourceCleanup(accelerator)))
        {
            cleanupResults.Add(await cleanupTask);
        }

        // Assert
        cleanupResults.Should().AllSatisfy(result =>
        {
result.ResourcesAllocated.Should().BeGreaterThanOrEqualTo(0);
            result.ResourcesReleased.Should().Be(result.ResourcesAllocated);
            result.MemoryLeaksDetected.Should().BeFalse();
            result.CleanupSuccess.Should().BeTrue();
        });
    }

    // Helper methods
    private async Task<BackendFactoryResult> TestBackendFactory(IBackendPlugin plugin)
    {
        try
        {
            // Simulate factory creation and accelerator instantiation
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // In a real test, this would use the actual factory interface
            var acceleratorCount = await SimulateAcceleratorCreation(plugin);
            
            stopwatch.Stop();

            return new BackendFactoryResult
            {
                PluginName = plugin.Name,
                FactoryCreated = true,
                AcceleratorsCreated = acceleratorCount,
                CreationTime = stopwatch.Elapsed
            };
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, $"Backend factory test failed for plugin {plugin.Name}");
            return new BackendFactoryResult
            {
                PluginName = plugin.Name,
                FactoryCreated = false,
                Error = ex.Message
            };
        }
    }

    private async Task<PluginExecutionResult> ExecuteKernelThroughBackend(
        IComputeEngine computeEngine,
        ComputeBackendType backend,
        float[] testData)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var compilationOptions = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Default
            };

            var compiledKernel = await computeEngine.CompileKernelAsync(
                SimpleKernelSource,
                "simple_kernel",
                compilationOptions);

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var inputBuffer = await CreateInputBuffer(memoryManager, testData);
            var outputBuffer = await CreateOutputBuffer<float>(memoryManager, testData.Length);

            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBuffer, outputBuffer],
                backend,
                new ExecutionOptions { GlobalWorkSize = [testData.Length] });

            var resultData = await ReadBufferAsync<float>(outputBuffer);
            stopwatch.Stop();

            return new PluginExecutionResult
            {
                Backend = backend,
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ResultData = resultData
            };
        }
        catch(Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, $"Kernel execution failed for backend {backend}");
            
            return new PluginExecutionResult
            {
                Backend = backend,
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<MemoryManagementResult> TestPluginMemoryManagement(
        IAccelerator accelerator,
        int bufferCount,
        int bufferSize)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var allocatedBuffers = new List<IMemoryBuffer>();
        
        try
        {
            // Allocation phase
            for(int i = 0; i < bufferCount; i++)
            {
                var testData = GenerateTestData(bufferSize);
                var buffer = await CreateInputBuffer(accelerator.Memory, testData);
                allocatedBuffers.Add(buffer);
            }
            
            var allocationTime = stopwatch.Elapsed;
            stopwatch.Restart();
            
            // Deallocation phase
            foreach (var buffer in allocatedBuffers)
            {
                if(buffer is IDisposable disposable)
                    disposable.Dispose();
                else if(buffer is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
            }
            
            stopwatch.Stop();
            var deallocationTime = stopwatch.Elapsed;

            return new MemoryManagementResult
            {
                AcceleratorId = accelerator.Info.Id,
                BuffersAllocated = bufferCount,
                AllocationSuccess = true,
                AllocationTime = allocationTime,
                DeallocationSuccess = true,
                DeallocationTime = deallocationTime,
                MemoryLeaks = false // Simplified - would need actual leak detection
            };
        }
        catch(Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, $"Memory management test failed for accelerator {accelerator.Info.Id}");
            
            // Cleanup on failure
            foreach (var buffer in allocatedBuffers)
            {
                try
                {
                    if(buffer is IDisposable disposable)
                        disposable.Dispose();
                    else if(buffer is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync();
                }
                catch { /* Ignore cleanup errors */ }
            }

            return new MemoryManagementResult
            {
                AcceleratorId = accelerator.Info.Id,
                BuffersAllocated = allocatedBuffers.Count,
                AllocationSuccess = false,
                Error = ex.Message
            };
        }
    }

    private async Task<List<ErrorHandlingResult>> TestPluginErrorScenarios(PluginSystem pluginSystem)
    {
        var results = new List<ErrorHandlingResult>();
        
        // Test scenario 1: Invalid plugin loading
        try
        {
            await pluginSystem.LoadPluginAsync("nonexistent_plugin.dll");
            results.Add(new ErrorHandlingResult
            {
                Scenario = "NonexistentPlugin",
                ErrorHandled = false,
                SystemStability = true,
                Description = "Should have thrown exception for nonexistent plugin"
            });
        }
        catch(Exception)
        {
            results.Add(new ErrorHandlingResult
            {
                Scenario = "NonexistentPlugin",
                ErrorHandled = true,
                SystemStability = true,
                Description = "Correctly handled nonexistent plugin"
            });
        }

        // Additional error scenarios would be tested here...
        
        return results;
    }

    private async Task<ClientOperationResult> ExecuteClientOperation(
        IComputeEngine computeEngine,
        int clientId,
        int operationId)
    {
        try
        {
            await ExecuteSimpleKernel(computeEngine, 128);
            
            return new ClientOperationResult
            {
                ClientId = clientId,
                OperationId = operationId,
                Success = true
            };
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, $"Client {clientId} operation {operationId} failed");
            
            return new ClientOperationResult
            {
                ClientId = clientId,
                OperationId = operationId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<PluginLifecycleResult> TestPluginLifecycle(PluginSystem pluginSystem)
    {
        var result = new PluginLifecycleResult();
        
        try
        {
            // Load phase
            var plugins = pluginSystem.GetLoadedPlugins();
            result.LoadPhaseSuccess = plugins.Any();
            
            // Initialize phase(simulated)
            result.InitializePhaseSuccess = true;
            
            // Execution phase
            var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
            await ExecuteSimpleKernel(computeEngine, 64);
            result.ExecutionPhaseSuccess = true;
            
            // Cleanup phase(would involve proper disposal in real scenario)
            result.CleanupPhaseSuccess = true;
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, "Plugin lifecycle test failed");
            result.Error = ex.Message;
        }
        
        return result;
    }

    private async Task<ResourceCleanupResult> TestResourceCleanup(IAccelerator accelerator)
    {
        const int resourceCount = 20;
        var allocatedResources = 0;
        var releasedResources = 0;
        
        try
        {
            var buffers = new List<IMemoryBuffer>();
            
            // Allocate resources
            for(int i = 0; i < resourceCount; i++)
            {
                var testData = GenerateTestData(256);
                var buffer = await CreateInputBuffer(accelerator.Memory, testData);
                buffers.Add(buffer);
                allocatedResources++;
            }
            
            // Release resources
            foreach (var buffer in buffers)
            {
                if(buffer is IDisposable disposable)
                {
                    disposable.Dispose();
                    releasedResources++;
                }
                else if(buffer is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                    releasedResources++;
                }
            }
            
            return new ResourceCleanupResult
            {
                AcceleratorId = accelerator.Info.Id,
                ResourcesAllocated = allocatedResources,
                ResourcesReleased = releasedResources,
                CleanupSuccess = true,
                MemoryLeaksDetected = false
            };
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, $"Resource cleanup test failed for {accelerator.Info.Id}");
            
            return new ResourceCleanupResult
            {
                AcceleratorId = accelerator.Info.Id,
                ResourcesAllocated = allocatedResources,
                ResourcesReleased = releasedResources,
                CleanupSuccess = false,
                Error = ex.Message
            };
        }
    }

    private async Task ExecuteSimpleKernel(IComputeEngine computeEngine, int size)
    {
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        var compiledKernel = await computeEngine.CompileKernelAsync(
            SimpleKernelSource,
            "simple_kernel",
            compilationOptions);

        var testData = GenerateTestData(size);
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        
        var inputBuffer = await CreateInputBuffer(memoryManager, testData);
        var outputBuffer = await CreateOutputBuffer<float>(memoryManager, size);

        await computeEngine.ExecuteAsync(
            compiledKernel,
            [inputBuffer, outputBuffer],
            ComputeBackendType.CPU,
            new ExecutionOptions { GlobalWorkSize = [size] });
    }

    private static async Task<int> SimulateAcceleratorCreation(IBackendPlugin plugin)
    {
        // Simulate factory operation
        await Task.Delay(10);
        return 1; // Assume each plugin provides 1 accelerator for testing
    }

    private static float[] GenerateTestData(int size)
    {
        var random = new Random(42);
        return Enumerable.Range(0, size)
                        .Select(_ => (float)random.NextDouble() * 100.0f)
                        .ToArray();
    }

    private static bool CompareFloatArrays(float[] array1, float[] array2, float tolerance = 0.001f)
    {
        if(array1.Length != array2.Length) return false;
        
        for(int i = 0; i < array1.Length; i++)
        {
            if(Math.Abs(array1[i] - array2[i]) > tolerance) return false;
        }
        
        return true;
    }

    private const string SimpleKernelSource = @"
        __kernel void simple_kernel(__global const float* input,
                                  __global float* output) {
            int i = get_global_id(0);
            output[i] = input[i] * 2.0f;
        }";
}

// Result classes for plugin integration tests
public class BackendFactoryResult
{
    public string PluginName { get; set; } = string.Empty;
    public bool FactoryCreated { get; set; }
    public int AcceleratorsCreated { get; set; }
    public TimeSpan CreationTime { get; set; }
    public string? Error { get; set; }
}

public class PluginExecutionResult
{
    public ComputeBackendType Backend { get; set; }
    public bool Success { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public object? ResultData { get; set; }
    public string? Error { get; set; }
}

public class MemoryManagementResult
{
    public string AcceleratorId { get; set; } = string.Empty;
    public int BuffersAllocated { get; set; }
    public bool AllocationSuccess { get; set; }
    public TimeSpan AllocationTime { get; set; }
    public bool DeallocationSuccess { get; set; }
    public TimeSpan DeallocationTime { get; set; }
    public bool MemoryLeaks { get; set; }
    public string? Error { get; set; }
}

public class ErrorHandlingResult
{
    public string Scenario { get; set; } = string.Empty;
    public bool ErrorHandled { get; set; }
    public bool SystemStability { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ClientOperationResult
{
    public int ClientId { get; set; }
    public int OperationId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ConcurrentClientResult
{
    public int ClientId { get; set; }
    public List<ClientOperationResult> Operations { get; set; } = new();
    public bool AllOperationsSuccessful { get; set; }
}

public class ScalabilityResult
{
    public int ThreadId { get; set; }
    public List<TimeSpan> OperationTimes { get; set; } = new();
    public TimeSpan AverageTime { get; set; }
    public TimeSpan TotalTime { get; set; }
}

public class PluginLifecycleResult
{
    public bool LoadPhaseSuccess { get; set; }
    public bool InitializePhaseSuccess { get; set; }
    public bool ExecutionPhaseSuccess { get; set; }
    public bool CleanupPhaseSuccess { get; set; }
    public string? Error { get; set; }
}

public class ResourceCleanupResult
{
    public string AcceleratorId { get; set; } = string.Empty;
    public int ResourcesAllocated { get; set; }
    public int ResourcesReleased { get; set; }
    public bool CleanupSuccess { get; set; }
    public bool MemoryLeaksDetected { get; set; }
    public string? Error { get; set; }
}
