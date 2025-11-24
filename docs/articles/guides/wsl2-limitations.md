# WSL2 CUDA Limitations and Mitigations

This document describes the known limitations when running DotCompute CUDA Ring Kernels under WSL2 (Windows Subsystem for Linux 2) and the mitigations implemented in the codebase.

## Overview

WSL2 provides GPU pass-through via NVIDIA's CUDA on WSL driver, but there are several limitations compared to native Linux or Windows CUDA:

1. **Pinned Memory Allocation Failures**
2. **Driver API vs Runtime API Context Management**
3. **Unified Memory Restrictions**
4. **Cooperative Kernel Limitations**

## Detailed Limitations and Mitigations

### 1. Pinned Memory Allocation (`cudaHostAlloc`)

**Limitation:** In WSL2, `cudaHostAlloc` with pinned/mapped flags often fails with `cudaErrorInvalidValue`. This prevents the use of zero-copy memory access patterns.

**Impact:**
- Cannot use pinned host memory for direct GPU access
- `cudaMemcpyAsync` requires pinned host memory and fails with regular malloc buffers

**Mitigation (implemented in `RingKernelControlBlockHelper.cs`):**
```csharp
// Try pinned allocation first
var stagingResult = CudaRuntime.cudaHostAlloc(ref stagingBuffer, size, pinnedFlags);
if (stagingResult != CudaError.Success)
{
    // Fall back to regular malloc - will use sync copies
    stagingBuffer = Marshal.AllocHGlobal(controlBlockSize);
    isStagingPinned = false;
}
```

When pinned allocation fails:
- Use regular `Marshal.AllocHGlobal` for staging buffers
- Track allocation type with `IsStagingPinned` flag
- Use synchronous `cudaMemcpy` instead of async `cudaMemcpyAsync`

### 2. Driver API vs Runtime API Context Management

**Limitation:** WSL2 has issues with CUDA Driver API context handles becoming stale or invalid after Runtime API calls. Mixing Driver API (`cuMemAlloc`, `cuMemcpyHtoD`) with Runtime API (`cudaMalloc`, `cudaMemcpy`) causes context corruption.

**Impact:**
- Driver API allocated memory cannot be accessed via Runtime API
- Context handles become invalid after `cudaSetDevice()` calls
- Kernel launches fail with `CUDA_ERROR_INVALID_HANDLE` (400)

**Mitigation (implemented in `RingKernelControlBlockHelper.cs` and `CudaRingKernelRuntime.cs`):**

1. **Consistent API Usage:** When using async control blocks in WSL2, use Runtime API exclusively for memory operations:
```csharp
// Use Runtime API for device memory allocation
var deviceAllocResult = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)controlBlockSize);

// Use Runtime API for memory copies
var copyResult = CudaRuntime.cudaMemcpy(
    devicePtr, hostPtr, (nuint)size, CudaMemcpyKind.HostToDevice);
```

2. **Context Restoration:** After Runtime API calls, restore Driver API context for kernel operations:
```csharp
// After Runtime API operations, restore Driver API context
var ctxRestoreResult = CudaRuntimeCore.cuCtxSetCurrent(state.Context);
```

### 3. Unified Memory Restrictions

**Limitation:** WSL2 does not support concurrent CPU/GPU access to unified memory (`cudaMallocManaged`). Attempting to use unified memory with concurrent access flags fails or causes undefined behavior.

**Impact:**
- Cannot use `cudaMallocManaged` with `cudaMemAttachGlobal`
- Zero-copy patterns using unified memory don't work
- Cooperative kernels with host-side control block access are problematic

**Mitigation (implemented in `RingKernelControlBlockHelper.cs`):**
```csharp
// WSL2 detection and fallback
if (pinnedAllocResult != CudaError.Success)
{
    Console.WriteLine("[DIAG] WSL2 detected - skipping unified memory");
    // Fall back to device-only memory with async staging
}
```

The async control block pattern provides an alternative:
- Device memory for the control block
- Separate staging buffer on host
- Explicit async/sync copies between them

### 4. Cooperative Kernel Limitations

**Limitation:** Cooperative kernel launch (`cuLaunchCooperativeKernel`) may not work reliably in WSL2 due to context management issues and memory restrictions.

**Impact:**
- Grid-wide synchronization may fail
- Cooperative groups functionality may be limited

**Mitigation (implemented in `CudaRingKernelRuntime.cs`):**
```csharp
// WSL2 compatibility mode: Use non-cooperative kernel
if (isWsl2Environment)
{
    Console.WriteLine("[DIAG] Using NON-COOPERATIVE kernel (WSL2 compatibility mode)");
    launchResult = CudaApi.cuLaunchKernel(
        state.Function, gridSize, 1, 1, blockSize, 1, 1,
        0, state.Stream, argPtrsHandle.AddrOfPinnedObject(), IntPtr.Zero);
}
```

## Environment Setup

### Required Configuration

Set the library path for WSL2 CUDA drivers:
```bash
export LD_LIBRARY_PATH="/usr/lib/wsl/lib:$LD_LIBRARY_PATH"
```

Or use the provided test script which handles this automatically:
```bash
./scripts/run-tests.sh tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj
```

### Diagnostic Output

The codebase includes extensive diagnostic logging with `[DIAG]` prefix to help identify WSL2-specific issues:
- `[DIAG] WSL2 detected` - WSL2 fallback path activated
- `[DIAG] Pinned alloc: InvalidValue` - Pinned memory failed, using fallback
- `[DIAG] Using NON-COOPERATIVE kernel` - WSL2 compatibility kernel launch

## Performance Implications

The WSL2 mitigations have the following performance characteristics:

| Feature | Native Linux | WSL2 |
|---------|-------------|------|
| Pinned Memory | Zero-copy access | Sync copies required |
| Async Transfers | Full async support | Sync fallback |
| Control Block Reads | Non-blocking | Blocking sync copy |
| Cooperative Kernels | Full support | Non-cooperative fallback |

**Estimated overhead:** 10-30% slower for control block operations compared to native Linux with pinned memory.

## Affected Files

The following files contain WSL2-specific mitigations:

1. **`src/Backends/DotCompute.Backends.CUDA/RingKernels/RingKernelControlBlockHelper.cs`**
   - Async control block allocation with pinned/malloc fallback
   - Sync copy fallback for non-pinned staging
   - Runtime API memory operations (`cudaMalloc`, `cudaMemcpy`)
   - `Read` method uses Runtime API for device-to-host copies
   - `Write` method uses Runtime API for host-to-device copies
   - Proper resource cleanup for both allocation types

2. **`src/Backends/DotCompute.Backends.CUDA/RingKernels/CudaRingKernelRuntime.cs`**
   - Driver API context restoration after Runtime API calls
   - Non-cooperative kernel launch fallback
   - WSL2 detection and compatibility mode
   - `ReadControlBlockAsync` uses AsyncControlBlock path when available
   - `GetStatusAsync` uses non-blocking read with cached values

## Testing

Run the WSL2-compatible tests:
```bash
# Single test
export LD_LIBRARY_PATH="/usr/lib/wsl/lib:$LD_LIBRARY_PATH"
dotnet test --filter "PageRankContributionSender_ShouldActivateDeactivate"

# All Ring Kernel tests
./scripts/run-tests.sh --filter "RingKernel"
```

## Known Issues

1. **Telemetry Buffer Allocation:** The `CudaTelemetryBuffer` also uses pinned memory allocation which fails in WSL2. Tests that use telemetry features (e.g., `E2E_Telemetry_EnableAndVerifyAllocation`) will fail until similar fallback logic is added.

2. **Test Isolation:** Running multiple E2E tests sequentially can lead to CUDA context corruption. Running tests in isolation is recommended for troubleshooting.

3. **Error 700 (Illegal Address):** This error can occur if Driver API and Runtime API memory are mixed incorrectly. The system now consistently uses Runtime API for async control block operations.

## Future Improvements

1. Consider implementing a full Runtime API path for kernel loading
2. Add automatic WSL2 detection based on environment
3. Implement hybrid mode that switches between pinned and sync paths based on operation success
4. Add performance metrics to compare WSL2 vs native performance

## References

- [CUDA on WSL Documentation](https://docs.nvidia.com/cuda/wsl-user-guide/)
- [DotCompute WSL2 Setup Guide](./wsl2-setup.md)
- [Ring Kernel Migration Guide](./unified-ring-kernel-migration-guide.md)
