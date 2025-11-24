# Metal Backend Architecture

This document provides detailed technical architecture documentation for the DotCompute Metal backend implementation.

## Native Layer Architecture

### File Structure

```
src/Backends/DotCompute.Backends.Metal/
├── native/
│   ├── include/
│   │   ├── DCMetalInterop.h     # Core type definitions
│   │   └── DCMetalMPS.h         # MPS function declarations
│   ├── src/
│   │   ├── DCMetalDevice.mm     # Device, buffers, kernels, command queues
│   │   └── DCMetalMPS.mm        # Metal Performance Shaders integration
│   ├── CMakeLists.txt           # Build configuration
│   └── build.sh                 # Build script
├── MPS/
│   ├── MetalMPSNative.cs        # P/Invoke declarations
│   ├── MetalMPSOrchestrator.cs  # Operation routing
│   └── MetalPerformanceShadersBackend.cs
├── Memory/
│   ├── MetalMemoryPoolManager.cs
│   └── MetalUnifiedBuffer.cs
├── MetalAccelerator.cs
├── MetalKernelCompiler.cs
└── MetalCommandQueuePool.cs
```

### Native Interop Design

The native layer uses `extern "C"` functions for P/Invoke compatibility:

```cpp
// DCMetalInterop.h
typedef void* DCMetalDevice;
typedef void* DCMetalBuffer;
typedef void* DCMetalLibrary;
typedef void* DCMetalKernel;
typedef void* DCMetalCommandQueue;

// Function naming convention: DCMetal_<Category>_<Action>
DCMetalDevice DCMetal_CreateDevice(void);
DCMetalBuffer DCMetal_CreateBuffer(DCMetalDevice device, size_t size, uint32_t options);
bool DCMetal_ExecuteKernel(DCMetalDevice device, DCMetalKernel kernel, ...);
```

## Command Queue Pool

### Problem

Metal's `MTLCommandQueue` is not thread-safe for concurrent command buffer creation. Using a single queue from multiple threads causes:
- Race conditions
- SIGSEGV crashes
- Undefined behavior

### Solution

Thread-safe command queue pool with per-device queues:

```cpp
// DCMetalDevice.mm
static std::mutex s_commandQueueMutex;
static std::map<void*, id<MTLCommandQueue>> s_sharedCommandQueues;

id<MTLCommandQueue> getSharedCommandQueue(id<MTLDevice> device) {
    std::lock_guard<std::mutex> lock(s_commandQueueMutex);
    void* key = (__bridge void*)device;
    auto it = s_sharedCommandQueues.find(key);
    if (it != s_sharedCommandQueues.end()) {
        return it->second;
    }
    id<MTLCommandQueue> queue = [device newCommandQueue];
    s_sharedCommandQueues[key] = queue;
    return queue;
}
```

### Cleanup

Explicit cleanup prevents ARC conflicts at exit:

```cpp
void DCMetal_CleanupCommandQueues(void) {
    @autoreleasepool {
        std::lock_guard<std::mutex> lock(s_commandQueueMutex);
        s_sharedCommandQueues.clear();
    }
}
```

## MPS Integration Architecture

### Thread-Safe MPS Command Queues

MPS operations have their own command queue pool:

```cpp
// DCMetalMPS.mm
static std::mutex s_mpsCommandQueueMutex;
static std::map<void*, id<MTLCommandQueue>> s_mpsCommandQueues;

static id<MTLCommandQueue> getMPSCommandQueue(id<MTLDevice> device) {
    std::lock_guard<std::mutex> lock(s_mpsCommandQueueMutex);
    void* key = (__bridge void*)device;
    auto it = s_mpsCommandQueues.find(key);
    if (it != s_mpsCommandQueues.end()) {
        return it->second;
    }
    id<MTLCommandQueue> queue = [device newCommandQueue];
    if (queue) {
        s_mpsCommandQueues[key] = queue;
    }
    return queue;
}
```

### Static Data Safety

Avoid `std::string` in static scope to prevent exit-time destructor issues:

```cpp
// BAD: std::string destructor conflicts with Objective-C runtime shutdown
static std::string familyStr; // Causes SIGSEGV on exit

// GOOD: Plain char array has no destructor
static char s_gpuFamilyBuffer[64] = "Unknown";
```

### MPS Operation Flow

```
┌─────────────────────┐
│   C# Application    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  MPSOrchestrator    │──► ShouldUseMPS() decision
└──────────┬──────────┘
           │
     ┌─────┴─────┐
     │           │
     ▼           ▼
┌─────────┐ ┌─────────┐
│  MPS    │ │ Custom  │
│ Backend │ │ Kernel  │
└────┬────┘ └────┬────┘
     │           │
     ▼           ▼
┌─────────────────────┐
│  Native P/Invoke    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  libDotComputeMetal │
│  (Objective-C++)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  Metal Framework    │
│  + MPS Framework    │
└─────────────────────┘
```

## Memory Architecture

### Unified Memory Model

Apple Silicon uses unified memory accessible by both CPU and GPU:

```cpp
// Storage mode selection
MTLResourceOptions options;
if (device.hasUnifiedMemory) {
    // Shared storage - direct CPU/GPU access
    options = MTLResourceStorageModeShared;
} else {
    // Private storage - GPU only, requires blit for CPU access
    options = MTLResourceStorageModePrivate;
}
```

### Memory Pool Design

```csharp
public class MetalMemoryPoolManager
{
    // Bucketed pools by size (powers of 2)
    private readonly ConcurrentDictionary<int, ConcurrentBag<MetalBuffer>> _pools;

    // Configuration
    private readonly int _maxPerBucket = 16;
    private readonly long _maxTotalBytes = 1024 * 1024 * 1024; // 1GB

    public MetalBuffer Rent(int size)
    {
        int bucket = GetBucket(size);
        if (_pools.TryGetValue(bucket, out var pool) && pool.TryTake(out var buffer))
        {
            return buffer; // 90%+ hit rate
        }
        return AllocateNew(size);
    }

    public void Return(MetalBuffer buffer)
    {
        int bucket = GetBucket(buffer.Size);
        if (_pools[bucket].Count < _maxPerBucket)
        {
            _pools[bucket].Add(buffer);
        }
        else
        {
            buffer.Dispose(); // Pool full, release
        }
    }
}
```

## Kernel Compilation Architecture

### Binary Caching

```cpp
bool DCMetal_CompileLibraryWithCache(
    DCMetalDevice device,
    const char* source,
    DCMetalLibrary* outLibrary,
    const char* cacheKey)
{
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;

        // Check binary cache first
        auto it = s_binaryArchives.find(cacheKey);
        if (it != s_binaryArchives.end()) {
            // Load from cached binary archive (~5μs)
            NSError* error = nil;
            id<MTLLibrary> library = [mtlDevice newLibraryWithData:it->second
                                                            error:&error];
            if (library) {
                *outLibrary = (__bridge_retained void*)library;
                return true;
            }
        }

        // Compile from source (~200ms)
        NSError* error = nil;
        id<MTLLibrary> library = [mtlDevice newLibraryWithSource:@(source)
                                                         options:nil
                                                           error:&error];
        if (library) {
            // Create and populate binary archive for future use
            MTLBinaryArchiveDescriptor* archiveDesc = [[MTLBinaryArchiveDescriptor alloc] init];
            id<MTLBinaryArchive> archive = [mtlDevice newBinaryArchiveWithDescriptor:archiveDesc
                                                                               error:&error];
            // ... populate archive with compiled functions ...
            s_binaryArchives[cacheKey] = archive;

            *outLibrary = (__bridge_retained void*)library;
            return true;
        }
        return false;
    }
}
```

### Compilation Pipeline

```
Source Code (MSL)
       │
       ▼
┌─────────────────┐
│  Hash Source    │──► Cache Key
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
[Cache Hit]  [Cache Miss]
    │              │
    ▼              ▼
Load Binary   Compile Source
Archive       (~200ms)
(~5μs)             │
    │              ▼
    │         Create Binary
    │         Archive
    │              │
    └──────┬───────┘
           │
           ▼
    MTLLibrary Ready
           │
           ▼
    Extract Functions
    (MTLFunction)
           │
           ▼
    Create Pipeline State
    (MTLComputePipelineState)
```

## Error Handling

### Native Error Propagation

```cpp
typedef struct {
    bool success;
    int errorCode;
    char errorMessage[256];
} DCMetalResult;

DCMetalResult DCMetal_ExecuteKernel(...) {
    DCMetalResult result = {true, 0, ""};

    @try {
        // ... execution code ...
    }
    @catch (NSException* exception) {
        result.success = false;
        result.errorCode = -1;
        strncpy(result.errorMessage,
                [[exception reason] UTF8String],
                sizeof(result.errorMessage) - 1);
    }

    return result;
}
```

### C# Exception Handling

```csharp
public void Execute(...)
{
    var result = MetalNative.ExecuteKernel(...);
    if (!result.success)
    {
        throw new MetalExecutionException(
            result.errorCode,
            Marshal.PtrToStringUTF8(result.errorMessage));
    }
}
```

## Build System

### Native Library Build

```bash
#!/bin/bash
# build.sh

# Configure CMake
cmake -B build \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET=10.13

# Build
cmake --build build --config Release

# Install to parent directory
cp build/libDotComputeMetal.dylib ../
```

### CMake Configuration

```cmake
# CMakeLists.txt
cmake_minimum_required(VERSION 3.20)
project(DotComputeMetal VERSION 1.0.0 LANGUAGES CXX OBJCXX)

find_library(METAL_FRAMEWORK Metal REQUIRED)
find_library(MPS_FRAMEWORK MetalPerformanceShaders REQUIRED)
find_library(FOUNDATION_FRAMEWORK Foundation REQUIRED)

add_library(DotComputeMetal SHARED
    src/DCMetalDevice.mm
    src/DCMetalMPS.mm
)

target_link_libraries(DotComputeMetal
    ${METAL_FRAMEWORK}
    ${MPS_FRAMEWORK}
    ${FOUNDATION_FRAMEWORK}
)

set_target_properties(DotComputeMetal PROPERTIES
    CXX_STANDARD 17
    OBJCXX_STANDARD 17
)
```

## Testing Architecture

### Unit Test Categories

```csharp
[Category("Metal")]
public class MetalAcceleratorTests
{
    [SkippableFact]
    public void CreateAccelerator_ReturnsValidDevice()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
        using var accelerator = MetalAccelerator.Create();
        Assert.NotNull(accelerator);
    }
}

[Category("Metal-MPS")]
public class MPSBackendTests
{
    [SkippableFact]
    public void MatrixMultiply_ProducesCorrectResult()
    {
        // ...
    }
}
```

### Performance Validation

```csharp
public class SimplePerformanceValidation
{
    public async Task<ValidationResult> RunAll()
    {
        var results = new List<ValidationResult>();

        // Claim #1: Unified Memory (2-3x speedup)
        results.Add(await ValidateUnifiedMemory());

        // Claim #2: MPS Performance (3-4x for large matrices)
        results.Add(await ValidateMPSPerformance());

        // Claim #4: Cold Start (<10ms)
        results.Add(await ValidateColdStart());

        // Claim #5: Kernel Cache (<1ms hits)
        results.Add(await ValidateKernelCache());

        // Claim #6: Command Buffer (<100μs)
        results.Add(await ValidateCommandBuffer());

        // Claim #7: Parallel Execution (>1.5x speedup)
        results.Add(await ValidateParallelExecution());

        return results;
    }
}
```

## Debugging

### Enable Native Logging

```cpp
#define METAL_DEBUG 1

#if METAL_DEBUG
    #define METAL_LOG(fmt, ...) NSLog(@"[METAL-DEBUG] " fmt, ##__VA_ARGS__)
#else
    #define METAL_LOG(fmt, ...)
#endif

// Usage in code
METAL_LOG("Setting pipeline state: %p", pipelineState);
METAL_LOG("Dispatching: grid=(%lu,%lu,%lu)", gridX, gridY, gridZ);
```

### GPU Validation

```bash
# Enable Metal validation layer
export METAL_DEVICE_WRAPPER_TYPE=1
export METAL_DEBUG_ERROR_MODE=0
./run-tests.sh
```

## Version Compatibility

| macOS Version | Metal Family | Features |
|---------------|--------------|----------|
| 10.13+ | MTLGPUFamilyMac1 | Basic compute |
| 10.15+ | MTLGPUFamilyMac2 | Binary archives |
| 11.0+ | MTLGPUFamilyApple7 | Full Apple Silicon |
| 12.0+ | MTLGPUFamilyApple8 | M2+ features |
| 14.0+ | MTLGPUFamilyApple9 | M3+ features |

## See Also

- [Metal Backend Guide](../guides/metal-backend.md)
- [Apple Metal Best Practices](https://developer.apple.com/documentation/metal/resource_fundamentals/choosing_a_resource_storage_mode_for_apple_gpus)
- [Metal Performance Shaders](https://developer.apple.com/documentation/metalperformanceshaders)
