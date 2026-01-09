# Phase 1: Foundation (v0.6.0)

**Duration**: Months 1-3 | **Target**: May 2026

---

## Sprint Breakdown

### Sprint 1-2 (Weeks 1-4): Architecture Foundation

| Task | Owner | Tests Required |
|------|-------|----------------|
| **A1.1** Define core ports interfaces | Core | Unit tests for contracts |
| **A1.2** Create architecture test project | Core | Self-validating |
| **A1.3** Refactor CudaDevice.cs (29K→5K lines) | Core | Regression tests |
| **A1.4** Extract buffer base abstractions | Core | Unit + integration |
| **A1.5** Setup NetArchTest rules | Core | Architecture validation |

**Exit Criteria**: Architecture tests pass, 25 god files eliminated

### Sprint 3-4 (Weeks 5-8): Backend & LINQ

| Task | Owner | Tests Required |
|------|-------|----------------|
| **B1.1** Metal: Math intrinsics translation | Backend | Unit + hardware |
| **B1.2** Metal: Struct support in translator | Backend | Unit + hardware |
| **B1.3** OpenCL timing provider | Backend | Unit + hardware |
| **B1.4** LINQ Join operation | DX | Unit + integration |
| **B1.5** LINQ GroupBy operation | DX | Unit + integration |
| **B1.6** LINQ OrderBy operation | DX | Unit + integration |

**Exit Criteria**: Metal at 85%, LINQ 54/54 tests passing

### Sprint 5-6 (Weeks 9-12): Enterprise & Integration

| Task | Owner | Tests Required |
|------|-------|----------------|
| **C1.1** Circuit breaker implementation | Enterprise | Unit + chaos |
| **C1.2** OpenTelemetry tracing | Enterprise | Integration |
| **C1.3** Prometheus metrics | Enterprise | Integration |
| **C1.4** Health check endpoints | Enterprise | Integration |
| **D1.1** CLI tool scaffold | DX | E2E tests |
| **D1.2** Orleans grain integration | DX | Integration |

**Exit Criteria**: Observability operational, Orleans demo working

---

## Testing Checkpoints

### Week 4 Checkpoint
```
□ Architecture tests: 100% pass
□ God files: 294 remaining (25 eliminated)
□ Buffer tests: All existing tests pass
□ No performance regression
```

### Week 8 Checkpoint
```
□ Metal translation: 85% complete
□ LINQ tests: 54/54 passing
□ OpenCL timing: Functional on NVIDIA
□ Hardware test suite: 100% pass
```

### Week 12 Checkpoint (Release Gate)
```
□ Unit coverage: ≥95%
□ Integration tests: 100% pass
□ Performance: <5% regression
□ Documentation: Updated for new features
□ Security: No new vulnerabilities
```

---

## Key Implementation Details

### God File Refactoring Pattern
```
1. Identify file with >1000 lines, 8+ types
2. Create Types/ subdirectory
3. Extract enums → [Feature]Enums.cs
4. Extract classes → [Feature]Types.cs
5. Add global using for backward compat
6. Update tests
7. Commit with detailed message
```

### LINQ GPU Implementation
```csharp
// Join kernel generation target
[Kernel]
public static void HashJoin<TLeft, TRight, TKey>(
    ReadOnlySpan<TLeft> left,
    ReadOnlySpan<TRight> right,
    Span<JoinResult<TLeft, TRight>> output,
    Func<TLeft, TKey> leftKeySelector,
    Func<TRight, TKey> rightKeySelector)
{
    // Hash-based GPU join implementation
}
```

### Circuit Breaker Pattern
```csharp
services.AddDotCompute(b => b
    .WithResilience(r => r
        .EnableCircuitBreaker(opts => {
            opts.FailureThreshold = 5;
            opts.RecoveryTimeout = TimeSpan.FromSeconds(30);
        })));
```

---

## Risk Mitigation

| Risk | Mitigation | Owner |
|------|------------|-------|
| God file refactoring breaks tests | Run full test suite after each file | Core |
| Metal translation complexity | Prioritize common patterns first | Backend |
| LINQ performance issues | Benchmark against CPU baseline | DX |

---

## Deliverables Checklist

- [ ] 50 god files eliminated (319→269)
- [ ] BufferBase<T> hierarchy established
- [ ] Architecture test suite (20+ rules)
- [ ] Metal C# translation at 85%
- [ ] OpenCL timing provider complete
- [ ] LINQ Join/GroupBy/OrderBy working
- [ ] Circuit breaker operational
- [ ] Distributed tracing enabled
- [ ] CLI tool (device list, kernel compile)
- [ ] Orleans integration sample
