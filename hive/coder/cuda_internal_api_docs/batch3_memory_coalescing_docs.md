# CudaMemoryCoalescingAnalyzer.cs - Comprehensive API Documentation

**File Location**: `/src/Backends/DotCompute.Backends.CUDA/Analysis/CudaMemoryCoalescingAnalyzer.cs`
**Purpose**: Production-grade CUDA memory coalescing analyzer for identifying and optimizing memory access patterns
**Namespace**: `DotCompute.Backends.CUDA.Analysis`

## Overview

The `CudaMemoryCoalescingAnalyzer` provides comprehensive analysis of CUDA memory access patterns to maximize bandwidth utilization through coalescing optimization. Memory coalescing is critical for GPU performance - uncoalesced access can result in up to 32x bandwidth overhead.

### Key Features
- **Architecture-Aware Analysis**: Adapts to compute capabilities (Fermi, Kepler/Maxwell, Pascal+)
- **Access Pattern Detection**: Identifies strided, random, and 2D matrix access patterns
- **Efficiency Calculation**: Quantifies coalescing efficiency and wasted bandwidth
- **Runtime Profiling**: Measures actual memory transfer performance during execution
- **Comparative Analysis**: Compares multiple access patterns to identify optimization opportunities

### Memory Coalescing Fundamentals

**Coalesced Access**: When threads in a warp access consecutive memory addresses, the GPU hardware combines these into a single memory transaction.

**Cache Line Sizes**:
- **Modern GPUs (CC 6.0+)**: 128-byte cache lines with 32-byte sectors
- **Kepler/Maxwell (CC 3.0-5.x)**: 128-byte cache lines
- **Fermi (CC 2.x)**: 32/64/128-byte transactions depending on access size

**Optimal Access Pattern**:
- Stride-1 access (consecutive addresses)
- Aligned to cache line boundary (128 bytes for modern GPUs)
- Element size of 4, 8, or 16 bytes

---

## Class: CudaMemoryCoalescingAnalyzer

```csharp
public sealed partial class CudaMemoryCoalescingAnalyzer
```

**Description**: Production-grade analyzer for identifying and optimizing CUDA memory access patterns to maximize bandwidth utilization through proper coalescing.

### Constructor

#### `CudaMemoryCoalescingAnalyzer(ILogger<CudaMemoryCoalescingAnalyzer> logger)`

**Description**: Initializes analyzer with logger for diagnostic messages.

**Parameters**:
- `logger` (`ILogger<CudaMemoryCoalescingAnalyzer>`): Logger instance for analysis reporting

**Exceptions**:
- `ArgumentNullException`: If logger is null

**Remarks**:
- Initializes internal cache for coalescing metrics
- Initializes access pattern storage for historical analysis

---

## Primary Analysis Methods

### `AnalyzeAccessPatternAsync` - Core Pattern Analysis

```csharp
public async Task<CoalescingAnalysis> AnalyzeAccessPatternAsync(
    MemoryAccessInfo accessInfo,
    int deviceId = 0)
```

**Description**: Analyzes memory access pattern for coalescing efficiency with architecture-specific optimizations.

**Parameters**:
- `accessInfo` (`MemoryAccessInfo`): Memory access information including:
  - `KernelName`: Identifier for the kernel
  - `BaseAddress`: Starting memory address (checked for alignment)
  - `AccessSize`: Total bytes accessed
  - `ElementSize`: Size of each element in bytes
  - `ThreadCount`: Number of threads accessing memory
  - `Stride`: Access stride (1 = consecutive, >1 = strided, 0 = broadcast)
  - `IsRandom`: True if access pattern is random/irregular
  - `ExecutionTime`: Optional execution time for bandwidth calculation
- `deviceId` (`int`, optional): CUDA device ID for compute capability detection

**Returns**: `Task<CoalescingAnalysis>` containing:
- `KernelName`: Kernel identifier
- `Timestamp`: Analysis timestamp
- `CoalescingEfficiency`: Efficiency ratio (0.0 to 1.0)
- `WastedBandwidth`: Bytes/second of wasted bandwidth
- `OptimalAccessSize`: Recommended access size for this architecture
- `TransactionCount`: Actual memory transactions required
- `IdealTransactionCount`: Minimum transactions for perfect coalescing
- `AccessPattern`: Detected pattern type (Sequential, Strided, Random, Broadcast)
- `ActualBytesTransferred`: Total bytes transferred including waste
- `UsefulBytesTransferred`: Bytes actually needed by kernel
- `Issues`: List of detected coalescing issues with severity
- `Optimizations`: Architecture-specific optimization recommendations
- `ArchitectureNotes`: GPU architecture-specific details

**Performance**: ~1-5ms for pattern analysis (no kernel execution required)

**Compute Capability Handling**:
- **CC 6.0+ (Pascal/Volta/Turing/Ampere/Ada)**: Relaxed coalescing rules, 32-byte sector granularity
- **CC 3.0-5.x (Kepler/Maxwell)**: Standard coalescing with 128-byte cache lines
- **CC 2.x (Fermi)**: Strict coalescing requirements

**Example**:
```csharp
var accessInfo = new MemoryAccessInfo
{
    KernelName = "VectorAdd",
    BaseAddress = 0x100000000, // Aligned to 128 bytes
    AccessSize = 1024 * sizeof(float),
    ElementSize = sizeof(float),
    ThreadCount = 256,
    Stride = 1,
    IsRandom = false
};

var analysis = await analyzer.AnalyzeAccessPatternAsync(accessInfo);

logger.LogInformation("Coalescing Efficiency: {0:P}", analysis.CoalescingEfficiency);
logger.LogInformation("Wasted Bandwidth: {0:F2} GB/s", analysis.WastedBandwidth / 1e9);

foreach (var issue in analysis.Issues)
{
    logger.LogWarning("{0}: {1} (Impact: {2})",
        issue.Type, issue.Description, issue.Impact);
}
```

**Efficiency Calculation**:
```
For stride-1 access with proper alignment:
  efficiency = 1.0 (perfect coalescing)

For broadcast access (stride = 0):
  efficiency = 1/32 = 0.03125 (one value replicated to all threads)

For strided access:
  efficiency = 1.0 / stride

For random access:
  efficiency = 1.0 / 32 (worst case, each thread separate transaction)
```

---

### `AnalyzeStridedAccessAsync` - Strided Pattern Analysis

```csharp
public async Task<StridedAccessAnalysis> AnalyzeStridedAccessAsync(
    int stride,
    int elementSize,
    int threadCount,
    int deviceId = 0)
```

**Description**: Analyzes strided memory access patterns common in AoS (Array of Structures) layouts.

**Parameters**:
- `stride` (`int`): Access stride (number of elements between consecutive accesses)
- `elementSize` (`int`): Size of each element in bytes
- `threadCount` (`int`): Number of threads accessing memory
- `deviceId` (`int`, optional): CUDA device ID

**Returns**: `Task<StridedAccessAnalysis>` containing:
- `Stride`: Access stride value
- `ElementSize`: Element size in bytes
- `ThreadCount`: Thread count
- `TransactionsPerWarp`: Memory transactions needed per warp (32 threads)
- `Efficiency`: Bandwidth efficiency (useful bytes / transferred bytes)
- `IsCoalesced`: True if access is fully coalesced
- `BandwidthUtilization`: Effective bandwidth usage
- `Recommendations`: Specific optimization suggestions

**Warp-Level Analysis**:
```
bytesPerWarp = warpSize (32) × elementSize × stride
transactionsPerWarp = ceil(bytesPerWarp / cacheLine)
usefulBytes = warpSize × elementSize
efficiency = usefulBytes / (transactionsPerWarp × cacheLine)
```

**Example**:
```csharp
// Analyze struct member access (typical AoS pattern)
struct Particle {
    float x, y, z;  // Position
    float vx, vy, vz;  // Velocity
};
// Accessing only 'x' component has stride = 6 (sizeof(Particle) / sizeof(float))

var analysis = await analyzer.AnalyzeStridedAccessAsync(
    stride: 6,
    elementSize: 4,  // sizeof(float)
    threadCount: 256);

// Result: efficiency ≈ 16.7% (1/6), very inefficient
// Recommendation: Convert to SoA (Structure of Arrays)
```

**Optimization Recommendations**:
- **Stride > 1**: Convert AoS to SoA layout
- **Small Stride (2-4)**: Use shared memory staging
- **Large Stride (>32)**: Each thread loads multiple non-consecutive elements

---

### `Analyze2DAccessPatternAsync` - Matrix Access Analysis

```csharp
public async Task<Matrix2DAccessAnalysis> Analyze2DAccessPatternAsync(
    int rows,
    int cols,
    Types.AccessOrder accessOrder,
    int elementSize,
    int blockDimX,
    int blockDimY,
    int deviceId = 0)
```

**Description**: Analyzes 2D memory access patterns common in matrix operations.

**Parameters**:
- `rows` (`int`): Number of matrix rows
- `cols` (`int`): Number of matrix columns
- `accessOrder` (`AccessOrder`): Row-major or column-major access order
- `elementSize` (`int`): Size of matrix elements in bytes
- `blockDimX` (`int`): Thread block X dimension
- `blockDimY` (`int`): Thread block Y dimension
- `deviceId` (`int`, optional): CUDA device ID

**Returns**: `Task<Matrix2DAccessAnalysis>` containing:
- `Rows`, `Columns`: Matrix dimensions
- `AccessOrder`: Row-major or column-major
- `IsOptimal`: True if access pattern is optimal for storage layout
- `CoalescingFactor`: Coalescing efficiency factor
- `TransactionsPerBlock`: Memory transactions per thread block
- `BandwidthEfficiency`: Effective bandwidth utilization
- `TileAnalysis`: Shared memory tiling recommendations
- `Optimizations`: Matrix-specific optimization strategies

**Access Order Impact**:
```
// Row-major storage (C/C++ default)
matrix[row][col] = base + row * cols + col

Row-major access (optimal):
  thread(x,y) accesses matrix[y][x]
  → consecutive threads access consecutive addresses
  → perfect coalescing (efficiency = 100%)

Column-major access (suboptimal):
  thread(x,y) accesses matrix[x][y]
  → consecutive threads access addresses stride=cols apart
  → poor coalescing (efficiency = 1/cols)
```

**Example**:
```csharp
// Analyze matrix transpose operation
var analysis = await analyzer.Analyze2DAccessPatternAsync(
    rows: 1024,
    cols: 1024,
    accessOrder: AccessOrder.ColumnMajor,  // Transpose reads column-major
    elementSize: sizeof(float),
    blockDimX: 32,
    blockDimY: 32);

// Result: IsOptimal = false, efficiency very low
// Recommendations:
// - Use 32x32 shared memory tiles
// - Read row-major, write column-major through shared memory
// - Use texture memory for 2D spatial locality
```

**Tile Size Recommendations**:
- **16x16**: Good for limited shared memory (16KB)
- **32x32**: Optimal for most modern GPUs (48KB+ shared memory)
- **64x64**: For GPUs with large shared memory (96KB+, CC 7.0+)

---

### `ProfileRuntimeAccessAsync` - Runtime Performance Profiling

```csharp
public async Task<RuntimeCoalescingProfile> ProfileRuntimeAccessAsync(
    Func<Task> kernelExecution,
    string kernelName,
    int warmupRuns = 3,
    int profileRuns = 10)
```

**Description**: Profiles actual memory access patterns during kernel execution to measure real-world coalescing performance.

**Parameters**:
- `kernelExecution` (`Func<Task>`): Async delegate that executes the kernel
- `kernelName` (`string`): Kernel identifier
- `warmupRuns` (`int`, optional): Number of warmup iterations (default: 3)
- `profileRuns` (`int`, optional): Number of profiling iterations (default: 10)

**Returns**: `Task<RuntimeCoalescingProfile>` containing:
- `KernelName`: Kernel identifier
- `ProfileStartTime`: UTC timestamp when profiling started
- `AverageExecutionTime`: Mean execution time
- `MinExecutionTime`: Fastest execution
- `MaxExecutionTime`: Slowest execution
- `EstimatedBandwidth`: Measured memory bandwidth (bytes/second)
- `EstimatedCoalescingEfficiency`: Inferred coalescing efficiency (0.0 to 1.0)

**Efficiency Estimation**:
```
// High timing variance suggests poor coalescing (thread divergence)
timeVariance = average of (time - mean)²
estimatedEfficiency = 1.0 - (timeVariance / averageTime)
// Clamped to [0.0, 1.0] range
```

**Example**:
```csharp
var profile = await analyzer.ProfileRuntimeAccessAsync(
    kernelExecution: async () =>
    {
        await cudaLaunchKernel(kernelPtr, gridSize, blockSize, args);
    },
    kernelName: "MatrixMultiply",
    warmupRuns: 5,
    profileRuns: 20);

logger.LogInformation("Bandwidth: {0:F2} GB/s", profile.EstimatedBandwidth / 1e9);
logger.LogInformation("Estimated Coalescing: {0:P}", profile.EstimatedCoalescingEfficiency);

// Compare with theoretical peak bandwidth to assess optimization
var peakBandwidth = GetDevicePeakBandwidth();
var efficiency = profile.EstimatedBandwidth / peakBandwidth;
logger.LogInformation("Bandwidth Efficiency: {0:P}", efficiency);
```

**Memory Measurement**:
- Uses `cudaMemGetInfo` to track memory usage before/after execution
- Calculates bandwidth as: `memoryUsed / executionTime`
- Note: May include allocations beyond just data transfers

---

### `CompareAccessPatternsAsync` - Pattern Comparison

```csharp
public async Task<CoalescingComparison> CompareAccessPatternsAsync(
    List<MemoryAccessInfo> patterns,
    int deviceId = 0)
```

**Description**: Compares coalescing efficiency across multiple access patterns to identify optimization opportunities.

**Parameters**:
- `patterns` (`List<MemoryAccessInfo>`): List of access patterns to compare
- `deviceId` (`int`, optional): CUDA device ID

**Returns**: `Task<CoalescingComparison>` containing:
- `Analyses`: Dictionary mapping kernel names to their coalescing analyses
- `BestPattern`: Name of most efficient pattern
- `WorstPattern`: Name of least efficient pattern
- `BestEfficiency`: Highest coalescing efficiency
- `WorstEfficiency`: Lowest coalescing efficiency
- `ImprovementPotential`: Percentage improvement from worst to best
- `Recommendations`: Cross-pattern optimization suggestions

**Example**:
```csharp
var patterns = new List<MemoryAccessInfo>
{
    new() { KernelName = "Baseline_AoS", Stride = 3, ... },
    new() { KernelName = "Optimized_SoA", Stride = 1, ... },
    new() { KernelName = "Hybrid_Tiled", Stride = 1, ... }
};

var comparison = await analyzer.CompareAccessPatternsAsync(patterns);

logger.LogInformation("Best: {0} ({1:P} efficiency)",
    comparison.BestPattern, comparison.BestEfficiency);
logger.LogInformation("Worst: {0} ({1:P} efficiency)",
    comparison.WorstPattern, comparison.WorstEfficiency);
logger.LogInformation("Improvement Potential: {0:P}",
    comparison.ImprovementPotential);

foreach (var recommendation in comparison.Recommendations)
{
    logger.LogInformation("- {0}", recommendation);
}
```

---

## Architecture-Specific Analysis Methods

### `AnalyzeModernArchitectureAsync` - Pascal+ (CC 6.0+)

```csharp
private static async Task<CoalescingAnalysis> AnalyzeModernArchitectureAsync(
    MemoryAccessInfo accessInfo,
    CoalescingAnalysis analysis)
```

**Description**: Analyzes memory access for Pascal/Volta/Turing/Ampere/Ada architectures with relaxed coalescing requirements.

**Modern Architecture Features**:
- **128-byte cache lines** divided into four 32-byte sectors
- **Relaxed coalescing**: Threads don't need consecutive addresses within a warp
- **Sector-level granularity**: Only accessed sectors are transferred
- **Improved broadcast**: Uniform access (stride=0) uses only one sector

**Coalescing Rules**:
1. **Sequential Access (stride=1)**: Optimal, uses only necessary sectors
2. **Broadcast (stride=0)**: Excellent, single 32-byte sector serves all threads
3. **Strided Access**: Efficiency depends on sector coverage, not perfect alignment

**Example Analysis**:
```
For warp accessing 32 consecutive floats (128 bytes):
- Cache line: 128 bytes (4 sectors of 32 bytes each)
- Access size: 32 × 4 = 128 bytes
- Aligned to cache line: efficiency = 100%
- Unaligned by 32 bytes: efficiency ≈ 80% (5 sectors instead of 4)
```

---

### `AnalyzeKeplerMaxwellAsync` - Kepler/Maxwell (CC 3.0-5.x)

```csharp
private static async Task<CoalescingAnalysis> AnalyzeKeplerMaxwellAsync(
    MemoryAccessInfo accessInfo,
    CoalescingAnalysis analysis)
```

**Description**: Analyzes memory access for Kepler and Maxwell architectures.

**Architecture Features**:
- **128-byte cache lines**
- **Standard coalescing rules**: Benefits from consecutive access
- **L1 cache** can be configured for texture operations
- **Moderate alignment sensitivity**

**Transaction Calculation**:
```
For stride-1 access:
  transactions = ceil(accessSize / 128)

For small stride (≤32):
  transactions = threadCount / (128 / (elementSize × stride))

For large stride (>32):
  transactions = threadCount (one per thread)
```

---

### `AnalyzeLegacyArchitectureAsync` - Fermi (CC 2.x)

```csharp
private static async Task<CoalescingAnalysis> AnalyzeLegacyArchitectureAsync(
    MemoryAccessInfo accessInfo,
    CoalescingAnalysis analysis)
```

**Description**: Analyzes memory access for Fermi architecture with strict coalescing requirements.

**Fermi Coalescing Rules**:
- **Strict alignment**: Base address must align to transaction size
- **Transaction sizes**: 32, 64, or 128 bytes depending on element size
- **Perfect coalescing requires**:
  - Stride = 1
  - Aligned base address
  - Element size ≥ 4 bytes

**Transaction Size Selection**:
```
if elementSize ≤ 4: transactionSize = 32 bytes
else if elementSize ≤ 8: transactionSize = 64 bytes
else: transactionSize = 128 bytes
```

---

## Helper Methods

### `CalculateCoalescingEfficiency` - Efficiency Calculation

```csharp
private static double CalculateCoalescingEfficiency(
    MemoryAccessInfo accessInfo,
    int computeCapability)
```

**Description**: Calculates coalescing efficiency based on access pattern and compute capability.

**Efficiency Formulas**:
- **Stride-1, aligned**: efficiency = 1.0 (100%)
- **Stride-1, unaligned**: efficiency = 0.95 (95%)
- **Broadcast (stride=0)**: efficiency = 0.03125 (3.125%, 1/32)
- **Strided (stride>1)**: efficiency = 1.0 / stride
- **Random**: efficiency = 0.03125 (worst case)

---

### `CalculateWastedBandwidth` - Bandwidth Waste

```csharp
private static double CalculateWastedBandwidth(MemoryAccessInfo accessInfo)
```

**Description**: Calculates wasted bandwidth in bytes/second due to uncoalesced access.

**Calculation**:
```
idealBytes = accessSize
actualBytes = transactions × transactionSize
wastedBytes = actualBytes - idealBytes
wastedBandwidth = wastedBytes / executionTime
```

---

### `IdentifyCoalescingIssues` - Issue Detection

```csharp
private static List<CoalescingIssue> IdentifyCoalescingIssues(
    MemoryAccessInfo accessInfo,
    int computeCapability)
```

**Description**: Identifies specific coalescing issues with severity ratings.

**Issues Detected**:
1. **Misalignment** (Medium Severity):
   - Base address not aligned to cache line boundary
   - Impact: 10-20% performance loss

2. **Strided Access** (Medium-High Severity):
   - Stride > 1 causes multiple transactions
   - Impact: Efficiency = 1/stride (e.g., stride=4 → 25% efficiency)

3. **Small Elements** (Low Severity):
   - Element size < 4 bytes underutilizes bandwidth
   - Impact: Bandwidth underutilization

4. **Random Access** (High Severity):
   - Random/irregular access pattern
   - Impact: Up to 32x bandwidth overhead

---

### `GenerateOptimizations` - Optimization Suggestions

```csharp
private static List<string> GenerateOptimizations(
    MemoryAccessInfo accessInfo,
    List<CoalescingIssue> issues)
```

**Description**: Generates architecture-specific optimization recommendations.

**Optimization Strategies by Issue**:

**Misalignment**:
- "Use aligned memory allocation (cudaMallocPitch or aligned allocators)"
- "Adjust data structure padding for alignment"

**Strided Access**:
- "Reorganize data layout from AoS to SoA"
- "Use shared memory to stage strided accesses"
- "Consider texture memory for 2D spatial locality"

**Small Elements**:
- "Pack multiple elements into larger types"
- "Use vector types (int2, float4) for better throughput"

**Random Access**:
- "Sort or reorder data to improve locality"
- "Use texture cache or constant memory for random read patterns"
- "Consider using __ldg() intrinsic for read-only data"

**Large Transfers**:
- "Consider using async memory operations with streams"
- "Overlap computation with memory transfers"

---

## Supporting Types

### `CoalescingAnalysis`

```csharp
public class CoalescingAnalysis
```

**Description**: Complete analysis of memory access coalescing efficiency.

**Properties**:
- `KernelName` (`string`): Kernel identifier
- `Timestamp` (`DateTimeOffset`): Analysis timestamp
- `EfficiencyPercent` (`double`): Efficiency percentage
- `CoalescingEfficiency` (`double`): Efficiency ratio (0.0 to 1.0)
- `WastedBandwidth` (`long`): Wasted bytes/second
- `OptimalAccessSize` (`int`): Recommended access size for architecture
- `TransactionCount` (`int`): Actual memory transactions
- `IdealTransactionCount` (`int`): Minimum transactions possible
- `AccessPattern` (`string`): Detected pattern type
- `ActualBytesTransferred` (`long`): Total bytes including overhead
- `UsefulBytesTransferred` (`long`): Bytes actually needed
- `Issues` (`IList<CoalescingIssue>`): Detected issues with severity
- `Optimizations` (`IList<string>`): Optimization recommendations
- `ArchitectureNotes` (`IList<string>`): GPU-specific notes

---

### `StridedAccessAnalysis`

**Properties**:
- `Stride`: Access stride value
- `ElementSize`: Element size in bytes
- `ThreadCount`: Thread count
- `TransactionsPerWarp`: Transactions needed per warp
- `Efficiency`: Bandwidth efficiency (0.0 to 1.0)
- `IsCoalesced`: True if fully coalesced
- `BandwidthUtilization`: Effective bandwidth usage
- `Recommendations`: Optimization suggestions

---

### `Matrix2DAccessAnalysis`

**Properties**:
- `Rows`, `Columns`: Matrix dimensions
- `AccessOrder`: Row-major or column-major
- `ElementSize`, `BlockDimX`, `BlockDimY`: Access configuration
- `IsOptimal`: True if access matches storage layout
- `CoalescingFactor`: Efficiency factor
- `TransactionsPerBlock`: Transactions per thread block
- `BandwidthEfficiency`: Bandwidth utilization
- `TileAnalysis`: Shared memory tiling recommendations
- `Optimizations`: Matrix-specific strategies

---

### `TileAnalysis`

**Properties**:
- `OptimalTileSize`: Recommended tile dimension (8, 16, 32, 64)
- `SharedMemoryRequired`: Bytes of shared memory needed
- `Efficiency`: Tiling efficiency factor

**Tile Size Selection**:
```
For each tile size [8, 16, 32, 64]:
  sharedMemory = tileSize² × elementSize
  if sharedMemory ≤ maxSharedMemory:
    reuseFactor = 2×tileSize / (tileSize²)
    efficiency = reuseFactor × (sharedMemory ≤ maxSharedMemory/2 ? 1.0 : 0.8)
    select tile with highest efficiency
```

---

## Compute Capability Requirements

| Feature | Minimum CC | Notes |
|---------|-----------|-------|
| Basic Analysis | 2.0 | Fermi and later |
| Modern Coalescing | 6.0 | Pascal relaxed rules |
| Large Shared Memory Tiles | 7.0 | 96KB shared memory |

---

## Performance Implications

### Analysis Overhead
- **Static Analysis**: ~1-5ms (no kernel execution)
- **Runtime Profiling**: Depends on kernel execution time + warmup
- **Memory Impact**: Minimal, primarily stores analysis results

---

## Best Practices

1. **Alignment**: Always align base addresses to 128-byte boundaries
2. **Stride**: Use stride-1 access whenever possible (SoA vs. AoS)
3. **Element Size**: Use 4, 8, or 16-byte elements for optimal transfer
4. **2D Access**: Match access order to storage layout (both row-major or both column-major)
5. **Tiling**: Use shared memory tiles for matrix operations and data reuse
6. **Vector Types**: Use float2/float4/int2/int4 for improved throughput

---

## Example: Complete Analysis Workflow

```csharp
// 1. Analyze static access pattern
var accessInfo = new MemoryAccessInfo
{
    KernelName = "GEMM",
    BaseAddress = matrixPtr,
    AccessSize = N * N * sizeof(float),
    ElementSize = sizeof(float),
    ThreadCount = gridSize * blockSize,
    Stride = N,  // Column-major access of row-major storage
    IsRandom = false
};

var analysis = await analyzer.AnalyzeAccessPatternAsync(accessInfo);

// 2. Check for issues
if (analysis.CoalescingEfficiency < 0.8)
{
    logger.LogWarning("Poor coalescing: {0:P}", analysis.CoalescingEfficiency);
    foreach (var opt in analysis.Optimizations)
    {
        logger.LogInformation("Suggestion: {0}", opt);
    }
}

// 3. Analyze specific pattern (strided)
var stridedAnalysis = await analyzer.AnalyzeStridedAccessAsync(
    stride: N,
    elementSize: sizeof(float),
    threadCount: blockSize);

// 4. Analyze 2D pattern (matrix transpose)
var matrixAnalysis = await analyzer.Analyze2DAccessPatternAsync(
    rows: N,
    cols: N,
    accessOrder: AccessOrder.ColumnMajor,
    elementSize: sizeof(float),
    blockDimX: 32,
    blockDimY: 32);

// 5. Profile runtime performance
var runtimeProfile = await analyzer.ProfileRuntimeAccessAsync(
    kernelExecution: async () => await ExecuteGEMM(),
    kernelName: "GEMM",
    warmupRuns: 5,
    profileRuns: 20);

logger.LogInformation("Measured Bandwidth: {0:F2} GB/s",
    runtimeProfile.EstimatedBandwidth / 1e9);
```

---

## Summary

The `CudaMemoryCoalescingAnalyzer` provides comprehensive memory access pattern analysis with:
- Architecture-aware coalescing rules (Fermi through Ada)
- Static pattern analysis without kernel execution
- Runtime profiling for actual performance measurement
- Strided and 2D matrix access analysis
- Pattern comparison for optimization identification
- Detailed issue detection and optimization recommendations
- Shared memory tiling suggestions for matrix operations

**Total Members Documented**: 40+ public/private members across analyzer and supporting types
