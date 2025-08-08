# DotCompute Testing Guide

## Overview

This guide provides comprehensive documentation for testing the DotCompute framework, including unit tests, integration tests, performance benchmarks, and hardware-specific tests.

## Test Architecture

### Test Projects Structure

```
tests/
├── DotCompute.Abstractions.Tests/      # Core abstraction tests
├── DotCompute.Core.Tests/              # Core functionality tests
├── DotCompute.Memory.Tests/            # Memory management tests
├── DotCompute.Integration.Tests/       # End-to-end integration tests
├── DotCompute.Hardware.RealTests/      # Real hardware tests (CUDA, OpenCL)
├── DotCompute.Plugins.Tests/           # Plugin system tests
├── DotCompute.SharedTestUtilities/     # Shared test utilities
└── DotCompute.BasicTests/              # Basic functionality tests

benchmarks/
└── DotCompute.Benchmarks/              # Performance benchmarks
```

## Running Tests

### Basic Test Execution

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj

# Run tests by category
dotnet test --filter "Category!=RequiresGPU"
dotnet test --filter "Category=Integration"
```

### Coverage Measurement

```bash
# Generate coverage report with proper settings
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Generate HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

## Test Categories

### Unit Tests
- **Purpose**: Test individual components in isolation
- **Location**: `*.Tests` projects
- **Characteristics**:
  - Fast execution (<100ms per test)
  - No external dependencies
  - Use mocks/stubs for dependencies
  - High code coverage target (>80%)

### Integration Tests
- **Purpose**: Test component interactions
- **Location**: `DotCompute.Integration.Tests`
- **Category**: `[Category("Integration")]`
- **Characteristics**:
  - May use real implementations
  - Test complete workflows
  - Longer execution time acceptable
  - Focus on system behavior

### Hardware Tests
- **Purpose**: Test GPU/accelerator functionality
- **Location**: `DotCompute.Hardware.RealTests`
- **Category**: `[Category("RequiresGPU")]`
- **Requirements**:
  - CUDA-capable GPU
  - OpenCL runtime
  - DirectCompute support
- **Environment Setup**:
  ```bash
  export LD_LIBRARY_PATH=/usr/lib/wsl/lib:$LD_LIBRARY_PATH
  ```

### Performance Benchmarks
- **Purpose**: Measure and track performance
- **Location**: `benchmarks/DotCompute.Benchmarks`
- **Framework**: BenchmarkDotNet
- **Categories**:
  - Memory allocation
  - Kernel compilation
  - Data transfer
  - End-to-end workflows

## Test Patterns and Best Practices

### 1. Async Test Pattern

```csharp
[Fact]
public async Task ShouldExecuteAsyncOperation()
{
    // Arrange
    var manager = new TestAcceleratorManager();
    await manager.InitializeAsync();
    
    // Act
    var result = await manager.ExecuteAsync();
    
    // Assert
    Assert.NotNull(result);
}
```

### 2. Cancellation Token Testing

```csharp
[Fact]
public async Task ShouldCancelOperation()
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    
    await Assert.ThrowsAsync<OperationCanceledException>(
        () => kernel.CompileAsync(source, cts.Token));
}
```

### 3. Memory Management Testing

```csharp
[Fact]
public async Task ShouldCleanupMemory()
{
    IMemoryBuffer buffer;
    using (buffer = await memory.AllocateAsync(1024))
    {
        Assert.False(buffer.IsDisposed);
    }
    Assert.True(buffer.IsDisposed);
}
```

### 4. Hardware Detection

```csharp
[SkippableFact]
public async Task ShouldDetectCudaHardware()
{
    Skip.IfNot(CudaHelper.IsCudaAvailable(), "CUDA not available");
    
    var devices = await CudaHelper.GetDevicesAsync();
    Assert.NotEmpty(devices);
}
```

## Writing Effective Tests

### Test Naming Convention

```csharp
// Pattern: MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public void AllocateAsync_WithValidSize_ReturnsBuffer() { }

[Fact]
public void CompileKernel_WithInvalidSource_ThrowsException() { }
```

### Test Organization

```csharp
public class AcceleratorManagerTests
{
    // Group related tests together
    public class InitializationTests
    {
        [Fact]
        public async Task ShouldInitializeProviders() { }
    }
    
    public class DiscoveryTests
    {
        [Fact]
        public async Task ShouldDiscoverAccelerators() { }
    }
}
```

### Using Test Utilities

```csharp
// Use shared test utilities
public class KernelCompilerTests : TestBase
{
    private readonly ITestOutputHelper _output;
    
    public KernelCompilerTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public async Task CompileKernel_Test()
    {
        // Use base class helpers
        var kernel = await CreateTestKernelAsync();
        LogTestOutput(_output, "Kernel compiled successfully");
    }
}
```

## Mocking and Test Doubles

### Using Moq

```csharp
[Fact]
public async Task ShouldUseAcceleratorProvider()
{
    // Create mock
    var mockProvider = new Mock<IAcceleratorProvider>();
    mockProvider.Setup(p => p.DiscoverAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { CreateMockAccelerator() });
    
    // Use in test
    var manager = new AcceleratorManager();
    manager.RegisterProvider(mockProvider.Object);
    
    // Verify interactions
    mockProvider.Verify(p => p.DiscoverAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

### Test Implementations

```csharp
// Create test-specific implementations
public class TestMemoryBuffer : IMemoryBuffer
{
    public long Size { get; }
    public bool IsDisposed { get; private set; }
    
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0)
    {
        // Simple test implementation
        return ValueTask.CompletedTask;
    }
}
```

## Performance Testing

### BenchmarkDotNet Setup

```csharp
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class MemoryBenchmarks
{
    [Params(1024, 1024 * 1024)]
    public int Size { get; set; }
    
    [GlobalSetup]
    public void Setup()
    {
        // Initialize resources
    }
    
    [Benchmark]
    public async Task AllocateMemory()
    {
        var buffer = await _memory.AllocateAsync(Size);
        await buffer.DisposeAsync();
    }
}
```

### Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project benchmarks/DotCompute.Benchmarks

# Run specific benchmarks
dotnet run -c Release -- --filter "*Memory*"

# Generate reports
dotnet run -c Release -- --exporters json html --artifacts ./results
```

## Continuous Integration

### GitHub Actions Integration

The CI pipeline automatically:
1. Runs tests on multiple platforms (Linux, Windows, macOS)
2. Generates code coverage reports
3. Runs performance benchmarks on main branch
4. Publishes test results and artifacts

### Local CI Validation

```bash
# Validate before pushing
./scripts/validate-ci.sh

# Run same tests as CI
dotnet test --configuration Release --logger "trx" --collect:"XPlat Code Coverage"
```

## Troubleshooting

### Common Issues

#### 1. CUDA Tests Failing
```bash
# Check CUDA installation
nvidia-smi
ldconfig -p | grep cuda

# Set library path
export LD_LIBRARY_PATH=/usr/lib/wsl/lib:$LD_LIBRARY_PATH
```

#### 2. Low Code Coverage
```bash
# Ensure debug symbols are generated
# Check Directory.Build.props for:
<DebugType>full</DebugType>
<DebugSymbols>true</DebugSymbols>
```

#### 3. Integration Test Failures
```bash
# Check service registration
# Ensure all dependencies are properly registered in DI container
services.AddSingleton<IComputeEngine, DefaultComputeEngine>();
services.AddSingleton<IAcceleratorManager, DefaultAcceleratorManager>();
```

#### 4. Benchmark Failures
```bash
# Ensure Release build
dotnet build -c Release

# Check for sufficient resources
# Benchmarks may require significant memory
```

## Test Metrics and Goals

### Coverage Goals
- **Core Libraries**: >80% coverage
- **Abstractions**: >90% coverage
- **Integration**: >70% coverage
- **Overall**: >75% coverage

### Performance Goals
- **Memory Allocation**: <1ms for buffers up to 1MB
- **Kernel Compilation**: <100ms for simple kernels
- **Data Transfer**: >1GB/s throughput
- **Test Execution**: <10s for unit tests, <60s for all tests

### Quality Metrics
- **Test Pass Rate**: 100% for main branch
- **Flaky Tests**: <1% failure rate
- **Test Coverage Trend**: Increasing or stable
- **Performance Regression**: <5% variation

## Advanced Testing Scenarios

### Stress Testing

```csharp
[Fact]
public async Task StressTest_ConcurrentAllocations()
{
    const int concurrency = 100;
    var tasks = Enumerable.Range(0, concurrency)
        .Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                var buffer = await _memory.AllocateAsync(1024);
                await buffer.DisposeAsync();
            }
        }));
    
    await Task.WhenAll(tasks);
}
```

### Edge Case Testing

```csharp
[Theory]
[InlineData(0)]           // Zero size
[InlineData(1)]           // Minimum size
[InlineData(int.MaxValue)] // Maximum size
public async Task AllocateAsync_EdgeCases(int size)
{
    // Test boundary conditions
}
```

### Error Recovery Testing

```csharp
[Fact]
public async Task ShouldRecoverFromError()
{
    // Simulate error condition
    _mockProvider.Setup(p => p.InitializeAsync())
                 .ThrowsAsync(new Exception("Initialization failed"));
    
    // Verify graceful handling
    var result = await _manager.TryInitializeAsync();
    Assert.False(result.Success);
    Assert.Contains("Initialization failed", result.Error);
}
```

## Contributing Tests

### Adding New Tests

1. **Identify test category** (unit, integration, hardware, benchmark)
2. **Choose appropriate project** based on component being tested
3. **Follow naming conventions** and patterns
4. **Include test documentation** in XML comments
5. **Verify coverage** improvement
6. **Ensure CI passes** before submitting PR

### Test Review Checklist

- [ ] Tests follow naming conventions
- [ ] Appropriate assertions used
- [ ] Resources properly disposed
- [ ] Async patterns correctly implemented
- [ ] Edge cases covered
- [ ] Performance impact considered
- [ ] Documentation updated if needed

## Resources

- [xUnit Documentation](https://xunit.net/)
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Moq Documentation](https://github.com/moq/moq4)
- [DotCompute Contributing Guide](./CONTRIBUTING.md)