using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Registration;
using DotCompute.Backends.Metal.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotCompute.Backends.Metal.Tests;

public class MetalAcceleratorTests : IClassFixture<MetalTestFixture>
{
    private readonly MetalTestFixture _fixture;

    public MetalAcceleratorTests(MetalTestFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public void CanCreateMetalAccelerator()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        
        accelerator.Should().NotBeNull();
        accelerator.Info.Should().NotBeNull();
        accelerator.Info.DeviceType.Should().ContainAny("GPU", "IntegratedGPU", "DiscreteGPU", "ExternalGPU");
        accelerator.Info.Vendor.Should().Be("Apple");
    }

    [SkippableFact]
    public void CanAllocateMemory()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        
        const int size = 1024 * sizeof(float);
        var memory = accelerator.Memory.Allocate(size);

        memory.Should().NotBeNull();
        memory.SizeInBytes.Should().Be(size);
        memory.Handle.Should().NotBe(IntPtr.Zero);

        accelerator.Memory.Free(memory);
    }

    [SkippableFact]
    public async Task CanCompileKernel()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        
        var kernelDefinition = new KernelDefinition
        {
            Name = "testKernel",
            Code = "VectorAdd",
            Parameters = new[]
            {
                new KernelParameter { Name = "a", DataType = DataType.Float32, MemoryKind = MemoryKind.Global },
                new KernelParameter { Name = "b", DataType = DataType.Float32, MemoryKind = MemoryKind.Global },
                new KernelParameter { Name = "result", DataType = DataType.Float32, MemoryKind = MemoryKind.Global },
                new KernelParameter { Name = "size", DataType = DataType.UInt32, MemoryKind = MemoryKind.Constant }
            }
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDefinition);

        kernel.Should().NotBeNull();
        kernel.Name.Should().Be("testKernel");
        kernel.Definition.Should().Be(kernelDefinition);
    }

    [SkippableFact]
    public async Task CanExecuteVectorAddKernel()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        const int size = 1024;
        
        // Prepare test data
        var a = new float[size];
        var b = new float[size];
        var expected = new float[size];
        
        for (int i = 0; i < size; i++)
        {
            a[i] = i;
            b[i] = i * 2;
            expected[i] = a[i] + b[i];
        }

        // Allocate GPU memory
        var bufferA = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferB = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferResult = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            // Copy data to GPU
            accelerator.Memory.CopyToDevice(bufferA, MemoryMarshal.AsBytes(a.AsSpan()));
            accelerator.Memory.CopyToDevice(bufferB, MemoryMarshal.AsBytes(b.AsSpan()));

            // Compile kernel
            var kernel = await accelerator.CompileKernelAsync(Kernels.MetalOptimizedKernels.CreateVectorAddKernel());

            // Execute kernel
            await kernel.ExecuteAsync(new KernelExecutionContext
            {
                GlobalWorkSize = new WorkSize(size, 1, 1),
                Arguments = new[]
                {
                    new KernelArgument("a", bufferA),
                    new KernelArgument("b", bufferB),
                    new KernelArgument("result", bufferResult),
                    new KernelArgument("size", (uint)size)
                }
            });

            // Synchronize
            await accelerator.SynchronizeAsync();

            // Copy result back
            var result = new float[size];
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), bufferResult);

            // Verify
            result.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        }
        finally
        {
            accelerator.Memory.Free(bufferA);
            accelerator.Memory.Free(bufferB);
            accelerator.Memory.Free(bufferResult);
        }
    }

    [SkippableFact]
    public void CanGetDeviceInformation()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var devices = MetalUtilities.GetAllDevices();
        
        devices.Should().NotBeEmpty();
        
        foreach (var device in devices)
        {
            device.Name.Should().NotBeNullOrWhiteSpace();
            device.MaxThreadgroupSize.Should().BeGreaterThan(0);
            device.MaxBufferLength.Should().BeGreaterThan(0);
            
            // Log device info
            _fixture.Logger.LogInformation("Metal Device: {Device}", device.ToString());
        }
    }
}

public class MetalTestFixture : IDisposable
{
    public ServiceProvider ServiceProvider { get; }
    public ILogger<MetalAcceleratorTests> Logger { get; }

    public MetalTestFixture()
    {
        var services = new ServiceCollection();
        
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && MetalUtilities.IsMetalAvailable())
        {
            services.AddMetalBackend(options =>
            {
                options.MaxMemoryAllocation = 1024 * 1024 * 1024; // 1GB for tests
                options.EnableMetalPerformanceShaders = true;
            });
        }

        ServiceProvider = services.BuildServiceProvider();
        Logger = ServiceProvider.GetRequiredService<ILogger<MetalAcceleratorTests>>();
    }

    public void Dispose()
    {
        ServiceProvider?.Dispose();
    }
}

// Helper attribute to skip tests when conditions aren't met
public static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if (!condition)
        {
            throw new SkipException(reason);
        }
    }
}

public class SkipException : Exception
{
    public SkipException(string reason) : base(reason) { }
}