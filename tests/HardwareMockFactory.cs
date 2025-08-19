// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Types;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Tests
{

/// <summary>
/// Factory for creating hardware mocks that work in CI/CD environments.
/// </summary>
public static class HardwareMockFactory
{
    /// <summary>
    /// Creates a mock CUDA accelerator for testing without hardware.
    /// </summary>
    public static Mock<IAccelerator> CreateMockCudaAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "cuda_device_0",
            Name = "Mock CUDA Device",
            DeviceType = AcceleratorType.CUDA.ToString(),
            Vendor = "NVIDIA Corporation",
            DriverVersion = "12.0",
            TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
            AvailableMemory = 7L * 1024 * 1024 * 1024,
            LocalMemorySize = 48 * 1024, // 48KB shared memory
            MaxSharedMemoryPerBlock = 48 * 1024,
            MaxMemoryAllocationSize = 2L * 1024 * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 80,
            MaxThreadsPerBlock = 1024
        });

        mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return mock;
    }

    /// <summary>
    /// Creates a mock OpenCL accelerator for testing without hardware.
    /// </summary>
    public static Mock<IAccelerator> CreateMockOpenCLAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "opencl_device_0",
            Name = "Mock OpenCL Device",
            DeviceType = AcceleratorType.OpenCL.ToString(),
            Vendor = "Mock Vendor",
            DriverVersion = "3.0",
            TotalMemory = 4L * 1024 * 1024 * 1024, // 4GB
            AvailableMemory = 3L * 1024 * 1024 * 1024,
            LocalMemorySize = 32 * 1024, // 32KB local memory
            MaxSharedMemoryPerBlock = 32 * 1024,
            MaxMemoryAllocationSize = 1L * 1024 * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 32,
            MaxThreadsPerBlock = 256
        });

        mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return mock;
    }

    /// <summary>
    /// Creates a mock DirectCompute accelerator for testing without hardware.
    /// </summary>
    public static Mock<IAccelerator> CreateMockDirectComputeAccelerator()
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "directcompute_device_0",
            Name = "Mock DirectCompute Device",
            DeviceType = "DirectCompute",
            Vendor = "Mock GPU Vendor",
            DriverVersion = "DirectX 11.4",
            TotalMemory = 6L * 1024 * 1024 * 1024, // 6GB VRAM
            AvailableMemory = 5L * 1024 * 1024 * 1024,
            LocalMemorySize = 32 * 1024, // 32KB group shared memory
            MaxSharedMemoryPerBlock = 32 * 1024,
            MaxMemoryAllocationSize = 1L * 1024 * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 2048,
            MaxThreadsPerBlock = 1024
        });

        mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return mock;
    }

    /// <summary>
    /// Creates a mock accelerator of any specified type.
    /// </summary>
    public static Mock<IAccelerator> CreateMockAcceleratorOfType(string deviceType)
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "device_0",
            Name = "Mock Device",
            DeviceType = deviceType,
            Vendor = "Mock Vendor",
            DriverVersion = "1.0",
            TotalMemory = 1L * 1024 * 1024 * 1024,
            AvailableMemory = 800L * 1024 * 1024,
            LocalMemorySize = 16 * 1024,
            MaxSharedMemoryPerBlock = 16 * 1024,
            MaxMemoryAllocationSize = 512L * 1024 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 16,
            MaxThreadsPerBlock = 256
        });

        return mock;
    }
}

/// <summary>
/// Configuration for CI/CD test environments.
/// </summary>
public static class CIConfiguration
{
    /// <summary>
    /// Gets whether we're running in a CI environment.
    /// </summary>
    public static bool IsCI => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                              !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                              !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_DEVOPS"));

    /// <summary>
    /// Gets the test filter for CI environments.
    /// </summary>
    public static string CITestFilter => TestFilters.CI;

    /// <summary>
    /// Skips test if hardware is required and not available.
    /// </summary>
    public static void SkipIfHardwareRequired()
    {
        if (IsCI)
        {
            throw new Xunit.SkipException("Hardware-dependent test skipped in CI environment");
        }
    }
}

/// <summary>
/// Test execution filters for different scenarios.
/// </summary>
public static class TestFilters
{
    /// <summary>
    /// Filter for CI/CD builds - excludes hardware-dependent and long-running tests.
    /// Usage: dotnet test --filter "Category!=RequiresHardware&Category!=LongRunning&Category!=Performance&Category!=Stress"
    /// </summary>
    public const string CI = "Category!=HardwareRequired&Category!=CudaRequired&Category!=OpenCLRequired&Category!=DirectComputeRequired&Category!=RTX2000&Category!=GPU&Category!=LongRunning&Category!=Performance&Category!=Stress&Category!=Manual&Category!=Flaky";

    /// <summary>
    /// Filter for unit tests only.
    /// Usage: dotnet test --filter "Category=Unit"
    /// </summary>
    public const string UnitOnly = "Category=Unit";

    /// <summary>
    /// Filter for integration tests only.
    /// Usage: dotnet test --filter "Category=Integration"
    /// </summary>
    public const string IntegrationOnly = "Category=Integration";

    /// <summary>
    /// Filter for hardware tests only (requires real hardware).
    /// Usage: dotnet test --filter "Category=Hardware|Category=HardwareRequired|Category=CudaRequired|Category=OpenCLRequired|Category=DirectComputeRequired|Category=RTX2000|Category=GPU"
    /// </summary>
    public const string HardwareOnly = "Category=Hardware|Category=HardwareRequired|Category=CudaRequired|Category=OpenCLRequired|Category=DirectComputeRequired|Category=RTX2000|Category=GPU";

    /// <summary>
    /// Filter for performance tests only.
    /// Usage: dotnet test --filter "Category=Performance"
    /// </summary>
    public const string PerformanceOnly = "Category=Performance";

    /// <summary>
    /// Filter for quick tests (unit + fast integration tests).
    /// Usage: dotnet test --filter "Category=Unit|Category=Integration&Category!=LongRunning&Category!=Performance"
    /// </summary>
    public const string Quick = "Category=Unit|(Category=Integration&Category!=LongRunning&Category!=Performance)";
}}
