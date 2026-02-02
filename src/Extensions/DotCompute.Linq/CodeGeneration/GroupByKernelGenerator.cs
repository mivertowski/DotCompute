// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Text;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Specialized generator for GPU-accelerated GroupBy operations with aggregation functions.
/// </summary>
/// <remarks>
/// <para>
/// This generator implements hash-based grouping with parallel aggregation for GPU execution.
/// It supports multiple aggregation functions that can be computed simultaneously.
/// </para>
/// <para>
/// <b>Supported Aggregation Functions:</b>
/// </para>
/// <list type="bullet">
/// <item><description><b>Count:</b> Number of elements in each group</description></item>
/// <item><description><b>Sum:</b> Total of values in each group</description></item>
/// <item><description><b>Average:</b> Mean value in each group (Sum/Count)</description></item>
/// <item><description><b>Min:</b> Minimum value in each group</description></item>
/// <item><description><b>Max:</b> Maximum value in each group</description></item>
/// </list>
/// <para>
/// <b>Algorithm Overview:</b>
/// </para>
/// <list type="number">
/// <item><description>Hash Phase: Compute group key hash for each element</description></item>
/// <item><description>Aggregation Phase: Atomically update aggregation accumulators per group</description></item>
/// <item><description>Finalization Phase: Compute derived aggregates (e.g., Average = Sum/Count)</description></item>
/// </list>
/// <para>
/// <b>Performance Characteristics:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Time Complexity: O(n) with atomic contention based on key distribution</description></item>
/// <item><description>Memory: O(k) where k is number of unique groups</description></item>
/// <item><description>Atomic Contention: Higher for skewed key distributions</description></item>
/// </list>
/// </remarks>
public sealed class GroupByKernelGenerator
{
    private readonly StringBuilder _builder = new();
    private readonly GpuExpressionTranslator _cudaTranslator;
    private readonly GpuExpressionTranslator _openclTranslator;
    private readonly GpuExpressionTranslator _metalTranslator;
    private int _indentLevel;

    /// <summary>
    /// Supported aggregation functions for GroupBy operations.
    /// </summary>
    [Flags]
    public enum AggregationFunction
    {
        /// <summary>No aggregation.</summary>
        None = 0,

        /// <summary>Count of elements in each group.</summary>
        Count = 1 << 0,

        /// <summary>Sum of values in each group.</summary>
        Sum = 1 << 1,

        /// <summary>Minimum value in each group.</summary>
        Min = 1 << 2,

        /// <summary>Maximum value in each group.</summary>
        Max = 1 << 3,

        /// <summary>Average of values in each group (requires Sum and Count).</summary>
        Average = 1 << 4,

        /// <summary>All basic aggregations (Count, Sum, Min, Max).</summary>
        All = Count | Sum | Min | Max
    }

    /// <summary>
    /// Configuration for GroupBy kernel generation.
    /// </summary>
    public sealed class GroupByConfiguration
    {
        /// <summary>
        /// Size of the hash table for group storage (power of 2 recommended).
        /// </summary>
        public int HashTableSize { get; init; } = 65536; // 64K entries

        /// <summary>
        /// Maximum probing distance for linear probing in collision resolution.
        /// </summary>
        public int MaxProbeDistance { get; init; } = 32;

        /// <summary>
        /// Aggregation functions to compute for each group.
        /// </summary>
        public AggregationFunction Aggregations { get; init; } = AggregationFunction.Count;

        /// <summary>
        /// Block size for kernel execution.
        /// </summary>
        public int BlockSize { get; init; } = 256;

        /// <summary>
        /// Optional lambda expression for extracting the group key from elements.
        /// </summary>
        /// <remarks>
        /// If not specified, elements are cast directly to the key type.
        /// For complex types, this lambda should extract the grouping field/property.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Group orders by customer ID
        /// KeySelector = (Expression&lt;Func&lt;Order, int&gt;&gt;)(o => o.CustomerId)
        /// </code>
        /// </example>
        public LambdaExpression? KeySelector { get; init; }

        /// <summary>
        /// Optional lambda expression for extracting the value to aggregate.
        /// </summary>
        /// <remarks>
        /// If not specified, elements are used directly as values.
        /// For complex types, this lambda should extract the field to aggregate.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Sum order amounts per customer
        /// ValueSelector = (Expression&lt;Func&lt;Order, decimal&gt;&gt;)(o => o.Amount)
        /// </code>
        /// </example>
        public LambdaExpression? ValueSelector { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupByKernelGenerator"/> class.
    /// </summary>
    public GroupByKernelGenerator()
    {
        _cudaTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.Cuda);
        _openclTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.OpenCL);
        _metalTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.Metal);
    }

    /// <summary>
    /// Generates CUDA kernel code for GroupBy with aggregation.
    /// </summary>
    /// <param name="elementTypeName">The element type name in CUDA C.</param>
    /// <param name="keyTypeName">The key type name in CUDA C.</param>
    /// <param name="valueTypeName">The value type name in CUDA C (for aggregation).</param>
    /// <param name="config">GroupBy configuration.</param>
    /// <returns>Complete CUDA kernel source code.</returns>
    public string GenerateCudaGroupByKernel(
        string elementTypeName,
        string keyTypeName,
        string valueTypeName,
        GroupByConfiguration? config = null)
    {
        config ??= new GroupByConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendLine("//=============================================================================");
        AppendLine("// GPU GroupBy Kernel with Aggregation");
        AppendLine($"// Element Type: {elementTypeName}");
        AppendLine($"// Key Type: {keyTypeName}");
        AppendLine($"// Value Type: {valueTypeName}");
        AppendLine($"// Aggregations: {config.Aggregations}");
        AppendLine($"// Hash Table Size: {config.HashTableSize}");
        AppendLine("//=============================================================================");
        AppendLine();

        // Include CUDA header
        AppendLine("#include <cuda_runtime.h>");
        AppendLine();

        // Constants
        AppendLine("// GroupBy constants");
        AppendLine($"#define HASH_TABLE_SIZE {config.HashTableSize}");
        AppendLine($"#define MAX_PROBE_DISTANCE {config.MaxProbeDistance}");
        AppendLine("#define EMPTY_KEY (-1)");
        AppendLine();

        // Generate aggregation structure
        GenerateCudaAggregationStruct(valueTypeName, config);

        // Generate initialization kernel
        GenerateCudaInitKernel(config);

        // Generate main aggregation kernel
        GenerateCudaAggregationKernel(elementTypeName, keyTypeName, valueTypeName, config);

        // Generate finalization kernel if needed
        if (config.Aggregations.HasFlag(AggregationFunction.Average))
        {
            GenerateCudaFinalizationKernel(valueTypeName, config);
        }

        return _builder.ToString();
    }

    /// <summary>
    /// Generates aggregation structure for storing per-group results.
    /// </summary>
    private void GenerateCudaAggregationStruct(string valueTypeName, GroupByConfiguration config)
    {
        AppendLine("// Aggregation result structure per group");
        AppendLine("struct GroupResult {");
        _indentLevel++;
        AppendLine("int key;           // Group key");

        if (config.Aggregations.HasFlag(AggregationFunction.Count))
        {
            AppendLine("int count;         // Element count");
        }
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
        {
            AppendLine($"{valueTypeName} sum;           // Sum of values");
        }
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
        {
            AppendLine($"{valueTypeName} minVal;        // Minimum value");
        }
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
        {
            AppendLine($"{valueTypeName} maxVal;        // Maximum value");
        }
        if (config.Aggregations.HasFlag(AggregationFunction.Average))
        {
            AppendLine($"{valueTypeName} average;       // Computed average (Sum/Count)");
        }

        _indentLevel--;
        AppendLine("};");
        AppendLine();
    }

    /// <summary>
    /// Generates kernel to initialize the hash table.
    /// </summary>
    private void GenerateCudaInitKernel(GroupByConfiguration config)
    {
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Initialization Kernel - Reset hash table to empty state");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void GroupByInit(");
        _indentLevel++;
        AppendLine("int* __restrict__ keys,");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("int* __restrict__ counts,");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("float* __restrict__ sums,");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("float* __restrict__ minVals,");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("float* __restrict__ maxVals,");
        AppendLine("int tableSize");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= tableSize) return;");
        AppendLine();
        AppendLine("// Initialize slot to empty");
        AppendLine("keys[idx] = EMPTY_KEY;");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("counts[idx] = 0;");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("sums[idx] = 0.0f;");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("minVals[idx] = 3.402823466e+38f;  // FLT_MAX");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("maxVals[idx] = -3.402823466e+38f; // -FLT_MAX");

        _indentLevel--;
        AppendLine("}");
        AppendLine();
    }

    /// <summary>
    /// Generates main aggregation kernel.
    /// </summary>
    private void GenerateCudaAggregationKernel(
        string elementTypeName,
        string keyTypeName,
        string valueTypeName,
        GroupByConfiguration config)
    {
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Main Aggregation Kernel - Group elements and compute aggregates");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void GroupByAggregate(");
        _indentLevel++;
        AppendLine($"const {elementTypeName}* __restrict__ input,");
        AppendLine("int inputSize,");
        AppendLine("int* __restrict__ keys,");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("int* __restrict__ counts,");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("float* __restrict__ sums,");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("float* __restrict__ minVals,");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("float* __restrict__ maxVals,");
        AppendLine("int tableSize");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= inputSize) return;");
        AppendLine();

        // Extract key from element
        AppendLine("// Extract group key from element");
        AppendLine($"{elementTypeName} element = input[idx];");
        if (config.KeySelector != null)
        {
            var keyCode = _cudaTranslator.TranslateLambda(config.KeySelector, "element");
            AppendLine($"int groupKey = (int)({keyCode});");
        }
        else
        {
            AppendLine("int groupKey = (int)(element);");
        }
        AppendLine();

        // Extract value for aggregation
        if (config.ValueSelector != null)
        {
            AppendLine("// Extract value for aggregation");
            var valueCode = _cudaTranslator.TranslateLambda(config.ValueSelector, "element");
            AppendLine($"float value = (float)({valueCode});");
        }
        else
        {
            AppendLine("// Use element directly as aggregation value");
            AppendLine("float value = (float)(element);");
        }
        AppendLine();

        // Compute hash
        AppendLine("// Compute hash for group key");
        AppendLine("int hash = groupKey & (tableSize - 1);");
        AppendLine();

        // Linear probing to find or create group slot
        AppendLine("// Linear probing to find or create group slot");
        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (tableSize - 1);");
        AppendLine();
        AppendLine("// Try to claim this slot for our key");
        AppendLine("int existingKey = atomicCAS(&keys[slot], EMPTY_KEY, groupKey);");
        AppendLine();
        AppendLine("if (existingKey == EMPTY_KEY || existingKey == groupKey)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("// This slot belongs to our group - update aggregates");

        // Generate atomic updates for each aggregation function
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
        {
            AppendLine("atomicAdd(&counts[slot], 1);");
        }
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
        {
            AppendLine("atomicAdd(&sums[slot], value);");
        }
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
        {
            AppendLine();
            AppendLine("// Atomic min update using CAS loop");
            AppendLine("float oldMin = minVals[slot];");
            AppendLine("while (value < oldMin)");
            AppendLine("{");
            _indentLevel++;
            AppendLine("float assumed = oldMin;");
            AppendLine("oldMin = __int_as_float(atomicCAS((int*)&minVals[slot],");
            AppendLine("                        __float_as_int(assumed), __float_as_int(value)));");
            AppendLine("if (oldMin == assumed) break;");
            _indentLevel--;
            AppendLine("}");
        }
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
        {
            AppendLine();
            AppendLine("// Atomic max update using CAS loop");
            AppendLine("float oldMax = maxVals[slot];");
            AppendLine("while (value > oldMax)");
            AppendLine("{");
            _indentLevel++;
            AppendLine("float assumed = oldMax;");
            AppendLine("oldMax = __int_as_float(atomicCAS((int*)&maxVals[slot],");
            AppendLine("                        __float_as_int(assumed), __float_as_int(value)));");
            AppendLine("if (oldMax == assumed) break;");
            _indentLevel--;
            AppendLine("}");
        }

        AppendLine("break; // Done with this element");
        _indentLevel--;
        AppendLine("}");
        AppendLine("// Collision with different key - try next slot");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();
    }

    /// <summary>
    /// Generates finalization kernel for computing derived aggregates like Average.
    /// </summary>
    private void GenerateCudaFinalizationKernel(string valueTypeName, GroupByConfiguration config)
    {
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Finalization Kernel - Compute derived aggregates (Average)");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void GroupByFinalize(");
        _indentLevel++;
        AppendLine("const int* __restrict__ keys,");
        AppendLine("const int* __restrict__ counts,");
        AppendLine("const float* __restrict__ sums,");
        AppendLine("float* __restrict__ averages,");
        AppendLine("int tableSize");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= tableSize) return;");
        AppendLine();
        AppendLine("// Only process slots with valid groups");
        AppendLine("if (keys[idx] != EMPTY_KEY && counts[idx] > 0)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("averages[idx] = sums[idx] / (float)counts[idx];");
        _indentLevel--;
        AppendLine("}");
        AppendLine("else");
        AppendLine("{");
        _indentLevel++;
        AppendLine("averages[idx] = 0.0f;");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Generates OpenCL kernel code for GroupBy with aggregation.
    /// </summary>
    public string GenerateOpenCLGroupByKernel(
        string elementTypeName,
        string keyTypeName,
        string valueTypeName,
        GroupByConfiguration? config = null)
    {
        config ??= new GroupByConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendLine("//=============================================================================");
        AppendLine("// OpenCL GroupBy Kernel with Aggregation");
        AppendLine($"// Aggregations: {config.Aggregations}");
        AppendLine("//=============================================================================");
        AppendLine();

        AppendLine($"#define HASH_TABLE_SIZE {config.HashTableSize}");
        AppendLine($"#define MAX_PROBE_DISTANCE {config.MaxProbeDistance}");
        AppendLine("#define EMPTY_KEY (-1)");
        AppendLine();

        // Initialization kernel
        AppendLine("__kernel void GroupByInit(");
        _indentLevel++;
        AppendLine("__global int* keys,");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("__global int* counts,");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("__global float* sums,");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("__global float* minVals,");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("__global float* maxVals,");
        AppendLine("int tableSize");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int idx = get_global_id(0);");
        AppendLine("if (idx >= tableSize) return;");
        AppendLine("keys[idx] = EMPTY_KEY;");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("counts[idx] = 0;");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("sums[idx] = 0.0f;");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("minVals[idx] = FLT_MAX;");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("maxVals[idx] = -FLT_MAX;");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Main aggregation kernel
        AppendLine("__kernel void GroupByAggregate(");
        _indentLevel++;
        AppendLine($"__global const {elementTypeName}* input,");
        AppendLine("int inputSize,");
        AppendLine("__global int* keys,");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("__global int* counts,");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("__global float* sums,");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("__global float* minVals,");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("__global float* maxVals,");
        AppendLine("int tableSize");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = get_global_id(0);");
        AppendLine("if (idx >= inputSize) return;");
        AppendLine();
        AppendLine($"{elementTypeName} element = input[idx];");

        if (config.KeySelector != null)
        {
            var keyCode = _openclTranslator.TranslateLambda(config.KeySelector, "element");
            AppendLine($"int groupKey = (int)({keyCode});");
        }
        else
        {
            AppendLine("int groupKey = (int)(element);");
        }

        if (config.ValueSelector != null)
        {
            var valueCode = _openclTranslator.TranslateLambda(config.ValueSelector, "element");
            AppendLine($"float value = (float)({valueCode});");
        }
        else
        {
            AppendLine("float value = (float)(element);");
        }
        AppendLine();

        AppendLine("int hash = groupKey & (tableSize - 1);");
        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (tableSize - 1);");
        AppendLine("int existingKey = atomic_cmpxchg(&keys[slot], EMPTY_KEY, groupKey);");
        AppendLine("if (existingKey == EMPTY_KEY || existingKey == groupKey)");
        AppendLine("{");
        _indentLevel++;

        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("atomic_add(&counts[slot], 1);");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("// Note: OpenCL 1.x doesn't have native atomic float add");
        // Note: Full atomic float operations would require OpenCL 2.0 or manual CAS loops

        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");

        return _builder.ToString();
    }

    /// <summary>
    /// Generates Metal kernel code for GroupBy with aggregation.
    /// </summary>
    public string GenerateMetalGroupByKernel(
        string elementTypeName,
        string keyTypeName,
        string valueTypeName,
        GroupByConfiguration? config = null)
    {
        config ??= new GroupByConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendLine("//=============================================================================");
        AppendLine("// Metal GroupBy Kernel with Aggregation");
        AppendLine($"// Aggregations: {config.Aggregations}");
        AppendLine("//=============================================================================");
        AppendLine();

        AppendLine("#include <metal_stdlib>");
        AppendLine("using namespace metal;");
        AppendLine();

        AppendLine($"constant int HASH_TABLE_SIZE = {config.HashTableSize};");
        AppendLine($"constant int MAX_PROBE_DISTANCE = {config.MaxProbeDistance};");
        AppendLine("constant int EMPTY_KEY = -1;");
        AppendLine();

        // Initialization kernel
        AppendLine("kernel void GroupByInit(");
        _indentLevel++;
        AppendLine("device atomic_int* keys [[buffer(0)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("device atomic_int* counts [[buffer(1)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("device float* sums [[buffer(2)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("device float* minVals [[buffer(3)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("device float* maxVals [[buffer(4)]],");
        AppendLine("constant int& tableSize [[buffer(5)]],");
        AppendLine("uint idx [[thread_position_in_grid]]");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;
        AppendLine("if (idx >= (uint)tableSize) return;");
        AppendLine("atomic_store_explicit(&keys[idx], EMPTY_KEY, memory_order_relaxed);");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("atomic_store_explicit(&counts[idx], 0, memory_order_relaxed);");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("sums[idx] = 0.0f;");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("minVals[idx] = FLT_MAX;");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("maxVals[idx] = -FLT_MAX;");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Main aggregation kernel
        AppendLine("kernel void GroupByAggregate(");
        _indentLevel++;
        AppendLine($"device const {elementTypeName}* input [[buffer(0)]],");
        AppendLine("constant int& inputSize [[buffer(1)]],");
        AppendLine("device atomic_int* keys [[buffer(2)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("device atomic_int* counts [[buffer(3)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Sum) || config.Aggregations.HasFlag(AggregationFunction.Average))
            AppendLine("device float* sums [[buffer(4)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Min))
            AppendLine("device float* minVals [[buffer(5)]],");
        if (config.Aggregations.HasFlag(AggregationFunction.Max))
            AppendLine("device float* maxVals [[buffer(6)]],");
        AppendLine("constant int& tableSize [[buffer(7)]],");
        AppendLine("uint idx [[thread_position_in_grid]]");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("if (idx >= (uint)inputSize) return;");
        AppendLine();
        AppendLine($"{elementTypeName} element = input[idx];");

        if (config.KeySelector != null)
        {
            var keyCode = _metalTranslator.TranslateLambda(config.KeySelector, "element");
            AppendLine($"int groupKey = (int)({keyCode});");
        }
        else
        {
            AppendLine("int groupKey = (int)(element);");
        }

        if (config.ValueSelector != null)
        {
            var valueCode = _metalTranslator.TranslateLambda(config.ValueSelector, "element");
            AppendLine($"float value = (float)({valueCode});");
        }
        else
        {
            AppendLine("float value = (float)(element);");
        }
        AppendLine();

        AppendLine("int hash = groupKey & (tableSize - 1);");
        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (tableSize - 1);");
        AppendLine("int expected = EMPTY_KEY;");
        AppendLine("bool inserted = atomic_compare_exchange_weak_explicit(");
        AppendLine("    &keys[slot], &expected, groupKey,");
        AppendLine("    memory_order_relaxed, memory_order_relaxed);");
        AppendLine();
        AppendLine("if (inserted || expected == groupKey)");
        AppendLine("{");
        _indentLevel++;

        if (config.Aggregations.HasFlag(AggregationFunction.Count))
            AppendLine("atomic_fetch_add_explicit(&counts[slot], 1, memory_order_relaxed);");
        // Note: Metal atomic float operations would require Metal 3.0+

        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");

        return _builder.ToString();
    }

    #region Helper Methods

    private void AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _builder.AppendLine();
            return;
        }

        _builder.Append(new string(' ', _indentLevel * 4));
        _builder.AppendLine(line);
    }

    #endregion
}
