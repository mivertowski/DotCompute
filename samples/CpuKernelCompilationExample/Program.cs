// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CpuKernelCompilationExample;


/// <summary>
/// Example demonstrating CPU kernel compilation with optimization and vectorization.
/// This sample uses mock implementations to showcase the DotCompute.Abstractions API patterns
/// without requiring the full backend infrastructure.
/// </summary>
internal sealed class Program
{
    // Logger message delegates for performance and CA1848 compliance
    private static readonly Action<ILogger, string, Exception?> LogCreatedAccelerator =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogCreatedAccelerator)),
            "Created CPU accelerator: {Name}");

    private static readonly Action<ILogger, Exception?> LogVectorAdditionHeader =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, nameof(LogVectorAdditionHeader)),
            "\n=== Vector Addition Example ===");

    private static readonly Action<ILogger, long, Exception?> LogKernelCompiled =
        LoggerMessage.Define<long>(LogLevel.Information, new EventId(3, nameof(LogKernelCompiled)),
            "Kernel compiled in {Time}ms");

    private static readonly Action<ILogger, string, Exception?> LogKernelCompiledSuccessfully =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, nameof(LogKernelCompiledSuccessfully)),
            "Kernel compiled successfully: {Name}");

    private static readonly Action<ILogger, Exception?> LogKernelReady =
        LoggerMessage.Define(LogLevel.Information, new EventId(5, nameof(LogKernelReady)),
            "Kernel ready for execution");

    private static readonly Action<ILogger, long, Exception?> LogKernelExecuted =
        LoggerMessage.Define<long>(LogLevel.Information, new EventId(6, nameof(LogKernelExecuted)),
            "Kernel executed in {Time}ms");

    private static readonly Action<ILogger, double, Exception?> LogThroughput =
        LoggerMessage.Define<double>(LogLevel.Information, new EventId(7, nameof(LogThroughput)),
            "Throughput: {Throughput:F2} GB/s");

    private static readonly Action<ILogger, int, float, float, Exception?> LogMismatch =
        LoggerMessage.Define<int, float, float>(LogLevel.Error, new EventId(8, nameof(LogMismatch)),
            "Mismatch at index {Index}: expected {Expected}, got {Actual}");

    private static readonly Action<ILogger, Exception?> LogVerificationSuccess =
        LoggerMessage.Define(LogLevel.Information, new EventId(9, nameof(LogVerificationSuccess)),
            "✓ Vector addition results verified successfully!");

    private static readonly Action<ILogger, Exception?> LogMatrixMultiplicationHeader =
        LoggerMessage.Define(LogLevel.Information, new EventId(10, nameof(LogMatrixMultiplicationHeader)),
            "\n=== Matrix Multiplication Example ===");

    private static readonly Action<ILogger, Exception?> LogMatrixKernelCompiled =
        LoggerMessage.Define(LogLevel.Information, new EventId(11, nameof(LogMatrixKernelCompiled)),
            "Matrix multiplication kernel compiled");

    private static readonly Action<ILogger, Exception?> LogOptimizationHeader =
        LoggerMessage.Define(LogLevel.Information, new EventId(12, nameof(LogOptimizationHeader)),
            "\n=== Optimization Level Comparison ===");

    private static readonly Action<ILogger, string, long, Exception?> LogOptimizationLevel =
        LoggerMessage.Define<string, long>(LogLevel.Information, new EventId(13, nameof(LogOptimizationLevel)),
            "Optimization {Level}: Compiled in {Time}ms");

    private static readonly Action<ILogger, string, string, Exception?> LogKernelOptimization =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(14, nameof(LogKernelOptimization)),
            "  Kernel {Name} compiled with {Level} optimization");
    private static async Task Main(string[] args)
    {
        // Setup services
        var services = new ServiceCollection();
        _ = services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _ = services.AddSingleton<IAcceleratorProvider, MockCpuAcceleratorProvider>();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var acceleratorProvider = serviceProvider.GetRequiredService<IAcceleratorProvider>();

        // Discover and create CPU accelerator
        var accelerators = await acceleratorProvider.DiscoverAsync();
        await using var accelerator = accelerators.First();
        LogCreatedAccelerator(logger, accelerator.Info.Name, null);

        // Example 1: Simple vector addition kernel
        await RunVectorAdditionExample(accelerator, logger);

        // Example 2: Matrix multiplication kernel
        await RunMatrixMultiplicationExample(accelerator, logger);

        // Example 3: Custom math kernel with optimization levels
        await RunOptimizationLevelComparison(accelerator, logger);
    }

    private static async Task RunVectorAdditionExample(IAccelerator accelerator, ILogger logger)
    {
        LogVectorAdditionHeader(logger, null);

        // Define kernel source
        var kernelSource = @"
            // Simple vector addition: C = A + B
            for (int i = 0; i < length; i++) {
                c[i] = a[i] + b[i];
            }";

        // Compile kernel with release optimization
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Release,
            FastMath = true,
            EnableDebugInfo = false
        };

        var textKernelSource = new TextKernelSource(
            code: kernelSource,
            name: "vector_add",
            language: KernelLanguage.CSharpIL,
            entryPoint: "vector_add");

        var kernelDefinition = new KernelDefinition("vector_add", textKernelSource, compilationOptions);

        var stopwatch = Stopwatch.StartNew();
        await using var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, compilationOptions);
        stopwatch.Stop();

        LogKernelCompiled(logger, stopwatch.ElapsedMilliseconds, null);
        LogKernelCompiledSuccessfully(logger, compiledKernel.Name, null);
        LogKernelReady(logger, null);

        // Prepare test data
        const int vectorSize = 1024 * 1024; // 1M elements
        var a = new float[vectorSize];
        var b = new float[vectorSize];
        var c = new float[vectorSize];

        // Initialize input data
        // Random used for non-cryptographic test data generation
#pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random(42);
        for (var i = 0; i < vectorSize; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }
#pragma warning restore CA5394 // Do not use insecure randomness

        // Allocate device memory
        using var bufferA = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));
        using var bufferB = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));
        using var bufferC = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));

        // Copy data to device
        await bufferA.CopyFromHostAsync<float>(a.AsMemory());
        await bufferB.CopyFromHostAsync<float>(b.AsMemory());

        // Execute kernel
        var arguments = new KernelArguments(bufferA, bufferB, bufferC);

        stopwatch.Restart();
        await compiledKernel.ExecuteAsync(arguments);
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        LogKernelExecuted(logger, stopwatch.ElapsedMilliseconds, null);
        LogThroughput(logger, (3.0 * vectorSize * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1e9), null);

        // Copy results back
        await bufferC.CopyToHostAsync<float>(c.AsMemory());

        // Verify results
        var correct = true;
        for (var i = 0; i < Math.Min(10, vectorSize); i++)
        {
            var expected = a[i] + b[i];
            if (Math.Abs(c[i] - expected) > 1e-6f)
            {
                correct = false;
                LogMismatch(logger, i, expected, c[i], null);
            }
        }

        if (correct)
        {
            LogVerificationSuccess(logger, null);
        }
    }

    private static async Task RunMatrixMultiplicationExample(IAccelerator accelerator, ILogger logger)
    {
        LogMatrixMultiplicationHeader(logger, null);

        // Define kernel for matrix multiplication
        var kernelSource = @"
            // Matrix multiplication: C = A * B
            int row = workItemId[0];
            int col = workItemId[1];
            float sum = 0.0f;
            
            for (int k = 0; k < width; k++) {
                sum += a[row * width + k] * b[k * width + col];
            }
            
            c[row * width + col] = sum;";

        var textKernelSource = new TextKernelSource(
            code: kernelSource,
            name: "matrix_multiply",
            language: KernelLanguage.CSharpIL,
            entryPoint: "matrix_multiply");

        var kernelDefinition = new KernelDefinition("matrix_multiply", textKernelSource, new CompilationOptions());

        await using var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        LogMatrixKernelCompiled(logger, null);

        // For brevity, actual execution is omitted
    }

    private static async Task RunOptimizationLevelComparison(IAccelerator accelerator, ILogger logger)
    {
        LogOptimizationHeader(logger, null);

        var textKernelSource = new TextKernelSource(
            code: "c[i] = sqrt(a[i] * a[i] + b[i] * b[i]);",
            name: "complex_math",
            language: KernelLanguage.CSharpIL,
            entryPoint: "complex_math");

        var kernelDefinition = new KernelDefinition("complex_math", textKernelSource, new CompilationOptions());

        var optimizationLevels = new[]
        {
        (OptimizationLevel.None, "None"),
        (OptimizationLevel.Debug, "Debug"),
        (OptimizationLevel.Release, "Release"),
        (OptimizationLevel.Maximum, "Maximum")
    };

        foreach (var (level, name) in optimizationLevels)
        {
            var options = new CompilationOptions
            {
                OptimizationLevel = level,
                FastMath = level >= OptimizationLevel.Release,
                EnableDebugInfo = level == OptimizationLevel.Debug
            };

            var stopwatch = Stopwatch.StartNew();
            await using var kernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
            stopwatch.Stop();

            LogOptimizationLevel(logger, name, stopwatch.ElapsedMilliseconds, null);

            // Log optimization information
            LogKernelOptimization(logger, kernel.Name, name, null);
        }
    }
}

/// <summary>
/// Simple mock implementation for demonstration purposes.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "This class is instantiated via dependency injection")]
internal sealed class MockCpuAcceleratorProvider : IAcceleratorProvider
{
    public string Name => "Mock CPU Provider";
    public AcceleratorType[] SupportedTypes => [AcceleratorType.CPU];

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        // CA2000: Ownership is transferred to caller - they are responsible for disposal
#pragma warning disable CA2000 // Dispose objects before losing scope
        IAccelerator accelerator = new MockCpuAccelerator();
#pragma warning restore CA2000 // Dispose objects before losing scope
        return ValueTask.FromResult<IEnumerable<IAccelerator>>([accelerator]);
    }

    // CA2000: Ownership is transferred to caller - they are responsible for disposal
#pragma warning disable CA2000 // Dispose objects before losing scope
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default) => ValueTask.FromResult<IAccelerator>(new MockCpuAccelerator());
#pragma warning restore CA2000 // Dispose objects before losing scope
}

/// <summary>
/// Simple mock CPU accelerator for demonstration.
/// </summary>
internal sealed class MockCpuAccelerator : IAccelerator
{
    private readonly AcceleratorInfo _info;
    private readonly MockMemoryManager _memory;

    public MockCpuAccelerator()
    {
        _info = new AcceleratorInfo
        {
            Id = "mock-cpu-01",
            Name = $"Mock CPU ({Environment.ProcessorCount} cores)",
            DeviceType = "CPU",
            Vendor = "Mock",
            DriverVersion = "1.0.0",
            TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
            AvailableMemory = 4L * 1024 * 1024 * 1024, // 4GB
            MaxSharedMemoryPerBlock = 64 * 1024, // 64KB
            MaxMemoryAllocationSize = 2L * 1024 * 1024 * 1024, // 2GB
            LocalMemorySize = 32 * 1024, // 32KB
            IsUnifiedMemory = true,
            ComputeUnits = Environment.ProcessorCount,
            MaxClockFrequency = 3000, // 3GHz
            MaxThreadsPerBlock = 1024,
            ComputeCapability = new Version(1, 0)
        };
        _memory = new MockMemoryManager();
    }

    public AcceleratorInfo Info => _info;
    public AcceleratorType Type => AcceleratorType.CPU;
    public IMemoryManager Memory => _memory;
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate compilation time
        await Task.Delay(100, cancellationToken);

        return new MockCompiledKernel(definition.Name);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _memory?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple mock memory manager.
/// </summary>
internal sealed class MockMemoryManager : IMemoryManager, IDisposable
{
    private readonly List<MockMemoryBuffer> _buffers = [];

    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        var buffer = new MockMemoryBuffer(sizeInBytes, options);
        _buffers.Add(buffer);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var buffer = new MockMemoryBuffer(sizeInBytes, options);
        await buffer.CopyFromHostAsync(source, cancellationToken: cancellationToken);
        _buffers.Add(buffer);
        return buffer;
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is MockMemoryBuffer mockBuffer)
        {
            return new MockMemoryBufferView(mockBuffer, offset, length);
        }
        throw new ArgumentException("Buffer must be a mock buffer", nameof(buffer));
    }

    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (buffer is MockMemoryBuffer mockBuffer)
        {
            // Simulate copying data to the mock buffer
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
            bytes.CopyTo(mockBuffer.GetData().AsSpan());
        }
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        if (buffer is MockMemoryBuffer mockBuffer)
        {
            // Simulate copying data from the mock buffer
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
            mockBuffer.GetData().AsSpan()[..bytes.Length].CopyTo(bytes);
        }
    }

    public void Free(IMemoryBuffer buffer)
    {
        if (buffer is MockMemoryBuffer mockBuffer)
        {
            _ = _buffers.Remove(mockBuffer);
            buffer.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }
        _buffers.Clear();
    }
}

/// <summary>
/// Simple mock memory buffer.
/// </summary>
internal sealed class MockMemoryBuffer : IMemoryBuffer
{
    private readonly byte[] _data;
    private bool _disposed;

    public MockMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        if (sizeInBytes > int.MaxValue)
        {
            throw new ArgumentException("Size too large for mock buffer");
        }
        _data = new byte[sizeInBytes];
        SizeInBytes = sizeInBytes;
        Options = options;
    }

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.Span);
        sourceBytes.CopyTo(_data.AsSpan((int)offset));
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sourceSpan = _data.AsSpan((int)offset);
        var destBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.Span);
        sourceSpan[..destBytes.Length].CopyTo(destBytes);
        return ValueTask.CompletedTask;
    }

    public void Dispose() => _disposed = true;

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // For testing purposes - direct access to data
    public byte[] GetData() => _data;
}

/// <summary>
/// Mock memory buffer view.
/// </summary>
internal sealed class MockMemoryBufferView(MockMemoryBuffer parent, long offset, long length) : IMemoryBuffer
{
    private readonly MockMemoryBuffer _parent = parent;
    private readonly long _offset = offset;

    public long SizeInBytes { get; } = length;
    public MemoryOptions Options { get; } = parent.Options;
    public bool IsDisposed => _parent.IsDisposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    public void Dispose()
    {
        // Views don't own the memory
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Simple mock compiled kernel.
/// </summary>
internal sealed class MockCompiledKernel(string name) : ICompiledKernel
{
    public string Name { get; } = name;

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        // Simulate kernel execution
        await Task.Delay(50, cancellationToken);

        // For demonstration, if this is a vector addition kernel, perform the operation
        if (Name == "vector_add" && arguments.Length >= 3)
        {
            if (arguments.Get(0) is MockMemoryBuffer bufferA &&
                arguments.Get(1) is MockMemoryBuffer &&
                arguments.Get(2) is MockMemoryBuffer)
            {
                // Simple mock vector addition simulation
                // In a real implementation, this would perform actual computation
                Console.WriteLine($"Executing {Name} kernel on {bufferA.SizeInBytes / sizeof(float)} elements");
            }
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
