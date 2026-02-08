// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Text;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Specialized generator for GPU-accelerated Join operations.
/// </summary>
/// <remarks>
/// <para>
/// This generator implements a two-phase hash join algorithm optimized for GPU execution:
/// </para>
/// <list type="bullet">
/// <item><description><b>Phase 1 (Build):</b> Populate a hash table from the inner (build) table</description></item>
/// <item><description><b>Phase 2 (Probe):</b> Probe the hash table using the outer table to find matches</description></item>
/// <item><description><b>Phase 3 (Gather):</b> Collect matched pairs into the output buffer</description></item>
/// </list>
/// <para>
/// <b>Supported Join Types:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Inner Join: Return only matching pairs from both tables</description></item>
/// <item><description>Left Join: Return all left rows, with nulls for non-matching right</description></item>
/// <item><description>Semi Join: Return left rows that have matches (existence check)</description></item>
/// <item><description>Anti Join: Return left rows that have no matches</description></item>
/// </list>
/// <para>
/// <b>Performance Characteristics:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Build Phase: O(n) where n = inner table size</description></item>
/// <item><description>Probe Phase: O(m) where m = outer table size, with O(1) expected per probe</description></item>
/// <item><description>Memory: O(n) for hash table in global memory</description></item>
/// <item><description>Optimal for: n &lt; 1M rows, m any size</description></item>
/// </list>
/// </remarks>
public class JoinKernelGenerator
{
    private readonly StringBuilder _builder = new();
    private readonly GpuExpressionTranslator _cudaTranslator;
    private readonly GpuExpressionTranslator _openclTranslator;
    private readonly GpuExpressionTranslator _metalTranslator;
    private int _indentLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="JoinKernelGenerator"/> class.
    /// </summary>
    public JoinKernelGenerator()
    {
        _cudaTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.Cuda);
        _openclTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.OpenCL);
        _metalTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.Metal);
    }

    /// <summary>
    /// Supported join types for GPU execution.
    /// </summary>
    public enum JoinType
    {
        /// <summary>Inner join: returns matched pairs only.</summary>
        Inner = 0,
        /// <summary>Left outer join: returns all left rows with optional right matches.</summary>
        LeftOuter = 1,
        /// <summary>Semi join: returns left rows that have at least one match.</summary>
        Semi = 2,
        /// <summary>Anti join: returns left rows that have no matches.</summary>
        Anti = 3
    }

    /// <summary>
    /// Configuration for join kernel generation.
    /// </summary>
    public sealed class JoinConfiguration
    {
        /// <summary>Size of the hash table (power of 2 recommended).</summary>
        public int HashTableSize { get; init; } = 65536; // 64K entries

        /// <summary>Maximum probing distance for linear probing.</summary>
        public int MaxProbeDistance { get; init; } = 32;

        /// <summary>The type of join to perform.</summary>
        public JoinType JoinType { get; init; } = JoinType.Inner;

        /// <summary>Whether to use cooperative groups for large joins.</summary>
        public bool UseCooperativeGroups { get; init; }

        /// <summary>Block size for kernel execution.</summary>
        public int BlockSize { get; init; } = 256;

        /// <summary>
        /// Optional lambda expression for extracting key from left (outer) table elements.
        /// </summary>
        /// <remarks>
        /// If not specified, elements are cast directly to the key type.
        /// For complex types, this lambda should extract the join key field/property.
        /// </remarks>
        /// <example>
        /// <code>
        /// // For joining on customer.Id
        /// OuterKeySelector = (Expression&lt;Func&lt;Customer, int&gt;&gt;)(c => c.Id)
        /// </code>
        /// </example>
        public LambdaExpression? OuterKeySelector { get; init; }

        /// <summary>
        /// Optional lambda expression for extracting key from right (inner/build) table elements.
        /// </summary>
        /// <remarks>
        /// If not specified, elements are cast directly to the key type.
        /// For complex types, this lambda should extract the join key field/property.
        /// </remarks>
        /// <example>
        /// <code>
        /// // For joining on order.CustomerId
        /// InnerKeySelector = (Expression&lt;Func&lt;Order, int&gt;&gt;)(o => o.CustomerId)
        /// </code>
        /// </example>
        public LambdaExpression? InnerKeySelector { get; init; }

        /// <summary>
        /// Optional lambda expression for creating the result from matched pairs.
        /// </summary>
        /// <remarks>
        /// If not specified, output contains the original left and right element values.
        /// </remarks>
        public LambdaExpression? ResultSelector { get; init; }
    }

    /// <summary>
    /// Generates a complete CUDA hash join kernel for two-table equi-join.
    /// </summary>
    /// <param name="leftTypeName">C type name for left (outer) table elements.</param>
    /// <param name="rightTypeName">C type name for right (inner/build) table elements.</param>
    /// <param name="keyTypeName">C type name for join keys.</param>
    /// <param name="config">Join configuration.</param>
    /// <returns>Complete CUDA kernel source code.</returns>
    public string GenerateCudaJoinKernel(
        string leftTypeName,
        string rightTypeName,
        string keyTypeName,
        JoinConfiguration? config = null)
    {
        config ??= new JoinConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendCudaHashJoinKernel(leftTypeName, rightTypeName, keyTypeName, config);

        return _builder.ToString();
    }

    /// <summary>
    /// Generates CUDA kernel for the build phase (populating hash table).
    /// </summary>
    /// <param name="typeName">Element type name.</param>
    /// <param name="keyExpression">Expression to extract key from element.</param>
    /// <param name="config">Join configuration.</param>
    /// <returns>CUDA kernel for build phase.</returns>
    public string GenerateCudaBuildPhaseKernel(
        string typeName,
        string keyExpression,
        JoinConfiguration? config = null)
    {
        config ??= new JoinConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendLine("// Hash Join - Build Phase Kernel");
        AppendLine("// Populates hash table from inner (build) table");
        AppendLine();

        // Kernel signature
        AppendLine("extern \"C\" __global__ void HashJoinBuild(");
        _indentLevel++;
        AppendLine($"const {typeName}* __restrict__ buildInput,  // Inner table");
        AppendLine("const int buildSize,                         // Inner table size");
        AppendLine("int* __restrict__ hashTable,                 // Hash table (key storage)");
        AppendLine("int* __restrict__ valueTable,                // Value table (indices)");
        AppendLine($"const int hashTableSize                     // Hash table size ({config.HashTableSize})");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= buildSize) return;");
        AppendLine();

        // Extract key and compute hash
        AppendLine($"// Extract key from element");
        AppendLine($"int key = (int)({keyExpression});");
        AppendLine("int hash = key & (hashTableSize - 1);");
        AppendLine();

        // Linear probing insertion
        AppendLine("// Linear probing insertion");
        AppendLine($"const int EMPTY = -1;");
        AppendLine($"for (int probe = 0; probe < {config.MaxProbeDistance}; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (hashTableSize - 1);");
        AppendLine("int expected = EMPTY;");
        AppendLine("int inserted = atomicCAS(&hashTable[slot], expected, key);");
        AppendLine("if (inserted == EMPTY)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("// Successfully inserted - store index");
        AppendLine("valueTable[slot] = idx;");
        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        AppendLine("else if (inserted == key)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("// Key already exists - handle collision (store first occurrence)");
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
    /// Generates CUDA kernel for the probe phase (finding matches).
    /// </summary>
    /// <param name="leftTypeName">Left (outer) table element type.</param>
    /// <param name="rightTypeName">Right (inner) table element type.</param>
    /// <param name="keyExpression">Expression to extract key from left element.</param>
    /// <param name="config">Join configuration.</param>
    /// <returns>CUDA kernel for probe phase.</returns>
    public string GenerateCudaProbePhaseKernel(
        string leftTypeName,
        string rightTypeName,
        string keyExpression,
        JoinConfiguration? config = null)
    {
        config ??= new JoinConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendLine("// Hash Join - Probe Phase Kernel");
        AppendLine($"// {config.JoinType} Join: Probes hash table with outer table");
        AppendLine();

        // Kernel signature
        AppendLine("extern \"C\" __global__ void HashJoinProbe(");
        _indentLevel++;
        AppendLine($"const {leftTypeName}* __restrict__ probeInput,   // Outer table");
        AppendLine("const int probeSize,                              // Outer table size");
        AppendLine("const int* __restrict__ hashTable,                // Hash table (keys)");
        AppendLine("const int* __restrict__ valueTable,               // Value table (indices)");
        AppendLine("const int hashTableSize,                          // Hash table size");
        AppendLine($"const {rightTypeName}* __restrict__ buildInput,  // Inner table (for value retrieval)");

        // Output depends on join type
        switch (config.JoinType)
        {
            case JoinType.Inner:
            case JoinType.LeftOuter:
                AppendLine("int* __restrict__ leftIndices,                    // Output: left indices");
                AppendLine("int* __restrict__ rightIndices,                   // Output: right indices");
                AppendLine("int* __restrict__ matchCount                      // Atomic counter");
                break;
            case JoinType.Semi:
            case JoinType.Anti:
                AppendLine("int* __restrict__ outputIndices,                  // Output: filtered left indices");
                AppendLine("int* __restrict__ matchCount                      // Atomic counter");
                break;
        }

        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= probeSize) return;");
        AppendLine();

        // Extract key and compute hash
        AppendLine($"// Extract key from probe element");
        AppendLine($"int key = (int)({keyExpression});");
        AppendLine("int hash = key & (hashTableSize - 1);");
        AppendLine();

        // Linear probing search
        AppendLine("// Linear probing search");
        AppendLine("const int EMPTY = -1;");
        AppendLine("int matchedRightIdx = -1;");
        AppendLine($"for (int probe = 0; probe < {config.MaxProbeDistance}; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (hashTableSize - 1);");
        AppendLine("int slotKey = hashTable[slot];");
        AppendLine();
        AppendLine("if (slotKey == EMPTY)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("break; // Key not found");
        _indentLevel--;
        AppendLine("}");
        AppendLine("if (slotKey == key)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("matchedRightIdx = valueTable[slot];");
        AppendLine("break; // Key found");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Output based on join type
        switch (config.JoinType)
        {
            case JoinType.Inner:
                AppendLine("// Inner Join: Output only if match found");
                AppendLine("if (matchedRightIdx >= 0)");
                AppendLine("{");
                _indentLevel++;
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("leftIndices[outIdx] = idx;");
                AppendLine("rightIndices[outIdx] = matchedRightIdx;");
                _indentLevel--;
                AppendLine("}");
                break;

            case JoinType.LeftOuter:
                AppendLine("// Left Outer Join: Output all left rows");
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("leftIndices[outIdx] = idx;");
                AppendLine("rightIndices[outIdx] = matchedRightIdx; // -1 if no match");
                break;

            case JoinType.Semi:
                AppendLine("// Semi Join: Output left index if match exists");
                AppendLine("if (matchedRightIdx >= 0)");
                AppendLine("{");
                _indentLevel++;
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("outputIndices[outIdx] = idx;");
                _indentLevel--;
                AppendLine("}");
                break;

            case JoinType.Anti:
                AppendLine("// Anti Join: Output left index if NO match");
                AppendLine("if (matchedRightIdx < 0)");
                AppendLine("{");
                _indentLevel++;
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("outputIndices[outIdx] = idx;");
                _indentLevel--;
                AppendLine("}");
                break;
        }

        _indentLevel--;
        AppendLine("}");

        return _builder.ToString();
    }

    /// <summary>
    /// Generates a combined CUDA kernel that performs complete hash join.
    /// </summary>
    private void AppendCudaHashJoinKernel(
        string leftTypeName,
        string rightTypeName,
        string keyTypeName,
        JoinConfiguration config)
    {
        AppendLine("//=============================================================================");
        AppendLine("// GPU Hash Join Kernel");
        AppendLine($"// Join Type: {config.JoinType}");
        AppendLine($"// Hash Table Size: {config.HashTableSize}");
        AppendLine("// Algorithm: Two-phase hash join (Build + Probe)");
        AppendLine("//=============================================================================");
        AppendLine();

        // Include CUDA header
        AppendLine("#include <cuda_runtime.h>");
        AppendLine();

        // Constants
        AppendLine("// Join constants");
        AppendLine($"#define HASH_TABLE_SIZE {config.HashTableSize}");
        AppendLine($"#define MAX_PROBE_DISTANCE {config.MaxProbeDistance}");
        AppendLine("#define EMPTY_SLOT (-1)");
        AppendLine();

        // Build phase kernel
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Phase 1: Build Hash Table from Inner (Right) Table");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void HashJoinBuild(");
        _indentLevel++;
        AppendLine($"const {rightTypeName}* __restrict__ buildTable,");
        AppendLine("const int buildSize,");
        AppendLine("int* __restrict__ hashKeys,");
        AppendLine("int* __restrict__ hashValues");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine();

        // Collaborative hash table initialization
        AppendLine("// Initialize hash table (collaborative)");
        AppendLine("for (int i = idx; i < HASH_TABLE_SIZE; i += gridDim.x * blockDim.x)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("hashKeys[i] = EMPTY_SLOT;");
        AppendLine("hashValues[i] = EMPTY_SLOT;");
        _indentLevel--;
        AppendLine("}");
        AppendLine("__syncthreads();");
        AppendLine();

        AppendLine("if (idx >= buildSize) return;");
        AppendLine();

        AppendLine("// Extract key and insert into hash table");
        AppendLine($"{rightTypeName} element = buildTable[idx];");

        // Generate key extraction code using key selector if provided
        if (config.InnerKeySelector != null)
        {
            var keyCode = _cudaTranslator.TranslateLambda(config.InnerKeySelector, "element");
            AppendLine($"int key = (int)({keyCode});");
        }
        else
        {
            // Default: cast element directly to key type
            AppendLine($"int key = (int)(element);");
        }
        AppendLine("int hash = key & (HASH_TABLE_SIZE - 1);");
        AppendLine();

        AppendLine("// Linear probing insertion");
        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (HASH_TABLE_SIZE - 1);");
        AppendLine("int inserted = atomicCAS(&hashKeys[slot], EMPTY_SLOT, key);");
        AppendLine("if (inserted == EMPTY_SLOT)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("hashValues[slot] = idx;");
        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Probe phase kernel
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Phase 2: Probe Hash Table with Outer (Left) Table");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void HashJoinProbe(");
        _indentLevel++;
        AppendLine($"const {leftTypeName}* __restrict__ probeTable,");
        AppendLine("const int probeSize,");
        AppendLine("const int* __restrict__ hashKeys,");
        AppendLine("const int* __restrict__ hashValues,");
        AppendLine("int* __restrict__ leftIndices,");
        AppendLine("int* __restrict__ rightIndices,");
        AppendLine("int* __restrict__ matchCount");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= probeSize) return;");
        AppendLine();

        AppendLine("// Extract key and search hash table");
        AppendLine($"{leftTypeName} element = probeTable[idx];");

        // Generate key extraction code using key selector if provided
        if (config.OuterKeySelector != null)
        {
            var keyCode = _cudaTranslator.TranslateLambda(config.OuterKeySelector, "element");
            AppendLine($"int key = (int)({keyCode});");
        }
        else
        {
            // Default: cast element directly to key type
            AppendLine($"int key = (int)(element);");
        }
        AppendLine("int hash = key & (HASH_TABLE_SIZE - 1);");
        AppendLine();

        AppendLine("// Linear probing search");
        AppendLine("int matchedIdx = -1;");
        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (HASH_TABLE_SIZE - 1);");
        AppendLine("int slotKey = hashKeys[slot];");
        AppendLine();
        AppendLine("if (slotKey == EMPTY_SLOT) break;");
        AppendLine("if (slotKey == key)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("matchedIdx = hashValues[slot];");
        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Output based on join type
        switch (config.JoinType)
        {
            case JoinType.Inner:
                AppendLine("// Inner Join: Output matched pairs");
                AppendLine("if (matchedIdx >= 0)");
                AppendLine("{");
                _indentLevel++;
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("leftIndices[outIdx] = idx;");
                AppendLine("rightIndices[outIdx] = matchedIdx;");
                _indentLevel--;
                AppendLine("}");
                break;

            case JoinType.LeftOuter:
                AppendLine("// Left Outer Join: Output all left with right (-1 if no match)");
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("leftIndices[outIdx] = idx;");
                AppendLine("rightIndices[outIdx] = matchedIdx;");
                break;

            case JoinType.Semi:
                AppendLine("// Semi Join: Output left indices with matches");
                AppendLine("if (matchedIdx >= 0)");
                AppendLine("{");
                _indentLevel++;
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("leftIndices[outIdx] = idx;");
                _indentLevel--;
                AppendLine("}");
                break;

            case JoinType.Anti:
                AppendLine("// Anti Join: Output left indices without matches");
                AppendLine("if (matchedIdx < 0)");
                AppendLine("{");
                _indentLevel++;
                AppendLine("int outIdx = atomicAdd(matchCount, 1);");
                AppendLine("leftIndices[outIdx] = idx;");
                _indentLevel--;
                AppendLine("}");
                break;
        }

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Result gathering kernel (for materializing join results)
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Phase 3: Materialize Join Results");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void HashJoinGather(");
        _indentLevel++;
        AppendLine($"const {leftTypeName}* __restrict__ leftTable,");
        AppendLine($"const {rightTypeName}* __restrict__ rightTable,");
        AppendLine("const int* __restrict__ leftIndices,");
        AppendLine("const int* __restrict__ rightIndices,");
        AppendLine("const int resultCount,");
        AppendLine($"{leftTypeName}* __restrict__ leftOutput,");
        AppendLine($"{rightTypeName}* __restrict__ rightOutput");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= resultCount) return;");
        AppendLine();

        AppendLine("// Gather matched elements");
        AppendLine("int leftIdx = leftIndices[idx];");
        AppendLine("int rightIdx = rightIndices[idx];");
        AppendLine();
        AppendLine("leftOutput[idx] = leftTable[leftIdx];");
        AppendLine("rightOutput[idx] = (rightIdx >= 0) ? rightTable[rightIdx] : 0;");

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Generates OpenCL hash join kernels.
    /// </summary>
    public string GenerateOpenCLJoinKernel(
        string leftTypeName,
        string rightTypeName,
        string keyTypeName,
        JoinConfiguration? config = null)
    {
        config ??= new JoinConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendLine("//=============================================================================");
        AppendLine("// OpenCL Hash Join Kernel");
        AppendLine($"// Join Type: {config.JoinType}");
        AppendLine("//=============================================================================");
        AppendLine();

        AppendLine($"#define HASH_TABLE_SIZE {config.HashTableSize}");
        AppendLine($"#define MAX_PROBE_DISTANCE {config.MaxProbeDistance}");
        AppendLine("#define EMPTY_SLOT (-1)");
        AppendLine();

        // Build kernel
        AppendLine("__kernel void HashJoinBuild(");
        _indentLevel++;
        AppendLine($"__global const {rightTypeName}* buildTable,");
        AppendLine("const int buildSize,");
        AppendLine("__global int* hashKeys,");
        AppendLine("__global int* hashValues");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = get_global_id(0);");
        AppendLine("if (idx >= buildSize) return;");
        AppendLine();

        AppendLine($"{rightTypeName} element = buildTable[idx];");

        // Generate key extraction code using key selector if provided
        if (config.InnerKeySelector != null)
        {
            var keyCode = _openclTranslator.TranslateLambda(config.InnerKeySelector, "element");
            AppendLine($"int key = (int)({keyCode});");
        }
        else
        {
            AppendLine("int key = (int)(element);");
        }
        AppendLine("int hash = key & (HASH_TABLE_SIZE - 1);");
        AppendLine();

        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (HASH_TABLE_SIZE - 1);");
        AppendLine("int inserted = atomic_cmpxchg(&hashKeys[slot], EMPTY_SLOT, key);");
        AppendLine("if (inserted == EMPTY_SLOT)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("hashValues[slot] = idx;");
        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Probe kernel
        AppendLine("__kernel void HashJoinProbe(");
        _indentLevel++;
        AppendLine($"__global const {leftTypeName}* probeTable,");
        AppendLine("const int probeSize,");
        AppendLine("__global const int* hashKeys,");
        AppendLine("__global const int* hashValues,");
        AppendLine("__global int* leftIndices,");
        AppendLine("__global int* rightIndices,");
        AppendLine("__global int* matchCount");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = get_global_id(0);");
        AppendLine("if (idx >= probeSize) return;");
        AppendLine();

        AppendLine($"{leftTypeName} element = probeTable[idx];");

        // Generate key extraction code using key selector if provided
        if (config.OuterKeySelector != null)
        {
            var keyCode = _openclTranslator.TranslateLambda(config.OuterKeySelector, "element");
            AppendLine($"int key = (int)({keyCode});");
        }
        else
        {
            AppendLine("int key = (int)(element);");
        }
        AppendLine("int hash = key & (HASH_TABLE_SIZE - 1);");
        AppendLine();

        AppendLine("int matchedIdx = -1;");
        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (HASH_TABLE_SIZE - 1);");
        AppendLine("int slotKey = hashKeys[slot];");
        AppendLine("if (slotKey == EMPTY_SLOT) break;");
        AppendLine("if (slotKey == key)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("matchedIdx = hashValues[slot];");
        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine("if (matchedIdx >= 0)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int outIdx = atomic_add(matchCount, 1);");
        AppendLine("leftIndices[outIdx] = idx;");
        AppendLine("rightIndices[outIdx] = matchedIdx;");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");

        return _builder.ToString();
    }

    /// <summary>
    /// Generates Metal hash join kernels.
    /// </summary>
    public string GenerateMetalJoinKernel(
        string leftTypeName,
        string rightTypeName,
        string keyTypeName,
        JoinConfiguration? config = null)
    {
        config ??= new JoinConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        AppendLine("//=============================================================================");
        AppendLine("// Metal Hash Join Kernel");
        AppendLine($"// Join Type: {config.JoinType}");
        AppendLine("//=============================================================================");
        AppendLine();

        AppendLine("#include <metal_stdlib>");
        AppendLine("using namespace metal;");
        AppendLine();

        AppendLine($"constant int HASH_TABLE_SIZE = {config.HashTableSize};");
        AppendLine($"constant int MAX_PROBE_DISTANCE = {config.MaxProbeDistance};");
        AppendLine("constant int EMPTY_SLOT = -1;");
        AppendLine();

        // Build kernel
        AppendLine("kernel void HashJoinBuild(");
        _indentLevel++;
        AppendLine($"device const {leftTypeName}* buildTable [[buffer(0)]],");
        AppendLine("constant int& buildSize [[buffer(1)]],");
        AppendLine("device atomic_int* hashKeys [[buffer(2)]],");
        AppendLine("device int* hashValues [[buffer(3)]],");
        AppendLine("uint idx [[thread_position_in_grid]]");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("if (idx >= (uint)buildSize) return;");
        AppendLine();

        AppendLine($"{leftTypeName} element = buildTable[idx];");

        // Generate key extraction code using key selector if provided
        if (config.InnerKeySelector != null)
        {
            var keyCode = _metalTranslator.TranslateLambda(config.InnerKeySelector, "element");
            AppendLine($"int key = (int)({keyCode});");
        }
        else
        {
            AppendLine("int key = (int)(element);");
        }
        AppendLine("int hash = key & (HASH_TABLE_SIZE - 1);");
        AppendLine();

        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (HASH_TABLE_SIZE - 1);");
        AppendLine("int expected = EMPTY_SLOT;");
        AppendLine("bool inserted = atomic_compare_exchange_weak_explicit(");
        AppendLine("    &hashKeys[slot], &expected, key,");
        AppendLine("    memory_order_relaxed, memory_order_relaxed);");
        AppendLine("if (inserted)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("hashValues[slot] = idx;");
        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Probe kernel
        AppendLine("kernel void HashJoinProbe(");
        _indentLevel++;
        AppendLine($"device const {leftTypeName}* probeTable [[buffer(0)]],");
        AppendLine("constant int& probeSize [[buffer(1)]],");
        AppendLine("device const int* hashKeys [[buffer(2)]],");
        AppendLine("device const int* hashValues [[buffer(3)]],");
        AppendLine("device int* leftIndices [[buffer(4)]],");
        AppendLine("device int* rightIndices [[buffer(5)]],");
        AppendLine("device atomic_int* matchCount [[buffer(6)]],");
        AppendLine("uint idx [[thread_position_in_grid]]");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("if (idx >= (uint)probeSize) return;");
        AppendLine();

        AppendLine($"{leftTypeName} element = probeTable[idx];");

        // Generate key extraction code using key selector if provided
        if (config.OuterKeySelector != null)
        {
            var keyCode = _metalTranslator.TranslateLambda(config.OuterKeySelector, "element");
            AppendLine($"int key = (int)({keyCode});");
        }
        else
        {
            AppendLine("int key = (int)(element);");
        }
        AppendLine("int hash = key & (HASH_TABLE_SIZE - 1);");
        AppendLine();

        AppendLine("int matchedIdx = -1;");
        AppendLine("for (int probe = 0; probe < MAX_PROBE_DISTANCE; probe++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int slot = (hash + probe) & (HASH_TABLE_SIZE - 1);");
        AppendLine("int slotKey = hashKeys[slot];");
        AppendLine("if (slotKey == EMPTY_SLOT) break;");
        AppendLine("if (slotKey == key)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("matchedIdx = hashValues[slot];");
        AppendLine("break;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine("if (matchedIdx >= 0)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("int outIdx = atomic_fetch_add_explicit(matchCount, 1, memory_order_relaxed);");
        AppendLine("leftIndices[outIdx] = idx;");
        AppendLine("rightIndices[outIdx] = matchedIdx;");
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
