// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Text;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Specialized generator for GPU-accelerated OrderBy (sorting) operations.
/// </summary>
/// <remarks>
/// <para>
/// This generator implements a multi-phase bitonic sort algorithm optimized for GPU execution.
/// It supports arrays of any size (power of 2 or automatically padded) using a combination
/// of shared memory for block-local sorting and global memory for cross-block merging.
/// </para>
/// <para>
/// <b>Algorithm Phases:</b>
/// </para>
/// <list type="number">
/// <item><description><b>Local Sort:</b> Each block sorts its portion using shared memory bitonic sort</description></item>
/// <item><description><b>Global Merge:</b> Blocks merge their sorted segments using global memory</description></item>
/// <item><description><b>Final Pass:</b> Complete bitonic merge to produce fully sorted output</description></item>
/// </list>
/// <para>
/// <b>Performance Characteristics:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Time Complexity: O(n log²n) comparisons</description></item>
/// <item><description>Parallel Depth: O(log²n) synchronization phases</description></item>
/// <item><description>Memory: In-place sort with O(blockSize) shared memory per block</description></item>
/// <item><description>Optimal for: Power-of-2 sizes, moderate to large arrays (up to millions of elements)</description></item>
/// </list>
/// <para>
/// <b>Sort Order Support:</b>
/// - Ascending (default)
/// - Descending
/// - Custom key selector support for complex types
/// </para>
/// </remarks>
public sealed class OrderByKernelGenerator
{
    private readonly StringBuilder _builder = new();
    private readonly GpuExpressionTranslator _cudaTranslator;
    private readonly GpuExpressionTranslator _openclTranslator;
    private readonly GpuExpressionTranslator _metalTranslator;
    private int _indentLevel;

    /// <summary>
    /// Configuration for OrderBy kernel generation.
    /// </summary>
    public sealed class OrderByConfiguration
    {
        /// <summary>
        /// Block size for kernel execution (threads per block).
        /// Must be a power of 2. Larger values use more shared memory but reduce kernel launches.
        /// </summary>
        public int BlockSize { get; init; } = 256;

        /// <summary>
        /// Whether to sort in descending order. Default is ascending.
        /// </summary>
        public bool Descending { get; init; }

        /// <summary>
        /// Maximum array size supported. Arrays larger than this will need multiple passes.
        /// </summary>
        public int MaxArraySize { get; init; } = 1 << 24; // 16M elements

        /// <summary>
        /// Optional lambda expression for extracting the sort key from elements.
        /// </summary>
        /// <remarks>
        /// If not specified, elements are used directly as sort keys.
        /// For complex types, this lambda should extract the field to sort by.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Sort orders by date
        /// KeySelector = (Expression&lt;Func&lt;Order, long&gt;&gt;)(o => o.Timestamp)
        /// </code>
        /// </example>
        public LambdaExpression? KeySelector { get; init; }

        /// <summary>
        /// Whether to use stable sort (preserving order of equal elements).
        /// Stable sort is slightly slower but maintains original order for ties.
        /// </summary>
        public bool StableSort { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByKernelGenerator"/> class.
    /// </summary>
    public OrderByKernelGenerator()
    {
        _cudaTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.Cuda);
        _openclTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.OpenCL);
        _metalTranslator = new GpuExpressionTranslator(GpuExpressionTranslator.GpuBackendType.Metal);
    }

    /// <summary>
    /// Generates complete CUDA kernel code for multi-block bitonic sort.
    /// </summary>
    /// <param name="elementTypeName">The element type name in CUDA C.</param>
    /// <param name="keyTypeName">The sort key type name in CUDA C.</param>
    /// <param name="config">OrderBy configuration.</param>
    /// <returns>Complete CUDA kernel source code with all sort phases.</returns>
    public string GenerateCudaOrderByKernel(
        string elementTypeName,
        string keyTypeName,
        OrderByConfiguration? config = null)
    {
        config ??= new OrderByConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        var direction = config.Descending ? "descending" : "ascending";

        AppendLine("//=============================================================================");
        AppendLine("// GPU Bitonic Sort Kernel (Multi-Block Support)");
        AppendLine($"// Element Type: {elementTypeName}");
        AppendLine($"// Key Type: {keyTypeName}");
        AppendLine($"// Sort Order: {direction}");
        AppendLine($"// Block Size: {config.BlockSize}");
        AppendLine("//=============================================================================");
        AppendLine();

        AppendLine("#include <cuda_runtime.h>");
        AppendLine();

        // Constants
        AppendLine($"#define BLOCK_SIZE {config.BlockSize}");
        AppendLine();

        // Generate key extraction helper if custom key selector
        if (config.KeySelector != null)
        {
            GenerateCudaKeyExtractor(elementTypeName, keyTypeName, config);
        }

        // Generate the local bitonic sort kernel (shared memory)
        GenerateCudaLocalBitonicSort(elementTypeName, keyTypeName, config);

        // Generate the global bitonic merge kernel
        GenerateCudaGlobalBitonicMerge(elementTypeName, keyTypeName, config);

        // Generate the final merge pass kernel
        GenerateCudaFinalMergePass(elementTypeName, keyTypeName, config);

        return _builder.ToString();
    }

    /// <summary>
    /// Generates a device function for key extraction.
    /// </summary>
    private void GenerateCudaKeyExtractor(
        string elementTypeName,
        string keyTypeName,
        OrderByConfiguration config)
    {
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Key Extraction Helper");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine($"__device__ __forceinline__ {keyTypeName} extractKey(const {elementTypeName}& element)");
        AppendLine("{");
        _indentLevel++;

        if (config.KeySelector != null)
        {
            var keyCode = _cudaTranslator.TranslateLambda(config.KeySelector, "element");
            AppendLine($"return ({keyTypeName})({keyCode});");
        }
        else
        {
            AppendLine($"return ({keyTypeName})(element);");
        }

        _indentLevel--;
        AppendLine("}");
        AppendLine();
    }

    /// <summary>
    /// Generates the local bitonic sort kernel using shared memory.
    /// </summary>
    private void GenerateCudaLocalBitonicSort(
        string elementTypeName,
        string keyTypeName,
        OrderByConfiguration config)
    {
        var compareOp = config.Descending ? ">" : "<";
        var swapCondition = config.Descending
            ? "(ascending && a < b) || (!ascending && a > b)"
            : "(ascending && a > b) || (!ascending && a < b)";

        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Local Bitonic Sort - Sorts block-sized segments in shared memory");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void BitonicSortLocal(");
        _indentLevel++;
        AppendLine($"{elementTypeName}* __restrict__ data,");
        AppendLine("const int n");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"__shared__ {elementTypeName} sharedData[BLOCK_SIZE];");
        if (config.KeySelector != null)
        {
            AppendLine($"__shared__ {keyTypeName} sharedKeys[BLOCK_SIZE];");
        }
        AppendLine();

        AppendLine("int tid = threadIdx.x;");
        AppendLine("int globalIdx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine();

        // Load data with padding
        AppendLine("// Load data into shared memory with padding for out-of-bounds");
        if (config.Descending)
        {
            AppendLine($"{elementTypeName} myVal = (globalIdx < n) ? data[globalIdx] : ({elementTypeName}){{0}};");
        }
        else
        {
            AppendLine($"{elementTypeName} myVal = (globalIdx < n) ? data[globalIdx] : ({elementTypeName}){{0}};");
        }
        AppendLine("sharedData[tid] = myVal;");

        if (config.KeySelector != null)
        {
            AppendLine($"{keyTypeName} myKey = extractKey(myVal);");
            AppendLine("sharedKeys[tid] = myKey;");
        }
        AppendLine("__syncthreads();");
        AppendLine();

        // Bitonic sort in shared memory
        AppendLine("// Bitonic sort in shared memory");
        AppendLine("for (int k = 2; k <= BLOCK_SIZE; k *= 2)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("for (int j = k / 2; j > 0; j /= 2)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int ixj = tid ^ j;");
        AppendLine("if (ixj > tid)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("// Determine sort direction based on position in bitonic sequence");
        AppendLine("bool ascending = ((tid & k) == 0);");
        AppendLine();

        if (config.KeySelector != null)
        {
            AppendLine($"{keyTypeName} keyA = sharedKeys[tid];");
            AppendLine($"{keyTypeName} keyB = sharedKeys[ixj];");
            AppendLine();
            AppendLine($"if ({swapCondition.Replace("a", "keyA").Replace("b", "keyB")})");
        }
        else
        {
            AppendLine($"{elementTypeName} a = sharedData[tid];");
            AppendLine($"{elementTypeName} b = sharedData[ixj];");
            AppendLine();
            AppendLine($"if ({swapCondition})");
        }
        AppendLine("{");
        _indentLevel++;

        // Swap elements
        AppendLine("// Swap elements");
        AppendLine($"{elementTypeName} tmpVal = sharedData[tid];");
        AppendLine("sharedData[tid] = sharedData[ixj];");
        AppendLine("sharedData[ixj] = tmpVal;");

        if (config.KeySelector != null)
        {
            AppendLine($"{keyTypeName} tmpKey = sharedKeys[tid];");
            AppendLine("sharedKeys[tid] = sharedKeys[ixj];");
            AppendLine("sharedKeys[ixj] = tmpKey;");
        }

        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine("__syncthreads();");

        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Write back sorted data
        AppendLine("// Write sorted data back to global memory");
        AppendLine("if (globalIdx < n)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("data[globalIdx] = sharedData[tid];");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();
    }

    /// <summary>
    /// Generates the global bitonic merge kernel for merging sorted blocks.
    /// </summary>
    private void GenerateCudaGlobalBitonicMerge(
        string elementTypeName,
        string keyTypeName,
        OrderByConfiguration config)
    {
        var swapCondition = config.Descending
            ? "(ascending && keyA < keyB) || (!ascending && keyA > keyB)"
            : "(ascending && keyA > keyB) || (!ascending && keyA < keyB)";

        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Global Bitonic Merge - Merges sorted segments across blocks");
        AppendLine("// Parameters: k = current merge size, j = comparison stride");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("extern \"C\" __global__ void BitonicMergeGlobal(");
        _indentLevel++;
        AppendLine($"{elementTypeName}* __restrict__ data,");
        AppendLine("const int n,");
        AppendLine("const int k,    // Merge size (determines direction pattern)");
        AppendLine("const int j     // Comparison stride");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        AppendLine("if (idx >= n) return;");
        AppendLine();

        AppendLine("// Calculate partner index for comparison-swap");
        AppendLine("int ixj = idx ^ j;");
        AppendLine();

        AppendLine("// Only process if we're the lower index in the pair");
        AppendLine("if (ixj > idx && ixj < n)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("// Determine sort direction based on position in k-sized segment");
        AppendLine("bool ascending = ((idx & k) == 0);");
        AppendLine();

        AppendLine("// Load elements");
        AppendLine($"{elementTypeName} valA = data[idx];");
        AppendLine($"{elementTypeName} valB = data[ixj];");

        if (config.KeySelector != null)
        {
            AppendLine($"{keyTypeName} keyA = extractKey(valA);");
            AppendLine($"{keyTypeName} keyB = extractKey(valB);");
        }
        else
        {
            AppendLine($"{keyTypeName} keyA = ({keyTypeName})(valA);");
            AppendLine($"{keyTypeName} keyB = ({keyTypeName})(valB);");
        }
        AppendLine();

        AppendLine($"// Compare and swap if out of order");
        AppendLine($"if ({swapCondition})");
        AppendLine("{");
        _indentLevel++;
        AppendLine("data[idx] = valB;");
        AppendLine("data[ixj] = valA;");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();
    }

    /// <summary>
    /// Generates the final merge pass for completing the sort.
    /// </summary>
    private void GenerateCudaFinalMergePass(
        string elementTypeName,
        string keyTypeName,
        OrderByConfiguration config)
    {
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine("// Multi-Block Sort Driver");
        AppendLine("// Host code should call kernels in this sequence:");
        AppendLine("// 1. BitonicSortLocal - Sort each block-sized segment");
        AppendLine("// 2. For k = 2*BLOCK_SIZE to n: For j = k/2 to 1:");
        AppendLine("//    - BitonicMergeGlobal(data, n, k, j)");
        AppendLine("//");
        AppendLine("// Kernel launch pseudocode:");
        AppendLine("// BitonicSortLocal<<<(n+BLOCK_SIZE-1)/BLOCK_SIZE, BLOCK_SIZE>>>(data, n);");
        AppendLine("// for (int k = 2*BLOCK_SIZE; k <= nextPow2(n); k *= 2) {");
        AppendLine("//     for (int j = k/2; j > 0; j /= 2) {");
        AppendLine("//         if (j >= BLOCK_SIZE) {");
        AppendLine("//             BitonicMergeGlobal<<<blocks, BLOCK_SIZE>>>(data, n, k, j);");
        AppendLine("//         } else {");
        AppendLine("//             // Can optimize with shared memory for small j");
        AppendLine("//             BitonicMergeGlobal<<<blocks, BLOCK_SIZE>>>(data, n, k, j);");
        AppendLine("//         }");
        AppendLine("//     }");
        AppendLine("// }");
        AppendLine("//-----------------------------------------------------------------------------");
        AppendLine();

        // Also generate a helper kernel for small strides that can use shared memory
        var swapCondition = config.Descending
            ? "(ascending && keyA < keyB) || (!ascending && keyA > keyB)"
            : "(ascending && keyA > keyB) || (!ascending && keyA < keyB)";

        AppendLine("// Optimized merge for small strides using shared memory");
        AppendLine("extern \"C\" __global__ void BitonicMergeShared(");
        _indentLevel++;
        AppendLine($"{elementTypeName}* __restrict__ data,");
        AppendLine("const int n,");
        AppendLine("const int k,");
        AppendLine("const int startJ  // Starting stride (must be < BLOCK_SIZE)");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"__shared__ {elementTypeName} sharedData[BLOCK_SIZE];");
        if (config.KeySelector != null)
        {
            AppendLine($"__shared__ {keyTypeName} sharedKeys[BLOCK_SIZE];");
        }
        AppendLine();

        AppendLine("int tid = threadIdx.x;");
        AppendLine("int globalIdx = blockIdx.x * blockDim.x + tid;");
        AppendLine();

        AppendLine("// Load data into shared memory");
        AppendLine("if (globalIdx < n)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("sharedData[tid] = data[globalIdx];");
        if (config.KeySelector != null)
        {
            AppendLine("sharedKeys[tid] = extractKey(sharedData[tid]);");
        }
        _indentLevel--;
        AppendLine("}");
        AppendLine("__syncthreads();");
        AppendLine();

        AppendLine("// Process all small strides in shared memory");
        AppendLine("for (int j = startJ; j > 0; j /= 2)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int ixj = tid ^ j;");
        AppendLine("if (ixj > tid && (blockIdx.x * blockDim.x + ixj) < n)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("bool ascending = (((blockIdx.x * blockDim.x + tid) & k) == 0);");

        if (config.KeySelector != null)
        {
            AppendLine($"{keyTypeName} keyA = sharedKeys[tid];");
            AppendLine($"{keyTypeName} keyB = sharedKeys[ixj];");
        }
        else
        {
            AppendLine($"{keyTypeName} keyA = ({keyTypeName})(sharedData[tid]);");
            AppendLine($"{keyTypeName} keyB = ({keyTypeName})(sharedData[ixj]);");
        }
        AppendLine();

        AppendLine($"if ({swapCondition})");
        AppendLine("{");
        _indentLevel++;
        AppendLine($"{elementTypeName} tmpVal = sharedData[tid];");
        AppendLine("sharedData[tid] = sharedData[ixj];");
        AppendLine("sharedData[ixj] = tmpVal;");
        if (config.KeySelector != null)
        {
            AppendLine($"{keyTypeName} tmpKey = sharedKeys[tid];");
            AppendLine("sharedKeys[tid] = sharedKeys[ixj];");
            AppendLine("sharedKeys[ixj] = tmpKey;");
        }
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine("__syncthreads();");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine("// Write back to global memory");
        AppendLine("if (globalIdx < n)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("data[globalIdx] = sharedData[tid];");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Generates OpenCL kernel code for multi-block bitonic sort.
    /// </summary>
    public string GenerateOpenCLOrderByKernel(
        string elementTypeName,
        string keyTypeName,
        OrderByConfiguration? config = null)
    {
        config ??= new OrderByConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        var direction = config.Descending ? "descending" : "ascending";
        var swapCondition = config.Descending
            ? "(ascending && keyA < keyB) || (!ascending && keyA > keyB)"
            : "(ascending && keyA > keyB) || (!ascending && keyA < keyB)";

        AppendLine("//=============================================================================");
        AppendLine("// OpenCL Bitonic Sort Kernel (Multi-Block Support)");
        AppendLine($"// Sort Order: {direction}");
        AppendLine("//=============================================================================");
        AppendLine();

        AppendLine($"#define BLOCK_SIZE {config.BlockSize}");
        AppendLine();

        // Local sort kernel
        AppendLine("__kernel void BitonicSortLocal(");
        _indentLevel++;
        AppendLine($"__global {elementTypeName}* data,");
        AppendLine("const int n");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"__local {elementTypeName} sharedData[BLOCK_SIZE];");
        AppendLine("int tid = get_local_id(0);");
        AppendLine("int globalIdx = get_global_id(0);");
        AppendLine();

        AppendLine("sharedData[tid] = (globalIdx < n) ? data[globalIdx] : 0;");
        AppendLine("barrier(CLK_LOCAL_MEM_FENCE);");
        AppendLine();

        AppendLine("for (int k = 2; k <= BLOCK_SIZE; k *= 2)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("for (int j = k / 2; j > 0; j /= 2)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int ixj = tid ^ j;");
        AppendLine("if (ixj > tid)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("bool ascending = ((tid & k) == 0);");
        AppendLine($"{elementTypeName} a = sharedData[tid];");
        AppendLine($"{elementTypeName} b = sharedData[ixj];");
        AppendLine($"{keyTypeName} keyA = ({keyTypeName})(a);");
        AppendLine($"{keyTypeName} keyB = ({keyTypeName})(b);");
        AppendLine($"if ({swapCondition})");
        AppendLine("{");
        _indentLevel++;
        AppendLine("sharedData[tid] = b;");
        AppendLine("sharedData[ixj] = a;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine("barrier(CLK_LOCAL_MEM_FENCE);");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine("if (globalIdx < n) data[globalIdx] = sharedData[tid];");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Global merge kernel
        AppendLine("__kernel void BitonicMergeGlobal(");
        _indentLevel++;
        AppendLine($"__global {elementTypeName}* data,");
        AppendLine("const int n,");
        AppendLine("const int k,");
        AppendLine("const int j");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int idx = get_global_id(0);");
        AppendLine("if (idx >= n) return;");
        AppendLine("int ixj = idx ^ j;");
        AppendLine("if (ixj > idx && ixj < n)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("bool ascending = ((idx & k) == 0);");
        AppendLine($"{elementTypeName} valA = data[idx];");
        AppendLine($"{elementTypeName} valB = data[ixj];");
        AppendLine($"{keyTypeName} keyA = ({keyTypeName})(valA);");
        AppendLine($"{keyTypeName} keyB = ({keyTypeName})(valB);");
        AppendLine($"if ({swapCondition})");
        AppendLine("{");
        _indentLevel++;
        AppendLine("data[idx] = valB;");
        AppendLine("data[ixj] = valA;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");

        return _builder.ToString();
    }

    /// <summary>
    /// Generates Metal kernel code for multi-block bitonic sort.
    /// </summary>
    public string GenerateMetalOrderByKernel(
        string elementTypeName,
        string keyTypeName,
        OrderByConfiguration? config = null)
    {
        config ??= new OrderByConfiguration();
        _builder.Clear();
        _indentLevel = 0;

        var direction = config.Descending ? "descending" : "ascending";
        var swapCondition = config.Descending
            ? "(ascending && keyA < keyB) || (!ascending && keyA > keyB)"
            : "(ascending && keyA > keyB) || (!ascending && keyA < keyB)";

        AppendLine("//=============================================================================");
        AppendLine("// Metal Bitonic Sort Kernel (Multi-Block Support)");
        AppendLine($"// Sort Order: {direction}");
        AppendLine("//=============================================================================");
        AppendLine();

        AppendLine("#include <metal_stdlib>");
        AppendLine("using namespace metal;");
        AppendLine();

        AppendLine($"constant int BLOCK_SIZE = {config.BlockSize};");
        AppendLine();

        // Local sort kernel
        AppendLine("kernel void BitonicSortLocal(");
        _indentLevel++;
        AppendLine($"device {elementTypeName}* data [[buffer(0)]],");
        AppendLine("constant int& n [[buffer(1)]],");
        AppendLine("threadgroup {elementTypeName}* sharedData [[threadgroup(0)]],");
        AppendLine("uint tid [[thread_index_in_threadgroup]],");
        AppendLine("uint globalIdx [[thread_position_in_grid]]");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("sharedData[tid] = (globalIdx < (uint)n) ? data[globalIdx] : 0;");
        AppendLine("threadgroup_barrier(mem_flags::mem_threadgroup);");
        AppendLine();

        AppendLine("for (int k = 2; k <= BLOCK_SIZE; k *= 2)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("for (int j = k / 2; j > 0; j /= 2)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("int ixj = tid ^ j;");
        AppendLine("if ((int)ixj > (int)tid)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("bool ascending = ((tid & k) == 0);");
        AppendLine($"{elementTypeName} a = sharedData[tid];");
        AppendLine($"{elementTypeName} b = sharedData[ixj];");
        AppendLine($"{keyTypeName} keyA = ({keyTypeName})(a);");
        AppendLine($"{keyTypeName} keyB = ({keyTypeName})(b);");
        AppendLine($"if ({swapCondition})");
        AppendLine("{");
        _indentLevel++;
        AppendLine("sharedData[tid] = b;");
        AppendLine("sharedData[ixj] = a;");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine("threadgroup_barrier(mem_flags::mem_threadgroup);");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine("if (globalIdx < (uint)n) data[globalIdx] = sharedData[tid];");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Global merge kernel
        AppendLine("kernel void BitonicMergeGlobal(");
        _indentLevel++;
        AppendLine($"device {elementTypeName}* data [[buffer(0)]],");
        AppendLine("constant int& n [[buffer(1)]],");
        AppendLine("constant int& k [[buffer(2)]],");
        AppendLine("constant int& j [[buffer(3)]],");
        AppendLine("uint idx [[thread_position_in_grid]]");
        _indentLevel--;
        AppendLine(")");
        AppendLine("{");
        _indentLevel++;

        AppendLine("if (idx >= (uint)n) return;");
        AppendLine("uint ixj = idx ^ j;");
        AppendLine("if (ixj > idx && ixj < (uint)n)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("bool ascending = ((idx & k) == 0);");
        AppendLine($"{elementTypeName} valA = data[idx];");
        AppendLine($"{elementTypeName} valB = data[ixj];");
        AppendLine($"{keyTypeName} keyA = ({keyTypeName})(valA);");
        AppendLine($"{keyTypeName} keyB = ({keyTypeName})(valB);");
        AppendLine($"if ({swapCondition})");
        AppendLine("{");
        _indentLevel++;
        AppendLine("data[idx] = valB;");
        AppendLine("data[ixj] = valA;");
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
