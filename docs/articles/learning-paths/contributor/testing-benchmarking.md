# Testing and Benchmarking

This module covers implementing comprehensive tests and performance benchmarks for DotCompute contributions.

## Test Organization

### Directory Structure

```
tests/
├── Unit/                           # Component tests
│   ├── DotCompute.Abstractions.Tests/
│   ├── DotCompute.Memory.Tests/
│   └── DotCompute.Generators.Tests/
├── Integration/                    # Cross-component tests
│   └── DotCompute.Integration.Tests/
├── Hardware/                       # GPU-specific tests
│   ├── DotCompute.Hardware.Cuda.Tests/
│   └── DotCompute.Hardware.Metal.Tests/
└── Shared/                         # Test utilities
    └── DotCompute.TestUtilities/
```

### Test Categories

```csharp
// Unit test - no hardware required
[Fact]
[Trait("Category", "Unit")]
public void KernelDefinition_ValidatesParameters()
{
    // ...
}

// Integration test - may need backends
[Fact]
[Trait("Category", "Integration")]
public async Task ComputeService_ExecutesKernel()
{
    // ...
}

// Hardware test - requires specific GPU
[SkippableFact]
[Trait("Category", "Hardware")]
[Trait("Backend", "CUDA")]
public async Task CudaBackend_ExecutesKernel()
{
    Skip.IfNot(CudaBackend.IsAvailable(), "CUDA not available");
    // ...
}
```

## Unit Testing

### Testing Abstractions

```csharp
public class KernelDefinitionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var definition = new KernelDefinition
        {
            Name = "TestKernel",
            Parameters = new[]
            {
                new ParameterDefinition("input", typeof(float[]), ParameterDirection.Input),
                new ParameterDefinition("output", typeof(float[]), ParameterDirection.Output)
            }
        };

        Assert.Equal("TestKernel", definition.Name);
        Assert.Equal(2, definition.Parameters.Length);
        Assert.Equal(ParameterDirection.Input, definition.Parameters[0].Direction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsForInvalidName(string? name)
    {
        Assert.Throws<ArgumentException>(() => new KernelDefinition { Name = name! });
    }
}
```

### Testing Memory Management

```csharp
public class MemoryPoolTests
{
    [Fact]
    public void Rent_ReturnsBuffer()
    {
        var pool = new MemoryPool(poolSize: 1024 * 1024);

        var buffer = pool.Rent<float>(1000);

        Assert.NotNull(buffer);
        Assert.Equal(1000, buffer.Length);
    }

    [Fact]
    public void Return_AddsToPool()
    {
        var pool = new MemoryPool(poolSize: 1024 * 1024);
        var buffer = pool.Rent<float>(1000);

        pool.Return(buffer);

        var stats = pool.GetStatistics();
        Assert.Equal(1, stats.PooledBuffers);
    }

    [Fact]
    public void Rent_ReusesReturnedBuffer()
    {
        var pool = new MemoryPool(poolSize: 1024 * 1024);
        var buffer1 = pool.Rent<float>(1000);
        pool.Return(buffer1);

        var buffer2 = pool.Rent<float>(1000);

        Assert.Same(buffer1, buffer2); // Same instance reused
    }
}
```

### Testing Source Generators

```csharp
public class KernelGeneratorTests
{
    [Fact]
    public void Generator_ProducesValidCode()
    {
        var source = @"
using DotCompute.Generators.Kernel.Attributes;

namespace Test;

public static partial class Kernels
{
    [Kernel]
    public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length) result[idx] = a[idx] + b[idx];
    }
}";

        var result = RunGenerator(source);

        // No errors
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Generated source exists
        var generated = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.Contains("Add"));
        Assert.NotNull(generated);

        // Contains expected elements
        var code = generated!.SourceText.ToString();
        Assert.Contains("AddDefinition", code);
        Assert.Contains("KernelDefinition", code);
    }

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test",
            new[] { syntaxTree },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new KernelSourceGenerator();
        return CSharpGeneratorDriver.Create(generator)
            .RunGenerators(compilation)
            .GetRunResult();
    }
}
```

## Integration Testing

### Testing Compute Service

```csharp
public class ComputeServiceIntegrationTests
{
    private readonly IComputeOrchestrator _orchestrator;

    public ComputeServiceIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddDotComputeRuntime();
        var provider = services.BuildServiceProvider();
        _orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
    }

    [Fact]
    public void GetAvailableBackends_ReturnsAtLeastOne()
    {
        var backends = _orchestrator.GetAvailableBackends();

        Assert.NotEmpty(backends);
        Assert.Contains(backends, b => b.Type == BackendType.CPU);
    }

    [Fact]
    public async Task ExecuteKernel_CompletesSuccessfully()
    {
        using var buffer = _orchestrator.CreateBuffer<float>(100);
        var data = new float[100];
        Array.Fill(data, 1.0f);

        await buffer.CopyFromAsync(data);

        await _orchestrator.ExecuteKernelAsync(
            TestKernels.DoubleValues,
            new KernelConfig { BlockSize = 32, GridSize = 4 },
            buffer);

        await buffer.CopyToAsync(data);

        Assert.All(data, d => Assert.Equal(2.0f, d));
    }
}
```

### Testing Ring Kernels

```csharp
public class RingKernelIntegrationTests
{
    [Fact]
    public async Task RingKernel_ProcessesMessages()
    {
        var services = new ServiceCollection();
        services.AddDotComputeRuntime();
        var provider = services.BuildServiceProvider();
        var ringService = provider.GetRequiredService<IRingKernelService>();

        // Launch ring kernel
        var kernel = await ringService.LaunchAsync<TestRequest, TestResponse>(
            EchoKernel.Process,
            new RingKernelLaunchOptions { QueueCapacity = 64 });

        try
        {
            // Send messages
            for (int i = 0; i < 10; i++)
            {
                await kernel.SendAsync(new TestRequest { Value = i });
            }

            // Receive responses
            var responses = new List<TestResponse>();
            for (int i = 0; i < 10; i++)
            {
                var response = await kernel.ReceiveAsync(TimeSpan.FromSeconds(5));
                responses.Add(response);
            }

            Assert.Equal(10, responses.Count);
            Assert.All(responses, r => Assert.True(r.Processed));
        }
        finally
        {
            await kernel.TerminateAsync();
        }
    }
}
```

## Hardware Testing

### Skippable Tests

```csharp
public class CudaHardwareTests
{
    [SkippableFact]
    [Trait("Category", "Hardware")]
    public async Task CudaKernel_ExecutesOnGpu()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA not available");

        var services = new ServiceCollection();
        services.AddDotComputeRuntime();
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();

        Assert.Equal(BackendType.CUDA, service.ActiveBackend.Type);

        // Test GPU execution
        using var buffer = service.CreateBuffer<float>(1000);
        await service.ExecuteKernelAsync(
            TestKernels.VectorAdd,
            new KernelConfig { BlockSize = 256, GridSize = 4 },
            buffer, buffer, buffer);

        await service.SynchronizeAsync();
    }

    [SkippableTheory]
    [Trait("Category", "Hardware")]
    [InlineData(1000)]
    [InlineData(100000)]
    [InlineData(10000000)]
    public async Task CudaKernel_HandlesVariousSizes(int size)
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA not available");

        var service = GetCudaService();
        using var buffer = service.CreateBuffer<float>(size);

        var config = new KernelConfig
        {
            BlockSize = 256,
            GridSize = (size + 255) / 256
        };

        await service.ExecuteKernelAsync(TestKernels.Identity, config, buffer);
    }
}
```

### GPU Memory Tests

```csharp
public class GpuMemoryTests
{
    [SkippableFact]
    [Trait("Category", "Hardware")]
    public async Task LargeAllocation_Succeeds()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA not available");

        var service = GetCudaService();
        var deviceInfo = service.ActiveBackend.DeviceInfo;

        // Allocate 50% of GPU memory
        var sizeBytes = deviceInfo.TotalMemory / 2;
        var elements = (int)(sizeBytes / sizeof(float));

        using var buffer = service.CreateBuffer<float>(elements);

        Assert.Equal(elements, buffer.Length);
    }

    [SkippableFact]
    [Trait("Category", "Hardware")]
    public void OutOfMemory_ThrowsException()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA not available");

        var service = GetCudaService();

        // Try to allocate more than GPU memory
        Assert.Throws<OutOfMemoryException>(() =>
            service.CreateBuffer<float>(int.MaxValue / 2));
    }
}
```

## Benchmarking

### BenchmarkDotNet Setup

```csharp
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
public class KernelBenchmarks
{
    private IComputeOrchestrator _orchestrator = null!;
    private float[] _buffer = null!;

    [Params(1024, 1024 * 1024, 10 * 1024 * 1024)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDotComputeRuntime();
        var provider = services.BuildServiceProvider();
        _orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
        _buffer = new float[Size];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task VectorAdd()
    {
        await _orchestrator.ExecuteKernelAsync(
            TestKernels.VectorAdd,
            new KernelConfig { BlockSize = 256, GridSize = (Size + 255) / 256 },
            _buffer, _buffer, _buffer);
        await _orchestrator.SynchronizeAsync();
    }

    [Benchmark]
    public async Task VectorMultiply()
    {
        await _orchestrator.ExecuteKernelAsync(
            TestKernels.VectorMultiply,
            new KernelConfig { BlockSize = 256, GridSize = (Size + 255) / 256 },
            _buffer, _buffer, _buffer);
        await _orchestrator.SynchronizeAsync();
    }
}
```

### Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project benchmarks/DotCompute.Benchmarks

# Run specific benchmark
dotnet run -c Release -- --filter "*VectorAdd*"

# Export results
dotnet run -c Release -- --exporters json html
```

### Custom Performance Tests

```csharp
public class PerformanceTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task KernelLaunch_UnderTargetLatency()
    {
        var service = GetService();
        using var buffer = service.CreateBuffer<float>(1000);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            await service.ExecuteKernelAsync(TestKernels.Identity, config, buffer);
        }
        await service.SynchronizeAsync();

        // Measure
        var sw = Stopwatch.StartNew();
        const int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            await service.ExecuteKernelAsync(TestKernels.Identity, config, buffer);
        }
        await service.SynchronizeAsync();

        var avgMs = sw.ElapsedMilliseconds / (double)iterations;

        // Assert under target (e.g., 0.1ms per launch)
        Assert.True(avgMs < 0.1, $"Average launch time {avgMs:F3}ms exceeds target");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MemoryTransfer_MeetsBandwidthTarget()
    {
        var service = GetService();
        const int sizeBytes = 100 * 1024 * 1024; // 100 MB
        var elements = sizeBytes / sizeof(float);
        var data = new float[elements];

        using var buffer = service.CreateBuffer<float>(elements);

        var sw = Stopwatch.StartNew();
        await buffer.CopyFromAsync(data);
        sw.Stop();

        var bandwidthGBps = (sizeBytes / 1e9) / (sw.Elapsed.TotalSeconds);

        // Assert meets minimum bandwidth (e.g., 5 GB/s)
        Assert.True(bandwidthGBps > 5.0,
            $"Transfer bandwidth {bandwidthGBps:F1} GB/s below target");
    }
}
```

## Test Utilities

### Test Fixtures

```csharp
public class ComputeServiceFixture : IAsyncLifetime
{
    public IServiceProvider Provider { get; private set; } = null!;
    public IComputeOrchestrator Orchestrator { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDotComputeRuntime();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        Provider = services.BuildServiceProvider();
        Orchestrator = Provider.GetRequiredService<IComputeOrchestrator>();

        // Warmup
        using var buffer = ComputeService.CreateBuffer<float>(100);
        await ComputeService.ExecuteKernelAsync(
            TestKernels.Identity,
            new KernelConfig { BlockSize = 32, GridSize = 4 },
            buffer);
    }

    public Task DisposeAsync()
    {
        if (Provider is IDisposable disposable)
            disposable.Dispose();
        return Task.CompletedTask;
    }
}

public class ComputeTests : IClassFixture<ComputeServiceFixture>
{
    private readonly ComputeServiceFixture _fixture;

    public ComputeTests(ComputeServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test()
    {
        var service = _fixture.ComputeService;
        // ...
    }
}
```

### Test Helpers

```csharp
public static class TestHelpers
{
    public static float[] GenerateRandomData(int size, int seed = 42)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, size)
            .Select(_ => (float)random.NextDouble())
            .ToArray();
    }

    public static void AssertArraysEqual(float[] expected, float[] actual, float tolerance = 1e-5f)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(
                Math.Abs(expected[i] - actual[i]) < tolerance,
                $"Arrays differ at index {i}: expected {expected[i]}, got {actual[i]}");
        }
    }
}
```

## Exercises

### Exercise 1: Unit Test Suite

Write comprehensive unit tests for a new kernel attribute.

### Exercise 2: Hardware Test

Create a hardware test that validates correct execution on GPU.

### Exercise 3: Performance Benchmark

Create a benchmark comparing two kernel implementations.

## Key Takeaways

1. **Categorize tests** for selective execution
2. **Use SkippableFact** for hardware-dependent tests
3. **Test edge cases** - empty arrays, large sizes, invalid inputs
4. **Benchmark with warmup** for accurate measurements
5. **Use fixtures** to share expensive setup

## Path Complete

Congratulations! You've completed the Contributor Learning Path.

**What you learned:**
- DotCompute's four-layer architecture
- Building source generators
- Creating Roslyn analyzers
- Implementing comprehensive tests

**Next steps:**
- Review open issues on GitHub
- Join discussions on design decisions
- Submit your first PR!

## Further Reading

- [CLAUDE.md](../../../CLAUDE.md) - Development guidelines
- [Benchmarking Guide](../../performance/benchmarking.md) - Detailed methodology
- [Architecture Overview](../../architecture/overview.md) - System design
