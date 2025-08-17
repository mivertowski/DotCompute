// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Mock CPU device implementation for testing CPU-based compute scenarios.
/// Simulates various CPU configurations and SIMD capabilities.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MockCpuDevice : MockHardwareDevice
{
    /// <summary>
    /// Gets the number of physical CPU cores.
    /// </summary>
    public int PhysicalCores { get; }

    /// <summary>
    /// Gets the number of logical CPU cores (with hyperthreading).
    /// </summary>
    public int LogicalCores { get; }

    /// <summary>
    /// Gets the base clock frequency in MHz.
    /// </summary>
    public int BaseClockMHz { get; }

    /// <summary>
    /// Gets the maximum boost clock frequency in MHz.
    /// </summary>
    public int MaxBoostClockMHz { get; }

    /// <summary>
    /// Gets the L3 cache size in MB.
    /// </summary>
    public int L3CacheSizeMB { get; }

    /// <summary>
    /// Gets the supported SIMD instruction sets.
    /// </summary>
    public SimdCapabilities SimdCapabilities { get; }

    /// <summary>
    /// Gets the CPU architecture.
    /// </summary>
    public string Architecture { get; }

    /// <summary>
    /// Gets whether hyperthreading is supported.
    /// </summary>
    public bool SupportsHyperthreading => LogicalCores > PhysicalCores;

    private MockCpuDevice(
        string id,
        string name,
        string vendor,
        string architecture,
        int physicalCores,
        int logicalCores,
        int baseClockMHz,
        int maxBoostClockMHz,
        long memorySize,
        int l3CacheSizeMB,
        SimdCapabilities simdCapabilities,
        ILogger? logger = null)
        : base(id, name, AcceleratorType.CPU, vendor, "1.0", memorySize,
               Math.Min(logicalCores, 64), logicalCores, logger)
    {
        Architecture = architecture;
        PhysicalCores = physicalCores;
        LogicalCores = logicalCores;
        BaseClockMHz = baseClockMHz;
        MaxBoostClockMHz = maxBoostClockMHz;
        L3CacheSizeMB = l3CacheSizeMB;
        SimdCapabilities = simdCapabilities;

        // Add CPU-specific capabilities
        Capabilities["PhysicalCores"] = physicalCores;
        Capabilities["LogicalCores"] = logicalCores;
        Capabilities["BaseClockMHz"] = baseClockMHz;
        Capabilities["MaxBoostClockMHz"] = maxBoostClockMHz;
        Capabilities["L3CacheSizeMB"] = l3CacheSizeMB;
        Capabilities["Architecture"] = architecture;
        Capabilities["SupportsHyperthreading"] = SupportsHyperthreading;
        Capabilities["SimdCapabilities"] = simdCapabilities.ToString();
        Capabilities["SupportsAVX512"] = simdCapabilities.HasFlag(SimdCapabilities.AVX512);
        Capabilities["SupportsAVX2"] = simdCapabilities.HasFlag(SimdCapabilities.AVX2);
        Capabilities["SupportsSSE42"] = simdCapabilities.HasFlag(SimdCapabilities.SSE42);
    }

    /// <summary>
    /// Creates an Intel Core i9-13900K mock device.
    /// </summary>
    public static MockCpuDevice CreateIntelCore(ILogger? logger = null)
    {
        return new MockCpuDevice(
            "cpu_intel_core_0",
            "Intel Core i9-13900K",
            "Intel",
            "Raptor Lake",
            24, // 8 P-cores + 16 E-cores
            32, // 16 + 16 threads
            3000, // 3.0 GHz base
            5800, // 5.8 GHz boost
            32L * 1024 * 1024 * 1024, // 32GB typical system RAM
            36, // 36MB L3
            SimdCapabilities.SSE | SimdCapabilities.SSE2 | SimdCapabilities.SSE3 |
            SimdCapabilities.SSSE3 | SimdCapabilities.SSE41 | SimdCapabilities.SSE42 |
            SimdCapabilities.AVX | SimdCapabilities.AVX2 | SimdCapabilities.AVX512,
            logger);
    }

    /// <summary>
    /// Creates an Intel Core laptop CPU mock device.
    /// </summary>
    public static MockCpuDevice CreateIntelCoreLaptop(ILogger? logger = null)
    {
        return new MockCpuDevice(
            "cpu_intel_core_laptop_0",
            "Intel Core i7-1280P",
            "Intel",
            "Alder Lake",
            14, // 6 P-cores + 8 E-cores
            20, // 12 + 8 threads
            1800, // 1.8 GHz base
            4800, // 4.8 GHz boost
            16L * 1024 * 1024 * 1024, // 16GB typical laptop RAM
            18, // 18MB L3
            SimdCapabilities.SSE | SimdCapabilities.SSE2 | SimdCapabilities.SSE3 |
            SimdCapabilities.SSSE3 | SimdCapabilities.SSE41 | SimdCapabilities.SSE42 |
            SimdCapabilities.AVX | SimdCapabilities.AVX2,
            logger);
    }

    /// <summary>
    /// Creates an AMD Ryzen 9 7950X mock device.
    /// </summary>
    public static MockCpuDevice CreateAmdRyzen(ILogger? logger = null)
    {
        return new MockCpuDevice(
            "cpu_amd_ryzen_0",
            "AMD Ryzen 9 7950X",
            "AMD",
            "Zen 4",
            16, // 16 cores
            32, // 32 threads
            4500, // 4.5 GHz base
            5700, // 5.7 GHz boost
            32L * 1024 * 1024 * 1024, // 32GB typical system RAM
            64, // 64MB L3
            SimdCapabilities.SSE | SimdCapabilities.SSE2 | SimdCapabilities.SSE3 |
            SimdCapabilities.SSSE3 | SimdCapabilities.SSE41 | SimdCapabilities.SSE42 |
            SimdCapabilities.AVX | SimdCapabilities.AVX2,
            logger);
    }

    /// <summary>
    /// Creates an AMD EPYC server CPU mock device.
    /// </summary>
    public static MockCpuDevice CreateEpycServer(ILogger? logger = null)
    {
        return new MockCpuDevice(
            "cpu_amd_epyc_0",
            "AMD EPYC 7713",
            "AMD",
            "Zen 3",
            64, // 64 cores
            128, // 128 threads
            2000, // 2.0 GHz base
            3675, // 3.675 GHz boost
            128L * 1024 * 1024 * 1024, // 128GB typical server RAM
            256, // 256MB L3
            SimdCapabilities.SSE | SimdCapabilities.SSE2 | SimdCapabilities.SSE3 |
            SimdCapabilities.SSSE3 | SimdCapabilities.SSE41 | SimdCapabilities.SSE42 |
            SimdCapabilities.AVX | SimdCapabilities.AVX2,
            logger);
    }

    /// <summary>
    /// Creates an Intel Xeon server CPU mock device.
    /// </summary>
    public static MockCpuDevice CreateXeonServer(ILogger? logger = null)
    {
        return new MockCpuDevice(
            "cpu_intel_xeon_0",
            "Intel Xeon Platinum 8380",
            "Intel",
            "Ice Lake",
            40, // 40 cores
            80, // 80 threads
            2300, // 2.3 GHz base
            3400, // 3.4 GHz boost
            256L * 1024 * 1024 * 1024, // 256GB typical server RAM
            60, // 60MB L3
            SimdCapabilities.SSE | SimdCapabilities.SSE2 | SimdCapabilities.SSE3 |
            SimdCapabilities.SSSE3 | SimdCapabilities.SSE41 | SimdCapabilities.SSE42 |
            SimdCapabilities.AVX | SimdCapabilities.AVX2 | SimdCapabilities.AVX512,
            logger);
    }

    /// <summary>
    /// Creates a basic CPU mock device for minimal testing.
    /// </summary>
    public static MockCpuDevice CreateBasic(ILogger? logger = null)
    {
        return new MockCpuDevice(
            "cpu_basic_0",
            "Basic CPU",
            "Generic",
            "x86_64",
            4, // 4 cores
            8, // 8 threads
            2400, // 2.4 GHz base
            3200, // 3.2 GHz boost
            8L * 1024 * 1024 * 1024, // 8GB RAM
            8, // 8MB L3
            SimdCapabilities.SSE | SimdCapabilities.SSE2,
            logger);
    }

    /// <inheritdoc/>
    public override Dictionary<string, object> GetProperties()
    {
        var properties = base.GetProperties();

        // Add CPU-specific properties
        properties["CPU_PhysicalCores"] = PhysicalCores;
        properties["CPU_LogicalCores"] = LogicalCores;
        properties["CPU_BaseClockMHz"] = BaseClockMHz;
        properties["CPU_MaxBoostClockMHz"] = MaxBoostClockMHz;
        properties["CPU_L3CacheSizeMB"] = L3CacheSizeMB;
        properties["CPU_Architecture"] = Architecture;
        properties["CPU_SupportsHyperthreading"] = SupportsHyperthreading;
        properties["CPU_SimdCapabilities"] = SimdCapabilities.ToString();

        return properties;
    }

    /// <inheritdoc/>
    public override bool HealthCheck()
    {
        if (!base.HealthCheck())
            return false;

        // CPU-specific health checks
        if (PhysicalCores == 0 || LogicalCores == 0)
        {
            Logger?.LogWarning("CPU device {DeviceId} has invalid core configuration", Id);
            return false;
        }

        if (LogicalCores < PhysicalCores)
        {
            Logger?.LogWarning("CPU device {DeviceId} has illogical core configuration", Id);
            return false;
        }

        if (BaseClockMHz == 0 || MaxBoostClockMHz < BaseClockMHz)
        {
            Logger?.LogWarning("CPU device {DeviceId} has invalid clock configuration", Id);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Simulates CPU-specific issues like thermal throttling.
    /// </summary>
    /// <param name="issue">The type of CPU issue to simulate.</param>
    public void SimulateCpuIssue(CpuIssueType issue)
    {
        var errorMessage = issue switch
        {
            CpuIssueType.ThermalThrottling => "CPU thermal throttling detected",
            CpuIssueType.HighLoad => "CPU at maximum load capacity",
            CpuIssueType.CacheError => "CPU cache error detected",
            CpuIssueType.MemoryError => "System memory error",
            _ => "Unknown CPU issue"
        };

        SimulateFailure(errorMessage);
    }

    /// <summary>
    /// Gets the theoretical peak performance in GFLOPS for the given precision.
    /// </summary>
    /// <param name="precision">The floating-point precision.</param>
    /// <returns>Peak GFLOPS estimation.</returns>
    public double GetPeakGflops(FloatingPointPrecision precision)
    {
        var vectorWidth = precision switch
        {
            FloatingPointPrecision.SinglePrecision when SimdCapabilities.HasFlag(SimdCapabilities.AVX512) => 16,
            FloatingPointPrecision.SinglePrecision when SimdCapabilities.HasFlag(SimdCapabilities.AVX2) => 8,
            FloatingPointPrecision.SinglePrecision when SimdCapabilities.HasFlag(SimdCapabilities.AVX) => 8,
            FloatingPointPrecision.SinglePrecision when SimdCapabilities.HasFlag(SimdCapabilities.SSE) => 4,
            FloatingPointPrecision.DoublePrecision when SimdCapabilities.HasFlag(SimdCapabilities.AVX512) => 8,
            FloatingPointPrecision.DoublePrecision when SimdCapabilities.HasFlag(SimdCapabilities.AVX2) => 4,
            FloatingPointPrecision.DoublePrecision when SimdCapabilities.HasFlag(SimdCapabilities.AVX) => 4,
            FloatingPointPrecision.DoublePrecision when SimdCapabilities.HasFlag(SimdCapabilities.SSE2) => 2,
            _ => 1
        };

        // Assume 2 FMA units per core (typical for modern CPUs)
        var fmaUnitsPerCore = 2;
        var opsPerCycle = vectorWidth * fmaUnitsPerCore * 2; // FMA = multiply + add

        return (LogicalCores * opsPerCycle * MaxBoostClockMHz) / 1000.0; // Convert MHz to GHz
    }
}

/// <summary>
/// SIMD instruction set capabilities.
/// </summary>
[Flags]
public enum SimdCapabilities
{
    None = 0,
    SSE = 1 << 0,
    SSE2 = 1 << 1,
    SSE3 = 1 << 2,
    SSSE3 = 1 << 3,
    SSE41 = 1 << 4,
    SSE42 = 1 << 5,
    AVX = 1 << 6,
    AVX2 = 1 << 7,
    AVX512 = 1 << 8,
    FMA3 = 1 << 9,
    BMI1 = 1 << 10,
    BMI2 = 1 << 11
}

/// <summary>
/// Types of CPU issues that can be simulated.
/// </summary>
public enum CpuIssueType
{
    ThermalThrottling,
    HighLoad,
    CacheError,
    MemoryError
}

/// <summary>
/// Floating-point precision levels.
/// </summary>
public enum FloatingPointPrecision
{
    Half,
    SinglePrecision,
    DoublePrecision
}
