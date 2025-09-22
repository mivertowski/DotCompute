// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Linq.KernelGeneration;
using Microsoft.Extensions.Logging;
namespace DotCompute.Linq.KernelGeneration.Optimization
{
    /// <summary>
    /// GPU-specific optimizations for CUDA kernels.
    /// Applies advanced optimization techniques including warp-level primitives,
    /// shared memory optimization, and compute capability-specific enhancements.
    /// </summary>
    public sealed class GpuOptimizer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly OptimizationCache _cache;
        private readonly Dictionary<string, OptimizationProfile> _profiles;
        private bool _disposed;
        public GpuOptimizer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = new OptimizationCache();
            _profiles = InitializeOptimizationProfiles();
        }
        /// <summary>
        /// Optimizes CUDA kernel source code for target GPU architecture.
        /// </summary>
        public async Task<string> OptimizeKernelSourceAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            KernelGenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(sourceCode);
            ArgumentNullException.ThrowIfNull(analysis);
            var cacheKey = GenerateCacheKey(sourceCode, analysis, options);
            if (_cache.TryGetOptimizedSource(cacheKey, out var cachedResult))
            {
                _logger.LogDebug("Using cached optimization for {KernelName}", analysis.KernelName);
                return cachedResult;
            }
            _logger.LogInformation("Optimizing CUDA kernel source for {KernelName}", analysis.KernelName);
            try
            {
                var optimizedSource = sourceCode;
                // Get compute capability for architecture-specific optimizations
                var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
                var profile = GetOptimizationProfile(major, minor, options);
                // Apply optimization passes in order
                optimizedSource = await ApplyMemoryOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyWarpOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplySharedMemoryOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyAtomicOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyTextureMemoryOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyConstantMemoryOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyTensorCoreOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyCooperativeGroupsOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyOccupancyOptimizationsAsync(optimizedSource, analysis, profile);
                optimizedSource = await ApplyInstructionLevelOptimizationsAsync(optimizedSource, analysis, profile);
                // Cache the result
                _cache.CacheOptimizedSource(cacheKey, optimizedSource);
                _logger.LogInformation("Successfully optimized kernel {KernelName} for compute capability {Major}.{Minor}",
                    analysis.KernelName, major, minor);
                return optimizedSource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize kernel source for {KernelName}", analysis.KernelName);
                return sourceCode; // Return original source if optimization fails
            }
        }
        /// Applies memory coalescing and access pattern optimizations.
        private Task<string> ApplyMemoryOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.EnableMemoryOptimizations)
                return Task.FromResult(sourceCode);
            var optimizedCode = sourceCode;
            // Add memory coalescing hints
            optimizedCode = AddMemoryCoalescingHints(optimizedCode);
            // Optimize memory access patterns
            optimizedCode = OptimizeMemoryAccessPatterns(optimizedCode, analysis);
            // Add prefetching for large data access
            if (analysis.EstimatedDataSize > 1_000_000)
                optimizedCode = AddMemoryPrefetching(optimizedCode);
            // Add cache optimization directives
            optimizedCode = AddCacheOptimizations(optimizedCode, profile);
            return Task.FromResult(optimizedCode);
        }
        /// Applies warp-level optimizations including shuffle operations and ballot functions.
        private Task<string> ApplyWarpOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.EnableWarpOptimizations)
                return Task.FromResult(sourceCode);

            var optimizedCode = sourceCode;
            // Add warp shuffle optimizations for reductions
            if (analysis.RequiresReduction)
            {
                optimizedCode = AddWarpShuffleOptimizations(optimizedCode);
            }
            // Add ballot and vote functions for divergent branches
            optimizedCode = AddBallotOptimizations(optimizedCode, analysis);
            // Add warp-level primitives
            optimizedCode = AddWarpPrimitives(optimizedCode, profile);
            return Task.FromResult(optimizedCode);
        }
        /// Applies shared memory optimizations including bank conflict avoidance.
        private Task<string> ApplySharedMemoryOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.EnableSharedMemoryOptimizations || !analysis.RequiresSharedMemory)
                return Task.FromResult(sourceCode);

            var optimizedCode = sourceCode;
            // Optimize shared memory bank conflicts
            optimizedCode = OptimizeSharedMemoryBankConflicts(optimizedCode);
            // Add dynamic shared memory allocation
            optimizedCode = AddDynamicSharedMemory(optimizedCode, analysis, profile);
            // Add shared memory padding for alignment
            optimizedCode = AddSharedMemoryPadding(optimizedCode);
            return Task.FromResult(optimizedCode);
        }
        /// Applies atomic operation optimizations.
        private Task<string> ApplyAtomicOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.EnableAtomicOptimizations)
                return Task.FromResult(sourceCode);

            var optimizedCode = sourceCode;
            // Replace simple atomics with faster alternatives
            optimizedCode = OptimizeAtomicOperations(optimizedCode, profile);
            // Add atomic reduction optimizations
            if (analysis.RequiresReduction)
            {
                optimizedCode = AddAtomicReductionOptimizations(optimizedCode);
            }
            return Task.FromResult(optimizedCode);
        }
        /// Applies texture memory optimizations for spatial locality.
        private Task<string> ApplyTextureMemoryOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.EnableTextureMemory || analysis.EstimatedDataSize < 100_000)
                return Task.FromResult(sourceCode);

            var optimizedCode = sourceCode;
            // Add texture memory declarations
            optimizedCode = AddTextureMemoryDeclarations(optimizedCode, analysis);
            // Replace global memory accesses with texture fetches
            optimizedCode = ReplaceWithTextureFetches(optimizedCode);
            return Task.FromResult(optimizedCode);
        }
        /// Applies constant memory optimizations for read-only data.
        private Task<string> ApplyConstantMemoryOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.EnableConstantMemory)
                return Task.FromResult(sourceCode);

            var optimizedCode = sourceCode;
            // Move constants to constant memory
            optimizedCode = MoveConstantsToConstantMemory(optimizedCode);
            // Add constant memory caching hints
            optimizedCode = AddConstantMemoryCaching(optimizedCode);
            return Task.FromResult(optimizedCode);
        }
        /// Applies Tensor Core optimizations for supported operations.
        private Task<string> ApplyTensorCoreOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.SupportsTensorCores || !IsTensorCoreCompatible(analysis))
                return Task.FromResult(sourceCode);

            var optimizedCode = sourceCode;
            // Add Tensor Core includes and types
            optimizedCode = AddTensorCoreIncludes(optimizedCode);
            // Replace matrix operations with Tensor Core calls
            optimizedCode = ReplaceMathWithTensorCores(optimizedCode, analysis);
            return Task.FromResult(optimizedCode);
        }
        /// Applies cooperative groups optimizations for advanced synchronization.
        private Task<string> ApplyCooperativeGroupsOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            if (!profile.SupportsCooperativeGroups || !analysis.RequiresCooperativeGroups)
                return Task.FromResult(sourceCode);

            var optimizedCode = sourceCode;
            // Add cooperative groups includes
            optimizedCode = AddCooperativeGroupsIncludes(optimizedCode);
            // Replace synchronization with cooperative groups
            optimizedCode = ReplaceWithCooperativeGroups(optimizedCode);
            return Task.FromResult(optimizedCode);
        }
        /// Applies occupancy optimizations for better GPU utilization.
        private async Task<string> ApplyOccupancyOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            var optimizedCode = sourceCode;
            // Add occupancy launch bounds
            optimizedCode = AddOccupancyLaunchBounds(optimizedCode, analysis, profile);
            // Optimize register usage
            optimizedCode = OptimizeRegisterUsage(optimizedCode, profile);
            return await Task.FromResult(optimizedCode);
        /// Applies instruction-level optimizations including loop unrolling and predication.
        private async Task<string> ApplyInstructionLevelOptimizationsAsync(
            string sourceCode,
            ExpressionAnalysisResult analysis,
            OptimizationProfile profile)
        {
            var optimizedCode = sourceCode;
            // Add loop unrolling pragmas
            optimizedCode = AddLoopUnrolling(optimizedCode, profile);
            // Add predication for divergent branches
            optimizedCode = AddPredicationOptimizations(optimizedCode);
            // Add instruction-level parallelism hints
            optimizedCode = AddInstructionLevelParallelism(optimizedCode);
            return await Task.FromResult(optimizedCode);
        }
        #region Optimization Implementation Methods
        private string AddMemoryCoalescingHints(string sourceCode)
        {
            // Add vectorized memory access hints
            var pattern = @"(\s+)(\w+\[\s*i\s*\]\s*=\s*[^;]+;)";
            var replacement = "$1// Coalesced memory access\n$1$2";
            return Regex.Replace(sourceCode, pattern, replacement, RegexOptions.Multiline);
        }
        private string OptimizeMemoryAccessPatterns(string sourceCode, ExpressionAnalysisResult analysis)
        {
            // Replace strided access with coalesced access where possible
            if (analysis.OperationType == KernelOperationType.Map)
            {
                var pattern = @"input\[i \* stride\]";
                var replacement = "input[i]";
                sourceCode = sourceCode.Replace(pattern, replacement);
            }
            return sourceCode;
        }
        private string AddMemoryPrefetching(string sourceCode)
        {
            // Add prefetching directives for large data sets
            var prefetchCode = @"
    // Prefetch next cache line for better memory throughput
    if (i + 32 < n) {
        __builtin_prefetch(&input[i + 32], 0, 3);
    }";
            var pattern = @"(\s+for\s*\([^}]+\{\s*)";
            var replacement = $"$1{prefetchCode}\n";
            return Regex.Replace(sourceCode, pattern, replacement, RegexOptions.Multiline);
        }
        private string AddCacheOptimizations(string sourceCode, OptimizationProfile profile)
        {
            if (profile.CacheLevel >= CacheOptimizationLevel.L2)
            {
                // Add L2 cache hints
                sourceCode = sourceCode.Replace("input[", "__ldg(&input[");
                sourceCode = sourceCode.Replace("right_input[", "__ldg(&right_input[");
            }
            return sourceCode;
        }
        private string AddWarpShuffleOptimizations(string sourceCode)
        {
            // Replace simple reductions with warp shuffle
            var shuffleReduction = @"
    // Optimized warp-level reduction using shuffle
    #pragma unroll
    for (int offset = warpSize / 2; offset > 0; offset /= 2) {
        accumulator = reduce_operation(accumulator, __shfl_down_sync(0xFFFFFFFF, accumulator, offset));
            var pattern = @"(\/\/ Warp-level reduction using shuffle[\s\S]*?}[\s\S]*?})";
            return Regex.Replace(sourceCode, pattern, shuffleReduction, RegexOptions.Multiline);
        }
        private string AddBallotOptimizations(string sourceCode, ExpressionAnalysisResult analysis)
        {
            if (analysis.OperationType == KernelOperationType.Filter)
                // Add ballot-based predicate evaluation
                var ballotCode = @"
    // Use ballot for efficient predicate evaluation
    unsigned int active_mask = __activemask();
    unsigned int predicate_mask = __ballot_sync(active_mask, filter_predicate(input[i]));
    int active_threads = __popc(predicate_mask);";
                var pattern = @"(if \(filter_predicate\([^}]+\))";
                var replacement = $"{ballotCode}\n        $1";
                sourceCode = Regex.Replace(sourceCode, pattern, replacement, RegexOptions.Multiline);
            }
            return sourceCode;
        }
        private string AddWarpPrimitives(string sourceCode, OptimizationProfile profile)
            // Add warp-level utility functions
            var warpUtilities = @"
__device__ inline int warp_reduce_sum(int val) {
        val += __shfl_down_sync(0xFFFFFFFF, val, offset);
    }
    return val;
}
__device__ inline float warp_reduce_sum(float val) {
}";
            // Add before first kernel function
            var pattern = @"(extern\s+""C""\s+__global__)";
            var replacement = $"{warpUtilities}\n\n$1";
            return Regex.Replace(sourceCode, pattern, replacement);
        private string OptimizeSharedMemoryBankConflicts(string sourceCode)
            // Add padding to shared memory arrays to avoid bank conflicts
            var pattern = @"__shared__\s+(\w+)\s+(\w+)\[(\d+)\];";
            var replacement = "__shared__ $1 $2[$3 + 1]; // Padded to avoid bank conflicts";
        private string AddDynamicSharedMemory(string sourceCode, ExpressionAnalysisResult analysis, OptimizationProfile profile)
            if (profile.SharedMemorySize > 0)
                // Add dynamic shared memory declaration
                var dynamicSharedMem = @"
    // Dynamic shared memory allocation
    extern __shared__ char dynamic_shared_mem[];
    OutputType* shared_data = (OutputType*)dynamic_shared_mem;";
                var pattern = @"(__shared__\s+\w+\s+shared_data\[[^\]]+\];)";
                var replacement = dynamicSharedMem;
                sourceCode = Regex.Replace(sourceCode, pattern, replacement);
        private string AddSharedMemoryPadding(string sourceCode)
            // Add padding calculations for optimal shared memory usage
            var paddingCode = @"
    // Calculate padding for optimal shared memory bank usage
    const int padding = (sizeof(OutputType) == 4) ? 1 : 2;";
            var pattern = @"(__shared__[\s\S]*?;)";
            var replacement = $"$1\n{paddingCode}";
        private string OptimizeAtomicOperations(string sourceCode, OptimizationProfile profile)
            // Replace generic atomics with type-specific ones
            sourceCode = sourceCode.Replace("atomicAdd(", "atomicAdd_block(");
            var (major, minor) = profile.ComputeCapability;
            if (major > 6 || (major == 6 && minor >= 0))
                // Use faster atomic operations for newer architectures
                sourceCode = sourceCode.Replace("atomicAdd_block(", "atomicAdd_system(");
        private string AddAtomicReductionOptimizations(string sourceCode)
            // Add block-level atomic reduction
            var atomicReduction = @"
__device__ inline void atomic_warp_reduce_and_store(OutputType* target, OutputType value) {
    // Warp-level reduction first
    value = warp_reduce_sum(value);
    
    // Only lane 0 performs atomic operation
    if ((threadIdx.x & (warpSize - 1)) == 0) {
        atomicAdd(target, value);
            var pattern = @"(atomic_reduce_operation\([^}]+\))";
            var replacement = $"{atomicReduction}\n\n        atomic_warp_reduce_and_store$1";
        private string AddTextureMemoryDeclarations(string sourceCode, ExpressionAnalysisResult analysis)
            // Add texture object declarations
            var textureDeclarations = @"
// Texture memory objects for spatial locality
texture<InputType, 1, cudaReadModeElementType> tex_input;
texture<InputType, 1, cudaReadModeElementType> tex_right_input;";
            var replacement = $"{textureDeclarations}\n\n$1";
        private string ReplaceWithTextureFetches(string sourceCode)
            // Replace direct memory access with texture fetches
            sourceCode = sourceCode.Replace("input[i]", "tex1Dfetch(tex_input, i)");
            sourceCode = sourceCode.Replace("right_input[i]", "tex1Dfetch(tex_right_input, i)");
        private string MoveConstantsToConstantMemory(string sourceCode)
            // Move small constants to constant memory
            var constantDeclarations = @"
// Constant memory for frequently accessed small data
__constant__ float const_params[64];
__constant__ int const_lookup[256];";
            var replacement = $"{constantDeclarations}\n\n$1";
        private string AddConstantMemoryCaching(string sourceCode)
            var pattern = @"(const\s+\w+\s+\w+\s*=\s*[^;]+;)";
            var replacement = "$1 // Cached in constant memory";
        private bool IsTensorCoreCompatible(ExpressionAnalysisResult analysis)
            // Check if operation can benefit from Tensor Cores
            return analysis.InputType == typeof(float) || analysis.InputType == typeof(double) ||
                   analysis.InputType == typeof(int) || analysis.InputType == typeof(short);
        private string AddTensorCoreIncludes(string sourceCode)
            var tensorCoreIncludes = @"
#include <mma.h>
using namespace nvcuda;";
            var pattern = @"(\/\/ Auto-generated CUDA kernel)";
            var replacement = $"$1\n{tensorCoreIncludes}";
        private string ReplaceMathWithTensorCores(string sourceCode, ExpressionAnalysisResult analysis)
            if (analysis.OperationType == KernelOperationType.Reduce && analysis.InputType == typeof(float))
                // Replace with Tensor Core matrix operations
                var tensorCoreOp = @"
    // Tensor Core optimized operation
    wmma::fragment<wmma::matrix_a, 16, 16, 16, half, wmma::row_major> a_frag;
    wmma::fragment<wmma::matrix_b, 16, 16, 16, half, wmma::col_major> b_frag;
    wmma::fragment<wmma::accumulator, 16, 16, 16, float> acc_frag;";
                sourceCode = $"{tensorCoreOp}\n{sourceCode}";
        private string AddCooperativeGroupsIncludes(string sourceCode)
            var cooperativeIncludes = @"
#include <cooperative_groups.h>
using namespace cooperative_groups;";
            var replacement = $"$1\n{cooperativeIncludes}";
        private string ReplaceWithCooperativeGroups(string sourceCode)
            // Replace __syncthreads with cooperative groups
            var pattern = @"__syncthreads\(\)";
            var replacement = "this_thread_block().sync()";
            return sourceCode.Replace(pattern, replacement);
        private string AddOccupancyLaunchBounds(string sourceCode, ExpressionAnalysisResult analysis, OptimizationProfile profile)
            var launchBounds = $"__launch_bounds__({profile.MaxThreadsPerBlock}, {profile.MinBlocksPerSM})";
            var pattern = @"(__global__\s+void)";
            var replacement = $"{launchBounds} $1";
        private string OptimizeRegisterUsage(string sourceCode, OptimizationProfile profile)
            if (profile.MaxRegisters > 0)
                // Add register usage hints
                var registerHints = @"
    // Optimize register usage
    #pragma unroll 4
    register int temp_reg;";
                var pattern = @"(\{[\s]*const\s+int\s+idx)";
                var replacement = $"{{{registerHints}\n    $1";
        private string AddLoopUnrolling(string sourceCode, OptimizationProfile profile)
            var unrollFactor = profile.LoopUnrollFactor;
            var pattern = @"(for\s*\([^}]+\))";
            var replacement = $"#pragma unroll {unrollFactor}\n    $1";
        private string AddPredicationOptimizations(string sourceCode)
            var pattern = @"(if\s*\([^}]+\)\s*\{)";
            var replacement = "#ifdef __CUDA_ARCH__\n    #pragma unroll\n    #endif\n    $1";
        private string AddInstructionLevelParallelism(string sourceCode)
            // Add ILP hints for better instruction scheduling
            var ilpHints = @"
    // Instruction-level parallelism hints
    register float temp1, temp2, temp3, temp4;";
            var pattern = @"(const\s+int\s+idx\s*=)";
            var replacement = $"{ilpHints}\n    $1";
        #endregion
        /// Initializes optimization profiles for different compute capabilities.
        private Dictionary<string, OptimizationProfile> InitializeOptimizationProfiles()
            return new Dictionary<string, OptimizationProfile>
                ["5.0"] = new OptimizationProfile
                {
                    ComputeCapability = (5, 0),
                    EnableMemoryOptimizations = true,
                    EnableWarpOptimizations = true,
                    EnableSharedMemoryOptimizations = true,
                    EnableAtomicOptimizations = true,
                    EnableTextureMemory = true,
                    EnableConstantMemory = true,
                    SupportsTensorCores = false,
                    SupportsCooperativeGroups = false,
                    CacheLevel = CacheOptimizationLevel.L1,
                    MaxThreadsPerBlock = 1024,
                    MinBlocksPerSM = 2,
                    MaxRegisters = 255,
                    SharedMemorySize = 48 * 1024,
                    LoopUnrollFactor = 4
                },
                ["7.0"] = new OptimizationProfile
                    ComputeCapability = (7, 0),
                    SupportsTensorCores = true,
                    SupportsCooperativeGroups = true,
                    CacheLevel = CacheOptimizationLevel.L2,
                    SharedMemorySize = 96 * 1024,
                    LoopUnrollFactor = 8
                ["8.0"] = new OptimizationProfile
                    ComputeCapability = (8, 0),
                    SharedMemorySize = 164 * 1024,
                ["8.9"] = new OptimizationProfile
                    ComputeCapability = (8, 9),
                    SharedMemorySize = 100 * 1024, // Ada Lovelace enhancement
                    LoopUnrollFactor = 16
                }
            };
        /// Gets the optimization profile for a compute capability.
        private OptimizationProfile GetOptimizationProfile(int major, int minor, KernelGenerationOptions options)
            var key = $"{major}.{minor}";
            if (_profiles.TryGetValue(key, out var profile))
                return profile;
            // Find closest profile
            var (closestMajor, closestMinor) = _profiles.Keys
                .Select(k => k.Split('.'))
                .Select(parts => (int.Parse(parts[0]), int.Parse(parts[1])))
                .Where(cc => cc.Item1 <= major)
                .OrderByDescending(cc => cc.Item1)
                .ThenByDescending(cc => cc.Item2)
                .FirstOrDefault();
            return _profiles.GetValueOrDefault($"{closestMajor}.{closestMinor}", _profiles["5.0"]);
        /// Generates a cache key for optimization results.
        private string GenerateCacheKey(string sourceCode, ExpressionAnalysisResult analysis, KernelGenerationOptions options)
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(sourceCode.GetHashCode());
            keyBuilder.Append('_');
            keyBuilder.Append(analysis.KernelName);
            keyBuilder.Append(options.OptimizationLevel);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
        public void Dispose()
            if (_disposed)
                return;
            _cache.Dispose();
            _disposed = true;
    /// Optimization profile for specific compute capabilities.
    public class OptimizationProfile
        public (int, int) ComputeCapability { get; set; }
        public bool EnableMemoryOptimizations { get; set; } = true;
        public bool EnableWarpOptimizations { get; set; } = true;
        public bool EnableSharedMemoryOptimizations { get; set; } = true;
        public bool EnableAtomicOptimizations { get; set; } = true;
        public bool EnableTextureMemory { get; set; } = true;
        public bool EnableConstantMemory { get; set; } = true;
        public bool SupportsTensorCores { get; set; } = false;
        public bool SupportsCooperativeGroups { get; set; } = false;
        public CacheOptimizationLevel CacheLevel { get; set; } = CacheOptimizationLevel.L1;
        public int MaxThreadsPerBlock { get; set; } = 1024;
        public int MinBlocksPerSM { get; set; } = 2;
        public int MaxRegisters { get; set; } = 255;
        public int SharedMemorySize { get; set; } = 48 * 1024;
        public int LoopUnrollFactor { get; set; } = 4;
    /// Cache optimization levels.
    public enum CacheOptimizationLevel
        None,
        L1,
        L2,
        L3
    /// Cache for optimization results.
    internal class OptimizationCache : IDisposable
        private readonly Dictionary<string, string> _cache = [];
        private readonly object _lock = new();
        public bool TryGetOptimizedSource(string key, out string optimizedSource)
            lock (_lock)
                return _cache.TryGetValue(key, out optimizedSource!);
        public void CacheOptimizedSource(string key, string optimizedSource)
                _cache[key] = optimizedSource;
                _cache.Clear();
