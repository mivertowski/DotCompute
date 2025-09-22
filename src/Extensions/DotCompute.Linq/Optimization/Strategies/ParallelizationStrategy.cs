using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Optimization.Enums;
using DotCompute.Core.Optimization.Models;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Optimization.CostModel;
using DotCompute.Linq.Optimization.Models;
using DotCompute.Linq.Types;
using ExecutionContext = DotCompute.Linq.Execution.ExecutionContext;
using Models = DotCompute.Linq.Optimization.Models;
using DotCompute.Linq.Pipelines.Models;

namespace DotCompute.Linq.Optimization.Strategies;
{
/// <summary>
/// Advanced parallelization strategy that provides dynamic parallelism selection,
/// work distribution optimization, load balancing, and GPU occupancy optimization.
/// </summary>
public sealed class ParallelizationStrategy : ILinqOptimizationStrategy
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ExecutionCostModel _costModel;
    private readonly WorkloadAnalyzer _workloadAnalyzer;
    private readonly LoadBalancer _loadBalancer;
    private readonly GpuOccupancyOptimizer _gpuOptimizer;
    private readonly DynamicScheduler _scheduler;
    // Parallelization thresholds and parameters
    private const int MinParallelizationThreshold = 1000;
    private const double LoadImbalanceThreshold = 0.15; // 15% imbalance tolerance
    private const double OptimalGpuOccupancy = 0.75;
    private const int MinWorkPerThread = 100;
    private const int MaxParallelismDegree = 1024;
    public ParallelizationStrategy(
        {
        IComputeOrchestrator orchestrator,
        ExecutionCostModel costModel)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _costModel = costModel ?? throw new ArgumentNullException(nameof(costModel));
        _workloadAnalyzer = new WorkloadAnalyzer(costModel);
        _loadBalancer = new LoadBalancer();
        _gpuOptimizer = new GpuOccupancyOptimizer();
        _scheduler = new DynamicScheduler();
    }
    public async Task<QueryPlan> OptimizeAsync(QueryPlan plan, ExecutionContext context)
        var optimizedPlan = plan.Clone();
        // Analyze workload characteristics for parallelization
        var workloadProfile = await _workloadAnalyzer.AnalyzeWorkload(plan, context);
        // Determine optimal parallelization strategy for each operation
        await OptimizeParallelization(optimizedPlan, workloadProfile, context);
        // Apply load balancing optimizations
        await ApplyLoadBalancing(optimizedPlan, workloadProfile, context);
        // Optimize for target backend (CPU vs GPU)
        if (context.TargetBackend == BackendType.CUDA)
        {
            await OptimizeForGpu(optimizedPlan, workloadProfile, context);
        }
        else
            await OptimizeForCpu(optimizedPlan, workloadProfile, context);
        // Apply dynamic scheduling optimizations
        await ApplyDynamicScheduling(optimizedPlan, context);
        return optimizedPlan;
    private async Task OptimizeParallelization(
        QueryPlan plan,
        WorkloadProfile profile,
        ExecutionContext context)
        foreach (var operation in plan.Operations)
            var parallelizationConfig = await DetermineOptimalParallelization(
                operation, profile, context);
            operation.ParallelizationConfig = ConvertToModelsParallelizationConfig(parallelizationConfig);
    private async Task<StrategiesParallelizationConfig> DetermineOptimalParallelization(
        QueryOperation operation,
        var workload = profile.GetWorkloadCharacteristics(operation);
        // Check if operation is suitable for parallelization
        if (!IsParallelizable(operation, workload))
            return CreateSequentialConfig();
        // Determine optimal parallelism degree
        var optimalDegree = await CalculateOptimalParallelismDegree(
            operation, workload, context);
        // Select parallelization pattern
        var pattern = SelectParallelizationPattern(operation, workload, context);
        // Configure work distribution
        var workDistribution = await ConfigureWorkDistribution(
            operation, optimalDegree, pattern, context);
        return new StrategiesParallelizationConfig
            Degree = optimalDegree,
            Pattern = pattern,
            WorkDistribution = workDistribution,
            LoadBalancingStrategy = SelectLoadBalancingStrategy(operation, workload),
            SynchronizationStrategy = SelectSynchronizationStrategy(operation, pattern),
            GrainSize = CalculateOptimalGrainSize(operation, optimalDegree, context)
        };
    private bool IsParallelizable(QueryOperation operation, Pipelines.Models.WorkloadCharacteristics workload)
        if (operation.HasSideEffects && !operation.IsSideEffectSafe)
            return false;
        if (operation.InputSize < MinParallelizationThreshold)
        if (workload.ComputeIntensity < 0.1)
            return false; // Too simple to benefit
        return operation.Type switch
            Models.OperationType.Map => true,
            Models.OperationType.Filter => true,
            Models.OperationType.Reduce => operation.IsAssociative,
            Models.OperationType.GroupBy => true,
            Models.OperationType.Join => workload.DataSize > MinParallelizationThreshold * 10,
            Models.OperationType.CustomKernel => operation.IsAssociative,
            _ => false
    private StrategiesParallelizationConfig CreateSequentialConfig()
        {
            Degree = 1,
            Pattern = ParallelizationPattern.Sequential,
            WorkDistribution = WorkDistribution.Single,
            LoadBalancingStrategy = LoadBalancingStrategy.None,
            SynchronizationStrategy = SynchronizationStrategy.None,
            GrainSize = int.MaxValue
    private async Task<int> CalculateOptimalParallelismDegree(
        Pipelines.Models.WorkloadCharacteristics workload,
        // Start with hardware-based upper bound
        var maxDegree = context.TargetBackend == BackendType.CUDA
            ? context.GpuInfo?.MaxThreadsPerBlock ?? 256
            : context.AvailableCores;
        // Apply workload-based constraints
        var workBasedLimit = (int)(operation.InputSize / MinWorkPerThread);
        maxDegree = Math.Min(maxDegree, workBasedLimit);
        // Apply memory bandwidth constraints
        var memoryBasedLimit = await CalculateMemoryBandwidthLimit(operation, workload, context);
        maxDegree = Math.Min(maxDegree, memoryBasedLimit);
        // Apply compute intensity scaling
        var computeScaling = CalculateComputeIntensityScaling(workload);
        var optimalDegree = (int)(maxDegree * computeScaling);
        // Apply Amdahl's law considerations
        var amdahlScaling = CalculateAmdahlScaling(operation, workload);
        optimalDegree = (int)(optimalDegree * amdahlScaling);
        return Math.Max(1, Math.Min(optimalDegree, MaxParallelismDegree));
    private Task<int> CalculateMemoryBandwidthLimit(
        var bytesPerElement = GetElementSize(operation.DataType);
        var totalBytes = operation.InputSize * bytesPerElement;
        var memoryBandwidth = context.MemoryBandwidth;
        var targetUtilization = 0.8; // 80% memory bandwidth utilization
        // Calculate threads that can be sustained by memory bandwidth
        var threadsPerBandwidth = (int)(memoryBandwidth * targetUtilization /
                                       (totalBytes / workload.ComputeIntensity));
        return Task.FromResult(Math.Max(1, threadsPerBandwidth));
    private double CalculateComputeIntensityScaling(Pipelines.Models.WorkloadCharacteristics workload)
        // Higher compute intensity allows for more parallelism
        return Math.Min(1.0, 0.3 + workload.ComputeIntensity * 0.7);
    private double CalculateAmdahlScaling(QueryOperation operation, Pipelines.Models.WorkloadCharacteristics workload)
        // Apply Amdahl's law based on sequential fraction
        var sequentialFraction = EstimateSequentialFraction(operation);
        var parallelFraction = 1.0 - sequentialFraction;
        // Simplified Amdahl's law application
        return Math.Min(1.0, parallelFraction + sequentialFraction * 0.1);
    private double EstimateSequentialFraction(QueryOperation operation)
            Models.OperationType.Map => 0.05, // Highly parallel
            Models.OperationType.Filter => 0.10,
            Models.OperationType.Reduce => 0.20, // Some reduction overhead
            Models.OperationType.GroupBy => 0.30, // Grouping coordination
            Models.OperationType.Join => 0.25, // Join coordination
            Models.OperationType.CustomKernel => 0.15,
            _ => 0.50 // Conservative default
    private ParallelizationPattern SelectParallelizationPattern(
            Models.OperationType.Map => ParallelizationPattern.DataParallel,
            Models.OperationType.Filter => ParallelizationPattern.DataParallel,
            Models.OperationType.Reduce => SelectReducePattern(workload, context),
            Models.OperationType.GroupBy => ParallelizationPattern.TaskParallel,
            Models.OperationType.Join => SelectJoinPattern(workload, context),
            Models.OperationType.CustomKernel => SelectAggregatePattern(workload, context),
            _ => ParallelizationPattern.Sequential
    private ParallelizationPattern SelectReducePattern(
            return ParallelizationPattern.TreeReduction;
        return workload.DataSize > 1_000_000
            ? ParallelizationPattern.MapReduce
            : ParallelizationPattern.DataParallel;
    private ParallelizationPattern SelectJoinPattern(
        var leftSize = workload.DataSize;
        var rightSize = workload.SecondaryDataSize;
        if (leftSize > rightSize * 10)
            return ParallelizationPattern.BroadcastJoin;
        return ParallelizationPattern.HashJoin;
    private ParallelizationPattern SelectAggregatePattern(
        return context.TargetBackend == BackendType.CUDA
            ? ParallelizationPattern.TreeReduction
            : ParallelizationPattern.MapReduce;
    private async Task<WorkDistribution> ConfigureWorkDistribution(
        int parallelismDegree,
        ParallelizationPattern pattern,
        return pattern switch
            ParallelizationPattern.DataParallel => ConfigureDataParallelDistribution(
                operation, parallelismDegree, context),
            ParallelizationPattern.TaskParallel => await ConfigureTaskParallelDistribution(
            ParallelizationPattern.MapReduce => ConfigureMapReduceDistribution(
            ParallelizationPattern.TreeReduction => ConfigureTreeReductionDistribution(
            _ => WorkDistribution.Single
    private WorkDistribution ConfigureDataParallelDistribution(
        var grainSize = CalculateOptimalGrainSize(operation, parallelismDegree, context);
        return new WorkDistribution
            Type = WorkDistributionType.ChunkedStatic,
            ChunkSize = grainSize,
            LoadBalancingEnabled = false // Static distribution doesn't need balancing
    private async Task<WorkDistribution> ConfigureTaskParallelDistribution(
        // Analyze task dependencies for optimal distribution
        var dependencies = await AnalyzeTaskDependencies(operation);
            Type = WorkDistributionType.TaskQueue,
            ChunkSize = MinWorkPerThread,
            LoadBalancingEnabled = true,
            TaskDependencies = dependencies
    private WorkDistribution ConfigureMapReduceDistribution(
        var mapGrainSize = operation.InputSize / parallelismDegree;
        var reduceGrainSize = parallelismDegree / Math.Min(parallelismDegree, context.AvailableCores);
            Type = WorkDistributionType.MapReduce,
            ChunkSize = (int)mapGrainSize,
            ReduceGrainSize = (int)reduceGrainSize,
            LoadBalancingEnabled = true
    private WorkDistribution ConfigureTreeReductionDistribution(
        var treeDepth = (int)Math.Ceiling(Math.Log2(parallelismDegree));
            Type = WorkDistributionType.TreeReduction,
            ChunkSize = (int)(operation.InputSize / parallelismDegree),
            TreeDepth = treeDepth,
            LoadBalancingEnabled = false // Tree structure is inherently balanced
    private LoadBalancingStrategy SelectLoadBalancingStrategy(
        Pipelines.Models.WorkloadCharacteristics workload)
        if (workload.WorkloadVariance < LoadImbalanceThreshold)
            return LoadBalancingStrategy.Static;
            Models.OperationType.Map => LoadBalancingStrategy.WorkStealing,
            Models.OperationType.Filter => LoadBalancingStrategy.Dynamic,
            Models.OperationType.GroupBy => LoadBalancingStrategy.WorkStealing,
            Models.OperationType.Join => LoadBalancingStrategy.Dynamic,
            _ => LoadBalancingStrategy.Static
    private SynchronizationStrategy SelectSynchronizationStrategy(
        ParallelizationPattern pattern)
            ParallelizationPattern.DataParallel => SynchronizationStrategy.Barrier,
            ParallelizationPattern.TaskParallel => SynchronizationStrategy.EventBased,
            ParallelizationPattern.MapReduce => SynchronizationStrategy.Phased,
            ParallelizationPattern.TreeReduction => SynchronizationStrategy.Hierarchical,
            _ => SynchronizationStrategy.None
    private int CalculateOptimalGrainSize(
        var baseGrainSize = (int)(operation.InputSize / parallelismDegree);
        // Apply cache-aware grain sizing
        var cacheOptimalSize = context.CacheLineSize * 16; // 16 cache lines per grain
        // Apply computational complexity scaling
        var complexityScaling = operation.Type switch
            Models.OperationType.Map => 1.0,
            Models.OperationType.Filter => 0.8,
            Models.OperationType.Reduce => 2.0,
            Models.OperationType.GroupBy => 1.5,
            Models.OperationType.Join => 3.0,
            _ => 1.0
        var adjustedGrainSize = (int)(baseGrainSize * complexityScaling);
        // Ensure minimum efficiency
        return Math.Max(MinWorkPerThread, Math.Min(adjustedGrainSize, (int)operation.InputSize));
    private async Task ApplyLoadBalancing(
            var config = operation.ParallelizationConfig;
            if (config?.LoadBalancingStrategy != "None")
            {
                var loadBalancingConfig = await _loadBalancer.CreateConfiguration(
                    operation, profile, context);
                // Convert from Strategies LoadBalancingConfig to Models LoadBalancingConfig
                operation.LoadBalancingConfig = new Models.LoadBalancingConfig
                {
                    Strategy = loadBalancingConfig.Strategy.ToString(),
                    MaxThreads = Environment.ProcessorCount,
                    ChunkSize = 1000,
                    UseDynamicScheduling = loadBalancingConfig.WorkStealingEnabled
                };
            }
    private async Task OptimizeForGpu(
            if (operation.ParallelizationConfig?.Degree > 1)
                var gpuConfig = await _gpuOptimizer.OptimizeForGpu(
                operation.GpuOptimizationConfig = ConvertToModelsGpuOptimizationConfig(gpuConfig);
        // Apply cross-operation GPU optimizations
        await OptimizeGpuMemoryTransfers(plan, context);
        await OptimizeGpuOccupancy(plan, context);
    private async Task OptimizeForCpu(
                var cpuConfig = await OptimizeCpuExecution(operation, profile, context);
                operation.CpuOptimizationConfig = cpuConfig;
        // Apply NUMA-aware optimizations
        if (context.IsNumaSystem)
            await ApplyNumaOptimizations(plan, context);
    private Task<Models.CpuOptimizationConfig> OptimizeCpuExecution(
        var threadAffinity = CalculateOptimalThreadAffinity(operation, context);
        var vectorizationHints = GenerateVectorizationHints(operation, workload);
        var config = new Models.CpuOptimizationConfig
            UseSIMD = vectorizationHints.Any(h => h.Type == VectorizationType.SIMD),
            PreferredVectorWidth = vectorizationHints.FirstOrDefault()?.VectorWidth ?? 256,
            EnableCacheBlocking = workload.DataSize > 1024 * 1024,
            CacheBlockSize = 64 * 1024,
            EnablePrefetching = true,
            PrefetchDistance = 8,
            EnableLoopUnrolling = vectorizationHints.FirstOrDefault()?.LoopUnrollFactor > 1,
            UnrollFactor = vectorizationHints.FirstOrDefault()?.LoopUnrollFactor ?? 4
        return Task.FromResult(config);
    private ThreadAffinityConfig CalculateOptimalThreadAffinity(
        // Distribute threads across physical cores for compute-intensive operations
        var physicalCores = context.AvailableCores / (context.HasHyperThreading ? 2 : 1);
        var threadsPerCore = context.HasHyperThreading ? 2 : 1;
        return new ThreadAffinityConfig
            UsePhysicalCores = operation.ParallelizationConfig?.Degree <= physicalCores,
            PreferredCores = GeneratePreferredCoreList(operation, context),
            AvoidHyperThreading = IsComputeIntensive(operation)
    private List<int> GeneratePreferredCoreList(QueryOperation operation, ExecutionContext context)
        var coreCount = operation.ParallelizationConfig?.Degree ?? 1;
        var preferredCores = new List<int>();
        // Distribute across NUMA nodes if available
            var coresPerNode = context.AvailableCores / context.NumaNodeCount;
            for (var i = 0; i < coreCount && i < context.AvailableCores; i++)
                preferredCores.Add(i % context.AvailableCores);
                preferredCores.Add(i);
        return preferredCores;
    private bool IsComputeIntensive(QueryOperation operation)
            Models.OperationType.Reduce => true,
            Models.OperationType.CustomKernel => true,
    private List<VectorizationHint> GenerateVectorizationHints(
        var hints = new List<VectorizationHint>();
        if (CanVectorize(operation))
            hints.Add(new VectorizationHint
                Type = VectorizationType.SIMD,
                VectorWidth = DetermineOptimalVectorWidth(operation.DataType),
                AlignmentRequirement = GetAlignmentRequirement(operation.DataType),
                LoopUnrollFactor = CalculateUnrollFactor(workload)
            });
        return hints;
    private bool CanVectorize(QueryOperation operation)
        // Check if operation can benefit from vectorization
    private int DetermineOptimalVectorWidth(Type dataType)
        var elementSize = GetElementSize(dataType);
        // Assume AVX2 (256-bit) or AVX-512 (512-bit) support
        var maxVectorSize = 512 / 8; // 64 bytes for AVX-512
        return maxVectorSize / elementSize;
    private int GetAlignmentRequirement(Type dataType)
        // Vector alignment requirements
        return Math.Max(32, GetElementSize(dataType)); // 32-byte alignment for AVX2
    private int CalculateUnrollFactor(Pipelines.Models.WorkloadCharacteristics workload)
        // Calculate optimal loop unroll factor based on instruction pipeline depth
        return workload.ComputeIntensity > 0.5 ? 8 : 4;
    private async Task OptimizeGpuMemoryTransfers(QueryPlan plan, ExecutionContext context)
        foreach (var group in transferGroups)
            await OptimizeTransferGroup(group, context);
    private async Task OptimizeGpuOccupancy(QueryPlan plan, ExecutionContext context)
        {
            if (operation.GpuOptimizationConfig != null)
                await _gpuOptimizer.OptimizeOccupancy(operation, context);
    private async Task ApplyDynamicScheduling(QueryPlan plan, ExecutionContext context)
        var schedule = await _scheduler.CreateSchedule(plan, context);
        plan.DynamicScheduleConfig = schedule;
    private Task<TaskDependencyGraph> AnalyzeTaskDependencies(QueryOperation operation)
        // Simplified dependency analysis
        return Task.FromResult(new TaskDependencyGraph());
    private List<MemoryTransferGroup> GroupMemoryTransfers(QueryPlan plan)
        // Group related memory transfers for batching
        return [];
    private async Task OptimizeTransferGroup(MemoryTransferGroup group, ExecutionContext context)
        // Optimize grouped memory transfers
        await Task.CompletedTask;
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
    private async Task ApplyNumaOptimizations(QueryPlan plan, ExecutionContext context)
        // Apply NUMA-aware thread and memory placement
            if (operation.CpuOptimizationConfig != null)
                var numaConfig = await CreateNumaConfiguration(operation, context);
                operation.CpuOptimizationConfig.NumaConfiguration = ConvertToModelsNumaConfiguration(numaConfig);
    private Task<NumaConfiguration> CreateNumaConfiguration(
        var config = new NumaConfiguration
            PreferredNode = 0, // Simplified
            ThreadDistribution = NumaThreadDistribution.Interleaved,
            MemoryBinding = NumaMemoryBinding.Local
    private static Models.ParallelizationConfig ConvertToModelsParallelizationConfig(StrategiesParallelizationConfig source)
        return new Models.ParallelizationConfig
            Degree = source.Degree,
            ChunkSize = source.GrainSize,
            LoadBalancingEnabled = source.LoadBalancingStrategy != LoadBalancingStrategy.None
    private static Models.GpuOptimizationConfig ConvertToModelsGpuOptimizationConfig(StrategiesGpuOptimizationConfig source)
        return new Models.GpuOptimizationConfig
            BlockSize = source.BlockSize,
            SharedMemorySize = source.SharedMemoryUsage
    private static CacheOptimizationStrategy CreateCacheOptimizationStrategy(QueryOperation operation, Pipelines.Models.WorkloadCharacteristics workload)
        return new CacheOptimizationStrategy
            CacheLineSize = 64, // Common cache line size
            PrefetchDistance = workload.DataSize > 1024 * 1024 ? 8 : 4, // Adjust based on workload
            TemporalLocality = operation.Name.Contains("reduce") || operation.Name.Contains("scan"),
            SpatialLocality = operation.Name.Contains("map") || operation.Name.Contains("transform")
    private static HyperThreadingStrategy DetermineHyperThreadingStrategy(QueryOperation operation, ExecutionContext context)
        // Simple heuristic: disable hyperthreading for compute-intensive operations
        var isComputeIntensive = operation.Name.Contains("complex") ||
                                operation.Name.Contains("mathematical") ||
                                operation.Name.Contains("crypto");
        return isComputeIntensive ? HyperThreadingStrategy.Disabled : HyperThreadingStrategy.Enabled;
    private static Models.NumaConfiguration ConvertToModelsNumaConfiguration(NumaConfiguration source)
        return new Models.NumaConfiguration
            PreferredNode = source.PreferredNode,
            ThreadDistribution = ConvertToModelsThreadDistribution(source.ThreadDistribution),
            MemoryBinding = ConvertToModelsMemoryBinding(source.MemoryBinding)
    private static Models.NumaThreadDistribution ConvertToModelsThreadDistribution(NumaThreadDistribution source)
        return source switch
            NumaThreadDistribution.None => Models.NumaThreadDistribution.None,
            NumaThreadDistribution.Balanced => Models.NumaThreadDistribution.Balanced,
            NumaThreadDistribution.Interleaved => Models.NumaThreadDistribution.Interleaved,
            NumaThreadDistribution.Packed => Models.NumaThreadDistribution.Packed,
            _ => Models.NumaThreadDistribution.None
    private static Models.NumaMemoryBinding ConvertToModelsMemoryBinding(NumaMemoryBinding source)
            NumaMemoryBinding.None => Models.NumaMemoryBinding.None,
            NumaMemoryBinding.Local => Models.NumaMemoryBinding.Local,
            NumaMemoryBinding.Specific => Models.NumaMemoryBinding.Specific,
            NumaMemoryBinding.Interleaved => Models.NumaMemoryBinding.Interleaved,
            _ => Models.NumaMemoryBinding.None
}
// Supporting classes and data structures
public class WorkloadAnalyzer
    {
    public WorkloadAnalyzer(ExecutionCostModel costModel)
        {
        _costModel = costModel;
    public async Task<WorkloadProfile> AnalyzeWorkload(QueryPlan plan, ExecutionContext context)
        var profile = new WorkloadProfile();
            var characteristics = await AnalyzeOperation(operation, context);
            profile.OperationCharacteristics[operation.Id] = characteristics;
        return profile;
    private async Task<Pipelines.Models.WorkloadCharacteristics> AnalyzeOperation(
        return new Pipelines.Models.WorkloadCharacteristics
            DataSize = operation.InputSize,
            ComputeIntensity = CalculateComputeIntensity(operation),
            MemoryIntensity = CalculateMemoryIntensity(operation),
            WorkloadVariance = await EstimateWorkloadVariance(operation, context),
            SecondaryDataSize = EstimateSecondaryDataSize(operation)
    private double CalculateComputeIntensity(QueryOperation operation)
            Models.OperationType.Map => 0.8,
            Models.OperationType.Filter => 0.4,
            Models.OperationType.Reduce => 0.9,
            Models.OperationType.GroupBy => 0.6,
            Models.OperationType.Join => 0.7,
            Models.OperationType.CustomKernel => 0.8,
            _ => 0.5
    private double CalculateMemoryIntensity(QueryOperation operation)
            Models.OperationType.Map => 0.3,
            Models.OperationType.Filter => 0.6,
            Models.OperationType.Reduce => 0.4,
            Models.OperationType.GroupBy => 0.8,
            Models.OperationType.Join => 0.9,
            Models.OperationType.CustomKernel => 0.5,
    private Task<double> EstimateWorkloadVariance(QueryOperation operation, ExecutionContext context)
        // Estimate variance in work per element
        var variance = operation.Type switch
            Models.OperationType.Filter => 0.3, // High variance due to selectivity
            Models.OperationType.GroupBy => 0.4, // Variance in group sizes
            Models.OperationType.Join => 0.5, // Variance in join cardinality
            _ => 0.1 // Low variance for uniform operations
        return Task.FromResult(variance);
    private long EstimateSecondaryDataSize(QueryOperation operation)
        return operation.Type == Models.OperationType.Join ? operation.InputSize / 2 : 0;
public class LoadBalancer
    {
    public Task<StrategiesLoadBalancingConfig> CreateConfiguration(
        var config = new StrategiesLoadBalancingConfig
            Strategy = Enum.TryParse<LoadBalancingStrategy>(operation.ParallelizationConfig?.LoadBalancingStrategy, out var parsed) ? parsed : LoadBalancingStrategy.Static,
            WorkStealingEnabled = workload.WorkloadVariance > 0.2,
            AdaptiveThreshold = CalculateAdaptiveThreshold(workload),
            MonitoringInterval = TimeSpan.FromMilliseconds(100)
    private double CalculateAdaptiveThreshold(Pipelines.Models.WorkloadCharacteristics workload)
        return 0.1 + workload.WorkloadVariance * 0.3;
public class GpuOccupancyOptimizer
    {
    public Task<StrategiesGpuOptimizationConfig> OptimizeForGpu(
        return Task.FromResult(new StrategiesGpuOptimizationConfig
            BlockSize = CalculateOptimalBlockSize(operation, context),
            GridSize = CalculateOptimalGridSize(operation, context),
            SharedMemoryUsage = CalculateSharedMemoryUsage(operation, workload),
            RegisterUsage = EstimateRegisterUsage(operation),
            OccupancyTarget = OptimalGpuOccupancy
        });
    public Task OptimizeOccupancy(QueryOperation operation, ExecutionContext context)
        {
        if (operation.GpuOptimizationConfig == null)
            return Task.CompletedTask;
        // Adjust configuration for optimal occupancy
        var config = operation.GpuOptimizationConfig;
        var maxOccupancy = CalculateMaxOccupancy(config, context);
        if (maxOccupancy < OptimalGpuOccupancy)
            AdjustConfigurationForOccupancy(config, context);
        return Task.CompletedTask;
    private int CalculateOptimalBlockSize(QueryOperation operation, ExecutionContext context)
        var gpuInfo = context.GpuInfo;
        if (gpuInfo == null)
            return 256; // Default
        // Start with warp size and scale up
        var warpSize = 32; // NVIDIA warp size
        var maxBlockSize = gpuInfo.MaxThreadsPerBlock;
        // Find optimal block size considering register usage and shared memory
        for (var blockSize = warpSize; blockSize <= maxBlockSize; blockSize += warpSize)
            if (IsOptimalBlockSize(blockSize, operation, context))
                return blockSize;
        return 256; // Safe default
    private bool IsOptimalBlockSize(int blockSize, QueryOperation operation, ExecutionContext context)
        // Check if block size provides good occupancy
        var occupancy = EstimateOccupancy(blockSize, operation, context);
        return occupancy >= OptimalGpuOccupancy * 0.9; // Within 90% of target
    private double EstimateOccupancy(int blockSize, QueryOperation operation, ExecutionContext context)
            return 0.5;
        var maxActiveBlocks = gpuInfo.MaxThreadsPerSM / blockSize;
        var targetActiveBlocks = gpuInfo.MaxBlocksPerSM;
        return Math.Min(1.0, (double)maxActiveBlocks / targetActiveBlocks);
    private int CalculateOptimalGridSize(QueryOperation operation, ExecutionContext context)
        var blockSize = operation.GpuOptimizationConfig?.BlockSize ?? 256;
        return (int)Math.Ceiling((double)operation.InputSize / blockSize);
    private int CalculateSharedMemoryUsage(QueryOperation operation, Pipelines.Models.WorkloadCharacteristics workload)
        // Estimate shared memory requirements
            Models.OperationType.Reduce => 1024, // For reduction trees
            Models.OperationType.GroupBy => 2048, // For hash tables
            _ => 0
    private int EstimateRegisterUsage(QueryOperation operation)
        // Estimate register requirements per thread
            Models.OperationType.Map => 16,
            Models.OperationType.Filter => 12,
            Models.OperationType.Reduce => 24,
            Models.OperationType.GroupBy => 32,
            Models.OperationType.Join => 40,
            _ => 16
    private double CalculateMaxOccupancy(Models.GpuOptimizationConfig config, ExecutionContext context)
        // Simplified occupancy calculation
        return OptimalGpuOccupancy * 0.8; // Assume 80% of optimal
    private void AdjustConfigurationForOccupancy(Models.GpuOptimizationConfig config, ExecutionContext context)
        // Adjust block size to improve occupancy
        config.BlockSize = Math.Max(32, config.BlockSize - 32);
        // Reduce shared memory usage if needed
        config.SharedMemorySize = (int)(config.SharedMemorySize * 0.9);
public class DynamicScheduler
    {
    public Task<DynamicSchedule> CreateSchedule(QueryPlan plan, ExecutionContext context)
        return Task.FromResult(new DynamicSchedule
            SchedulingPolicy = SchedulingPolicy.Adaptive,
            PreemptionEnabled = true,
            PriorityLevels = 3,
            TimeSliceMs = 10
// Data structures
public class WorkloadProfile
    {
    public Dictionary<string, DotCompute.Linq.Pipelines.Models.WorkloadCharacteristics> OperationCharacteristics { get; set; } = [];
    public DotCompute.Linq.Pipelines.Models.WorkloadCharacteristics GetWorkloadCharacteristics(QueryOperation operation)
        return OperationCharacteristics.TryGetValue(operation.Id, out var characteristics)
            ? characteristics
            : new DotCompute.Linq.Pipelines.Models.WorkloadCharacteristics();
// Pipelines.Models.WorkloadCharacteristics moved to shared location to avoid duplicates
public class StrategiesParallelizationConfig
    {
    public int Degree { get; set; }
    public ParallelizationPattern Pattern { get; set; }
    public WorkDistribution WorkDistribution { get; set; } = new();
    public LoadBalancingStrategy LoadBalancingStrategy { get; set; }
    public SynchronizationStrategy SynchronizationStrategy { get; set; }
    public int GrainSize { get; set; }
    public int ChunkSize { get; set; }
    public bool LoadBalancingEnabled { get; set; }
public class WorkDistribution
    {
    public WorkDistributionType Type { get; set; }
    public int ReduceGrainSize { get; set; }
    public TaskDependencyGraph? TaskDependencies { get; set; }
    public int TreeDepth { get; set; }
    public static WorkDistribution Single => new() { Type = WorkDistributionType.Single };
public class StrategiesLoadBalancingConfig
    {
    public LoadBalancingStrategy Strategy { get; set; }
    public bool WorkStealingEnabled { get; set; }
    public double AdaptiveThreshold { get; set; }
    public TimeSpan MonitoringInterval { get; set; }
public class StrategiesGpuOptimizationConfig
    {
    public int BlockSize { get; set; }
    public int GridSize { get; set; }
    public int SharedMemoryUsage { get; set; }
    public int RegisterUsage { get; set; }
    public double OccupancyTarget { get; set; }
public class StrategiesCpuOptimizationConfig
    {
    public ThreadAffinityConfig ThreadAffinity { get; set; } = new();
    public List<VectorizationHint> VectorizationHints { get; set; } = [];
    public CacheOptimizationStrategy CacheOptimization { get; set; } = new();
    public HyperThreadingStrategy HyperThreadingStrategy { get; set; }
    public NumaConfiguration? NumaConfiguration { get; set; }
public class ThreadAffinityConfig
    {
    public bool UsePhysicalCores { get; set; }
    public List<int> PreferredCores { get; set; } = [];
    public bool AvoidHyperThreading { get; set; }
public class VectorizationHint
    {
    public VectorizationType Type { get; set; }
    public int VectorWidth { get; set; }
    public int AlignmentRequirement { get; set; }
    public int LoopUnrollFactor { get; set; }
public class CacheOptimizationStrategy
    {
    public int CacheLineSize { get; set; } = 64;
    public int PrefetchDistance { get; set; } = 8;
    public bool TemporalLocality { get; set; }
    public bool SpatialLocality { get; set; }
public class NumaConfiguration
    {
    public int PreferredNode { get; set; }
    public NumaThreadDistribution ThreadDistribution { get; set; }
    public NumaMemoryBinding MemoryBinding { get; set; }
public class TaskDependencyGraph
    {
    // Implementation details
public class MemoryTransferGroup
    {
public class DynamicSchedule
    {
    public SchedulingPolicy SchedulingPolicy { get; set; }
    public bool PreemptionEnabled { get; set; }
    public int PriorityLevels { get; set; }
    public int TimeSliceMs { get; set; }
// Enums
public enum ParallelizationPattern
    {
    Sequential,
    DataParallel,
    TaskParallel,
    MapReduce,
    TreeReduction,
    BroadcastJoin,
    HashJoin
public enum WorkDistributionType
    {
    Single,
    ChunkedStatic,
    ChunkedDynamic,
    TaskQueue,
    TreeReduction
public enum LoadBalancingStrategy
    {
    None,
    Static,
    Dynamic,
    WorkStealing
public enum SynchronizationStrategy
    {
    Barrier,
    EventBased,
    Phased,
    Hierarchical
public enum VectorizationType
    {
    SIMD,
    AVX2,
    AVX512
public enum HyperThreadingStrategy
    {
    Avoid,
    Utilize,
    Adaptive,
    Enabled,
    Disabled
public enum NumaThreadDistribution
    {
    /// <summary>No specific NUMA thread placement.</summary>
    /// <summary>Distribute threads evenly across NUMA nodes.</summary>
    Balanced,
    /// <summary>Interleave threads across NUMA nodes.</summary>
    Interleaved,
    /// <summary>Pack threads onto specific NUMA nodes.</summary>
    Packed
public enum NumaMemoryBinding
    {
    /// <summary>No specific memory binding.</summary>
    /// <summary>Bind memory to local NUMA node.</summary>
    Local,
    /// <summary>Bind memory to specific NUMA node.</summary>
    Specific,
    /// <summary>Interleave memory across NUMA nodes.</summary>
    Interleaved
public enum SchedulingPolicy
    {
    FIFO,
    RoundRobin,
    Adaptive
