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

    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

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

    public AcceleratorInfo Info { get; }
    public AcceleratorType Type => AcceleratorType.CPU;
    public IMemoryManager Memory => _memoryManager;

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await Task.Delay(10, cancellationToken); // Simulate compilation time

        var kernel = new TestCompiledKernel(
            definition.Name,
            definition.Code,
            options ?? new CompilationOptions());

        _compiledKernels[definition.Name] = kernel;
        return kernel;
    }

    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => await Task.Yield(); // Simulate synchronization

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

    public string Name { get; } = name;

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

    public long ExecutionCount => _executionCount;
    public TimeSpan TotalExecutionTime => _executionTimer.Elapsed;
    public double AverageExecutionTimeMs => _executionCount > 0
        ? _executionTimer.Elapsed.TotalMilliseconds / _executionCount
        : 0;

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
