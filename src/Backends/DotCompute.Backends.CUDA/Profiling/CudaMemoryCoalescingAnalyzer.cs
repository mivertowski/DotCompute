using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using global::System.Runtime.InteropServices;
using System.Threading.Tasks;
// using DotCompute.Backends.CUDA.Analysis.Enums; // Not needed for core functionality
using DotCompute.Backends.CUDA.Analysis.Models;
using DotCompute.Backends.CUDA.Analysis.Types;
// using DotCompute.Core.Models; // Commented out to avoid conflicts
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

// Use Analysis types directly as this is part of the CUDA backend
using CoalescingComparison = DotCompute.Backends.CUDA.Analysis.Types.CoalescingComparison;
using CoalescingAnalysis = DotCompute.Backends.CUDA.Analysis.Types.CoalescingAnalysis;
using StridedAccessAnalysis = DotCompute.Backends.CUDA.Analysis.Types.StridedAccessAnalysis;
using Matrix2DAccessAnalysis = DotCompute.Backends.CUDA.Analysis.Types.Matrix2DAccessAnalysis;
using RuntimeCoalescingProfile = DotCompute.Backends.CUDA.Analysis.Types.RuntimeCoalescingProfile;
// Use types from Abstractions for consistency
using MemoryAccessInfo = DotCompute.Abstractions.Types.MemoryAccessInfo;
using CoalescingIssue = DotCompute.Abstractions.Types.CoalescingIssue;
using TileAnalysis = DotCompute.Abstractions.Types.TileAnalysis;
using IssueType = DotCompute.Abstractions.Types.IssueType;
using IssueSeverity = DotCompute.Abstractions.Types.IssueSeverity;

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
            _accessPatterns = [];
            _metricsCache = [];
            
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
            analysis.WastedBandwidth = (long)CalculateWastedBandwidth(accessInfo);
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
            var warpSize = 32;
            var cacheLine = computeCapability >= 20 ? 128 : 32; // L1 cache line size
            
            // Calculate bytes accessed per warp
            var bytesPerWarp = warpSize * elementSize * stride;
            
            // Calculate transactions needed
            analysis.TransactionsPerWarp = (bytesPerWarp + cacheLine - 1) / cacheLine;
            
            // Calculate efficiency
            var usefulBytes = warpSize * elementSize;
            var transferredBytes = analysis.TransactionsPerWarp * cacheLine;
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
            DotCompute.Backends.CUDA.Analysis.Types.AccessOrder accessOrder,
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
            var cacheLine = computeCapability >= 20 ? 128 : 32;
            
            // Analyze based on access order
            if (accessOrder == DotCompute.Backends.CUDA.Analysis.Types.AccessOrder.RowMajor)
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
            var usefulBytes = blockDimX * blockDimY * elementSize;
            var transferredBytes = analysis.TransactionsPerBlock * cacheLine;
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
            for (var i = 0; i < warmupRuns; i++)
            {
                await kernelExecution();
            }

            // Profile runs with metrics collection
            var metrics = new List<MemoryMetrics>();
            
            for (var i = 0; i < profileRuns; i++)
            {
                var runMetrics = new MemoryMetrics();
                
                // Get memory info before
                cudaMemGetInfo(out var freeBefore, out var totalBefore);
                var startTime = Stopwatch.GetTimestamp();
                
                // Execute kernel
                await kernelExecution();
                
                // Get memory info after
                var elapsed = Stopwatch.GetElapsedTime(startTime);
                cudaMemGetInfo(out var freeAfter, out var totalAfter);
                
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

        // Private helper methods remain the same but reference the new types
        private static async Task<CoalescingAnalysis> AnalyzeModernArchitectureAsync(
            MemoryAccessInfo accessInfo,
            CoalescingAnalysis analysis)
        {
            // Implementation remains the same
            var cacheLine = 128;
            var sectorSize = 32;
            
            var sectorsNeeded = (accessInfo.AccessSize + sectorSize - 1) / sectorSize;
            var actualTransferSize = sectorsNeeded * sectorSize;
            
            var isAligned = (accessInfo.BaseAddress % cacheLine) == 0;
            var isSequential = accessInfo.Stride == 1;
            var isUniform = accessInfo.Stride == 0;
            
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

        private static async Task<CoalescingAnalysis> AnalyzeKeplerMaxwellAsync(
            MemoryAccessInfo accessInfo,
            CoalescingAnalysis analysis)
        {
            var cacheLine = 128;
            
            if (accessInfo.Stride == 1)
            {
                analysis.TransactionCount = (accessInfo.AccessSize + cacheLine - 1) / cacheLine;
            }
            else if (accessInfo.Stride <= 32)
            {
                analysis.TransactionCount = accessInfo.ThreadCount / (cacheLine / (accessInfo.ElementSize * accessInfo.Stride));
            }
            else
            {
                analysis.TransactionCount = accessInfo.ThreadCount;
            }
            
            analysis.ActualBytesTransferred = analysis.TransactionCount * cacheLine;
            analysis.UsefulBytesTransferred = accessInfo.AccessSize;
            
            analysis.ArchitectureNotes.Add("Kepler/Maxwell architecture");
            analysis.ArchitectureNotes.Add("L1 cache can be configured for texture operations");

            await Task.CompletedTask;
            return analysis;
        }

        private static async Task<CoalescingAnalysis> AnalyzeLegacyArchitectureAsync(
            MemoryAccessInfo accessInfo,
            CoalescingAnalysis analysis)
        {
            var transactionSize = accessInfo.ElementSize <= 4 ? 32 : 
                                 accessInfo.ElementSize <= 8 ? 64 : 128;
            
            var isPerfectlyCoalesced = 
                accessInfo.Stride == 1 && 
                (accessInfo.BaseAddress % transactionSize) == 0 &&
                accessInfo.ElementSize >= 4;
            
            if (isPerfectlyCoalesced)
            {
                analysis.TransactionCount = 1;
            }
            else
            {
                analysis.TransactionCount = accessInfo.ThreadCount;
            }
            
            analysis.ActualBytesTransferred = analysis.TransactionCount * transactionSize;
            analysis.UsefulBytesTransferred = accessInfo.AccessSize;
            
            analysis.ArchitectureNotes.Add("Fermi architecture with strict coalescing rules");
            analysis.ArchitectureNotes.Add($"Transaction size: {transactionSize} bytes");

            await Task.CompletedTask;
            return analysis;
        }

        private static double CalculateCoalescingEfficiency(MemoryAccessInfo accessInfo, int computeCapability)
        {
            if (accessInfo.Stride == 1 && accessInfo.ElementSize >= 4)
            {
                var alignmentRequirement = computeCapability >= 20 ? 128 : 32;
                var isAligned = (accessInfo.BaseAddress % alignmentRequirement) == 0;
                return isAligned ? 1.0 : 0.95;
            }
            
            if (accessInfo.Stride == 0)
            {
                return 0.03125; // 1/32 efficiency (broadcast)
            }
            
            if (accessInfo.Stride > 1)
            {
                return 1.0 / accessInfo.Stride;
            }
            
            return 1.0 / 32.0; // Worst case
        }

        private double CalculateWastedBandwidth(MemoryAccessInfo accessInfo)
        {
            var idealTransferSize = accessInfo.AccessSize;
            var actualTransferSize = CalculateActualTransferSize(accessInfo);
            
            var wastedBytes = actualTransferSize - idealTransferSize;
            var transferTime = accessInfo.ExecutionTime?.TotalSeconds ?? 1.0;
            
            return wastedBytes / transferTime;
        }

        private static int CalculateActualTransferSize(MemoryAccessInfo accessInfo)
        {
            if (accessInfo.Stride == 1)
            {
                var cacheLine = 128;
                return ((accessInfo.AccessSize + cacheLine - 1) / cacheLine) * cacheLine;
            }
            else
            {
                return accessInfo.ThreadCount * 128;
            }
        }

        private static int GetOptimalAccessSize(int computeCapability)
        {
            if (computeCapability >= 50)
            {
                return 128;
            }

            else if (computeCapability >= 30)
            {
                return 128;
            }
            else
            {
                return 32;
            }
        }

        private static List<CoalescingIssue> IdentifyCoalescingIssues(
            MemoryAccessInfo accessInfo,
            int computeCapability)
        {
            var issues = new List<CoalescingIssue>();

            var alignmentReq = computeCapability >= 20 ? 128 : 32;
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

        private static List<string> GenerateOptimizations(
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

            if (accessInfo.AccessSize > 1024 * 1024)
            {
                optimizations.Add("Consider using async memory operations with streams");
                optimizations.Add("Overlap computation with memory transfers");
            }

            return optimizations.Distinct().ToList();
        }

        private static TileAnalysis AnalyzeTileEfficiency(
            int rows, int cols,
            int blockDimX, int blockDimY,
            int elementSize,
            int computeCapability)
        {
            var analysis = new TileAnalysis();
            
            var maxSharedMemory = computeCapability >= 70 ? 96 * 1024 :
                                  computeCapability >= 50 ? 48 * 1024 :
                                  16 * 1024;
            
            int[] tileSizes = { 8, 16, 32, 64 };
            double bestEfficiency = 0;
            
            foreach (var tileSize in tileSizes)
            {
                var sharedMemNeeded = tileSize * tileSize * elementSize;
                
                if (sharedMemNeeded <= maxSharedMemory)
                {
                    var reuse = (double)(2 * tileSize) / (tileSize * tileSize);
                    var efficiency = reuse * (sharedMemNeeded <= maxSharedMemory / 2 ? 1.0 : 0.8);
                    
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

        private static List<string> GenerateComparisonRecommendations(CoalescingComparison comparison)
        {
            var recommendations = new List<string>();
            
            if (comparison.ImprovementPotential > 0.1)
            {
                recommendations.Add($"Adopt access pattern from '{comparison.BestPattern}' for {comparison.ImprovementPotential:P} improvement");
            }
            
            var commonIssues = new Dictionary<IssueType, int>();
            foreach (var analysis in comparison.Analyses.Values)
            {
                foreach (var issue in analysis.Issues)
                {
                    commonIssues[issue.Type] = commonIssues.GetValueOrDefault(issue.Type) + 1;
                }
            }
            
            foreach (var (issueType, count) in commonIssues.Where(i => i.Value > comparison.Analyses.Count / 2))
            {
                recommendations.Add($"Address {issueType} affecting {count}/{comparison.Analyses.Count} kernels");
            }
            
            return recommendations;
        }

        private static async Task<int> GetComputeCapabilityAsync(int deviceId)
        {
            cudaDeviceGetAttribute(out var major, CudaDeviceAttribute.ComputeCapabilityMajor, deviceId);
            cudaDeviceGetAttribute(out var minor, CudaDeviceAttribute.ComputeCapabilityMinor, deviceId);
            
            await Task.CompletedTask;
            return major * 10 + minor;
        }

        private static double CalculateVariance(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (!list.Any())
            {
                return 0;
            }


            var mean = list.Average();
            return list.Average(v => Math.Pow(v - mean, 2));
        }

        // Private helper classes
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

        // Private enums for P/Invoke
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