// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;

namespace DotCompute.Tests.Hardware;

/// <summary>
/// Test collection definition for CUDA hardware tests to ensure proper test isolation
/// </summary>
[CollectionDefinition("CUDA Hardware Tests")]
public class CudaHardwareTestCollection : ICollectionFixture<CudaTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

/// <summary>
/// Test collection definition for CUDA mock tests that don't require hardware
/// </summary>
[CollectionDefinition("CUDA Mock Tests")]
public class CudaMockTestCollection
{
    // Mock tests don't need fixtures since they don't use actual hardware
}

/// <summary>
/// Test fixture for CUDA hardware tests to manage shared test resources
/// </summary>
public class CudaTestFixture : IDisposable
{
    public CudaTestFixture()
    {
        // Initialize shared test resources
        InitializeTestEnvironment();
    }

    /// <summary>
    /// Validates and initializes the CUDA test environment
    /// </summary>
    private void InitializeTestEnvironment()
    {
        try
        {
            // Check CUDA availability
            var result = DotCompute.Backends.CUDA.Native.CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            IsCudaAvailable = result == DotCompute.Backends.CUDA.Native.CudaError.Success && deviceCount > 0;
            DeviceCount = deviceCount;

            if (IsCudaAvailable)
            {
                // Get primary device properties
                var props = new DotCompute.Backends.CUDA.Native.CudaDeviceProperties();
                var propResult = DotCompute.Backends.CUDA.Native.CudaRuntime.cudaGetDeviceProperties(ref props, 0);
                if (propResult == DotCompute.Backends.CUDA.Native.CudaError.Success)
                {
                    PrimaryDeviceName = props.Name;
                    ComputeCapability = new Version(props.Major, props.Minor);
                }
            }

            // Check NVRTC availability
            IsNvrtcAvailable = DotCompute.Backends.CUDA.Compilation.CudaKernelCompiler.IsNvrtcAvailable();

            // Set test environment flags
            Environment.SetEnvironmentVariable("DOTCOMPUTE_CUDA_TESTS_AVAILABLE", IsCudaAvailable.ToString());
            Environment.SetEnvironmentVariable("DOTCOMPUTE_NVRTC_TESTS_AVAILABLE", IsNvrtcAvailable.ToString());
            Environment.SetEnvironmentVariable("DOTCOMPUTE_DEVICE_COUNT", DeviceCount.ToString());

            Console.WriteLine($"CUDA Test Environment Initialized:");
            Console.WriteLine($"  CUDA Available: {IsCudaAvailable}");
            Console.WriteLine($"  Device Count: {DeviceCount}");
            Console.WriteLine($"  Primary Device: {PrimaryDeviceName}");
            Console.WriteLine($"  Compute Capability: {ComputeCapability}");
            Console.WriteLine($"  NVRTC Available: {IsNvrtcAvailable}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize CUDA test environment: {ex.Message}");
            IsCudaAvailable = false;
            IsNvrtcAvailable = false;
            DeviceCount = 0;
        }
    }

    /// <summary>
    /// Indicates whether CUDA is available for testing
    /// </summary>
    public bool IsCudaAvailable { get; private set; }

    /// <summary>
    /// Indicates whether NVRTC is available for kernel compilation tests
    /// </summary>
    public bool IsNvrtcAvailable { get; private set; }

    /// <summary>
    /// Number of available CUDA devices
    /// </summary>
    public int DeviceCount { get; private set; }

    /// <summary>
    /// Name of the primary CUDA device
    /// </summary>
    public string PrimaryDeviceName { get; private set; } = string.Empty;

    /// <summary>
    /// Compute capability of the primary device
    /// </summary>
    public Version? ComputeCapability { get; private set; }

    /// <summary>
    /// Indicates whether the environment is suitable for comprehensive testing
    /// </summary>
    public bool IsFullyFunctional => IsCudaAvailable && IsNvrtcAvailable && DeviceCount > 0;

    public void Dispose()
    {
        // Cleanup any shared resources
        try
        {
            // Reset any CUDA context if needed
            if (IsCudaAvailable)
            {
                // Note: In a real implementation, we might want to reset the device
                // but we'll be conservative here to avoid affecting other processes
            }

            Console.WriteLine("CUDA test environment cleaned up");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during CUDA test cleanup: {ex.Message}");
        }
    }
}
