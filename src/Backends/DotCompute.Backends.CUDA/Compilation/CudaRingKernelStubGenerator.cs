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

            // 2. Add struct definitions at namespace scope
            AppendStructDefinitions(sourceBuilder);

            // 3. Generate kernel function signature
            AppendKernelSignature(sourceBuilder, kernel);

            // 4. Generate kernel body with message queue integration
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

        // Message handler note
        var handlerName = kernel.Method.Name.Replace("RingKernel", "", StringComparison.Ordinal).Replace("Kernel", "", StringComparison.Ordinal);
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Message handler for {handlerName}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"// Implementation provided by {handlerName}Serialization.cu (included by compiler)");

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
        _ = builder.AppendLine("#include <cuda/atomic>");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// Define standard integer types (NVRTC doesn't support <stdint.h>)");
        _ = builder.AppendLine("typedef signed char        int8_t;");
        _ = builder.AppendLine("typedef unsigned char      uint8_t;");
        _ = builder.AppendLine("typedef short              int16_t;");
        _ = builder.AppendLine("typedef unsigned short     uint16_t;");
        _ = builder.AppendLine("typedef int                int32_t;");
        _ = builder.AppendLine("typedef unsigned int       uint32_t;");
        _ = builder.AppendLine("typedef long long          int64_t;");
        _ = builder.AppendLine("typedef unsigned long long uint64_t;");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends struct definitions required by ring kernels at namespace scope.
    /// </summary>
    private static void AppendStructDefinitions(StringBuilder builder)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Ring Kernel Infrastructure Structures");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// RingKernelControlBlock struct - matches C# layout (64 bytes, 4-byte aligned)");
        _ = builder.AppendLine("struct RingKernelControlBlock {");
        _ = builder.AppendLine("    int is_active;              // Atomic flag: 1 = active, 0 = inactive");
        _ = builder.AppendLine("    int should_terminate;       // Atomic flag: 1 = terminate, 0 = continue");
        _ = builder.AppendLine("    int has_terminated;         // Atomic flag: 1 = terminated, 0 = running");
        _ = builder.AppendLine("    int errors_encountered;     // Atomic error counter");
        _ = builder.AppendLine("    long long messages_processed;    // Atomic message counter");
        _ = builder.AppendLine("    long long last_activity_ticks;   // Atomic timestamp");
        _ = builder.AppendLine("    long long input_queue_head_ptr;  // Device pointer to input queue head");
        _ = builder.AppendLine("    long long input_queue_tail_ptr;  // Device pointer to input queue tail");
        _ = builder.AppendLine("    long long output_queue_head_ptr; // Device pointer to output queue head");
        _ = builder.AppendLine("    long long output_queue_tail_ptr; // Device pointer to output queue tail");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// MessageQueue struct - lock-free message passing infrastructure");
        _ = builder.AppendLine("struct MessageQueue {");
        _ = builder.AppendLine("    unsigned char* buffer;            // Message buffer (serialized)");
        _ = builder.AppendLine("    unsigned int capacity;            // Queue capacity (power of 2)");
        _ = builder.AppendLine("    unsigned int message_size;        // Size of each message in bytes");
        _ = builder.AppendLine("    cuda::atomic<unsigned int>* head; // Dequeue position");
        _ = builder.AppendLine("    cuda::atomic<unsigned int>* tail; // Enqueue position");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    /// Try to enqueue a message");
        _ = builder.AppendLine("    __device__ bool try_enqueue(const unsigned char* message) {");
        _ = builder.AppendLine("        unsigned int current_tail = tail->load(cuda::memory_order_relaxed);");
        _ = builder.AppendLine("        unsigned int next_tail = (current_tail + 1) & (capacity - 1);");
        _ = builder.AppendLine("        unsigned int current_head = head->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        if (next_tail == current_head) {");
        _ = builder.AppendLine("            return false; // Queue full");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Try to claim slot");
        _ = builder.AppendLine("        if (tail->compare_exchange_strong(");
        _ = builder.AppendLine("                current_tail,");
        _ = builder.AppendLine("                next_tail,");
        _ = builder.AppendLine("                cuda::memory_order_release,");
        _ = builder.AppendLine("                cuda::memory_order_relaxed)) {");
        _ = builder.AppendLine("            // Copy message to buffer");
        _ = builder.AppendLine("            unsigned char* dest = buffer + (current_tail * message_size);");
        _ = builder.AppendLine("            for (unsigned int i = 0; i < message_size; ++i) {");
        _ = builder.AppendLine("                dest[i] = message[i];");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine("            return true;");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        return false; // CAS failed, retry");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    /// Try to dequeue a message");
        _ = builder.AppendLine("    __device__ bool try_dequeue(unsigned char* out_message) {");
        _ = builder.AppendLine("        unsigned int current_head = head->load(cuda::memory_order_relaxed);");
        _ = builder.AppendLine("        unsigned int current_tail = tail->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        if (current_head == current_tail) {");
        _ = builder.AppendLine("            return false; // Queue empty");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Try to claim slot");
        _ = builder.AppendLine("        unsigned int next_head = (current_head + 1) & (capacity - 1);");
        _ = builder.AppendLine("        if (head->compare_exchange_strong(");
        _ = builder.AppendLine("                current_head,");
        _ = builder.AppendLine("                next_head,");
        _ = builder.AppendLine("                cuda::memory_order_release,");
        _ = builder.AppendLine("                cuda::memory_order_relaxed)) {");
        _ = builder.AppendLine("            // Copy message from buffer");
        _ = builder.AppendLine("            const unsigned char* src = buffer + (current_head * message_size);");
        _ = builder.AppendLine("            for (unsigned int i = 0; i < message_size; ++i) {");
        _ = builder.AppendLine("                out_message[i] = src[i];");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine("            return true;");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        return false; // CAS failed, retry");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    /// Check if queue is empty");
        _ = builder.AppendLine("    __device__ bool is_empty() const {");
        _ = builder.AppendLine("        return head->load(cuda::memory_order_acquire) == tail->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("};");
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
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                unsigned char msg_buffer[{kernel.MaxInputMessageSizeBytes}];");
        _ = builder.AppendLine("                if (input_queue->try_dequeue(msg_buffer))");
        _ = builder.AppendLine("                {");
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine("                    // Message Processing with Handler Function");
        _ = builder.AppendLine("                    // ====================================================================");
        _ = builder.AppendLine();
        _ = builder.AppendLine("                    // Prepare output buffer for response");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                    unsigned char response_buffer[{kernel.MaxOutputMessageSizeBytes}];");
        _ = builder.AppendLine();
        _ = builder.AppendLine("                    // Call message handler to process the message");
        _ = builder.AppendLine("                    // The handler deserializes input, executes logic, and serializes output");

        // Generate handler function call based on kernel method name
        var handlerFunctionName = $"process_{ToSnakeCase(kernel.Method.Name.Replace("RingKernel", "", StringComparison.Ordinal).Replace("Kernel", "", StringComparison.Ordinal))}_message";
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                    bool success = {handlerFunctionName}(");
        _ = builder.AppendLine("                        msg_buffer,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                        {kernel.MaxInputMessageSizeBytes},");
        _ = builder.AppendLine("                        response_buffer,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                        {kernel.MaxOutputMessageSizeBytes});");
        _ = builder.AppendLine();
        _ = builder.AppendLine("                    // Enqueue response if processing succeeded");
        _ = builder.AppendLine("                    if (success && output_queue != nullptr)");
        _ = builder.AppendLine("                    {");
        _ = builder.AppendLine("                        if (output_queue->try_enqueue(response_buffer))");
        _ = builder.AppendLine("                        {");
        _ = builder.AppendLine("                            // Atomically increment messages processed counter");
        _ = builder.AppendLine("                            atomicAdd((unsigned long long*)&control_block->messages_processed, 1ULL);");
        _ = builder.AppendLine("                        }");
        _ = builder.AppendLine("                        else");
        _ = builder.AppendLine("                        {");
        _ = builder.AppendLine("                            // Output queue full - increment error counter");
        _ = builder.AppendLine("                            atomicAdd((unsigned*)&control_block->errors_encountered, 1);");
        _ = builder.AppendLine("                        }");
        _ = builder.AppendLine("                    }");
        _ = builder.AppendLine("                    else if (!success)");
        _ = builder.AppendLine("                    {");
        _ = builder.AppendLine("                        // Message processing failed - increment error counter");
        _ = builder.AppendLine("                        atomicAdd((unsigned*)&control_block->errors_encountered, 1);");
        _ = builder.AppendLine("                    }");
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

    /// <summary>
    /// Converts a PascalCase string to snake_case.
    /// </summary>
    /// <param name="str">The PascalCase string to convert.</param>
    /// <returns>The snake_case equivalent.</returns>
    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        var result = new StringBuilder(str.Length + 10);
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (char.IsUpper(c) && i > 0 && (i + 1 < str.Length && char.IsLower(str[i + 1]) || char.IsLower(str[i - 1])))
            {
                _ = result.Append('_');
                _ = result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                _ = result.Append(char.ToLowerInvariant(c));
            }
        }

        return result.ToString();
    }

    #endregion
}
