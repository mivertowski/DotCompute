# CUDA Backend Testing Strategy

## Overview

This document outlines a comprehensive testing strategy for the DotCompute CUDA backend implementation, targeting the RTX 2000 Ada Generation GPU with robust validation across multiple scenarios.

## Testing Pyramid Structure

```
                    ┌─────────────────────┐
                    │   E2E Tests (5%)    │
                    │  Real GPU Hardware  │
                    └─────────────────────┘
                  ┌─────────────────────────┐
                  │ Integration Tests (25%) │
                  │  Mock + Real Hardware   │
                  └─────────────────────────┘
                ┌───────────────────────────────┐
                │     Unit Tests (70%)          │
                │  Mock CUDA Runtime/Driver     │
                └───────────────────────────────┘
```

## 1. Unit Tests (70% Coverage)

### Test Categories

#### A. Device Management Tests
```csharp
namespace DotCompute.Backends.CUDA.Tests.Unit;

[TestFixture]
public class CudaDeviceTests
{
    private Mock<ICudaRuntime> _mockRuntime;
    
    [SetUp]
    public void Setup()
    {
        _mockRuntime = new Mock<ICudaRuntime>();
    }

    [Test]
    public void EnumerateDevices_WithAvailableDevices_ReturnsCorrectCount()
    {
        // Arrange
        var deviceProperties = CreateMockRTX2000Properties();
        _mockRuntime.Setup(r => r.cudaGetDeviceCount(out It.Ref<int>.IsAny))
                   .Returns((out int count) => { count = 1; return CudaError.Success; });
        _mockRuntime.Setup(r => r.cudaGetDeviceProperties(ref It.Ref<CudaDeviceProperties>.IsAny, 0))
                   .Returns((ref CudaDeviceProperties props, int device) => 
                   { 
                       props = deviceProperties; 
                       return CudaError.Success; 
                   });

        // Act
        var devices = CudaDevice.EnumerateDevices();

        // Assert
        Assert.That(devices, Has.Length.EqualTo(1));
        Assert.That(devices[0].Name, Is.EqualTo("NVIDIA RTX 2000 Ada Generation Laptop GPU"));
        Assert.That(devices[0].Capability.Major, Is.EqualTo(8));
        Assert.That(devices[0].Capability.Minor, Is.EqualTo(9));
    }

    [Test]
    public void GetBestDevice_WithMultipleDevices_SelectsHighestCapability()
    {
        // Test device selection logic
    }

    [Test]
    public void SupportsFeature_WithAdaArchitecture_ReturnsTrueForTensorCores()
    {
        // Test feature detection for Ada Lovelace architecture
    }

    private CudaDeviceProperties CreateMockRTX2000Properties()
    {
        return new CudaDeviceProperties
        {
            Name = "NVIDIA RTX 2000 Ada Generation Laptop GPU",
            Major = 8,
            Minor = 9,
            TotalGlobalMem = 8 * 1024UL * 1024 * 1024, // 8GB
            MultiProcessorCount = 35, // RTX 2000 Ada specs
            WarpSize = 32,
            MaxThreadsPerBlock = 1024,
            SharedMemPerBlock = 49152, // 48KB
            ManagedMemory = 1,
            ConcurrentKernels = 1,
            // ... other properties
        };
    }
}
```

#### B. Memory Management Tests
```csharp
[TestFixture]
public class CudaMemoryManagerTests
{
    private Mock<ICudaRuntime> _mockRuntime;
    private Mock<CudaContext> _mockContext;
    private CudaMemoryManager _memoryManager;

    [SetUp]
    public void Setup()
    {
        _mockRuntime = new Mock<ICudaRuntime>();
        _mockContext = new Mock<CudaContext>();
        _memoryManager = new CudaMemoryManager(_mockContext.Object, Mock.Of<ILogger>());
    }

    [Test]
    public async Task AllocateAsync_WithValidSize_ReturnsBuffer()
    {
        // Arrange
        const long size = 1024 * 1024; // 1MB
        var expectedPtr = new IntPtr(0x12345678);
        
        _mockRuntime.Setup(r => r.cudaMalloc(out It.Ref<IntPtr>.IsAny, (ulong)size))
                   .Returns((out IntPtr ptr, ulong s) => 
                   { 
                       ptr = expectedPtr; 
                       return CudaError.Success; 
                   });

        // Act
        var buffer = await _memoryManager.AllocateAsync(size);

        // Assert
        Assert.That(buffer, Is.Not.Null);
        Assert.That(buffer.SizeInBytes, Is.EqualTo(size));
        Assert.That(buffer.IsDisposed, Is.False);
    }

    [Test]
    public async Task AllocateAsync_WithUnifiedMemoryOption_UsesCorrectAllocation()
    {
        // Test unified memory allocation path
    }

    [Test]
    public void Copy_BetweenGPUBuffers_InvokesDeviceToDeviceCopy()
    {
        // Test D2D memory copy operations
    }

    [Test]
    public void MemoryPool_WithRepeatedAllocations_ReusesBuffers()
    {
        // Test memory pooling efficiency
    }
}
```

#### C. Kernel Compilation Tests
```csharp
[TestFixture]
public class CudaKernelCompilerTests
{
    private Mock<INvrtcRuntime> _mockNvrtc;
    private Mock<CudaContext> _mockContext;
    private CudaKernelCompiler _compiler;

    [Test]
    public async Task CompileAsync_WithValidKernel_ReturnsCompiledKernel()
    {
        // Arrange
        var kernelSource = @"
            __global__ void vectorAdd(float* a, float* b, float* c, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    c[idx] = a[idx] + b[idx];
                }
            }";
        
        var definition = new KernelDefinition("vectorAdd", Encoding.UTF8.GetBytes(kernelSource), null);
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Release };

        SetupSuccessfulCompilation();

        // Act
        var kernel = await _compiler.CompileAsync(definition, options);

        // Assert
        Assert.That(kernel, Is.Not.Null);
        Assert.That(kernel.Name, Is.EqualTo("vectorAdd"));
    }

    [Test]
    public async Task CompileAsync_WithSyntaxError_ThrowsCompilationException()
    {
        // Test error handling for invalid kernel code
    }

    [Test]
    public void GetOptimalOptions_ForRTX2000_ReturnsAdaOptimizations()
    {
        // Test architecture-specific optimization flags
        var options = _compiler.GetOptimalOptionsForDevice(_mockContext.Object.Device);
        
        Assert.That(options.TargetArchitecture, Is.EqualTo("sm_89"));
        Assert.That(options.AdditionalFlags, Contains.Item("--use_fast_math"));
    }

    private void SetupSuccessfulCompilation()
    {
        var program = new IntPtr(0x1000);
        var ptx = "Valid PTX code here";
        
        _mockNvrtc.Setup(n => n.nvrtcCreateProgram(out It.Ref<IntPtr>.IsAny, 
                                                   It.IsAny<string>(), 
                                                   It.IsAny<string>(), 
                                                   It.IsAny<int>(), 
                                                   It.IsAny<string[]>(), 
                                                   It.IsAny<string[]>()))
                  .Returns((out IntPtr prog, string src, string name, int numHeaders, string[] headers, string[] includes) => 
                  { 
                      prog = program; 
                      return NvrtcResult.Success; 
                  });
        
        _mockNvrtc.Setup(n => n.nvrtcCompileProgram(program, It.IsAny<int>(), It.IsAny<string[]>()))
                  .Returns(NvrtcResult.Success);
        
        // ... additional mock setups
    }
}
```

#### D. Kernel Execution Tests
```csharp
[TestFixture]
public class CudaKernelExecutorTests
{
    [Test]
    public async Task ExecuteAsync_WithValidKernel_ReturnsSuccessResult()
    {
        // Test successful kernel execution
    }

    [Test]
    public void GetOptimalExecutionConfig_ForRTX2000_CalculatesCorrectOccupancy()
    {
        // Test occupancy calculation for Ada architecture
        var kernel = CreateMockKernel(registersPerThread: 32, sharedMemorySize: 16384);
        var problemSize = new[] { 1024 * 1024 }; // 1M elements

        var config = _executor.GetOptimalExecutionConfig(kernel, problemSize);

        // For RTX 2000 Ada: 35 SMs, 1536 threads per SM
        Assert.That(config.LocalWorkSize[0], Is.EqualTo(256)); // Optimal block size
        Assert.That(config.GlobalWorkSize[0], Is.GreaterThanOrEqualTo(problemSize[0]));
    }

    [Test]
    public async Task ProfileAsync_WithMultipleIterations_ReturnsAccurateStatistics()
    {
        // Test performance profiling functionality
    }
}
```

### Mock Infrastructure

#### ICudaRuntime Interface
```csharp
public interface ICudaRuntime
{
    CudaError cudaGetDeviceCount(out int count);
    CudaError cudaGetDeviceProperties(ref CudaDeviceProperties prop, int device);
    CudaError cudaMalloc(out IntPtr devPtr, ulong size);
    CudaError cudaFree(IntPtr devPtr);
    // ... all other CUDA Runtime API methods
}

public class MockCudaRuntimeFactory
{
    public static Mock<ICudaRuntime> CreateRTX2000Mock()
    {
        var mock = new Mock<ICudaRuntime>();
        
        // Setup default behaviors for RTX 2000
        mock.Setup(r => r.cudaGetDeviceCount(out It.Ref<int>.IsAny))
            .Returns((out int count) => { count = 1; return CudaError.Success; });
            
        // ... other default setups
        
        return mock;
    }
}
```

## 2. Integration Tests (25% Coverage)

### Hardware-Agnostic Integration Tests

```csharp
[TestFixture]
[Category("Integration")]
public class CudaBackendIntegrationTests
{
    private CudaAccelerator _accelerator;
    private bool _cudaAvailable;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _cudaAvailable = CudaRuntime.IsCudaSupported();
        if (_cudaAvailable)
        {
            _accelerator = new CudaAccelerator(0);
        }
    }

    [Test]
    public async Task EndToEndWorkflow_VectorAddition_ProducesCorrectResults()
    {
        if (!_cudaAvailable)
            Assert.Ignore("CUDA not available");

        // Arrange
        const int size = 1024;
        var a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var b = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
        var expected = a.Zip(b, (x, y) => x + y).ToArray();
        var c = new float[size];

        // Act - Full pipeline: Allocate -> Copy -> Compile -> Execute -> Copy Back
        var bufferA = await _accelerator.Memory.AllocateAndCopyAsync(a.AsMemory());
        var bufferB = await _accelerator.Memory.AllocateAndCopyAsync(b.AsMemory());
        var bufferC = await _accelerator.Memory.AllocateAsync(size * sizeof(float));

        var kernelSource = CreateVectorAddKernelSource();
        var definition = new KernelDefinition("vectorAdd", Encoding.UTF8.GetBytes(kernelSource), "vectorAdd");
        var kernel = await _accelerator.CompileKernelAsync(definition);

        var args = new KernelArgument[]
        {
            new() { Name = "a", Value = bufferA, Type = typeof(IntPtr), IsDeviceMemory = true },
            new() { Name = "b", Value = bufferB, Type = typeof(IntPtr), IsDeviceMemory = true },
            new() { Name = "c", Value = bufferC, Type = typeof(IntPtr), IsDeviceMemory = true },
            new() { Name = "n", Value = size, Type = typeof(int) }
        };

        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = new[] { size },
            LocalWorkSize = new[] { 256 }
        };

        var executor = new CudaKernelExecutor(_accelerator.Context, _accelerator, Mock.Of<ILogger>());
        var result = await executor.ExecuteAndWaitAsync(kernel, args, config);

        await bufferC.CopyToHostAsync(c.AsMemory());

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(c, Is.EqualTo(expected).Within(1e-6f));
    }

    [Test]
    public async Task MemoryManagement_LargeAllocations_HandlesFragmentation()
    {
        // Test memory fragmentation scenarios
    }

    [Test]
    public async Task StreamManagement_ConcurrentOperations_ExecutesCorrectly()
    {
        // Test concurrent kernel execution on multiple streams
    }
}
```

### Multi-GPU Integration Tests

```csharp
[TestFixture]
[Category("MultiGPU")]
public class MultiGpuIntegrationTests
{
    [Test]
    public void P2PMemoryTransfer_BetweenDevices_TransfersDataCorrectly()
    {
        var devices = CudaDevice.EnumerateDevices();
        if (devices.Length < 2)
            Assert.Ignore("Multiple GPUs required for P2P testing");

        // Test peer-to-peer memory transfers
    }
}
```

## 3. Hardware-Specific Tests (10% Coverage)

### RTX 2000 Ada Generation Specific Tests

```csharp
[TestFixture]
[Category("RTX2000")]
[Category("Hardware")]
public class RTX2000HardwareTests
{
    private CudaDevice _rtx2000Device;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var devices = CudaDevice.EnumerateDevices();
        _rtx2000Device = devices.FirstOrDefault(d => d.Name.Contains("RTX 2000 Ada"));
        
        if (_rtx2000Device == null)
            Assert.Ignore("RTX 2000 Ada Generation GPU not found");
    }

    [Test]
    public void DeviceProperties_RTX2000Ada_HasCorrectSpecifications()
    {
        // Verify RTX 2000 specific properties
        Assert.That(_rtx2000Device.Capability.Major, Is.EqualTo(8));
        Assert.That(_rtx2000Device.Capability.Minor, Is.EqualTo(9));
        Assert.That(_rtx2000Device.MultiprocessorCount, Is.EqualTo(35));
        Assert.That(_rtx2000Device.WarpSize, Is.EqualTo(32));
        Assert.That(_rtx2000Device.TotalMemory, Is.GreaterThan(8L * 1024 * 1024 * 1024 - 512L * 1024 * 1024)); // ~8GB
    }

    [Test]
    public void TensorCores_AdaGeneration_SupportedAndFunctional()
    {
        Assert.That(_rtx2000Device.SupportsFeature(CudaFeature.TensorCores), Is.True);
        // Additional tensor core functionality tests
    }

    [Test]
    public void ComputeCapability89_Features_AllSupported()
    {
        // Test Ada Lovelace specific features
        Assert.That(_rtx2000Device.SupportsFeature(CudaFeature.CooperativeGroups), Is.True);
        Assert.That(_rtx2000Device.SupportsFeature(CudaFeature.BFloat16), Is.True);
        Assert.That(_rtx2000Device.SupportsFeature(CudaFeature.SparseTensors), Is.True);
    }

    [TestCase(32, 16384, ExpectedResult = 1.0)] // Light kernel
    [TestCase(64, 32768, ExpectedResult = 0.75)] // Medium kernel  
    [TestCase(128, 49152, ExpectedResult = 0.5)] // Heavy kernel
    public double OccupancyCalculation_VariousKernelSizes_ReturnsExpectedOccupancy(
        int registersPerThread, int sharedMemorySize)
    {
        var calculator = new OccupancyCalculator(_rtx2000Device);
        var mockKernel = CreateMockKernel(registersPerThread, sharedMemorySize);
        var config = new KernelExecutionConfig { LocalWorkSize = new[] { 256 } };
        
        return calculator.CalculateTheoreticalOccupancy(mockKernel, config);
    }

    [Test]
    [Timeout(5000)] // 5 second timeout
    public async Task MemoryBandwidth_Peak_AchievesExpectedThroughput()
    {
        // Test peak memory bandwidth (should be close to spec: ~288 GB/s)
        var bandwidthTest = new MemoryBandwidthTest(_rtx2000Device);
        var throughput = await bandwidthTest.MeasurePeakBandwidthAsync();
        
        Assert.That(throughput, Is.GreaterThan(200.0), // At least 200 GB/s
            "RTX 2000 Ada should achieve high memory bandwidth");
    }
}
```

## 4. Performance & Benchmarking Tests

### Performance Regression Tests
```csharp
[TestFixture]
[Category("Performance")]
public class CudaPerformanceTests
{
    private PerformanceBaseline _baseline;

    [OneTimeSetUp]
    public void LoadBaseline()
    {
        _baseline = PerformanceBaseline.Load("RTX2000_Ada_Baseline.json");
    }

    [Test]
    [Performance]
    public async Task KernelExecution_VectorAdd1M_MeetsPerformanceBaseline()
    {
        const int size = 1024 * 1024;
        var stopwatch = Stopwatch.StartNew();
        
        // Execute vector addition kernel
        await ExecuteVectorAddKernel(size);
        
        stopwatch.Stop();
        var executionTime = stopwatch.Elapsed.TotalMilliseconds;
        
        Assert.That(executionTime, Is.LessThan(_baseline.VectorAdd1M * 1.1), 
            "Performance regression detected: execution time exceeds baseline by >10%");
        
        // Update baseline if significantly better
        if (executionTime < _baseline.VectorAdd1M * 0.9)
        {
            TestContext.WriteLine($"Performance improved: {executionTime:F2}ms vs baseline {_baseline.VectorAdd1M:F2}ms");
        }
    }

    [Test]
    public async Task MemoryTransfer_HostToDevice_MeetsExpectedBandwidth()
    {
        // Test memory transfer performance
    }

    [Test]
    public async Task KernelCompilation_ComplexKernel_CompletesWithinTimeout()
    {
        // Test compilation performance for complex kernels
    }
}
```

## 5. Error Handling & Edge Case Tests

### Error Handling Tests
```csharp
[TestFixture]
[Category("ErrorHandling")]
public class CudaErrorHandlingTests
{
    [Test]
    public void OutOfMemory_LargeAllocation_ThrowsAppropriateException()
    {
        var memoryManager = new CudaMemoryManager(_context, _logger);
        
        Assert.ThrowsAsync<OutOfMemoryException>(() => 
            memoryManager.AllocateAsync(long.MaxValue));
    }

    [Test]
    public void InvalidKernel_CompilationError_ThrowsWithDetailedMessage()
    {
        var invalidKernel = "__global__ void invalid() { syntax error }";
        var definition = new KernelDefinition("invalid", Encoding.UTF8.GetBytes(invalidKernel), null);
        
        var exception = Assert.ThrowsAsync<KernelCompilationException>(() =>
            _compiler.CompileAsync(definition));
            
        Assert.That(exception.CompilerLog, Is.Not.Null.And.Not.Empty);
        Assert.That(exception.Message, Contains.Substring("syntax"));
    }

    [Test]
    public void DeviceReset_RecoveryScenario_RestoresOperationality()
    {
        // Test device reset and recovery scenarios
    }
}
```

## 6. Test Infrastructure

### Test Utilities

```csharp
public static class CudaTestUtilities
{
    public static string CreateVectorAddKernelSource() => @"
        extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n) {
            int idx = blockIdx.x * blockDim.x + threadIdx.x;
            if (idx < n) {
                c[idx] = a[idx] + b[idx];
            }
        }";

    public static CompiledKernel CreateMockKernel(int registersPerThread = 32, int sharedMemorySize = 16384)
    {
        return new CompiledKernel(
            Guid.NewGuid(), 
            new IntPtr(0x12345678), 
            sharedMemorySize,
            new KernelConfiguration(
                new Dim3(256, 1, 1), // Grid
                new Dim3(256, 1, 1)  // Block
            )
        );
    }

    public static CudaDeviceProperties CreateRTX2000Properties()
    {
        return new CudaDeviceProperties
        {
            Name = "NVIDIA RTX 2000 Ada Generation Laptop GPU",
            Major = 8,
            Minor = 9,
            TotalGlobalMem = 8UL * 1024 * 1024 * 1024,
            MultiProcessorCount = 35,
            MaxThreadsPerBlock = 1024,
            WarpSize = 32,
            SharedMemPerBlock = 49152,
            RegsPerBlock = 65536,
            ManagedMemory = 1,
            ConcurrentKernels = 1
        };
    }
}

public class PerformanceBaseline
{
    public double VectorAdd1M { get; set; } = 0.5; // ms
    public double MatrixMul512 { get; set; } = 2.0; // ms
    public double MemoryBandwidth { get; set; } = 250.0; // GB/s
    
    public static PerformanceBaseline Load(string filename)
    {
        if (File.Exists(filename))
        {
            return JsonSerializer.Deserialize<PerformanceBaseline>(
                File.ReadAllText(filename)) ?? new PerformanceBaseline();
        }
        return new PerformanceBaseline();
    }
    
    public void Save(string filename)
    {
        File.WriteAllText(filename, JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        }));
    }
}
```

### Continuous Integration Configuration

```yaml
# .github/workflows/cuda-tests.yml
name: CUDA Backend Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Unit Tests
        run: dotnet test --filter "Category!=Hardware&Category!=Integration" --logger trx --collect:"XPlat Code Coverage"
      - name: Upload Coverage
        uses: codecov/codecov-action@v3

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Integration Tests (Mock)
        run: dotnet test --filter "Category=Integration&Category!=Hardware" --logger trx

  hardware-tests:
    runs-on: [self-hosted, cuda, rtx2000]
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Verify CUDA Installation
        run: nvidia-smi
      - name: Run Hardware Tests
        run: dotnet test --filter "Category=Hardware" --logger trx
      - name: Run Performance Tests
        run: dotnet test --filter "Category=Performance" --logger trx
```

## Test Execution Strategy

### Local Development
1. **Fast Feedback Loop**: Unit tests run on every build (~5 seconds)
2. **Pre-commit**: Integration tests with mocks (~30 seconds)
3. **Daily**: Full hardware test suite (~5 minutes)

### CI/CD Pipeline
1. **PR Validation**: Unit + Integration tests (mock)
2. **Main Branch**: Full test suite including hardware
3. **Nightly**: Performance regression tests + baseline updates

### Test Data Management
- **Synthetic Data**: Generated test vectors and matrices
- **Real-world Datasets**: Small representative datasets for integration tests
- **Performance Baselines**: JSON files with expected performance metrics

### Test Coverage Goals
- **Unit Tests**: >90% line coverage
- **Integration Tests**: >80% scenario coverage  
- **Hardware Tests**: 100% feature coverage for RTX 2000
- **Performance Tests**: Baseline validation for key operations

This comprehensive testing strategy ensures robust validation of the CUDA backend across all scenarios while maintaining fast development cycles and reliable deployment.