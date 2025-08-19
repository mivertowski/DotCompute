using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Utilities.TestFixtures;


/// <summary>
/// Shared test fixture for memory-related tests.
/// </summary>
public sealed class MemoryTestFixture(ITestOutputHelper? output = null) : IAsyncLifetime, IDisposable
{
    private readonly List<IMemoryBuffer> _allocatedBuffers = [];
    private readonly ITestOutputHelper? _output = output;
    private readonly IMemoryManager? _memoryManager = null;
    private bool _disposed;

    /// <summary>
    /// Gets the memory manager.
    /// </summary>
    public IMemoryManager? MemoryManager => _memoryManager;

    /// <summary>
    /// Gets the total allocated memory.
    /// </summary>
    public long TotalAllocatedMemory => CalculateTotalAllocatedMemory();

    /// <summary>
    /// Creates a test buffer with specified pattern.
    /// </summary>
    public async Task<IMemoryBuffer> CreateTestBufferAsync<T>(
        int elementCount,
        Func<int, T> valueGenerator,
        MemoryOptions options = MemoryOptions.None) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(_memoryManager, "Memory manager not initialized");

        var sizeInBytes = elementCount * Marshal.SizeOf<T>();
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes, options);
        _allocatedBuffers.Add(buffer);

        // Initialize buffer with test data
        var data = new T[elementCount];
        for (var i = 0; i < elementCount; i++)
        {
            data[i] = valueGenerator(i);
        }

        await buffer.CopyFromHostAsync<T>(data.AsMemory());
        return buffer;
    }

    /// <summary>
    /// Creates a random test buffer.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Used for test data generation only")]
    public async Task<IMemoryBuffer> CreateRandomBufferAsync(long sizeInBytes, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(_memoryManager, "Memory manager not initialized");

        var buffer = await _memoryManager.AllocateAsync(sizeInBytes);
        _allocatedBuffers.Add(buffer);

        // Fill with random data
        var random = new Random(seed);
        var data = new byte[sizeInBytes];
        random.NextBytes(data);

        await buffer.CopyFromHostAsync<byte>(data.AsMemory());
        return buffer;
    }

    /// <summary>
    /// Verifies buffer contents match expected pattern.
    /// </summary>
    public async Task<bool> VerifyBufferAsync<T>(
        IMemoryBuffer buffer,
        Func<int, T> expectedValueGenerator,
        double tolerance = 1e-6) where T : unmanaged, IComparable<T>
    {
        var elementCount = (int)(buffer.SizeInBytes / Marshal.SizeOf<T>());
        var hostData = new T[elementCount];

        await buffer.CopyToHostAsync<T>(hostData.AsMemory());

        for (var i = 0; i < elementCount; i++)
        {
            var expected = expectedValueGenerator(i);
            var actual = hostData[i];

            if (typeof(T) == typeof(float) || typeof(T) == typeof(double))
            {
                // For floating point, use tolerance
                var diff = Math.Abs(Convert.ToDouble(expected) - Convert.ToDouble(actual));
                if (diff > tolerance)
                {
                    _output?.WriteLine($"Mismatch at index {i}: expected {expected}, got {actual}");
                    return false;
                }
            }
            else
            {
                // For other types, use exact comparison
                if (actual.CompareTo(expected) != 0)
                {
                    _output?.WriteLine($"Mismatch at index {i}: expected {expected}, got {actual}");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Performs memory stress test by allocating and deallocating buffers.
    /// </summary>
    public async Task<MemoryStressTestResult> RunMemoryStressTestAsync(
        int iterations,
        long minSize,
        long maxSize,
        int concurrency = 4)
    {
        ArgumentNullException.ThrowIfNull(_memoryManager, "Memory manager not initialized");

        var result = new MemoryStressTestResult
        {
            Iterations = iterations,
            StartTime = DateTime.UtcNow
        };

        var random = new Random();
        var tasks = new List<Task>();
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);

        for (var i = 0; i < iterations; i++)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var size = random.NextInt64(minSize, maxSize);
                    var buffer = await _memoryManager.AllocateAsync(size);

                    // Perform some operations
                    var data = new byte[Math.Min(1024, size)];
                    await buffer.CopyFromHostAsync<byte>(data.AsMemory());
                    await buffer.CopyToHostAsync<byte>(data.AsMemory());

                    await buffer.DisposeAsync();

                    var success = result.SuccessfulAllocations;
                    result.SuccessfulAllocations = success + 1;
                }
                catch (Exception ex)
                {
                    var failed = result.FailedAllocations;
                    result.FailedAllocations = failed + 1;
                    _output?.WriteLine($"Allocation failed: {ex.Message}");
                }
                finally
                {
                    _ = semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;
        result.PeakMemoryUsage = GC.GetTotalMemory(false);

        return result;
    }

    private long CalculateTotalAllocatedMemory() => _allocatedBuffers.Sum(b => b.SizeInBytes);

    public async Task InitializeAsync()
    {
        _output?.WriteLine("Initializing memory test fixture...");

        // TODO: Initialize real memory manager when available
        // For now, we'll use a simple implementation

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        foreach (var buffer in _allocatedBuffers)
        {
            await buffer.DisposeAsync();
        }
        _allocatedBuffers.Clear();

        _disposed = true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}

/// <summary>
/// Result of memory stress test.
/// </summary>
public sealed class MemoryStressTestResult
{
    public int Iterations { get; set; }
    public int SuccessfulAllocations { get; set; }
    public int FailedAllocations { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public long PeakMemoryUsage { get; set; }

    public double SuccessRate
        => Iterations > 0 ? (double)SuccessfulAllocations / Iterations * 100 : 0;

    public double AllocationsPerSecond
        => Duration.TotalSeconds > 0 ? SuccessfulAllocations / Duration.TotalSeconds : 0;
}
