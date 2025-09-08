using DotCompute.Tests.Common.Helpers;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common.Fixtures;

/// <summary>
/// Provides shared test fixtures and setup/teardown functionality for test classes.
/// Implements IDisposable and IAsyncDisposable for proper resource cleanup.
/// </summary>
public class CommonTestFixture : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    
    /// <summary>
    /// Gets the test output helper for logging test information.
    /// </summary>
    public ITestOutputHelper? Output { get; private set; }
    
    /// <summary>
    /// Gets hardware information detected at fixture creation.
    /// </summary>
    public HardwareDetection.HardwareInfo HardwareInfo { get; }
    
    /// <summary>
    /// Gets the temporary directory created for this test fixture.
    /// </summary>
    public string TempDirectory { get; }
    
    /// <summary>
    /// Gets a value indicating whether CUDA is available for testing.
    /// </summary>
    public bool IsCudaAvailable => HardwareInfo.CudaAvailable;
    
    /// <summary>
    /// Gets a value indicating whether OpenCL is available for testing.
    /// </summary>
    public bool IsOpenCLAvailable => HardwareInfo.OpenCLAvailable;
    
    /// <summary>
    /// Gets a value indicating whether GPU testing is available.
    /// </summary>
    public bool IsGpuAvailable => IsCudaAvailable || IsOpenCLAvailable;
    
    /// <summary>
    /// Gets a value indicating whether multi-GPU testing is available.
    /// </summary>
    public bool IsMultiGpuAvailable => HardwareInfo.CudaDeviceCount > 1 || HardwareInfo.OpenCLDeviceCount > 1;
    
    /// <summary>
    /// Gets a value indicating whether high-performance CPU features are available.
    /// </summary>
    public bool IsHighPerformanceCpuAvailable => HardwareInfo.AvxSupported && HardwareInfo.PhysicalCores >= 4;
    
    /// <summary>
    /// Gets a value indicating whether sufficient memory is available for intensive tests.
    /// </summary>
    public bool IsHighMemoryAvailable => HardwareInfo.AvailableMemory >= 4L * 1024 * 1024 * 1024; // 4GB
    
    /// <summary>
    /// Initializes a new instance of the CommonTestFixture class.
    /// </summary>
    public CommonTestFixture()
    {
        HardwareInfo = HardwareDetection.Info;
        TempDirectory = CreateTempDirectory();
        
        // Log hardware information for debugging
        LogHardwareInfo();
    }


    /// <summary>
    /// Sets the test output helper for logging.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public void SetOutput(ITestOutputHelper output) => Output = output;


    /// <summary>
    /// Skips the current test if CUDA is not available.
    /// </summary>
    public void RequireCuda() => Skip.If(!IsCudaAvailable, "CUDA is not available on this system");


    /// <summary>
    /// Skips the current test if OpenCL is not available.
    /// </summary>
    public void RequireOpenCL() => Skip.If(!IsOpenCLAvailable, "OpenCL is not available on this system");


    /// <summary>
    /// Skips the current test if GPU is not available.
    /// </summary>
    public void RequireGpu() => Skip.If(!IsGpuAvailable, "GPU support (CUDA or OpenCL) is not available on this system");


    /// <summary>
    /// Skips the current test if multi-GPU setup is not available.
    /// </summary>
    public void RequireMultiGpu() => Skip.If(!IsMultiGpuAvailable, "Multi-GPU setup is not available on this system");


    /// <summary>
    /// Skips the current test if high-performance CPU features are not available.
    /// </summary>
    public void RequireHighPerformanceCpu() => Skip.If(!IsHighPerformanceCpuAvailable, "High-performance CPU features (AVX, 4+ cores) are not available on this system");


    /// <summary>
    /// Skips the current test if sufficient memory is not available.
    /// </summary>
    public void RequireHighMemory() => Skip.If(!IsHighMemoryAvailable, "Insufficient memory available for high-memory tests (4GB+ required)");


    /// <summary>
    /// Creates a temporary file in the fixture's temp directory.
    /// </summary>
    /// <param name="fileName">Optional file name. If not provided, a random name is generated.</param>
    /// <returns>The full path to the created temporary file.</returns>
    public string CreateTempFile(string? fileName = null)
    {
        fileName ??= $"temp_{Guid.NewGuid():N}.tmp";
        var filePath = Path.Combine(TempDirectory, fileName);
        
        // Ensure the file is created
        File.WriteAllText(filePath, "");
        
        return filePath;
    }
    
    /// <summary>
    /// Creates a temporary subdirectory in the fixture's temp directory.
    /// </summary>
    /// <param name="directoryName">Optional directory name. If not provided, a random name is generated.</param>
    /// <returns>The full path to the created temporary directory.</returns>
    public string CreateTempSubDirectory(string? directoryName = null)
    {
        directoryName ??= $"temp_{Guid.NewGuid():N}";
        var dirPath = Path.Combine(TempDirectory, directoryName);

        _ = Directory.CreateDirectory(dirPath);
        
        return dirPath;
    }


    /// <summary>
    /// Logs a message to the test output if available.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Log(string message) => Output?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");


    /// <summary>
    /// Logs hardware information for debugging purposes.
    /// </summary>
    private void LogHardwareInfo()
    {
        Log("=== Hardware Information ===");
        Log($"Platform: {HardwareInfo.Platform} ({HardwareInfo.Architecture})");
        Log($"CPU: {HardwareInfo.CpuName}");
        Log($"Logical Cores: {HardwareInfo.LogicalCores}, Physical Cores: {HardwareInfo.PhysicalCores}");
        Log($"AVX Support: {HardwareInfo.AvxSupported}, AVX2: {HardwareInfo.Avx2Supported}, AVX-512: {HardwareInfo.Avx512Supported}");
        Log($"Total Memory: {HardwareInfo.TotalMemory / (1024 * 1024 * 1024):F1} GB");
        Log($"Available Memory: {HardwareInfo.AvailableMemory / (1024 * 1024 * 1024):F1} GB");
        Log($"CUDA Available: {HardwareInfo.CudaAvailable} ({HardwareInfo.CudaDeviceCount} devices)");
        Log($"OpenCL Available: {HardwareInfo.OpenCLAvailable} ({HardwareInfo.OpenCLDeviceCount} devices)");
        
        if (HardwareInfo.GpuNames.Count > 0)
        {
            Log("GPUs:");
            foreach (var gpu in HardwareInfo.GpuNames)
            {
                Log($"  - {gpu}");
            }
        }
        
        Log($"Test Capabilities:");
        Log($"  GPU Testing: {IsGpuAvailable}");
        Log($"  Multi-GPU Testing: {IsMultiGpuAvailable}");
        Log($"  High-Performance CPU: {IsHighPerformanceCpuAvailable}");
        Log($"  High-Memory Testing: {IsHighMemoryAvailable}");
        Log("===============================");
    }
    
    /// <summary>
    /// Creates a temporary directory for the test fixture.
    /// </summary>
    /// <returns>The path to the created temporary directory.</returns>
    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DotCompute.Tests", $"fixture_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);
        return tempDir;
    }
    
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                try
                {
                    if (Directory.Exists(TempDirectory))
                    {
                        Directory.Delete(TempDirectory, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            
            _disposed = true;
        }
    }


    /// <summary>
    /// Performs asynchronous cleanup operations.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual ValueTask DisposeAsyncCore()
        // Perform any async cleanup here
        => ValueTask.CompletedTask;


    /// <summary>
    /// Finalizer for CommonTestFixture.
    /// </summary>
    ~CommonTestFixture()
    {
        Dispose(false);
    }
}

/// <summary>
/// Collection definition for shared test fixture.
/// Use this with [Collection(SharedFixtureCollection.Name)] on test classes.
/// </summary>
[CollectionDefinition(Name)]
public class SharedFixtureCollection : ICollectionFixture<CommonTestFixture>
{
    public const string Name = "SharedFixture";
}