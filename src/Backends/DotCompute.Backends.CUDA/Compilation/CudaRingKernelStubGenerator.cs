// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using DotCompute.Backends.CUDA.Configuration;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Generates CUDA C++ kernel stubs from runtime-discovered Ring Kernel metadata.
/// </summary>
/// <remarks>
/// <para>
/// This generator creates CUDA C++ source code (.cu files) from Ring Kernel definitions
/// discovered at runtime via reflection. The generated code includes:
/// - CUDA kernel function declarations with proper type mapping
/// - Message queue parameter setup
/// - Ring buffer management code
/// - Cooperative kernel launch configuration
/// </para>
/// <para>
/// The generated stubs integrate with the Ring Kernel message processing pipeline,
/// supporting MemoryPack serialization and multi-GPU execution.
/// </para>
/// <para>
/// Generated code targets the compute capability detected by <see cref="CudaCapabilityManager"/>.
/// </para>
/// </remarks>
public sealed class CudaRingKernelStubGenerator
{
    private readonly ILogger<CudaRingKernelStubGenerator> _logger;

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, string, Exception?> _sLogGenerationStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(GenerateKernelStub)),
            "Starting CUDA stub generation for Ring Kernel '{KernelId}'");

    private static readonly Action<ILogger, string, int, Exception?> _sLogGenerationCompleted =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(2, nameof(GenerateKernelStub)),
            "Completed CUDA stub generation for Ring Kernel '{KernelId}' ({LineCount} lines)");

    private static readonly Action<ILogger, string, string, Exception> _sLogGenerationError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(3, nameof(GenerateKernelStub)),
            "Failed to generate CUDA stub for Ring Kernel '{KernelId}': {ErrorMessage}");

    private static readonly Action<ILogger, string, int, Exception?> _sLogParameterMapping =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(4, nameof(GenerateParameterDeclarations)),
            "Mapped {ParameterCount} parameters for kernel '{KernelId}'");

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaRingKernelStubGenerator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public CudaRingKernelStubGenerator(ILogger<CudaRingKernelStubGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Generates a complete CUDA C++ source file (.cu) for a Ring Kernel.
    /// </summary>
    /// <param name="kernel">The discovered Ring Kernel metadata.</param>
    /// <param name="includeHostLauncher">
    /// If true, generates host-side launch wrapper functions.
    /// Set to false for NVRTC device-only compilation (JIT mode).
    /// </param>
    /// <returns>The generated CUDA C++ source code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kernel"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when code generation fails.</exception>
    public string GenerateKernelStub(DiscoveredRingKernel kernel, bool includeHostLauncher = false)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        _sLogGenerationStarted(_logger, kernel.KernelId, null);

        try
        {
            var sourceBuilder = new StringBuilder(2048);

            // 1. Add header comments and includes
            AppendFileHeader(sourceBuilder, kernel);
            AppendIncludes(sourceBuilder, kernel);

            // 2. Generate kernel function signature
            AppendKernelSignature(sourceBuilder, kernel);

            // 3. Generate kernel body with message queue integration
            AppendKernelBody(sourceBuilder, kernel);

            // 4. Generate cooperative launch wrapper (only if requested for host compilation)
            if (includeHostLauncher)
            {
                AppendLaunchWrapper(sourceBuilder, kernel);
            }

            var generatedCode = sourceBuilder.ToString();
            var lineCount = generatedCode.Count(c => c == '\n') + 1;

            _sLogGenerationCompleted(_logger, kernel.KernelId, lineCount, null);

            return generatedCode;
        }
        catch (Exception ex)
        {
            _sLogGenerationError(_logger, kernel.KernelId, ex.Message, ex);
            throw new InvalidOperationException(
                $"Failed to generate CUDA stub for Ring Kernel '{kernel.KernelId}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Generates CUDA stubs for multiple Ring Kernels in a single compilation unit.
    /// </summary>
    /// <param name="kernels">The collection of discovered Ring Kernels.</param>
    /// <param name="compilationUnitName">The name of the compilation unit (e.g., "RingKernels").</param>
    /// <param name="includeHostLauncher">
    /// If true, generates host-side launch wrapper functions.
    /// Set to false for NVRTC device-only compilation (JIT mode).
    /// </param>
    /// <returns>The generated CUDA C++ source code containing all kernels.</returns>
    public string GenerateBatchKernelStubs(
        IEnumerable<DiscoveredRingKernel> kernels,
        string compilationUnitName = "RingKernels",
        bool includeHostLauncher = false)
    {
        ArgumentNullException.ThrowIfNull(kernels);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilationUnitName);

        var kernelList = kernels.ToList();
        if (kernelList.Count == 0)
        {
            return string.Empty;
        }

        var sourceBuilder = new StringBuilder(4096);

        // Add batch header
        AppendBatchHeader(sourceBuilder, compilationUnitName, kernelList.Count);

        // Add common includes (only once for all kernels)
        AppendCommonIncludes(sourceBuilder);

        // Generate each kernel
        foreach (var kernel in kernelList)
        {
            _ = sourceBuilder.AppendLine();
            _ = sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// ============================================================================");
            _ = sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// Ring Kernel: {kernel.KernelId}");
            _ = sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// ============================================================================");
            _ = sourceBuilder.AppendLine();

            // Generate kernel without header/includes (already added)
            AppendKernelSignature(sourceBuilder, kernel);
            AppendKernelBody(sourceBuilder, kernel);

            // Generate launcher only if requested
            if (includeHostLauncher)
            {
                AppendLaunchWrapper(sourceBuilder, kernel);
            }
        }

        return sourceBuilder.ToString();
    }

    #region Code Generation Methods

    /// <summary>
    /// Appends the file header with copyright and generation metadata.
    /// </summary>
    private static void AppendFileHeader(StringBuilder builder, DiscoveredRingKernel kernel)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Auto-Generated CUDA Ring Kernel Stub");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Kernel ID: {kernel.KernelId}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the batch compilation unit header.
    /// </summary>
    private static void AppendBatchHeader(StringBuilder builder, string unitName, int kernelCount)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Auto-Generated CUDA Ring Kernel Batch Compilation Unit");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Unit Name: {unitName}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Kernel Count: {kernelCount}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends required CUDA includes for a single kernel.
    /// </summary>
    private static void AppendIncludes(StringBuilder builder, DiscoveredRingKernel kernel)
    {
        AppendCommonIncludes(builder);

        // Add parameter-specific includes
        var requiresVectorTypes = kernel.Parameters.Any(p =>
            CudaTypeMapper.GetRequiredCudaHeader(p.ParameterType) == "vector_types.h");

        if (requiresVectorTypes)
        {
            _ = builder.AppendLine("#include <cuda/std/vector>");
        }

        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends common CUDA includes used by all kernels.
    /// </summary>
    private static void AppendCommonIncludes(StringBuilder builder)
    {
        _ = builder.AppendLine("#include <cuda_runtime.h>");
        _ = builder.AppendLine("#include <cooperative_groups.h>");
        _ = builder.AppendLine("#include <cooperative_groups/memcpy_async.h>");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Ring Kernel infrastructure");
        _ = builder.AppendLine("#include \"ring_buffer.cuh\"");
        _ = builder.AppendLine("#include \"message_queue.cuh\"");
        _ = builder.AppendLine("#include \"memorypack_deserializer.cuh\"");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the CUDA kernel function signature.
    /// </summary>
    private void AppendKernelSignature(StringBuilder builder, DiscoveredRingKernel kernel)
    {
        // Add XML-style documentation comment
        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Ring Kernel: {kernel.KernelId}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @details Capacity: {kernel.Capacity}, Mode: {kernel.Mode}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @details Input Queue: {kernel.InputQueueSize}, Output Queue: {kernel.OutputQueueSize}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @details Messaging: {kernel.MessagingStrategy}, Domain: {kernel.Domain}");
        _ = builder.AppendLine(" */");

        // Kernel attribute for cooperative launch
        _ = builder.AppendLine("extern \"C\" __global__ void __launch_bounds__(1024)");
        _ = builder.Append(CultureInfo.InvariantCulture, $"{kernel.KernelId}_kernel(");

        // Generate parameter list
        var parameters = GenerateParameterDeclarations(kernel);
        _ = builder.Append(string.Join(",\n    ", parameters));

        _ = builder.AppendLine(")");

        _sLogParameterMapping(_logger, kernel.KernelId, kernel.Parameters.Count, null);
    }

    /// <summary>
    /// Generates CUDA parameter declarations from kernel metadata.
    /// </summary>
    /// <remarks>
    /// Ring Kernels receive a single parameter: a pointer to the RingKernelControlBlock
    /// structure on the GPU. This control block contains all kernel state including
    /// activation flags, message queue pointers, and performance counters.
    /// </remarks>
    private static List<string> GenerateParameterDeclarations(DiscoveredRingKernel kernel)
    {
        // Ring Kernels use a single control block parameter
        // The control block contains activation flags and message queue pointers
        var declarations = new List<string>
        {
            "RingKernelControlBlock* control_block"
        };

        return declarations;
    }

    /// <summary>
    /// Appends the kernel body with message processing logic.
    /// </summary>
    private static void AppendKernelBody(StringBuilder builder, DiscoveredRingKernel kernel)
    {
        _ = builder.AppendLine("{");
        _ = builder.AppendLine("    // Define RingKernelControlBlock struct to match C# layout");
        _ = builder.AppendLine("    // Note: This struct must exactly match RingKernelControlBlock.cs (64 bytes, 4-byte aligned)");
        _ = builder.AppendLine("    struct RingKernelControlBlock {");
        _ = builder.AppendLine("        int is_active;              // Atomic flag: 1 = active, 0 = inactive");
        _ = builder.AppendLine("        int should_terminate;       // Atomic flag: 1 = terminate, 0 = continue");
        _ = builder.AppendLine("        int has_terminated;         // Atomic flag: 1 = terminated, 0 = running");
        _ = builder.AppendLine("        int errors_encountered;     // Atomic error counter");
        _ = builder.AppendLine("        long long messages_processed;    // Atomic message counter");
        _ = builder.AppendLine("        long long last_activity_ticks;   // Atomic timestamp");
        _ = builder.AppendLine("        long long input_queue_head_ptr;  // Device pointer to input queue head");
        _ = builder.AppendLine("        long long input_queue_tail_ptr;  // Device pointer to input queue tail");
        _ = builder.AppendLine("        long long output_queue_head_ptr; // Device pointer to output queue head");
        _ = builder.AppendLine("        long long output_queue_tail_ptr; // Device pointer to output queue tail");
        _ = builder.AppendLine("    };");
        _ = builder.AppendLine();

        _ = builder.AppendLine("    // Cooperative groups setup");
        _ = builder.AppendLine("    cooperative_groups::grid_group grid = cooperative_groups::this_grid();");
        _ = builder.AppendLine("    cooperative_groups::thread_block block = cooperative_groups::this_thread_block();");
        _ = builder.AppendLine();

        _ = builder.AppendLine("    // Thread/block identification");
        _ = builder.AppendLine("    const int tid = threadIdx.x;");
        _ = builder.AppendLine("    const int bid = blockIdx.x;");
        _ = builder.AppendLine("    const int gid = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = builder.AppendLine();

        _ = builder.AppendLine("    // Extract message queue pointers from control block");
        _ = builder.AppendLine("    // These pointers are set by the host during kernel activation");
        _ = builder.AppendLine("    MessageQueue* input_queue = reinterpret_cast<MessageQueue*>(control_block->input_queue_head_ptr);");
        _ = builder.AppendLine("    MessageQueue* output_queue = reinterpret_cast<MessageQueue*>(control_block->output_queue_head_ptr);");
        _ = builder.AppendLine();

        _ = builder.AppendLine("    // Persistent kernel main loop - runs until termination flag is set");
        _ = builder.AppendLine("    // The kernel starts inactive (is_active=0) and waits for activation from the host");
        _ = builder.AppendLine("    while (control_block->should_terminate == 0)");
        _ = builder.AppendLine("    {");
        _ = builder.AppendLine("        // Check if kernel is activated by the host");
        _ = builder.AppendLine("        if (control_block->is_active == 1)");
        _ = builder.AppendLine("        {");
        _ = builder.AppendLine("            // Poll input queue for incoming messages");
        _ = builder.AppendLine("            // Only thread 0 in each block handles message dequeuing to avoid races");
        _ = builder.AppendLine("            if (tid == 0 && input_queue != nullptr && !input_queue->is_empty())");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine("                // Dequeue message into byte buffer");
        _ = builder.AppendLine("                unsigned char msg_buffer[256]; // TODO: Use actual message size from kernel metadata");
        _ = builder.AppendLine("                if (input_queue->try_dequeue(msg_buffer))");
        _ = builder.AppendLine("                {");
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine("                    // Message Deserialization (Placeholder)");
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine("                    // TODO Phase 1 Integration: Call auto-generated MemoryPack deserializer");
        _ = builder.AppendLine("                    // Example: auto message = deserialize_<MessageType>(msg_buffer);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine("                    // User Kernel Logic Invocation (Placeholder)");
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                    // TODO: Call user kernel '{kernel.Method.Name}' with deserialized message");
        _ = builder.AppendLine("                    // Example: auto result = process_message(message);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine("                    // Response Serialization (Placeholder)");
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine("                    // TODO Phase 1 Integration: Call auto-generated MemoryPack serializer");
        _ = builder.AppendLine("                    // Example: serialize_<ResponseType>(result, response_buffer);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("                    // TODO: Enqueue serialized response to output queue");
        _ = builder.AppendLine("                    // unsigned char response_buffer[256];");
        _ = builder.AppendLine("                    // if (output_queue != nullptr) {");
        _ = builder.AppendLine("                    //     output_queue->try_enqueue(response_buffer);");
        _ = builder.AppendLine("                    // }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("                    // Atomically increment messages processed counter");
        _ = builder.AppendLine("                    atomicAdd((unsigned long long*)&control_block->messages_processed, 1ULL);");
        _ = builder.AppendLine("                }");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Cooperative grid-wide synchronization");
        _ = builder.AppendLine("        // Ensures all threads in the grid reach this point before continuing");
        _ = builder.AppendLine("        grid.sync();");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Mark kernel as terminated");
        _ = builder.AppendLine("    if (tid == 0 && bid == 0)");
        _ = builder.AppendLine("    {");
        _ = builder.AppendLine("        control_block->has_terminated = 1;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the cooperative launch wrapper function.
    /// </summary>
    private static void AppendLaunchWrapper(StringBuilder builder, DiscoveredRingKernel kernel)
    {
        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Host-side launch wrapper for {kernel.KernelId}");
        _ = builder.AppendLine(" * @details Configures cooperative launch parameters and launches the kernel");
        _ = builder.AppendLine(" */");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"extern \"C\" cudaError_t launch_{kernel.KernelId}(");
        _ = builder.AppendLine("    dim3 grid_dim,");
        _ = builder.AppendLine("    dim3 block_dim,");
        _ = builder.AppendLine("    cudaStream_t stream,");
        _ = builder.AppendLine("    void** kernel_params)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine("    // Launch cooperative kernel");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    return cudaLaunchCooperativeKernel(");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        (void*){kernel.KernelId}_kernel,");
        _ = builder.AppendLine("        grid_dim,");
        _ = builder.AppendLine("        block_dim,");
        _ = builder.AppendLine("        kernel_params,");
        _ = builder.AppendLine("        0, // shared memory");
        _ = builder.AppendLine("        stream);");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Validates that a kernel can be compiled to CUDA.
    /// </summary>
    /// <param name="kernel">The kernel to validate.</param>
    /// <returns>True if the kernel is valid for CUDA compilation; otherwise, false.</returns>
    public static bool ValidateKernelForCuda(DiscoveredRingKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        // Check that all parameter types are supported
        foreach (var param in kernel.Parameters)
        {
            if (!CudaTypeMapper.IsTypeSupported(param.ParameterType))
            {
                return false;
            }
        }

        // Ensure kernel has valid configuration
        if (kernel.Capacity <= 0 || kernel.InputQueueSize <= 0 || kernel.OutputQueueSize <= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Estimates the shared memory requirements for a Ring Kernel.
    /// </summary>
    /// <param name="kernel">The kernel to analyze.</param>
    /// <returns>The estimated shared memory size in bytes.</returns>
    public static int EstimateSharedMemorySize(DiscoveredRingKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        // Base ring buffer overhead
        const int ringBufferOverhead = 64; // Control structures

        // Message queue overhead
        var messageQueueSize = kernel.InputQueueSize * CudaTypeMapper.GetMemoryPackSerializedSize(typeof(object));

        // User parameter storage (if needed)
        var parameterSize = kernel.Parameters
            .Where(p => !p.IsBuffer) // Only scalar parameters in shared memory
            .Sum(p => CudaTypeMapper.GetCudaTypeSize(CudaTypeMapper.GetCudaType(p.ParameterType)));

        return ringBufferOverhead + messageQueueSize + parameterSize;
    }

    #endregion
}
