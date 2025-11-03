# Debugging System Architecture

The Debugging System provides production-grade cross-backend validation, performance analysis, and determinism testing to ensure kernel correctness across CPU and GPU implementations.

## Architecture Overview

```
Application Code
    ↓
IComputeOrchestrator
    ↓
DebugIntegratedOrchestrator (Transparent Wrapper)
    ↓
┌─────────────────────────────────────────────────┐
│         IKernelDebugService                     │
├─────────────────────────────────────────────────┤
│  - Cross-backend validation                     │
│  - Performance profiling                        │
│  - Determinism testing                          │
│  - Memory pattern analysis                      │
└─────────────────────────────────────────────────┘
    ↓                         ↓
Primary Execution      Reference Execution
(GPU - Fast)          (CPU - Trusted)
    ↓                         ↓
Compare Results → Validation Report
```

## Design Principles

### 1. **Production-Safe**
- **Minimal Overhead**: < 5% in Production profile
- **Selective Validation**: Only suspicious results checked
- **No Breaking Changes**: Warnings only, never throws

### 2. **Transparent Integration**
- **Wrapper Pattern**: Wraps `IComputeOrchestrator` transparently
- **No Code Changes**: Enable via configuration only
- **Opt-In**: Disabled by default in Release builds

### 3. **Actionable Insights**
- **Detailed Reports**: What failed, where, and why
- **Suggested Fixes**: Guidance on resolving issues
- **Performance Data**: Execution times, memory usage

### 4. **Multiple Profiles**
- **Development**: Extensive validation (2-5x overhead)
- **Testing**: Balanced validation (20-50% overhead)
- **Production**: Selective validation (< 5% overhead)
- **Disabled**: No validation overhead

## IKernelDebugService Interface

Complete debugging and validation interface:

```csharp
public interface IKernelDebugService
{
    /// <summary>
    /// Validates kernel execution across multiple backends
    /// </summary>
    Task<ValidationResult> ValidateCrossBackendAsync(
        string kernelName,
        object parameters,
        AcceleratorType primaryBackend,
        AcceleratorType referenceBackend,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Profiles kernel performance characteristics
    /// </summary>
    Task<PerformanceProfile> ProfileKernelAsync(
        string kernelName,
        object parameters,
        AcceleratorType backend,
        int iterations = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests kernel determinism (same input → same output)
    /// </summary>
    Task<DeterminismResult> TestDeterminismAsync(
        string kernelName,
        object parameters,
        AcceleratorType backend,
        int runs = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes memory access patterns for optimization opportunities
    /// </summary>
    Task<MemoryAccessReport> AnalyzeMemoryPatternsAsync(
        string kernelName,
        object parameters,
        AcceleratorType backend,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates numerical stability (no overflow, NaN, Inf)
    /// </summary>
    Task<NumericalStabilityResult> ValidateNumericalStabilityAsync(
        string kernelName,
        object parameters,
        AcceleratorType backend,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares kernel output against known golden reference
    /// </summary>
    Task<ValidationResult> ValidateAgainstGoldenAsync(
        string kernelName,
        object parameters,
        object expectedOutput,
        AcceleratorType backend,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stress tests kernel with random inputs
    /// </summary>
    Task<StressTestResult> StressTestKernelAsync(
        string kernelName,
        IInputGenerator inputGenerator,
        AcceleratorType backend,
        int iterations = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects race conditions and thread-safety issues
    /// </summary>
    Task<RaceConditionReport> DetectRaceConditionsAsync(
        string kernelName,
        object parameters,
        AcceleratorType backend,
        int concurrentExecutions = 100,
        CancellationToken cancellationToken = default);
}
```

## Cross-Backend Validation

### Validation Strategy

```csharp
public class KernelDebugService : IKernelDebugService
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly DebugProfile _profile;
    private readonly ILogger<KernelDebugService> _logger;

    public async Task<ValidationResult> ValidateCrossBackendAsync(
        string kernelName,
        object parameters,
        AcceleratorType primaryBackend,
        AcceleratorType referenceBackend)
    {
        // 1. Execute on primary backend (e.g., GPU)
        var primaryStopwatch = Stopwatch.StartNew();
        var primaryResult = await _orchestrator.ExecuteKernelAsync(
            kernelName,
            parameters,
            forceBackend: primaryBackend
        );
        primaryStopwatch.Stop();

        // 2. Execute on reference backend (e.g., CPU)
        var referenceStopwatch = Stopwatch.StartNew();
        var referenceResult = await _orchestrator.ExecuteKernelAsync(
            kernelName,
            parameters,
            forceBackend: referenceBackend
        );
        referenceStopwatch.Stop();

        // 3. Compare results
        var differences = CompareResults(
            primaryResult,
            referenceResult,
            tolerance: _profile.ToleranceThreshold
        );

        // 4. Analyze differences
        var severity = DetermineSeverity(differences);

        // 5. Log findings
        if (differences.Any())
        {
            _logger.LogWarning(
                "Cross-backend validation found {Count} differences for {Kernel}. " +
                "Primary: {Primary} ({PrimaryTime}ms), Reference: {Reference} ({ReferenceTime}ms)",
                differences.Count,
                kernelName,
                primaryBackend,
                primaryStopwatch.Elapsed.TotalMilliseconds,
                referenceBackend,
                referenceStopwatch.Elapsed.TotalMilliseconds
            );
        }

        return new ValidationResult
        {
            IsValid = !differences.Any() || severity < Severity.Critical,
            Differences = differences,
            PrimaryBackend = primaryBackend,
            PrimaryExecutionTime = primaryStopwatch.Elapsed,
            ReferenceBackend = referenceBackend,
            ReferenceExecutionTime = referenceStopwatch.Elapsed,
            Severity = severity,
            Recommendation = GenerateRecommendation(differences, severity)
        };
    }

    private List<Difference> CompareResults(
        object primaryResult,
        object referenceResult,
        double tolerance)
    {
        var differences = new List<Difference>();

        // Handle arrays
        if (primaryResult is Array primaryArray && referenceResult is Array referenceArray)
        {
            if (primaryArray.Length != referenceArray.Length)
            {
                differences.Add(new Difference
                {
                    Type = DifferenceType.LengthMismatch,
                    Index = -1,
                    PrimaryValue = primaryArray.Length,
                    ReferenceValue = referenceArray.Length
                });
                return differences;
            }

            // Compare elements
            for (int i = 0; i < primaryArray.Length; i++)
            {
                var primaryValue = Convert.ToDouble(primaryArray.GetValue(i));
                var referenceValue = Convert.ToDouble(referenceArray.GetValue(i));

                if (!AreEqual(primaryValue, referenceValue, tolerance))
                {
                    differences.Add(new Difference
                    {
                        Type = DifferenceType.ValueMismatch,
                        Index = i,
                        PrimaryValue = primaryValue,
                        ReferenceValue = referenceValue,
                        RelativeError = Math.Abs((primaryValue - referenceValue) / referenceValue)
                    });
                }
            }
        }

        return differences;
    }

    private bool AreEqual(double a, double b, double tolerance)
    {
        // Handle special values
        if (double.IsNaN(a) && double.IsNaN(b)) return true;
        if (double.IsInfinity(a) && double.IsInfinity(b)) return a.Equals(b);

        // Relative tolerance comparison
        var diff = Math.Abs(a - b);
        var magnitude = Math.Max(Math.Abs(a), Math.Abs(b));

        return diff <= tolerance * magnitude || diff <= tolerance;
    }
}
```

### Validation Result

```csharp
public class ValidationResult
{
    /// <summary>
    /// True if validation passed (within tolerance)
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of differences found
    /// </summary>
    public IReadOnlyList<Difference> Differences { get; init; }

    /// <summary>
    /// Primary backend used for execution
    /// </summary>
    public AcceleratorType PrimaryBackend { get; init; }

    /// <summary>
    /// Primary execution time
    /// </summary>
    public TimeSpan PrimaryExecutionTime { get; init; }

    /// <summary>
    /// Reference backend (typically CPU)
    /// </summary>
    public AcceleratorType ReferenceBackend { get; init; }

    /// <summary>
    /// Reference execution time
    /// </summary>
    public TimeSpan ReferenceExecutionTime { get; init; }

    /// <summary>
    /// Speedup factor (reference time / primary time)
    /// </summary>
    public double Speedup => ReferenceExecutionTime.TotalMilliseconds /
                             PrimaryExecutionTime.TotalMilliseconds;

    /// <summary>
    /// Severity of validation failure
    /// </summary>
    public Severity Severity { get; init; }

    /// <summary>
    /// Recommended action to fix issues
    /// </summary>
    public string Recommendation { get; init; }
}

public class Difference
{
    public DifferenceType Type { get; init; }
    public int Index { get; init; }
    public object PrimaryValue { get; init; }
    public object ReferenceValue { get; init; }
    public double RelativeError { get; init; }
}

public enum DifferenceType
{
    ValueMismatch,
    LengthMismatch,
    TypeMismatch,
    NumericalInstability
}

public enum Severity
{
    None,
    Low,      // Within 10x tolerance
    Medium,   // Within 100x tolerance
    High,     // Within 1000x tolerance
    Critical  // Beyond 1000x tolerance
}
```

## Debugging Profiles

### Profile Configuration

```csharp
public enum DebugProfile
{
    /// <summary>
    /// No validation (0% overhead)
    /// </summary>
    Disabled,

    /// <summary>
    /// Minimal validation in production (< 5% overhead)
    /// Validates only suspicious results (NaN, Inf, unusually large values)
    /// </summary>
    Production,

    /// <summary>
    /// Balanced validation for testing (20-50% overhead)
    /// Validates random 10% sample of executions
    /// </summary>
    Testing,

    /// <summary>
    /// Extensive validation for development (2-5x overhead)
    /// Validates every execution
    /// </summary>
    Development
}

public class DebugOptions
{
    /// <summary>
    /// Debugging profile
    /// </summary>
    public DebugProfile Profile { get; set; } = DebugProfile.Disabled;

    /// <summary>
    /// Enable cross-backend validation
    /// </summary>
    public bool EnableCrossBackendValidation { get; set; } = true;

    /// <summary>
    /// Reference backend for validation (default: CPU)
    /// </summary>
    public AcceleratorType ReferenceBackend { get; set; } = AcceleratorType.CPU;

    /// <summary>
    /// Tolerance threshold for floating-point comparison
    /// </summary>
    public double ToleranceThreshold { get; set; } = 1e-5;

    /// <summary>
    /// Validate all executions (Development profile)
    /// </summary>
    public bool ValidateAllExecutions { get; set; } = false;

    /// <summary>
    /// Sampling rate for Testing profile (0.0 to 1.0)
    /// </summary>
    public double SamplingRate { get; set; } = 0.1; // 10% sample

    /// <summary>
    /// Throw exception on validation failure
    /// </summary>
    public bool ThrowOnValidationFailure { get; set; } = false;

    /// <summary>
    /// Enable performance profiling
    /// </summary>
    public bool EnablePerformanceProfiling { get; set; } = true;

    /// <summary>
    /// Enable determinism testing
    /// </summary>
    public bool EnableDeterminismTesting { get; set; } = false;
}
```

### Profile Characteristics

| Profile | Overhead | Validation | Use Case |
|---------|----------|------------|----------|
| **Disabled** | 0% | None | Production, performance-critical |
| **Production** | < 5% | Suspicious results only | Production monitoring |
| **Testing** | 20-50% | 10% random sample | CI/CD, integration tests |
| **Development** | 2-5x | Every execution | Local development, debugging |

### Profile Implementation

```csharp
public class DebugIntegratedOrchestrator : IComputeOrchestrator
{
    private readonly IComputeOrchestrator _innerOrchestrator;
    private readonly IKernelDebugService _debugService;
    private readonly DebugOptions _options;
    private readonly Random _random = new();

    public async Task<TResult> ExecuteKernelAsync<TResult>(
        string kernelName,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        // Check if validation needed based on profile
        if (!ShouldValidate(kernelName))
        {
            return await _innerOrchestrator.ExecuteKernelAsync<TResult>(
                kernelName,
                parameters,
                cancellationToken
            );
        }

        // Execute with validation
        var result = await _innerOrchestrator.ExecuteKernelAsync<TResult>(
            kernelName,
            parameters,
            cancellationToken
        );

        // Validate result
        var validation = await _debugService.ValidateCrossBackendAsync(
            kernelName,
            parameters,
            primaryBackend: AcceleratorType.Auto,
            referenceBackend: _options.ReferenceBackend,
            cancellationToken
        );

        // Handle validation failure
        if (!validation.IsValid)
        {
            HandleValidationFailure(kernelName, validation);
        }

        return result;
    }

    private bool ShouldValidate(string kernelName)
    {
        return _options.Profile switch
        {
            DebugProfile.Disabled => false,
            DebugProfile.Development => true,
            DebugProfile.Testing => _random.NextDouble() < _options.SamplingRate,
            DebugProfile.Production => false, // Validate in post-execution hook instead
            _ => false
        };
    }

    private void HandleValidationFailure(string kernelName, ValidationResult validation)
    {
        var message = $"Validation failed for kernel '{kernelName}': " +
                      $"{validation.Differences.Count} differences found " +
                      $"(severity: {validation.Severity}). " +
                      $"Recommendation: {validation.Recommendation}";

        if (_options.ThrowOnValidationFailure)
        {
            throw new ValidationException(message, validation);
        }
        else
        {
            _logger.LogWarning(message);
        }
    }
}
```

## Performance Profiling

### Performance Profile

```csharp
public class PerformanceProfile
{
    /// <summary>
    /// Kernel name
    /// </summary>
    public string KernelName { get; init; }

    /// <summary>
    /// Backend used for execution
    /// </summary>
    public AcceleratorType Backend { get; init; }

    /// <summary>
    /// Number of iterations executed
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Total execution time
    /// </summary>
    public TimeSpan TotalTime { get; init; }

    /// <summary>
    /// Average execution time per iteration
    /// </summary>
    public TimeSpan AverageTime => TimeSpan.FromTicks(TotalTime.Ticks / Iterations);

    /// <summary>
    /// Minimum execution time
    /// </summary>
    public TimeSpan MinTime { get; init; }

    /// <summary>
    /// Maximum execution time
    /// </summary>
    public TimeSpan MaxTime { get; init; }

    /// <summary>
    /// Standard deviation of execution times
    /// </summary>
    public TimeSpan StandardDeviation { get; init; }

    /// <summary>
    /// Throughput (iterations per second)
    /// </summary>
    public double Throughput => Iterations / TotalTime.TotalSeconds;

    /// <summary>
    /// Memory allocated (bytes)
    /// </summary>
    public long MemoryAllocated { get; init; }

    /// <summary>
    /// Memory transferred (bytes)
    /// </summary>
    public long MemoryTransferred { get; init; }

    /// <summary>
    /// Compute intensity (FLOPS)
    /// </summary>
    public double ComputeIntensity { get; init; }

    /// <summary>
    /// Performance in GFLOPS
    /// </summary>
    public double GFLOPS => ComputeIntensity / AverageTime.TotalSeconds / 1e9;
}
```

### Profiling Implementation

```csharp
public async Task<PerformanceProfile> ProfileKernelAsync(
    string kernelName,
    object parameters,
    AcceleratorType backend,
    int iterations = 100)
{
    var times = new List<TimeSpan>();
    long memoryAllocated = 0;
    long memoryTransferred = 0;

    // Warm-up (exclude from measurements)
    await _orchestrator.ExecuteKernelAsync(
        kernelName,
        parameters,
        forceBackend: backend
    );

    // Profile iterations
    var totalStopwatch = Stopwatch.StartNew();

    for (int i = 0; i < iterations; i++)
    {
        var iterationStopwatch = Stopwatch.StartNew();

        await _orchestrator.ExecuteKernelAsync(
            kernelName,
            parameters,
            forceBackend: backend
        );

        iterationStopwatch.Stop();
        times.Add(iterationStopwatch.Elapsed);
    }

    totalStopwatch.Stop();

    // Calculate statistics
    var minTime = times.Min();
    var maxTime = times.Max();
    var avgTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks));
    var variance = times.Average(t => Math.Pow((t - avgTime).Ticks, 2));
    var stdDev = TimeSpan.FromTicks((long)Math.Sqrt(variance));

    return new PerformanceProfile
    {
        KernelName = kernelName,
        Backend = backend,
        Iterations = iterations,
        TotalTime = totalStopwatch.Elapsed,
        MinTime = minTime,
        MaxTime = maxTime,
        StandardDeviation = stdDev,
        MemoryAllocated = memoryAllocated,
        MemoryTransferred = memoryTransferred
    };
}
```

## Determinism Testing

### Determinism Result

```csharp
public class DeterminismResult
{
    /// <summary>
    /// True if kernel is deterministic (all runs produce same output)
    /// </summary>
    public bool IsDeterministic { get; init; }

    /// <summary>
    /// Number of runs executed
    /// </summary>
    public int Runs { get; init; }

    /// <summary>
    /// Differences found between runs
    /// </summary>
    public IReadOnlyList<DeterminismViolation> Violations { get; init; }

    /// <summary>
    /// Likely cause of non-determinism
    /// </summary>
    public NonDeterminismCause? Cause { get; init; }
}

public class DeterminismViolation
{
    public int RunIndex { get; init; }
    public int ElementIndex { get; init; }
    public object ExpectedValue { get; init; }
    public object ActualValue { get; init; }
}

public enum NonDeterminismCause
{
    RaceCondition,        // Concurrent writes to same location
    UnorderedReduction,   // Non-associative reduction operation
    FloatingPointError,   // Accumulated FP rounding errors
    RandomNumberGenerator, // Uses RNG without fixed seed
    SystemClock,          // Uses system time
    Unknown
}
```

### Testing Implementation

```csharp
public async Task<DeterminismResult> TestDeterminismAsync(
    string kernelName,
    object parameters,
    AcceleratorType backend,
    int runs = 10)
{
    var results = new List<object>();

    // Execute multiple runs with same input
    for (int i = 0; i < runs; i++)
    {
        var result = await _orchestrator.ExecuteKernelAsync(
            kernelName,
            parameters,
            forceBackend: backend
        );

        results.Add(result);
    }

    // Compare all results against first run
    var referenceResult = results[0];
    var violations = new List<DeterminismViolation>();

    for (int run = 1; run < runs; run++)
    {
        var differences = CompareResults(referenceResult, results[run], tolerance: 0);

        foreach (var diff in differences)
        {
            violations.Add(new DeterminismViolation
            {
                RunIndex = run,
                ElementIndex = diff.Index,
                ExpectedValue = diff.ReferenceValue,
                ActualValue = diff.PrimaryValue
            });
        }
    }

    // Analyze violations to determine cause
    var cause = AnalyzeDeterminismViolations(violations);

    return new DeterminismResult
    {
        IsDeterministic = !violations.Any(),
        Runs = runs,
        Violations = violations,
        Cause = violations.Any() ? cause : null
    };
}

private NonDeterminismCause AnalyzeDeterminismViolations(
    List<DeterminismViolation> violations)
{
    // Pattern analysis to determine likely cause
    if (violations.All(v => v.ElementIndex % 32 == 0))
    {
        return NonDeterminismCause.RaceCondition; // Aligned accesses suggest race
    }

    if (violations.Count > 1000)
    {
        return NonDeterminismCause.FloatingPointError; // Many small errors
    }

    // More sophisticated analysis...
    return NonDeterminismCause.Unknown;
}
```

## Memory Pattern Analysis

### Memory Access Report

```csharp
public class MemoryAccessReport
{
    /// <summary>
    /// Kernel name
    /// </summary>
    public string KernelName { get; init; }

    /// <summary>
    /// Total memory accesses
    /// </summary>
    public long TotalAccesses { get; init; }

    /// <summary>
    /// Sequential access percentage (0.0 to 1.0)
    /// </summary>
    public double SequentialAccessRate { get; init; }

    /// <summary>
    /// Random access percentage (0.0 to 1.0)
    /// </summary>
    public double RandomAccessRate { get; init; }

    /// <summary>
    /// Cache hit rate (estimated, 0.0 to 1.0)
    /// </summary>
    public double CacheHitRate { get; init; }

    /// <summary>
    /// Memory bandwidth utilization (0.0 to 1.0)
    /// </summary>
    public double BandwidthUtilization { get; init; }

    /// <summary>
    /// Bank conflicts detected (GPU specific)
    /// </summary>
    public int BankConflicts { get; init; }

    /// <summary>
    /// Optimization suggestions
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; init; }
}
```

## Service Registration

### Configuration

```csharp
// Minimal setup (disabled by default)
services.AddDotComputeRuntime();

// Enable production debugging
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Production;
    options.EnableCrossBackendValidation = true;
    options.ReferenceBackend = AcceleratorType.CPU;
    options.ToleranceThreshold = 1e-5;
    options.ThrowOnValidationFailure = false; // Log only
});

// Development debugging (extensive validation)
#if DEBUG
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Development;
    options.ValidateAllExecutions = true;
    options.EnablePerformanceProfiling = true;
    options.EnableDeterminismTesting = true;
    options.ThrowOnValidationFailure = true; // Fail fast
});
#endif

// Testing profile (CI/CD)
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Testing;
    options.SamplingRate = 0.1; // Validate 10% of executions
    options.ThrowOnValidationFailure = true;
});
```

## Usage Examples

### Example 1: Cross-Backend Validation

```csharp
// Arrange
var debugService = serviceProvider.GetRequiredService<IKernelDebugService>();

// Act
var validation = await debugService.ValidateCrossBackendAsync(
    kernelName: "MatrixMultiply",
    parameters: new { matrixA, matrixB },
    primaryBackend: AcceleratorType.CUDA,
    referenceBackend: AcceleratorType.CPU
);

// Assert
if (!validation.IsValid)
{
    Console.WriteLine($"Validation failed with {validation.Differences.Count} differences");
    Console.WriteLine($"Severity: {validation.Severity}");
    Console.WriteLine($"Recommendation: {validation.Recommendation}");
    Console.WriteLine($"GPU execution: {validation.PrimaryExecutionTime.TotalMilliseconds}ms");
    Console.WriteLine($"CPU execution: {validation.ReferenceExecutionTime.TotalMilliseconds}ms");
    Console.WriteLine($"Speedup: {validation.Speedup:F2}x");
}
```

### Example 2: Performance Profiling

```csharp
var profile = await debugService.ProfileKernelAsync(
    kernelName: "VectorAdd",
    parameters: new { a = dataA, b = dataB },
    backend: AcceleratorType.CUDA,
    iterations: 1000
);

Console.WriteLine($"Average time: {profile.AverageTime.TotalMicroseconds:F2}μs");
Console.WriteLine($"Min/Max: {profile.MinTime.TotalMicroseconds:F2}μs / {profile.MaxTime.TotalMicroseconds:F2}μs");
Console.WriteLine($"Std dev: {profile.StandardDeviation.TotalMicroseconds:F2}μs");
Console.WriteLine($"Throughput: {profile.Throughput:F0} ops/sec");
```

### Example 3: Determinism Testing

```csharp
var determinism = await debugService.TestDeterminismAsync(
    kernelName: "ParallelSum",
    parameters: new { data = inputData },
    backend: AcceleratorType.CUDA,
    runs: 100
);

if (!determinism.IsDeterministic)
{
    Console.WriteLine($"Kernel is non-deterministic!");
    Console.WriteLine($"Found {determinism.Violations.Count} violations");
    Console.WriteLine($"Likely cause: {determinism.Cause}");
}
```

## Testing Strategy

### Debug Service Tests

```csharp
[Fact]
public async Task ValidateCrossBackend_IdenticalResults_ReturnsValid()
{
    // Arrange
    var debugService = CreateDebugService();
    var input = Enumerable.Range(0, 1000).Select(i => (float)i).ToArray();

    // Act
    var validation = await debugService.ValidateCrossBackendAsync(
        "VectorDouble",
        new { input },
        AcceleratorType.CUDA,
        AcceleratorType.CPU
    );

    // Assert
    Assert.True(validation.IsValid);
    Assert.Empty(validation.Differences);
}

[Fact]
public async Task ProfileKernel_ReturnsAccurateStatistics()
{
    // Arrange
    var debugService = CreateDebugService();

    // Act
    var profile = await debugService.ProfileKernelAsync(
        "SimpleKernel",
        parameters: new { size = 1000 },
        backend: AcceleratorType.CPU,
        iterations: 100
    );

    // Assert
    Assert.Equal(100, profile.Iterations);
    Assert.True(profile.MinTime <= profile.AverageTime);
    Assert.True(profile.AverageTime <= profile.MaxTime);
    Assert.True(profile.Throughput > 0);
}
```

## Related Documentation

- [Architecture Overview](overview.md)
- [Core Orchestration](core-orchestration.md)
- [Optimization Engine](optimization-engine.md)
- [Debugging Guide](../guides/debugging-guide.md)
- [Troubleshooting](../guides/troubleshooting.md)
