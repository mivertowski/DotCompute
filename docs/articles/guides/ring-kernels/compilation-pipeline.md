# Ring Kernel Compilation Pipeline - Architecture Document

**Version**: 1.0
**Date**: January 2025
**Status**: Design Proposal
**Author**: DotCompute Team

## Executive Summary

This document outlines the architecture and implementation plan for a complete Ring Kernel compilation pipeline that transforms C# methods annotated with `[RingKernel]` into GPU-executable PTX code. The current implementation uses placeholder PTX and does not execute actual GPU kernels. This design addresses 5 critical gaps identified in GPU execution validation testing.

## Table of Contents

1. [Current State Analysis](#current-state-analysis)
2. [Problem Statement](#problem-statement)
3. [Design Goals](#design-goals)
4. [Architecture Overview](#architecture-overview)
5. [Component Design](#component-design)
6. [Implementation Phases](#implementation-phases)
7. [Testing Strategy](#testing-strategy)
8. [Performance Considerations](#performance-considerations)
9. [Risk Mitigation](#risk-mitigation)

---

## Current State Analysis

### Existing Infrastructure

**Source Generators and Analyzers**:
- `RingKernelAttributeAnalyzer.cs` - Validates `[RingKernel]` attribute usage
- `RingKernelCodeBuilder.cs` - Generates C# wrapper code for ring kernel methods
- Source generator creates host-side invocation infrastructure

**CUDA Backend**:
- `CudaRingKernelRuntime.cs` - Runtime execution engine
- `CudaRingKernelCompiler.cs` - Kernel compilation infrastructure
- `CudaMessageQueueBridgeFactory.cs` - Host ↔ Device message transfer
- `PTXCompiler.cs` - General PTX compilation using NVRTC

**Message Infrastructure**:
- `MessageQueueBridge.cs` - Bidirectional message transfer (recently enhanced with validation)
- `MemoryPackMessageSerializer.cs` - High-performance serialization (2-5x faster than JSON)
- `IRingKernelMessage` - Message interface with MessageId, MessageType, Timestamp

### Current Kernel Launch Flow

```csharp
// CudaRingKernelRuntime.cs:328-329 (PLACEHOLDER - NO ACTUAL EXECUTION)
// TODO: Replace placeholder with actual cuLaunchCooperativeKernel call
// For now, just log the kernel function pointer
_logger.LogDebug("Ring kernel compiled successfully: {KernelPtr:X16}", kernelPtr);
```

**Key Finding**: The kernel is compiled to placeholder PTX but **never actually launched on the GPU**.

### Placeholder PTX Generation

```csharp
// CudaRingKernelRuntime.cs:1013-1053
private string GeneratePlaceholderPTX(string kernelId, int inputQueueCapacity, int outputQueueCapacity)
{
    // Generates simple PTX that just logs execution
    // DOES NOT process messages from input queue
    // DOES NOT write results to output queue
    // Serves only as a compilation test
}
```

**Gap**: This PTX is not derived from the actual `[RingKernel]` C# method. User's kernel logic is completely ignored.

### Example Ring Kernel (Not Compiled)

```csharp
// VectorAddRingKernel.cs
[RingKernel(
    KernelId = "VectorAdd",
    InputQueueCapacity = 128,
    OutputQueueCapacity = 128)]
public static void Execute(
    Span<long> timestamps,
    Span<VectorAddRequest> requestQueue,
    Span<VectorAddResponse> responseQueue,
    ReadOnlySpan<float> vectorA,
    ReadOnlySpan<float> vectorB,
    Span<float> result)
{
    // This C# code is NEVER compiled to PTX
    // The placeholder PTX ignores this logic entirely
}
```

---

## Problem Statement

### Critical Gaps

1. **No C# → PTX Compilation**
   - `[RingKernel]` methods are not compiled to PTX
   - Placeholder PTX ignores user kernel logic
   - No integration between C# source and GPU execution

2. **No Cooperative Kernel Launch**
   - `cuLaunchCooperativeKernel` call is commented out
   - Kernels are compiled but never executed
   - No message processing on GPU

3. **No Execution Validation**
   - No CUDA event timing
   - No verification of GPU execution
   - No correctness tests for kernel output

4. **No Performance Metrics**
   - No profiling infrastructure
   - No Nsight Compute integration
   - No latency/throughput measurements

5. **No End-to-End Testing**
   - Tests only verify placeholder compilation
   - No tests for actual message processing
   - No GPU execution validation

### Impact

- **Current Status**: Ring Kernel system is a **non-functional prototype**
- **User Experience**: Developers cannot write custom GPU kernels
- **Performance**: Zero GPU acceleration (CPU fallback not implemented)
- **Production Readiness**: Cannot be deployed in current state

---

## Design Goals

### Primary Goals

1. **Compile C# to PTX**: Transform `[RingKernel]` methods into executable GPU code
2. **Execute on GPU**: Launch compiled kernels using cooperative groups
3. **Process Messages**: Read from input queue, execute logic, write to output queue
4. **Validate Execution**: Prove kernels run on GPU with event timing
5. **Measure Performance**: Integrate profiling and metrics collection

### Non-Goals (Future Work)

- Multi-GPU ring kernels (Phase 4 - future)
- Dynamic kernel recompilation
- JIT optimization
- CPU fallback execution

### Quality Requirements

- **Correctness**: 100% of messages processed correctly
- **Performance**: <50ns per message overhead (Ring Kernel latency budget)
- **Reliability**: Graceful error handling, no GPU crashes
- **Testability**: 90%+ code coverage, comprehensive validation tests

---

## Architecture Overview

### High-Level Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ Developer writes [RingKernel] method in C#                      │
│                                                                  │
│  [RingKernel(KernelId = "VectorAdd")]                          │
│  public static void Execute(                                    │
│      Span<VectorAddRequest> requests,                          │
│      Span<VectorAddResponse> responses) { ... }                │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ Source Generator (RingKernelCodeBuilder)                        │
│  ✓ Generates C# host-side wrapper                              │
│  ✓ Generates CUDA kernel stub (NEW)                            │
│  ✓ Emits kernel signature metadata                             │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ CudaRingKernelCompiler                                          │
│  ✓ Detects [RingKernel] method signature                       │
│  ✓ Generates CUDA C++ kernel code                              │
│  ✓ Invokes PTXCompiler with NVRTC                              │
│  ✓ Loads compiled PTX module                                   │
│  ✓ Retrieves mangled function pointer                          │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ CudaRingKernelRuntime                                           │
│  ✓ Creates MessageQueueBridge instances                        │
│  ✓ Allocates GPU buffers for message queues                    │
│  ✓ Launches kernel with cuLaunchCooperativeKernel              │
│  ✓ Monitors execution with CUDA events                         │
│  ✓ Pumps messages Host ↔ Device                                │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ GPU Kernel Execution (Persistent)                               │
│  ✓ Cooperative thread block synchronization                    │
│  ✓ Reads messages from input queue buffer                      │
│  ✓ Executes user kernel logic                                  │
│  ✓ Writes results to output queue buffer                       │
│  ✓ Synchronizes with grid barrier                              │
└─────────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Responsibility | Status |
|-----------|---------------|--------|
| `RingKernelCodeBuilder` | Generate C# wrapper + CUDA stub | ✅ Partial (wrapper only) |
| `CudaRingKernelCompiler` | Compile C# → PTX | ❌ Placeholder only |
| `PTXCompiler` | Invoke NVRTC, load module | ✅ Complete |
| `CudaRingKernelRuntime` | Launch kernel, manage lifecycle | ❌ No kernel launch |
| `MessageQueueBridge` | Host ↔ Device message transfer | ✅ Complete (validated) |
| `CudaEventTimer` | GPU execution timing | ❌ Not implemented |

---

## Component Design

### 1. Source Generator Enhancement

**File**: `/src/Runtime/DotCompute.SourceGenerators/RingKernelCodeBuilder.cs`

**Current Behavior**:
- Generates C# host-side wrapper class
- Creates method signature metadata

**Required Changes**:

```csharp
// NEW: Generate CUDA kernel stub alongside C# wrapper
public void GenerateCudaKernelStub(RingKernelMethod kernel, StringBuilder output)
{
    output.AppendLine($"extern \"C\" __global__ void __launch_bounds__({kernel.ThreadsPerBlock})");
    output.AppendLine($"{kernel.KernelId}_kernel(");

    // Generate parameter list from C# signature
    output.AppendLine("    long* timestamps,");
    output.AppendLine($"    {GetCudaType(kernel.InputType)}* input_queue,");
    output.AppendLine($"    {GetCudaType(kernel.OutputType)}* output_queue,");

    // Additional kernel parameters from Span<> arguments
    foreach (var param in kernel.AdditionalParameters)
    {
        output.AppendLine($"    {GetCudaType(param.Type)}* {param.Name},");
    }

    output.AppendLine("    int input_capacity,");
    output.AppendLine("    int output_capacity)");
    output.AppendLine("{");
    output.AppendLine("    // Cooperative grid synchronization");
    output.AppendLine("    cooperative_groups::grid_group grid = cooperative_groups::this_grid();");
    output.AppendLine("    ");
    output.AppendLine("    // Persistent kernel loop");
    output.AppendLine("    while (true) {");
    output.AppendLine("        // TODO: Implement message processing logic");
    output.AppendLine("        grid.sync();");
    output.AppendLine("    }");
    output.AppendLine("}");
}
```

**Design Decision**: Generate CUDA stub during source generation phase, not runtime. This allows developers to see the CUDA code and debug more easily.

### 2. C# → CUDA Type Mapping

**File**: `/src/Backends/DotCompute.Backends.CUDA/Compilation/CudaTypeMapper.cs` (NEW)

```csharp
/// <summary>
/// Maps C# types to CUDA C++ types for kernel generation.
/// </summary>
public static class CudaTypeMapper
{
    public static string GetCudaType(Type csType)
    {
        return csType.Name switch
        {
            "Int32" => "int",
            "Int64" => "long long",
            "Single" => "float",
            "Double" => "double",
            "Byte" => "unsigned char",
            "Boolean" => "bool",
            _ when csType.IsGenericType && csType.GetGenericTypeDefinition() == typeof(Span<>)
                => $"{GetCudaType(csType.GenericTypeArguments[0])}*",
            _ when csType.IsValueType
                => csType.Name, // Assume struct with same name exists in CUDA
            _ => throw new NotSupportedException($"Type {csType.Name} not supported in CUDA kernels")
        };
    }

    public static string GetMemoryPackSerializationSize(Type messageType)
    {
        // Calculate serialized size for MemoryPack messages
        // Header (256 bytes) + Payload (up to 64KB)
        return "65792"; // 256 + 65536
    }
}
```

### 3. Ring Kernel Compiler (Complete Rewrite)

**File**: `/src/Backends/DotCompute.Backends.CUDA/RingKernels/CudaRingKernelCompiler.cs`

**Current Issues**:
- Only generates placeholder PTX
- Ignores `[RingKernel]` method body
- No integration with user C# code

**New Architecture**:

```csharp
/// <summary>
/// Compiles [RingKernel] C# methods to CUDA PTX using multi-stage pipeline.
/// </summary>
public class CudaRingKernelCompiler
{
    private readonly ILogger<CudaRingKernelCompiler> _logger;
    private readonly ConcurrentDictionary<string, CompiledKernel> _compiledKernels = new();

    /// <summary>
    /// Compilation stages for Ring Kernel PTX generation.
    /// </summary>
    public enum CompilationStage
    {
        Discovery,      // Find [RingKernel] method via reflection
        Analysis,       // Extract method signature, parameters, types
        CudaGeneration, // Generate CUDA C++ kernel code
        PTXCompilation, // Compile CUDA → PTX with NVRTC
        ModuleLoad,     // Load PTX module into CUDA context
        Verification    // Verify function pointer retrieval
    }

    public async Task<CompiledKernel> CompileRingKernelAsync(
        string kernelId,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        // Stage 1: Discovery - Find [RingKernel] method
        var kernelMethod = DiscoverRingKernelMethod(kernelId);
        if (kernelMethod == null)
        {
            throw new InvalidOperationException(
                $"Ring kernel '{kernelId}' not found. Ensure method has [RingKernel] attribute.");
        }

        // Stage 2: Analysis - Extract signature metadata
        var metadata = AnalyzeKernelSignature(kernelMethod);

        // Stage 3: CUDA Generation - Create CUDA C++ code
        var cudaSource = GenerateCudaKernelSource(metadata, options);

        // Stage 4: PTX Compilation - Invoke NVRTC
        var ptxBytes = await PTXCompiler.CompileToPtxAsync(
            cudaSource,
            kernelId,
            options with {
                IncludePaths = ["/usr/local/cuda/include"],
                CompilerFlags = ["--use_fast_math", "-std=c++17", "-rdc=true"]
            },
            _logger);

        // Stage 5: Module Load - Load into CUDA context
        var module = await LoadPTXModuleAsync(ptxBytes, kernelId);

        // Stage 6: Verification - Get kernel function pointer
        var functionPtr = GetKernelFunctionPointer(module, kernelId);

        var compiled = new CompiledKernel(
            kernelId,
            metadata,
            module,
            functionPtr,
            ptxBytes);

        _compiledKernels[kernelId] = compiled;

        _logger.LogInformation(
            "Compiled Ring Kernel '{KernelId}': PTX={PtxSize} bytes, " +
            "Input={InputType}, Output={OutputType}, Function={FunctionPtr:X16}",
            kernelId, ptxBytes.Length, metadata.InputType.Name,
            metadata.OutputType.Name, functionPtr.ToInt64());

        return compiled;
    }

    private MethodInfo? DiscoverRingKernelMethod(string kernelId)
    {
        // Search all loaded assemblies for [RingKernel] methods
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = method.GetCustomAttribute<RingKernelAttribute>();
                    if (attr?.KernelId == kernelId)
                    {
                        return method;
                    }
                }
            }
        }

        return null;
    }

    private KernelMetadata AnalyzeKernelSignature(MethodInfo method)
    {
        var parameters = method.GetParameters();

        // Ring kernel signature pattern:
        // param[0]: Span<long> timestamps
        // param[1]: Span<TInput> requestQueue
        // param[2]: Span<TOutput> responseQueue
        // param[3+]: Additional Span<T> parameters

        if (parameters.Length < 3)
        {
            throw new InvalidOperationException(
                $"Ring kernel must have at least 3 parameters: " +
                $"Span<long> timestamps, Span<TInput> requests, Span<TOutput> responses");
        }

        var inputType = ExtractSpanElementType(parameters[1].ParameterType);
        var outputType = ExtractSpanElementType(parameters[2].ParameterType);

        var additionalParams = parameters.Skip(3)
            .Select(p => new KernelParameter(
                p.Name!,
                ExtractSpanElementType(p.ParameterType)))
            .ToList();

        return new KernelMetadata(
            method.Name,
            inputType,
            outputType,
            additionalParams);
    }

    private string GenerateCudaKernelSource(
        KernelMetadata metadata,
        CompilationOptions options)
    {
        var sb = new StringBuilder();

        // CUDA headers
        sb.AppendLine("#include <cooperative_groups.h>");
        sb.AppendLine("#include <cuda_runtime.h>");
        sb.AppendLine();

        // MemoryPack message structure definitions
        sb.AppendLine($"// Input message: {metadata.InputType.Name}");
        sb.AppendLine(GenerateMessageStructure(metadata.InputType));
        sb.AppendLine();

        sb.AppendLine($"// Output message: {metadata.OutputType.Name}");
        sb.AppendLine(GenerateMessageStructure(metadata.OutputType));
        sb.AppendLine();

        // Kernel function
        sb.AppendLine($"extern \"C\" __global__ void __launch_bounds__(256)");
        sb.AppendLine($"{metadata.KernelName}_kernel(");
        sb.AppendLine($"    long long* timestamps,");
        sb.AppendLine($"    {CudaTypeMapper.GetCudaType(metadata.InputType)}* input_queue,");
        sb.AppendLine($"    {CudaTypeMapper.GetCudaType(metadata.OutputType)}* output_queue,");

        foreach (var param in metadata.AdditionalParameters)
        {
            sb.AppendLine($"    {CudaTypeMapper.GetCudaType(param.Type)}* {param.Name},");
        }

        sb.AppendLine($"    int input_capacity,");
        sb.AppendLine($"    int output_capacity)");
        sb.AppendLine("{");
        sb.AppendLine("    // Cooperative grid group for synchronization");
        sb.AppendLine("    namespace cg = cooperative_groups;");
        sb.AppendLine("    cg::grid_group grid = cg::this_grid();");
        sb.AppendLine();
        sb.AppendLine("    const int tid = blockIdx.x * blockDim.x + threadIdx.x;");
        sb.AppendLine();
        sb.AppendLine("    // Persistent kernel loop");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("        // Poll for messages");
        sb.AppendLine("        if (tid < input_capacity) {");
        sb.AppendLine("            auto request = input_queue[tid];");
        sb.AppendLine("            ");
        sb.AppendLine("            // TODO: Translate C# kernel logic to CUDA");
        sb.AppendLine("            // This requires IL → CUDA transpilation (future work)");
        sb.AppendLine("            ");
        sb.AppendLine("            // Write response");
        sb.AppendLine("            if (tid < output_capacity) {");
        sb.AppendLine("                output_queue[tid] = response;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Synchronize all threads in grid");
        sb.AppendLine("        grid.sync();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateMessageStructure(Type messageType)
    {
        // Generate CUDA struct matching MemoryPack serialization layout
        var sb = new StringBuilder();

        sb.AppendLine($"struct {messageType.Name} {{");

        // IRingKernelMessage base fields
        sb.AppendLine("    char message_id[16];      // Guid (128 bits)");
        sb.AppendLine("    char message_type[256];   // String (max 256 chars)");
        sb.AppendLine("    long long timestamp;      // Int64");

        // Message-specific fields
        foreach (var prop in messageType.GetProperties())
        {
            if (prop.DeclaringType == typeof(IRingKernelMessage))
                continue; // Skip base interface properties

            var cudaType = CudaTypeMapper.GetCudaType(prop.PropertyType);
            sb.AppendLine($"    {cudaType} {ToCamelCase(prop.Name)};");
        }

        sb.AppendLine("};");

        return sb.ToString();
    }
}
```

**Key Design Decisions**:

1. **Multi-Stage Compilation**: Clear separation of Discovery → Analysis → Generation → Compilation → Load → Verification
2. **Reflection-Based Discovery**: Find `[RingKernel]` methods at runtime (compatible with Native AOT via source generators)
3. **MemoryPack Structure Generation**: Generate CUDA structs matching MemoryPack serialization layout
4. **Cooperative Groups**: Use `cooperative_groups::grid_group` for grid-wide synchronization
5. **Persistent Kernel Loop**: Kernel runs indefinitely, polling for messages

### 4. Cooperative Kernel Launch

**File**: `/src/Backends/DotCompute.Backends.CUDA/RingKernels/CudaRingKernelRuntime.cs`

**Current Code** (lines 328-329):
```csharp
// TODO: Replace placeholder with actual cuLaunchCooperativeKernel call
// For now, just log the kernel function pointer
_logger.LogDebug("Ring kernel compiled successfully: {KernelPtr:X16}", kernelPtr);
```

**New Implementation**:

```csharp
/// <summary>
/// Launches a ring kernel using cooperative groups for grid-wide synchronization.
/// </summary>
private async Task LaunchCooperativeKernelAsync(
    CompiledKernel kernel,
    GpuByteBuffer inputBuffer,
    GpuByteBuffer outputBuffer,
    CudaStream stream,
    CancellationToken cancellationToken)
{
    // Calculate grid dimensions
    var (gridDim, blockDim) = CalculateKernelDimensions(
        kernel.Metadata.InputQueueCapacity,
        _deviceProperties.MaxThreadsPerBlock);

    // Prepare kernel parameters
    var kernelParams = new IntPtr[]
    {
        Marshal.AllocHGlobal(IntPtr.Size), // timestamps
        Marshal.AllocHGlobal(IntPtr.Size), // input_queue
        Marshal.AllocHGlobal(IntPtr.Size), // output_queue
        Marshal.AllocHGlobal(sizeof(int)),  // input_capacity
        Marshal.AllocHGlobal(sizeof(int))   // output_capacity
    };

    try
    {
        // Marshal pointers
        Marshal.WriteIntPtr(kernelParams[0], _timestampBuffer.DevicePtr);
        Marshal.WriteIntPtr(kernelParams[1], inputBuffer.DevicePtr);
        Marshal.WriteIntPtr(kernelParams[2], outputBuffer.DevicePtr);
        Marshal.WriteInt32(kernelParams[3], kernel.Metadata.InputQueueCapacity);
        Marshal.WriteInt32(kernelParams[4], kernel.Metadata.OutputQueueCapacity);

        // Create CUDA events for timing
        var startEvent = await CudaEventTimer.CreateEventAsync(_cudaContext);
        var endEvent = await CudaEventTimer.CreateEventAsync(_cudaContext);

        // Record start event
        await CudaEventTimer.RecordEventAsync(startEvent, stream, _cudaContext);

        // Launch cooperative kernel
        _logger.LogInformation(
            "Launching cooperative kernel '{KernelId}': Grid={GridDim}, Block={BlockDim}",
            kernel.KernelId, gridDim, blockDim);

        var launchResult = CudaApi.cuLaunchCooperativeKernel(
            kernel.FunctionPtr,
            gridDim.x, gridDim.y, gridDim.z,
            blockDim.x, blockDim.y, blockDim.z,
            sharedMemBytes: 0,
            stream.Handle,
            kernelParams);

        if (launchResult != CudaError.Success)
        {
            throw new InvalidOperationException(
                $"Cooperative kernel launch failed: {launchResult}");
        }

        // Record end event
        await CudaEventTimer.RecordEventAsync(endEvent, stream, _cudaContext);

        // Synchronize stream to ensure kernel started
        await stream.SynchronizeAsync(cancellationToken);

        // Measure elapsed time
        var elapsedMs = await CudaEventTimer.ElapsedTimeAsync(
            startEvent, endEvent, _cudaContext);

        _logger.LogInformation(
            "Cooperative kernel '{KernelId}' launched successfully. " +
            "Kernel startup time: {ElapsedMs:F3} ms",
            kernel.KernelId, elapsedMs);

        // Store timing event for validation tests
        _kernelTimingEvents[kernel.KernelId] = (startEvent, endEvent);
    }
    finally
    {
        // Free parameter memory
        foreach (var param in kernelParams)
        {
            Marshal.FreeHGlobal(param);
        }
    }
}

private (dim3 gridDim, dim3 blockDim) CalculateKernelDimensions(
    int messageCapacity,
    int maxThreadsPerBlock)
{
    // Use 256 threads per block (optimal for most GPUs)
    var threadsPerBlock = Math.Min(256, maxThreadsPerBlock);

    // Calculate blocks needed to cover message capacity
    var blocks = (messageCapacity + threadsPerBlock - 1) / threadsPerBlock;

    // Limit blocks to device maximum
    blocks = Math.Min(blocks, _deviceProperties.MaxGridDim.x);

    return (
        gridDim: new dim3((uint)blocks, 1, 1),
        blockDim: new dim3((uint)threadsPerBlock, 1, 1)
    );
}
```

### 5. CUDA Event Timing Infrastructure

**File**: `/src/Backends/DotCompute.Backends.CUDA/Timing/CudaEventTimer.cs` (NEW)

```csharp
/// <summary>
/// Provides high-precision GPU timing using CUDA events.
/// </summary>
public static class CudaEventTimer
{
    /// <summary>
    /// Creates a CUDA event for timing.
    /// </summary>
    public static async Task<IntPtr> CreateEventAsync(IntPtr cudaContext)
    {
        return await Task.Run(() =>
        {
            CudaRuntime.cuCtxSetCurrent(cudaContext);

            var eventPtr = IntPtr.Zero;
            var result = CudaRuntime.cudaEventCreate(ref eventPtr);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to create CUDA event: {result}");
            }

            return eventPtr;
        });
    }

    /// <summary>
    /// Records a CUDA event in the specified stream.
    /// </summary>
    public static async Task RecordEventAsync(
        IntPtr eventPtr,
        CudaStream stream,
        IntPtr cudaContext)
    {
        await Task.Run(() =>
        {
            CudaRuntime.cuCtxSetCurrent(cudaContext);

            var result = CudaRuntime.cudaEventRecord(eventPtr, stream.Handle);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to record CUDA event: {result}");
            }
        });
    }

    /// <summary>
    /// Calculates elapsed time between two CUDA events in milliseconds.
    /// </summary>
    public static async Task<float> ElapsedTimeAsync(
        IntPtr startEvent,
        IntPtr endEvent,
        IntPtr cudaContext)
    {
        return await Task.Run(() =>
        {
            CudaRuntime.cuCtxSetCurrent(cudaContext);

            // Synchronize end event
            var syncResult = CudaRuntime.cudaEventSynchronize(endEvent);
            if (syncResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to synchronize CUDA event: {syncResult}");
            }

            // Calculate elapsed time
            var elapsedMs = 0f;
            var result = CudaRuntime.cudaEventElapsedTime(
                ref elapsedMs, startEvent, endEvent);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to calculate CUDA event elapsed time: {result}");
            }

            return elapsedMs;
        });
    }

    /// <summary>
    /// Destroys a CUDA event and releases resources.
    /// </summary>
    public static async Task DestroyEventAsync(
        IntPtr eventPtr,
        IntPtr cudaContext)
    {
        await Task.Run(() =>
        {
            CudaRuntime.cuCtxSetCurrent(cudaContext);

            var result = CudaRuntime.cudaEventDestroy(eventPtr);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to destroy CUDA event: {result}");
            }
        });
    }
}
```

---

## Implementation Phases

### Phase 1: Foundation (Week 1-2)

**Goal**: Establish compilation pipeline infrastructure

**Tasks**:
1. Create `CudaTypeMapper` for C# → CUDA type conversion
2. Enhance `RingKernelCodeBuilder` to generate CUDA stubs
3. Implement kernel discovery via reflection
4. Create `CudaEventTimer` timing infrastructure

**Deliverables**:
- Type mapping for primitive types and structs
- CUDA kernel stub generation
- Reflection-based kernel discovery
- CUDA event timing API

**Success Criteria**:
- ✅ Can discover `[RingKernel]` methods via reflection
- ✅ Can generate CUDA kernel stub from C# signature
- ✅ Can map C# types to CUDA types
- ✅ Can create and record CUDA events

### Phase 2: Compilation Pipeline (Week 3-4)

**Goal**: Implement full C# → PTX compilation

**Tasks**:
1. Rewrite `CudaRingKernelCompiler` multi-stage pipeline
2. Implement `GenerateCudaKernelSource()` with MemoryPack structs
3. Integrate with existing `PTXCompiler` infrastructure
4. Add PTX module loading and function pointer retrieval
5. Implement cooperative kernel launch

**Deliverables**:
- Complete compilation pipeline (Discovery → PTX)
- MemoryPack-compatible CUDA structures
- Cooperative kernel launch implementation
- Function pointer verification

**Success Criteria**:
- ✅ Can compile `[RingKernel]` method to valid PTX
- ✅ PTX module loads successfully
- ✅ Can retrieve kernel function pointer
- ✅ `cuLaunchCooperativeKernel` succeeds without errors

### Phase 3: Message Processing (Week 5-6)

**Goal**: Implement GPU-side message processing logic

**Tasks**:
1. Implement message deserialization in CUDA kernel
2. Add message validation on GPU (MessageId, MessageType checks)
3. Implement kernel logic placeholder (developer customization point)
4. Add response serialization and queue write
5. Integrate with `MessageQueueBridge` for Host ↔ Device transfer

**Deliverables**:
- GPU message deserialization
- Message validation logic
- Response queue writing
- End-to-end message flow

**Success Criteria**:
- ✅ Kernel reads valid messages from input queue
- ✅ Kernel writes responses to output queue
- ✅ Host receives responses via `MessageQueueBridge`
- ✅ Message count matches expected (no anomalies)

### Phase 4: Validation & Testing (Week 7-8)

**Goal**: Comprehensive testing and validation

**Tasks**:
1. Create GPU execution validation tests
2. Add CUDA event timing verification
3. Implement correctness tests (vector addition, matrix multiply)
4. Add performance benchmarks
5. Integrate Nsight Compute profiling

**Deliverables**:
- 20+ GPU execution validation tests
- CUDA event timing tests
- Correctness validation (VectorAdd, MatMul)
- Performance benchmarks
- Nsight Compute integration

**Success Criteria**:
- ✅ 100% of messages processed correctly
- ✅ CUDA event timing shows GPU execution
- ✅ Kernel appears in Nsight Compute trace
- ✅ Performance meets <50ns per message target

---

## Testing Strategy

### Unit Tests

**Test Coverage**:
- `CudaTypeMapperTests` - C# → CUDA type mapping
- `KernelDiscoveryTests` - Reflection-based method discovery
- `CudaKernelSourceGenerationTests` - CUDA code generation
- `PTXCompilationTests` - NVRTC compilation
- `CooperativeKernelLaunchTests` - Kernel launch verification

### Integration Tests

**Test Coverage**:
- `EndToEndRingKernelTests` - Full pipeline (C# → GPU execution)
- `MessageQueueIntegrationTests` - Host ↔ Device message flow
- `CudaEventTimingTests` - GPU execution timing validation

### Hardware Tests

**Test Coverage**:
- `VectorAddRingKernelTests` - Correctness validation
- `MatrixMultiplyRingKernelTests` - Complex kernel validation
- `PerformanceBenchmarkTests` - Latency/throughput measurements

### Validation Criteria

| Test Category | Pass Criteria |
|--------------|---------------|
| Type Mapping | 100% of C# types map to valid CUDA types |
| Discovery | All `[RingKernel]` methods found |
| Compilation | PTX compiles without errors |
| Launch | `cuLaunchCooperativeKernel` returns `Success` |
| Timing | CUDA events show >0ms elapsed time |
| Correctness | 100% of messages processed correctly |
| Performance | <50ns per message overhead |

---

## Performance Considerations

### Optimization Targets

**Compilation Performance**:
- **Target**: <500ms for kernel compilation (NVRTC)
- **Strategy**: Cache compiled PTX modules by kernel ID + input/output types

**Message Throughput**:
- **Target**: >20M messages/second (RTX 2000 Ada)
- **Strategy**: Batch message processing, minimize grid synchronization

**Latency**:
- **Target**: <50ns per message overhead
- **Strategy**: Persistent kernel loop, avoid kernel launch overhead

### Memory Optimization

**Buffer Sizing**:
- Input queue: 128 messages × 65,792 bytes = 8.4 MB
- Output queue: 128 messages × 65,792 bytes = 8.4 MB
- Total GPU memory: ~17 MB per ring kernel instance

**Memory Pooling**:
- Reuse GPU buffers across kernel invocations
- Use existing `MemoryPool` infrastructure
- Implement buffer compaction for sparse messages

### CUDA Optimization

**Thread Configuration**:
- Use 256 threads per block (optimal for most GPUs)
- Launch enough blocks to cover message capacity
- Leverage L1 cache for message buffers

**Grid Synchronization**:
- Minimize `grid.sync()` calls (high overhead)
- Use warp-level synchronization where possible
- Consider per-CTA (thread block) processing

---

## Risk Mitigation

### Technical Risks

**Risk 1: C# → CUDA Translation Complexity**
- **Impact**: High - Core functionality
- **Mitigation**: Start with simple kernels (VectorAdd), expand incrementally
- **Fallback**: Manual CUDA kernel development for complex logic

**Risk 2: Cooperative Kernel Launch Failures**
- **Impact**: Medium - Requires specific GPU capabilities
- **Mitigation**: Validate device supports cooperative launch during initialization
- **Fallback**: Use standard kernel launch with manual synchronization

**Risk 3: MemoryPack Serialization Overhead**
- **Impact**: Medium - Affects performance
- **Mitigation**: Benchmark serialization latency, optimize hot paths
- **Fallback**: Custom binary serialization for performance-critical kernels

**Risk 4: NVRTC Compilation Errors**
- **Impact**: High - Blocks kernel execution
- **Mitigation**: Generate valid CUDA C++ with extensive testing
- **Fallback**: Provide detailed error messages, suggest fixes

### Schedule Risks

**Risk 1: Underestimated Complexity**
- **Impact**: High - Delays delivery
- **Mitigation**: Incremental phases with clear milestones
- **Contingency**: Prioritize core functionality, defer optimizations

**Risk 2: Hardware Availability**
- **Impact**: Low - Testing blocked
- **Mitigation**: Use existing RTX 2000 Ada (CC 8.9)
- **Contingency**: Emulator testing for non-critical paths

---

## Conclusion

This architecture document provides a comprehensive roadmap for implementing a production-ready Ring Kernel compilation pipeline. The multi-phase approach balances technical risk with incremental delivery, ensuring each component is thoroughly tested before integration.

**Next Steps**:
1. Review and approve this architecture
2. Begin Phase 1 implementation (Foundation)
3. Create tracking issues for each phase
4. Schedule weekly progress reviews

**Timeline**: 8 weeks to production-ready implementation
**Quality Standard**: Production-grade, no shortcuts, comprehensive testing

---

**Document Revision History**:
- v1.0 (January 2025) - Initial architecture design
