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
/// An gpu backend enumeration.
/// </summary>

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
/// An dot compute test platform enumeration.
/// </summary>

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
    /// <summary>
    /// Gets or sets a value indicating whether sse.
    /// </summary>
    /// <value>The has sse.</value>
    public bool HasSse { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sse2.
    /// </summary>
    /// <value>The has sse2.</value>
    public bool HasSse2 { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sse3.
    /// </summary>
    /// <value>The has sse3.</value>
    public bool HasSse3 { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether ssse3.
    /// </summary>
    /// <value>The has ssse3.</value>
    public bool HasSsse3 { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sse41.
    /// </summary>
    /// <value>The has sse41.</value>
    public bool HasSse41 { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sse42.
    /// </summary>
    /// <value>The has sse42.</value>
    public bool HasSse42 { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether avx.
    /// </summary>
    /// <value>The has avx.</value>
    public bool HasAvx { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether avx2.
    /// </summary>
    /// <value>The has avx2.</value>
    public bool HasAvx2 { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether fma.
    /// </summary>
    /// <value>The has fma.</value>
    public bool HasFma { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether avx512 f.
    /// </summary>
    /// <value>The has avx512 f.</value>
    public bool HasAvx512F { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether avx512 bw.
    /// </summary>
    /// <value>The has avx512 bw.</value>
    public bool HasAvx512Bw { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether avx512 cd.
    /// </summary>
    /// <value>The has avx512 cd.</value>
    public bool HasAvx512Cd { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether avx512 dq.
    /// </summary>
    /// <value>The has avx512 dq.</value>
    public bool HasAvx512Dq { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether avx512 vl.
    /// </summary>
    /// <value>The has avx512 vl.</value>
    public bool HasAvx512Vl { get; set; }
    /// <summary>
    /// Gets or sets the vector size.
    /// </summary>
    /// <value>The vector size.</value>
    public int VectorSize { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether hardware accelerated.
    /// </summary>
    /// <value>The is hardware accelerated.</value>
    public bool IsHardwareAccelerated { get; set; }
}

/// <summary>
/// GPU capabilities detection result
/// </summary>
public class GpuCapabilities
{
    /// <summary>
    /// Gets or sets a value indicating whether cuda.
    /// </summary>
    /// <value>The has cuda.</value>
    public bool HasCuda { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether open c l.
    /// </summary>
    /// <value>The has open c l.</value>
    public bool HasOpenCL { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether metal.
    /// </summary>
    /// <value>The has metal.</value>
    public bool HasMetal { get; set; }
    /// <summary>
    /// Gets or sets the cuda devices.
    /// </summary>
    /// <value>The cuda devices.</value>
    public GpuDeviceInfo[] CudaDevices { get; set; } = Array.Empty<GpuDeviceInfo>();
    /// <summary>
    /// Gets or sets the open c l devices.
    /// </summary>
    /// <value>The open c l devices.</value>
    public GpuDeviceInfo[] OpenCLDevices { get; set; } = Array.Empty<GpuDeviceInfo>();
    /// <summary>
    /// Gets or sets the metal devices.
    /// </summary>
    /// <value>The metal devices.</value>
    public GpuDeviceInfo[] MetalDevices { get; set; } = Array.Empty<GpuDeviceInfo>();
}

/// <summary>
/// GPU device information
/// </summary>
public class GpuDeviceInfo
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the memory size.
    /// </summary>
    /// <value>The memory size.</value>
    public long MemorySize { get; set; }
    /// <summary>
    /// Gets or sets the compute capability.
    /// </summary>
    /// <value>The compute capability.</value>
    public string ComputeCapability { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the backend.
    /// </summary>
    /// <value>The backend.</value>
    public GpuBackend Backend { get; set; }
}

/// <summary>
/// Test environment information
/// </summary>
public class TestEnvironment
{
    /// <summary>
    /// Gets or sets the simd capabilities.
    /// </summary>
    /// <value>The simd capabilities.</value>
    public SimdCapabilities SimdCapabilities { get; set; } = new();
    /// <summary>
    /// Gets or sets the available instruction sets.
    /// </summary>
    /// <value>The available instruction sets.</value>
    public SimdInstructionSet[] AvailableInstructionSets { get; set; } = Array.Empty<SimdInstructionSet>();
    /// <summary>
    /// Gets or sets the required instruction sets.
    /// </summary>
    /// <value>The required instruction sets.</value>
    public SimdInstructionSet[] RequiredInstructionSets { get; set; } = Array.Empty<SimdInstructionSet>();
    /// <summary>
    /// Gets or sets a value indicating whether valid environment.
    /// </summary>
    /// <value>The is valid environment.</value>
    public bool IsValidEnvironment { get; set; }
    /// <summary>
    /// Gets or sets the platform.
    /// </summary>
    /// <value>The platform.</value>
    public DotComputeTestPlatform Platform { get; set; }
}

/// <summary>
/// SIMD performance validation result
/// </summary>
public class SimdPerformanceResult
{
    /// <summary>
    /// Gets or sets the scalar time ms.
    /// </summary>
    /// <value>The scalar time ms.</value>
    public double ScalarTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the vector time ms.
    /// </summary>
    /// <value>The vector time ms.</value>
    public double VectorTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the speedup ratio.
    /// </summary>
    /// <value>The speedup ratio.</value>
    public double SpeedupRatio { get; set; }
    /// <summary>
    /// Gets or sets the expected speedup ratio.
    /// </summary>
    /// <value>The expected speedup ratio.</value>
    public double ExpectedSpeedupRatio { get; set; }
    /// <summary>
    /// Gets or sets the meets expectation.
    /// </summary>
    /// <value>The meets expectation.</value>
    public bool MeetsExpectation { get; set; }
    /// <summary>
    /// Gets or sets the scalar result.
    /// </summary>
    /// <value>The scalar result.</value>
    public PerformanceStats ScalarResult { get; set; } = new();
    /// <summary>
    /// Gets or sets the vector result.
    /// </summary>
    /// <value>The vector result.</value>
    public PerformanceStats VectorResult { get; set; } = new();
}

/// <summary>
/// GPU performance validation result
/// </summary>
public class GpuPerformanceResult
{
    /// <summary>
    /// Gets or sets the cpu time ms.
    /// </summary>
    /// <value>The cpu time ms.</value>
    public double CpuTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the gpu time ms.
    /// </summary>
    /// <value>The gpu time ms.</value>
    public double GpuTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the speedup ratio.
    /// </summary>
    /// <value>The speedup ratio.</value>
    public double SpeedupRatio { get; set; }
    /// <summary>
    /// Gets or sets the expected speedup ratio.
    /// </summary>
    /// <value>The expected speedup ratio.</value>
    public double ExpectedSpeedupRatio { get; set; }
    /// <summary>
    /// Gets or sets the meets expectation.
    /// </summary>
    /// <value>The meets expectation.</value>
    public bool MeetsExpectation { get; set; }
    /// <summary>
    /// Gets or sets the cpu result.
    /// </summary>
    /// <value>The cpu result.</value>
    public PerformanceStats CpuResult { get; set; } = new();
    /// <summary>
    /// Gets or sets the gpu result.
    /// </summary>
    /// <value>The gpu result.</value>
    public PerformanceStats GpuResult { get; set; } = new();
}



#endregion