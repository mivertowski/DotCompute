// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core;
using DotCompute.Integration.Tests.Fixtures;
using DotCompute.Plugins;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace DotCompute.Integration.Tests.Scenarios;

[Collection(nameof(IntegrationTestCollection))]
public class PluginSystemIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public PluginSystemIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PluginShould_Load_And_Execute_With_CPU_Backend()
    {
        // Arrange - Create a simple plugin assembly in memory
        var pluginCode = @"
using DotCompute.Core;
using DotCompute.Core.Plugins;

namespace TestPlugin;

[ComputePlugin(""TestPlugin"", ""1.0.0"")]
public class TestComputePlugin : IComputePlugin
{
    public string Name => ""TestPlugin"";
    public string Version => ""1.0.0"";
    public string Description => ""Test plugin for integration testing"";

    public async Task<bool> InitializeAsync(IComputeEngine engine)
    {
        // Register a simple kernel
        await engine.RegisterKernelAsync(""test_add"", @""
            kernel void test_add(global float* a, global float* b, global float* result, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    result[i] = a[i] + b[i];
                }
            }"");
        return true;
    }

    public Task<bool> ShutdownAsync()
    {
        return Task.FromResult(true);
    }
}";

        // Create temporary plugin file
        var tempPluginDir = Path.Combine(Path.GetTempPath(), "DotComputeTestPlugin");
        Directory.CreateDirectory(tempPluginDir);
        var pluginFile = Path.Combine(tempPluginDir, "TestPlugin.cs");
        await File.WriteAllTextAsync(pluginFile, pluginCode);

        try
        {
            // Act - Load plugin
            var loadResult = await _fixture.PluginManager.LoadPluginAsync(tempPluginDir);
            loadResult.Should().BeTrue("Plugin should load successfully");

            // Verify plugin is loaded
            var loadedPlugins = await _fixture.PluginManager.GetLoadedPluginsAsync();
            loadedPlugins.Should().ContainSingle(p => p.Name == "TestPlugin");

            // Test plugin functionality
            var testData1 = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
            var testData2 = new float[] { 5.0f, 6.0f, 7.0f, 8.0f };

            using var buffer1 = await _fixture.CreateBufferAsync(testData1);
            using var buffer2 = await _fixture.CreateBufferAsync(testData2);
            using var resultBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(testData1.Length);

            var kernel = await _fixture.ComputeEngine.GetKernelAsync("test_add");
            kernel.Should().NotBeNull("Registered kernel should be available");

            await _fixture.ComputeEngine.ExecuteAsync(kernel!, 
                new object[] { buffer1, buffer2, resultBuffer, testData1.Length }, 
                ComputeBackendType.CPU);

            var result = await resultBuffer.ReadAsync();

            // Assert
            result.Should().HaveCount(4);
            result[0].Should().BeApproximately(6.0f, 0.001f);
            result[1].Should().BeApproximately(8.0f, 0.001f);
            result[2].Should().BeApproximately(10.0f, 0.001f);
            result[3].Should().BeApproximately(12.0f, 0.001f);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempPluginDir))
            {
                Directory.Delete(tempPluginDir, true);
            }
        }
    }

    [Fact]
    public async Task PluginHot_Reload_Should_Work_With_Running_Kernels()
    {
        // Arrange - Create initial plugin
        var pluginDir = Path.Combine(Path.GetTempPath(), "DotComputeHotReloadTest");
        Directory.CreateDirectory(pluginDir);

        var initialPluginCode = @"
using DotCompute.Core;
using DotCompute.Core.Plugins;

namespace HotReloadPlugin;

[ComputePlugin(""HotReloadPlugin"", ""1.0.0"")]
public class HotReloadComputePlugin : IComputePlugin
{
    public string Name => ""HotReloadPlugin"";
    public string Version => ""1.0.0"";
    public string Description => ""Hot reload test plugin"";

    public async Task<bool> InitializeAsync(IComputeEngine engine)
    {
        await engine.RegisterKernelAsync(""multiply"", @""
            kernel void multiply(global float* input, global float* output, float factor, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = input[i] * factor;
                }
            }"");
        return true;
    }

    public Task<bool> ShutdownAsync() => Task.FromResult(true);
}";

        var pluginFile = Path.Combine(pluginDir, "HotReloadPlugin.cs");
        await File.WriteAllTextAsync(pluginFile, initialPluginCode);

        try
        {
            // Act - Load initial plugin
            await _fixture.PluginManager.LoadPluginAsync(pluginDir);

            // Test initial functionality
            var testData = new float[] { 1.0f, 2.0f, 3.0f };
            using var inputBuffer = await _fixture.CreateBufferAsync(testData);
            using var outputBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(testData.Length);

            var kernel = await _fixture.ComputeEngine.GetKernelAsync("multiply");
            await _fixture.ComputeEngine.ExecuteAsync(kernel!, 
                new object[] { inputBuffer, outputBuffer, 2.0f, testData.Length }, 
                ComputeBackendType.CPU);

            var initialResult = await outputBuffer.ReadAsync();

            // Simulate hot reload by updating plugin
            var updatedPluginCode = initialPluginCode.Replace("output[i] = input[i] * factor;", 
                "output[i] = input[i] * factor + 1.0f;"); // Add +1 to the operation

            await File.WriteAllTextAsync(pluginFile, updatedPluginCode);

            // Trigger reload
            await _fixture.PluginManager.ReloadPluginAsync("HotReloadPlugin");

            // Test updated functionality
            using var outputBuffer2 = await _fixture.MemoryManager.CreateBufferAsync<float>(testData.Length);
            var updatedKernel = await _fixture.ComputeEngine.GetKernelAsync("multiply");
            
            await _fixture.ComputeEngine.ExecuteAsync(updatedKernel!, 
                new object[] { inputBuffer, outputBuffer2, 2.0f, testData.Length }, 
                ComputeBackendType.CPU);

            var updatedResult = await outputBuffer2.ReadAsync();

            // Assert
            initialResult.Should().HaveCount(3);
            initialResult[0].Should().BeApproximately(2.0f, 0.001f);
            initialResult[1].Should().BeApproximately(4.0f, 0.001f);
            initialResult[2].Should().BeApproximately(6.0f, 0.001f);

            updatedResult.Should().HaveCount(3);
            updatedResult[0].Should().BeApproximately(3.0f, 0.001f); // 1*2+1
            updatedResult[1].Should().BeApproximately(5.0f, 0.001f); // 2*2+1
            updatedResult[2].Should().BeApproximately(7.0f, 0.001f); // 3*2+1
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
            }
        }
    }

    [Fact]
    public async Task MultiplePlugins_Should_Coexist_And_Share_Resources()
    {
        // Test that multiple plugins can be loaded and work together
        var pluginDirs = new List<string>();

        try
        {
            // Create Plugin A - Math operations
            var pluginADir = Path.Combine(Path.GetTempPath(), "MathPlugin");
            Directory.CreateDirectory(pluginADir);
            pluginDirs.Add(pluginADir);

            var mathPluginCode = @"
using DotCompute.Core;
using DotCompute.Core.Plugins;

namespace MathPlugin;

[ComputePlugin(""MathPlugin"", ""1.0.0"")]
public class MathComputePlugin : IComputePlugin
{
    public string Name => ""MathPlugin"";
    public string Version => ""1.0.0"";
    public string Description => ""Math operations plugin"";

    public async Task<bool> InitializeAsync(IComputeEngine engine)
    {
        await engine.RegisterKernelAsync(""square"", @""
            kernel void square(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = input[i] * input[i];
                }
            }"");
        return true;
    }

    public Task<bool> ShutdownAsync() => Task.FromResult(true);
}";

            await File.WriteAllTextAsync(Path.Combine(pluginADir, "MathPlugin.cs"), mathPluginCode);

            // Create Plugin B - Array operations
            var pluginBDir = Path.Combine(Path.GetTempPath(), "ArrayPlugin");
            Directory.CreateDirectory(pluginBDir);
            pluginDirs.Add(pluginBDir);

            var arrayPluginCode = @"
using DotCompute.Core;
using DotCompute.Core.Plugins;

namespace ArrayPlugin;

[ComputePlugin(""ArrayPlugin"", ""1.0.0"")]
public class ArrayComputePlugin : IComputePlugin
{
    public string Name => ""ArrayPlugin"";
    public string Version => ""1.0.0"";
    public string Description => ""Array operations plugin"";

    public async Task<bool> InitializeAsync(IComputeEngine engine)
    {
        await engine.RegisterKernelAsync(""reverse"", @""
            kernel void reverse(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = input[size - 1 - i];
                }
            }"");
        return true;
    }

    public Task<bool> ShutdownAsync() => Task.FromResult(true);
}";

            await File.WriteAllTextAsync(Path.Combine(pluginBDir, "ArrayPlugin.cs"), arrayPluginCode);

            // Load both plugins
            await _fixture.PluginManager.LoadPluginAsync(pluginADir);
            await _fixture.PluginManager.LoadPluginAsync(pluginBDir);

            // Verify both plugins are loaded
            var loadedPlugins = await _fixture.PluginManager.GetLoadedPluginsAsync();
            loadedPlugins.Should().Contain(p => p.Name == "MathPlugin");
            loadedPlugins.Should().Contain(p => p.Name == "ArrayPlugin");

            // Test chained operations using both plugins
            var testData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
            
            using var inputBuffer = await _fixture.CreateBufferAsync(testData);
            using var squaredBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(testData.Length);
            using var reversedBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(testData.Length);

            // Step 1: Square the numbers (MathPlugin)
            var squareKernel = await _fixture.ComputeEngine.GetKernelAsync("square");
            await _fixture.ComputeEngine.ExecuteAsync(squareKernel!, 
                new object[] { inputBuffer, squaredBuffer, testData.Length }, 
                ComputeBackendType.CPU);

            // Step 2: Reverse the array (ArrayPlugin)
            var reverseKernel = await _fixture.ComputeEngine.GetKernelAsync("reverse");
            await _fixture.ComputeEngine.ExecuteAsync(reverseKernel!, 
                new object[] { squaredBuffer, reversedBuffer, testData.Length }, 
                ComputeBackendType.CPU);

            var result = await reversedBuffer.ReadAsync();

            // Assert - Should be reversed squared values: [16, 9, 4, 1]
            result.Should().HaveCount(4);
            result[0].Should().BeApproximately(16.0f, 0.001f); // 4^2
            result[1].Should().BeApproximately(9.0f, 0.001f);  // 3^2
            result[2].Should().BeApproximately(4.0f, 0.001f);  // 2^2
            result[3].Should().BeApproximately(1.0f, 0.001f);  // 1^2
        }
        finally
        {
            // Cleanup
            foreach (var dir in pluginDirs)
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
    }
}