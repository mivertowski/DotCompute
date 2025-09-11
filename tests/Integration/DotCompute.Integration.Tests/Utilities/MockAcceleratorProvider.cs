// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using Moq;

namespace DotCompute.Integration.Tests.Utilities;

/// <summary>
/// Provides mock accelerators for testing without requiring real hardware.
/// </summary>
public class MockAcceleratorProvider
{
    public Mock<IAccelerator> CpuAccelerator { get; }
    public Mock<IAccelerator> GpuAccelerator { get; }
    public Mock<IUnifiedMemoryManager> MemoryManager { get; }
    public Mock<ICompiledKernel> CompiledKernel { get; }

    public MockAcceleratorProvider()
    {
        CpuAccelerator = CreateCpuAcceleratorMock();
        GpuAccelerator = CreateGpuAcceleratorMock();
        MemoryManager = CreateMemoryManagerMock();
        CompiledKernel = CreateCompiledKernelMock();
    }

    private Mock<IAccelerator> CreateCpuAcceleratorMock()
    {
        var mock = new Mock<IAccelerator>();
        
        var cpuInfo = new AcceleratorInfo
        {
            Id = "cpu_mock",
            Name = "Mock CPU",
            DeviceType = "CPU",
            Vendor = "Mock",
            DriverVersion = "1.0.0",
            TotalMemory = 16L * 1024 * 1024 * 1024, // 16 GB
            AvailableMemory = 12L * 1024 * 1024 * 1024, // 12 GB
            MaxSharedMemoryPerBlock = 64 * 1024, // 64 KB
            MaxMemoryAllocationSize = 8L * 1024 * 1024 * 1024, // 8 GB
            LocalMemorySize = 32 * 1024, // 32 KB
            IsUnifiedMemory = true,
            MaxThreadsPerBlock = Environment.ProcessorCount,
            MaxComputeUnits = Environment.ProcessorCount,
            GlobalMemorySize = 16L * 1024 * 1024 * 1024,
            SupportsFloat64 = true,
            SupportsInt64 = true
        };

        mock.Setup(a => a.Info).Returns(cpuInfo);
        mock.Setup(a => a.Type).Returns(AcceleratorType.CPU);
        mock.Setup(a => a.Memory).Returns(MemoryManager.Object);
        mock.Setup(a => a.Context).Returns(new AcceleratorContext());

        mock.Setup(a => a.CompileKernelAsync(
                It.IsAny<KernelDefinition>(),
                It.IsAny<CompilationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompiledKernel.Object);

        mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return mock;
    }

    private Mock<IAccelerator> CreateGpuAcceleratorMock()
    {
        var mock = new Mock<IAccelerator>();
        
        var gpuInfo = new AcceleratorInfo
        {
            Id = "gpu_mock",
            Name = "Mock GPU",
            DeviceType = "GPU",
            Vendor = "NVIDIA",
            DriverVersion = "535.171.04",
            TotalMemory = 24L * 1024 * 1024 * 1024, // 24 GB
            AvailableMemory = 20L * 1024 * 1024 * 1024, // 20 GB
            MaxSharedMemoryPerBlock = 48 * 1024, // 48 KB
            MaxMemoryAllocationSize = 12L * 1024 * 1024 * 1024, // 12 GB
            LocalMemorySize = 48 * 1024, // 48 KB
            IsUnifiedMemory = false,
            MaxThreadsPerBlock = 1024,
            ComputeCapability = new Version(8, 9), // RTX 2000 Ada
            MaxComputeUnits = 102, // Streaming multiprocessors
            GlobalMemorySize = 24L * 1024 * 1024 * 1024,
            SupportsFloat64 = true,
            SupportsInt64 = true
        };

        mock.Setup(a => a.Info).Returns(gpuInfo);
        mock.Setup(a => a.Type).Returns(AcceleratorType.GPU);
        mock.Setup(a => a.Memory).Returns(MemoryManager.Object);
        mock.Setup(a => a.Context).Returns(new AcceleratorContext());

        mock.Setup(a => a.CompileKernelAsync(
                It.IsAny<KernelDefinition>(),
                It.IsAny<CompilationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompiledKernel.Object);

        mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return mock;
    }

    private Mock<IUnifiedMemoryManager> CreateMemoryManagerMock()
    {
        var mock = new Mock<IUnifiedMemoryManager>();
        
        mock.Setup(m => m.AllocateAsync<It.IsAnyType>(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, CancellationToken>((size, ct) =>
            {
                var buffer = new Mock<IUnifiedMemoryBuffer<It.IsAnyType>>();
                return ValueTask.FromResult(buffer.Object);
            });

        mock.Setup(m => m.GetTotalMemoryUsage())
            .Returns(0L);

        mock.Setup(m => m.GetAvailableMemory())
            .Returns(8L * 1024 * 1024 * 1024); // 8 GB

        return mock;
    }

    private Mock<ICompiledKernel> CreateCompiledKernelMock()
    {
        var mock = new Mock<ICompiledKernel>();
        
        mock.Setup(k => k.Id).Returns(Guid.NewGuid());
        mock.Setup(k => k.Name).Returns("MockKernel");
        
        mock.Setup(k => k.ExecuteAsync(
                It.IsAny<KernelArguments>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return mock;
    }

    /// <summary>
    /// Creates a collection of mock accelerators for testing.
    /// </summary>
    public List<IAccelerator> GetMockAccelerators()
    {
        return [CpuAccelerator.Object, GpuAccelerator.Object];
    }

    /// <summary>
    /// Configures the GPU accelerator to simulate compilation failures.
    /// </summary>
    public void SimulateGpuCompilationFailure()
    {
        GpuAccelerator.Setup(a => a.CompileKernelAsync(
                It.IsAny<KernelDefinition>(),
                It.IsAny<CompilationOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GPU compilation failed"));
    }

    /// <summary>
    /// Configures the accelerator to simulate memory allocation failures.
    /// </summary>
    public void SimulateMemoryAllocationFailure()
    {
        MemoryManager.Setup(m => m.AllocateAsync<It.IsAnyType>(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutOfMemoryException("Insufficient memory"));
    }

    /// <summary>
    /// Configures the accelerator to simulate slow operations.
    /// </summary>
    public void SimulateSlowOperations(TimeSpan delay)
    {
        CompiledKernel.Setup(k => k.ExecuteAsync(
                It.IsAny<KernelArguments>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (KernelArguments args, CancellationToken ct) =>
            {
                await Task.Delay(delay, ct);
            });
    }
}