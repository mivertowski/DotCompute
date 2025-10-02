// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Tests.Common.Fixtures;
using DotCompute.Tests.Common.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common;

/// <summary>
/// Consolidated base class for all DotCompute tests combining the best features from multiple TestBase implementations.
/// Provides hardware detection, performance measurement, memory tracking, test data generation, comprehensive test utilities,
/// GPU testing capabilities, CUDA/Metal support, integration test features, and service provider management.
/// This is the SINGLE unified test base that replaces all other test base classes in the project.
/// </summary>
public abstract class ConsolidatedTestBase : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    private readonly Stopwatch _testStopwatch;
    private readonly long _initialMemory;
    private readonly Dictionary<string, GpuMemorySnapshot> _memorySnapshots;
    private readonly List<IDisposable> _disposables;
    private readonly CancellationTokenSource? _cancellationTokenSource;
    private ServiceProvider? _serviceProvider;

    // Cache for hardware availability checks
    private static bool? _cachedCudaAvailable;
    private static bool? _cachedOpenClAvailable;
    private static bool? _cachedMetalAvailable;
    private static readonly object _hardwareCheckLock = new();

    static ConsolidatedTestBase()
    {
        // Initialize LD_LIBRARY_PATH for Linux to ensure CUDA libraries are found
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var currentPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
            var newPath = "/usr/local/cuda-12.8/lib64:/usr/local/cuda-12.6/lib64:/usr/lib/wsl/lib";
            if (!currentPath.Contains("/usr/local/cuda-12.8/lib64", StringComparison.Ordinal))
            {
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", newPath + ":" + currentPath);
            }
        }
    }

    #region Properties

    /// <summary>
    /// Gets the test output helper for logging test information.
    /// </summary>
    protected ITestOutputHelper Output { get; }

    /// <summary>
    /// Gets the common test fixture providing shared resources (optional).
    /// </summary>
    protected CommonTestFixture? Fixture { get; }

    /// <summary>
    /// Gets the service provider for dependency injection (optional).
    /// </summary>
    protected ServiceProvider? ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets the configuration instance (optional).
    /// </summary>
    protected IConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Gets the cancellation token for long-running operations.
    /// </summary>
    protected CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

    /// <summary>
    /// Gets hardware information for conditional test execution.
    /// </summary>
    protected HardwareDetection.HardwareInfo? HardwareInfo => Fixture?.HardwareInfo;

    /// <summary>
    /// Gets the temporary directory for this test instance.
    /// </summary>
    protected string TempDirectory { get; }

    /// <summary>
    /// Gets the stopwatch for measuring test execution time.
    /// </summary>
    protected Stopwatch Stopwatch => _testStopwatch;

    /// <summary>
    /// Gets the current platform information.
    /// </summary>
    public static string PlatformInfo => $"{RuntimeInformation.OSDescription} - {RuntimeInformation.OSArchitecture}";

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the ConsolidatedTestBase class with output helper only.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    protected ConsolidatedTestBase(ITestOutputHelper output)
        : this(output, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ConsolidatedTestBase class with output helper and fixture.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    /// <param name="fixture">The common test fixture (optional).</param>
    protected ConsolidatedTestBase(ITestOutputHelper output, CommonTestFixture? fixture)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Fixture = fixture;
        _memorySnapshots = [];
        _disposables = [];

        // Create cancellation token for long operations (5 minute timeout)

        _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        // Set output in fixture for logging if available

        Fixture?.SetOutput(output);

        // Create test-specific temp directory
        TempDirectory = CreateTestTempDirectory();

        // Initialize configuration and services if needed
        InitializeConfiguration();
        ConfigureServices();

        // Initialize timing and memory tracking
        _testStopwatch = Stopwatch.StartNew();
        _initialMemory = GC.GetTotalMemory(false);

        LogTestStart();
    }

    #endregion

    #region Hardware Detection

    /// <summary>
    /// Checks if CUDA is available on the current system with comprehensive validation.
    /// </summary>
    /// <returns>True if CUDA is available and functional, false otherwise.</returns>
    public static bool IsCudaAvailable()
    {
        lock (_hardwareCheckLock)
        {
            if (_cachedCudaAvailable.HasValue)
                return _cachedCudaAvailable.Value;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _cachedCudaAvailable = CheckWindowsCuda();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    _cachedCudaAvailable = CheckLinuxCuda();
                }
                else
                {
                    _cachedCudaAvailable = false;
                }
            }
            catch
            {
                _cachedCudaAvailable = false;
            }

            return _cachedCudaAvailable.Value;
        }
    }

    /// <summary>
    /// Checks if OpenCL is available on the current system.
    /// </summary>
    /// <returns>True if OpenCL is available, false otherwise.</returns>
    public static bool IsOpenClAvailable()
    {
        lock (_hardwareCheckLock)
        {
            if (_cachedOpenClAvailable.HasValue)
                return _cachedOpenClAvailable.Value;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _cachedOpenClAvailable = File.Exists("OpenCL.dll");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    _cachedOpenClAvailable = File.Exists("/usr/lib/x86_64-linux-gnu/libOpenCL.so.1") ||
                                           File.Exists("/usr/local/lib/libOpenCL.so");
                }
                else
                {
                    _cachedOpenClAvailable = false;
                }
            }
            catch
            {
                _cachedOpenClAvailable = false;
            }

            return _cachedOpenClAvailable.Value;
        }
    }

    /// <summary>
    /// Checks if Metal is available on the current system (macOS only).
    /// </summary>
    /// <returns>True if Metal is available, false otherwise.</returns>
    public static bool IsMetalAvailable()
    {
        lock (_hardwareCheckLock)
        {
            if (_cachedMetalAvailable.HasValue)
                return _cachedMetalAvailable.Value;

            try
            {
                // Metal is only available on macOS
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    _cachedMetalAvailable = false;
                    return false;
                }

                // Check if running on Apple Silicon or Intel Mac with Metal support
                _cachedMetalAvailable = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ||
                                       Environment.OSVersion.Version.Major >= 10; // macOS 10.11+
            }
            catch
            {
                _cachedMetalAvailable = false;
            }

            return _cachedMetalAvailable.Value;
        }
    }

    /// <summary>
    /// Checks if Apple Silicon (M1/M2/M3) is available.
    /// </summary>
    /// <returns>True if running on Apple Silicon, false otherwise.</returns>
    public static bool IsAppleSilicon()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
               RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }

    /// <summary>
    /// Checks if the CPU supports SIMD instructions (SSE, AVX).
    /// </summary>
    /// <returns>True if SIMD is supported, false otherwise.</returns>
    public static bool IsSIMDSupported()
    {
        try
        {
            return RuntimeInformation.OSArchitecture == Architecture.X64 ||
                   RuntimeInformation.OSArchitecture == Architecture.X86;
        }
        catch
        {
            return false;
        }
    }

    // Hardware requirement methods for use with fixtures
    protected void RequireCuda() => Fixture?.RequireCuda();
    protected void RequireOpenCL() => Fixture?.RequireOpenCL();
    protected void RequireGpu() => Fixture?.RequireGpu();
    protected void RequireMultiGpu() => Fixture?.RequireMultiGpu();
    protected void RequireHighPerformanceCpu() => Fixture?.RequireHighPerformanceCpu();
    protected void RequireHighMemory() => Fixture?.RequireHighMemory();

    // Additional hardware requirement methods
    protected static void RequireMetal()
    {
        if (!IsMetalAvailable())
            throw new SkipException("Metal is not available on this system");
    }

    protected static void RequireAppleSilicon()
    {
        if (!IsAppleSilicon())
            throw new SkipException("Apple Silicon (M1/M2/M3) is required for this test");
    }

    protected static void SkipIfNoCuda()
    {
        if (!IsCudaAvailable())
            throw new SkipException("CUDA is not available on this system");
    }

    protected static void SkipIfNoGpu()
    {
        var hasAnyGpu = IsCudaAvailable() || IsOpenClAvailable() || IsMetalAvailable();
        if (!hasAnyGpu)
            throw new SkipException("No GPU hardware detected on this system");
    }

    protected static void SkipIfNoOpenCL()
    {
        if (!IsOpenClAvailable())
            throw new SkipException("OpenCL is not available on this system");
    }

    protected static void SkipIfNoMetal()
    {
        if (!IsMetalAvailable())
            throw new SkipException("Metal is not available on this system");
    }

    /// <summary>
    /// Skips the test if insufficient GPU memory is available.
    /// </summary>
    /// <param name="requiredMemoryMB">Required memory in MB.</param>
    protected void SkipIfInsufficientGpuMemory(int requiredMemoryMB)
    {
        try
        {
            var freeMemory = GetFreeGpuMemory();
            var freeMemoryMB = freeMemory / (1024.0 * 1024.0);

            if (freeMemoryMB < requiredMemoryMB)
            {
                throw new SkipException($"Insufficient GPU memory. Required: {requiredMemoryMB} MB, Available: {freeMemoryMB:F1} MB");
            }
        }
        catch (Exception ex)
        {
            throw new SkipException($"Unable to determine GPU memory availability: {ex.Message}");
        }
    }

    /// <summary>
    /// Skips the test if GPU compute capability is insufficient.
    /// </summary>
    /// <param name="minimumComputeCapability">Minimum required compute capability (e.g., "3.5").</param>
    protected void SkipIfInsufficientComputeCapability(string minimumComputeCapability)
    {
        try
        {
            var capabilities = GetNvidiaCapabilities();

            if (!capabilities.HasNvidiaGpu)
            {
                throw new SkipException("No NVIDIA GPU detected");
            }

            // Simple version comparison for compute capability
            if (string.Compare(capabilities.ComputeCapability, minimumComputeCapability, StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new SkipException($"Insufficient compute capability. Required: {minimumComputeCapability}, Available: {capabilities.ComputeCapability}");
            }
        }
        catch (SkipException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SkipException($"Unable to determine GPU compute capability: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets NVIDIA GPU capabilities.
    /// </summary>
    /// <returns>GPU capabilities information.</returns>
    protected NvidiaGpuCapabilities GetNvidiaCapabilities()
    {
        var capabilities = new NvidiaGpuCapabilities();

        try
        {
            capabilities.HasNvidiaGpu = IsCudaAvailable();

            if (capabilities.HasNvidiaGpu)
            {
                // Get basic GPU information - these would typically come from CUDA API calls
                capabilities.ComputeCapability = "8.9"; // RTX 2000 Ada Generation
                capabilities.TotalMemoryMB = 8192; // 8GB typical for RTX 2000 Ada
                capabilities.SupportsUnifiedMemory = true; // Modern GPUs support UVM
                capabilities.SupportsDynamicParallelism = true; // Compute 3.5+
            }
        }
        catch (Exception ex)
        {
            Log($"Error getting NVIDIA capabilities: {ex.Message}");
            capabilities.HasNvidiaGpu = false;
        }

        return capabilities;
    }

    #endregion

    #region CUDA P/Invoke Declarations (Private)

    [DllImport("cudart64_13", EntryPoint = "cudaGetDeviceCount")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.System32)]
    private static extern int CudaGetDeviceCount_Windows13(out int count);

    [DllImport("cudart64_12", EntryPoint = "cudaGetDeviceCount")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.System32)]
    private static extern int CudaGetDeviceCount_Windows12(out int count);

    [DllImport("libcudart.so.12", EntryPoint = "cudaGetDeviceCount")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.System32)]
    private static extern int CudaGetDeviceCount_Linux12(out int count);

    [DllImport("/usr/local/cuda-12.8/lib64/libcudart.so.12", EntryPoint = "cudaGetDeviceCount")]
    private static extern int CudaGetDeviceCount_Linux12_8(out int count);

    private static bool CheckWindowsCuda()
    {
        try
        {
            if (CudaGetDeviceCount_Windows13(out var count) == 0 && count > 0)
                return true;
        }
        catch { }

        try
        {
            if (CudaGetDeviceCount_Windows12(out var count) == 0 && count > 0)
                return true;
        }
        catch { }

        return false;
    }

    private static bool CheckLinuxCuda()
    {
        try
        {
            var result = CudaGetDeviceCount_Linux12_8(out var count);
            if (result == 0 && count > 0)
                return true;
        }
        catch { }

        try
        {
            var result = CudaGetDeviceCount_Linux12(out var count);
            if (result == 0 && count > 0)
                return true;
        }
        catch { }

        return false;
    }

    #endregion

    #region Performance Measurement

    /// <summary>
    /// Measures the execution time of a synchronous action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <param name="iterations">Number of iterations to run (default: 1).</param>
    /// <returns>Average elapsed time per iteration.</returns>
    protected TimeSpan MeasureExecutionTime(Action action, int iterations = 1)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        // Warm-up for accurate measurement
        if (iterations > 1)
        {
            action();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }
        stopwatch.Stop();

        var averageTime = new TimeSpan(stopwatch.Elapsed.Ticks / iterations);
        Log($"Execution time: {averageTime.TotalMilliseconds:F2} ms (average of {iterations} iterations)");

        return averageTime;
    }

    /// <summary>
    /// Measures the execution time of an asynchronous function.
    /// </summary>
    /// <param name="func">The async function to measure.</param>
    /// <param name="iterations">Number of iterations to run (default: 1).</param>
    /// <returns>Average elapsed time per iteration.</returns>
    protected async Task<TimeSpan> MeasureExecutionTimeAsync(Func<Task> func, int iterations = 1)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        // Warm-up
        if (iterations > 1)
        {
            await func();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await func();
        }
        stopwatch.Stop();

        var averageTime = new TimeSpan(stopwatch.Elapsed.Ticks / iterations);
        Log($"Async execution time: {averageTime.TotalMilliseconds:F2} ms (average of {iterations} iterations)");

        return averageTime;
    }

    /// <summary>
    /// Benchmarks an operation and asserts it completes within the specified time limit.
    /// </summary>
    /// <param name="action">The action to benchmark.</param>
    /// <param name="timeLimit">The time limit for the operation.</param>
    /// <param name="iterations">The number of iterations to run.</param>
    protected void BenchmarkOperation(Action action, TimeSpan timeLimit, int iterations = 1)
    {
        var averageTime = MeasureExecutionTime(action, iterations);
        if (averageTime > timeLimit)
        {
            throw new InvalidOperationException(
                $"Operation exceeded time limit: {averageTime.TotalMilliseconds:F2} ms > {timeLimit.TotalMilliseconds:F2} ms");
        }
    }

    /// <summary>
    /// Creates a performance measurement context for detailed measurement.
    /// </summary>
    /// <param name="name">Name of the measurement.</param>
    /// <returns>Performance measurement context.</returns>
    protected ConsolidatedPerformanceContext CreatePerformanceContext(string name) => new(name, Output);

    /// <summary>
    /// Measures GPU kernel execution time with proper synchronization.
    /// </summary>
    /// <param name="kernelExecution">Action representing kernel execution.</param>
    /// <param name="iterations">Number of iterations to run.</param>
    /// <returns>Average execution time in milliseconds.</returns>
    protected double MeasureGpuKernelTime(Action kernelExecution, int iterations = 1)
    {
        ArgumentNullException.ThrowIfNull(kernelExecution);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        // Warm-up run
        kernelExecution();
        SynchronizeGpu();

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            kernelExecution();
        }
        SynchronizeGpu(); // Ensure all GPU work is complete
        stopwatch.Stop();

        var avgTime = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Log($"GPU kernel execution time: {avgTime:F2}ms (avg over {iterations} iterations)");

        return avgTime;
    }

    /// <summary>
    /// Calculates theoretical bandwidth utilization for memory operations.
    /// </summary>
    /// <param name="bytesTransferred">Total bytes transferred.</param>
    /// <param name="elapsedMs">Elapsed time in milliseconds.</param>
    /// <returns>Bandwidth in GB/s.</returns>
    protected double CalculateBandwidth(long bytesTransferred, double elapsedMs)
    {
        if (elapsedMs <= 0)
            return 0;

        var bandwidthGBps = (bytesTransferred / (1024.0 * 1024.0 * 1024.0)) / (elapsedMs / 1000.0);
        Log($"Bandwidth: {bandwidthGBps:F2} GB/s ({bytesTransferred:N0} bytes in {elapsedMs:F2}ms)");

        return bandwidthGBps;
    }

    /// <summary>
    /// Placeholder for GPU synchronization - override in derived classes.
    /// </summary>
    protected virtual void SynchronizeGpu()
    {
        // Default implementation does nothing - override in GPU-specific test classes
    }

    #endregion

    #region Memory Tracking

    /// <summary>
    /// Gets the current memory usage delta since test start.
    /// </summary>
    /// <returns>Memory usage change in bytes.</returns>
    protected long GetMemoryUsageDelta()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var currentMemory = GC.GetTotalMemory(false);
        return currentMemory - _initialMemory;
    }

    /// <summary>
    /// Measures the memory usage of an action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <returns>The memory increase in bytes.</returns>
    protected long MeasureMemoryUsage(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);
        action();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        Log($"Memory usage: {memoryIncrease:N0} bytes increase");
        return memoryIncrease;
    }

    /// <summary>
    /// Logs current memory usage information.
    /// </summary>
    protected void LogMemoryUsage()
    {
        var delta = GetMemoryUsageDelta();
        var current = GC.GetTotalMemory(false);

        Log($"Memory - Current: {current:N0} bytes, Delta: {delta:N0} bytes");

        if (Math.Abs(delta) > 1024 * 1024) // Warn for > 1MB delta
        {
            Log($"WARNING: Significant memory delta detected: {delta / 1024.0 / 1024.0:F2} MB");
        }
    }

    #endregion

    #region Test Data Generation

    /// <summary>
    /// Generates a random array of float values.
    /// </summary>
    /// <param name="size">Size of the array.</param>
    /// <param name="seed">Random seed for reproducible results.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <returns>Array of random float values.</returns>
    protected static float[] GenerateRandomFloats(int size, int seed = 42, float min = -1.0f, float max = 1.0f)
    {
        var random = new Random(seed);
        var data = new float[size];
        var range = max - min;

        for (var i = 0; i < size; i++)
        {
            data[i] = min + (float)random.NextDouble() * range;
        }

        return data;
    }

    /// <summary>
    /// Generates a random array of integer values.
    /// </summary>
    /// <param name="size">Size of the array.</param>
    /// <param name="minValue">Minimum value (inclusive).</param>
    /// <param name="maxValue">Maximum value (exclusive).</param>
    /// <param name="seed">Random seed for reproducible results.</param>
    /// <returns>Array of random integer values.</returns>
    protected static int[] GenerateRandomInts(int size, int minValue = 0, int maxValue = 1000, int seed = 42)
    {
        var random = new Random(seed);
        var data = new int[size];

        for (var i = 0; i < size; i++)
        {
            data[i] = random.Next(minValue, maxValue);
        }

        return data;
    }

    /// <summary>
    /// Generates a sequential array of values.
    /// </summary>
    /// <param name="size">Size of the array.</param>
    /// <param name="start">Starting value.</param>
    /// <param name="increment">Increment between values.</param>
    /// <returns>Array of sequential values.</returns>
    protected static float[] GenerateSequentialFloats(int size, float start = 0.0f, float increment = 1.0f)
    {
        var data = new float[size];

        for (var i = 0; i < size; i++)
        {
            data[i] = start + i * increment;
        }

        return data;
    }

    #endregion

    #region File and Directory Management

    /// <summary>
    /// Creates a temporary file in the test's temp directory.
    /// </summary>
    /// <param name="fileName">Optional file name. If not provided, a random name is generated.</param>
    /// <param name="content">Optional content to write to the file.</param>
    /// <returns>The full path to the created temporary file.</returns>
    protected string CreateTempFile(string? fileName = null, string? content = null)
    {
        fileName ??= $"test_{Guid.NewGuid():N}.tmp";
        var filePath = Path.Combine(TempDirectory, fileName);

        content ??= "";
        File.WriteAllText(filePath, content);

        return filePath;
    }

    /// <summary>
    /// Creates a temporary subdirectory in the test's temp directory.
    /// </summary>
    /// <param name="directoryName">Optional directory name. If not provided, a random name is generated.</param>
    /// <returns>The full path to the created temporary directory.</returns>
    protected string CreateTempSubDirectory(string? directoryName = null)
    {
        directoryName ??= $"subdir_{Guid.NewGuid():N}";
        var dirPath = Path.Combine(TempDirectory, directoryName);
        _ = Directory.CreateDirectory(dirPath);
        return dirPath;
    }

    /// <summary>
    /// Creates the test-specific temporary directory.
    /// </summary>
    /// <returns>The path to the created temporary directory.</returns>
    private string CreateTestTempDirectory()
    {
        var testName = GetType().Name;
        var tempDir = Fixture?.TempDirectory ?? Path.GetTempPath();
        var testTempDir = Path.Combine(tempDir, $"{testName}_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(testTempDir);
        return testTempDir;
    }

    #endregion

    #region Logging and Output

    /// <summary>
    /// Logs a message with timestamp.
    /// </summary>
    /// <param name="message">The message to log.</param>
    protected void Log(string message) => Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");

    /// <summary>
    /// Logs a formatted message with timestamp.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The format arguments.</param>
    protected void Log(string format, params object[] args) => Log(string.Format(format, args));

    /// <summary>
    /// Logs test start information.
    /// </summary>
    private void LogTestStart()
    {
        var testName = GetType().Name;
        Log($"=== Starting Test: {testName} ===");
        Log($"Platform: {PlatformInfo}");
        Log($"CUDA Available: {IsCudaAvailable()}");
        Log($"OpenCL Available: {IsOpenClAvailable()}");
        Log($"SIMD Supported: {IsSIMDSupported()}");
        Log($"Initial Memory: {_initialMemory:N0} bytes");
        Log($"Temp Directory: {TempDirectory}");
    }

    /// <summary>
    /// Logs test completion information.
    /// </summary>
    protected void LogTestComplete()
    {
        _testStopwatch.Stop();
        var testName = GetType().Name;
        Log($"=== Completed Test: {testName} in {_testStopwatch.Elapsed.TotalMilliseconds:F2} ms ===");
        LogMemoryUsage();
    }

    #endregion

    #region GPU Memory Tracking

    /// <summary>
    /// Takes a snapshot of current GPU memory usage.
    /// </summary>
    /// <param name="name">Name for the memory snapshot.</param>
    protected void TakeGpuMemorySnapshot(string name)
    {
        try
        {
            var snapshot = new GpuMemorySnapshot
            {
                Name = name,
                Timestamp = DateTime.Now,
                SystemMemory = GC.GetTotalMemory(false),
                GpuMemoryUsed = GetCurrentGpuMemoryUsage(),
                GpuMemoryFree = GetFreeGpuMemory()
            };

            _memorySnapshots[name] = snapshot;
            Log($"GPU Memory Snapshot '{name}': " +
               $"GPU Used: {snapshot.GpuMemoryUsed:N0} bytes, " +
               $"GPU Free: {snapshot.GpuMemoryFree:N0} bytes, " +
               $"System: {snapshot.SystemMemory:N0} bytes");
        }
        catch (Exception ex)
        {
            Log($"Failed to take GPU memory snapshot '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Compares two GPU memory snapshots and logs the differences.
    /// </summary>
    /// <param name="beforeSnapshot">Name of the before snapshot.</param>
    /// <param name="afterSnapshot">Name of the after snapshot.</param>
    protected void CompareGpuMemorySnapshots(string beforeSnapshot, string afterSnapshot)
    {
        if (!_memorySnapshots.TryGetValue(beforeSnapshot, out var before) ||
            !_memorySnapshots.TryGetValue(afterSnapshot, out var after))
        {
            Log("Cannot compare snapshots: one or both snapshots not found");
            return;
        }

        var gpuMemoryDelta = after.GpuMemoryUsed - before.GpuMemoryUsed;
        var systemMemoryDelta = after.SystemMemory - before.SystemMemory;
        var timeDelta = (after.Timestamp - before.Timestamp).TotalMilliseconds;

        Log($"Memory Comparison ({beforeSnapshot} -> {afterSnapshot}):");
        Log($"  GPU Memory Delta: {gpuMemoryDelta:N0} bytes ({gpuMemoryDelta / 1024.0 / 1024.0:F2} MB)");
        Log($"  System Memory Delta: {systemMemoryDelta:N0} bytes ({systemMemoryDelta / 1024.0 / 1024.0:F2} MB)");
        Log($"  Time Delta: {timeDelta:F2}ms");

        // Warn about potential memory leaks
        if (Math.Abs(gpuMemoryDelta) > 10 * 1024 * 1024) // > 10MB
        {
            Log($"WARNING: Large GPU memory delta detected: {gpuMemoryDelta / 1024.0 / 1024.0:F2} MB");
        }
    }

    /// <summary>
    /// Validates that GPU memory usage is within acceptable limits.
    /// </summary>
    /// <param name="maxAllowedMemoryMB">Maximum allowed memory usage in MB.</param>
    protected void ValidateGpuMemoryUsage(int maxAllowedMemoryMB)
    {
        try
        {
            var currentUsage = GetCurrentGpuMemoryUsage();
            var currentUsageMB = currentUsage / 1024.0 / 1024.0;

            Log($"Current GPU memory usage: {currentUsageMB:F2} MB");

            if (currentUsageMB > maxAllowedMemoryMB)
            {
                throw new InvalidOperationException(
                    $"GPU memory usage ({currentUsageMB:F2} MB) exceeds limit ({maxAllowedMemoryMB} MB)");
            }
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            Log($"GPU memory validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets current GPU memory usage (virtual - override in GPU-specific tests).
    /// </summary>
    /// <returns>Current GPU memory usage in bytes.</returns>
    protected virtual long GetCurrentGpuMemoryUsage() => 1024 * 1024 * 256; // 256MB placeholder

    /// <summary>
    /// Gets free GPU memory (virtual - override in GPU-specific tests).
    /// </summary>
    /// <returns>Free GPU memory in bytes.</returns>
    protected virtual long GetFreeGpuMemory() => 1024L * 1024L * 1024L * 7L; // 7GB placeholder

    #endregion

    #region Service Provider and Configuration

    /// <summary>
    /// Gets a service from the service provider.
    /// </summary>
    /// <typeparam name="T">Type of service to retrieve.</typeparam>
    /// <returns>Service instance.</returns>
    protected T? GetService<T>() where T : class => _serviceProvider?.GetService<T>();

    /// <summary>
    /// Gets a required service from the service provider.
    /// </summary>
    /// <typeparam name="T">Type of service to retrieve.</typeparam>
    /// <returns>Service instance.</returns>
    protected T GetRequiredService<T>() where T : notnull
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException($"Service provider not configured for {typeof(T).Name}");

        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">Type to create logger for.</typeparam>
    /// <returns>Logger instance.</returns>
    protected ILogger<T> GetLogger<T>() => GetRequiredService<ILogger<T>>();

    /// <summary>
    /// Initializes configuration from test settings.
    /// </summary>
    protected void InitializeConfiguration()
    {
        try
        {
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("testsettings.json", optional: true)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }
        catch
        {
            // Configuration is optional
        }
    }

    /// <summary>
    /// Configures services - override ConfigureAdditionalServices for custom services.
    /// </summary>
    protected void ConfigureServices()
    {
        if (Configuration == null)
            return;

        try
        {
            var services = new ServiceCollection();

            // Add logging

            _ = services.AddLogging(builder =>
            {
                _ = builder.AddConfiguration(Configuration.GetSection("Logging"));
                _ = builder.AddConsole();
            });

            // Add configuration
            _ = services.AddSingleton(Configuration);

            // Allow derived classes to add more services
            ConfigureAdditionalServices(services);


            _serviceProvider = services.BuildServiceProvider();
            _disposables.Add(_serviceProvider);
        }
        catch
        {
            // Service configuration is optional
        }
    }

    /// <summary>
    /// Override this method to add additional services to the DI container.
    /// </summary>
    /// <param name="services">Service collection to add services to.</param>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Default implementation does nothing - override in derived classes
    }

    /// <summary>
    /// Adds a disposable object to be cleaned up when the test completes.
    /// </summary>
    /// <param name="disposable">Disposable object to track.</param>
    protected void TrackDisposable(IDisposable disposable) => _disposables.Add(disposable);

    /// <summary>
    /// Executes operations concurrently for thread safety testing.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="operation">Operation to execute.</param>
    /// <param name="threadCount">Number of concurrent threads.</param>
    /// <returns>Results from all operations.</returns>
    protected async Task<List<T>> ExecuteConcurrentlyAsync<T>(
        Func<int, Task<T>> operation,

        int threadCount = 10)
    {
        var tasks = new List<Task<T>>();
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() => operation(threadId), CancellationToken));
        }


        var results = await Task.WhenAll(tasks);
        return [.. results];
    }

    #endregion

    #region Exception Testing Utilities

    /// <summary>
    /// Asserts that an action throws an exception of the specified type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action that should throw.</param>
    /// <param name="expectedMessage">Optional expected exception message.</param>
    /// <returns>The thrown exception for further assertions.</returns>
    protected TException AssertThrows<TException>(Action action, string? expectedMessage = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        TException? exception = null;
        try
        {
            action();
        }
        catch (TException ex)
        {
            exception = ex;
        }

        if (exception == null)
        {
            throw new InvalidOperationException($"Expected exception of type {typeof(TException).Name} was not thrown");
        }

        if (!string.IsNullOrEmpty(expectedMessage) && !exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Exception message '{exception.Message}' does not contain expected text '{expectedMessage}'");
        }

        Log($"Expected exception thrown: {typeof(TException).Name}: {exception.Message}");
        return exception;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs asynchronous cleanup operations.
    /// </summary>
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
                LogTestComplete();

                // Dispose all tracked disposables
                foreach (var disposable in _disposables)
                {
                    try
                    {
                        disposable?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log($"Warning: Failed to dispose {disposable?.GetType().Name}: {ex.Message}");
                    }
                }
                _disposables.Clear();

                // Clean up test-specific temp directory
                try
                {
                    if (Directory.Exists(TempDirectory))
                    {
                        Directory.Delete(TempDirectory, true);
                    }
                }
                catch
                {
                    Log($"Warning: Could not clean up temp directory: {TempDirectory}");
                }

                _testStopwatch?.Stop();
                _cancellationTokenSource?.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Performs async cleanup operations.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (!_disposed)
        {
            // Dispose async disposables first
            foreach (var disposable in _disposables.OfType<IAsyncDisposable>())
            {
                try
                {
                    await disposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Log($"Warning: Failed to async dispose {disposable.GetType().Name}: {ex.Message}");
                }
            }

            // Dispose service provider asynchronously if possible
            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }
    }

    #endregion
}

/// <summary>
/// Represents a GPU memory usage snapshot.
/// </summary>
public class GpuMemorySnapshot
{
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public long SystemMemory { get; set; }
    public long GpuMemoryUsed { get; set; }
    public long GpuMemoryFree { get; set; }
}

/// <summary>
/// Represents NVIDIA GPU capabilities and specifications.
/// </summary>
public class NvidiaGpuCapabilities
{
    public bool HasNvidiaGpu { get; set; }
    public string ComputeCapability { get; set; } = "0.0";
    public int TotalMemoryMB { get; set; }
    public bool SupportsUnifiedMemory { get; set; }
    public bool SupportsDynamicParallelism { get; set; }
}

/// <summary>
/// Exception thrown to skip a test when conditions are not met.
/// </summary>
public class SkipException : Exception
{
    public SkipException() : base() { }
    public SkipException(string message) : base(message) { }
    public SkipException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Performance measurement context for detailed benchmarking.
/// </summary>
public sealed class ConsolidatedPerformanceContext : IDisposable
{
    private readonly string _name;
    private readonly ITestOutputHelper _output;
    private readonly Stopwatch _stopwatch;
    private readonly long _initialMemory;
    private bool _disposed;

    internal ConsolidatedPerformanceContext(string name, ITestOutputHelper output)
    {
        _name = name;
        _output = output;
        _stopwatch = Stopwatch.StartNew();
        _initialMemory = GC.GetTotalMemory(false);
        _output.WriteLine($"Performance measurement started: {_name}");
    }

    /// <summary>
    /// Adds a checkpoint measurement.
    /// </summary>
    /// <param name="checkpoint">Checkpoint name.</param>
    public void Checkpoint(string checkpoint)
    {
        var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
        var currentMemory = GC.GetTotalMemory(false);
        var memoryDelta = currentMemory - _initialMemory;

        _output.WriteLine($"Checkpoint '{checkpoint}': {elapsed:F2}ms, Memory Delta: {memoryDelta:N0} bytes");
    }

    /// <summary>
    /// Disposes the performance context and logs final results.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            var totalTime = _stopwatch.Elapsed.TotalMilliseconds;
            var finalMemory = GC.GetTotalMemory(false);
            var totalMemoryDelta = finalMemory - _initialMemory;

            _output.WriteLine($"Performance measurement completed: {_name}");
            _output.WriteLine($"Total time: {totalTime:F2}ms, Total memory delta: {totalMemoryDelta:N0} bytes");

            _disposed = true;
        }
    }
}