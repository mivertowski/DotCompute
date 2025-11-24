# PageRank: Standard vs Distributed Actor (GPU Native) Approach

This document compares the traditional PageRank algorithm implementation with the distributed actor model using GPU-native Ring Kernels in DotCompute.

## Overview

PageRank is an algorithm used to rank nodes in a graph by their importance, originally developed by Google to rank web pages. The algorithm iteratively computes rank scores based on the link structure between nodes.

---

## Standard PageRank Implementation

### Algorithm Description

The standard PageRank algorithm follows an iterative approach:

```
PR(u) = (1-d)/N + d * Σ PR(v)/L(v)
```

Where:
- `PR(u)` = PageRank of page u
- `d` = damping factor (typically 0.85)
- `N` = total number of pages
- `PR(v)` = PageRank of pages linking to u
- `L(v)` = number of outbound links from page v

### Data Input Format

**Standard approach uses dense matrix or CSR (Compressed Sparse Row) format:**

```csharp
// Dense adjacency matrix (small graphs)
float[,] adjacencyMatrix = new float[N, N];

// CSR format (large sparse graphs)
int[] rowPointers;     // Start index of each row's edges
int[] columnIndices;   // Destination node for each edge
float[] values;        // Edge weights (usually 1/outDegree)
```

### Execution Flow

```
┌─────────────────────────────────────────────────────────────┐
│                    Standard PageRank                        │
├─────────────────────────────────────────────────────────────┤
│  1. Load entire graph into memory                           │
│  2. Initialize ranks: PR[i] = 1/N for all nodes             │
│  3. For each iteration until convergence:                   │
│     a. For each node u:                                     │
│        - Sum contributions from all incoming edges          │
│        - Apply damping factor                               │
│        - Store new rank                                     │
│     b. Check convergence (||PR_new - PR_old|| < epsilon)    │
│  4. Return final ranks                                      │
└─────────────────────────────────────────────────────────────┘
```

### GPU Implementation (Traditional)

```cuda
// Traditional GPU kernel - processes all nodes per iteration
__global__ void pagerank_iteration(
    float* ranks_in,
    float* ranks_out,
    int* row_ptrs,
    int* col_indices,
    float* values,
    int num_nodes,
    float damping)
{
    int node = blockIdx.x * blockDim.x + threadIdx.x;
    if (node >= num_nodes) return;

    float sum = 0.0f;
    for (int e = row_ptrs[node]; e < row_ptrs[node + 1]; e++) {
        int src = col_indices[e];
        sum += values[e] * ranks_in[src];
    }
    ranks_out[node] = (1.0f - damping) / num_nodes + damping * sum;
}
```

---

## Distributed Actor (GPU Native) Approach

### Architecture Overview

The distributed actor model decomposes PageRank into independent, message-passing actors (Ring Kernels) that run persistently on the GPU:

```
┌───────────────────────────────────────────────────────────────────┐
│               Distributed Actor Ring Kernel System                │
├───────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────┐    ┌──────────────────┐    ┌──────────────┐ │
│  │  Contribution   │───▶│  Rank            │───▶│ Convergence  │ │
│  │  Sender         │    │  Aggregator      │    │ Checker      │ │
│  │  (Producer)     │    │  (Consumer/Prod) │    │ (Consumer)   │ │
│  └─────────────────┘    └──────────────────┘    └──────────────┘ │
│         │                       │                      │         │
│         │    K2K Messages       │   K2K Messages       │         │
│         └───────────────────────┴──────────────────────┘         │
│                                                                   │
│  Each kernel runs PERSISTENTLY on GPU, processing messages        │
│  asynchronously without CPU intervention                          │
└───────────────────────────────────────────────────────────────────┘
```

### Data Input Format

**Actor model uses message-based edge representation:**

```csharp
// Edge information as discrete messages
[MemoryPackable]
public partial struct GraphEdgeInfo : IRingKernelMessage
{
    public int SourceNodeId { get; set; }
    public int TargetNodeId { get; set; }
    public float ContributionWeight { get; set; }  // 1/outDegree
    public int Iteration { get; set; }
}

// Rank update messages between kernels
[MemoryPackable]
public partial struct RankContribution : IRingKernelMessage
{
    public int TargetNodeId { get; set; }
    public float ContributionValue { get; set; }
    public int SourceIteration { get; set; }
}
```

### Ring Kernel Definitions

```csharp
// Kernel 1: Sends rank contributions along outgoing edges
[RingKernel("pagerank_contribution_sender")]
[InputMessage(typeof(GraphEdgeInfo))]
[OutputMessage(typeof(RankContribution))]
[PublishesTo("pagerank_rank_aggregator")]
public static class ContributionSenderKernel
{
    public static void Process(
        RingKernelContext ctx,
        GraphEdgeInfo edge,
        out RankContribution contribution)
    {
        float currentRank = ctx.GetNodeRank(edge.SourceNodeId);
        contribution = new RankContribution
        {
            TargetNodeId = edge.TargetNodeId,
            ContributionValue = currentRank * edge.ContributionWeight,
            SourceIteration = edge.Iteration
        };
    }
}

// Kernel 2: Aggregates contributions and updates ranks
[RingKernel("pagerank_rank_aggregator")]
[InputMessage(typeof(RankContribution))]
[OutputMessage(typeof(ConvergenceCheck))]
[SubscribesTo("pagerank_contribution_sender")]
[PublishesTo("pagerank_convergence_checker")]
public static class RankAggregatorKernel
{
    public static void Process(
        RingKernelContext ctx,
        RankContribution contribution,
        out ConvergenceCheck check)
    {
        // Atomically aggregate contribution
        float oldRank = ctx.GetNodeRank(contribution.TargetNodeId);
        float newRank = ctx.AtomicAdd(
            contribution.TargetNodeId,
            contribution.ContributionValue);

        check = new ConvergenceCheck
        {
            NodeId = contribution.TargetNodeId,
            RankDelta = Math.Abs(newRank - oldRank),
            Iteration = contribution.SourceIteration
        };
    }
}
```

### Execution Flow

```
┌─────────────────────────────────────────────────────────────────┐
│               Distributed Actor Execution Flow                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  SETUP PHASE (Once):                                            │
│  1. Launch Ring Kernels (persistent GPU threads)                │
│  2. Initialize node ranks in shared GPU memory                  │
│  3. Activate kernels for message processing                     │
│                                                                 │
│  EXECUTION PHASE (Continuous):                                  │
│  4. Stream edge messages to ContributionSender                  │
│     - CPU sends edges asynchronously                            │
│     - GPU processes immediately without sync                    │
│                                                                 │
│  5. K2K Message Flow (GPU-only, no CPU involvement):            │
│     ContributionSender ──▶ RankAggregator ──▶ ConvergenceChecker│
│                                                                 │
│  6. Monitor convergence via telemetry (zero-copy polling)       │
│                                                                 │
│  TEARDOWN PHASE:                                                │
│  7. Deactivate and terminate kernels                            │
│  8. Read final ranks from GPU memory                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Comparison: Pros and Cons

### Standard PageRank

| Aspect | Pros | Cons |
|--------|------|------|
| **Simplicity** | Simple to implement and understand | Limited to batch processing |
| **Memory** | Efficient CSR format for sparse graphs | Must load entire graph before processing |
| **Debugging** | Easy to trace execution step-by-step | N/A |
| **Convergence** | Global sync ensures consistent iterations | CPU-GPU sync overhead each iteration |
| **Latency** | N/A | High latency for streaming updates |
| **Scaling** | Works well for static graphs | Poor for dynamic graph updates |

### Distributed Actor (GPU Native)

| Aspect | Pros | Cons |
|--------|------|------|
| **Streaming** | Process edges as they arrive | More complex programming model |
| **Latency** | Sub-millisecond message processing | Initial kernel launch overhead |
| **CPU Offload** | GPU processes autonomously | Debugging more challenging |
| **Dynamic Graphs** | Natural support for edge insertions/deletions | Memory overhead for message queues |
| **Scalability** | Linear scaling with GPU count | Requires careful load balancing |
| **Throughput** | Higher sustained throughput | Convergence detection more complex |

---

## Performance Characteristics

### Standard Approach

```
Iteration Time = O(E) GPU work + O(1) CPU-GPU sync
Total Time = Iterations * (GPU_kernel + CPU_sync + Launch_overhead)

Typical per-iteration:
- Kernel launch: ~5-10μs
- GPU execution: ~100μs - 10ms (depends on graph size)
- Sync: ~10-50μs
- Total: ~115μs - 10ms per iteration
```

### Distributed Actor Approach

```
Setup Time = Kernel_launch + Memory_allocation (one-time)
Processing Time = Message_rate * Processing_latency (continuous)

Typical characteristics:
- Initial launch: ~50-100ms (PTX compilation + kernel start)
- Message latency: ~1-5μs per message (GPU-native)
- Throughput: 10M+ messages/second sustained
- Zero CPU intervention during processing
```

---

## Use Case Recommendations

### Use Standard PageRank When:

1. **Static graphs** - Graph structure doesn't change during computation
2. **Batch processing** - All edges known upfront
3. **Simple deployment** - Need straightforward implementation
4. **Debugging priority** - Requires easy step-through debugging
5. **Small to medium graphs** - < 10M nodes, < 100M edges

### Use Distributed Actor When:

1. **Streaming data** - Edges arrive continuously
2. **Dynamic graphs** - Frequent edge insertions/deletions
3. **Real-time requirements** - Sub-millisecond update latency needed
4. **High throughput** - Sustained millions of updates per second
5. **Large-scale graphs** - 100M+ nodes, billions of edges
6. **Multi-GPU deployment** - Need to scale across GPU cluster

---

## Code Examples

### Standard PageRank (DotCompute)

```csharp
// Traditional batch approach
public async Task<float[]> ComputePageRank(
    SparseGraph graph,
    int maxIterations = 100,
    float epsilon = 1e-6f)
{
    var accelerator = _orchestrator.GetAccelerator(BackendType.CUDA);

    // Allocate buffers
    using var ranks = accelerator.Allocate<float>(graph.NodeCount);
    using var newRanks = accelerator.Allocate<float>(graph.NodeCount);
    using var rowPtrs = accelerator.Allocate<int>(graph.RowPointers);
    using var colIdx = accelerator.Allocate<int>(graph.ColumnIndices);

    // Initialize ranks
    ranks.Fill(1.0f / graph.NodeCount);

    // Iterate until convergence
    for (int iter = 0; iter < maxIterations; iter++)
    {
        // Launch kernel (blocking)
        await accelerator.LaunchAsync(
            "pagerank_iteration",
            gridSize: (graph.NodeCount + 255) / 256,
            blockSize: 256,
            ranks, newRanks, rowPtrs, colIdx, graph.NodeCount, 0.85f);

        // Check convergence (requires CPU-GPU sync)
        float delta = await ComputeDelta(ranks, newRanks);
        if (delta < epsilon) break;

        // Swap buffers
        (ranks, newRanks) = (newRanks, ranks);
    }

    return await ranks.CopyToHostAsync();
}
```

### Distributed Actor PageRank (DotCompute Ring Kernels)

```csharp
// Streaming actor approach
public async Task<IAsyncEnumerable<RankUpdate>> ComputePageRankStreaming(
    IAsyncEnumerable<GraphEdge> edgeStream,
    CancellationToken cancellationToken)
{
    // Launch persistent kernels (one-time setup)
    await _runtime.LaunchAsync("pagerank_contribution_sender", gridSize: 4, blockSize: 256);
    await _runtime.LaunchAsync("pagerank_rank_aggregator", gridSize: 4, blockSize: 256);
    await _runtime.LaunchAsync("pagerank_convergence_checker", gridSize: 1, blockSize: 256);

    // Activate for processing
    await _runtime.ActivateAsync("pagerank_contribution_sender");
    await _runtime.ActivateAsync("pagerank_rank_aggregator");
    await _runtime.ActivateAsync("pagerank_convergence_checker");

    // Stream edges to GPU (non-blocking)
    var sendTask = StreamEdgesToGpu(edgeStream, cancellationToken);

    // Poll for rank updates (zero-copy)
    await foreach (var update in PollRankUpdates(cancellationToken))
    {
        yield return update;
    }

    // Cleanup
    await _runtime.DeactivateAsync("pagerank_contribution_sender");
    await _runtime.TerminateAsync("pagerank_contribution_sender");
    // ... terminate other kernels
}

private async Task StreamEdgesToGpu(
    IAsyncEnumerable<GraphEdge> edges,
    CancellationToken ct)
{
    await foreach (var edge in edges.WithCancellation(ct))
    {
        // Send edge as message - GPU processes immediately
        await _runtime.SendMessageAsync("pagerank_contribution_sender",
            new GraphEdgeInfo
            {
                SourceNodeId = edge.Source,
                TargetNodeId = edge.Target,
                ContributionWeight = 1.0f / edge.SourceOutDegree
            });
    }
}
```

---

## Memory Layout Comparison

### Standard Approach

```
GPU Memory Layout:
┌──────────────────────────────────────────────────┐
│ ranks[]        - N floats (node ranks)           │
│ newRanks[]     - N floats (temporary)            │
│ rowPointers[]  - (N+1) ints (CSR structure)      │
│ columnIndices[]- E ints (edge destinations)      │
│ values[]       - E floats (edge weights)         │
└──────────────────────────────────────────────────┘
Total: 2N*4 + (N+1)*4 + 2E*4 = 12N + 8E + 4 bytes
```

### Distributed Actor Approach

```
GPU Memory Layout:
┌──────────────────────────────────────────────────┐
│ Control Blocks    - 64 bytes per kernel          │
│ Message Queues    - Configurable (e.g., 1MB each)│
│ Shared Rank State - N floats (atomic access)     │
│ Telemetry Buffers - 64 bytes per kernel          │
│ K2K Channels      - Variable (inter-kernel msgs) │
└──────────────────────────────────────────────────┘
Total: Per-kernel overhead + N*4 + queue capacity
```

---

## Conclusion

The choice between standard and distributed actor approaches depends on your specific requirements:

- **Standard PageRank** is ideal for batch processing of static graphs where simplicity and debuggability are priorities.

- **Distributed Actor (GPU Native)** excels in streaming scenarios, dynamic graphs, and real-time applications where low latency and high throughput are critical.

DotCompute's Ring Kernel system enables the distributed actor pattern while maintaining the performance benefits of native GPU execution, making it possible to achieve both the programming model benefits of actors and the computational efficiency of GPU acceleration.
