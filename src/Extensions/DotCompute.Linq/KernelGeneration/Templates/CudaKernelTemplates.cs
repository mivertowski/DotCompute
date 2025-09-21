// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using DotCompute.Linq.KernelGeneration;
using DotCompute.Linq.KernelGeneration.Templates;
using Microsoft.Extensions.Logging;
using TemplateOptions = DotCompute.Linq.KernelGeneration.Templates.KernelGenerationOptions;
namespace DotCompute.Linq.KernelGeneration.Templates
{
    /// <summary>
    /// CUDA kernel templates for common LINQ operations.
    /// Provides optimized kernel implementations for various operation types.
    /// </summary>
    public sealed class CudaKernelTemplates
    {
        private readonly ILogger _logger;
        private readonly Dictionary<KernelOperationType, KernelTemplate> _templates;
        public CudaKernelTemplates(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _templates = InitializeTemplates();
        }
        /// <summary>
        /// Selects the appropriate kernel template for an operation type.
        /// </summary>
        public KernelTemplate SelectTemplate(KernelOperationType operationType, Type inputType, Type outputType)
            if (_templates.TryGetValue(operationType, out var template))
            {
                return template;
            }
            _logger.LogWarning("No specific template found for operation {OperationType}, using custom template", operationType);
            return _templates[KernelOperationType.Custom];
        /// Initializes all available kernel templates.
        private Dictionary<KernelOperationType, KernelTemplate> InitializeTemplates()
            return new Dictionary<KernelOperationType, KernelTemplate>
                [KernelOperationType.Map] = new MapKernelTemplate(_logger),
                [KernelOperationType.Filter] = new FilterKernelTemplate(_logger),
                [KernelOperationType.Reduce] = new ReductionKernelTemplate(_logger),
                [KernelOperationType.Scan] = new ScanKernelTemplate(_logger),
                [KernelOperationType.Join] = new JoinKernelTemplate(_logger),
                [KernelOperationType.GroupBy] = new GroupByKernelTemplate(_logger),
                [KernelOperationType.Sort] = new SortKernelTemplate(_logger),
                [KernelOperationType.Custom] = new CustomKernelTemplate(_logger)
            };
    }
    /// Base class for all kernel templates.
    public abstract class KernelTemplate
        protected readonly ILogger Logger;
        protected KernelTemplate(ILogger logger)
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        /// Generates CUDA kernel source code for the given analysis result.
        public abstract Task<string> GenerateKernelAsync(
            ExpressionAnalysisResult analysis,
            TemplateOptions options);
        /// Gets the optimal block size for this kernel type.
        public virtual int GetOptimalBlockSize(ExpressionAnalysisResult analysis)
            return analysis.EstimatedDataSize switch
                > 1_000_000 => 512,  // Large datasets
                > 100_000 => 256,    // Medium datasets
                _ => 128             // Small datasets
        /// Determines if cooperative groups are needed.
        public virtual bool RequiresCooperativeGroups(ExpressionAnalysisResult analysis)
            return analysis.RequiresReduction || analysis.OperationType == KernelOperationType.GroupBy;
    /// Template for map operations (Select).
    public sealed class MapKernelTemplate : KernelTemplate
        public MapKernelTemplate(ILogger logger) : base(logger) { }
        public override Task<string> GenerateKernelAsync(
            TemplateOptions options)
            var builder = new StringBuilder();
            // Generate map kernel
            builder.AppendLine("extern \"C\" __global__ void " + analysis.KernelName + "(");
            builder.AppendLine("    const InputType* __restrict__ input,");
            builder.AppendLine("    OutputType* __restrict__ output,");
            builder.AppendLine("    const int n)");
            builder.AppendLine("{");
            builder.AppendLine("    const int idx = blockIdx.x * blockDim.x + threadIdx.x;");
            builder.AppendLine("    const int stride = blockDim.x * gridDim.x;");
            builder.AppendLine();
            builder.AppendLine("    // Grid-stride loop for better memory coalescing");
            builder.AppendLine("    for (int i = idx; i < n; i += stride) {");
            builder.AppendLine("        // Apply transformation to input[i]");
            builder.AppendLine("        output[i] = transform_element(input[i]);");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            // Add transformation function
            builder.AppendLine("__device__ inline OutputType transform_element(const InputType& input)");
            builder.AppendLine("    // Generated transformation logic based on LINQ expression");
            builder.AppendLine("    return (OutputType)input; // Placeholder - replace with actual transformation");
            Logger.LogDebug("Generated map kernel for {KernelName}", analysis.KernelName);
            return Task.FromResult(builder.ToString());
        public override int GetOptimalBlockSize(ExpressionAnalysisResult analysis)
            // Map operations benefit from larger block sizes for memory coalescing
                > 10_000_000 => 512,
                > 1_000_000 => 256,
                _ => 128
    /// Template for filter operations (Where).
    public sealed class FilterKernelTemplate : KernelTemplate
        public FilterKernelTemplate(ILogger logger) : base(logger) { }
            // Generate filter kernel with compaction
            builder.AppendLine("    int* __restrict__ output_count,");
            if (options.UseSharedMemory)
                builder.AppendLine("    // Shared memory for local compaction");
                builder.AppendLine("    __shared__ int local_count;");
                builder.AppendLine("    __shared__ int global_offset;");
                builder.AppendLine();
                builder.AppendLine("    if (threadIdx.x == 0) {");
                builder.AppendLine("        local_count = 0;");
                builder.AppendLine("    }");
                builder.AppendLine("    __syncthreads();");
            builder.AppendLine("    // Grid-stride loop with predicate evaluation");
            builder.AppendLine("        if (filter_predicate(input[i])) {");
                builder.AppendLine("            int local_pos = atomicAdd(&local_count, 1);");
                builder.AppendLine("            __shared__ OutputType temp_buffer[1024];");
                builder.AppendLine("            if (local_pos < 1024) {");
                builder.AppendLine("                temp_buffer[local_pos] = input[i];");
                builder.AppendLine("            }");
            else
                builder.AppendLine("            int pos = atomicAdd(output_count, 1);");
                builder.AppendLine("            output[pos] = input[i];");
            builder.AppendLine("        }");
                builder.AppendLine("    // Copy from shared memory to global memory");
                builder.AppendLine("    if (threadIdx.x == 0 && local_count > 0) {");
                builder.AppendLine("        global_offset = atomicAdd(output_count, local_count);");
                builder.AppendLine("    for (int i = threadIdx.x; i < local_count; i += blockDim.x) {");
                builder.AppendLine("        output[global_offset + i] = temp_buffer[i];");
            // Add predicate function
            builder.AppendLine("__device__ inline bool filter_predicate(const InputType& input)");
            builder.AppendLine("    // Generated predicate logic based on LINQ expression");
            builder.AppendLine("    return true; // Placeholder - replace with actual predicate");
            Logger.LogDebug("Generated filter kernel for {KernelName}", analysis.KernelName);
    /// Template for reduction operations (Sum, Average, Min, Max, Count).
    public sealed class ReductionKernelTemplate : KernelTemplate
        public ReductionKernelTemplate(ILogger logger) : base(logger) { }
            // Generate reduction kernel with warp-level primitives
            builder.AppendLine("    // Shared memory for block-level reduction");
            builder.AppendLine("    __shared__ OutputType shared_data[1024];");
            builder.AppendLine("    // Initialize accumulator");
            builder.AppendLine("    OutputType accumulator = get_identity_value();");
            builder.AppendLine("    // Grid-stride loop to process elements");
            builder.AppendLine("        accumulator = reduce_operation(accumulator, input[i]);");
            if (options.EnableWarpShuffle)
                builder.AppendLine("    // Warp-level reduction using shuffle");
                builder.AppendLine("    for (int offset = warpSize / 2; offset > 0; offset /= 2) {");
                builder.AppendLine("        accumulator = reduce_operation(accumulator, __shfl_down_sync(0xFFFFFFFF, accumulator, offset));");
            builder.AppendLine("    // Store warp results to shared memory");
            builder.AppendLine("    const int lane = threadIdx.x % warpSize;");
            builder.AppendLine("    const int warp_id = threadIdx.x / warpSize;");
            builder.AppendLine("    if (lane == 0) {");
            builder.AppendLine("        shared_data[warp_id] = accumulator;");
            builder.AppendLine("    __syncthreads();");
            builder.AppendLine("    // Final reduction by first warp");
            builder.AppendLine("    if (warp_id == 0) {");
            builder.AppendLine("        accumulator = (lane < blockDim.x / warpSize) ? shared_data[lane] : get_identity_value();");
                builder.AppendLine("        for (int offset = warpSize / 2; offset > 0; offset /= 2) {");
                builder.AppendLine("            accumulator = reduce_operation(accumulator, __shfl_down_sync(0xFFFFFFFF, accumulator, offset));");
                builder.AppendLine("        }");
            builder.AppendLine("    // Write block result");
            builder.AppendLine("    if (threadIdx.x == 0) {");
            if (options.EnableAtomics)
                builder.AppendLine("        atomic_reduce_operation(output, accumulator);");
                builder.AppendLine("        output[blockIdx.x] = accumulator;");
            // Add reduction operation functions
            AddReductionFunctions(builder, analysis);
            Logger.LogDebug("Generated reduction kernel for {KernelName}", analysis.KernelName);
        private void AddReductionFunctions(StringBuilder builder, ExpressionAnalysisResult analysis)
            builder.AppendLine("__device__ inline OutputType get_identity_value()");
            builder.AppendLine("    // Identity value for the reduction operation");
            builder.AppendLine("    return (OutputType)0; // Placeholder - replace based on operation");
            builder.AppendLine("__device__ inline OutputType reduce_operation(const OutputType& a, const InputType& b)");
            builder.AppendLine("    // Reduction operation implementation");
            builder.AppendLine("    return a + (OutputType)b; // Placeholder - replace based on operation");
            builder.AppendLine("__device__ inline void atomic_reduce_operation(OutputType* target, const OutputType& value)");
            builder.AppendLine("    // Atomic reduction operation");
            builder.AppendLine("    atomicAdd(target, value); // Placeholder - replace based on operation");
        public override bool RequiresCooperativeGroups(ExpressionAnalysisResult analysis)
            return true; // Reductions always benefit from cooperative groups
    /// Template for scan operations (prefix sum).
    public sealed class ScanKernelTemplate : KernelTemplate
        public ScanKernelTemplate(ILogger logger) : base(logger) { }
            // Generate scan kernel using Blelloch algorithm
            builder.AppendLine("    const int idx = threadIdx.x;");
            builder.AppendLine("    const int block_start = blockIdx.x * blockDim.x * 2;");
            builder.AppendLine("    // Shared memory for block-level scan");
            builder.AppendLine("    __shared__ OutputType shared_data[2048];");
            builder.AppendLine("    // Load input data into shared memory");
            builder.AppendLine("    int ai = idx;");
            builder.AppendLine("    int bi = idx + blockDim.x;");
            builder.AppendLine("    int global_ai = block_start + ai;");
            builder.AppendLine("    int global_bi = block_start + bi;");
            builder.AppendLine("    shared_data[ai] = (global_ai < n) ? (OutputType)input[global_ai] : (OutputType)0;");
            builder.AppendLine("    shared_data[bi] = (global_bi < n) ? (OutputType)input[global_bi] : (OutputType)0;");
            builder.AppendLine("    int offset = 1;");
            builder.AppendLine("    // Up-sweep (reduce) phase");
            builder.AppendLine("    for (int d = blockDim.x; d > 0; d >>= 1) {");
            builder.AppendLine("        __syncthreads();");
            builder.AppendLine("        if (idx < d) {");
            builder.AppendLine("            int ai = offset * (2 * idx + 1) - 1;");
            builder.AppendLine("            int bi = offset * (2 * idx + 2) - 1;");
            builder.AppendLine("            shared_data[bi] += shared_data[ai];");
            builder.AppendLine("        offset *= 2;");
            builder.AppendLine("    // Clear the last element");
            builder.AppendLine("    if (idx == 0) {");
            builder.AppendLine("        shared_data[2 * blockDim.x - 1] = 0;");
            builder.AppendLine("    // Down-sweep phase");
            builder.AppendLine("    for (int d = 1; d < 2 * blockDim.x; d *= 2) {");
            builder.AppendLine("        offset >>= 1;");
            builder.AppendLine("            OutputType temp = shared_data[ai];");
            builder.AppendLine("            shared_data[ai] = shared_data[bi];");
            builder.AppendLine("            shared_data[bi] += temp;");
            builder.AppendLine("    // Write results back to global memory");
            builder.AppendLine("    if (global_ai < n) output[global_ai] = shared_data[ai];");
            builder.AppendLine("    if (global_bi < n) output[global_bi] = shared_data[bi];");
            Logger.LogDebug("Generated scan kernel for {KernelName}", analysis.KernelName);
            // Scan operations work best with power-of-2 block sizes
            return 256; // Good balance between occupancy and shared memory usage
    /// Template for join operations.
    public sealed class JoinKernelTemplate : KernelTemplate
        public JoinKernelTemplate(ILogger logger) : base(logger) { }
            // Generate hash join kernel
            builder.AppendLine("    const InputType* __restrict__ left_input,");
            builder.AppendLine("    const InputType* __restrict__ right_input,");
            builder.AppendLine("    const int left_count,");
            builder.AppendLine("    const int right_count)");
            builder.AppendLine("    // Simple nested loop join (can be optimized with hash table)");
            builder.AppendLine("    if (idx < left_count) {");
            builder.AppendLine("        const InputType left_item = left_input[idx];");
            builder.AppendLine("        for (int i = 0; i < right_count; i++) {");
            builder.AppendLine("            const InputType right_item = right_input[i];");
            builder.AppendLine("            if (join_predicate(left_item, right_item)) {");
            builder.AppendLine("                int pos = atomicAdd(output_count, 1);");
            builder.AppendLine("                output[pos] = join_selector(left_item, right_item);");
            builder.AppendLine("            }");
            // Add join functions
            builder.AppendLine("__device__ inline bool join_predicate(const InputType& left, const InputType& right)");
            builder.AppendLine("    // Join predicate implementation");
            builder.AppendLine("__device__ inline OutputType join_selector(const InputType& left, const InputType& right)");
            builder.AppendLine("    // Join result selector implementation");
            builder.AppendLine("    return (OutputType)left; // Placeholder - replace with actual selector");
            Logger.LogDebug("Generated join kernel for {KernelName}", analysis.KernelName);
    /// Template for group by operations.
    public sealed class GroupByKernelTemplate : KernelTemplate
        public GroupByKernelTemplate(ILogger logger) : base(logger) { }
            // Generate group by kernel with hash table
            builder.AppendLine("    int* __restrict__ group_counts,");
            builder.AppendLine("    const int n,");
            builder.AppendLine("    const int max_groups)");
            builder.AppendLine("    // Process elements in grid-stride loop");
            builder.AppendLine("        const InputType item = input[i];");
            builder.AppendLine("        const int group_key = get_group_key(item);");
            builder.AppendLine("        const int group_index = group_key % max_groups;");
            builder.AppendLine("        // Atomic increment for group count");
            builder.AppendLine("        atomicAdd(&group_counts[group_index], 1);");
            builder.AppendLine("        // Apply group aggregation");
            builder.AppendLine("        atomic_aggregate(&output[group_index], item);");
            // Add group by functions
            builder.AppendLine("__device__ inline int get_group_key(const InputType& item)");
            builder.AppendLine("    // Group key extraction implementation");
            builder.AppendLine("    return 0; // Placeholder - replace with actual key extraction");
            builder.AppendLine("__device__ inline void atomic_aggregate(OutputType* target, const InputType& item)");
            builder.AppendLine("    // Atomic aggregation implementation");
            builder.AppendLine("    atomicAdd(target, (OutputType)item); // Placeholder - replace with actual aggregation");
            Logger.LogDebug("Generated group by kernel for {KernelName}", analysis.KernelName);
    /// Template for sort operations.
    public sealed class SortKernelTemplate : KernelTemplate
        public SortKernelTemplate(ILogger logger) : base(logger) { }
            // Generate bitonic sort kernel
            builder.AppendLine("    InputType* __restrict__ data,");
            builder.AppendLine("    const int stage,");
            builder.AppendLine("    const int step)");
            builder.AppendLine("    if (idx < n) {");
            builder.AppendLine("        const int partner = idx ^ step;");
            builder.AppendLine("        if (partner > idx && partner < n) {");
            builder.AppendLine("            const bool ascending = ((idx & stage) == 0);");
            builder.AppendLine("            if ((compare_elements(data[idx], data[partner]) > 0) == ascending) {");
            builder.AppendLine("                // Swap elements");
            builder.AppendLine("                InputType temp = data[idx];");
            builder.AppendLine("                data[idx] = data[partner];");
            builder.AppendLine("                data[partner] = temp;");
            // Add comparison function
            builder.AppendLine("__device__ inline int compare_elements(const InputType& a, const InputType& b)");
            builder.AppendLine("    // Element comparison implementation");
            builder.AppendLine("    if (a < b) return -1;");
            builder.AppendLine("    if (a > b) return 1;");
            builder.AppendLine("    return 0;");
            Logger.LogDebug("Generated sort kernel for {KernelName}", analysis.KernelName);
    /// Template for custom operations.
    public sealed class CustomKernelTemplate : KernelTemplate
        public CustomKernelTemplate(ILogger logger) : base(logger) { }
            // Generate generic custom kernel
            builder.AppendLine("    // Grid-stride loop for custom processing");
            builder.AppendLine("        // Custom operation implementation");
            builder.AppendLine("        output[i] = custom_operation(input[i]);");
            // Add custom operation function
            builder.AppendLine("__device__ inline OutputType custom_operation(const InputType& input)");
            builder.AppendLine("    // Custom operation implementation");
            builder.AppendLine("    return (OutputType)input; // Placeholder - replace with actual operation");
            Logger.LogDebug("Generated custom kernel for {KernelName}", analysis.KernelName);
}
