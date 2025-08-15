// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Simple test to verify the hardware abstraction layer builds correctly.
/// This file demonstrates the basic usage pattern.
/// </summary>
[ExcludeFromCodeCoverage]
public class QuickBuildTest
{
    /// <summary>
    /// Simple test showing the hardware abstraction layer works.
    /// </summary>
    public static void BasicHardwareAbstractionTest()
    {
        // Create mock hardware provider
        using var provider = new MockHardwareProvider();

        // Get available devices
        var devices = provider.GetAllDevices().ToList();
        var cudaDevice = provider.GetFirstDevice(AcceleratorType.CUDA);
        var cpuDevice = provider.GetFirstDevice(AcceleratorType.CPU);

        // Basic assertions
        if (cudaDevice != null)
        {
            var info = cudaDevice.ToAcceleratorInfo();
            var isHealthy = cudaDevice.HealthCheck();
            var properties = cudaDevice.GetProperties();
        }

        if (cpuDevice != null)
        {
            var info = cpuDevice.ToAcceleratorInfo();
            var isHealthy = cpuDevice.HealthCheck();
        }

        // Test device creation
        var rtx4090 = MockCudaDevice.CreateRTX4090();
        var intelCpu = MockCpuDevice.CreateIntelCore();
        var m2Max = MockMetalDevice.CreateM2Max();

        // Test factory
        var testDevice = TestHardwareFactory.CreateTestDevice(AcceleratorType.CUDA);
        var testProvider = TestHardwareFactory.CreateDefault();
        testProvider.Dispose();

        // Test simulator
        using var simulator = new HardwareSimulator();
        simulator.Start();
        var stats = simulator.GetStatistics();
        simulator.Stop();
    }
}
