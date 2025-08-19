// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Common.Hardware
{

/// <summary>
/// Mock CUDA device implementation for testing without actual NVIDIA hardware.
/// Simulates various NVIDIA GPU configurations and behaviors.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MockCudaDevice : MockHardwareDevice
{
    /// <summary>
    /// Gets the compute capability version.
    /// </summary>
    public Version ComputeCapability { get; }

    /// <summary>
    /// Gets the number of streaming multiprocessors.
    /// </summary>
    public int StreamingMultiprocessors { get; }

    /// <summary>
    /// Gets the CUDA cores count estimation.
    /// </summary>
    public int CudaCores { get; }

    /// <summary>
    /// Gets the memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidth { get; }

    /// <summary>
    /// Gets the memory bus width in bits.
    /// </summary>
    public int MemoryBusWidth { get; }

    /// <summary>
    /// Gets the base clock speed in MHz.
    /// </summary>
    public int BaseClock { get; }

    /// <summary>
    /// Gets the boost clock speed in MHz.
    /// </summary>
    public int BoostClock { get; }

    private MockCudaDevice(
        string id,
        string name,
        long totalMemory,
        Version computeCapability,
        int streamingMultiprocessors,
        int cudaCores,
        double memoryBandwidth,
        int memoryBusWidth,
        int baseClock,
        int boostClock,
        ILogger? logger = null)
        : base(id, name, AcceleratorType.CUDA, "NVIDIA", "12.3", totalMemory,
               Math.Min(1024, cudaCores / streamingMultiprocessors), streamingMultiprocessors, logger)
    {
        ComputeCapability = computeCapability;
        StreamingMultiprocessors = streamingMultiprocessors;
        CudaCores = cudaCores;
        MemoryBandwidth = memoryBandwidth;
        MemoryBusWidth = memoryBusWidth;
        BaseClock = baseClock;
        BoostClock = boostClock;

        // Add CUDA-specific capabilities
        Capabilities["ComputeCapability"] = computeCapability.ToString();
        Capabilities["StreamingMultiprocessors"] = streamingMultiprocessors;
        Capabilities["CudaCores"] = cudaCores;
        Capabilities["MemoryBandwidth"] = memoryBandwidth;
        Capabilities["MemoryBusWidth"] = memoryBusWidth;
        Capabilities["BaseClock"] = baseClock;
        Capabilities["BoostClock"] = boostClock;
        Capabilities["SupportsTensorCores"] = computeCapability.Major >= 7;
        Capabilities["SupportsRTCores"] = name.Contains("RTX");
        Capabilities["SupportsDLSS"] = name.Contains("RTX") && computeCapability.Major >= 7;
        Capabilities["Architecture"] = GetArchitectureName(computeCapability);
    }

    /// <summary>
    /// Creates an RTX 4090 mock device.
    /// </summary>
    public static MockCudaDevice CreateRTX4090(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_rtx4090_0",
            "NVIDIA GeForce RTX 4090",
            24L * 1024 * 1024 * 1024, // 24GB GDDR6X
            new Version(8, 9), // Ada Lovelace
            128, // SMs
            16384, // CUDA cores
            1008.0, // GB/s
            384, // bit bus
            2230, // Base MHz
            2520, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates an RTX 4080 mock device.
    /// </summary>
    public static MockCudaDevice CreateRTX4080(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_rtx4080_0",
            "NVIDIA GeForce RTX 4080",
            16L * 1024 * 1024 * 1024, // 16GB GDDR6X
            new Version(8, 9), // Ada Lovelace
            76, // SMs
            9728, // CUDA cores
            717.0, // GB/s
            256, // bit bus
            2205, // Base MHz
            2505, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates an RTX 4070 mock device.
    /// </summary>
    public static MockCudaDevice CreateRTX4070(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_rtx4070_0",
            "NVIDIA GeForce RTX 4070",
            12L * 1024 * 1024 * 1024, // 12GB GDDR6X
            new Version(8, 9), // Ada Lovelace
            60, // SMs
            5888, // CUDA cores
            504.0, // GB/s
            192, // bit bus
            1920, // Base MHz
            2475, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates an RTX 3080 mock device.
    /// </summary>
    public static MockCudaDevice CreateRTX3080(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_rtx3080_0",
            "NVIDIA GeForce RTX 3080",
            10L * 1024 * 1024 * 1024, // 10GB GDDR6X
            new Version(8, 6), // Ampere
            68, // SMs
            8704, // CUDA cores
            760.0, // GB/s
            320, // bit bus
            1440, // Base MHz
            1710, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates an RTX 3060 Laptop mock device.
    /// </summary>
    public static MockCudaDevice CreateRTX3060Laptop(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_rtx3060_laptop_0",
            "NVIDIA GeForce RTX 3060 Laptop GPU",
            6L * 1024 * 1024 * 1024, // 6GB GDDR6
            new Version(8, 6), // Ampere
            30, // SMs
            3840, // CUDA cores
            336.0, // GB/s
            192, // bit bus
            900, // Base MHz
            1425, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates an A100 data center mock device.
    /// </summary>
    public static MockCudaDevice CreateA100(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_a100_0",
            "NVIDIA A100-SXM4-40GB",
            40L * 1024 * 1024 * 1024, // 40GB HBM2e
            new Version(8, 0), // Ampere
            108, // SMs
            6912, // CUDA cores
            1555.0, // GB/s
            5120, // bit bus (HBM2e)
            765, // Base MHz
            1410, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates a V100 data center mock device.
    /// </summary>
    public static MockCudaDevice CreateV100(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_v100_0",
            "NVIDIA Tesla V100-SXM2-32GB",
            32L * 1024 * 1024 * 1024, // 32GB HBM2
            new Version(7, 0), // Volta
            80, // SMs
            5120, // CUDA cores
            900.0, // GB/s
            4096, // bit bus (HBM2)
            1245, // Base MHz
            1530, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates an H100 data center mock device.
    /// </summary>
    public static MockCudaDevice CreateH100(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_h100_0",
            "NVIDIA H100 SXM5 80GB",
            80L * 1024 * 1024 * 1024, // 80GB HBM3
            new Version(9, 0), // Hopper
            132, // SMs
            16896, // CUDA cores
            3350.0, // GB/s
            5120, // bit bus (HBM3)
            1095, // Base MHz
            1980, // Boost MHz
            logger);
    }

    /// <summary>
    /// Creates an RTX 2000 Ada Generation mock device.
    /// </summary>
    public static MockCudaDevice CreateRTX2000Ada(ILogger? logger = null)
    {
        return new MockCudaDevice(
            "cuda_rtx2000_ada_0",
            "NVIDIA RTX 2000 Ada Generation",
            16L * 1024 * 1024 * 1024, // 16GB GDDR6
            new Version(8, 9), // Ada Lovelace
            35, // SMs
            4480, // CUDA cores
            224.0, // GB/s
            128, // bit bus
            1485, // Base MHz
            2880, // Boost MHz
            logger);
    }

    /// <inheritdoc/>
    public override Dictionary<string, object> GetProperties()
    {
        var properties = base.GetProperties();

        // Add CUDA-specific properties
        properties["CUDA_ComputeCapability"] = ComputeCapability.ToString();
        properties["CUDA_StreamingMultiprocessors"] = StreamingMultiprocessors;
        properties["CUDA_CudaCores"] = CudaCores;
        properties["CUDA_MemoryBandwidth"] = MemoryBandwidth;
        properties["CUDA_MemoryBusWidth"] = MemoryBusWidth;
        properties["CUDA_BaseClock"] = BaseClock;
        properties["CUDA_BoostClock"] = BoostClock;
        properties["CUDA_Architecture"] = GetArchitectureName(ComputeCapability);
        properties["CUDA_WarpSize"] = 32;
        properties["CUDA_MaxBlockSize"] = 1024;

        return properties;
    }

    /// <inheritdoc/>
    public override bool HealthCheck()
    {
        if (!base.HealthCheck())
            return false;

        // CUDA-specific health checks
        if (ComputeCapability.Major < 3)
        {
            Logger?.LogWarning("CUDA device {DeviceId} has unsupported compute capability {ComputeCapability}",
                Id, ComputeCapability);
            return false;
        }

        if (StreamingMultiprocessors == 0 || CudaCores == 0)
        {
            Logger?.LogWarning("CUDA device {DeviceId} has invalid core configuration", Id);
            return false;
        }

        return true;
    }

    private static string GetArchitectureName(Version computeCapability)
    {
        return computeCapability.Major switch
        {
            3 => "Kepler",
            5 => "Maxwell",
            6 => "Pascal",
            7 => computeCapability.Minor == 0 ? "Volta" : "Turing",
            8 => computeCapability.Minor == 0 ? "Ampere" :
                 computeCapability.Minor == 6 ? "Ampere" : "Ada Lovelace",
            9 => "Hopper",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Simulates CUDA driver or runtime issues.
    /// </summary>
    /// <param name="errorType">The type of CUDA error to simulate.</param>
    public void SimulateCudaError(CudaErrorType errorType)
    {
        var errorMessage = errorType switch
        {
            CudaErrorType.OutOfMemory => "CUDA_ERROR_OUT_OF_MEMORY: out of memory",
            CudaErrorType.InitializationError => "CUDA_ERROR_INITIALIZATION_ERROR: initialization error",
            CudaErrorType.DeviceUnavailable => "CUDA_ERROR_NO_DEVICE: no CUDA-capable device is detected",
            CudaErrorType.InvalidDevice => "CUDA_ERROR_INVALID_DEVICE: device ordinal is not valid",
            CudaErrorType.KernelTimeout => "CUDA_ERROR_LAUNCH_TIMEOUT: the kernel launch timed out",
            _ => "CUDA_ERROR_UNKNOWN: unknown error"
        };

        SimulateFailure(errorMessage);
    }
}

/// <summary>
/// Types of CUDA errors that can be simulated.
/// </summary>
public enum CudaErrorType
{
    OutOfMemory,
    InitializationError,
    DeviceUnavailable,
    InvalidDevice,
    KernelTimeout
}
}
