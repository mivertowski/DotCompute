# DotCompute Testing Strategy

**Version**: 1.0 | **Last Updated**: January 2026

---

## Test Pyramid

```
                 ╔═══════════════╗
                 ║   E2E/Perf    ║  5% (~25 tests)
                 ║     Tests     ║
             ╔═══╩═══════════════╩═══╗
             ║  Integration Tests    ║  20% (~100 tests)
         ╔═══╩═══════════════════════╩═══╗
         ║        Unit Tests             ║  75% (~375 tests)
         ╚═══════════════════════════════╝
```

---

## Test Categories

### 1. Unit Tests
**Location**: `tests/Unit/`
**Framework**: xUnit + Moq

```csharp
[Fact]
public async Task BufferAllocation_ReturnsValidBuffer()
{
    var allocator = new MockAllocator();
    var buffer = await allocator.AllocateAsync<float>(1024);

    Assert.NotNull(buffer);
    Assert.Equal(1024, buffer.Length);
}
```

**Coverage Targets**:
| Phase | Target |
|-------|--------|
| v0.6.0 | 95% |
| v0.7.0 | 96% |
| v0.8.0 | 97% |
| v1.0.0 | 98% |

---

### 2. Integration Tests
**Location**: `tests/Integration/`
**Framework**: xUnit + TestContainers

```csharp
[Fact]
public async Task KernelExecution_EndToEnd_ProducesCorrectResult()
{
    await using var accelerator = await CudaAccelerator.CreateAsync();
    using var buffer = await accelerator.AllocateAsync<float>(1024);

    await accelerator.ExecuteAsync("VectorScale", buffer, 2.0f);

    var result = buffer.AsSpan().ToArray();
    Assert.All(result, v => Assert.Equal(2.0f, v));
}
```

---

### 3. Hardware Tests
**Location**: `tests/Hardware/`
**Annotation**: `[SkippableFact]`

```csharp
[SkippableFact]
[Trait("Category", "Hardware")]
public async Task CudaKernel_ExecutesOnGpu()
{
    Skip.IfNot(CudaDeviceDetector.HasDevice(), "No CUDA device");

    await using var accelerator = await CudaAccelerator.CreateAsync();
    // Test GPU-specific functionality
}
```

**Hardware Matrix**:
| Backend | Required Devices |
|---------|-----------------|
| CUDA | RTX 2000+ (CC 7.5+) |
| Metal | M1/M2/M3 Mac |
| OpenCL | NVIDIA + AMD + Intel |
| CPU | Any x64/ARM64 |

---

### 4. Architecture Tests
**Location**: `tests/Architecture/`
**Framework**: NetArchTest

```csharp
[Fact]
public void Core_ShouldNotDependOn_Backends()
{
    var result = Types.InAssembly(typeof(IAccelerator).Assembly)
        .ShouldNot()
        .HaveDependencyOn("DotCompute.Backend.Cuda")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

**Rules**:
- Core → No backend dependencies
- Backends → No cross-dependencies
- Extensions → Only Core dependencies
- Naming conventions enforced

---

### 5. Performance Tests
**Location**: `benchmarks/`
**Framework**: BenchmarkDotNet

```csharp
[Benchmark]
public async Task VectorAdd_1M_Elements()
{
    await _accelerator.ExecuteAsync("VectorAdd", _a, _b, _c);
}
```

**Regression Threshold**: <5% degradation

---

### 6. Security Tests
**Schedule**: Monthly
**Approach**: SAST + DAST + Penetration

| Test Type | Tool | Frequency |
|-----------|------|-----------|
| SAST | CodeQL | Every PR |
| Dependency | Dependabot | Daily |
| Secrets | Gitleaks | Every PR |
| Penetration | External | Quarterly |

---

### 7. Chaos Tests
**Location**: `tests/Chaos/`
**Framework**: Custom + Polly

```csharp
[Fact]
public async Task CircuitBreaker_OpensAfterFailures()
{
    var chaos = new ChaosInjector()
        .InjectFailure(probability: 1.0);

    for (int i = 0; i < 5; i++)
        await Assert.ThrowsAsync<DeviceException>(
            () => _accelerator.ExecuteAsync("Kernel"));

    Assert.Equal(CircuitState.Open, _breaker.State);
}
```

---

## CI/CD Pipeline

### Pull Request Checks
```yaml
pr-checks:
  - unit-tests (5 min)
  - integration-tests (10 min)
  - architecture-tests (2 min)
  - security-scan (3 min)
  - build-all-platforms (15 min)
```

### Nightly Builds
```yaml
nightly:
  - full-test-suite
  - hardware-tests (CUDA, Metal, OpenCL)
  - performance-benchmarks
  - security-scans
  - documentation-build
```

### Release Validation
```yaml
release:
  - all-nightly-checks
  - 72-hour-soak-test
  - multi-platform-validation
  - performance-certification
```

---

## Quality Gates

| Gate | Criteria | Enforcement |
|------|----------|-------------|
| **G1** | Unit tests pass | PR blocking |
| **G2** | Coverage ≥ target | PR blocking |
| **G3** | No security issues | PR blocking |
| **G4** | Architecture valid | PR blocking |
| **G5** | Performance ok | Nightly blocking |
| **G6** | Hardware tests pass | Release blocking |

---

## Test Data Management

### Synthetic Data
```csharp
public static class TestData
{
    public static float[] RandomFloats(int count, int seed = 42)
    {
        var rng = new Random(seed);
        return Enumerable.Range(0, count)
            .Select(_ => (float)rng.NextDouble())
            .ToArray();
    }
}
```

### Golden Files
```
tests/GoldenFiles/
├── kernels/
│   ├── vectoradd.expected.json
│   └── matmul.expected.json
└── compilation/
    ├── cuda-ptx.expected.txt
    └── metal-msl.expected.txt
```

---

## Backend-Agnostic Testing

```csharp
[Theory]
[InlineData(BackendType.CPU)]
[InlineData(BackendType.CUDA)]
[InlineData(BackendType.Metal)]
[InlineData(BackendType.OpenCL)]
public async Task Buffer_SupportsBasicOperations(BackendType backend)
{
    Skip.IfNot(await IsAvailable(backend));

    using var accelerator = await CreateAccelerator(backend);
    using var buffer = await accelerator.AllocateAsync<float>(1024);

    Assert.Equal(1024, buffer.Length);
}
```

---

## Reporting

### Coverage Reports
- **Tool**: Coverlet + ReportGenerator
- **Location**: `artifacts/coverage/`
- **Format**: HTML + Cobertura

### Performance Reports
- **Tool**: BenchmarkDotNet
- **Location**: `artifacts/benchmarks/`
- **Tracking**: Historical comparison

### Test Results
- **Format**: TRX + JUnit XML
- **Dashboard**: GitHub Actions summary
