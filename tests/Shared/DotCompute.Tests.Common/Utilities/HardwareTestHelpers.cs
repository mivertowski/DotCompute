// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
// using DotCompute.SharedTestUtilities.Performance; // Removed to avoid confusion with PerformanceStats
using DotCompute.Core.Extensions;
using DotCompute.Tests.Common.Helpers;
using Moq;

namespace DotCompute.Tests.Common.Utilities;

/// <summary>
/// Hardware-specific test helpers for validating SIMD capabilities,
/// GPU detection, and cross-platform compute backend testing.
/// </summary>
public static class HardwareTestHelpers
{
    #region SIMD Capability Testing

    /// <summary>
    /// Detects and validates SIMD instruction set support for testing.
    /// </summary>
    public static SimdCapabilities DetectSimdCapabilities()
    {
        return new SimdCapabilities
        {
            HasSse = Sse.IsSupported,
            HasSse2 = Sse2.IsSupported,
            HasSse3 = Sse3.IsSupported,
            HasSsse3 = Ssse3.IsSupported,
            HasSse41 = Sse41.IsSupported,
            HasSse42 = Sse42.IsSupported,
            HasAvx = Avx.IsSupported,
            HasAvx2 = Avx2.IsSupported,
            HasFma = Fma.IsSupported,
            HasAvx512F = Avx512F.IsSupported,
            HasAvx512Bw = Avx512BW.IsSupported,
            HasAvx512Cd = Avx512CD.IsSupported,
            HasAvx512Dq = Avx512DQ.IsSupported,
            HasAvx512Vl = false, // Avx512VL not available in .NET runtime intrinsics
            VectorSize = System.Numerics.Vector<float>.Count,
            IsHardwareAccelerated = System.Numerics.Vector.IsHardwareAccelerated
        };
    }

    /// <summary>
    /// Skips test if required SIMD instruction set is not available.
    /// </summary>
    public static void RequireSimdSupport(SimdInstructionSet requiredSet)
    {
        var capabilities = DetectSimdCapabilities();
        var hasSupport = requiredSet switch
        {
            SimdInstructionSet.SSE => capabilities.HasSse,
            SimdInstructionSet.SSE2 => capabilities.HasSse2,
            SimdInstructionSet.SSE3 => capabilities.HasSse3,
            SimdInstructionSet.SSSE3 => capabilities.HasSsse3,
            SimdInstructionSet.SSE41 => capabilities.HasSse41,
            SimdInstructionSet.SSE42 => capabilities.HasSse42,
            SimdInstructionSet.AVX => capabilities.HasAvx,
            SimdInstructionSet.AVX2 => capabilities.HasAvx2,
            SimdInstructionSet.FMA => capabilities.HasFma,
            SimdInstructionSet.AVX512F => capabilities.HasAvx512F,
            _ => false
        };

        Skip.IfNot(hasSupport, $"{requiredSet} instruction set not supported on this platform");
    }

    /// <summary>
    /// Creates a test environment with specified SIMD capabilities.
    /// </summary>
    public static TestEnvironment CreateSimdTestEnvironment(params SimdInstructionSet[] requiredSets)
    {
        var capabilities = DetectSimdCapabilities();
        var availableSets = new List<SimdInstructionSet>();

        foreach (var set in Enum.GetValues<SimdInstructionSet>())
        {
            var hasSupport = set switch
            {
                SimdInstructionSet.SSE => capabilities.HasSse,
                SimdInstructionSet.SSE2 => capabilities.HasSse2,
                SimdInstructionSet.SSE3 => capabilities.HasSse3,
                SimdInstructionSet.SSSE3 => capabilities.HasSsse3,
                SimdInstructionSet.SSE41 => capabilities.HasSse41,
                SimdInstructionSet.SSE42 => capabilities.HasSse42,
                SimdInstructionSet.AVX => capabilities.HasAvx,
                SimdInstructionSet.AVX2 => capabilities.HasAvx2,
                SimdInstructionSet.FMA => capabilities.HasFma,
                SimdInstructionSet.AVX512F => capabilities.HasAvx512F,
                _ => false
            };

            if (hasSupport)
                availableSets.Add(set);
        }

        var missingRequired = requiredSets.Except(availableSets).ToArray();
        if (missingRequired.Length > 0)
        {
            Skip.If(true, $"Required SIMD instruction sets not available: {string.Join(", ", missingRequired)}");
        }

        return new TestEnvironment
        {
            SimdCapabilities = capabilities,
            AvailableInstructionSets = [.. availableSets],
            RequiredInstructionSets = requiredSets,
            IsValidEnvironment = missingRequired.Length == 0
        };
    }

    #endregion

    #region GPU Detection and Testing

    /// <summary>
    /// Detects available GPU compute backends for testing.
    /// </summary>
    public static async Task<GpuCapabilities> DetectGpuCapabilitiesAsync()
    {
        var capabilities = new GpuCapabilities();

        try
        {
            // Try to detect CUDA
            capabilities.HasCuda = await TryDetectCudaAsync();
            if (capabilities.HasCuda)
            {
                capabilities.CudaDevices = await GetCudaDevicesAsync();
            }
        }
        catch
        {
            capabilities.HasCuda = false;
        }

        try
        {
            // Try to detect OpenCL
            capabilities.HasOpenCL = await TryDetectOpenCLAsync();
            if (capabilities.HasOpenCL)
            {
                capabilities.OpenCLDevices = await GetOpenCLDevicesAsync();
            }
        }
        catch
        {
            capabilities.HasOpenCL = false;
        }

        try
        {
            // Try to detect Metal (macOS)
            capabilities.HasMetal = await TryDetectMetalAsync();
            if (capabilities.HasMetal)
            {
                capabilities.MetalDevices = await GetMetalDevicesAsync();
            }
        }
        catch
        {
            capabilities.HasMetal = false;
        }

        return capabilities;
    }

    /// <summary>
    /// Skips test if required GPU backend is not available.
    /// </summary>
    public static async Task RequireGpuSupportAsync(GpuBackend requiredBackend)
    {
        var capabilities = await DetectGpuCapabilitiesAsync();
        var hasSupport = requiredBackend switch
        {
            GpuBackend.CUDA => capabilities.HasCuda,
            GpuBackend.OpenCL => capabilities.HasOpenCL,
            GpuBackend.Metal => capabilities.HasMetal,
            _ => false
        };

        Skip.IfNot(hasSupport, $"{requiredBackend} GPU backend not available on this platform");
    }

    /// <summary>
    /// Creates mock GPU accelerators for testing scenarios where hardware is not available.
    /// </summary>
    public static List<Mock<IAccelerator>> CreateMockGpuAccelerators()
    {
        var mocks = new List<Mock<IAccelerator>>();

        // CUDA accelerator
        var cudaMock = TestUtilities.CreateMockAccelerator(
            "CUDA", "NVIDIA RTX 4090", true, 24L * 1024L * 1024L * 1024L); // 24GB
        _ = cudaMock.Setup(a => a.SupportsFeature("CUDA")).Returns(true);
        _ = cudaMock.Setup(a => a.SupportsFeature("DoublePrecision")).Returns(true);
        mocks.Add(cudaMock);

        // OpenCL accelerator
        var openclMock = TestUtilities.CreateMockAccelerator(
            "OpenCL", "AMD RX 7900 XTX", true, 20L * 1024L * 1024L * 1024L); // 20GB
        _ = openclMock.Setup(a => a.SupportsFeature("OpenCL")).Returns(true);
        _ = openclMock.Setup(a => a.SupportsFeature("ImageSupport")).Returns(true);
        mocks.Add(openclMock);

        // Metal accelerator
        var metalMock = TestUtilities.CreateMockAccelerator(
            "Metal", "Apple M3 Max", true, 128L * 1024L * 1024L * 1024L); // 128GB unified memory
        _ = metalMock.Setup(a => a.SupportsFeature("Metal")).Returns(true);
        _ = metalMock.Setup(a => a.SupportsFeature("UnifiedMemory")).Returns(true);
        mocks.Add(metalMock);

        return mocks;
    }

    #endregion

    #region Platform-Specific Testing

    /// <summary>
    /// Determines the current platform for platform-specific test logic.
    /// </summary>
    public static DotComputeTestPlatform GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return DotComputeTestPlatform.Windows;
        if (OperatingSystem.IsLinux())
            return DotComputeTestPlatform.Linux;
        if (OperatingSystem.IsMacOS())
            return DotComputeTestPlatform.MacOS;

        return DotComputeTestPlatform.Unknown;
    }

    /// <summary>
    /// Skips test if not running on specified platform.
    /// </summary>
    public static void RequirePlatform(DotComputeTestPlatform requiredPlatform)
    {
        var currentPlatform = GetCurrentPlatform();
        Skip.IfNot(currentPlatform == requiredPlatform,
            $"Test requires {requiredPlatform} platform, currently running on {currentPlatform}");
    }

    /// <summary>
    /// Skips test if running on specified platform.
    /// </summary>
    public static void SkipOnPlatform(DotComputeTestPlatform platformToSkip)
    {
        var currentPlatform = GetCurrentPlatform();
        Skip.If(currentPlatform == platformToSkip,
            $"Test skipped on {platformToSkip} platform");
    }

    #endregion

    #region Performance Validation

    /// <summary>
    /// Validates that SIMD operations achieve expected performance improvements.
    /// </summary>
    public static async Task<SimdPerformanceResult> ValidateSimdPerformanceAsync(
        Func<Task> scalarOperation,
        Func<Task> vectorOperation,
        double expectedSpeedupRatio = 2.0,
        int iterations = 100)
    {
        var scalarResult = await TestUtilities.MeasurePerformanceAsync(scalarOperation, iterations: iterations);
        var vectorResult = await TestUtilities.MeasurePerformanceAsync(vectorOperation, iterations: iterations);

        var speedupRatio = scalarResult.MeanTimeMs / vectorResult.MeanTimeMs;

        return new SimdPerformanceResult
        {
            ScalarTimeMs = scalarResult.MeanTimeMs,
            VectorTimeMs = vectorResult.MeanTimeMs,
            SpeedupRatio = speedupRatio,
            ExpectedSpeedupRatio = expectedSpeedupRatio,
            MeetsExpectation = speedupRatio >= expectedSpeedupRatio,
            ScalarResult = scalarResult,
            VectorResult = vectorResult
        };
    }

    /// <summary>
    /// Validates GPU vs CPU performance for compute-intensive workloads.
    /// </summary>
    public static async Task<GpuPerformanceResult> ValidateGpuPerformanceAsync(
        Func<Task> cpuOperation,
        Func<Task> gpuOperation,
        double expectedSpeedupRatio = 5.0,
        int iterations = 50)
    {
        var cpuResult = await TestUtilities.MeasurePerformanceAsync(cpuOperation, iterations: iterations);
        var gpuResult = await TestUtilities.MeasurePerformanceAsync(gpuOperation, iterations: iterations);

        var speedupRatio = cpuResult.MeanTimeMs / gpuResult.MeanTimeMs;

        return new GpuPerformanceResult
        {
            CpuTimeMs = cpuResult.MeanTimeMs,
            GpuTimeMs = gpuResult.MeanTimeMs,
            SpeedupRatio = speedupRatio,
            ExpectedSpeedupRatio = expectedSpeedupRatio,
            MeetsExpectation = speedupRatio >= expectedSpeedupRatio,
            CpuResult = cpuResult,
            GpuResult = gpuResult
        };
    }

    #endregion

    #region Private Helper Methods

    private static async Task<bool> TryDetectCudaAsync()
    {
        // This would need actual CUDA detection logic
        // For testing purposes, we'll simulate detection
        await Task.Delay(1);
        return HardwareDetection.IsCudaAvailable();
    }

    private static async Task<GpuDeviceInfo[]> GetCudaDevicesAsync()
    {
        await Task.Delay(1);
        return
        [
            new GpuDeviceInfo
            {
                Name = "NVIDIA RTX 4090",
                MemorySize = 24L * 1024L * 1024L * 1024L,
                ComputeCapability = "8.9",
                Backend = GpuBackend.CUDA
            }
        ];
    }

    private static async Task<bool> TryDetectOpenCLAsync()
    {
        await Task.Delay(1);
        return false; // Placeholder
    }

    private static async Task<GpuDeviceInfo[]> GetOpenCLDevicesAsync()
    {
        await Task.Delay(1);
        return Array.Empty<GpuDeviceInfo>();
    }

    private static async Task<bool> TryDetectMetalAsync()
    {
        await Task.Delay(1);
        return OperatingSystem.IsMacOS(); // Metal only available on macOS
    }

    private static async Task<GpuDeviceInfo[]> GetMetalDevicesAsync()
    {
        await Task.Delay(1);
        if (!OperatingSystem.IsMacOS())
            return Array.Empty<GpuDeviceInfo>();

        return
        [
            new GpuDeviceInfo
            {
                Name = "Apple M3 Max",
                MemorySize = 128L * 1024L * 1024L * 1024L,
                ComputeCapability = "Metal 3.1",
                Backend = GpuBackend.Metal
            }
        ];
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// SIMD instruction set enumeration
/// </summary>
public enum SimdInstructionSet
{
    SSE,
    SSE2,
    SSE3,
    SSSE3,
    SSE41,
    SSE42,
    AVX,
    AVX2,
    FMA,
    AVX512F,
    AVX512BW,
    AVX512CD,
    AVX512DQ,
    AVX512VL
}

/// <summary>
/// GPU backend enumeration
/// </summary>
public enum GpuBackend
{
    CUDA,
    OpenCL,
    Metal,
    DirectCompute
}

/// <summary>
/// Test platform enumeration for DotCompute tests
/// </summary>
public enum DotComputeTestPlatform
{
    Windows,
    Linux,
    MacOS,
    Unknown
}

/// <summary>
/// SIMD capabilities detection result
/// </summary>
public class SimdCapabilities
{
    public bool HasSse { get; set; }
    public bool HasSse2 { get; set; }
    public bool HasSse3 { get; set; }
    public bool HasSsse3 { get; set; }
    public bool HasSse41 { get; set; }
    public bool HasSse42 { get; set; }
    public bool HasAvx { get; set; }
    public bool HasAvx2 { get; set; }
    public bool HasFma { get; set; }
    public bool HasAvx512F { get; set; }
    public bool HasAvx512Bw { get; set; }
    public bool HasAvx512Cd { get; set; }
    public bool HasAvx512Dq { get; set; }
    public bool HasAvx512Vl { get; set; }
    public int VectorSize { get; set; }
    public bool IsHardwareAccelerated { get; set; }
}

/// <summary>
/// GPU capabilities detection result
/// </summary>
public class GpuCapabilities
{
    public bool HasCuda { get; set; }
    public bool HasOpenCL { get; set; }
    public bool HasMetal { get; set; }
    public GpuDeviceInfo[] CudaDevices { get; set; } = Array.Empty<GpuDeviceInfo>();
    public GpuDeviceInfo[] OpenCLDevices { get; set; } = Array.Empty<GpuDeviceInfo>();
    public GpuDeviceInfo[] MetalDevices { get; set; } = Array.Empty<GpuDeviceInfo>();
}

/// <summary>
/// GPU device information
/// </summary>
public class GpuDeviceInfo
{
    public string Name { get; set; } = string.Empty;
    public long MemorySize { get; set; }
    public string ComputeCapability { get; set; } = string.Empty;
    public GpuBackend Backend { get; set; }
}

/// <summary>
/// Test environment information
/// </summary>
public class TestEnvironment
{
    public SimdCapabilities SimdCapabilities { get; set; } = new();
    public SimdInstructionSet[] AvailableInstructionSets { get; set; } = Array.Empty<SimdInstructionSet>();
    public SimdInstructionSet[] RequiredInstructionSets { get; set; } = Array.Empty<SimdInstructionSet>();
    public bool IsValidEnvironment { get; set; }
    public DotComputeTestPlatform Platform { get; set; }
}

/// <summary>
/// SIMD performance validation result
/// </summary>
public class SimdPerformanceResult
{
    public double ScalarTimeMs { get; set; }
    public double VectorTimeMs { get; set; }
    public double SpeedupRatio { get; set; }
    public double ExpectedSpeedupRatio { get; set; }
    public bool MeetsExpectation { get; set; }
    public PerformanceStats ScalarResult { get; set; } = new();
    public PerformanceStats VectorResult { get; set; } = new();
}

/// <summary>
/// GPU performance validation result
/// </summary>
public class GpuPerformanceResult
{
    public double CpuTimeMs { get; set; }
    public double GpuTimeMs { get; set; }
    public double SpeedupRatio { get; set; }
    public double ExpectedSpeedupRatio { get; set; }
    public bool MeetsExpectation { get; set; }
    public PerformanceStats CpuResult { get; set; } = new();
    public PerformanceStats GpuResult { get; set; } = new();
}


#endregion