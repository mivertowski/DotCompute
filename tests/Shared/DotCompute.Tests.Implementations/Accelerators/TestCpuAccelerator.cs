using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Tests.Implementations.Memory;

namespace DotCompute.Tests.Implementations.Accelerators;


/// <summary>
/// Test CPU-based accelerator implementation for testing without GPU hardware.
/// </summary>
public sealed class TestCpuAccelerator : IAccelerator
{
    private readonly TestMemoryManager _memoryManager;
    private readonly ConcurrentDictionary<string, TestCompiledKernel> _compiledKernels;
    private bool _disposed;

    /// <summary>
    /// Gets the accelerator context.
    /// </summary>
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="TestCpuAccelerator"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    public TestCpuAccelerator(string name = "Test CPU Accelerator")
    {
        Info = new AcceleratorInfo
        {
            Id = $"test_cpu_{Guid.NewGuid():N}",
            Name = name,
            DeviceType = "CPU",
            Vendor = "Test",
            DriverVersion = "1.0.0",
            TotalMemory = Environment.WorkingSet,
            AvailableMemory = Environment.WorkingSet,
            MaxThreadsPerBlock = Environment.ProcessorCount * 4,
            ComputeUnits = Environment.ProcessorCount,
            MaxClockFrequency = 3000,
            IsUnifiedMemory = true,
            ComputeCapability = new Version(1, 0)
        };

        _memoryManager = new TestMemoryManager();
        _compiledKernels = new ConcurrentDictionary<string, TestCompiledKernel>();
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public AcceleratorInfo Info { get; }

    /// <summary>
    /// Gets the accelerator type.
    /// </summary>
    public AcceleratorType Type => AcceleratorType.CPU;

    /// <summary>
    /// Gets memory manager for this accelerator.
    /// </summary>
    public IMemoryManager Memory => _memoryManager;

    /// <summary>
    /// Compiles a kernel for execution.
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await Task.Delay(10, cancellationToken); // Simulate compilation time

        var kernel = new TestCompiledKernel(
            definition.Name,
            System.Text.Encoding.UTF8.GetBytes(definition.Code ?? string.Empty),
            options ?? new CompilationOptions());

        _compiledKernels[definition.Name] = kernel;
        return kernel;
    }

    /// <summary>
    /// Synchronizes all pending operations.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => await Task.Yield(); // Simulate synchronization

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await SynchronizeAsync();
            _memoryManager.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    public void Dispose() => DisposeAsync().AsTask().Wait();
}

/// <summary>
/// Test implementation of a compiled kernel.
/// </summary>
#pragma warning disable CS9113 // Parameter is unread
public sealed class TestCompiledKernel(string name, byte[] code, CompilationOptions options) : ICompiledKernel
#pragma warning restore CS9113
{
    private readonly Stopwatch _executionTimer = new();
    private long _executionCount;
    private bool _disposed;

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Executes the kernel with given arguments.
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _executionTimer.Restart();

        // Simulate kernel execution with parallel CPU computation
        await Task.Run(() =>
        {
            // Simulate different execution patterns based on kernel name
            var iterations = Name.Contains("simple", StringComparison.OrdinalIgnoreCase) ? 100 : 1000;

            _ = Parallel.For(0, iterations, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, i =>
            {
                // Simulate compute work
                var result = 0.0;
                for (var j = 0; j < 100; j++)
                {
                    result += Math.Sin(i * j * 0.001) * Math.Cos(i * j * 0.001);
                }
            });
        }, cancellationToken);

        _executionTimer.Stop();
        _ = Interlocked.Increment(ref _executionCount);
    }

    /// <summary>
    /// Gets the execution count.
    /// </summary>
    /// <value>
    /// The execution count.
    /// </value>
    public long ExecutionCount => _executionCount;

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    /// <value>
    /// The total execution time.
    /// </value>
    public TimeSpan TotalExecutionTime => _executionTimer.Elapsed;

    /// <summary>
    /// Gets the average execution time ms.
    /// </summary>
    /// <value>
    /// The average execution time ms.
    /// </value>
    public double AverageExecutionTimeMs => _executionCount > 0
        ? _executionTimer.Elapsed.TotalMilliseconds / _executionCount
        : 0;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await Task.CompletedTask;
            GC.SuppressFinalize(this);
        }
    }
}
