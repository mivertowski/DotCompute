# DotCompute.Tests.Common

This library provides comprehensive test infrastructure for DotCompute projects, including specialized base classes, fixtures, and assertions for GPU and accelerated computing scenarios.

## Quick Start

### Basic Test Structure

```csharp
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Fixtures;
using DotCompute.Tests.Common.Assertions;
using Xunit;
using Xunit.Abstractions;

public class MyComponentTests : TestBase
{
    public MyComponentTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Category", TestCategories.HardwareIndependent)]
    public void Should_ProcessData_WithCorrectResults()
    {
        // Arrange
        var input = TestDataFixture.Small.RandomFloats;
        var expected = TestDataFixture.ExpectedResults.VectorScalarMultiply(input, 2.0f);
        
        // Act
        var actual = ProcessData(input, 2.0f);
        
        // Assert
        actual.ShouldBeApproximatelyEqualTo(expected, tolerance: 1e-6f);
    }
}
```

### GPU-Specific Tests

```csharp
public class GpuComponentTests : GpuTestBase
{
    public GpuComponentTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Category", TestCategories.RequiresCUDA)]
    public void Should_ExecuteKernel_OnGpu()
    {
        // Skip if CUDA not available
        SkipIfNoCuda();
        
        // Take memory snapshot
        TakeGpuMemorySnapshot("before");
        
        // Arrange
        var input = TestDataFixture.Medium.RandomFloats;
        
        // Act
        var executionTime = MeasureGpuKernelTime(() => {
            // Execute your GPU kernel here
        });
        
        // Assert
        executionTime.ShouldBeWithinTimeLimit(100.0); // 100ms limit
        
        TakeGpuMemorySnapshot("after");
        CompareGpuMemorySnapshots("before", "after");
    }
}
```

## Components

### Test Categories (`TestCategories`)

Organize tests by hardware requirements and execution characteristics:

- `HardwareIndependent` - CPU-only tests
- `RequiresGPU` - Any GPU required
- `RequiresCUDA` - NVIDIA CUDA required
- `RequiresOpenCL` - OpenCL required
- `Performance` - Performance benchmarks
- `Integration` - Integration tests
- `MemoryIntensive` - High memory usage tests

### Base Classes

#### `TestBase`
- Hardware detection utilities
- Performance measurement tools
- Memory tracking
- Test data generation helpers
- Platform information logging

#### `GpuTestBase`
- Extends `TestBase` with GPU-specific functionality
- GPU hardware capability detection
- GPU memory tracking and leak detection
- GPU kernel execution timing
- Hardware-specific test skip conditions

### Test Data Fixtures (`TestDataFixture`)

#### Pre-defined Data Sets
- `Small` - 1K elements for unit tests
- `Medium` - 64K elements for integration tests  
- `Large` - 16M elements for performance tests

#### Matrix Data
- `Small32x32` - 32×32 matrices
- `Medium256x256` - 256×256 matrices
- `Large1024x1024` - 1024×1024 matrices

#### Test Kernels
- `VectorAddCuda` - CUDA vector addition
- `VectorAddOpenCL` - OpenCL vector addition
- `MatrixMultiplyCuda` - CUDA matrix multiplication
- `ReductionSumCuda` - CUDA reduction operations

#### Expected Results
- `VectorAdd()` - Expected vector addition results
- `MatrixMultiply()` - Expected matrix multiplication results
- `ReductionSum()` - Expected reduction results
- `ElementWiseOperation()` - Expected element-wise operation results

### Custom Assertions (`CustomAssertions`)

#### Array Assertions
```csharp
// Float array comparison with tolerance
actual.ShouldBeApproximatelyEqualTo(expected, tolerance: 1e-6f);

// Check for finite values only (no NaN/Infinity)
results.ShouldContainOnlyFiniteValues();

// Verify values within bounds
data.ShouldBeWithinBounds(-1.0f, 1.0f);
```

#### Matrix Assertions
```csharp
// Matrix comparison with dimensions
actualMatrix.ShouldBeApproximatelyEqualTo(expectedMatrix, rows: 32, cols: 32, tolerance: 1e-6f);
```

#### Performance Assertions
```csharp
// Execution time validation
executionTime.ShouldBeWithinTimeLimit(maxMs: 100.0);

// Bandwidth requirements
bandwidth.ShouldMeetBandwidthRequirement(minGBps: 50.0);

// Speedup validation
speedup.ShouldAchieveMinimumSpeedup(minSpeedup: 2.0);
```

#### GPU Memory Assertions
```csharp
// Memory usage limits
this.Should().HaveGpuMemoryUsageBelow(maxMemoryMB: 512);

// Memory leak detection
this.Should().NotHaveMemoryLeaks(toleranceMB: 1);
```

## Usage Patterns

### Hardware Detection Pattern
```csharp
[Fact]
[Trait("Category", TestCategories.RequiresCUDA)]
public void CudaSpecificTest()
{
    SkipIfNoCuda();
    // Test implementation
}
```

### Performance Measurement Pattern
```csharp
[Fact]
[Trait("Category", TestCategories.Performance)]
public void PerformanceTest()
{
    using var perfContext = CreatePerformanceContext("MyOperation");
    
    // Setup
    perfContext.Checkpoint("Setup");
    
    // Execute
    var time = MeasureExecutionTime(() => MyOperation(), iterations: 100);
    perfContext.Checkpoint("Execution");
    
    // Validate
    time.ShouldBeWithinTimeLimit(expectedMaxMs);
}
```

### Memory Tracking Pattern
```csharp
[Fact]
[Trait("Category", TestCategories.MemoryIntensive)]
public void MemoryIntensiveTest()
{
    TakeGpuMemorySnapshot("initial");
    
    // Allocate GPU resources
    AllocateGpuMemory();
    TakeGpuMemorySnapshot("allocated");
    
    // Process data
    ProcessData();
    TakeGpuMemorySnapshot("processed");
    
    // Clean up
    CleanupGpuMemory();
    TakeGpuMemorySnapshot("cleanup");
    
    // Verify no leaks
    CompareGpuMemorySnapshots("initial", "cleanup");
}
```

## Best Practices

1. **Use appropriate test categories** to enable selective test execution
2. **Always check hardware availability** before GPU-specific tests
3. **Measure and validate performance** for performance-critical code
4. **Track memory usage** to prevent leaks
5. **Use tolerances** for floating-point comparisons
6. **Generate reproducible test data** using fixed seeds
7. **Log comprehensive test information** for debugging

## Requirements

- .NET 9.0+
- xUnit 2.6+
- FluentAssertions 6.12+
- DotCompute.Core
- DotCompute.Memory

For GPU tests:
- CUDA Toolkit (for CUDA tests)
- OpenCL runtime (for OpenCL tests)
- Compatible GPU hardware