// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Mock Metal device implementation for testing Apple Silicon and macOS GPU scenarios.
/// Simulates various Apple GPU configurations and Metal framework behaviors.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MockMetalDevice : MockHardwareDevice
{
    /// <summary>
    /// Gets the Metal feature set version.
    /// </summary>
    public string MetalFeatureSet { get; }

    /// <summary>
    /// Gets the GPU family.
    /// </summary>
    public string GpuFamily { get; }

    /// <summary>
    /// Gets the number of execution units.
    /// </summary>
    public int ExecutionUnits { get; }

    /// <summary>
    /// Gets the shader ALUs count.
    /// </summary>
    public int ShaderAlus { get; }

    /// <summary>
    /// Gets the memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidth { get; }

    /// <summary>
    /// Gets the metal performance shaders supported version.
    /// </summary>
    public Version MpsVersion { get; }

    /// <summary>
    /// Gets whether this is unified memory(typical for Apple Silicon).
    /// </summary>
    public bool IsUnifiedMemory { get; }

    /// <summary>
    /// Gets the neural engine availability.
    /// </summary>
    public bool HasNeuralEngine { get; }

    private MockMetalDevice(
        string id,
        string name,
        long totalMemory,
        string metalFeatureSet,
        string gpuFamily,
        int executionUnits,
        int shaderAlus,
        double memoryBandwidth,
        Version mpsVersion,
        bool isUnifiedMemory,
        bool hasNeuralEngine,
        ILogger? logger = null)
        : base(id, name, AcceleratorType.Metal, "Apple", "macOS 14.0", totalMemory, 
               1024, executionUnits, logger)
    {
        MetalFeatureSet = metalFeatureSet;
        GpuFamily = gpuFamily;
        ExecutionUnits = executionUnits;
        ShaderAlus = shaderAlus;
        MemoryBandwidth = memoryBandwidth;
        MpsVersion = mpsVersion;
        IsUnifiedMemory = isUnifiedMemory;
        HasNeuralEngine = hasNeuralEngine;

        // Add Metal-specific capabilities
        Capabilities["MetalFeatureSet"] = metalFeatureSet;
        Capabilities["GpuFamily"] = gpuFamily;
        Capabilities["ExecutionUnits"] = executionUnits;
        Capabilities["ShaderAlus"] = shaderAlus;
        Capabilities["MemoryBandwidth"] = memoryBandwidth;
        Capabilities["MpsVersion"] = mpsVersion.ToString();
        Capabilities["IsUnifiedMemory"] = isUnifiedMemory;
        Capabilities["HasNeuralEngine"] = hasNeuralEngine;
        Capabilities["SupportsRayTracing"] = gpuFamily.Contains("M3") || gpuFamily.Contains("M4");
        Capabilities["SupportsMeshShaders"] = gpuFamily.Contains("M2") || gpuFamily.Contains("M3") || gpuFamily.Contains("M4");
        Capabilities["MaxThreadsPerThreadgroup"] = 1024;
        Capabilities["MaxTextureSize"] = 16384;
    }

    /// <summary>
    /// Creates an Apple M2 Max mock device.
    /// </summary>
    public static MockMetalDevice CreateM2Max(ILogger? logger = null)
    {
        return new MockMetalDevice(
            "metal_m2_max_0",
            "Apple M2 Max",
            32L * 1024 * 1024 * 1024, // 32GB unified memory
            "Metal 3.1",
            "M2 Max",
            38, // GPU cores
            9728, // ALUs(estimate)
            400.0, // GB/s
            new Version(3, 1),
            true, // Unified memory
            true, // Has Neural Engine
            logger);
    }

    /// <summary>
    /// Creates an Apple M2 mock device.
    /// </summary>
    public static MockMetalDevice CreateM2(ILogger? logger = null)
    {
        return new MockMetalDevice(
            "metal_m2_0",
            "Apple M2",
            16L * 1024 * 1024 * 1024, // 16GB unified memory
            "Metal 3.1",
            "M2",
            10, // GPU cores
            2560, // ALUs(estimate)
            100.0, // GB/s
            new Version(3, 1),
            true, // Unified memory
            true, // Has Neural Engine
            logger);
    }

    /// <summary>
    /// Creates an Apple M1 Pro mock device.
    /// </summary>
    public static MockMetalDevice CreateM1Pro(ILogger? logger = null)
    {
        return new MockMetalDevice(
            "metal_m1_pro_0",
            "Apple M1 Pro",
            16L * 1024 * 1024 * 1024, // 16GB unified memory
            "Metal 3.0",
            "M1 Pro",
            16, // GPU cores
            4096, // ALUs(estimate)
            200.0, // GB/s
            new Version(3, 0),
            true, // Unified memory
            true, // Has Neural Engine
            logger);
    }

    /// <summary>
    /// Creates an Apple M1 mock device.
    /// </summary>
    public static MockMetalDevice CreateM1(ILogger? logger = null)
    {
        return new MockMetalDevice(
            "metal_m1_0",
            "Apple M1",
            8L * 1024 * 1024 * 1024, // 8GB unified memory
            "Metal 3.0",
            "M1",
            8, // GPU cores
            2048, // ALUs(estimate)
            68.25, // GB/s
            new Version(3, 0),
            true, // Unified memory
            true, // Has Neural Engine
            logger);
    }

    /// <summary>
    /// Creates an Apple M3 mock device.
    /// </summary>
    public static MockMetalDevice CreateM3(ILogger? logger = null)
    {
        return new MockMetalDevice(
            "metal_m3_0",
            "Apple M3",
            24L * 1024 * 1024 * 1024, // 24GB unified memory
            "Metal 3.2",
            "M3",
            10, // GPU cores
            2560, // ALUs(estimate)
            100.0, // GB/s
            new Version(3, 2),
            true, // Unified memory
            true, // Has Neural Engine
            logger);
    }

    /// <summary>
    /// Creates an Intel Iris Xe integrated GPU mock device for Intel Macs.
    /// </summary>
    public static MockMetalDevice CreateIntelIrisXe(ILogger? logger = null)
    {
        return new MockMetalDevice(
            "metal_intel_iris_xe_0",
            "Intel Iris Xe Graphics",
            8L * 1024 * 1024 * 1024, // 8GB shared memory
            "Metal 2.4",
            "Intel Iris Xe",
            80, // Execution units
            2560, // ALUs(80 EUs * 32 ALUs)
            68.0, // GB/s(shared with system)
            new Version(2, 4),
            true, // Unified memory(integrated)
            false, // No Neural Engine
            logger);
    }

    /// <inheritdoc/>
    public override Dictionary<string, object> GetProperties()
    {
        var properties = base.GetProperties();
        
        // Add Metal-specific properties
        properties["Metal_FeatureSet"] = MetalFeatureSet;
        properties["Metal_GpuFamily"] = GpuFamily;
        properties["Metal_ExecutionUnits"] = ExecutionUnits;
        properties["Metal_ShaderAlus"] = ShaderAlus;
        properties["Metal_MemoryBandwidth"] = MemoryBandwidth;
        properties["Metal_MpsVersion"] = MpsVersion.ToString();
        properties["Metal_IsUnifiedMemory"] = IsUnifiedMemory;
        properties["Metal_HasNeuralEngine"] = HasNeuralEngine;
        properties["Metal_MaxThreadsPerThreadgroup"] = 1024;
        properties["Metal_MaxTextureSize"] = 16384;

        return properties;
    }

    /// <inheritdoc/>
    public override bool HealthCheck()
    {
        if(!base.HealthCheck())
            return false;

        // Metal-specific health checks
        if(ExecutionUnits == 0 || ShaderAlus == 0)
        {
            Logger?.LogWarning("Metal device {DeviceId} has invalid execution unit configuration", Id);
            return false;
        }

        if(string.IsNullOrEmpty(MetalFeatureSet))
        {
            Logger?.LogWarning("Metal device {DeviceId} has no feature set specified", Id);
            return false;
        }

        if(MemoryBandwidth <= 0)
        {
            Logger?.LogWarning("Metal device {DeviceId} has invalid memory bandwidth", Id);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Simulates Metal framework-specific issues.
    /// </summary>
    /// <param name="issue">The type of Metal issue to simulate.</param>
    public void SimulateMetalIssue(MetalIssueType issue)
    {
        var errorMessage = issue switch
        {
            MetalIssueType.DeviceRemoved => "Metal device was removed or became unavailable",
            MetalIssueType.OutOfMemory => "Metal device ran out of memory",
            MetalIssueType.CommandBufferError => "Metal command buffer execution failed",
            MetalIssueType.ShaderCompilationError => "Metal shader compilation failed",
            MetalIssueType.InvalidResource => "Metal resource became invalid",
            _ => "Unknown Metal framework error"
        };

        SimulateFailure(errorMessage);
    }

    /// <summary>
    /// Gets the theoretical peak performance for different operations.
    /// </summary>
    /// <param name="operationType">The type of operation to calculate performance for.</param>
    /// <returns>Peak performance in appropriate units.</returns>
    public double GetPeakPerformance(MetalOperationType operationType)
    {
        return operationType switch
        {
            MetalOperationType.ScalarOps => ShaderAlus * MaxClockFrequency * 1e6, // Ops per second
            MetalOperationType.VectorOps => ShaderAlus * MaxClockFrequency * 4 * 1e6, // Assuming 4-wide SIMD
            MetalOperationType.MatrixOps when HasNeuralEngine => ShaderAlus * MaxClockFrequency * 16 * 1e6, // ML accelerated
            MetalOperationType.MatrixOps => ShaderAlus * MaxClockFrequency * 8 * 1e6, // Standard matrix ops
            MetalOperationType.TextureOps => ExecutionUnits * MaxClockFrequency * 4 * 1e6, // Texture units
            _ => 0
        };
    }

    /// <summary>
    /// Estimates the Neural Engine performance in operations per second.
    /// </summary>
    /// <returns>Neural Engine TOPS(Trillions of Operations Per Second).</returns>
    public double GetNeuralEnginePerformance()
    {
        if(!HasNeuralEngine)
            return 0;

        // Rough estimates based on Apple Silicon generations
        return GpuFamily switch
        {
            "M1" => 11.5, // 11.5 TOPS
            "M1 Pro" => 11.5,
            "M1 Max" => 11.5,
            "M2" => 15.8, // 15.8 TOPS
            "M2 Pro" => 15.8,
            "M2 Max" => 15.8,
            "M3" => 18.0, // Estimated 18 TOPS
            "M3 Pro" => 18.0,
            "M3 Max" => 18.0,
            _ => 10.0 // Default estimate
        };
    }
}

/// <summary>
/// Types of Metal framework issues that can be simulated.
/// </summary>
public enum MetalIssueType
{
    DeviceRemoved,
    OutOfMemory,
    CommandBufferError,
    ShaderCompilationError,
    InvalidResource
}

/// <summary>
/// Types of Metal operations for performance estimation.
/// </summary>
public enum MetalOperationType
{
    ScalarOps,
    VectorOps,
    MatrixOps,
    TextureOps
}
