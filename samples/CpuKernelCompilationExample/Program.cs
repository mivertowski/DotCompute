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

namespace DotCompute.Samples.CpuKernelCompilationExample;

/// <summary>
/// Example demonstrating CPU kernel compilation with optimization and vectorization.
/// This sample uses mock implementations to showcase the DotCompute.Abstractions API patterns
/// without requiring the full backend infrastructure.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IAcceleratorProvider, MockCpuAcceleratorProvider>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var acceleratorProvider = serviceProvider.GetRequiredService<IAcceleratorProvider>();

        // Discover and create CPU accelerator
        var accelerators = await acceleratorProvider.DiscoverAsync();
        var accelerator = accelerators.First();
        logger.LogInformation("Created CPU accelerator: {Name}", accelerator.Info.Name);

        // Example 1: Simple vector addition kernel
        await RunVectorAdditionExample(accelerator, logger);
        
        // Example 2: Matrix multiplication kernel
        await RunMatrixMultiplicationExample(accelerator, logger);
        
        // Example 3: Custom math kernel with optimization levels
        await RunOptimizationLevelComparison(accelerator, logger);

        // Cleanup
        await accelerator.DisposeAsync();
    }

    static async Task RunVectorAdditionExample(IAccelerator accelerator, ILogger logger)
    {
        logger.LogInformation("\n=== Vector Addition Example ===");
        
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
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, compilationOptions);
        stopwatch.Stop();
        
        logger.LogInformation("Kernel compiled in {Time}ms", stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Kernel compiled successfully: {Name}", compiledKernel.Name);
        logger.LogInformation("Kernel ready for execution");

        // Prepare test data
        const int vectorSize = 1024 * 1024; // 1M elements
        var a = new float[vectorSize];
        var b = new float[vectorSize];
        var c = new float[vectorSize];
        
        // Initialize input data
        var random = new Random(42);
        for (int i = 0; i < vectorSize; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Allocate device memory
        var bufferA = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));
        var bufferB = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));
        var bufferC = await accelerator.Memory.AllocateAsync(vectorSize * sizeof(float));

        // Copy data to device
        await bufferA.CopyFromHostAsync<float>(a.AsMemory());
        await bufferB.CopyFromHostAsync<float>(b.AsMemory());

        // Execute kernel
        var arguments = new KernelArguments(bufferA, bufferB, bufferC);

        stopwatch.Restart();
        await compiledKernel.ExecuteAsync(arguments);
        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        logger.LogInformation("Kernel executed in {Time}ms", stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Throughput: {Throughput:F2} GB/s", 
            (3.0 * vectorSize * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1e9));

        // Copy results back
        await bufferC.CopyToHostAsync<float>(c.AsMemory());

        // Verify results
        bool correct = true;
        for (int i = 0; i < Math.Min(10, vectorSize); i++)
        {
            var expected = a[i] + b[i];
            if (Math.Abs(c[i] - expected) > 1e-6f)
            {
                correct = false;
                logger.LogError("Mismatch at index {Index}: expected {Expected}, got {Actual}", 
                    i, expected, c[i]);
            }
        }
        
        if (correct)
            logger.LogInformation("✓ Vector addition results verified successfully!");

        // Cleanup
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferC.DisposeAsync();
        await compiledKernel.DisposeAsync();
    }

    static async Task RunMatrixMultiplicationExample(IAccelerator accelerator, ILogger logger)
    {
        logger.LogInformation("\n=== Matrix Multiplication Example ===");
        
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

        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        logger.LogInformation("Matrix multiplication kernel compiled");
        
        // For brevity, actual execution is omitted
        await compiledKernel.DisposeAsync();
    }

    static async Task RunOptimizationLevelComparison(IAccelerator accelerator, ILogger logger)
    {
        logger.LogInformation("\n=== Optimization Level Comparison ===");
        
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
            var kernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
            stopwatch.Stop();
            
            logger.LogInformation("Optimization {Level}: Compiled in {Time}ms", 
                name, stopwatch.ElapsedMilliseconds);
            
            // Log optimization information
            logger.LogInformation("  Kernel {Name} compiled with {Level} optimization", kernel.Name, name);
            
            await kernel.DisposeAsync();
        }
    }
}

/// <summary>
/// Simple mock implementation for demonstration purposes.
/// </summary>
internal class MockCpuAcceleratorProvider : IAcceleratorProvider
{
    public string Name => "Mock CPU Provider";
    public AcceleratorType[] SupportedTypes => [AcceleratorType.CPU];

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var accelerator = new MockCpuAccelerator();
        return ValueTask.FromResult<IEnumerable<IAccelerator>>([accelerator]);
    }

    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IAccelerator>(new MockCpuAccelerator());
    }
}

/// <summary>
/// Simple mock CPU accelerator for demonstration.
/// </summary>
internal class MockCpuAccelerator : IAccelerator
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

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate compilation time
        await Task.Delay(100, cancellationToken);
        
        return new MockCompiledKernel(definition.Name);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _memory?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple mock memory manager.
/// </summary>
internal class MockMemoryManager : IMemoryManager, IDisposable
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
internal class MockMemoryBuffer : IMemoryBuffer
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

    public void Dispose()
    {
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Mock memory buffer view.
/// </summary>
internal class MockMemoryBufferView : IMemoryBuffer
{
    private readonly MockMemoryBuffer _parent;
    private readonly long _offset;

    public MockMemoryBufferView(MockMemoryBuffer parent, long offset, long length)
    {
        _parent = parent;
        _offset = offset;
        SizeInBytes = length;
        Options = parent.Options;
    }

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => _parent.IsDisposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);
    }

    public void Dispose()
    {
        // Views don't own the memory
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple mock compiled kernel.
/// </summary>
internal class MockCompiledKernel : ICompiledKernel
{
    public string Name { get; }

    public MockCompiledKernel(string name)
    {
        Name = name;
    }

    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        // Simulate kernel execution
        await Task.Delay(50, cancellationToken);
        
        // For demonstration, if this is a vector addition kernel, perform the operation
        if (Name == "vector_add" && arguments.Length >= 3)
        {
            var bufferA = arguments.Get(0) as MockMemoryBuffer;
            var bufferB = arguments.Get(1) as MockMemoryBuffer;
            var bufferC = arguments.Get(2) as MockMemoryBuffer;

            if (bufferA != null && bufferB != null && bufferC != null)
            {
                // Simple mock vector addition simulation
                // In a real implementation, this would perform actual computation
                Console.WriteLine($"Executing {Name} kernel on {bufferA.SizeInBytes / sizeof(float)} elements");
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}