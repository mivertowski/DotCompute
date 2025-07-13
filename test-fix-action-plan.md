# Test Suite Fix Action Plan

## Immediate Actions Required (In Order)

### 1. Fix Runtime Compilation Errors
**File**: `src/DotCompute.Runtime/AcceleratorRuntime.cs`
- [ ] Fix IDisposable pattern (CA1063, CA1816)
- [ ] Add braces to if statement (IDE0011, IDE2001)
- [ ] Replace synchronous wait with async (VSTHRD002)
- [ ] Use LoggerMessage delegates for performance (CA1848)

### 2. Fix Plugin Compilation Errors
**File**: `src/DotCompute.Plugins/Core/AotPluginRegistry.cs`
- [ ] Remove or implement unused events (CS0067)
- [ ] Fix code style violations (IDE2001)
- [ ] Make fields readonly (IDE0044)

### 3. Fix Generator Test Dependencies
**File**: `tests/DotCompute.Generators.Tests/DotCompute.Generators.Tests.csproj`
- [ ] Update Microsoft.CodeAnalysis packages to consistent versions
- [ ] Add missing Microsoft.CodeAnalysis.CSharp.SourceGenerators package
- [ ] Fix sealed type inheritance issues in test helpers

### 4. Fix Abstractions Test Issues
**File**: `tests/DotCompute.Abstractions.Tests/InterfaceContractTests.cs`
- [ ] Update FluentAssertions to latest version
- [ ] Fix missing extension methods (BeInterface, BeClass)
- [ ] Fix property access issues (DeviceMemory)
- [ ] Add missing property definitions

### 5. Fix Package Management
**All test projects**
- [ ] Remove duplicate PackageReference entries
- [ ] Update SixLabors.ImageSharp to 3.1.7+ (security fix)
- [ ] Ensure Directory.Packages.props has correct versions

### 6. Verify Phase 3 Test Coverage
After fixing compilation:
- [ ] Ensure CUDA backend tests cover all operations
- [ ] Ensure Metal backend tests cover all operations
- [ ] Verify pipeline tests cover all stages
- [ ] Verify source generator tests are comprehensive
- [ ] Check for and fix any skipped tests

### 7. Add Missing Tests
- [ ] Pipeline stage tests in Core.Tests
- [ ] Multi-backend integration scenarios
- [ ] Source generator edge cases
- [ ] Performance benchmarks for GPU operations

## Expected Outcome

Once all fixes are applied:
1. `dotnet test` should run without compilation errors
2. All tests should pass (or have valid skip reasons)
3. Test coverage should be >80% for Phase 3 components
4. No security vulnerabilities in dependencies
5. Clean code analysis results

## Verification Steps

```bash
# After fixes, run:
dotnet clean
dotnet restore
dotnet build
dotnet test --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage"

# Generate coverage report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

## Success Criteria

- ✅ All projects build successfully
- ✅ All tests pass (excluding validly skipped tests)
- ✅ Test coverage >80% for GPU backends
- ✅ Test coverage >80% for pipeline infrastructure
- ✅ Test coverage >80% for source generators
- ✅ No high/critical security vulnerabilities
- ✅ Clean static analysis results