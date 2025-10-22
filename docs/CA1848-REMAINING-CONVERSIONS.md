# CA1848 Remaining Conversions - Complete Instructions

## Summary of Work Completed
- ✅ **CudaPersistentKernelManager.cs** - Event IDs 6600-6605 (6 delegates) - FULLY CONVERTED

## Files Requiring Conversion (10 files, ~78 delegates)

### File 2: NvmlWrapper.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/Monitoring/NvmlWrapper.cs`
**Event IDs**: 6620-6621
**Changes Required**:

1. Change class declaration to `partial`:
```csharp
public sealed partial class NvmlWrapper : IDisposable
```

2. Add LoggerMessage delegates after class declaration:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6620,
    Level = LogLevel.Information,
    Message = "NVML initialized successfully")]
private static partial void LogNvmlInitialized(ILogger logger);

[LoggerMessage(
    EventId = 6621,
    Level = LogLevel.Error,
    Message = "NVML initialization failed: {Error}")]
private static partial void LogNvmlInitFailed(ILogger logger, string error);

#endregion
```

3. Replace logging calls:
```csharp
// Line ~XX: Replace
_logger.LogErrorMessage($"NVML initialization failed: {result}");
// With:
LogNvmlInitFailed(_logger, result.ToString());

// Line ~XX: Replace
_logger.LogInfoMessage("NVML initialized successfully");
// With:
LogNvmlInitialized(_logger);
```

---

### File 3: CuptiWrapper.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/Monitoring/CuptiWrapper.cs`
**Event IDs**: 6640-6641
**Changes Required**:

1. Change class declaration to `partial`:
```csharp
public sealed partial class CuptiWrapper : IDisposable
```

2. Add LoggerMessage delegates:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6640,
    Level = LogLevel.Information,
    Message = "CUPTI initialized successfully")]
private static partial void LogCuptiInitialized(ILogger logger);

[LoggerMessage(
    EventId = 6641,
    Level = LogLevel.Error,
    Message = "CUPTI initialization failed: {Error}")]
private static partial void LogCuptiInitFailed(ILogger logger, string error);

#endregion
```

3. Replace logging calls:
```csharp
// Replace
_logger.LogErrorMessage($"CUPTI initialization failed: {result}");
// With:
LogCuptiInitFailed(_logger, result.ToString());

// Replace
_logger.LogInfoMessage("CUPTI initialized successfully");
// With:
LogCuptiInitialized(_logger);
```

---

### File 4: CudaPinnedMemoryAllocator.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/Memory/CudaPinnedMemoryAllocator.cs`
**Event IDs**: 6660-6661
**Changes Required**:

1. Change class to `partial`:
```csharp
public sealed partial class CudaPinnedMemoryAllocator : IDisposable
```

2. Add LoggerMessage delegates:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6660,
    Level = LogLevel.Debug,
    Message = "Pinned memory allocator initialized with pool size {PoolSize}")]
private static partial void LogAllocatorInitialized(ILogger logger, int poolSize);

[LoggerMessage(
    EventId = 6661,
    Level = LogLevel.Debug,
    Message = "Pinned memory cleanup completed, released {Count} allocations")]
private static partial void LogCleanupCompleted(ILogger logger, int count);

#endregion
```

3. Replace logging calls (search for `_logger.LogDebugMessage` and `_logger.LogInfoMessage`)

---

### File 5: CudaMemoryManager.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/Memory/CudaMemoryManager.cs`
**Event IDs**: 6680-6681
**Changes Required**:

1. Change class to `partial`:
```csharp
public sealed partial class CudaMemoryManager : IDisposable
```

2. Add LoggerMessage delegates:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6680,
    Level = LogLevel.Debug,
    Message = "Memory manager initialized for device {DeviceId}")]
private static partial void LogManagerInitialized(ILogger logger, int deviceId);

[LoggerMessage(
    EventId = 6681,
    Level = LogLevel.Information,
    Message = "Memory cleanup completed: {FreedBytes} bytes freed")]
private static partial void LogCleanupResult(ILogger logger, long freedBytes);

#endregion
```

3. Replace logging calls throughout the file

---

### File 6: CudaIntegrationOrchestrator.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/Integration/CudaIntegrationOrchestrator.cs`
**Event IDs**: 6700-6701
**Changes Required**:

1. Change class to `partial`:
```csharp
public sealed partial class CudaIntegrationOrchestrator : IDisposable
```

2. Add LoggerMessage delegates:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6700,
    Level = LogLevel.Information,
    Message = "CUDA integration orchestrator initialized")]
private static partial void LogOrchestratorInitialized(ILogger logger);

[LoggerMessage(
    EventId = 6701,
    Level = LogLevel.Information,
    Message = "CUDA integration orchestrator disposed")]
private static partial void LogOrchestratorDisposed(ILogger logger);

#endregion
```

3. Replace logging calls

---

### File 7: CudaContextManager.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/Integration/CudaContextManager.cs`
**Event IDs**: 6720-6721
**Changes Required**:

1. Change class to `partial`:
```csharp
public sealed partial class CudaContextManager : IDisposable
```

2. Add LoggerMessage delegates:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6720,
    Level = LogLevel.Debug,
    Message = "Context manager initialized for device {DeviceId}")]
private static partial void LogContextManagerInitialized(ILogger logger, int deviceId);

[LoggerMessage(
    EventId = 6721,
    Level = LogLevel.Debug,
    Message = "Created CUDA context for device {DeviceId}")]
private static partial void LogContextCreated(ILogger logger, int deviceId);

#endregion
```

3. Replace logging calls

---

### File 8: CudaBackendIntegration.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/Integration/CudaBackendIntegration.cs`
**Event IDs**: 6740-6749
**Changes Required**:

1. Change class to `partial` (it's already `sealed partial` according to the file read)

2. Add LoggerMessage delegates:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6740,
    Level = LogLevel.Information,
    Message = "CUDA backend integration initialized for device {DeviceId}")]
private static partial void LogBackendInitialized(ILogger logger, int deviceId);

[LoggerMessage(
    EventId = 6741,
    Level = LogLevel.Information,
    Message = "Optimization completed with {Count} optimizations applied")]
private static partial void LogOptimizationCompleted(ILogger logger, int count);

[LoggerMessage(
    EventId = 6742,
    Level = LogLevel.Warning,
    Message = "Backend health degraded: {Health:F2}")]
private static partial void LogBackendHealthDegraded(ILogger logger, double health);

[LoggerMessage(
    EventId = 6743,
    Level = LogLevel.Debug,
    Message = "Backend health check: {Health:F2}")]
private static partial void LogBackendHealth(ILogger logger, double health);

[LoggerMessage(
    EventId = 6744,
    Level = LogLevel.Error,
    Message = "Health check failed")]
private static partial void LogHealthCheckError(ILogger logger, Exception ex);

[LoggerMessage(
    EventId = 6745,
    Level = LogLevel.Error,
    Message = "Memory cleanup failed")]
private static partial void LogMemoryCleanupError(ILogger logger, Exception ex);

[LoggerMessage(
    EventId = 6746,
    Level = LogLevel.Information,
    Message = "Maintenance completed")]
private static partial void LogMaintenanceCompleted(ILogger logger);

[LoggerMessage(
    EventId = 6747,
    Level = LogLevel.Information,
    Message = "Backend integration disposed")]
private static partial void LogBackendDisposed(ILogger logger);

#endregion
```

3. Replace logging calls:
- Line 97: `_logger.LogInformation("CUDA backend integration initialized for device {DeviceId}", context.DeviceId);` → `LogBackendInitialized(_logger, context.DeviceId);`
- Line 407: Replace optimization log
- Line 607, 611: Replace health check logs
- Line 622: Replace health check error log
- Line 639: Replace memory cleanup error log
- Line 643: Replace maintenance completed log
- Line 680: Replace disposed log

---

### File 9: CudaKernelExecutor.cs (Integration/Components)
**Location**: `src/Backends/DotCompute.Backends.CUDA/Integration/Components/CudaKernelExecutor.cs`
**Event IDs**: 6760-6773
**Changes Required**:

1. Class is already `partial`

2. Add LoggerMessage delegates:
```csharp
#region LoggerMessage Delegates

[LoggerMessage(
    EventId = 6760,
    Level = LogLevel.Debug,
    Message = "Kernel executor orchestrator initialized for device {DeviceId}")]
private static partial void LogOrchestratorInitialized(ILogger logger, int deviceId);

[LoggerMessage(
    EventId = 6761,
    Level = LogLevel.Debug,
    Message = "Executing kernel {KernelName} with {ArgCount} arguments")]
private static partial void LogExecutingKernel(ILogger logger, string kernelName, int argCount);

[LoggerMessage(
    EventId = 6762,
    Level = LogLevel.Debug,
    Message = "Kernel {KernelName} executed in {Duration}ms, success: {Success}")]
private static partial void LogKernelExecuted(ILogger logger, string kernelName, double duration, bool success);

[LoggerMessage(
    EventId = 6763,
    Level = LogLevel.Error,
    Message = "Kernel {KernelName} execution failed")]
private static partial void LogKernelExecutionFailed(ILogger logger, Exception ex, string kernelName);

[LoggerMessage(
    EventId = 6764,
    Level = LogLevel.Warning,
    Message = "Configuration optimization failed for kernel {KernelName}")]
private static partial void LogConfigurationOptimizationFailed(ILogger logger, Exception ex, string kernelName);

[LoggerMessage(
    EventId = 6765,
    Level = LogLevel.Debug,
    Message = "Using cached kernel {KernelName}")]
private static partial void LogUsingCachedKernel(ILogger logger, string kernelName);

[LoggerMessage(
    EventId = 6766,
    Level = LogLevel.Debug,
    Message = "Compiling kernel {KernelName}")]
private static partial void LogCompilingKernel(ILogger logger, string kernelName);

[LoggerMessage(
    EventId = 6767,
    Level = LogLevel.Information,
    Message = "Kernel {KernelName} compiled and cached")]
private static partial void LogKernelCompiledAndCached(ILogger logger, string kernelName);

[LoggerMessage(
    EventId = 6768,
    Level = LogLevel.Error,
    Message = "Kernel {KernelName} compilation failed")]
private static partial void LogKernelCompilationFailed(ILogger logger, Exception ex, string kernelName);

[LoggerMessage(
    EventId = 6769,
    Level = LogLevel.Information,
    Message = "Executing batch of {Count} kernels")]
private static partial void LogExecutingBatch(ILogger logger, int count);

[LoggerMessage(
    EventId = 6770,
    Level = LogLevel.Error,
    Message = "Batch kernel {Index} execution failed")]
private static partial void LogBatchKernelExecutionFailed(ILogger logger, Exception ex, int index);

[LoggerMessage(
    EventId = 6771,
    Level = LogLevel.Information,
    Message = "Batch execution completed: {SuccessCount}/{TotalCount} successful")]
private static partial void LogBatchExecutionCompleted(ILogger logger, int successCount, int totalCount);

[LoggerMessage(
    EventId = 6772,
    Level = LogLevel.Warning,
    Message = "Failed to dispose cached kernel")]
private static partial void LogCachedKernelDisposalFailed(ILogger logger, Exception ex);

[LoggerMessage(
    EventId = 6773,
    Level = LogLevel.Debug,
    Message = "Kernel executor orchestrator disposed")]
private static partial void LogOrchestratorDisposed(ILogger logger);

#endregion
```

3. Replace logging calls:
- Line 57: Context initialization
- Line 83: Executing kernel
- Line 101: Kernel executed
- Line 119: Kernel execution failed
- Line 148: Configuration optimization failed
- Line 173: Using cached kernel
- Line 179: Compiling kernel
- Line 192: Kernel compiled and cached
- Line 197: Kernel compilation failed
- Line 220: Executing batch
- Line 248: Batch kernel execution failed
- Line 261: Batch execution completed
- Line 390: Cached kernel disposal failed
- Line 398: Orchestrator disposed

---

### File 10: CudaDeviceManager.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/DeviceManagement/CudaDeviceManager.cs`
**Event IDs**: 6780-6799
**Changes Required**:

**NOTE**: This file already has one LoggerMessage delegate (LogDeviceInfo at line 26, EventId 6076). Need to convert all other logging calls.

Logging calls to convert (using LogXXXMessage extension methods from DotCompute.Backends.CUDA.Logging):
- Line 76: "Enumerating CUDA devices..."
- Line 85: "No CUDA devices found or CUDA not available: {result}"
- Line 89: "Found {deviceCount} CUDA device(s)"
- Line 106: "Error getting device info"
- Line 124: "Failed to enumerate CUDA devices"
- Line 209: "Detecting P2P capabilities between devices..."
- Line 233: $"P2P access available: Device {device1} -> Device {device2}"
- Line 239: "Failed to check P2P capability between device {From} and {To}"
- Line 275: "Set current device to {deviceId}"
- Line 352: "Enabled P2P access: Device {From} -> Device {fromDevice, toDevice}"
- Line 389: $"Failed to disable peer access from device {fromDevice} to {toDevice}: {result}"
- Line 446: "Failed to reset device {DeviceId}: {deviceId, result}"
- Line 451: "Error during device disposal"
- Line 482: $"Selected device {bestDevice.Device.DeviceId} ({bestDevice.Device.Name}) with score {bestDevice.Score}"

Add delegates for all these calls with Event IDs 6780-6799.

---

### File 11: CudaDevice.cs
**Location**: `src/Backends/DotCompute.Backends.CUDA/CudaDevice.cs`
**Event IDs**: 6800-6819
**Changes Required**:

**NOTE**: File already uses some LoggerMessage patterns. Need to convert remaining calls.

Logging calls to convert (using LogXXXMessage extension methods):
- Line 263: $"Device {deviceId} ManagedMemory field value: {_deviceProperties.ManagedMemory}, SupportsManagedMemory: {SupportsManagedMemory}"
- Line 265: $"Initialized CUDA 13.0-compatible device {_deviceId}: {Name} (CC {ComputeCapabilityMajor}.{ComputeCapabilityMinor}, {GetArchitectureGeneration()})"
- Line 600: $"Disposed CUDA device {_deviceId}"

Add delegates for these calls with Event IDs 6800-6802.

---

## Build Verification

After all conversions, run:
```bash
# Check for remaining CA1848 warnings
dotnet build DotCompute.sln --no-restore 2>&1 | grep -E "CA1848|XFIX003"

# Should show 0 warnings for these 11 files
```

## Summary
- **Total Files**: 11
- **Total Delegates**: ~84
- **Event ID Range**: 6600-6819
- **Completed**: 1 file (CudaPersistentKernelManager.cs)
- **Remaining**: 10 files
