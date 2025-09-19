// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
internal sealed class KernelExecutionModeHandler
{
    private readonly KernelMethodInfo _kernelInfo;
    private readonly KernelExecutionMode _mode;
    private readonly Dictionary<string, object> _configuration;

    public KernelExecutionModeHandler(
        KernelMethodInfo kernelInfo,

        KernelExecutionMode mode,
        Dictionary<string, object>? configuration = null)
    {
        _kernelInfo = kernelInfo ?? throw new ArgumentNullException(nameof(kernelInfo));
        _mode = mode;
        _configuration = configuration ?? [];
    }


    /// <summary>
    /// Handles register spilling configuration for kernels with high register pressure.
    /// </summary>
    private void HandleRegisterSpilling(StringBuilder sb)
    {
        // Check if kernel has MaxRegisters attribute or configuration
        var maxRegisters = _configuration.ContainsKey("maxRegisters") ? (int)_configuration["maxRegisters"] : 0;

        // Also check for explicit register pressure hints

        var hasHighRegisterPressure = _configuration.ContainsKey("highRegisterPressure") &&
                                      (bool)_configuration["highRegisterPressure"];


        if (maxRegisters > 0 || hasHighRegisterPressure)
        {
            sb.AppendLine("// Register spilling configuration");


            if (maxRegisters > 0)
            {
                sb.AppendLine($"// Limiting to {maxRegisters} registers per thread");
                sb.AppendLine($"#define MAX_REGISTERS {maxRegisters}");

                // Calculate optimal occupancy based on register limit

                var registersPerSM = 65536; // For compute capability 7.0+
                var maxThreadsPerSM = registersPerSM / maxRegisters;
                var maxBlocksPerSM = Math.Min(maxThreadsPerSM / 256, 32);


                sb.AppendLine($"// Estimated max occupancy: {maxBlocksPerSM} blocks per SM");
            }

            // Enable shared memory register spilling for Turing and newer (CC 7.5+)

            sb.AppendLine("#if __CUDA_ARCH__ >= 750");
            sb.AppendLine("  // Enable shared memory register spilling on Turing and newer");
            sb.AppendLine("  // This allows the compiler to spill registers to shared memory");
            sb.AppendLine("  // instead of local memory, improving performance");
            sb.AppendLine("  #pragma unroll 1  // Disable unrolling to reduce register pressure");
            sb.AppendLine("  #define USE_SHARED_MEM_SPILLING 1");
            sb.AppendLine("#else");
            sb.AppendLine("  // Older architectures spill to local memory");
            sb.AppendLine("  #define USE_SHARED_MEM_SPILLING 0");
            sb.AppendLine("#endif");
            sb.AppendLine();

            // Add shared memory buffer for manual register spilling if needed

            if (hasHighRegisterPressure)
            {
                sb.AppendLine("// Shared memory buffer for manual register spilling");
                sb.AppendLine("__shared__ float spill_buffer[256 * 8]; // 8 floats per thread");
                sb.AppendLine("#define SPILL_TO_SHARED(tid, idx, val) spill_buffer[(tid) * 8 + (idx)] = (val)");
                sb.AppendLine("#define LOAD_FROM_SHARED(tid, idx) spill_buffer[(tid) * 8 + (idx)]");
                sb.AppendLine();
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
                sb.AppendLine("#include <cooperative_groups.h>");
                sb.AppendLine("namespace cg = cooperative_groups;");
                sb.AppendLine();
                break;


            case KernelExecutionMode.TensorCore:
                sb.AppendLine("#include <mma.h>");
                sb.AppendLine("using namespace nvcuda;");
                sb.AppendLine();
                break;


            case KernelExecutionMode.Dynamic:
                sb.AppendLine("#define CUDA_DYNAMIC_PARALLELISM");
                sb.AppendLine();
                break;
        }

        // Generate kernel signature with mode-specific modifiers

        sb.Append("extern \"C\" ");

        // Apply launch_bounds for register limiting or mode-specific requirements

        var maxRegisters = _configuration.ContainsKey("maxRegisters") ? (int)_configuration["maxRegisters"] : 0;
        var needsLaunchBounds = _mode == KernelExecutionMode.Cooperative ||

                                _mode == KernelExecutionMode.Persistent ||
                                maxRegisters > 0;


        if (needsLaunchBounds)
        {
            sb.Append("__global__ void ");

            // Add launch_bounds attribute

            var maxThreadsPerBlock = _configuration.ContainsKey("maxThreadsPerBlock") ? (int)_configuration["maxThreadsPerBlock"] : 256;
            var minBlocksPerSM = _configuration.ContainsKey("minBlocksPerMultiprocessor") ? (int)_configuration["minBlocksPerMultiprocessor"] : 2;

            // Adjust for register pressure

            if (maxRegisters > 0 && maxRegisters < 64)
            {
                // Lower occupancy for kernels with high register pressure
                minBlocksPerSM = Math.Min(minBlocksPerSM, 65536 / (maxRegisters * maxThreadsPerBlock));
            }


            sb.Append($"__launch_bounds__({maxThreadsPerBlock}, {minBlocksPerSM}) ");
        }
        else
        {
            sb.Append("__global__ void ");
        }


        sb.Append($"{_kernelInfo.Name}_cuda_kernel");


        return sb.ToString();
    }

    /// <summary>
    /// Generates the kernel body with mode-specific optimizations.
    /// </summary>
    public string GenerateKernelBody(string baseBody)
    {
        var sb = new StringBuilder();


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
        sb.AppendLine("{");
        sb.AppendLine("    // Standard kernel execution");
        sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        sb.AppendLine("    const uint32_t stride = blockDim.x * gridDim.x;");
        sb.AppendLine();
        sb.AppendLine("    for (uint32_t i = idx; i < length; i += stride) {");
        sb.Append(baseBody);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GeneratePersistentKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    // Persistent kernel with work queue");
        sb.AppendLine("    __shared__ volatile int work_queue_index;");
        sb.AppendLine("    __shared__ volatile bool should_exit;");
        sb.AppendLine();
        sb.AppendLine("    if (threadIdx.x == 0) {");
        sb.AppendLine("        work_queue_index = 0;");
        sb.AppendLine("        should_exit = false;");
        sb.AppendLine("    }");
        sb.AppendLine("    __syncthreads();");
        sb.AppendLine();
        sb.AppendLine("    const uint32_t tid = blockIdx.x * blockDim.x + threadIdx.x;");
        sb.AppendLine("    const uint32_t total_threads = blockDim.x * gridDim.x;");
        sb.AppendLine();
        sb.AppendLine("    while (!should_exit) {");
        sb.AppendLine("        // Get next work item");
        sb.AppendLine("        int work_idx;");
        sb.AppendLine("        if (threadIdx.x == 0) {");
        sb.AppendLine("            work_idx = atomicAdd((int*)&global_work_counter, 1);");
        sb.AppendLine("            if (work_idx >= length) {");
        sb.AppendLine("                should_exit = true;");
        sb.AppendLine("            }");
        sb.AppendLine("            work_queue_index = work_idx;");
        sb.AppendLine("        }");
        sb.AppendLine("        __syncthreads();");
        sb.AppendLine();
        sb.AppendLine("        if (!should_exit) {");
        sb.AppendLine("            work_idx = work_queue_index;");
        sb.AppendLine("            if (work_idx < length) {");
        sb.AppendLine("                const uint32_t i = work_idx;");
        sb.Append(baseBody);
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        __syncthreads();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateCooperativeKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    // Cooperative groups kernel");
        sb.AppendLine("    cg::grid_group grid = cg::this_grid();");
        sb.AppendLine("    cg::thread_block block = cg::this_thread_block();");
        sb.AppendLine();
        sb.AppendLine("    const uint32_t tid = grid.thread_rank();");
        sb.AppendLine("    const uint32_t grid_size = grid.size();");
        sb.AppendLine();
        sb.AppendLine("    for (uint32_t i = tid; i < length; i += grid_size) {");
        sb.Append(baseBody);
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Grid-wide synchronization");
        sb.AppendLine("    grid.sync();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateDynamicKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    // Dynamic parallelism enabled kernel");
        sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        sb.AppendLine();
        sb.AppendLine("    if (idx < length) {");
        sb.AppendLine("        const uint32_t i = idx;");
        sb.AppendLine();
        sb.AppendLine("        // Check if we need to spawn child kernel");
        sb.AppendLine("        const uint32_t work_size = length / gridDim.x;");
        sb.AppendLine("        if (work_size > DYNAMIC_THRESHOLD && threadIdx.x == 0) {");
        sb.AppendLine("            // Launch child kernel for subdivision");
        sb.AppendLine("            dim3 child_grid((work_size + 255) / 256);");
        sb.AppendLine("            dim3 child_block(256);");
        sb.AppendLine($"            {_kernelInfo.Name}_cuda_kernel_child<<<child_grid, child_block>>>");
        sb.AppendLine("                (/* pass subdivided parameters */);");
        sb.AppendLine("            cudaDeviceSynchronize();");
        sb.AppendLine("        } else {");
        sb.AppendLine("            // Execute work directly");
        sb.Append(baseBody);
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateGraphKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    // Graph-optimized kernel for stream capture");
        sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        sb.AppendLine("    const uint32_t stride = blockDim.x * gridDim.x;");
        sb.AppendLine();
        sb.AppendLine("    // Optimized for graph instantiation");
        sb.AppendLine("    #pragma unroll 4");
        sb.AppendLine("    for (uint32_t i = idx; i < length; i += stride) {");
        sb.Append(baseBody);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateTensorCoreKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    // Tensor Core optimized kernel");
        sb.AppendLine("    const uint32_t warpId = threadIdx.x / 32;");
        sb.AppendLine("    const uint32_t laneId = threadIdx.x % 32;");
        sb.AppendLine();
        sb.AppendLine("    // Declare fragments for tensor cores");
        sb.AppendLine("    wmma::fragment<wmma::matrix_a, 16, 16, 16, half, wmma::row_major> a_frag;");
        sb.AppendLine("    wmma::fragment<wmma::matrix_b, 16, 16, 16, half, wmma::col_major> b_frag;");
        sb.AppendLine("    wmma::fragment<wmma::accumulator, 16, 16, 16, float> c_frag;");
        sb.AppendLine();
        sb.AppendLine("    // Initialize accumulator");
        sb.AppendLine("    wmma::fill_fragment(c_frag, 0.0f);");
        sb.AppendLine();
        sb.AppendLine("    // Tensor core computation loop");
        sb.AppendLine("    for (uint32_t i = warpId * 16; i < length; i += gridDim.x * 16) {");
        sb.AppendLine("        // Load matrices into fragments");
        sb.AppendLine("        // wmma::load_matrix_sync(a_frag, ...);");
        sb.AppendLine("        // wmma::load_matrix_sync(b_frag, ...);");
        sb.AppendLine();
        sb.AppendLine("        // Perform matrix multiply-accumulate");
        sb.AppendLine("        // wmma::mma_sync(c_frag, a_frag, b_frag, c_frag);");
        sb.AppendLine();
        sb.AppendLine("        // Custom kernel logic");
        sb.Append(baseBody);
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Store result");
        sb.AppendLine("    // wmma::store_matrix_sync(..., c_frag, ...);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateWarpSpecializedKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    // Warp-specialized kernel with divergence optimization");
        sb.AppendLine("    const uint32_t warpId = threadIdx.x / 32;");
        sb.AppendLine("    const uint32_t laneId = threadIdx.x % 32;");
        sb.AppendLine("    const uint32_t warpsPerBlock = blockDim.x / 32;");
        sb.AppendLine();
        sb.AppendLine("    // Warp-level primitives");
        sb.AppendLine("    const uint32_t warp_idx = blockIdx.x * warpsPerBlock + warpId;");
        sb.AppendLine("    const uint32_t total_warps = gridDim.x * warpsPerBlock;");
        sb.AppendLine();
        sb.AppendLine("    for (uint32_t i = warp_idx * 32 + laneId; i < length; i += total_warps * 32) {");
        sb.AppendLine("        // Warp-level operations");
        sb.AppendLine("        uint32_t mask = __ballot_sync(0xffffffff, i < length);");
        sb.AppendLine();
        sb.AppendLine("        if (i < length) {");
        sb.Append(baseBody);
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Warp-level reduction example");
        sb.AppendLine("        // float val = ...;");
        sb.AppendLine("        // for (int offset = 16; offset > 0; offset /= 2) {");
        sb.AppendLine("        //     val += __shfl_down_sync(mask, val, offset);");
        sb.AppendLine("        // }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateStreamingKernelBody(string baseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    // Stream-optimized kernel for concurrent execution");
        sb.AppendLine("    const uint32_t stream_id = blockIdx.y;  // Y dimension for stream ID");
        sb.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        sb.AppendLine("    const uint32_t stride = blockDim.x * gridDim.x;");
        sb.AppendLine();
        sb.AppendLine("    // Stream-specific work partitioning");
        sb.AppendLine("    const uint32_t stream_chunk_size = (length + gridDim.y - 1) / gridDim.y;");
        sb.AppendLine("    const uint32_t stream_start = stream_id * stream_chunk_size;");
        sb.AppendLine("    const uint32_t stream_end = min(stream_start + stream_chunk_size, length);");
        sb.AppendLine();
        sb.AppendLine("    for (uint32_t i = stream_start + idx; i < stream_end; i += stride) {");
        sb.Append(baseBody);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates the host-side launch configuration for the kernel.
    /// </summary>
    public string GenerateLaunchConfiguration()
    {
        var sb = new StringBuilder();


        sb.AppendLine("// Launch configuration for " + _mode + " mode");


        switch (_mode)
        {
            case KernelExecutionMode.Persistent:
                sb.AppendLine("const int persistent_blocks = device_props.multiProcessorCount * 2;");
                sb.AppendLine("const int block_size = 256;");
                sb.AppendLine("dim3 grid(persistent_blocks);");
                sb.AppendLine("dim3 block(block_size);");
                break;


            case KernelExecutionMode.Cooperative:
                sb.AppendLine("int block_size = 256;");
                sb.AppendLine("int grid_size = 0;");
                sb.AppendLine("cudaOccupancyMaxActiveBlocksPerMultiprocessor(&grid_size,");
                sb.AppendLine($"    {_kernelInfo.Name}_cuda_kernel, block_size, 0);");
                sb.AppendLine("grid_size *= device_props.multiProcessorCount;");
                sb.AppendLine("dim3 grid(grid_size);");
                sb.AppendLine("dim3 block(block_size);");
                break;


            case KernelExecutionMode.TensorCore:
                sb.AppendLine("// Tensor cores require multiples of 16");
                sb.AppendLine("const int warp_size = 32;");
                sb.AppendLine("const int warps_per_block = 8;");
                sb.AppendLine("const int block_size = warp_size * warps_per_block;");
                sb.AppendLine("const int grid_size = (length + block_size * 16 - 1) / (block_size * 16);");
                sb.AppendLine("dim3 grid(grid_size);");
                sb.AppendLine("dim3 block(block_size);");
                break;


            case KernelExecutionMode.Streaming:
                sb.AppendLine("const int num_streams = 4;  // Configurable");
                sb.AppendLine("const int block_size = 256;");
                sb.AppendLine("const int blocks_per_stream = (length + block_size - 1) / block_size / num_streams;");
                sb.AppendLine("dim3 grid(blocks_per_stream, num_streams);");
                sb.AppendLine("dim3 block(block_size);");
                break;


            default:
                sb.AppendLine("// Standard launch configuration");
                sb.AppendLine("int block_size = 256;");
                sb.AppendLine("int grid_size = (length + block_size - 1) / block_size;");
                sb.AppendLine("dim3 grid(grid_size);");
                sb.AppendLine("dim3 block(block_size);");
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