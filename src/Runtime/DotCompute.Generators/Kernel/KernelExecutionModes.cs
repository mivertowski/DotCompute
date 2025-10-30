// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using DotCompute.Generators.Models.Kernel;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Defines various kernel execution modes supported by the generator.
/// </summary>
public enum KernelExecutionMode
{
    /// <summary>
    /// Standard kernel execution with global memory access.
    /// </summary>
    Standard,

    /// <summary>
    /// Persistent kernel that stays resident and processes multiple work items.
    /// </summary>
    Persistent,

    /// <summary>
    /// Dynamic parallelism enabled kernel that can launch child kernels.
    /// </summary>
    Dynamic,

    /// <summary>
    /// Cooperative groups enabled for advanced synchronization.
    /// </summary>
    Cooperative,

    /// <summary>
    /// Graph-based execution for optimized kernel chains.
    /// </summary>
    Graph,

    /// <summary>
    /// Tensor core optimized for matrix operations.
    /// </summary>
    TensorCore,

    /// <summary>
    /// Warp-specialized execution with divergence optimization.
    /// </summary>
    WarpSpecialized,


    /// <summary>
    /// Stream-based concurrent execution.
    /// </summary>
    Streaming
}

/// <summary>
/// Handles generation of kernel code for different execution modes.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via reflection or dependency injection")]
internal sealed class KernelExecutionModeHandler(
    KernelMethodInfo kernelInfo,

    KernelExecutionMode mode,
    Dictionary<string, object>? configuration = null)
{
    private readonly KernelMethodInfo _kernelInfo = kernelInfo ?? throw new ArgumentNullException(nameof(kernelInfo));
    private readonly KernelExecutionMode _mode = mode;
    private readonly Dictionary<string, object> _configuration = configuration ?? [];


    /// <summary>
    /// Handles register spilling configuration for kernels with high register pressure.
    /// </summary>
    private void HandleRegisterSpilling(StringBuilder sb)
    {
        // Check if kernel has MaxRegisters attribute or configuration
        var maxRegisters = _configuration.TryGetValue("maxRegisters", out var maxRegsObj) ? (int)maxRegsObj : 0;

        // Also check for explicit register pressure hints

        var hasHighRegisterPressure = _configuration.ContainsKey("highRegisterPressure") &&
                                      (bool)_configuration["highRegisterPressure"];


        if (maxRegisters > 0 || hasHighRegisterPressure)
        {
            _ = sb.AppendLine("// Register spilling configuration");


            if (maxRegisters > 0)
            {
                _ = sb.AppendLine($"// Limiting to {maxRegisters} registers per thread");
                _ = sb.AppendLine($"#define MAX_REGISTERS {maxRegisters}");

                // Calculate optimal occupancy based on register limit

                var registersPerSM = 65536; // For compute capability 7.0+
                var maxThreadsPerSM = registersPerSM / maxRegisters;
                var maxBlocksPerSM = Math.Min(maxThreadsPerSM / 256, 32);


                _ = sb.AppendLine($"// Estimated max occupancy: {maxBlocksPerSM} blocks per SM");
            }

            // Enable shared memory register spilling for Turing and newer (CC 7.5+)

            _ = sb.AppendLine("#if __CUDA_ARCH__ >= 750");
            _ = sb.AppendLine("  // Enable shared memory register spilling on Turing and newer");
            _ = sb.AppendLine("  // This allows the compiler to spill registers to shared memory");
            _ = sb.AppendLine("  // instead of local memory, improving performance");
            _ = sb.AppendLine("  #pragma unroll 1  // Disable unrolling to reduce register pressure");
            _ = sb.AppendLine("  #define USE_SHARED_MEM_SPILLING 1");
            _ = sb.AppendLine("#else");
            _ = sb.AppendLine("  // Older architectures spill to local memory");
            _ = sb.AppendLine("  #define USE_SHARED_MEM_SPILLING 0");
            _ = sb.AppendLine("#endif");
            _ = sb.AppendLine();

            // Add shared memory buffer for manual register spilling if needed

            if (hasHighRegisterPressure)
            {
                _ = sb.AppendLine("// Shared memory buffer for manual register spilling");
                _ = sb.AppendLine("__shared__ float spill_buffer[256 * 8]; // 8 floats per thread");
                _ = sb.AppendLine("#define SPILL_TO_SHARED(tid, idx, val) spill_buffer[(tid) * 8 + (idx)] = (val)");
                _ = sb.AppendLine("#define LOAD_FROM_SHARED(tid, idx) spill_buffer[(tid) * 8 + (idx)]");
                _ = sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Generates the kernel header with mode-specific attributes and parameters.
    /// </summary>
    public string GenerateKernelHeader()
    {
        var sb = new StringBuilder();

        // Handle register spilling configuration

        HandleRegisterSpilling(sb);

        // Add mode-specific attributes

        switch (_mode)
        {
            case KernelExecutionMode.Cooperative:
                _ = sb.AppendLine("#include <cooperative_groups.h>");
                _ = sb.AppendLine("namespace cg = cooperative_groups;");
                _ = sb.AppendLine();
                break;


            case KernelExecutionMode.TensorCore:
                _ = sb.AppendLine("#include <mma.h>");
                _ = sb.AppendLine("using namespace nvcuda;");
                _ = sb.AppendLine();
                break;


            case KernelExecutionMode.Dynamic:
                _ = sb.AppendLine("#define CUDA_DYNAMIC_PARALLELISM");
                _ = sb.AppendLine();
                break;
        }

        // Generate kernel signature with mode-specific modifiers

        _ = sb.Append("extern \"C\" ");

        // Apply launch_bounds for register limiting or mode-specific requirements

        var maxRegisters = _configuration.TryGetValue("maxRegisters", out var maxRegsObj2) ? (int)maxRegsObj2 : 0;
        var needsLaunchBounds = _mode == KernelExecutionMode.Cooperative ||

                                _mode == KernelExecutionMode.Persistent ||
                                maxRegisters > 0;


        if (needsLaunchBounds)
        {
            _ = sb.Append("__global__ void ");

            // Add launch_bounds attribute

            var maxThreadsPerBlock = _configuration.TryGetValue("maxThreadsPerBlock", out var maxThreadsObj) ? (int)maxThreadsObj : 256;
            var minBlocksPerSM = _configuration.TryGetValue("minBlocksPerMultiprocessor", out var minBlocksObj) ? (int)minBlocksObj : 2;

            // Adjust for register pressure

            if (maxRegisters is > 0 and < 64)
            {
                // Lower occupancy for kernels with high register pressure
                minBlocksPerSM = Math.Min(minBlocksPerSM, 65536 / (maxRegisters * maxThreadsPerBlock));
            }


            _ = sb.Append($"__launch_bounds__({maxThreadsPerBlock}, {minBlocksPerSM}) ");
        }
        else
        {
            _ = sb.Append("__global__ void ");
        }


        _ = sb.Append($"{_kernelInfo.Name}_cuda_kernel");


        return sb.ToString();
    }

    /// <summary>
    /// Generates the kernel body with mode-specific optimizations.
    /// </summary>
    public string GenerateKernelBody(string baseBody)
    {
        _ = new StringBuilder();


        switch (_mode)
        {
            case KernelExecutionMode.Standard:
                return GenerateStandardKernelBody(baseBody);


            case KernelExecutionMode.Persistent:
                return GeneratePersistentKernelBody(baseBody);


            case KernelExecutionMode.Cooperative:
                return GenerateCooperativeKernelBody(baseBody);


            case KernelExecutionMode.Dynamic:
                return GenerateDynamicKernelBody(baseBody);


            case KernelExecutionMode.Graph:
                return GenerateGraphKernelBody(baseBody);


            case KernelExecutionMode.TensorCore:
                return GenerateTensorCoreKernelBody(baseBody);


            case KernelExecutionMode.WarpSpecialized:
                return GenerateWarpSpecializedKernelBody(baseBody);


            case KernelExecutionMode.Streaming:
                return GenerateStreamingKernelBody(baseBody);


            default:
                return baseBody;
        }
    }

    private static string GenerateStandardKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Standard kernel execution");
        _ = sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    const uint32_t stride = blockDim.x * gridDim.x;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    for (uint32_t i = idx; i < length; i += stride) {");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GeneratePersistentKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Persistent kernel with work queue");
        _ = sb.AppendLine("    __shared__ volatile int work_queue_index;");
        _ = sb.AppendLine("    __shared__ volatile bool should_exit;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    if (threadIdx.x == 0) {");
        _ = sb.AppendLine("        work_queue_index = 0;");
        _ = sb.AppendLine("        should_exit = false;");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("    __syncthreads();");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    const uint32_t tid = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    const uint32_t total_threads = blockDim.x * gridDim.x;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    while (!should_exit) {");
        _ = sb.AppendLine("        // Get next work item");
        _ = sb.AppendLine("        int work_idx;");
        _ = sb.AppendLine("        if (threadIdx.x == 0) {");
        _ = sb.AppendLine("            work_idx = atomicAdd((int*)&global_work_counter, 1);");
        _ = sb.AppendLine("            if (work_idx >= length) {");
        _ = sb.AppendLine("                should_exit = true;");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("            work_queue_index = work_idx;");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        __syncthreads();");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        if (!should_exit) {");
        _ = sb.AppendLine("            work_idx = work_queue_index;");
        _ = sb.AppendLine("            if (work_idx < length) {");
        _ = sb.AppendLine("                const uint32_t i = work_idx;");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("        __syncthreads();");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateCooperativeKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Cooperative groups kernel");
        _ = sb.AppendLine("    cg::grid_group grid = cg::this_grid();");
        _ = sb.AppendLine("    cg::thread_block block = cg::this_thread_block();");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    const uint32_t tid = grid.thread_rank();");
        _ = sb.AppendLine("    const uint32_t grid_size = grid.size();");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    for (uint32_t i = tid; i < length; i += grid_size) {");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Grid-wide synchronization");
        _ = sb.AppendLine("    grid.sync();");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateDynamicKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Dynamic parallelism enabled kernel");
        _ = sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    if (idx < length) {");
        _ = sb.AppendLine("        const uint32_t i = idx;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        // Check if we need to spawn child kernel");
        _ = sb.AppendLine("        const uint32_t work_size = length / gridDim.x;");
        _ = sb.AppendLine("        if (work_size > DYNAMIC_THRESHOLD && threadIdx.x == 0) {");
        _ = sb.AppendLine("            // Launch child kernel for subdivision");
        _ = sb.AppendLine("            dim3 child_grid((work_size + 255) / 256);");
        _ = sb.AppendLine("            dim3 child_block(256);");
        _ = sb.AppendLine($"            {_kernelInfo.Name}_cuda_kernel_child<<<child_grid, child_block>>>");
        _ = sb.AppendLine("                (/* pass subdivided parameters */);");
        _ = sb.AppendLine("            cudaDeviceSynchronize();");
        _ = sb.AppendLine("        } else {");
        _ = sb.AppendLine("            // Execute work directly");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateGraphKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Graph-optimized kernel for stream capture");
        _ = sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    const uint32_t stride = blockDim.x * gridDim.x;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Optimized for graph instantiation");
        _ = sb.AppendLine("    #pragma unroll 4");
        _ = sb.AppendLine("    for (uint32_t i = idx; i < length; i += stride) {");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateTensorCoreKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Tensor Core optimized kernel");
        _ = sb.AppendLine("    const uint32_t warpId = threadIdx.x / 32;");
        _ = sb.AppendLine("    const uint32_t laneId = threadIdx.x % 32;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Declare fragments for tensor cores");
        _ = sb.AppendLine("    wmma::fragment<wmma::matrix_a, 16, 16, 16, half, wmma::row_major> a_frag;");
        _ = sb.AppendLine("    wmma::fragment<wmma::matrix_b, 16, 16, 16, half, wmma::col_major> b_frag;");
        _ = sb.AppendLine("    wmma::fragment<wmma::accumulator, 16, 16, 16, float> c_frag;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Initialize accumulator");
        _ = sb.AppendLine("    wmma::fill_fragment(c_frag, 0.0f);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Tensor core computation loop");
        _ = sb.AppendLine("    for (uint32_t i = warpId * 16; i < length; i += gridDim.x * 16) {");
        _ = sb.AppendLine("        // Load matrices into fragments");
        _ = sb.AppendLine("        // wmma::load_matrix_sync(a_frag, ...);");
        _ = sb.AppendLine("        // wmma::load_matrix_sync(b_frag, ...);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        // Perform matrix multiply-accumulate");
        _ = sb.AppendLine("        // wmma::mma_sync(c_frag, a_frag, b_frag, c_frag);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        // Custom kernel logic");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Store result");
        _ = sb.AppendLine("    // wmma::store_matrix_sync(..., c_frag, ...);");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateWarpSpecializedKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Warp-specialized kernel with divergence optimization");
        _ = sb.AppendLine("    const uint32_t warpId = threadIdx.x / 32;");
        _ = sb.AppendLine("    const uint32_t laneId = threadIdx.x % 32;");
        _ = sb.AppendLine("    const uint32_t warpsPerBlock = blockDim.x / 32;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Warp-level primitives");
        _ = sb.AppendLine("    const uint32_t warp_idx = blockIdx.x * warpsPerBlock + warpId;");
        _ = sb.AppendLine("    const uint32_t total_warps = gridDim.x * warpsPerBlock;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    for (uint32_t i = warp_idx * 32 + laneId; i < length; i += total_warps * 32) {");
        _ = sb.AppendLine("        // Warp-level operations");
        _ = sb.AppendLine("        uint32_t mask = __ballot_sync(0xffffffff, i < length);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        if (i < length) {");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        // Warp-level reduction example");
        _ = sb.AppendLine("        // float val = ...;");
        _ = sb.AppendLine("        // for (int offset = 16; offset > 0; offset /= 2) {");
        _ = sb.AppendLine("        //     val += __shfl_down_sync(mask, val, offset);");
        _ = sb.AppendLine("        // }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateStreamingKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    // Stream-optimized kernel for concurrent execution");
        _ = sb.AppendLine("    const uint32_t stream_id = blockIdx.y;  // Y dimension for stream ID");
        _ = sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = sb.AppendLine("    const uint32_t stride = blockDim.x * gridDim.x;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    // Stream-specific work partitioning");
        _ = sb.AppendLine("    const uint32_t stream_chunk_size = (length + gridDim.y - 1) / gridDim.y;");
        _ = sb.AppendLine("    const uint32_t stream_start = stream_id * stream_chunk_size;");
        _ = sb.AppendLine("    const uint32_t stream_end = min(stream_start + stream_chunk_size, length);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("    for (uint32_t i = stream_start + idx; i < stream_end; i += stride) {");
        _ = sb.Append(baseBody);
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates the host-side launch configuration for the kernel.
    /// </summary>
    public string GenerateLaunchConfiguration()
    {
        var sb = new StringBuilder();


        _ = sb.AppendLine("// Launch configuration for " + _mode + " mode");


        switch (_mode)
        {
            case KernelExecutionMode.Persistent:
                _ = sb.AppendLine("const int persistent_blocks = device_props.multiProcessorCount * 2;");
                _ = sb.AppendLine("const int block_size = 256;");
                _ = sb.AppendLine("dim3 grid(persistent_blocks);");
                _ = sb.AppendLine("dim3 block(block_size);");
                break;


            case KernelExecutionMode.Cooperative:
                _ = sb.AppendLine("int block_size = 256;");
                _ = sb.AppendLine("int grid_size = 0;");
                _ = sb.AppendLine("cudaOccupancyMaxActiveBlocksPerMultiprocessor(&grid_size,");
                _ = sb.AppendLine($"    {_kernelInfo.Name}_cuda_kernel, block_size, 0);");
                _ = sb.AppendLine("grid_size *= device_props.multiProcessorCount;");
                _ = sb.AppendLine("dim3 grid(grid_size);");
                _ = sb.AppendLine("dim3 block(block_size);");
                break;


            case KernelExecutionMode.TensorCore:
                _ = sb.AppendLine("// Tensor cores require multiples of 16");
                _ = sb.AppendLine("const int warp_size = 32;");
                _ = sb.AppendLine("const int warps_per_block = 8;");
                _ = sb.AppendLine("const int block_size = warp_size * warps_per_block;");
                _ = sb.AppendLine("const int grid_size = (length + block_size * 16 - 1) / (block_size * 16);");
                _ = sb.AppendLine("dim3 grid(grid_size);");
                _ = sb.AppendLine("dim3 block(block_size);");
                break;


            case KernelExecutionMode.Streaming:
                _ = sb.AppendLine("const int num_streams = 4;  // Configurable");
                _ = sb.AppendLine("const int block_size = 256;");
                _ = sb.AppendLine("const int blocks_per_stream = (length + block_size - 1) / block_size / num_streams;");
                _ = sb.AppendLine("dim3 grid(blocks_per_stream, num_streams);");
                _ = sb.AppendLine("dim3 block(block_size);");
                break;


            default:
                _ = sb.AppendLine("// Standard launch configuration");
                _ = sb.AppendLine("int block_size = 256;");
                _ = sb.AppendLine("int grid_size = (length + block_size - 1) / block_size;");
                _ = sb.AppendLine("dim3 grid(grid_size);");
                _ = sb.AppendLine("dim3 block(block_size);");
                break;
        }


        return sb.ToString();
    }

    /// <summary>
    /// Determines the best execution mode based on kernel characteristics.
    /// </summary>
    public static KernelExecutionMode DetermineOptimalMode(KernelMethodInfo kernelInfo)
    {
        // Analyze kernel attributes and characteristics

        // Check for tensor operations

        if (kernelInfo.Parameters.Any(p => p.Type.Contains("Matrix") || p.Type.Contains("Tensor")))
        {
            return KernelExecutionMode.TensorCore;
        }

        // Check for reduction patterns

        if (kernelInfo.Name.Contains("Reduce") || kernelInfo.Name.Contains("Sum"))
        {
            return KernelExecutionMode.WarpSpecialized;
        }

        // Check for iterative patterns

        if (kernelInfo.Name.Contains("Iterate") || kernelInfo.Name.Contains("Loop"))
        {
            return KernelExecutionMode.Persistent;
        }

        // Check for graph patterns

        if (kernelInfo.Name.Contains("Chain") || kernelInfo.Name.Contains("Pipeline"))
        {
            return KernelExecutionMode.Graph;
        }

        // Default to standard mode

        return KernelExecutionMode.Standard;
    }
}
