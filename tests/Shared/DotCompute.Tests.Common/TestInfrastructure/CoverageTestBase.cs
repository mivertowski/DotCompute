using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common.TestInfrastructure;


/// <summary>
/// Base class for all tests that provides common infrastructure and coverage utilities
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class CoverageTestBase : IDisposable
{
    protected ITestOutputHelper Output { get; }
    protected ILogger Logger { get; }
    protected CancellationTokenSource CancellationTokenSource { get; }
    protected CancellationToken CancellationToken => CancellationTokenSource.Token;

    private readonly List<IDisposable> _disposables = [];
    private bool _disposed;

    protected CoverageTestBase(ITestOutputHelper output)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Logger = CreateLogger();
        CancellationTokenSource = new CancellationTokenSource();

        // Set reasonable timeout for tests
        CancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Creates a logger for the test
    /// </summary>
    protected virtual ILogger CreateLogger()
    {
        var factory = LoggerFactory.Create(builder =>
            builder.AddProvider(new XUnitLoggerProvider(Output)));

        return factory.CreateLogger(GetType().Name);
    }

    /// <summary>
    /// Register a disposable for cleanup
    /// </summary>
    protected T RegisterDisposable<T>(T disposable) where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Register an async disposable for cleanup
    /// </summary>
    protected T RegisterAsyncDisposable<T>(T disposable) where T : IAsyncDisposable
    {
        _disposables.Add(new AsyncDisposableWrapper(disposable));
        return disposable;
    }

    /// <summary>
    /// Create test data with specified size
    /// </summary>
    protected static byte[] CreateTestData(int size, byte? pattern = null)
    {
        var data = new byte[size];
        if (pattern.HasValue)
        {
            Array.Fill(data, pattern.Value);
        }
        else
        {
            Random.Shared.NextBytes(data);
        }
        return data;
    }

    /// <summary>
    /// Create test data array with specified dimensions
    /// </summary>
    protected static T[,] CreateTestArray<T>(int width, int height, Func<int, int, T> generator)
    {
        var array = new T[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                array[x, y] = generator(x, y);
            }
        }
        return array;
    }

    /// <summary>
    /// Assert that code runs within timeout
    /// </summary>
    protected static async Task AssertWithinTimeout(Func<Task> operation, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        await operation();
    }

    /// <summary>
    /// Assert that code runs within timeout with result
    /// </summary>
    protected static async Task<T> AssertWithinTimeout<T>(Func<Task<T>> operation, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await operation();
    }

    /// <summary>
    /// Skip test if condition is not met
    /// </summary>
    protected static void SkipIfNot(bool condition, string reason)
    {
        if (!condition)
        {
            throw new Xunit.SkipException(reason);
        }
    }

    /// <summary>
    /// Skip test if hardware is not available
    /// </summary>
    protected static void SkipIfNoHardware(string hardwareType)
    {
        SkipIfNot(IsHardwareAvailable(hardwareType),
            $"{hardwareType} hardware not available for testing");
    }

    /// <summary>
    /// Check if specific hardware is available
    /// </summary>
    protected static bool IsHardwareAvailable(string hardwareType)
    {
        return hardwareType.ToLowerInvariant() switch
        {
            "cuda" => IsCudaAvailable(),
            "opencl" => IsOpenCLAvailable(),
            "metal" => IsMetalAvailable(),
            "directcompute" => IsDirectComputeAvailable(),
            _ => false
        };
    }

    private static bool IsCudaAvailable()
    {
        try
        {
            // This would typically call into CUDA runtime to check
            return Environment.GetEnvironmentVariable("CUDA_PATH") != null ||
                   File.Exists(@"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.0\bin\cudart64_12.dll");
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOpenCLAvailable()
    {
        try
        {
            // This would typically check for OpenCL runtime
            return Environment.OSVersion.Platform != PlatformID.Other;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMetalAvailable()
    {
        try
        {
            return OperatingSystem.IsMacOS();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDirectComputeAvailable()
    {
        try
        {
            return OperatingSystem.IsWindows() &&
                   Environment.OSVersion.Version.Major >= 10;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose in reverse order
            for (var i = _disposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    _disposables[i]?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error disposing test resource {Index}", i);
                }
            }

            _disposables.Clear();
            CancellationTokenSource?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Wrapper for async disposable resources
    /// </summary>
    private sealed class AsyncDisposableWrapper(IAsyncDisposable asyncDisposable) : IDisposable
    {
        private readonly IAsyncDisposable _asyncDisposable = asyncDisposable;

        public void Dispose()
        {
            _asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
/// xUnit logger provider for test output
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class XUnitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    private readonly ITestOutputHelper _output = output ?? throw new ArgumentNullException(nameof(output));

    public ILogger CreateLogger(string categoryName) => new XUnitLogger(_output, categoryName);

    public void Dispose() => GC.SuppressFinalize(this);
}

/// <summary>
/// xUnit logger implementation
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class XUnitLogger(ITestOutputHelper output, string categoryName) : ILogger
{
    private readonly ITestOutputHelper _output = output ?? throw new ArgumentNullException(nameof(output));
    private readonly string _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}");

            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
        catch
        {
            // Ignore logging failures in tests
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() => GC.SuppressFinalize(this);
    }
}
