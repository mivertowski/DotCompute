# Metal PageRank GPU Native Actors - Implementation Guide

**Document Version**: 1.0
**Last Updated**: 2025-01-24
**Status**: Active Development
**Estimated Completion**: 6-9 weeks

## Quick Reference

| Phase | Priority | Duration | Dependencies | Status |
|-------|----------|----------|--------------|--------|
| Phase 1: Foundation (MSL Generation) | CRITICAL | 1-2 weeks | None | 🔄 IN PROGRESS |
| Phase 2: PageRank Kernels | HIGH | 2-3 weeks | Phase 1 | ⏳ PENDING |
| Phase 3: Integration & Testing | HIGH | 1-2 weeks | Phase 2 | ⏳ PENDING |
| Phase 4: Benchmarking | MEDIUM | 1 week | Phase 3 | ⏳ PENDING |
| Phase 5: Fix TODOs | LOW | 3-5 days | None (parallel) | ⏳ PENDING |
| Phase 6: Documentation | LOW | 1 week | All phases | ⏳ PENDING |

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Current State Analysis](#current-state-analysis)
3. [Phase 1: Foundation](#phase-1-foundation---msl-code-generation)
4. [Phase 2: PageRank Kernels](#phase-2-pagerank-kernel-implementation)
5. [Phase 3: Integration](#phase-3-integration--e2e-testing)
6. [Phase 4: Benchmarking](#phase-4-benchmarking--performance)
7. [Phase 5: TODO Cleanup](#phase-5-fix-outstanding-todos)
8. [Phase 6: Documentation](#phase-6-documentation--samples)
9. [Testing Strategy](#testing-strategy)
10. [Performance Targets](#performance-targets)
11. [Known Limitations](#known-limitations)
12. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

### Ring Kernel System

The Metal Ring Kernel system implements persistent GPU kernels using an actor-based message-passing architecture:

```
┌──────────────────────────────────────────────────────────────┐
│                    Ring Kernel Architecture                  │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐                      ┌──────────────┐     │
│  │  Host (CPU)  │ ◄───── Control ─────►│ GPU Kernel   │     │
│  │              │                      │  (Persistent) │     │
│  └──────────────┘                      └──────────────┘     │
│         │                                      │             │
│         │ Messages                             │ K2K         │
│         ▼                                      ▼             │
│  ┌──────────────┐                      ┌──────────────┐     │
│  │ Message      │                      │ Message      │     │
│  │ Queue (GPU)  │ ◄──────────────────► │ Queue (GPU)  │     │
│  └──────────────┘    Lock-Free Ring    └──────────────┘     │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### PageRank 3-Kernel Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                   PageRank Message Flow                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  StartIterationCommand (from Host)                              │
│         │                                                       │
│         ▼                                                       │
│  ┌──────────────────────┐                                      │
│  │ ContributionSender   │                                      │
│  │  - Read node ranks   │                                      │
│  │  - Split by edges    │                                      │
│  └──────────────────────┘                                      │
│         │  PageRankContribution                                │
│         │  (SourceNodeId, TargetNodeId,                        │
│         │   Contribution, Iteration)                           │
│         ▼                                                       │
│  ┌──────────────────────┐                                      │
│  │  RankAggregator      │                                      │
│  │  - Aggregate contrib │                                      │
│  │  - Apply PageRank    │                                      │
│  │    formula           │                                      │
│  │  - Use barriers      │                                      │
│  └──────────────────────┘                                      │
│         │  RankAggregationResult                               │
│         │  (NodeId, NewRank, Delta,                            │
│         │   Iteration)                                         │
│         ▼                                                       │
│  ┌──────────────────────┐                                      │
│  │ ConvergenceChecker   │                                      │
│  │  - Check delta       │                                      │
│  │  - Detect converged  │                                      │
│  └──────────────────────┘                                      │
│         │  NodeRankUpdate (if not converged)                   │
│         │  (loop back to next iteration)                       │
│         ▼                                                       │
│     Convergence or MaxIterations                               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Metal Compilation Pipeline (6 Stages)

```
Stage 1: Discovery          → Find [RingKernel] methods via reflection
Stage 2: Analysis           → Extract metadata, validate parameters
Stage 2.5: Handler Translation → Translate inline C# handlers to MSL
Stage 3: MSL Generation     → Generate Metal Shading Language code
Stage 4: Library Compilation → Compile MSL to Metal library
Stage 5: Pipeline State     → Create MTLComputePipelineState
Stage 6: Verification       → Verify pipeline is executable
```

---

## Current State Analysis

### ✅ What's Already Implemented

#### Core Infrastructure (230KB+ of code)

1. **MetalRingKernelRuntime** (`src/Backends/DotCompute.Backends.Metal/RingKernels/MetalRingKernelRuntime.cs`)
   - ✅ Full IRingKernelRuntime implementation (1,078 lines)
   - ✅ Lifecycle management (Launch, Activate, Deactivate, Terminate)
   - ✅ Named message queue management
   - ✅ Telemetry support
   - ✅ Device/command queue lifecycle

2. **MetalRingKernelCompiler** (2 files, 858 lines total)
   - ✅ 6-stage compilation pipeline (`MetalRingKernelCompiler.Pipeline.cs`, 578 lines)
   - ✅ Basic MSL generation (`MetalRingKernelCompiler.cs`, 280 lines)
   - ✅ Caching support
   - ✅ Discovery, Analysis, MSL Generation, Library Compilation, Pipeline State, Verification

3. **MetalMemoryPackSerializerGenerator** (`src/Backends/DotCompute.Backends.Metal/Compilation/MetalMemoryPackSerializerGenerator.cs`)
   - ✅ Generates MSL structs, deserializers, serializers (837 lines)
   - ✅ Handles MemoryPack binary format
   - ✅ Batch generation capability
   - ✅ Message handler generation

4. **Supporting Infrastructure**
   - ✅ **MetalMessageQueue**: Lock-free ring buffer (19,875 bytes)
   - ✅ **MetalKernelRoutingTable**: K2K routing infrastructure (17,937 bytes)
   - ✅ **MetalMultiKernelBarrier**: Cross-kernel barriers (13,655 bytes)
   - ✅ **MetalTopicRegistry**: Pub/sub system (26,121 bytes)
   - ✅ **MetalTaskQueue**: Task scheduling (14,169 bytes)
   - ✅ **MetalTelemetryBuffer**: Real-time metrics (7,945 bytes)

### ⚠️ What's Missing

#### Critical Gaps

1. **MSL Message Processing Logic**
   - Current code has `TODO: Process message based on kernel logic` (lines 211, 238)
   - No integration of MemoryPack deserialization into generated kernels
   - No handler invocation in persistent loop

2. **Barrier MSL Kernels**
   - `MetalMultiKernelBarrier.cs` has 3 TODOs for actual kernel execution
   - Needs `MultiKernelBarrier.metal` with wait/reset/failed kernels

3. **K2K Routing MSL Kernels**
   - Routing infrastructure exists but no actual routing kernels
   - Needs `KernelRouter.metal` with hash table lookup

4. **PageRank Implementation**
   - No Metal-specific PageRank kernels
   - No message definitions for Metal
   - No orchestrator

---

## Phase 1: Foundation - MSL Code Generation

**Priority**: CRITICAL
**Duration**: 1-2 weeks
**Status**: 🔄 IN PROGRESS

### Objective

Fix the MSL code generation to properly integrate MemoryPack serialization and generate complete, executable kernel code.

### Tasks

#### 1.1 Enhance MetalRingKernelCompiler.cs

**File**: `src/Backends/DotCompute.Backends.Metal/RingKernels/MetalRingKernelCompiler.cs`

**Changes Required**:

```csharp
// CURRENT (Line 210-212):
sb.AppendLine("        if (input_queue->try_dequeue(msg_buffer)) {");
sb.AppendLine("            // TODO: Process message based on kernel logic");
sb.AppendLine("            // This is where the translated C# code goes");

// NEW:
sb.AppendLine("        char msg_buffer[512]; // Sized for largest message");
sb.AppendLine("        if (input_queue->try_dequeue(msg_buffer)) {");
sb.AppendLine("            // Deserialize message");
sb.AppendLine($"            {inputMessageStruct} input_msg;");
sb.AppendLine($"            if (!deserialize_{inputMessageStruct}((device const uchar*)msg_buffer, 512, &input_msg)) {{");
sb.AppendLine("                continue; // Skip malformed messages");
sb.AppendLine("            }");
sb.AppendLine();
sb.AppendLine("            // Process message using inline handler or default logic");
if (!string.IsNullOrEmpty(config.InlineHandlerMslCode)) {
    sb.AppendLine(config.InlineHandlerMslCode);  // Insert translated handler
} else {
    sb.AppendLine("            // Default echo behavior");
    sb.AppendLine($"            {outputMessageStruct} output_msg = input_msg;");
}
sb.AppendLine();
sb.AppendLine("            // Serialize result");
sb.AppendLine("            char output_buffer[512];");
sb.AppendLine($"            int bytes_written = serialize_{outputMessageStruct}(&output_msg, (device uchar*)output_buffer, 512);");
sb.AppendLine("            if (bytes_written > 0) {");
sb.AppendLine("                output_queue->try_enqueue(output_buffer);");
sb.AppendLine("            }");
```

**Key Points**:
- Replace generic `char msg_buffer[256]` with proper message struct deserialization
- Use `_serializerGenerator` to generate MSL serialization code
- Integrate inline handler MSL code if available
- Add error handling for deserialization failures

#### 1.2 Add MemoryPack Integration Method

**Add to MetalRingKernelCompiler.cs**:

```csharp
/// <summary>
/// Generates MemoryPack serialization code for ring kernel message types.
/// </summary>
private string GenerateMemoryPackSerializers(
    string inputMessageTypeName,
    string? outputMessageTypeName,
    IEnumerable<Assembly>? assemblies)
{
    if (_serializerGenerator == null)
    {
        throw new InvalidOperationException("Serializer generator not initialized");
    }

    var messageTypes = new List<Type>();

    // Find input message type
    var inputType = FindMessageType(inputMessageTypeName, assemblies);
    if (inputType != null)
    {
        messageTypes.Add(inputType);
    }

    // Find output message type if different
    if (!string.IsNullOrEmpty(outputMessageTypeName) && outputMessageTypeName != inputMessageTypeName)
    {
        var outputType = FindMessageType(outputMessageTypeName, assemblies);
        if (outputType != null)
        {
            messageTypes.Add(outputType);
        }
    }

    if (messageTypes.Count == 0)
    {
        _logger.LogWarning("No message types found for serialization generation");
        return string.Empty;
    }

    // Generate batch serializers (skip handler generation - we'll use inline handler)
    return _serializerGenerator.GenerateBatchSerializer(
        messageTypes,
        compilationUnitName: "RingKernelMessages",
        skipHandlerGeneration: true);
}

/// <summary>
/// Finds a message type by name across assemblies.
/// </summary>
[RequiresUnreferencedCode("Message type lookup uses runtime reflection")]
private static Type? FindMessageType(string typeName, IEnumerable<Assembly>? assemblies)
{
    var assembliesToSearch = assemblies ?? AppDomain.CurrentDomain.GetAssemblies();

    foreach (var assembly in assembliesToSearch)
    {
        try
        {
            var type = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
            if (type != null)
            {
                return type;
            }
        }
        catch
        {
            // Skip assemblies that can't be loaded
        }
    }

    return null;
}
```

#### 1.3 Update CompileToMSL Method

**Modify `CompileToMSL` to include serializers**:

```csharp
public string CompileToMSL(
    KernelDefinition kernelDefinition,
    string sourceCode,
    RingKernelConfig config,
    IEnumerable<Assembly>? assemblies = null)  // ADD assemblies parameter
{
    ArgumentNullException.ThrowIfNull(kernelDefinition);
    ArgumentException.ThrowIfNullOrWhiteSpace(sourceCode);
    ArgumentNullException.ThrowIfNull(config);

    _logger.LogInformation(
        "Compiling ring kernel '{KernelName}' (ID: {KernelId}) to MSL",
        kernelDefinition.Name,
        config.KernelId);

    var mslSource = new StringBuilder(8192);  // Larger buffer

    // Include necessary headers
    GenerateHeaders(mslSource);

    // Generate MemoryPack serializers for message types
    if (!string.IsNullOrEmpty(config.InputMessageTypeName))
    {
        var serializers = GenerateMemoryPackSerializers(
            config.InputMessageTypeName,
            config.OutputMessageTypeName,
            assemblies);

        if (!string.IsNullOrEmpty(serializers))
        {
            mslSource.AppendLine("// ===== MemoryPack Serializers =====");
            mslSource.AppendLine(serializers);
            mslSource.AppendLine();
        }
    }

    // Generate message queue structure
    GenerateMessageQueueStructure(mslSource, config);

    // Generate control structure
    GenerateControlStructure(mslSource);

    // Generate persistent kernel (NOW with proper message processing)
    GeneratePersistentKernel(mslSource, kernelDefinition, config);

    var result = mslSource.ToString();
    _logger.LogDebug("Generated {Length} bytes of MSL code", result.Length);

    return result;
}
```

#### 1.4 Add Barrier Support to Kernels

**Enhance `GeneratePersistentLoop` for graph analytics**:

```csharp
private static void GeneratePersistentLoop(StringBuilder sb, RingKernelConfig config)
{
    sb.AppendLine("    // Persistent kernel loop");
    sb.AppendLine("    while (true) {");
    sb.AppendLine("        // Check for termination");
    sb.AppendLine("        if (atomic_load_explicit(&control->terminate, memory_order_acquire) == 1) {");
    sb.AppendLine("            break;");
    sb.AppendLine("        }");
    sb.AppendLine();
    sb.AppendLine("        // Wait for activation");
    sb.AppendLine("        while (atomic_load_explicit(&control->active, memory_order_acquire) == 0) {");
    sb.AppendLine("            if (atomic_load_explicit(&control->terminate, memory_order_acquire) == 1) {");
    sb.AppendLine("                return;");
    sb.AppendLine("            }");

    if (config.Domain == RingKernelDomain.GraphAnalytics)
    {
        sb.AppendLine("            // Brief sleep with memory fence for graph analytics");
        sb.AppendLine("            threadgroup_barrier(mem_flags::mem_device);");
    }
    else
    {
        sb.AppendLine("            // Brief sleep to reduce power consumption");
        sb.AppendLine("            threadgroup_barrier(mem_flags::mem_none);");
    }

    sb.AppendLine("        }");
    sb.AppendLine();

    // ADD: Shared memory for aggregation (graph analytics)
    if (config.Domain == RingKernelDomain.GraphAnalytics)
    {
        sb.AppendLine("        // Threadgroup shared memory for aggregation");
        sb.AppendLine("        threadgroup float shared_accumulator[256];");
        sb.AppendLine("        if (tid < 256) {");
        sb.AppendLine("            shared_accumulator[tid] = 0.0f;");
        sb.AppendLine("        }");
        sb.AppendLine("        threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine();
    }

    // Process messages with MemoryPack deserialization
    var inputStruct = ToSnakeCase(config.InputMessageTypeName ?? "message");
    var outputStruct = ToSnakeCase(config.OutputMessageTypeName ?? "message");

    sb.AppendLine("        // Process messages");
    sb.AppendLine("        char msg_buffer[512];");
    sb.AppendLine("        if (input_queue->try_dequeue(msg_buffer)) {");
    sb.AppendLine($"            // Deserialize input message");
    sb.AppendLine($"            {inputStruct} input_msg;");
    sb.AppendLine($"            if (deserialize_{inputStruct}((device const uchar*)msg_buffer, 512, &input_msg)) {{");
    sb.AppendLine();

    // Handler invocation
    if (!string.IsNullOrEmpty(config.InlineHandlerMslCode))
    {
        sb.AppendLine("                // Invoke inline handler");
        sb.AppendLine($"                {outputStruct} output_msg;");
        sb.AppendLine(config.InlineHandlerMslCode);
    }
    else
    {
        sb.AppendLine("                // Default echo behavior");
        sb.AppendLine($"                {outputStruct} output_msg = input_msg;");
    }

    sb.AppendLine();
    sb.AppendLine("                // Update message counter");
    sb.AppendLine("                atomic_fetch_add_explicit(&control->msg_count, 1L, memory_order_relaxed);");
    sb.AppendLine();
    sb.AppendLine("                // Serialize output");
    sb.AppendLine("                char output_buffer[512];");
    sb.AppendLine($"                int bytes_written = serialize_{outputStruct}(&output_msg, (device uchar*)output_buffer, 512);");
    sb.AppendLine("                if (bytes_written > 0) {");
    sb.AppendLine("                    output_queue->try_enqueue(output_buffer);");
    sb.AppendLine("                }");
    sb.AppendLine("            }");
    sb.AppendLine("        }");
    sb.AppendLine();

    // Barrier synchronization for graph analytics
    if (config.Domain == RingKernelDomain.GraphAnalytics)
    {
        sb.AppendLine("        // Graph analytics: Synchronize after message processing");
        sb.AppendLine("        threadgroup_barrier(mem_flags::mem_device | mem_flags::mem_threadgroup);");
    }

    sb.AppendLine("    }");
}

// Helper method
private static string ToSnakeCase(string str)
{
    if (string.IsNullOrEmpty(str)) return str;

    var result = new StringBuilder(str.Length + 10);
    for (int i = 0; i < str.Length; i++)
    {
        char c = str[i];
        if (char.IsUpper(c) && i > 0 &&
            (i + 1 < str.Length && char.IsLower(str[i + 1]) || char.IsLower(str[i - 1])))
        {
            result.Append('_');
            result.Append(char.ToLowerInvariant(c));
        }
        else
        {
            result.Append(char.ToLowerInvariant(c));
        }
    }
    return result.ToString();
}
```

### Testing Phase 1

**Create test**: `tests/Unit/DotCompute.Backends.Metal.Tests/RingKernels/MetalMslGenerationTests.cs`

```csharp
[Fact]
public void GeneratePersistentKernel_WithMemoryPackMessages_IncludesDeserializers()
{
    // Arrange
    var compiler = new MetalRingKernelCompiler(_logger);
    var config = new RingKernelConfig
    {
        KernelId = "test_kernel",
        Mode = RingKernelMode.Persistent,
        InputMessageTypeName = "VectorAddRequest",
        OutputMessageTypeName = "VectorAddResponse",
        Domain = RingKernelDomain.General
    };

    // Act
    var msl = compiler.CompileToMSL(
        new KernelDefinition("test", "test_kernel"),
        "// placeholder",
        config,
        assemblies: new[] { typeof(VectorAddRequest).Assembly });

    // Assert
    Assert.Contains("deserialize_vector_add_request", msl);
    Assert.Contains("serialize_vector_add_response", msl);
    Assert.Contains("struct vector_add_request", msl);
    Assert.Contains("struct vector_add_response", msl);
}
```

---

## Phase 2: PageRank Kernel Implementation

**Priority**: HIGH
**Duration**: 2-3 weeks
**Depends On**: Phase 1 (MSL generation)

### 2.1 PageRank Message Definitions

**New File**: `samples/RingKernels/PageRank/Metal/PageRankMetalMessages.cs`

```csharp
using MemoryPack;

namespace DotCompute.Samples.RingKernels.PageRank.Metal;

/// <summary>
/// Contribution message sent from a node to its neighbors.
/// </summary>
[MemoryPackable]
public partial struct PageRankContribution
{
    /// <summary>Source node ID sending the contribution.</summary>
    public int SourceNodeId { get; set; }

    /// <summary>Target node ID receiving the contribution.</summary>
    public int TargetNodeId { get; set; }

    /// <summary>Rank contribution value.</summary>
    public float Contribution { get; set; }

    /// <summary>Current iteration number.</summary>
    public int Iteration { get; set; }
}

/// <summary>
/// Rank aggregation result after summing contributions.
/// </summary>
[MemoryPackable]
public partial struct RankAggregationResult
{
    /// <summary>Node ID whose rank was aggregated.</summary>
    public int NodeId { get; set; }

    /// <summary>New rank value after aggregation.</summary>
    public float NewRank { get; set; }

    /// <summary>Change in rank from previous iteration.</summary>
    public float Delta { get; set; }

    /// <summary>Current iteration number.</summary>
    public int Iteration { get; set; }
}

/// <summary>
/// Node rank update for non-converged nodes.
/// </summary>
[MemoryPackable]
public partial struct NodeRankUpdate
{
    /// <summary>Node ID to update.</summary>
    public int NodeId { get; set; }

    /// <summary>Current rank value.</summary>
    public float Rank { get; set; }

    /// <summary>Number of outbound edges.</summary>
    public int OutboundEdgeCount { get; set; }

    /// <summary>Current iteration number.</summary>
    public int Iteration { get; set; }
}
```

### 2.2 ContributionSender Kernel

**New File**: `samples/RingKernels/PageRank/Metal/PageRankContributionSenderKernel.cs`

```csharp
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.Metal.RingKernels;

namespace DotCompute.Samples.RingKernels.PageRank.Metal;

/// <summary>
/// ContributionSender kernel: Sends rank contributions to neighbor nodes.
/// </summary>
public static class PageRankContributionSenderKernel
{
    [RingKernel(
        KernelId = "pagerank_contribution_sender",
        Backends = new[] { "Metal" },
        ProcessingMode = RingProcessingMode.Batch,
        BatchSize = 64)]
    [InputQueue(typeof(GraphEdgeInfo), capacity: 1024)]
    [OutputQueue(typeof(PageRankContribution), capacity: 2048)]
    [PublishesToKernel("pagerank_rank_aggregator")]
    public static void SendContributions(RingKernelContext context)
    {
        // This method will be translated to MSL by the compiler
        // For now, it serves as metadata for kernel generation

        // MSL implementation will:
        // 1. Dequeue GraphEdgeInfo message
        // 2. Calculate contribution = rank / outbound_edge_count
        // 3. For each target in TargetNodeIds:
        //    - Create PageRankContribution message
        //    - Enqueue to output (K2K to rank_aggregator)
    }

    // Inline handler (will be translated to MSL)
    private static PageRankContribution ProcessEdgeInfo(GraphEdgeInfo edgeInfo)
    {
        float contribution = edgeInfo.CurrentRank / edgeInfo.TargetNodeIds.Length;
        return new PageRankContribution
        {
            SourceNodeId = edgeInfo.SourceNodeId,
            TargetNodeId = edgeInfo.TargetNodeIds[0], // Simplified - real impl loops
            Contribution = contribution,
            Iteration = edgeInfo.Iteration
        };
    }
}

/// <summary>
/// Graph edge information for a node.
/// </summary>
[MemoryPackable]
public partial struct GraphEdgeInfo
{
    public int SourceNodeId { get; set; }
    public int[] TargetNodeIds { get; set; }  // Outbound edges
    public float CurrentRank { get; set; }
    public int Iteration { get; set; }
}
```

**Corresponding MSL**: `samples/RingKernels/PageRank/Metal/PageRankContributionSender.metal`

```metal
// Auto-generated MSL for pagerank_contribution_sender
// This is what the compiler should generate

kernel void pagerank_contribution_sender_kernel(
    device MessageQueue<char>* input_queue  [[buffer(0)]],
    device MessageQueue<char>* output_queue [[buffer(1)]],
    device KernelControl* control           [[buffer(2)]],
    uint thread_id [[thread_position_in_grid]],
    uint threadgroup_id [[threadgroup_position_in_grid]],
    uint thread_in_threadgroup [[thread_position_in_threadgroup]],
    uint threads_per_threadgroup [[threads_per_threadgroup]])
{
    uint tid = thread_in_threadgroup;

    // Persistent loop
    while (true) {
        if (atomic_load_explicit(&control->terminate, memory_order_acquire) == 1) {
            break;
        }

        while (atomic_load_explicit(&control->active, memory_order_acquire) == 0) {
            if (atomic_load_explicit(&control->terminate, memory_order_acquire) == 1) {
                return;
            }
            threadgroup_barrier(mem_flags::mem_none);
        }

        // Process messages
        char msg_buffer[512];
        if (input_queue->try_dequeue(msg_buffer)) {
            // Deserialize GraphEdgeInfo
            graph_edge_info edge_info;
            if (deserialize_graph_edge_info((device const uchar*)msg_buffer, 512, &edge_info)) {
                // Calculate contribution per edge
                float contribution_per_edge = edge_info.current_rank / edge_info.outbound_edge_count;

                // Send contribution to each neighbor
                for (int i = 0; i < edge_info.outbound_edge_count; i++) {
                    page_rank_contribution contrib;
                    contrib.source_node_id = edge_info.source_node_id;
                    contrib.target_node_id = edge_info.target_node_ids[i];
                    contrib.contribution = contribution_per_edge;
                    contrib.iteration = edge_info.iteration;

                    // Serialize and enqueue
                    char output_buffer[512];
                    int bytes = serialize_page_rank_contribution(&contrib, (device uchar*)output_buffer, 512);
                    if (bytes > 0) {
                        output_queue->try_enqueue(output_buffer);
                    }
                }

                atomic_fetch_add_explicit(&control->msg_count, 1L, memory_order_relaxed);
            }
        }
    }
}
```

### 2.3 RankAggregator Kernel

**Key Features**:
- Uses threadgroup shared memory for aggregation
- Applies PageRank formula: `newRank = (1-d)/N + d * sum(contributions)`
- Requires barriers for synchronization

**New File**: `samples/RingKernels/PageRank/Metal/PageRankRankAggregatorKernel.cs`

```csharp
[RingKernel(
    KernelId = "pagerank_rank_aggregator",
    Backends = new[] { "Metal" },
    ProcessingMode = RingProcessingMode.Batch,
    BatchSize = 128)]
[InputQueue(typeof(PageRankContribution), capacity: 4096)]
[OutputQueue(typeof(RankAggregationResult), capacity: 1024)]
[SubscribesToKernel("pagerank_contribution_sender")]
[PublishesToKernel("pagerank_convergence_checker")]
[UseBarrier(BarrierScope.ThreadBlock)] // Threadgroup barrier for aggregation
public static void AggregateRanks(RingKernelContext context)
{
    // MSL implementation will:
    // 1. Dequeue PageRankContribution messages
    // 2. Use shared memory to accumulate contributions per node
    // 3. Apply barrier for synchronization
    // 4. Apply PageRank formula
    // 5. Calculate delta from previous rank
    // 6. Enqueue RankAggregationResult
}
```

**Corresponding MSL** (excerpt):

```metal
kernel void pagerank_rank_aggregator_kernel(...)
{
    // Shared memory for aggregation
    threadgroup float shared_contributions[256];
    threadgroup int shared_node_ids[256];
    threadgroup int contribution_count;

    if (tid == 0) {
        contribution_count = 0;
    }
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Batch process contributions
    for (int batch = 0; batch < 128; batch++) {
        char msg_buffer[512];
        if (input_queue->try_dequeue(msg_buffer)) {
            page_rank_contribution contrib;
            if (deserialize_page_rank_contribution((device const uchar*)msg_buffer, 512, &contrib)) {
                // Atomic add to shared memory
                int slot = contrib.target_node_id % 256;
                shared_contributions[slot] += contrib.contribution;
                shared_node_ids[slot] = contrib.target_node_id;
                atomic_fetch_add_explicit(&contribution_count, 1, memory_order_relaxed);
            }
        }
    }

    // Barrier: Wait for all threads to finish accumulation
    threadgroup_barrier(mem_flags::mem_device | mem_flags::mem_threadgroup);

    // Apply PageRank formula
    if (tid < 256 && shared_contributions[tid] > 0.0f) {
        int node_id = shared_node_ids[tid];
        float contribution_sum = shared_contributions[tid];

        // PageRank formula: newRank = (1-d)/N + d * sum(contributions)
        constant float damping_factor = 0.85f;
        constant int total_nodes = 1000; // From config
        float new_rank = (1.0f - damping_factor) / total_nodes +
                         damping_factor * contribution_sum;

        // Calculate delta
        float old_rank = 0.0f; // Read from previous rank storage
        float delta = abs(new_rank - old_rank);

        // Create result
        rank_aggregation_result result;
        result.node_id = node_id;
        result.new_rank = new_rank;
        result.delta = delta;
        result.iteration = 0; // From message

        // Serialize and enqueue
        char output_buffer[512];
        int bytes = serialize_rank_aggregation_result(&result, (device uchar*)output_buffer, 512);
        if (bytes > 0) {
            output_queue->try_enqueue(output_buffer);
        }
    }
}
```

### 2.4 ConvergenceChecker Kernel

**Key Features**:
- Checks delta against convergence threshold
- Uses atomic operations for global convergence detection
- Adaptive processing mode

**Implementation** follows similar pattern to above.

---

## Phase 3: Integration & E2E Testing

**Priority**: HIGH
**Duration**: 1-2 weeks
**Depends On**: Phase 2

### 3.1 MetalPageRankOrchestrator

**File**: `samples/RingKernels/PageRank/Metal/MetalPageRankOrchestrator.cs`

```csharp
public class MetalPageRankOrchestrator : IDisposable
{
    private readonly IMetalRingKernelRuntime _runtime;
    private readonly ILogger<MetalPageRankOrchestrator> _logger;

    // Kernel IDs
    private const string ContributionSenderKernelId = "pagerank_contribution_sender";
    private const string RankAggregatorKernelId = "pagerank_rank_aggregator";
    private const string ConvergenceCheckerKernelId = "pagerank_convergence_checker";

    public async Task<PageRankResult> ComputePageRankAsync(
        Graph graph,
        PageRankConfig config,
        CancellationToken cancellationToken = default)
    {
        // 1. Launch all 3 kernels
        await _runtime.LaunchAsync(ContributionSenderKernelId, gridSize: 1, blockSize: 256);
        await _runtime.LaunchAsync(RankAggregatorKernelId, gridSize: 1, blockSize: 256);
        await _runtime.LaunchAsync(ConvergenceCheckerKernelId, gridSize: 1, blockSize: 256);

        // 2. Enable telemetry
        await _runtime.SetTelemetryEnabledAsync(ContributionSenderKernelId, true);
        await _runtime.SetTelemetryEnabledAsync(RankAggregatorKernelId, true);
        await _runtime.SetTelemetryEnabledAsync(ConvergenceCheckerKernelId, true);

        // 3. Activate all kernels
        await _runtime.ActivateAsync(ContributionSenderKernelId);
        await _runtime.ActivateAsync(RankAggregatorKernelId);
        await _runtime.ActivateAsync(ConvergenceCheckerKernelId);

        // 4. Iteration loop
        var ranks = new float[graph.NodeCount];
        for (int iteration = 0; iteration < config.MaxIterations; iteration++)
        {
            // Send edge info messages
            foreach (var node in graph.Nodes)
            {
                var edgeInfo = new GraphEdgeInfo
                {
                    SourceNodeId = node.Id,
                    TargetNodeIds = node.Neighbors.ToArray(),
                    CurrentRank = ranks[node.Id],
                    Iteration = iteration
                };

                await _runtime.SendMessageAsync(ContributionSenderKernelId, edgeInfo);
            }

            // Wait for convergence check results
            // ... (collect NodeRankUpdate messages)

            // Check convergence
            if (allConverged) break;
        }

        // 5. Deactivate and collect telemetry
        await _runtime.DeactivateAsync(ContributionSenderKernelId);
        await _runtime.DeactivateAsync(RankAggregatorKernelId);
        await _runtime.DeactivateAsync(ConvergenceCheckerKernelId);

        var telemetry = await CollectTelemetryAsync();

        return new PageRankResult
        {
            Ranks = ranks,
            Iterations = iteration,
            Telemetry = telemetry
        };
    }
}
```

### 3.2 Unit Tests

Create tests for each kernel in isolation:

```csharp
[SkippableFact]
public async Task ContributionSender_WithValidEdges_SendsCorrectContributions()
{
    Skip.IfNot(MetalNative.IsMetalSupported(), "Metal not supported");

    // Arrange
    var runtime = CreateRuntime();
    await runtime.LaunchAsync("pagerank_contribution_sender", 1, 256);
    await runtime.ActivateAsync("pagerank_contribution_sender");

    var edgeInfo = new GraphEdgeInfo
    {
        SourceNodeId = 0,
        TargetNodeIds = new[] { 1, 2, 3 },
        CurrentRank = 1.0f,
        Iteration = 0
    };

    // Act
    await runtime.SendMessageAsync("pagerank_contribution_sender", edgeInfo);

    // Wait for processing
    await Task.Delay(100);

    // Assert - check telemetry
    var telemetry = await runtime.GetTelemetryAsync("pagerank_contribution_sender");
    telemetry.MessagesProcessed.Should().Be(1);

    // Could also check output queue for PageRankContribution messages
}
```

### 3.3 E2E Integration Tests

Test complete workflow with small graphs:

```csharp
[SkippableFact]
public async Task PageRank_SmallGraph_ConvergesCorrectly()
{
    Skip.IfNot(MetalNative.IsMetalSupported(), "Metal not supported");

    // Arrange - Create simple 4-node graph
    var graph = new Graph
    {
        Nodes = new[]
        {
            new Node { Id = 0, Neighbors = new[] { 1, 2 } },
            new Node { Id = 1, Neighbors = new[] { 2 } },
            new Node { Id = 2, Neighbors = new[] { 0 } },
            new Node { Id = 3, Neighbors = new[] { 0, 1, 2 } }
        }
    };

    var config = new PageRankConfig
    {
        DampingFactor = 0.85f,
        MaxIterations = 100,
        ConvergenceThreshold = 0.0001f
    };

    var orchestrator = new MetalPageRankOrchestrator(_runtime, _logger);

    // Act
    var result = await orchestrator.ComputePageRankAsync(graph, config);

    // Assert
    result.Ranks.Sum().Should().BeApproximately(4.0f, 0.01f);
    result.Ranks[2].Should().BeGreaterThan(result.Ranks[0]);
    result.Iterations.Should().BeLessThan(100);
}
```

---

## Phase 4: Benchmarking & Performance

**Priority**: MEDIUM
**Duration**: 1 week
**Depends On**: Phase 3

### 4.1 CPU Baseline Implementation

**File**: `benchmarks/PageRank/CpuPageRankBenchmark.cs`

```csharp
public class CpuPageRankBenchmark
{
    [Benchmark]
    [ArgumentsSource(nameof(GraphSizes))]
    public float[] CPU_Sequential(int nodeCount)
    {
        var graph = GenerateRandomGraph(nodeCount);
        var ranks = new float[nodeCount];
        Array.Fill(ranks, 1.0f);

        var newRanks = new float[nodeCount];

        for (int iter = 0; iter < 100; iter++)
        {
            // Reset
            Array.Fill(newRanks, (1.0f - 0.85f));

            // Accumulate contributions
            for (int source = 0; source < nodeCount; source++)
            {
                var contribution = 0.85f * ranks[source] / graph[source].Length;
                foreach (var target in graph[source])
                {
                    newRanks[target] += contribution;
                }
            }

            // Swap
            (ranks, newRanks) = (newRanks, ranks);
        }

        return ranks;
    }

    public IEnumerable<object[]> GraphSizes()
    {
        yield return new object[] { 100 };
        yield return new object[] { 1000 };
        yield return new object[] { 10000 };
        yield return new object[] { 100000 };
    }
}
```

### 4.2 GPU Batch Processing

**File**: `benchmarks/PageRank/MetalBatchPageRankBenchmark.cs`

```csharp
[Benchmark]
[ArgumentsSource(nameof(GraphSizes))]
public async Task<float[]> Metal_Batch(int nodeCount)
{
    var graph = GenerateRandomGraph(nodeCount);
    var accelerator = new MetalAccelerator();

    // Compile kernel for parallel rank update
    var kernel = await accelerator.CompileKernelAsync("pagerank_batch_update");

    var ranks = new float[nodeCount];
    Array.Fill(ranks, 1.0f);

    for (int iter = 0; iter < 100; iter++)
    {
        // Launch kernel for each iteration
        await kernel.ExecuteAsync(
            gridSize: (nodeCount + 255) / 256,
            blockSize: 256,
            ranks, graph, nodeCount);
    }

    return ranks;
}
```

### 4.3 GPU Native Actor

**File**: `benchmarks/PageRank/MetalNativeActorPageRankBenchmark.cs`

```csharp
[Benchmark]
[ArgumentsSource(nameof(GraphSizes))]
public async Task<float[]> Metal_NativeActor(int nodeCount)
{
    var graph = GenerateRandomGraph(nodeCount);
    var orchestrator = new MetalPageRankOrchestrator(_runtime, _logger);

    var config = new PageRankConfig
    {
        DampingFactor = 0.85f,
        MaxIterations = 100,
        ConvergenceThreshold = 0.0001f
    };

    // Use persistent ring kernels with K2K messaging
    var result = await orchestrator.ComputePageRankAsync(graph, config);

    return result.Ranks;
}
```

### 4.4 Comprehensive Comparison Suite

**File**: `tests/Performance/DotCompute.PageRank.Benchmarks/PageRankPerformanceComparison.cs`

```csharp
public class PageRankPerformanceComparison
{
    [Fact]
    public async Task GeneratePerformanceReport()
    {
        var report = new StringBuilder();
        report.AppendLine("PageRank Performance Comparison Report");
        report.AppendLine("=" .PadRight(80, '='));
        report.AppendLine();

        var graphSizes = new[] { 100, 1000, 10000, 100000 };

        foreach (var size in graphSizes)
        {
            report.AppendLine($"Graph Size: {size} nodes");
            report.AppendLine("-".PadRight(80, '-'));

            // CPU Baseline
            var cpuTime = await MeasureAsync(() => RunCpuPageRank(size));
            report.AppendLine($"  CPU Sequential:    {cpuTime.TotalMilliseconds:F2} ms");

            // GPU Batch
            var batchTime = await MeasureAsync(() => RunBatchGpuPageRank(size));
            var batchSpeedup = cpuTime.TotalMilliseconds / batchTime.TotalMilliseconds;
            report.AppendLine($"  GPU Batch:         {batchTime.TotalMilliseconds:F2} ms ({batchSpeedup:F2}x speedup)");

            // GPU Native Actor
            var actorTime = await MeasureAsync(() => RunNativeActorPageRank(size));
            var actorSpeedup = cpuTime.TotalMilliseconds / actorTime.TotalMilliseconds;
            report.AppendLine($"  GPU Native Actor:  {actorTime.TotalMilliseconds:F2} ms ({actorSpeedup:F2}x speedup)");

            report.AppendLine();
        }

        // Write report to file
        File.WriteAllText("pagerank_performance_report.txt", report.ToString());
        _output.WriteLine(report.ToString());
    }
}
```

---

## Phase 5: Fix Outstanding TODOs

**Priority**: LOW (can be done in parallel)
**Duration**: 3-5 days

### 13 TODOs in Metal Backend

1. **MetalKernelCache.cs**: Get Metal version from device capabilities
2. **MetalMemoryManager.cs**: Implement pool hit rate tracking
3. **MetalUnifiedMemoryOptimizer.cs**: Cleanup resources when fully implemented
4. **MetalMemoryOrderingProvider.cs**: Integration with kernel compiler (Phase 3)
5. **MetalMultiKernelBarrier.cs** (3x): Execute multi-kernel barrier Metal kernels
6. **MetalRingKernelCompiler.cs** (2x): Process message based on kernel logic (DONE in Phase 1)
7. **MetalOperationDescriptor.cs** (2x): Extract from other files
8. **MetalKernelParameterBinder.cs**: Add support for unified and pooled buffers

---

## Phase 6: Documentation & Samples

**Priority**: LOW
**Duration**: 1 week
**Depends On**: All phases

### Documentation Files

1. **Implementation Guide** (this document): ✅ Complete
2. **API Documentation**: Update XML docs for all new public APIs
3. **Performance Characteristics**: Document benchmarks and optimization tips
4. **Known Limitations**: Metal watchdog timer, grid barriers, etc.

### Sample Application

**File**: `samples/RingKernels/PageRank/Metal/PageRankMetalSample.cs`

Complete working example with:
- Graph loading from file
- Results visualization
- Benchmark comparison tool
- Web crawl simulation

---

## Testing Strategy

### Test Pyramid

```
     /\
    /  \     E2E Tests (5-10 tests)
   /____\    - Full PageRank workflow
  /      \   - Multi-graph validation
 /________\  Integration Tests (20-30 tests)
/          \ - 3-kernel coordination
/____________\ - K2K messaging
              Unit Tests (100+ tests)
              - Per-kernel logic
              - MSL generation
              - Serialization
```

### Test Categories

1. **Unit Tests** (src tests)
   - MSL code generation
   - MemoryPack serialization
   - Message queue operations
   - Barrier synchronization

2. **Integration Tests** (integration tests)
   - Multi-kernel coordination
   - K2K message routing
   - Telemetry collection
   - Lifecycle management

3. **Hardware Tests** (Metal-specific)
   - Actual GPU execution
   - Performance validation
   - Resource usage
   - Memory management

4. **Performance Tests** (benchmarks)
   - CPU baseline
   - GPU batch
   - GPU native actor
   - Comparison metrics

---

## Performance Targets

### Ring Kernel Infrastructure

| Metric | Target | Measured | Status |
|--------|--------|----------|--------|
| Kernel launch latency | <5ms | TBD | ⏳ |
| K2K message throughput | >1M msgs/sec | TBD | ⏳ |
| Multi-kernel coordination overhead | <100μs | TBD | ⏳ |
| Memory allocation overhead | <1ms | TBD | ⏳ |

### PageRank Specific

| Graph Size | CPU Time | GPU Batch Target | GPU Actor Target |
|------------|----------|------------------|------------------|
| 100 nodes | baseline | 0.5-1x (overhead dominates) | 0.5-1x |
| 1K nodes | baseline | 2-5x faster | 3-7x faster |
| 10K nodes | baseline | 5-15x faster | 10-30x faster |
| 100K nodes | baseline | 10-30x faster | 30-100x faster |

**Note**: GPU becomes faster as graph size increases due to better parallelization.

---

## Known Limitations

### 1. Metal Watchdog Timer

**Issue**: macOS Metal has a GPU watchdog that kills kernels running >10 seconds
**Workaround**: Use event-driven mode instead of persistent mode for long-running computations
**Alternative**: Break computation into smaller batches

### 2. Grid-Level Barriers

**Issue**: Metal lacks CUDA's grid-wide barriers (`cooperative_groups::grid_group`)
**Workaround**: Use multiple kernel dispatches with CPU-side synchronization
**Impact**: PageRank needs careful iteration management

### 3. Shared Memory Limitations

**Issue**: Metal has 32KB threadgroup memory (vs CUDA's 48KB)
**Workaround**: Use smaller batch sizes for aggregation
**Impact**: RankAggregator may need multiple passes for large graphs

### 4. Memory Model Differences

**Issue**: Metal's memory ordering is slightly different from CUDA
**Workaround**: Explicit memory fences (`threadgroup_barrier(mem_flags::mem_device)`)
**Impact**: Requires careful barrier placement

### 5. Debugging Challenges

**Issue**: Metal Shader Debugger not as mature as CUDA debuggers
**Workaround**: Extensive logging and telemetry
**Tool**: Xcode Metal Frame Capture

---

## Troubleshooting

### Common Issues

#### 1. "Failed to compile MSL to Metal library"

**Cause**: Syntax error in generated MSL code
**Fix**:
- Check MSL generation logs
- Validate struct definitions
- Ensure all types are Metal-compatible

**Debug**:
```bash
# Save generated MSL to file
echo "$mslSource" > /tmp/debug_kernel.metal

# Try compiling manually
xcrun -sdk macosx metal -c /tmp/debug_kernel.metal -o /tmp/debug_kernel.air
```

#### 2. "Kernel timeout after 10 seconds"

**Cause**: Metal watchdog timer
**Fix**: Switch to event-driven mode or batch processing

#### 3. "K2K messages not arriving"

**Cause**: Routing table misconfiguration
**Fix**:
- Verify `SubscribesToKernel` and `PublishesToKernel` attributes
- Check routing table initialization
- Enable K2K routing debug logs

#### 4. "Serialization buffer overflow"

**Cause**: Message size exceeds buffer
**Fix**:
- Increase buffer size (currently 512 bytes)
- Split large messages
- Use chunked serialization

#### 5. "Test host crash during cleanup"

**Cause**: Known Metal GPU cleanup issue (cosmetic)
**Status**: Non-blocking, all tests pass before crash
**Fix**: Ignore crash in test output, tests succeeded

---

## Next Steps

### Immediate (Week 1-2)

1. ✅ Create this implementation guide
2. 🔄 Implement Phase 1.1 MSL generation enhancements
3. ⏳ Test enhanced MSL generation with simple kernels
4. ⏳ Create unit tests for MSL generation

### Short Term (Week 3-4)

5. ⏳ Implement Phase 1.2-1.4 (barriers, K2K routing)
6. ⏳ Start Phase 2.1-2.2 (PageRank messages and ContributionSender)
7. ⏳ Test ContributionSender in isolation

### Medium Term (Week 5-8)

8. ⏳ Complete Phase 2.3-2.4 (RankAggregator, ConvergenceChecker)
9. ⏳ Phase 3 integration and E2E tests
10. ⏳ Phase 4 benchmarking

### Long Term (Week 9+)

11. ⏳ Phase 5 TODO cleanup
12. ⏳ Phase 6 documentation and samples
13. ⏳ Performance optimization
14. ⏳ Production deployment

---

## Conclusion

This implementation guide provides a comprehensive roadmap for implementing Metal PageRank with GPU native actors. The project is structured in 6 phases with clear dependencies, testing strategies, and performance targets.

**Key Success Factors**:
- Phase 1 is CRITICAL - everything depends on proper MSL generation
- Test incrementally - don't wait until end
- Use CPU simulation to validate correctness before GPU execution
- Monitor performance at each phase
- Document issues and workarounds as you encounter them

**Estimated Timeline**: 6-9 weeks for full implementation

**Current Status**: Phase 1 implementation in progress

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-01-24 | 1.0 | Initial comprehensive implementation guide created |

---

## References

- [Metal Shading Language Specification](https://developer.apple.com/metal/Metal-Shading-Language-Specification.pdf)
- [MemoryPack Binary Format](https://github.com/Cysharp/MemoryPack)
- [PageRank Algorithm](https://en.wikipedia.org/wiki/PageRank)
- [CUDA Ring Kernel Reference](../../samples/RingKernels/PageRank/PageRankKernels.cs)

---

**End of Implementation Guide**
