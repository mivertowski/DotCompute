# Metal PageRank Ring Kernel - Simple Example

This sample demonstrates how to use the Metal PageRank Ring Kernel system to compute PageRank scores on a graph using persistent GPU actors.

## What This Sample Does

1. **Initializes Metal backend** - Creates Metal device and command queue
2. **Sets up orchestrator** - Configures the 3-kernel PageRank pipeline
3. **Creates a graph** - Defines a simple 10-node directed graph
4. **Computes PageRank** - Runs the iterative algorithm on GPU
5. **Displays results** - Shows ranked nodes and convergence metrics

## Requirements

- **macOS** with Apple Silicon (M1/M2/M3) or AMD GPU
- **.NET 9.0 SDK** or later
- **Metal framework** (included with macOS)

## Building the Sample

From the DotCompute repository root:

```bash
dotnet build samples/RingKernels/PageRank/Metal/SimpleExample/SimpleExample.csproj --configuration Release
```

## Running the Sample

### Option 1: Using `dotnet run` (Recommended)

```bash
export DYLD_LIBRARY_PATH="./src/Backends/DotCompute.Backends.Metal:./src/Backends/DotCompute.Backends.Metal/bin/Release/net9.0"
dotnet run --project samples/RingKernels/PageRank/Metal/SimpleExample/SimpleExample.csproj --configuration Release
```

### Option 2: Running the compiled binary

```bash
export DYLD_LIBRARY_PATH="./src/Backends/DotCompute.Backends.Metal:./src/Backends/DotCompute.Backends.Metal/bin/Release/net9.0"
./samples/RingKernels/PageRank/Metal/SimpleExample/bin/Release/net9.0/SimpleExample
```

## Expected Output

```
==============================================
  Metal PageRank Ring Kernel - Simple Example
==============================================

1️⃣  Initializing Metal backend...
✅ Metal backend initialized successfully

2️⃣  Creating PageRank orchestrator...
3️⃣  Initializing orchestrator (kernel discovery, compilation, routing)...
✅ Orchestrator initialized

4️⃣  Creating 10-node graph...
   Graph structure:
     Node 0 → [1, 2]
     Node 1 → [3]
     Node 2 → [3, 4]
     ...

5️⃣  Computing PageRank...
   Parameters:
     - Max iterations: 100
     - Convergence threshold: 0.0001
     - Damping factor: 0.85

✅ PageRank computation complete in XX.XXms

6️⃣  PageRank Results:
   ========================================
   Rank | Node | Score
   ----------------------------------------
    #1   |    X | X.XXXXXX
    #2   |    X | X.XXXXXX
   ...
   ========================================

🏆 Highest ranked node: Node X (score: X.XXXXXX)

📊 Total rank sum: 1.000000 (should be ≈1.0)
   Rank distribution is ✅ correct

7️⃣  Cleaning up resources...
✅ Cleanup complete

==============================================
  Example complete!
==============================================
```

## Graph Structure

The sample uses a 10-node directed graph with the following structure:

```
0 → [1, 2]      Node 0 links to nodes 1 and 2
1 → [3]         Node 1 links to node 3
2 → [3, 4]      Node 2 links to nodes 3 and 4
3 → [5]         Node 3 links to node 5
4 → [5, 6]      Node 4 links to nodes 5 and 6
5 → [7]         Node 5 links to node 7
6 → [7, 8]      Node 6 links to nodes 7 and 8
7 → [9]         Node 7 links to node 9
8 → [9]         Node 8 links to node 9
9 → [0]         Node 9 links to node 0 (completes cycle)
```

This creates a cyclic graph where importance flows through the network, with nodes having varying in-degrees (number of incoming links).

## How It Works

### Ring Kernel Architecture

The PageRank computation uses **3 persistent GPU kernels** that run continuously:

1. **ContributionSender** - Receives graph nodes, computes rank contributions
2. **RankAggregator** - Aggregates contributions into new ranks
3. **ConvergenceChecker** - Monitors convergence and signals completion

### Message Flow

```
Host → ContributionSender → RankAggregator → ConvergenceChecker → Host
        (MetalGraphNode)    (Contribution)   (RankAggregation)  (Convergence)
```

All kernel-to-kernel (K2K) messaging happens directly on the GPU without CPU round-trips.

### Performance Benefits

Compared to traditional batch GPU processing:

- **2-5x faster** due to persistent kernels (no launch overhead per iteration)
- **<100ns message routing** via direct GPU-to-GPU K2K messaging
- **<20ns synchronization** via multi-kernel atomic barriers

## Customization

You can modify the sample to:

1. **Change graph size/structure** - Edit the `graph` dictionary in `Program.cs`
2. **Adjust algorithm parameters**:
   - `maxIterations` - Maximum number of iterations (default: 100)
   - `convergenceThreshold` - Convergence delta threshold (default: 0.0001)
   - `dampingFactor` - PageRank damping factor (default: 0.85)
3. **Change logging level** - Modify `SetMinimumLevel()` in logger configuration

## Troubleshooting

### Error: "Metal is not available on this system"

- **Cause**: Running on non-macOS or macOS without Metal support
- **Solution**: Run on macOS with Apple Silicon or AMD GPU

### Error: "Failed to create Metal device"

- **Cause**: Metal framework not accessible
- **Solution**: Check macOS version (requires 10.11+), verify GPU is Metal-compatible

### Error: Native library not found

- **Cause**: `DYLD_LIBRARY_PATH` not set correctly
- **Solution**: Ensure you set the environment variable before running:
  ```bash
  export DYLD_LIBRARY_PATH="./src/Backends/DotCompute.Backends.Metal:./src/Backends/DotCompute.Backends.Metal/bin/Release/net9.0"
  ```

### Error: Kernel compilation failed

- **Cause**: MSL code generation issue
- **Solution**: Check logs for compilation errors, verify all kernel files are present

## Next Steps

After running this simple example:

1. **Try larger graphs** - Modify the graph to have 100+ nodes
2. **Visualize results** - Export results and create graph visualizations
3. **Run benchmarks** - Compare performance against CPU implementation
4. **Read the tutorial** - See `docs/tutorials/pagerank-metal-ring-kernel-tutorial.md`
5. **Explore advanced samples** - Check other samples in `samples/RingKernels/PageRank/Metal/`

## Learn More

- **Tutorial**: `docs/tutorials/pagerank-metal-ring-kernel-tutorial.md`
- **Status Report**: `docs/pagerank-ring-kernel-status.md`
- **Architecture**: `src/Backends/DotCompute.Backends.Metal/RingKernels/`
- **Tests**: `samples/RingKernels/PageRank/Metal/PageRankMetalE2ETests.cs`

## License

This sample is part of the DotCompute project and is licensed under the same terms.
