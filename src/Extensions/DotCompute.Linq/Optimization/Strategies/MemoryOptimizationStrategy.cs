using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Memory;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Optimization.CostModel;
using DotCompute.Linq.Optimization.Models;
using ExecutionContext = DotCompute.Linq.Execution.ExecutionContext;

namespace DotCompute.Linq.Optimization.Strategies;
{
/// <summary>
/// Comprehensive memory optimization strategy that optimizes memory access patterns,
/// implements cache-aware algorithms, and provides intelligent memory pooling and prefetching.
/// </summary>
public sealed class MemoryOptimizationStrategy : ILinqOptimizationStrategy
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ExecutionCostModel _costModel;
    private readonly MemoryAnalyzer _memoryAnalyzer;
    private readonly CacheOptimizer _cacheOptimizer;
    private readonly PrefetchingEngine _prefetchingEngine;
    private readonly MemoryPoolManager _poolManager;
    // Memory optimization thresholds
    private const long LargeDatasaThreshold = 100_000_000; // 100MB
    private const double CacheHitRatioThreshold = 0.85;
    private const int OptimalBlockSize = 64 * 1024; // 64KB blocks
    private const int PrefetchDistance = 8; // Cache lines to prefetch ahead
    public MemoryOptimizationStrategy(
        {
        IComputeOrchestrator orchestrator,
        ExecutionCostModel costModel)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _costModel = costModel ?? throw new ArgumentNullException(nameof(costModel));
        _memoryAnalyzer = new MemoryAnalyzer();
        _cacheOptimizer = new CacheOptimizer();
        _prefetchingEngine = new PrefetchingEngine();
        _poolManager = new MemoryPoolManager();
    }
    public async Task<QueryPlan> OptimizeAsync(QueryPlan plan, ExecutionContext context)
        var optimizedPlan = plan.Clone();
        // Analyze memory access patterns
        var memoryProfile = await _memoryAnalyzer.AnalyzeAccessPatterns(plan, context);
        // Apply memory layout optimizations
        await OptimizeMemoryLayout(optimizedPlan, memoryProfile, context);
        // Apply cache-aware optimizations
        await OptimizeCacheUsage(optimizedPlan, memoryProfile, context);
        // Setup intelligent prefetching
        await SetupPrefetching(optimizedPlan, memoryProfile, context);
        // Configure memory pooling
        await ConfigureMemoryPooling(optimizedPlan, memoryProfile, context);
        // Apply NUMA-aware optimizations if applicable
        if (context.IsNumaSystem)
        {
            await ApplyNumaOptimizations(optimizedPlan, context);
        }
        return optimizedPlan;
    private async Task OptimizeMemoryLayout(
        QueryPlan plan,
        MemoryAccessProfile profile,
        ExecutionContext context)
        foreach (var operation in plan.Operations)
            // Determine optimal memory layout for operation
            var optimalLayout = await DetermineOptimalLayout(operation, profile, context);
            operation.MemoryLayout = optimalLayout?.ToString() ?? "Linear";
            // Apply data structure optimizations
            await OptimizeDataStructures(operation, profile, context);
            // Configure memory alignment
            ConfigureMemoryAlignment(operation, context);
    private async Task<MemoryLayout> DetermineOptimalLayout(
        QueryOperation operation,
        var accessPattern = profile.GetAccessPattern(operation);
        return accessPattern.PrimaryPattern switch
            AccessPatternType.Sequential => await OptimizeForSequentialAccess(operation, context),
            AccessPatternType.Random => await OptimizeForRandomAccess(operation, context),
            AccessPatternType.Strided => await OptimizeForStridedAccess(operation, accessPattern, context),
            AccessPatternType.Blocked => await OptimizeForBlockedAccess(operation, context),
            _ => CreateDefaultLayout(operation)
        };
    private Task<MemoryLayout> OptimizeForSequentialAccess(
            Prefetching = new PrefetchingConfig
            {
                Enabled = true,
                Distance = PrefetchDistance,
                Strategy = PrefetchingStrategy.Sequential
            }
        return Task.FromResult(layout);
    private Task<MemoryLayout> OptimizeForRandomAccess(
                Strategy = PrefetchingStrategy.None
            },
            IndexingStrategy = IndexingStrategy.HashTable
    private Task<MemoryLayout> OptimizeForStridedAccess(
        {
        OperationAccessPattern accessPattern,
        var stride = accessPattern.Stride;
        var optimalBlockSize = CalculateStridedBlockSize(stride, context.CacheSize);
            Type = MemoryLayoutType.Strided,
            Alignment = Math.Max(context.CacheLineSize, stride * GetElementSize(operation.DataType)),
            Padding = CalculateStridePadding(stride, context),
            BlockSize = optimalBlockSize,
            Stride = stride,
                Distance = Math.Max(1, PrefetchDistance / stride),
                Strategy = PrefetchingStrategy.Strided
    private Task<MemoryLayout> OptimizeForBlockedAccess(
        // Blocked layout optimized for cache blocking algorithms
        var blockDimension = (int)Math.Sqrt(context.CacheSize / GetElementSize(operation.DataType));
            Type = MemoryLayoutType.Blocked,
            BlockSize = blockDimension * blockDimension * GetElementSize(operation.DataType),
            BlockDimensions = [blockDimension, blockDimension],
                Distance = 2, // Prefetch next block
                Strategy = PrefetchingStrategy.BlockBased
    private MemoryLayout CreateDefaultLayout(QueryOperation operation)
        return new MemoryLayout
            Type = MemoryLayoutType.Linear,
            Alignment = 64, // Standard cache line size
            BlockSize = OptimalBlockSize
    private int CalculateOptimalPadding(QueryOperation operation, ExecutionContext context)
        var elementSize = GetElementSize(operation.DataType);
        var elementsPerCacheLine = context.CacheLineSize / elementSize;
        // Pad to avoid false sharing
        return (int)((elementsPerCacheLine - (operation.InputSize % elementsPerCacheLine)) * elementSize);
    private int CalculateStridedBlockSize(int stride, long cacheSize)
        // Calculate block size that minimizes cache misses for strided access
        return (int)Math.Min(cacheSize / stride, OptimalBlockSize);
    private int CalculateStridePadding(int stride, ExecutionContext context)
        // Align strided accesses to cache line boundaries
        var remainder = stride % context.CacheLineSize;
        return remainder == 0 ? 0 : context.CacheLineSize - remainder;
    private int GetElementSize(Type dataType)
        if (dataType == typeof(byte))
            return 1;
        if (dataType == typeof(short))
            return 2;
        if (dataType == typeof(int))
            return 4;
        if (dataType == typeof(long))
            return 8;
        if (dataType == typeof(float))
        if (dataType == typeof(double))
        return 8; // Default
    private static MemoryAccessPattern ConvertAccessPattern(Models.AccessPattern accessPattern)
        return accessPattern switch
            Models.AccessPattern.Sequential => MemoryAccessPattern.Sequential,
            Models.AccessPattern.Random => MemoryAccessPattern.Random,
            Models.AccessPattern.Strided => MemoryAccessPattern.Strided,
            Models.AccessPattern.Broadcast => MemoryAccessPattern.Broadcast,
            Models.AccessPattern.Gather => MemoryAccessPattern.Gather,
            Models.AccessPattern.Scatter => MemoryAccessPattern.Scatter,
            _ => MemoryAccessPattern.Sequential
    private async Task OptimizeDataStructures(
        // Choose optimal data structure based on access pattern
        operation.DataStructureHint = accessPattern.PrimaryPattern switch
            AccessPatternType.Sequential => DataStructureType.Array.ToString(),
            AccessPatternType.Random => DataStructureType.HashMap.ToString(),
            AccessPatternType.Strided => DataStructureType.StridedArray.ToString(),
            AccessPatternType.Blocked => DataStructureType.BlockedArray.ToString(),
            _ => DataStructureType.Array.ToString()
        // Apply compression if beneficial
        if (ShouldApplyCompression(operation, profile, context))
            operation.CompressionStrategy = (await SelectCompressionStrategy(operation, context))?.ToString() ?? "None";
    private void ConfigureMemoryAlignment(QueryOperation operation, ExecutionContext context)
        // Configure alignment for optimal memory access
        var optimalAlignment = Math.Max(elementSize, context.CacheLineSize);
        // Ensure alignment is power of 2
        while ((optimalAlignment & (optimalAlignment - 1)) != 0)
            optimalAlignment = (optimalAlignment | (optimalAlignment - 1)) + 1;
        operation.MemoryAlignment = optimalAlignment;
    private async Task OptimizeCacheUsage(
        // Apply cache blocking for large datasets
        await ApplyCacheBlocking(plan, profile, context);
        // Optimize cache line utilization
        await OptimizeCacheLineUtilization(plan, context);
        // Configure cache-aware scheduling
        await ConfigureCacheAwareScheduling(plan, context);
    private async Task ApplyCacheBlocking(
            if (operation.InputSize * GetElementSize(operation.DataType) > context.CacheSize)
                var blockingStrategy = await _cacheOptimizer.DetermineBlockingStrategy(
                    operation, profile, context);
                operation.CacheBlockingStrategy = blockingStrategy?.ToString() ?? "None";
    private Task OptimizeCacheLineUtilization(QueryPlan plan, ExecutionContext context)
            // Ensure data structures are aligned to cache line boundaries
            var elementSize = GetElementSize(operation.DataType);
            var elementsPerCacheLine = context.CacheLineSize / elementSize;
            // Adjust block sizes to maximize cache line utilization
            if (operation.MemoryLayout != null)
                // Convert to structured memory layout object if needed
                var blockSize = 64; // Default block size
                var newBlockSize = ((blockSize / context.CacheLineSize) + 1) * context.CacheLineSize;
                // Store the optimized block size in metadata
                operation.OptimizedBlockSize = newBlockSize;
        return Task.CompletedTask;
    private async Task ConfigureCacheAwareScheduling(QueryPlan plan, ExecutionContext context)
        // Schedule operations to maximize cache reuse
        var cacheAwareSchedule = await _cacheOptimizer.CreateCacheAwareSchedule(plan, context);
        // plan.ExecutionSchedule = cacheAwareSchedule; // Commented out - ExecutionSchedule doesn't exist on QueryPlan"
    private async Task SetupPrefetching(
            var accessPattern = profile.GetAccessPattern(operation);
            if (ShouldUsePrefetching(operation, accessPattern, context))
                var prefetchConfig = await _prefetchingEngine.CreatePrefetchConfiguration(
                    operation, accessPattern, context);
                operation.PrefetchingConfig = prefetchConfig;
    private bool ShouldUsePrefetching(
        // Prefetching is beneficial for predictable access patterns
            AccessPatternType.Sequential => true,
            AccessPatternType.Strided => accessPattern.Stride <= context.CacheLineSize * 4,
            AccessPatternType.Blocked => true,
            AccessPatternType.Random => false,
            _ => false
    private async Task ConfigureMemoryPooling(
        // Configure memory pools for frequent allocations
        var poolingStrategy = await _poolManager.CreatePoolingStrategy(plan, profile, context);
        plan.MemoryPoolingStrategy = poolingStrategy?.ToString() ?? "Default";
        // Setup buffer reuse optimization
        await SetupBufferReuse(plan, context);
    private Task SetupBufferReuse(QueryPlan plan, ExecutionContext context)
        // Analyze buffer lifetimes and setup reuse strategy
        var bufferAnalysis = AnalyzeBufferLifetimes(plan);
        var reuseStrategy = CreateBufferReuseStrategy(bufferAnalysis, context);
        plan.BufferReuseStrategy = reuseStrategy?.ToString() ?? "None";
    private BufferLifetimeAnalysis AnalyzeBufferLifetimes(QueryPlan plan)
        var analysis = new BufferLifetimeAnalysis();
            var lifetime = new BufferLifetime
                OperationId = operation.Id,
                AllocationPoint = operation.StartTime ?? DateTime.UtcNow,
                DeallocationPoint = operation.EndTime ?? DateTime.UtcNow,
                Size = operation.InputSize * GetElementSize(operation.DataType),
                AccessPattern = ConvertAccessPattern(operation.AccessPattern)
            };
            analysis.Lifetimes.Add(lifetime);
        return analysis;
    private BufferReuseStrategy CreateBufferReuseStrategy(
        {
        BufferLifetimeAnalysis analysis,
        var strategy = new BufferReuseStrategy();
        // Find non-overlapping lifetimes for buffer reuse
        var sortedLifetimes = analysis.Lifetimes.OrderBy(l => l.AllocationPoint).ToList();
        for (var i = 0; i < sortedLifetimes.Count; i++)
            var current = sortedLifetimes[i];
            for (var j = i + 1; j < sortedLifetimes.Count; j++)
                var candidate = sortedLifetimes[j];
                if (candidate.AllocationPoint >= current.DeallocationPoint &&
                    Math.Abs(candidate.Size - current.Size) < current.Size * 0.1) // 10% size tolerance
                {
                    strategy.ReuseMapping[candidate.OperationId] = current.OperationId;
                    break;
                }
        return strategy;
    private async Task ApplyNumaOptimizations(QueryPlan plan, ExecutionContext context)
        // Apply NUMA-aware memory allocation and task scheduling
            var numaNode = await DetermineOptimalNumaNode(operation, context);
            operation.PreferredNumaNode = numaNode;
            // Configure NUMA-aware memory allocation
            operation.NumaMemoryPolicy = NumaConversionExtensions.ConvertToModelsNumaMemoryPolicy(CreateNumaMemoryPolicy(operation, numaNode, context));
    private async Task<int> DetermineOptimalNumaNode(QueryOperation operation, ExecutionContext context)
        // Analyze memory access patterns and CPU affinity to determine optimal NUMA node
        var cpuUsage = await AnalyzeCpuUsage(operation, context);
        var memoryBandwidth = await AnalyzeMemoryBandwidth(operation, context);
        // Choose NUMA node with best balance of CPU availability and memory bandwidth
        var scores = new Dictionary<int, double>();
        for (var node = 0; node < context.NumaNodeCount; node++)
            var cpuScore = cpuUsage.GetScore(node);
            var memoryScore = memoryBandwidth.GetScore(node);
            scores[node] = cpuScore * 0.6 + memoryScore * 0.4; // Weight CPU slightly higher
        return scores.OrderByDescending(kvp => kvp.Value).First().Key;
    private NumaMemoryPolicy CreateNumaMemoryPolicy(
        int numaNode,
        return new NumaMemoryPolicy
            PreferredNode = numaNode,
            AllocationPolicy = operation.AccessPattern switch
                Models.AccessPattern.Sequential => NumaAllocationPolicy.Local,
                Models.AccessPattern.Random => NumaAllocationPolicy.Interleaved,
                Models.AccessPattern.Strided => NumaAllocationPolicy.Local,
                _ => NumaAllocationPolicy.Default
            MigrationEnabled = operation.InputSize > LargeDatasaThreshold
    private Task<CpuUsageAnalysis> AnalyzeCpuUsage(QueryOperation operation, ExecutionContext context)
        // Analyze CPU usage patterns across NUMA nodes
        return Task.FromResult(new CpuUsageAnalysis()); // Simplified for brevity
    private Task<MemoryBandwidthAnalysis> AnalyzeMemoryBandwidth(
        // Analyze memory bandwidth requirements and availability
        return Task.FromResult(new MemoryBandwidthAnalysis()); // Simplified for brevity
    private bool ShouldApplyCompression(
        // Apply compression for large datasets with low entropy
        var dataSize = operation.InputSize * GetElementSize(operation.DataType);
        var entropy = profile.GetDataEntropy(operation);
        return dataSize > LargeDatasaThreshold && entropy < 0.7;
    private Task<CompressionStrategy> SelectCompressionStrategy(
        // Select optimal compression strategy based on data characteristics
        var strategy = new CompressionStrategy
            Algorithm = CompressionAlgorithm.LZ4, // Fast compression/decompression
            Level = CompressionLevel.Balanced,
        return Task.FromResult(strategy);
}
// Supporting classes and enums
public class MemoryAnalyzer
    {
    public async Task<MemoryAccessProfile> AnalyzeAccessPatterns(QueryPlan plan, ExecutionContext context)
        {
        var profile = new MemoryAccessProfile();
            var pattern = await AnalyzeOperationAccessPattern(operation, context);
            profile.AccessPatterns[operation.Id] = pattern;
        return profile;
    private async Task<OperationAccessPattern> AnalyzeOperationAccessPattern(
        return new OperationAccessPattern
            PrimaryPattern = DetermineAccessPattern(operation),
            Stride = CalculateStride(operation),
            Locality = CalculateLocality(operation),
            CacheEfficiency = await EstimateCacheEfficiency(operation, context)
    private AccessPatternType DetermineAccessPattern(QueryOperation operation)
        return operation.Type switch
            (Models.OperationType)OperationType.Map => AccessPatternType.Sequential,
            (Models.OperationType)OperationType.Filter => AccessPatternType.Sequential,
            (Models.OperationType)OperationType.Reduce => AccessPatternType.Sequential,
            (Models.OperationType)OperationType.GroupBy => AccessPatternType.Random,
            (Models.OperationType)OperationType.Join => AccessPatternType.Random,
            _ => AccessPatternType.Sequential
    private int CalculateStride(QueryOperation operation)
        // Calculate access stride based on operation characteristics
        return operation.AccessPattern == (Models.AccessPattern)AccessPattern.Strided ? operation.Stride : 1;
    private double CalculateLocality(QueryOperation operation)
        // Estimate temporal and spatial locality
            (Models.OperationType)OperationType.Map => 0.9, // High spatial locality
            (Models.OperationType)OperationType.Filter => 0.8,
            (Models.OperationType)OperationType.Reduce => 0.7,
            (Models.OperationType)OperationType.GroupBy => 0.3, // Low locality
            (Models.OperationType)OperationType.Join => 0.2,
            _ => 0.5
    private Task<double> EstimateCacheEfficiency(QueryOperation operation, ExecutionContext context)
        var workingSet = Math.Min(dataSize, context.CacheSize);
        return Task.FromResult((double)workingSet / dataSize);
        return 8;
public class CacheOptimizer
    {
    public Task<CacheBlockingStrategy> DetermineBlockingStrategy(
        var optimalBlockSize = context.CacheSize / 4; // Use 1/4 of cache for blocks
        var strategy = new CacheBlockingStrategy
            BlockSize = (int)Math.Min(optimalBlockSize, operation.InputSize * elementSize),
            TilingDimensions = CalculateTilingDimensions(operation, context),
            PrefetchDistance = 2,
            WriteBackPolicy = CacheWriteBackPolicy.Delayed
    public Task<ExecutionSchedule> CreateCacheAwareSchedule(QueryPlan plan, ExecutionContext context)
        var schedule = new ExecutionSchedule();
        var sortedOperations = plan.Operations
            .OrderBy(op => op.CacheEfficiency)
            .ThenBy(op => op.InputSize)
            .ToList();
        schedule.Operations = sortedOperations;
        return Task.FromResult(schedule);
    private int[] CalculateTilingDimensions(QueryOperation operation, ExecutionContext context)
        if (operation.Type == (Models.OperationType)OperationType.Join)
            // 2D tiling for join operations
            var tileDim = (int)Math.Sqrt(context.CacheSize / GetElementSize(operation.DataType));
            return [tileDim, tileDim];
        // 1D tiling for other operations
        return [(int)(context.CacheSize / GetElementSize(operation.DataType))];
public class PrefetchingEngine
    {
    public Task<PrefetchingConfig> CreatePrefetchConfiguration(
        var config = new PrefetchingConfig
            Enabled = true,
            Distance = CalculatePrefetchDistance(accessPattern, context),
            Strategy = DeterminePrefetchingStrategy(accessPattern),
            Aggressiveness = CalculateAggressiveness(operation, context)
        return Task.FromResult(config);
    private int CalculatePrefetchDistance(OperationAccessPattern accessPattern, ExecutionContext context)
            AccessPatternType.Sequential => context.CacheLineSize / 8,
            AccessPatternType.Strided => Math.Max(1, context.CacheLineSize / accessPattern.Stride),
            AccessPatternType.Blocked => 2,
            _ => 1
    private PrefetchingStrategy DeterminePrefetchingStrategy(OperationAccessPattern accessPattern)
            AccessPatternType.Sequential => PrefetchingStrategy.Sequential,
            AccessPatternType.Strided => PrefetchingStrategy.Strided,
            AccessPatternType.Blocked => PrefetchingStrategy.BlockBased,
            _ => PrefetchingStrategy.None
    private double CalculateAggressiveness(QueryOperation operation, ExecutionContext context)
        // More aggressive prefetching for larger datasets and higher cache miss costs
        var sizeFactor = Math.Min(1.0, dataSize / (double)context.CacheSize);
        return 0.3 + sizeFactor * 0.4; // Range: 0.3 to 0.7
public class MemoryPoolManager
    {
    public Task<MemoryPoolingStrategy> CreatePoolingStrategy(
        var strategy = new MemoryPoolingStrategy();
        // Analyze memory allocation patterns
        var allocationSizes = plan.Operations
            .Select(op => op.InputSize * GetElementSize(op.DataType))
            .Distinct()
            .OrderBy(size => size)
        // Create pools for common allocation sizes
        foreach (var size in allocationSizes)
            if (ShouldCreatePool(size, plan, context))
                strategy.Pools.Add(new MemoryPool
                    BlockSize = size,
                    InitialCapacity = CalculateInitialCapacity(size, plan),
                    MaxCapacity = CalculateMaxCapacity(size, context),
                    AllocationStrategy = MemoryAllocationStrategy.BestFit
                });
    private bool ShouldCreatePool(long size, QueryPlan plan, ExecutionContext context)
        return usage >= 2 || size > 1024 * 1024; // Multiple uses or large allocations
    private int CalculateInitialCapacity(long size, QueryPlan plan)
        {
        return Math.Max(2, usage / 2);
    private int CalculateMaxCapacity(long size, ExecutionContext context)
        return (int)Math.Min(16, context.AvailableMemory / size / 4);
// Supporting data structures
public class MemoryAccessProfile
    {
    public Dictionary<string, OperationAccessPattern> AccessPatterns { get; set; } = [];
    public OperationAccessPattern GetAccessPattern(QueryOperation operation)
        return AccessPatterns.TryGetValue(operation.Id, out var pattern)
            ? pattern
            : new OperationAccessPattern();
    public double GetDataEntropy(QueryOperation operation)
        // Simplified entropy calculation
        return 0.8; // Default medium entropy
public class OperationAccessPattern
    {
    public AccessPatternType PrimaryPattern { get; set; }
    public int Stride { get; set; } = 1;
    public double Locality { get; set; }
    public double CacheEfficiency { get; set; }
public class MemoryLayout
    {
    public MemoryLayoutType Type { get; set; }
    public int Alignment { get; set; }
    public int Padding { get; set; }
    public long BlockSize { get; set; }
    public int Stride { get; set; }
    public int[] BlockDimensions { get; set; } = Array.Empty<int>();
    public PrefetchingConfig? Prefetching { get; set; }
    public IndexingStrategy IndexingStrategy { get; set; }
public class PrefetchingConfig
    {
    public bool Enabled { get; set; }
    public int Distance { get; set; }
    public PrefetchingStrategy Strategy { get; set; }
    public double Aggressiveness { get; set; }
public class CacheBlockingStrategy
    {
    public int BlockSize { get; set; }
    public int[] TilingDimensions { get; set; } = Array.Empty<int>();
    public int PrefetchDistance { get; set; }
    public CacheWriteBackPolicy WriteBackPolicy { get; set; }
public class ExecutionSchedule
    {
    public List<QueryOperation> Operations { get; set; } = [];
public class MemoryPoolingStrategy
    {
    public List<MemoryPool> Pools { get; set; } = [];
public class MemoryPool
    {
    public int InitialCapacity { get; set; }
    public int MaxCapacity { get; set; }
    public MemoryAllocationStrategy AllocationStrategy { get; set; }
public class BufferLifetimeAnalysis
    {
    public List<BufferLifetime> Lifetimes { get; set; } = [];
public class BufferLifetime
    {
    public string OperationId { get; set; } = string.Empty;
    public DateTime AllocationPoint { get; set; }
    public DateTime DeallocationPoint { get; set; }
    public long Size { get; set; }
    public MemoryAccessPattern AccessPattern { get; set; }
public class BufferReuseStrategy
    {
    public Dictionary<string, string> ReuseMapping { get; set; } = [];
public class NumaMemoryPolicy
    {
    public int PreferredNode { get; set; }
    public NumaAllocationPolicy AllocationPolicy { get; set; }
    public bool MigrationEnabled { get; set; }
public class CompressionStrategy
    {
    public CompressionAlgorithm Algorithm { get; set; }
    public CompressionLevel Level { get; set; }
public class CpuUsageAnalysis
    {
    public double GetScore(int numaNode) => 0.5; // Simplified
public class MemoryBandwidthAnalysis
    {
// Enums
public enum AccessPatternType
    {
    Sequential,
    Random,
    Strided,
    Blocked
public enum MemoryLayoutType
    {
    Linear,
    ArrayOfStructures,
    StructureOfArrays,
public enum IndexingStrategy
    {
    HashTable,
    BTree
public enum PrefetchingStrategy
    {
    None,
    BlockBased
public enum CacheWriteBackPolicy
    {
    Immediate,
    Delayed,
    Batched
public enum MemoryAllocationStrategy
    {
    FirstFit,
    BestFit,
    WorstFit
public enum DataStructureType
    {
    Array,
    HashMap,
    StridedArray,
    BlockedArray
public enum NumaAllocationPolicy
    {
    Default,
    Local,
    Interleaved,
    Preferred
public enum CompressionAlgorithm
    {
    LZ4,
    Snappy,
    ZSTD
public enum CompressionLevel
    {
    Fast,
    Balanced,
    Maximum
public enum MemoryAccessPattern
    {
    Broadcast,
    Gather,
    Scatter
/// Conversion methods for NUMA-related types.
internal static class NumaConversionExtensions
    {
    public static Models.NumaMemoryPolicy ConvertToModelsNumaMemoryPolicy(NumaMemoryPolicy source)
        return new Models.NumaMemoryPolicy
            PreferredNode = source.PreferredNode,
            AllocationPolicy = ConvertToModelsAllocationPolicy(source.AllocationPolicy),
            MigrationEnabled = source.MigrationEnabled
    private static Models.NumaAllocationPolicy ConvertToModelsAllocationPolicy(NumaAllocationPolicy source)
        return source switch
            NumaAllocationPolicy.Default => Models.NumaAllocationPolicy.Default,
            NumaAllocationPolicy.Local => Models.NumaAllocationPolicy.Local,
            NumaAllocationPolicy.Interleaved => Models.NumaAllocationPolicy.Interleaved,
            NumaAllocationPolicy.Preferred => Models.NumaAllocationPolicy.Preferred,
            _ => Models.NumaAllocationPolicy.Default
