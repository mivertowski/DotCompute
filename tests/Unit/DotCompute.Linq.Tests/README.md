# DotCompute.Linq.Tests

Comprehensive unit tests for the DotCompute LINQ extensions module.

## Test Coverage

Total Tests: **180 comprehensive tests**

### Test Files

1. **ComputeQueryableExtensionsTests.cs** (50 tests)
   - AsComputeQueryable extension method tests
   - ToComputeArray conversion tests
   - ComputeSelect projection tests
   - ComputeWhere filtering tests
   - Integration tests for complete query chains

2. **ComputeQueryProviderTests.cs** (50 tests)
   - CreateQuery method tests (generic and non-generic)
   - Execute method tests (expression compilation)
   - ComputeQueryable implementation tests
   - Expression handling tests (Where, Select, OrderBy, etc.)
   - Performance and edge case tests

3. **ServiceCollectionExtensionsTests.cs** (40 tests)
   - AddDotComputeLinq registration tests
   - IComputeLinqProvider implementation tests
   - Dependency injection integration tests
   - Service lifecycle tests (singleton behavior)
   - Edge cases and error handling

4. **FutureExpansionTests.cs** (40 tests)
   - Expression tree analysis patterns
   - Kernel generation pattern documentation
   - Optimization strategy tests
   - Backend selection decision tests
   - Future API design tests

## Test Organization

### AAA Pattern
All tests follow the Arrange-Act-Assert pattern for clarity and maintainability.

### Test Categories
- **Unit Tests**: Isolated component testing
- **Integration Tests**: Multi-component interaction testing
- **Future Design Tests**: Documentation of planned features

### Testing Tools
- **xUnit**: Test framework
- **FluentAssertions**: Assertion library for readable tests
- **NSubstitute**: Mocking framework (for future accelerator mocks)
- **Microsoft.Extensions.DependencyInjection**: DI testing

## Running Tests

```bash
# Run all LINQ tests
dotnet test tests/Unit/DotCompute.Linq.Tests/DotCompute.Linq.Tests.csproj

# Run specific test class
dotnet test --filter "FullyQualifiedName~ComputeQueryableExtensionsTests"

# Run with detailed output
dotnet test --verbosity detailed tests/Unit/DotCompute.Linq.Tests/DotCompute.Linq.Tests.csproj

# Generate code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Current Implementation Coverage

The tests cover the minimal LINQ implementation currently in the codebase:

### Implemented Features
- ✅ `AsComputeQueryable()` - Converts IQueryable to compute-enabled queryable
- ✅ `ToComputeArray()` - Executes query and returns array
- ✅ `ComputeSelect()` - Maps elements with compute acceleration (currently CPU)
- ✅ `ComputeWhere()` - Filters elements with compute acceleration (currently CPU)
- ✅ `ComputeQueryProvider` - LINQ query provider implementation
- ✅ `IComputeLinqProvider` - Service interface for DI
- ✅ `AddDotComputeLinq()` - Service registration extension

### Planned Features (Documented in FutureExpansionTests)
- 🔄 Expression tree analysis for GPU compilation
- 🔄 Kernel generation from LINQ expressions
- 🔄 Optimization strategies (fusion, memory access patterns)
- 🔄 Adaptive backend selection (CPU/GPU)
- 🔄 SIMD vectorization for CPU operations
- 🔄 Custom kernel configuration options
- 🔄 Performance hints and workload characteristics

## Test Scenarios

### Basic Operations
- Element projection (Select)
- Element filtering (Where)
- Type conversions
- Empty collections
- Large datasets (10,000+ elements)

### Expression Handling
- Simple expressions
- Complex expressions with multiple operations
- Chained operations
- Standard LINQ methods (OrderBy, Take, Skip, etc.)
- Aggregations (Sum, Count, Max, Min)

### Service Integration
- Dependency injection registration
- Service lifecycle management
- Logger integration
- Multiple service scopes
- Complex service configurations

### Edge Cases
- Null inputs
- Empty sources
- Single element collections
- Very large datasets (100,000+ elements)
- Complex object types
- Different collection types (Array, List, HashSet, ImmutableArray)

## Future Expansion

The FutureExpansionTests.cs file documents expected behavior for upcoming features:

1. **Expression Compilation**: GPU kernel generation from LINQ expressions
2. **Optimization**: Query fusion and memory access optimization
3. **Backend Selection**: Automatic CPU/GPU decision based on workload
4. **Advanced Operations**: Parallel reductions, SIMD operations
5. **Performance Tuning**: Custom kernel configurations and hints

## Notes

- All tests currently pass with the minimal LINQ implementation
- Tests are designed to be expanded as features are implemented
- Future tests use comments to document expected behavior
- No hardware dependencies - all tests run on CPU
- Thread-safe and can be run in parallel

## Contributing

When adding new LINQ features:
1. Update existing tests to cover new behavior
2. Add new test cases for new functionality
3. Update FutureExpansionTests to reflect implemented features
4. Maintain AAA pattern and clear test naming
5. Ensure tests are independent and can run in any order
