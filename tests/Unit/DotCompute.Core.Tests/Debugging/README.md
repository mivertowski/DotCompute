# DotCompute.Core Debugging Tests

## Overview

This directory contains comprehensive unit tests for the DotCompute.Core debugging subsystem, targeting 160-200 tests with full coverage of debug services and integration.

## Test Files

### 1. KernelDebugServiceTests.cs (~56 tests)
Tests for the main `KernelDebugService` class:
- Cross-backend validation
- Kernel execution on specific backends
- Result comparison between backends
- Execution tracing
- Determinism validation
- Memory pattern analysis
- Backend information retrieval
- Service configuration
- Comprehensive debugging workflows
- Report generation and export
- Performance reporting
- Resource utilization analysis
- Accelerator management
- Service statistics
- Disposal and cleanup

### 2. DebugIntegratedOrchestratorTests.cs (~40-50 tests)
Tests for the `DebugIntegratedOrchestrator` wrapper:
- Transparent debugging integration
- Debug hooks enablement/disablement
- Execution with various accelerators
- Buffer-based execution
- Pre-execution validation
- Post-execution validation
- Cross-backend validation
- Performance monitoring
- Error analysis
- Determinism testing
- Validation failure handling

### 3. DebugExecutionOptionsTests.cs (~20-25 tests)
Tests for debug configuration options:
- Option property validation
- Default value verification
- Option combinations
- Configuration scenarios

### 4. ValidationResultTypesTests.cs (~25-30 tests)
Tests for validation result types:
- KernelValidationResult
- DeterminismReport
- MemoryAnalysisReport
- KernelExecutionResult
- ResultComparisonReport
- KernelExecutionTrace
- Report format types

## Current Status

**⚠️ Compilation Status**: Tests exist but have compilation errors that need fixing

### Known Issues

1. **KernelExecutionResult initialization**: Missing required `Success` and `Handle` properties
2. **Property name mismatches**: `Iterations` vs `ExecutionCount`/`RunCount` in DeterminismReport
3. **Missing type imports**: Several report types need proper using statements
4. **Type ambiguities**: ComparisonStrategy exists in multiple namespaces
5. **Interface changes**: IAccelerator property access needs updates

## Test Coverage Target

- **Target**: 160-200 comprehensive tests
- **Current**: ~140-160 tests (estimated)
- **Remaining**: ~20-40 tests to reach upper target

## Running Tests

Once compilation errors are fixed:

```bash
# Build tests
dotnet build tests/Unit/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj

# Run all debugging tests
dotnet test tests/Unit/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj --filter "FullyQualifiedName~DotCompute.Core.Tests.Debugging"

# Run specific test class
dotnet test --filter "FullyQualifiedName~KernelDebugServiceTests"
dotnet test --filter "FullyQualifiedName~DebugIntegratedOrchestratorTests"
```

## Test Patterns

All tests follow these patterns:

### AAA Pattern
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var service = new KernelDebugService(_logger, _loggerFactory);
    var input = CreateTestInput();
    
    // Act
    var result = await service.MethodAsync(input);
    
    // Assert
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
}
```

### Mocking
```csharp
_mockDebugService.GetAvailableBackendsAsync().Returns(new List<BackendInfo>
{
    new() { Name = "CPU", IsAvailable = true }
});
```

### Disposal Testing
```csharp
[Fact]
public void Method_AfterDispose_ShouldThrowObjectDisposedException()
{
    var service = new KernelDebugService(_logger, _loggerFactory);
    service.Dispose();
    
    var act = () => service.Method();
    act.Should().Throw<ObjectDisposedException>();
}
```

## Next Steps

1. **Fix Compilation Errors**:
   - Create test helper methods
   - Fix KernelExecutionResult initializations
   - Add missing using statements
   - Resolve type ambiguities

2. **Verify Test Correctness**:
   - Run all tests
   - Fix failing assertions
   - Validate mock setups

3. **Enhance Coverage**:
   - Add edge case tests
   - Add integration scenarios
   - Add thread-safety tests
   - Reach 200 test target

## Dependencies

- xUnit: Test framework
- FluentAssertions: Fluent assertion library
- NSubstitute: Mocking framework
- DotCompute.Core: System under test
- DotCompute.Abstractions: Interface definitions

## Test Quality Standards

- ✅ Follow AAA pattern
- ✅ Use descriptive test names
- ✅ Test both success and failure scenarios
- ✅ Mock external dependencies
- ✅ Test disposal patterns
- ✅ Verify ObjectDisposedException after disposal
- ✅ Test argument validation
- ✅ Test edge cases

## Documentation

See `/docs/testing/debugging-tests-analysis.md` for detailed analysis and implementation plan.
