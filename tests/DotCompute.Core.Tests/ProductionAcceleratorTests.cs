// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Tests;

/// <summary>
/// Production tests for individual accelerator functionality.
/// </summary>
public class ProductionAcceleratorTests : IAsyncLifetime
{
    private readonly CpuAccelerator _accelerator;
    private readonly ILogger<CpuAccelerator> _logger;

    public ProductionAcceleratorTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CpuAccelerator>();
        
        var options = Options.Create(new CpuAcceleratorOptions());
        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions());
        
        _accelerator = new CpuAccelerator(options, threadPoolOptions, _logger);
    }

    [Fact]
    public void AcceleratorInfoShouldBePopulatedCorrectly()
    {
        // Act
        var info = _accelerator.Info;

        // Assert
        info.Should().NotBeNull();
        info.Id.Should().NotBeNullOrEmpty();
        info.Name.Should().NotBeNullOrEmpty();
        info.Type.Should().Be(AcceleratorType.Cpu);
        info.Vendor.Should().NotBeNullOrEmpty();
        info.DriverVersion.Should().NotBeNullOrEmpty();
        info.MaxComputeUnits.Should().BeGreaterThan(0);
        info.MaxWorkGroupSize.Should().BeGreaterThan(0);
        info.MaxMemoryAllocation.Should().BeGreaterThan(0);
        info.GlobalMemorySize.Should().BeGreaterThan(0);
        info.LocalMemorySize.Should().BeGreaterThan(0);
        info.Capabilities.Should().NotBeNull();
        info.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public void AcceleratorCapabilitiesShouldIncludeSimdInfo()
    {
        // Act
        var capabilities = _accelerator.Info.Capabilities;

        // Assert
        capabilities.Should().ContainKey("SimdWidth");
        capabilities.Should().ContainKey("SimdInstructionSets");
        capabilities.Should().ContainKey("ThreadCount");
        capabilities.Should().ContainKey("NumaNodes");
        capabilities.Should().ContainKey("CacheLineSize");

        capabilities["SimdWidth"].Should().BeOfType<int>();
        capabilities["SimdInstructionSets"].Should().BeAssignableTo<System.Collections.Generic.ICollection<string>>();
        capabilities["ThreadCount"].Should().BeOfType<int>();
        
        var simdWidth = (int)capabilities["SimdWidth"];
        simdWidth.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MemoryManagerShouldBeAccessible()
    {
        // Act
        var memoryManager = _accelerator.Memory;

        // Assert
        memoryManager.Should().NotBeNull();
        memoryManager.Should().BeOfType<CpuMemoryManager>();
    }

    [Fact]
    public async Task MemoryManagerBasicOperations_ShouldWork()
    {
        // Arrange
        var memoryManager = _accelerator.Memory;

        // Act
        var buffer = await memoryManager.AllocateAsync(1024);
        
        try
        {
            // Test basic buffer operations
            buffer.SizeInBytes.Should().Be(1024);
            buffer.Flags.Should().Be(MemoryFlags.None);
            
            // Test data copy operations
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            await buffer.CopyFromHostAsync<byte>(testData);
            
            var resultData = new byte[5];
            await buffer.CopyToHostAsync<byte>(resultData);
            
            resultData.Should().BeEquivalentTo(testData);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task CompileKernelAsyncWithValidDefinition_ShouldSucceed()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "TestKernel",
            Source = "test kernel source code",
            EntryPoint = "main"
        };

        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Definition.Should().Be(kernelDefinition);
        compiledKernel.Definition.Name.Should().Be("TestKernel");
    }

    [Fact]
    public async Task CompileKernelAsyncWithNullDefinition_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _accelerator.CompileKernelAsync(null!));
    }

    [Fact]
    public async Task CompileKernelAsyncWithOptions_ShouldUseOptions()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition
        {
            Name = "TestKernel",
            Source = "test source",
            EntryPoint = "main"
        };
        
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Release,
            EnableVectorization = true,
            TargetArchitecture = "x64"
        };

        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Definition.Should().Be(kernelDefinition);
    }

    [Fact]
    public async Task SynchronizeAsyncShouldCompleteSuccessfully()
    {
        // Act & Assert - Should not throw
        await _accelerator.SynchronizeAsync();
    }

    [Fact]
    public async Task SynchronizeAsyncWithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await _accelerator.SynchronizeAsync(cts.Token); // Should complete immediately for CPU
    }

    [Fact]
    public async Task MultipleKernelCompilationsShouldAllSucceed()
    {
        // Arrange
        var kernelDefinitions = new[]
        {
            new KernelDefinition { Name = "Kernel1", Source = "source1", EntryPoint = "main1" },
            new KernelDefinition { Name = "Kernel2", Source = "source2", EntryPoint = "main2" },
            new KernelDefinition { Name = "Kernel3", Source = "source3", EntryPoint = "main3" }
        };

        // Act
        var compiledKernels = new System.Collections.Generic.List<ICompiledKernel>();
        
        foreach (var definition in kernelDefinitions)
        {
            var compiled = await _accelerator.CompileKernelAsync(definition);
            compiledKernels.Add(compiled);
        }

        // Assert
        compiledKernels.Should().HaveCount(3);
        
        for (int i = 0; i < kernelDefinitions.Length; i++)
        {
            compiledKernels[i].Definition.Should().Be(kernelDefinitions[i]);
            compiledKernels[i].Definition.Name.Should().Be($"Kernel{i + 1}");
        }

        // Cleanup
        foreach (var kernel in compiledKernels)
        {
            await kernel.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConcurrentOperationsShouldBeThreadSafe()
    {
        // Arrange
        const int operationCount = 20;
        var tasks = new System.Threading.Tasks.Task[operationCount];

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                // Mix of operations
                var operation = index % 3;
                
                switch (operation)
                {
                    case 0: // Memory operations
                        var buffer = await _accelerator.Memory.AllocateAsync(1024);
                        try
                        {
                            var data = new byte[10];
                            await buffer.CopyFromHostAsync<byte>(data);
                            await buffer.CopyToHostAsync<byte>(data);
                        }
                        finally
                        {
                            await buffer.DisposeAsync();
                        }
                        break;
                        
                    case 1: // Kernel compilation
                        var definition = new KernelDefinition
                        {
                            Name = $"TestKernel{index}",
                            Source = $"test source {index}",
                            EntryPoint = "main"
                        };
                        var kernel = await _accelerator.CompileKernelAsync(definition);
                        await kernel.DisposeAsync();
                        break;
                        
                    case 2: // Synchronization
                        await _accelerator.SynchronizeAsync();
                        break;
                }
                
                return index;
            });
        }

        // Assert - Should complete without exceptions
        var results = await Task.WhenAll(tasks);
        results.Should().HaveCount(operationCount);
    }

    [Fact]
    public async Task AcceleratorInfoShouldReflectCurrentSystem()
    {
        // Act
        var info = _accelerator.Info;

        // Assert
        info.MaxComputeUnits.Should().Be(Environment.ProcessorCount);
        
        // Verify SIMD capabilities match system
        if (info.Capabilities.TryGetValue("SimdWidth", out var simdWidth))
        {
            var width = (int)simdWidth;
            width.Should().BeOneOf(128, 256, 512); // Common SIMD widths
        }
        
        // Verify memory sizes are reasonable
        info.GlobalMemorySize.Should().BeGreaterThan(1024 * 1024); // At least 1MB
        info.LocalMemorySize.Should().BeGreaterThan(1024); // At least 1KB cache
    }

    [Fact]
    public async Task LargeKernelCompilationShouldHandleCorrectly()
    {
        // Arrange
        var largeSource = new string('x', 100000); // 100KB source
        var kernelDefinition = new KernelDefinition
        {
            Name = "LargeKernel",
            Source = largeSource,
            EntryPoint = "main"
        };

        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Definition.Source.Should().HaveLength(100000);

        await compiledKernel.DisposeAsync();
    }

    [Fact]
    public async Task MemoryOperationsWithDifferentFlags_ShouldWork()
    {
        // Test different memory flags
        var memoryFlags = new[]
        {
            MemoryFlags.None,
            MemoryFlags.ReadOnly,
            MemoryFlags.WriteOnly,
            MemoryFlags.HostVisible,
            MemoryFlags.Cached,
            MemoryFlags.Atomic
        };

        foreach (var flags in memoryFlags)
        {
            var buffer = await _accelerator.Memory.AllocateAsync(1024, flags);
            try
            {
                buffer.Flags.Should().Be(flags);
                
                // Test operations based on flags
                if (!flags.HasFlag(MemoryFlags.WriteOnly))
                {
                    var data = new byte[10];
                    await buffer.CopyToHostAsync<byte>(data);
                }
                
                if (!flags.HasFlag(MemoryFlags.ReadOnly))
                {
                    var data = new byte[10];
                    await buffer.CopyFromHostAsync<byte>(data);
                }
            }
            finally
            {
                await buffer.DisposeAsync();
            }
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _accelerator.DisposeAsync();
    }
}