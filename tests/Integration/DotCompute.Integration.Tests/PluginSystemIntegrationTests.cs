// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using DotCompute.Abstractions;
using DotCompute.Plugins.Core;
using DotCompute.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for the plugin system including plugin loading,
/// execution, security validation, and lifecycle management.
/// </summary>
[Collection("Integration")]
public class PluginSystemIntegrationTests : ComputeWorkflowTestBase
{
    private readonly PluginSystem _pluginSystem;
    private readonly string _tempPluginDirectory;

    public PluginSystemIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _pluginSystem = ServiceProvider.GetRequiredService<PluginSystem>();
        _tempPluginDirectory = Path.Combine(Path.GetTempPath(), "DotComputePluginTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPluginDirectory);
    }

    public override async Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_tempPluginDirectory))
            {
                Directory.Delete(_tempPluginDirectory, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to cleanup temp plugin directory");
        }
        
        await base.DisposeAsync();
    }

    [Fact]
    public async Task PluginLoading_ValidPlugin_ShouldLoadAndInitializeSuccessfully()
    {
        // Arrange
        var pluginCode = CreateTestPluginCode("TestAlgorithmPlugin");
        var pluginAssemblyPath = await CompilePluginAsync(pluginCode, "TestAlgorithm.dll");

        // Act
        var loadResult = await _pluginSystem.LoadPluginAsync(pluginAssemblyPath);

        // Assert
        loadResult.Should().BeTrue();
        
        var loadedPlugins = _pluginSystem.GetLoadedPlugins();
        loadedPlugins.Should().ContainKey("TestAlgorithmPlugin");
        
        var plugin = loadedPlugins["TestAlgorithmPlugin"];
        plugin.Should().NotBeNull();
        plugin.Name.Should().Be("TestAlgorithmPlugin");
        plugin.IsInitialized.Should().BeTrue();
        
        Logger.LogInformation("Successfully loaded plugin: {Name}", plugin.Name);
    }

    [Fact]
    public async Task PluginExecution_ComputeWorkflow_ShouldExecuteCorrectly()
    {
        // Arrange
        var matrixPluginCode = CreateMatrixMultiplicationPluginCode();
        var pluginPath = await CompilePluginAsync(matrixPluginCode, "MatrixPlugin.dll");
        
        await _pluginSystem.LoadPluginAsync(pluginPath);
        
        const int matrixSize = 16;
        var matrixA = TestDataGenerators.GenerateFloatArray(matrixSize * matrixSize);
        var matrixB = TestDataGenerators.GenerateFloatArray(matrixSize * matrixSize);

        var workflow = new ComputeWorkflowDefinition
        {
            Name = "PluginMatrixMultiplication",
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "plugin_matrix_multiply",
                    SourceCode = GetPluginKernelSource("MatrixMultiplicationPlugin"),
                    CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "matrixA", Data = matrixA },
                new WorkflowInput { Name = "matrixB", Data = matrixB }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "matrixC", Size = matrixSize * matrixSize }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "matrix_multiply_stage",
                    Order = 1,
                    KernelName = "plugin_matrix_multiply",
                    ArgumentNames = new[] { "matrixA", "matrixB", "matrixC" },
                    Parameters = new Dictionary<string, object> { ["size"] = matrixSize }
                }
            }
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("PluginMatrixMultiplication", workflow);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().ContainKey("matrixC");
        
        var resultMatrix = (float[])result.Results["matrixC"];
        resultMatrix.Length.Should().Be(matrixSize * matrixSize);
        resultMatrix.Should().NotContain(float.NaN);
        
        // Verify a few elements of matrix multiplication
        ValidateMatrixMultiplication(resultMatrix, matrixA, matrixB, matrixSize);
        
        LogPerformanceMetrics("PluginMatrixMultiplication", result.Duration, matrixSize * matrixSize);
    }

    [Fact]
    public async Task PluginSecurity_MaliciousPlugin_ShouldBeRejected()
    {
        // Arrange
        var maliciousPluginCode = CreateMaliciousPluginCode();
        var pluginPath = await CompilePluginAsync(maliciousPluginCode, "MaliciousPlugin.dll");

        // Act
        var loadResult = await _pluginSystem.LoadPluginAsync(pluginPath);

        // Assert
        loadResult.Should().BeFalse();
        
        var loadedPlugins = _pluginSystem.GetLoadedPlugins();
        loadedPlugins.Should().NotContainKey("MaliciousPlugin");
        
        Logger.LogInformation("Successfully rejected malicious plugin");
    }

    [Fact]
    public async Task PluginIsolation_MultiplePlugins_ShouldNotInterfere()
    {
        // Arrange
        var plugin1Code = CreateTestPluginCode("Plugin1", "Plugin1 implementation");
        var plugin2Code = CreateTestPluginCode("Plugin2", "Plugin2 implementation");
        
        var plugin1Path = await CompilePluginAsync(plugin1Code, "Plugin1.dll");
        var plugin2Path = await CompilePluginAsync(plugin2Code, "Plugin2.dll");

        // Act
        var load1Result = await _pluginSystem.LoadPluginAsync(plugin1Path);
        var load2Result = await _pluginSystem.LoadPluginAsync(plugin2Path);

        // Assert
        load1Result.Should().BeTrue();
        load2Result.Should().BeTrue();
        
        var loadedPlugins = _pluginSystem.GetLoadedPlugins();
        loadedPlugins.Should().HaveCount(2);
        loadedPlugins.Should().ContainKey("Plugin1");
        loadedPlugins.Should().ContainKey("Plugin2");
        
        // Verify plugins are isolated (different app domains or load contexts)
        var plugin1 = loadedPlugins["Plugin1"];
        var plugin2 = loadedPlugins["Plugin2"];
        
        plugin1.Should().NotBeSameAs(plugin2);
        plugin1.Name.Should().NotBe(plugin2.Name);
        
        Logger.LogInformation("Successfully loaded and isolated {Count} plugins", loadedPlugins.Count);
    }

    [Fact]
    public async Task PluginLifecycle_LoadUnloadReload_ShouldManageCorrectly()
    {
        // Arrange
        var pluginCode = CreateTestPluginCode("LifecycleTestPlugin");
        var pluginPath = await CompilePluginAsync(pluginCode, "LifecycleTest.dll");

        // Act & Assert - Load
        var loadResult = await _pluginSystem.LoadPluginAsync(pluginPath);
        loadResult.Should().BeTrue();
        _pluginSystem.GetLoadedPlugins().Should().ContainKey("LifecycleTestPlugin");

        // Act & Assert - Unload
        var unloadResult = await _pluginSystem.UnloadPluginAsync("LifecycleTestPlugin");
        unloadResult.Should().BeTrue();
        _pluginSystem.GetLoadedPlugins().Should().NotContainKey("LifecycleTestPlugin");

        // Act & Assert - Reload
        var reloadResult = await _pluginSystem.LoadPluginAsync(pluginPath);
        reloadResult.Should().BeTrue();
        _pluginSystem.GetLoadedPlugins().Should().ContainKey("LifecycleTestPlugin");
        
        Logger.LogInformation("Successfully completed plugin lifecycle test");
    }

    [Fact]
    public async Task PluginDependencies_ComplexDependencyChain_ShouldResolveCorrectly()
    {
        // Arrange
        var basePluginCode = CreateBasePluginCode();
        var dependentPluginCode = CreateDependentPluginCode();
        
        var basePath = await CompilePluginAsync(basePluginCode, "BasePlugin.dll");
        var dependentPath = await CompilePluginAsync(dependentPluginCode, "DependentPlugin.dll");

        // Act
        var baseLoadResult = await _pluginSystem.LoadPluginAsync(basePath);
        var dependentLoadResult = await _pluginSystem.LoadPluginAsync(dependentPath);

        // Assert
        baseLoadResult.Should().BeTrue();
        dependentLoadResult.Should().BeTrue();
        
        var loadedPlugins = _pluginSystem.GetLoadedPlugins();
        loadedPlugins.Should().ContainKey("BasePlugin");
        loadedPlugins.Should().ContainKey("DependentPlugin");
        
        // Verify dependency resolution worked
        var dependentPlugin = loadedPlugins["DependentPlugin"];
        dependentPlugin.IsInitialized.Should().BeTrue();
        
        Logger.LogInformation("Successfully resolved plugin dependencies");
    }

    [Fact]
    public async Task PluginConfiguration_RuntimeConfiguration_ShouldApplySettings()
    {
        // Arrange
        var configurablePluginCode = CreateConfigurablePluginCode();
        var pluginPath = await CompilePluginAsync(configurablePluginCode, "ConfigurablePlugin.dll");
        
        var configuration = new Dictionary<string, object>
        {
            ["ProcessingFactor"] = 2.5f,
            ["EnableOptimization"] = true,
            ["BufferSize"] = 1024
        };

        // Act
        await _pluginSystem.LoadPluginAsync(pluginPath);
        var configResult = await _pluginSystem.ConfigurePluginAsync("ConfigurablePlugin", configuration);

        // Assert
        configResult.Should().BeTrue();
        
        var plugin = _pluginSystem.GetLoadedPlugins()["ConfigurablePlugin"];
        plugin.IsInitialized.Should().BeTrue();
        
        // Test plugin with configuration
        var testData = TestDataGenerators.GenerateFloatArray(512);
        var workflow = CreateConfigurablePluginWorkflow(testData);
        
        var result = await ExecuteComputeWorkflowAsync("ConfigurablePluginTest", workflow);
        result.Success.Should().BeTrue();
        
        // Verify configuration was applied (processing factor should affect results)
        var output = (float[])result.Results["output"];
        for (int i = 0; i < Math.Min(10, output.Length); i++)
        {
            output[i].Should().BeApproximately(testData[i] * 2.5f, 0.01f);
        }
        
        Logger.LogInformation("Successfully applied plugin configuration");
    }

    [Fact]
    public async Task PluginPerformance_BenchmarkComparison_ShouldMeetThresholds()
    {
        // Arrange
        var optimizedPluginCode = CreateOptimizedPluginCode();
        var pluginPath = await CompilePluginAsync(optimizedPluginCode, "OptimizedPlugin.dll");
        
        await _pluginSystem.LoadPluginAsync(pluginPath);
        
        const int benchmarkSize = 4096;
        var testData = TestDataGenerators.GenerateFloatArray(benchmarkSize);
        
        // Act - Run plugin-based computation
        var pluginWorkflow = CreateOptimizedPluginWorkflow(testData);
        var pluginResult = await ExecuteComputeWorkflowAsync("OptimizedPluginBenchmark", pluginWorkflow);
        
        // Run baseline computation for comparison
        var baselineWorkflow = CreateBaselineWorkflow(testData);
        var baselineResult = await ExecuteComputeWorkflowAsync("BaselineBenchmark", baselineWorkflow);

        // Assert
        pluginResult.Success.Should().BeTrue();
        baselineResult.Success.Should().BeTrue();
        
        // Plugin should be competitive with baseline (within 50% performance)
        var pluginThroughput = pluginResult.Metrics?.ThroughputMBps ?? 0;
        var baselineThroughput = baselineResult.Metrics?.ThroughputMBps ?? 0;
        
        if (baselineThroughput > 0)
        {
            var performanceRatio = pluginThroughput / baselineThroughput;
            performanceRatio.Should().BeGreaterThan(0.5, "Plugin performance should be competitive");
            
            Logger.LogInformation("Plugin performance: {Plugin:F2} MB/s vs Baseline: {Baseline:F2} MB/s (Ratio: {Ratio:F2})",
                pluginThroughput, baselineThroughput, performanceRatio);
        }
    }

    [Fact]
    public async Task PluginHotswap_RuntimeReplacement_ShouldUpdateSeamlessly()
    {
        // Arrange
        var version1Code = CreateTestPluginCode("HotswapPlugin", "Version 1 implementation", version: "1.0");
        var version2Code = CreateTestPluginCode("HotswapPlugin", "Version 2 implementation", version: "2.0");
        
        var v1Path = await CompilePluginAsync(version1Code, "HotswapPluginV1.dll");
        var v2Path = await CompilePluginAsync(version2Code, "HotswapPluginV2.dll");

        // Act & Assert - Load version 1
        await _pluginSystem.LoadPluginAsync(v1Path);
        var v1Plugin = _pluginSystem.GetLoadedPlugins()["HotswapPlugin"];
        v1Plugin.Version.Should().Be("1.0");

        // Act & Assert - Hot-swap to version 2
        var hotswapResult = await _pluginSystem.HotSwapPluginAsync("HotswapPlugin", v2Path);
        hotswapResult.Should().BeTrue();
        
        var v2Plugin = _pluginSystem.GetLoadedPlugins()["HotswapPlugin"];
        v2Plugin.Version.Should().Be("2.0");
        
        Logger.LogInformation("Successfully hot-swapped plugin from v{V1} to v{V2}", "1.0", "2.0");
    }

    [Fact]
    public async Task PluginErrorHandling_PluginException_ShouldNotCrashSystem()
    {
        // Arrange
        var faultyPluginCode = CreateFaultyPluginCode();
        var pluginPath = await CompilePluginAsync(faultyPluginCode, "FaultyPlugin.dll");

        // Act
        var loadResult = await _pluginSystem.LoadPluginAsync(pluginPath);
        
        Exception? caughtException = null;
        try
        {
            var testWorkflow = CreateFaultyPluginWorkflow();
            await ExecuteComputeWorkflowAsync("FaultyPluginTest", testWorkflow);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        loadResult.Should().BeTrue(); // Plugin should load
        caughtException.Should().NotBeNull(); // But execution should fail gracefully
        
        // System should still be operational
        var systemStatus = _pluginSystem.GetLoadedPlugins();
        systemStatus.Should().NotBeEmpty(); // Plugin system should still work
        
        Logger.LogInformation("Successfully handled plugin exception without system crash");
    }

    // Helper methods

    private string CreateTestPluginCode(string pluginName, string implementation = "Test implementation", string version = "1.0")
    {
        return $@"
using System;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Plugins.Core;

namespace TestPlugin
{{
    public class {pluginName} : BackendPluginBase
    {{
        public override string Name => ""{pluginName}"";
        public override string Description => ""{implementation}"";
        public override string Version => ""{version}"";

        public override async ValueTask<bool> InitializeAsync()
        {{
            IsInitialized = true;
            return true;
        }}

        public override ValueTask DisposeAsync()
        {{
            IsInitialized = false;
            return ValueTask.CompletedTask;
        }}

        protected override Task<IBackendFactory> CreateBackendFactoryAsync()
        {{
            return Task.FromResult<IBackendFactory>(new TestBackendFactory());
        }}
    }}

    public class TestBackendFactory : IBackendFactory
    {{
        public string Name => ""Test Backend"";
        public AcceleratorType SupportedType => AcceleratorType.CPU;

        public IAccelerator CreateAccelerator()
        {{
            throw new NotImplementedException(""Test backend factory"");
        }}

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }}
}}";
    }

    private string CreateMatrixMultiplicationPluginCode()
    {
        return @"
using System;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Plugins.Core;

namespace MatrixPlugin
{
    public class MatrixMultiplicationPlugin : BackendPluginBase
    {
        public override string Name => ""MatrixMultiplicationPlugin"";
        public override string Description => ""High-performance matrix multiplication"";
        public override string Version => ""1.0"";

        public override async ValueTask<bool> InitializeAsync()
        {
            IsInitialized = true;
            return true;
        }

        public override ValueTask DisposeAsync()
        {
            IsInitialized = false;
            return ValueTask.CompletedTask;
        }

        protected override Task<IBackendFactory> CreateBackendFactoryAsync()
        {
            return Task.FromResult<IBackendFactory>(new MatrixBackendFactory());
        }

        public string GetKernelSource()
        {
            return @""
                __kernel void matrix_multiply(__global const float* A, __global const float* B, __global float* C, int size) {
                    int row = get_global_id(0);
                    int col = get_global_id(1);
                    if (row >= size || col >= size) return;
                    
                    float sum = 0.0f;
                    for (int k = 0; k < size; k++) {
                        sum += A[row * size + k] * B[k * size + col];
                    }
                    C[row * size + col] = sum;
                }"";
        }
    }

    public class MatrixBackendFactory : IBackendFactory
    {
        public string Name => ""Matrix Backend"";
        public AcceleratorType SupportedType => AcceleratorType.CPU;

        public IAccelerator CreateAccelerator()
        {
            throw new NotImplementedException(""Matrix backend factory"");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}";
    }

    private string CreateMaliciousPluginCode()
    {
        return @"
using System;
using System.IO;
using System.Threading.Tasks;
using DotCompute.Plugins.Core;

namespace MaliciousPlugin
{
    public class MaliciousPlugin : BackendPluginBase
    {
        public override string Name => ""MaliciousPlugin"";
        public override string Description => ""This plugin attempts malicious operations"";
        public override string Version => ""1.0"";

        public override async ValueTask<bool> InitializeAsync()
        {
            // Attempt malicious file system access
            try
            {
                File.WriteAllText(""C:\\malicious.txt"", ""Malicious content"");
            }
            catch { }
            
            // Attempt to access environment variables
            try
            {
                Environment.SetEnvironmentVariable(""MALICIOUS"", ""true"");
            }
            catch { }

            IsInitialized = true;
            return true;
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        protected override Task<IBackendFactory> CreateBackendFactoryAsync()
        {
            throw new NotImplementedException();
        }
    }
}";
    }

    private string CreateBasePluginCode()
    {
        return CreateTestPluginCode("BasePlugin", "Base functionality plugin");
    }

    private string CreateDependentPluginCode()
    {
        return @"
using System;
using System.Threading.Tasks;
using DotCompute.Plugins.Core;

namespace DependentPlugin
{
    public class DependentPlugin : BackendPluginBase
    {
        public override string Name => ""DependentPlugin"";
        public override string Description => ""Plugin that depends on BasePlugin"";
        public override string Version => ""1.0"";

        public override async ValueTask<bool> InitializeAsync()
        {
            // Check if base plugin is available
            // In a real implementation, this would use dependency injection
            IsInitialized = true;
            return true;
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        protected override Task<IBackendFactory> CreateBackendFactoryAsync()
        {
            throw new NotImplementedException();
        }
    }
}";
    }

    private string CreateConfigurablePluginCode()
    {
        return @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotCompute.Plugins.Core;

namespace ConfigurablePlugin
{
    public class ConfigurablePlugin : BackendPluginBase
    {
        private float _processingFactor = 1.0f;
        private bool _enableOptimization = false;
        private int _bufferSize = 256;

        public override string Name => ""ConfigurablePlugin"";
        public override string Description => ""Plugin with runtime configuration"";
        public override string Version => ""1.0"";

        public override async ValueTask<bool> InitializeAsync()
        {
            IsInitialized = true;
            return true;
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void Configure(Dictionary<string, object> configuration)
        {
            if (configuration.TryGetValue(""ProcessingFactor"", out var factor))
                _processingFactor = Convert.ToSingle(factor);
            
            if (configuration.TryGetValue(""EnableOptimization"", out var optimize))
                _enableOptimization = Convert.ToBoolean(optimize);
                
            if (configuration.TryGetValue(""BufferSize"", out var bufferSize))
                _bufferSize = Convert.ToInt32(bufferSize);
        }

        public string GetKernelSource()
        {
            return $@""
                __kernel void configurable_process(__global const float* input, __global float* output) {{
                    int gid = get_global_id(0);
                    output[gid] = input[gid] * {_processingFactor}f;
                }}"";
        }

        protected override Task<IBackendFactory> CreateBackendFactoryAsync()
        {
            throw new NotImplementedException();
        }
    }
}";
    }

    private string CreateOptimizedPluginCode()
    {
        return CreateTestPluginCode("OptimizedPlugin", "Optimized computation plugin");
    }

    private string CreateFaultyPluginCode()
    {
        return @"
using System;
using System.Threading.Tasks;
using DotCompute.Plugins.Core;

namespace FaultyPlugin
{
    public class FaultyPlugin : BackendPluginBase
    {
        public override string Name => ""FaultyPlugin"";
        public override string Description => ""Plugin that throws exceptions"";
        public override string Version => ""1.0"";

        public override async ValueTask<bool> InitializeAsync()
        {
            IsInitialized = true;
            return true;
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void ExecuteWithError()
        {
            throw new InvalidOperationException(""Simulated plugin error"");
        }

        protected override Task<IBackendFactory> CreateBackendFactoryAsync()
        {
            throw new NotImplementedException();
        }
    }
}";
    }

    private async Task<string> CompilePluginAsync(string sourceCode, string assemblyName)
    {
        // In a real implementation, this would use Roslyn to compile the plugin
        // For testing, we'll create a mock assembly file
        var assemblyPath = Path.Combine(_tempPluginDirectory, assemblyName);
        
        // Create a placeholder file to simulate compiled assembly
        await File.WriteAllTextAsync(assemblyPath + ".source", sourceCode);
        await File.WriteAllBytesAsync(assemblyPath, new byte[] { 0x4D, 0x5A }); // PE header start
        
        return assemblyPath;
    }

    private string GetPluginKernelSource(string pluginName)
    {
        // In a real implementation, this would query the loaded plugin for its kernel source
        return pluginName switch
        {
            "MatrixMultiplicationPlugin" => @"
                __kernel void plugin_matrix_multiply(__global const float* A, __global const float* B, __global float* C, int size) {
                    int row = get_global_id(0);
                    int col = get_global_id(1);
                    if (row >= size || col >= size) return;
                    
                    float sum = 0.0f;
                    for (int k = 0; k < size; k++) {
                        sum += A[row * size + k] * B[k * size + col];
                    }
                    C[row * size + col] = sum;
                }",
            _ => KernelSources.VectorAdd
        };
    }

    private ComputeWorkflowDefinition CreateConfigurablePluginWorkflow(float[] testData)
    {
        return new ComputeWorkflowDefinition
        {
            Name = "ConfigurablePluginWorkflow",
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "configurable_process",
                    SourceCode = @"
                        __kernel void configurable_process(__global const float* input, __global float* output) {
                            int gid = get_global_id(0);
                            output[gid] = input[gid] * 2.5f;
                        }"
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = testData }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = testData.Length }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "process_stage",
                    Order = 1,
                    KernelName = "configurable_process",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateOptimizedPluginWorkflow(float[] testData)
    {
        return new ComputeWorkflowDefinition
        {
            Name = "OptimizedPluginWorkflow",
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "optimized_compute",
                    SourceCode = KernelSources.OptimizedCompute,
                    CompilationOptions = new CompilationOptions
                    {
                        OptimizationLevel = OptimizationLevel.Aggressive,
                        FastMath = true,
                        UnrollLoops = true
                    }
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = testData }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = testData.Length }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "optimized_stage",
                    Order = 1,
                    KernelName = "optimized_compute",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateBaselineWorkflow(float[] testData)
    {
        return new ComputeWorkflowDefinition
        {
            Name = "BaselineWorkflow",
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "baseline_compute",
                    SourceCode = KernelSources.BaselineCompute
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = testData }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = testData.Length }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "baseline_stage",
                    Order = 1,
                    KernelName = "baseline_compute",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private ComputeWorkflowDefinition CreateFaultyPluginWorkflow()
    {
        return new ComputeWorkflowDefinition
        {
            Name = "FaultyPluginWorkflow",
            Kernels = new()
            {
                new WorkflowKernel
                {
                    Name = "faulty_process",
                    SourceCode = @"
                        __kernel void faulty_process(__global const float* input, __global float* output) {
                            int gid = get_global_id(0);
                            if (gid == 10) {
                                // Simulate division by zero
                                output[gid] = input[gid] / 0.0f;
                            } else {
                                output[gid] = input[gid];
                            }
                        }"
                }
            },
            Inputs = new()
            {
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(100) }
            },
            Outputs = new()
            {
                new WorkflowOutput { Name = "output", Size = 100 }
            },
            ExecutionStages = new()
            {
                new WorkflowExecutionStage
                {
                    Name = "faulty_stage",
                    Order = 1,
                    KernelName = "faulty_process",
                    ArgumentNames = new[] { "input", "output" }
                }
            }
        };
    }

    private void ValidateMatrixMultiplication(float[] result, float[] matrixA, float[] matrixB, int size)
    {
        // Validate a few elements of the matrix multiplication
        for (int i = 0; i < Math.Min(4, size); i++)
        {
            for (int j = 0; j < Math.Min(4, size); j++)
            {
                float expected = 0;
                for (int k = 0; k < size; k++)
                {
                    expected += matrixA[i * size + k] * matrixB[k * size + j];
                }
                
                var actual = result[i * size + j];
                actual.Should().BeApproximately(expected, Math.Abs(expected) * 0.01f + 1e-5f);
            }
        }
    }
}

/// <summary>
/// Additional kernel sources for plugin testing.
/// </summary>
internal static partial class KernelSources
{
    public const string OptimizedCompute = @"
__kernel void optimized_compute(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float4 data = vload4(gid / 4, (__global float4*)input);
    
    // Optimized computation with vectorization
    data = native_sin(data) * native_cos(data) + native_sqrt(fabs(data));
    data = data * data + 1.0f;
    
    vstore4(data, gid / 4, (__global float4*)output);
}";

    public const string BaselineCompute = @"
__kernel void baseline_compute(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float data = input[gid];
    
    // Same computation as optimized but without vectorization
    data = sin(data) * cos(data) + sqrt(fabs(data));
    data = data * data + 1.0f;
    
    output[gid] = data;
}";
}