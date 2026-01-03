# Phase 2: Enterprise (v0.7.0)

**Duration**: Months 4-6 | **Target**: August 2026

---

## Sprint Breakdown

### Sprint 7-8 (Weeks 1-4): Ports & Adapters

| Task | Owner | Tests Required |
|------|-------|----------------|
| **A2.1** Define compilation port interface | Core | Contract tests |
| **A2.2** Define memory management port | Core | Contract tests |
| **A2.3** Implement CUDA adapters | Backend | Unit + integration |
| **A2.4** Implement Metal adapters | Backend | Unit + hardware |
| **A2.5** Create API surface project | Core | API analyzer |
| **A2.6** Add PublicAPI.*.txt tracking | Core | CI validation |

**Exit Criteria**: All backends use adapter pattern

### Sprint 9-10 (Weeks 5-8): Security & Metal

| Task | Owner | Tests Required |
|------|-------|----------------|
| **B2.1** Metal: Threadgroup memory | Backend | Hardware tests |
| **B2.2** Metal: Atomic operations | Backend | Hardware tests |
| **B2.3** Metal: Complete translation (100%) | Backend | Full kernel suite |
| **B2.4** OpenCL: NVIDIA vendor testing | Backend | Vendor-specific |
| **B2.5** OpenCL: AMD vendor testing | Backend | Vendor-specific |
| **C2.1** IDeviceAccessControl interface | Enterprise | Unit tests |
| **C2.2** Policy-based authorization | Enterprise | Integration |
| **C2.3** Audit logging infrastructure | Enterprise | Integration |

**Exit Criteria**: Metal production-ready, security controls operational

### Sprint 11-12 (Weeks 9-12): Messaging & Quotas

| Task | Owner | Tests Required |
|------|-------|----------------|
| **C2.4** Resource quota manager | Enterprise | Unit + load |
| **C2.5** Priority scheduler | Enterprise | Unit + integration |
| **C2.6** Graceful degradation | Enterprise | Chaos tests |
| **D2.1** P2P message queue | DX | Hardware (multi-GPU) |
| **D2.2** NCCL integration | DX | Hardware (NVLink) |
| **D2.3** Auto-tuner implementation | DX | Performance |
| **D2.4** ML.NET integration sample | DX | Integration |

**Exit Criteria**: Multi-GPU messaging, resource governance

---

## Testing Checkpoints

### Week 4 Checkpoint
```
□ Adapter pattern: All backends converted
□ API surface: Tracked in PublicAPI.txt
□ Breaking changes: Documented
□ Architecture tests: 100% pass
```

### Week 8 Checkpoint
```
□ Metal: 100% C# translation
□ Metal: Performance within 10% of MSL direct
□ OpenCL: NVIDIA + AMD validated
□ Security: Auth + audit functional
```

### Week 12 Checkpoint (Release Gate)
```
□ Unit coverage: ≥96%
□ Security penetration test: Passed
□ Multi-GPU: P2P messaging validated
□ Performance: <3% regression
□ Documentation: Security guide complete
```

---

## Key Implementation Details

### Ports & Adapters
```csharp
// Port definition (Core layer)
public interface IKernelCompilationPort
{
    ValueTask<CompiledKernel> CompileAsync(
        KernelSource source,
        CompilationOptions options,
        CancellationToken ct = default);
}

// Adapter (Backend layer)
public sealed class CudaKernelCompilationAdapter : IKernelCompilationPort
{
    public async ValueTask<CompiledKernel> CompileAsync(...)
    {
        var nvrtcOptions = MapOptions(options);
        return await _nvrtc.CompileAsync(source, nvrtcOptions, ct);
    }
}
```

### Authorization Model
```csharp
[Authorize(Policy = "GpuExecute")]
public async Task<Result> ExecuteKernelAsync(string kernelName)
{
    _audit.Log(AuditEvent.KernelExecution, kernelName);
    return await _orchestrator.ExecuteAsync(kernelName);
}
```

### P2P Messaging
```csharp
[RingKernel(MessagingStrategy = MessagingStrategy.P2P)]
public static void DistributedKernel(
    P2PMessageQueue<Message> input,
    P2PMessageQueue<Message> output)
{
    // Direct GPU-GPU transfer via NVLink
}
```

---

## Vendor Testing Matrix

| Vendor | Device | OpenCL Version | Status |
|--------|--------|----------------|--------|
| NVIDIA | RTX 3000/4000 | 3.0 | Required |
| AMD | RX 6000/7000 | 2.1 | Required |
| Intel | Arc A-series | 3.0 | Best effort |
| Intel | Iris Xe | 2.1 | Best effort |

---

## Deliverables Checklist

- [ ] Backend adapters for all ports
- [ ] API surface tracking enabled
- [ ] Metal C# translation at 100%
- [ ] OpenCL vendor validation (NVIDIA, AMD)
- [ ] Authentication & authorization
- [ ] Audit logging
- [ ] Resource quota management
- [ ] Priority scheduling
- [ ] Graceful degradation to CPU
- [ ] P2P GPU messaging
- [ ] Auto-tuner
- [ ] ML.NET integration sample
