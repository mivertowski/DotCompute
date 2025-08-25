using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Analysis
{
    /// <summary>
    /// Production-grade CUDA memory coalescing analyzer for identifying and optimizing
    /// memory access patterns to maximize bandwidth utilization.
    /// </summary>
    public sealed class CudaMemoryCoalescingAnalyzer
    {
        private readonly ILogger<CudaMemoryCoalescingAnalyzer> _logger;
        private readonly List<AccessPattern> _accessPatterns;
        private readonly Dictionary<string, CoalescingMetrics> _metricsCache;

        // CUDA API imports
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaDeviceGetAttribute(
            out int value,
            CudaDeviceAttribute attr,
            int device);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaMemGetInfo(out ulong free, out ulong total);

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlDeviceGetMemoryBusWidth(
            IntPtr device, out uint busWidth);

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlDeviceGetPcieThroughput(
            IntPtr device, NvmlPcieUtilCounter counter, out uint value);

        public CudaMemoryCoalescingAnalyzer(ILogger<CudaMemoryCoalescingAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accessPatterns = new List<AccessPattern>();
            _metricsCache = new Dictionary<string, CoalescingMetrics>();
            
            _logger.LogInformation("CUDA Memory Coalescing Analyzer initialized");
        }

        /// <summary>
        /// Analyzes memory access pattern for coalescing efficiency.
        /// </summary>
        public async Task<CoalescingAnalysis> AnalyzeAccessPatternAsync(
            MemoryAccessInfo accessInfo,
            int deviceId = 0)
        {
            _logger.LogDebug("Analyzing memory access pattern for {KernelName}", accessInfo.KernelName);

            var analysis = new CoalescingAnalysis
            {
                KernelName = accessInfo.KernelName,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Get device compute capability
            var computeCapability = await GetComputeCapabilityAsync(deviceId);
            
            // Analyze based on architecture
            if (computeCapability >= 60) // Pascal and newer
            {
                analysis = await AnalyzeModernArchitectureAsync(accessInfo, analysis);
            }
            else if (computeCapability >= 30) // Kepler to Maxwell
            {
                analysis = await AnalyzeKeplerMaxwellAsync(accessInfo, analysis);
            }
            else // Fermi and older
            {
                analysis = await AnalyzeLegacyArchitectureAsync(accessInfo, analysis);
            }

            // Calculate efficiency metrics
            analysis.CoalescingEfficiency = CalculateCoalescingEfficiency(accessInfo, computeCapability);
            analysis.WastedBandwidth = CalculateWastedBandwidth(accessInfo);
            analysis.OptimalAccessSize = GetOptimalAccessSize(computeCapability);
            
            // Identify specific issues
            analysis.Issues = IdentifyCoalescingIssues(accessInfo, computeCapability);
            
            // Generate optimization suggestions
            analysis.Optimizations = GenerateOptimizations(accessInfo, analysis.Issues);

            // Cache metrics for trend analysis
            _metricsCache[accessInfo.KernelName] = new CoalescingMetrics
            {
                Efficiency = analysis.CoalescingEfficiency,
                WastedBandwidth = analysis.WastedBandwidth,
                Timestamp = analysis.Timestamp
            };

            _logger.LogInformation(
                "Coalescing analysis for {KernelName}: Efficiency={Efficiency:P}, Wasted BW={WastedBW:F2} GB/s",
                accessInfo.KernelName,
                analysis.CoalescingEfficiency,
                analysis.WastedBandwidth / 1e9);

            return analysis;
        }

        /// <summary>
        /// Analyzes strided memory access patterns.
        /// </summary>
        public async Task<StridedAccessAnalysis> AnalyzeStridedAccessAsync(
            int stride,
            int elementSize,
            int threadCount,
            int deviceId = 0)
        {
            var analysis = new StridedAccessAnalysis
            {
                Stride = stride,
                ElementSize = elementSize,
                ThreadCount = threadCount
            };

            var computeCapability = await GetComputeCapabilityAsync(deviceId);
            
            // Calculate number of transactions needed
            int warpSize = 32;
            int cacheLine = computeCapability >= 20 ? 128 : 32; // L1 cache line size
            
            // Calculate bytes accessed per warp
            int bytesPerWarp = warpSize * elementSize * stride;
            
            // Calculate transactions needed
            analysis.TransactionsPerWarp = (bytesPerWarp + cacheLine - 1) / cacheLine;
            
            // Calculate efficiency
            int usefulBytes = warpSize * elementSize;
            int transferredBytes = analysis.TransactionsPerWarp * cacheLine;
            analysis.Efficiency = (double)usefulBytes / transferredBytes;
            
            // Determine if access is coalesced
            analysis.IsCoalesced = stride == 1 && 
                                   (elementSize == 4 || elementSize == 8 || elementSize == 16);
            
            // Calculate bandwidth utilization
            analysis.BandwidthUtilization = analysis.IsCoalesced ? 1.0 : analysis.Efficiency;
            
            // Generate recommendations
            if (!analysis.IsCoalesced)
            {
                if (stride > 1)
                {
                    analysis.Recommendations.Add($"Stride of {stride} causes uncoalesced access");
                    analysis.Recommendations.Add("Consider data structure reorganization (AoS to SoA)");
                    analysis.Recommendations.Add($"Each warp requires {analysis.TransactionsPerWarp} transactions instead of 1");
                }
                
                if (elementSize % 4 != 0)
                {
                    analysis.Recommendations.Add($"Element size {elementSize} is not aligned to 4 bytes");
                    analysis.Recommendations.Add("Consider padding data structures for alignment");
                }
            }

            _logger.LogDebug(
                "Strided access analysis: Stride={Stride}, Efficiency={Efficiency:P}, Transactions={Trans}",
                stride, analysis.Efficiency, analysis.TransactionsPerWarp);

            return analysis;
        }

        /// <summary>
        /// Analyzes 2D memory access patterns (common in matrix operations).
        /// </summary>
        public async Task<Matrix2DAccessAnalysis> Analyze2DAccessPatternAsync(
            int rows,
            int cols,
            AccessOrder accessOrder,
            int elementSize,
            int blockDimX,
            int blockDimY,
            int deviceId = 0)
        {
            var analysis = new Matrix2DAccessAnalysis
            {
                Rows = rows,
                Columns = cols,
                AccessOrder = accessOrder,
                ElementSize = elementSize,
                BlockDimX = blockDimX,
                BlockDimY = blockDimY
            };

            var computeCapability = await GetComputeCapabilityAsync(deviceId);
            int cacheLine = computeCapability >= 20 ? 128 : 32;
            
            // Analyze based on access order
            if (accessOrder == AccessOrder.RowMajor)
            {
                // Row-major access with row-major storage is optimal
                analysis.IsOptimal = true;
                analysis.CoalescingFactor = 1.0;
                analysis.TransactionsPerBlock = (blockDimX * elementSize + cacheLine - 1) / cacheLine;
            }
            else // Column-major
            {
                // Column-major access with row-major storage causes strided access
                analysis.IsOptimal = false;
                analysis.CoalescingFactor = 1.0 / blockDimX; // Worst case
                analysis.TransactionsPerBlock = blockDimX * blockDimY; // One per thread
            }

            // Calculate bandwidth efficiency
            int usefulBytes = blockDimX * blockDimY * elementSize;
            int transferredBytes = analysis.TransactionsPerBlock * cacheLine;
            analysis.BandwidthEfficiency = (double)usefulBytes / transferredBytes;

            // Analyze tile efficiency for shared memory optimization
            analysis.TileAnalysis = AnalyzeTileEfficiency(
                rows, cols, blockDimX, blockDimY, elementSize, computeCapability);

            // Generate optimization strategies
            if (!analysis.IsOptimal)
            {
                analysis.Optimizations.Add("Transpose matrix for coalesced access");
                analysis.Optimizations.Add($"Use shared memory tiling with {analysis.TileAnalysis.OptimalTileSize}x{analysis.TileAnalysis.OptimalTileSize} tiles");
                analysis.Optimizations.Add("Consider texture memory for 2D spatial locality");
                
                if (elementSize < 16)
                {
                    analysis.Optimizations.Add("Use vector loads (float2/float4) to improve throughput");
                }
            }

            _logger.LogInformation(
                "2D access analysis: {Order} access, Efficiency={Efficiency:P}, Optimal={Optimal}",
                accessOrder, analysis.BandwidthEfficiency, analysis.IsOptimal);

            return analysis;
        }

        /// <summary>
        /// Profiles actual memory access patterns during kernel execution.
        /// </summary>
        public async Task<RuntimeCoalescingProfile> ProfileRuntimeAccessAsync(
            Func<Task> kernelExecution,
            string kernelName,
            int warmupRuns = 3,
            int profileRuns = 10)
        {
            _logger.LogDebug("Profiling runtime memory access for {KernelName}", kernelName);

            var profile = new RuntimeCoalescingProfile
            {
                KernelName = kernelName,
                ProfileStartTime = DateTimeOffset.UtcNow
            };

            // Warmup runs
            for (int i = 0; i < warmupRuns; i++)
            {
                await kernelExecution();
            }

            // Profile runs with metrics collection
            var metrics = new List<MemoryMetrics>();
            
            for (int i = 0; i < profileRuns; i++)
            {
                var runMetrics = new MemoryMetrics();
                
                // Get memory info before
                cudaMemGetInfo(out ulong freeBefore, out ulong totalBefore);
                var startTime = Stopwatch.GetTimestamp();
                
                // Execute kernel
                await kernelExecution();
                
                // Get memory info after
                var elapsed = Stopwatch.GetElapsedTime(startTime);
                cudaMemGetInfo(out ulong freeAfter, out ulong totalAfter);
                
                runMetrics.ExecutionTime = elapsed;
                runMetrics.MemoryUsed = (long)(freeBefore - freeAfter);
                runMetrics.Timestamp = DateTimeOffset.UtcNow;
                
                metrics.Add(runMetrics);
            }

            // Analyze collected metrics
            profile.AverageExecutionTime = TimeSpan.FromMilliseconds(
                metrics.Average(m => m.ExecutionTime.TotalMilliseconds));
            profile.MinExecutionTime = metrics.Min(m => m.ExecutionTime);
            profile.MaxExecutionTime = metrics.Max(m => m.ExecutionTime);
            
            // Calculate memory throughput
            if (metrics.Any(m => m.MemoryUsed > 0))
            {
                var avgMemoryUsed = metrics.Average(m => m.MemoryUsed);
                profile.EstimatedBandwidth = avgMemoryUsed / profile.AverageExecutionTime.TotalSeconds;
            }

            // Estimate coalescing efficiency based on execution time variance
            var timeVariance = CalculateVariance(metrics.Select(m => m.ExecutionTime.TotalMilliseconds));
            profile.EstimatedCoalescingEfficiency = 1.0 - (timeVariance / profile.AverageExecutionTime.TotalMilliseconds);
            profile.EstimatedCoalescingEfficiency = Math.Max(0, Math.Min(1, profile.EstimatedCoalescingEfficiency));

            _logger.LogInformation(
                "Runtime profile for {KernelName}: Avg={AvgTime:F3}ms, Bandwidth={BW:F2} GB/s, Coalescing={Coal:P}",
                kernelName,
                profile.AverageExecutionTime.TotalMilliseconds,
                profile.EstimatedBandwidth / 1e9,
                profile.EstimatedCoalescingEfficiency);

            return profile;
        }

        /// <summary>
        /// Compares coalescing efficiency across different access patterns.
        /// </summary>
        public async Task<CoalescingComparison> CompareAccessPatternsAsync(
            List<MemoryAccessInfo> patterns,
            int deviceId = 0)
        {
            var comparison = new CoalescingComparison();
            
            foreach (var pattern in patterns)
            {
                var analysis = await AnalyzeAccessPatternAsync(pattern, deviceId);
                
                comparison.Analyses.Add(pattern.KernelName, analysis);
                
                // Track best and worst
                if (comparison.BestPattern == null || 
                    analysis.CoalescingEfficiency > comparison.BestEfficiency)
                {
                    comparison.BestPattern = pattern.KernelName;
                    comparison.BestEfficiency = analysis.CoalescingEfficiency;
                }
                
                if (comparison.WorstPattern == null || 
                    analysis.CoalescingEfficiency < comparison.WorstEfficiency)
                {
                    comparison.WorstPattern = pattern.KernelName;
                    comparison.WorstEfficiency = analysis.CoalescingEfficiency;
                }
            }

            // Calculate improvement potential
            if (comparison.BestEfficiency > 0 && comparison.WorstEfficiency > 0)
            {
                comparison.ImprovementPotential = 
                    (comparison.BestEfficiency - comparison.WorstEfficiency) / comparison.WorstEfficiency;
            }

            // Generate recommendations
            comparison.Recommendations = GenerateComparisonRecommendations(comparison);

            return comparison;
        }

        /// <summary>
        /// Analyzes memory access for modern GPU architectures (Pascal+).
        /// </summary>
        private async Task<CoalescingAnalysis> AnalyzeModernArchitectureAsync(
            MemoryAccessInfo accessInfo,
            CoalescingAnalysis analysis)
        {
            // Modern GPUs have more relaxed coalescing requirements
            // L1 cache line is 128 bytes
            int cacheLine = 128;
            int sectorSize = 32; // Minimum transfer size
            
            // Calculate sectors needed
            int sectorsNeeded = (accessInfo.AccessSize + sectorSize - 1) / sectorSize;
            int actualTransferSize = sectorsNeeded * sectorSize;
            
            // Check alignment
            bool isAligned = (accessInfo.BaseAddress % cacheLine) == 0;
            
            // Check access pattern
            bool isSequential = accessInfo.Stride == 1;
            bool isUniform = accessInfo.Stride == 0; // All threads access same address
            
            analysis.TransactionCount = isUniform ? 1 : 
                                        isSequential ? sectorsNeeded : 
                                        accessInfo.ThreadCount;
            
            analysis.ActualBytesTransferred = analysis.TransactionCount * sectorSize;
            analysis.UsefulBytesTransferred = accessInfo.AccessSize;
            
            analysis.ArchitectureNotes.Add("Pascal+ architecture with relaxed coalescing");
            analysis.ArchitectureNotes.Add($"L1 cache line: {cacheLine} bytes");
            analysis.ArchitectureNotes.Add($"Sector size: {sectorSize} bytes");
            
            if (!isAligned)
            {
                analysis.ArchitectureNotes.Add("Unaligned access may cause additional transactions");
            }

            await Task.CompletedTask;
            return analysis;
        }

        /// <summary>
        /// Analyzes memory access for Kepler/Maxwell architectures.
        /// </summary>
        private async Task<CoalescingAnalysis> AnalyzeKeplerMaxwellAsync(
            MemoryAccessInfo accessInfo,
            CoalescingAnalysis analysis)
        {
            // Kepler/Maxwell have 128-byte cache lines
            int cacheLine = 128;
            
            // Calculate transactions based on access pattern
            if (accessInfo.Stride == 1)
            {
                // Sequential access - optimal
                analysis.TransactionCount = (accessInfo.AccessSize + cacheLine - 1) / cacheLine;
            }
            else if (accessInfo.Stride <= 32)
            {
                // Strided but within reasonable range
                analysis.TransactionCount = accessInfo.ThreadCount / (cacheLine / (accessInfo.ElementSize * accessInfo.Stride));
            }
            else
            {
                // Random or highly strided - worst case
                analysis.TransactionCount = accessInfo.ThreadCount;
            }
            
            analysis.ActualBytesTransferred = analysis.TransactionCount * cacheLine;
            analysis.UsefulBytesTransferred = accessInfo.AccessSize;
            
            analysis.ArchitectureNotes.Add("Kepler/Maxwell architecture");
            analysis.ArchitectureNotes.Add("L1 cache can be configured for texture operations");

            await Task.CompletedTask;
            return analysis;
        }

        /// <summary>
        /// Analyzes memory access for legacy architectures (Fermi).
        /// </summary>
        private async Task<CoalescingAnalysis> AnalyzeLegacyArchitectureAsync(
            MemoryAccessInfo accessInfo,
            CoalescingAnalysis analysis)
        {
            // Fermi has strict coalescing requirements
            int transactionSize = accessInfo.ElementSize <= 4 ? 32 : 
                                 accessInfo.ElementSize <= 8 ? 64 : 128;
            
            // Check for perfect coalescing
            bool isPerfectlyCoalesced = 
                accessInfo.Stride == 1 && 
                (accessInfo.BaseAddress % transactionSize) == 0 &&
                accessInfo.ElementSize >= 4;
            
            if (isPerfectlyCoalesced)
            {
                analysis.TransactionCount = 1;
            }
            else
            {
                // Penalty for uncoalesced access
                analysis.TransactionCount = accessInfo.ThreadCount;
            }
            
            analysis.ActualBytesTransferred = analysis.TransactionCount * transactionSize;
            analysis.UsefulBytesTransferred = accessInfo.AccessSize;
            
            analysis.ArchitectureNotes.Add("Fermi architecture with strict coalescing rules");
            analysis.ArchitectureNotes.Add($"Transaction size: {transactionSize} bytes");

            await Task.CompletedTask;
            return analysis;
        }

        /// <summary>
        /// Calculates coalescing efficiency percentage.
        /// </summary>
        private double CalculateCoalescingEfficiency(MemoryAccessInfo accessInfo, int computeCapability)
        {
            // Perfect coalescing conditions
            if (accessInfo.Stride == 1 && accessInfo.ElementSize >= 4)
            {
                // Check alignment
                int alignmentRequirement = computeCapability >= 20 ? 128 : 32;
                bool isAligned = (accessInfo.BaseAddress % alignmentRequirement) == 0;
                
                return isAligned ? 1.0 : 0.95; // Small penalty for misalignment
            }
            
            // Uniform access (all threads access same location)
            if (accessInfo.Stride == 0)
            {
                return 0.03125; // 1/32 efficiency (broadcast)
            }
            
            // Strided access
            if (accessInfo.Stride > 1)
            {
                return 1.0 / accessInfo.Stride; // Efficiency decreases with stride
            }
            
            // Random access
            return 1.0 / 32.0; // Worst case
        }

        /// <summary>
        /// Calculates wasted bandwidth in bytes/second.
        /// </summary>
        private double CalculateWastedBandwidth(MemoryAccessInfo accessInfo)
        {
            int idealTransferSize = accessInfo.AccessSize;
            int actualTransferSize = CalculateActualTransferSize(accessInfo);
            
            int wastedBytes = actualTransferSize - idealTransferSize;
            double transferTime = accessInfo.ExecutionTime?.TotalSeconds ?? 1.0;
            
            return wastedBytes / transferTime;
        }

        /// <summary>
        /// Calculates actual transfer size including overhead.
        /// </summary>
        private int CalculateActualTransferSize(MemoryAccessInfo accessInfo)
        {
            // Simplified calculation - would need profiling for accurate numbers
            if (accessInfo.Stride == 1)
            {
                // Coalesced access - minimal overhead
                int cacheLine = 128;
                return ((accessInfo.AccessSize + cacheLine - 1) / cacheLine) * cacheLine;
            }
            else
            {
                // Uncoalesced - each thread causes separate transaction
                return accessInfo.ThreadCount * 128; // Assume 128-byte transactions
            }
        }

        /// <summary>
        /// Gets optimal access size for architecture.
        /// </summary>
        private int GetOptimalAccessSize(int computeCapability)
        {
            if (computeCapability >= 50) // Maxwell and newer
                return 128; // Full cache line
            else if (computeCapability >= 30) // Kepler
                return 128;
            else // Fermi and older
                return 32;
        }

        /// <summary>
        /// Identifies specific coalescing issues.
        /// </summary>
        private List<CoalescingIssue> IdentifyCoalescingIssues(
            MemoryAccessInfo accessInfo,
            int computeCapability)
        {
            var issues = new List<CoalescingIssue>();

            // Check alignment
            int alignmentReq = computeCapability >= 20 ? 128 : 32;
            if (accessInfo.BaseAddress % alignmentReq != 0)
            {
                issues.Add(new CoalescingIssue
                {
                    Type = IssueType.Misalignment,
                    Severity = IssueSeverity.Medium,
                    Description = $"Base address not aligned to {alignmentReq}-byte boundary",
                    Impact = "10-20% performance loss"
                });
            }

            // Check stride
            if (accessInfo.Stride > 1)
            {
                issues.Add(new CoalescingIssue
                {
                    Type = IssueType.StridedAccess,
                    Severity = accessInfo.Stride > 32 ? IssueSeverity.High : IssueSeverity.Medium,
                    Description = $"Strided access with stride {accessInfo.Stride}",
                    Impact = $"{(1.0 / accessInfo.Stride):P} efficiency"
                });
            }

            // Check element size
            if (accessInfo.ElementSize < 4)
            {
                issues.Add(new CoalescingIssue
                {
                    Type = IssueType.SmallElements,
                    Severity = IssueSeverity.Low,
                    Description = $"Element size {accessInfo.ElementSize} bytes is suboptimal",
                    Impact = "Underutilized memory bandwidth"
                });
            }

            // Check access pattern
            if (accessInfo.IsRandom)
            {
                issues.Add(new CoalescingIssue
                {
                    Type = IssueType.RandomAccess,
                    Severity = IssueSeverity.High,
                    Description = "Random memory access pattern detected",
                    Impact = "Up to 32x bandwidth overhead"
                });
            }

            return issues;
        }

        /// <summary>
        /// Generates optimization suggestions based on issues.
        /// </summary>
        private List<string> GenerateOptimizations(
            MemoryAccessInfo accessInfo,
            List<CoalescingIssue> issues)
        {
            var optimizations = new List<string>();

            foreach (var issue in issues)
            {
                switch (issue.Type)
                {
                    case IssueType.Misalignment:
                        optimizations.Add("Use aligned memory allocation (cudaMallocPitch or aligned allocators)");
                        optimizations.Add("Adjust data structure padding for alignment");
                        break;
                        
                    case IssueType.StridedAccess:
                        optimizations.Add("Reorganize data layout from AoS (Array of Structures) to SoA (Structure of Arrays)");
                        optimizations.Add("Use shared memory to stage strided accesses");
                        optimizations.Add("Consider texture memory for 2D spatial locality");
                        break;
                        
                    case IssueType.SmallElements:
                        optimizations.Add($"Pack multiple {accessInfo.ElementSize}-byte elements into larger types");
                        optimizations.Add("Use vector types (int2, float4) for better throughput");
                        break;
                        
                    case IssueType.RandomAccess:
                        optimizations.Add("Sort or reorder data to improve locality");
                        optimizations.Add("Use texture cache or constant memory for random read patterns");
                        optimizations.Add("Consider using __ldg() intrinsic for read-only data");
                        break;
                }
            }

            // General optimizations
            if (accessInfo.AccessSize > 1024 * 1024) // Large transfers
            {
                optimizations.Add("Consider using async memory operations with streams");
                optimizations.Add("Overlap computation with memory transfers");
            }

            return optimizations.Distinct().ToList();
        }

        /// <summary>
        /// Analyzes tile efficiency for shared memory optimization.
        /// </summary>
        private TileAnalysis AnalyzeTileEfficiency(
            int rows, int cols,
            int blockDimX, int blockDimY,
            int elementSize,
            int computeCapability)
        {
            var analysis = new TileAnalysis();
            
            // Shared memory limits
            int maxSharedMemory = computeCapability >= 70 ? 96 * 1024 : // Volta+
                                  computeCapability >= 50 ? 48 * 1024 : // Maxwell
                                  16 * 1024; // Older
            
            // Find optimal tile size
            int[] tileSizes = { 8, 16, 32, 64 };
            double bestEfficiency = 0;
            
            foreach (int tileSize in tileSizes)
            {
                int sharedMemNeeded = tileSize * tileSize * elementSize;
                
                if (sharedMemNeeded <= maxSharedMemory)
                {
                    // Calculate efficiency
                    double reuse = (double)(2 * tileSize) / (tileSize * tileSize);
                    double efficiency = reuse * (sharedMemNeeded <= maxSharedMemory / 2 ? 1.0 : 0.8);
                    
                    if (efficiency > bestEfficiency)
                    {
                        bestEfficiency = efficiency;
                        analysis.OptimalTileSize = tileSize;
                    }
                }
            }
            
            analysis.SharedMemoryRequired = analysis.OptimalTileSize * analysis.OptimalTileSize * elementSize;
            analysis.Efficiency = bestEfficiency;
            
            return analysis;
        }

        /// <summary>
        /// Generates recommendations from comparison.
        /// </summary>
        private List<string> GenerateComparisonRecommendations(CoalescingComparison comparison)
        {
            var recommendations = new List<string>();
            
            if (comparison.ImprovementPotential > 0.1) // 10% improvement possible
            {
                recommendations.Add($"Adopt access pattern from '{comparison.BestPattern}' for {comparison.ImprovementPotential:P} improvement");
            }
            
            // Find common issues
            var commonIssues = new Dictionary<IssueType, int>();
            foreach (var analysis in comparison.Analyses.Values)
            {
                foreach (var issue in analysis.Issues)
                {
                    commonIssues[issue.Type] = commonIssues.GetValueOrDefault(issue.Type) + 1;
                }
            }
            
            // Recommend fixes for common issues
            foreach (var (issueType, count) in commonIssues.Where(i => i.Value > comparison.Analyses.Count / 2))
            {
                recommendations.Add($"Address {issueType} affecting {count}/{comparison.Analyses.Count} kernels");
            }
            
            return recommendations;
        }

        /// <summary>
        /// Gets compute capability for device.
        /// </summary>
        private async Task<int> GetComputeCapabilityAsync(int deviceId)
        {
            cudaDeviceGetAttribute(out int major, CudaDeviceAttribute.ComputeCapabilityMajor, deviceId);
            cudaDeviceGetAttribute(out int minor, CudaDeviceAttribute.ComputeCapabilityMinor, deviceId);
            
            await Task.CompletedTask;
            return major * 10 + minor;
        }

        /// <summary>
        /// Calculates variance for a collection of values.
        /// </summary>
        private double CalculateVariance(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (!list.Any()) return 0;
            
            double mean = list.Average();
            return list.Average(v => Math.Pow(v - mean, 2));
        }

        // Supporting classes
        public class MemoryAccessInfo
        {
            public required string KernelName { get; init; }
            public long BaseAddress { get; set; }
            public int AccessSize { get; set; }
            public int ElementSize { get; set; }
            public int ThreadCount { get; set; }
            public int Stride { get; set; } = 1;
            public bool IsRandom { get; set; }
            public TimeSpan? ExecutionTime { get; set; }
        }

        public class CoalescingAnalysis
        {
            public required string KernelName { get; init; }
            public DateTimeOffset Timestamp { get; init; }
            public double CoalescingEfficiency { get; set; }
            public double WastedBandwidth { get; set; }
            public int OptimalAccessSize { get; set; }
            public int TransactionCount { get; set; }
            public int ActualBytesTransferred { get; set; }
            public int UsefulBytesTransferred { get; set; }
            public List<CoalescingIssue> Issues { get; set; } = new();
            public List<string> Optimizations { get; set; } = new();
            public List<string> ArchitectureNotes { get; set; } = new();
        }

        public class CoalescingIssue
        {
            public IssueType Type { get; set; }
            public IssueSeverity Severity { get; set; }
            public required string Description { get; init; }
            public required string Impact { get; init; }
        }

        public class StridedAccessAnalysis
        {
            public int Stride { get; set; }
            public int ElementSize { get; set; }
            public int ThreadCount { get; set; }
            public int TransactionsPerWarp { get; set; }
            public double Efficiency { get; set; }
            public double BandwidthUtilization { get; set; }
            public bool IsCoalesced { get; set; }
            public List<string> Recommendations { get; } = new();
        }

        public class Matrix2DAccessAnalysis
        {
            public int Rows { get; set; }
            public int Columns { get; set; }
            public AccessOrder AccessOrder { get; set; }
            public int ElementSize { get; set; }
            public int BlockDimX { get; set; }
            public int BlockDimY { get; set; }
            public bool IsOptimal { get; set; }
            public double CoalescingFactor { get; set; }
            public double BandwidthEfficiency { get; set; }
            public int TransactionsPerBlock { get; set; }
            public TileAnalysis TileAnalysis { get; set; } = new();
            public List<string> Optimizations { get; } = new();
        }

        public class RuntimeCoalescingProfile
        {
            public required string KernelName { get; init; }
            public DateTimeOffset ProfileStartTime { get; init; }
            public TimeSpan AverageExecutionTime { get; set; }
            public TimeSpan MinExecutionTime { get; set; }
            public TimeSpan MaxExecutionTime { get; set; }
            public double EstimatedBandwidth { get; set; }
            public double EstimatedCoalescingEfficiency { get; set; }
        }

        public class CoalescingComparison
        {
            public Dictionary<string, CoalescingAnalysis> Analyses { get; } = new();
            public string? BestPattern { get; set; }
            public string? WorstPattern { get; set; }
            public double BestEfficiency { get; set; }
            public double WorstEfficiency { get; set; }
            public double ImprovementPotential { get; set; }
            public List<string> Recommendations { get; set; } = new();
        }

        public class TileAnalysis
        {
            public int OptimalTileSize { get; set; }
            public int SharedMemoryRequired { get; set; }
            public double Efficiency { get; set; }
        }

        private class AccessPattern
        {
            public string Name { get; set; } = "";
            public int Stride { get; set; }
            public int ElementSize { get; set; }
            public double Efficiency { get; set; }
        }

        private class CoalescingMetrics
        {
            public double Efficiency { get; set; }
            public double WastedBandwidth { get; set; }
            public DateTimeOffset Timestamp { get; set; }
        }

        private class MemoryMetrics
        {
            public TimeSpan ExecutionTime { get; set; }
            public long MemoryUsed { get; set; }
            public DateTimeOffset Timestamp { get; set; }
        }

        public enum IssueType
        {
            Misalignment,
            StridedAccess,
            SmallElements,
            RandomAccess,
            BankConflict,
            Divergence
        }

        public enum IssueSeverity
        {
            Low,
            Medium,
            High,
            Critical
        }

        public enum AccessOrder
        {
            RowMajor,
            ColumnMajor
        }

        // CUDA enums
        private enum CudaError
        {
            Success = 0
        }

        private enum CudaDeviceAttribute
        {
            ComputeCapabilityMajor = 75,
            ComputeCapabilityMinor = 76
        }

        private enum NvmlReturn
        {
            Success = 0
        }

        private enum NvmlPcieUtilCounter
        {
            TxBytes = 0,
            RxBytes = 1
        }
    }
}