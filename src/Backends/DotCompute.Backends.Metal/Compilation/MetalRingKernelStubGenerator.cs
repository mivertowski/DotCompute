// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Compilation;

/// <summary>
/// Generates Metal Shading Language (MSL) kernel stubs from runtime-discovered Ring Kernel metadata.
/// </summary>
/// <remarks>
/// <para>
/// This generator creates Metal Shading Language source code (.metal files) from Ring Kernel definitions
/// discovered at runtime via reflection. The generated code includes:
/// - MSL kernel function declarations with proper type mapping
/// - Message queue parameter setup using Metal buffers
/// - Ring buffer management code for persistent kernels
/// - Thread group and SIMD group synchronization
/// </para>
/// <para>
/// The generated stubs integrate with the Ring Kernel message processing pipeline,
/// supporting MemoryPack serialization and Apple Silicon unified memory.
/// </para>
/// </remarks>
public sealed class MetalRingKernelStubGenerator
{
    private readonly ILogger<MetalRingKernelStubGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalRingKernelStubGenerator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MetalRingKernelStubGenerator(ILogger<MetalRingKernelStubGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Generates a complete MSL source file (.metal) for a Ring Kernel.
    /// </summary>
    /// <param name="kernel">The discovered Ring Kernel metadata.</param>
    /// <returns>The generated Metal Shading Language source code.</returns>
    public string GenerateKernelStub(DiscoveredMetalRingKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        _logger.LogInformation("Starting MSL stub generation for Ring Kernel '{KernelId}'", kernel.KernelId);

        try
        {
            var sourceBuilder = new StringBuilder(4096);

            // 1. Add header comments and includes
            AppendFileHeader(sourceBuilder, kernel);
            AppendIncludes(sourceBuilder);

            // 2. Add struct definitions
            AppendStructDefinitions(sourceBuilder);

            // 3. Add K2K infrastructure if needed
            if (kernel.UsesK2KMessaging)
            {
                AppendK2KMessagingInfrastructure(sourceBuilder);
            }

            // 4. Generate message handler function
            AppendHandlerFunction(sourceBuilder, kernel);

            // 5. Generate kernel function
            AppendKernelSignature(sourceBuilder, kernel);
            AppendKernelBody(sourceBuilder, kernel);

            var generatedCode = sourceBuilder.ToString();
            var lineCount = generatedCode.Count(c => c == '\n') + 1;

            _logger.LogInformation("Completed MSL stub generation for Ring Kernel '{KernelId}' ({LineCount} lines)",
                kernel.KernelId, lineCount);

            return generatedCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate MSL stub for Ring Kernel '{KernelId}'", kernel.KernelId);
            throw new InvalidOperationException(
                $"Failed to generate MSL stub for Ring Kernel '{kernel.KernelId}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates MSL stubs for multiple Ring Kernels in a single compilation unit.
    /// </summary>
    public static string GenerateBatchKernelStubs(
        IEnumerable<DiscoveredMetalRingKernel> kernels,
        string compilationUnitName = "RingKernels")
    {
        ArgumentNullException.ThrowIfNull(kernels);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilationUnitName);

        var kernelList = kernels.ToList();
        if (kernelList.Count == 0)
        {
            return string.Empty;
        }

        var sourceBuilder = new StringBuilder(8192);

        // Add batch header
        AppendBatchHeader(sourceBuilder, compilationUnitName, kernelList.Count);
        AppendIncludes(sourceBuilder);

        // Analyze all kernels for required infrastructure
        var needsK2K = kernelList.Any(k => k.UsesK2KMessaging);

        // Add struct definitions (shared by all)
        AppendStructDefinitions(sourceBuilder);

        // Add K2K infrastructure if needed
        if (needsK2K)
        {
            AppendK2KMessagingInfrastructure(sourceBuilder);
        }

        // Generate each kernel
        foreach (var kernel in kernelList)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("// ============================================================================");
            sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// Ring Kernel: {kernel.KernelId}");
            sourceBuilder.AppendLine("// ============================================================================");
            sourceBuilder.AppendLine();

            AppendHandlerFunction(sourceBuilder, kernel);
            AppendKernelSignature(sourceBuilder, kernel);
            AppendKernelBody(sourceBuilder, kernel);
        }

        return sourceBuilder.ToString();
    }

    #region Code Generation Methods

    private static void AppendFileHeader(StringBuilder builder, DiscoveredMetalRingKernel kernel)
    {
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine("// Auto-Generated Metal Ring Kernel Stub");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Kernel ID: {kernel.KernelId}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine();
    }

    private static void AppendBatchHeader(StringBuilder builder, string unitName, int kernelCount)
    {
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine("// Auto-Generated Metal Ring Kernel Batch Compilation Unit");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Unit Name: {unitName}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Kernel Count: {kernelCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine();
    }

    private static void AppendIncludes(StringBuilder builder)
    {
        builder.AppendLine("#include <metal_stdlib>");
        builder.AppendLine("#include <metal_atomic>");
        builder.AppendLine("using namespace metal;");
        builder.AppendLine();
    }

    private static void AppendStructDefinitions(StringBuilder builder)
    {
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine("// Ring Kernel Infrastructure Structures");
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine();

        // Control block matching C# layout
        builder.AppendLine("// RingKernelControlBlock struct - matches C# layout (64 bytes, 4-byte aligned)");
        builder.AppendLine("struct RingKernelControlBlock {");
        builder.AppendLine("    atomic_int is_active;              // 1 = active, 0 = inactive");
        builder.AppendLine("    atomic_int should_terminate;       // 1 = terminate, 0 = continue");
        builder.AppendLine("    atomic_int has_terminated;         // 1 = terminated, 0 = running");
        builder.AppendLine("    atomic_int errors_encountered;     // Error counter");
        builder.AppendLine("    atomic_long messages_processed;    // Message counter");
        builder.AppendLine("    atomic_long last_activity_ticks;   // Timestamp");
        builder.AppendLine("    long input_queue_head_ptr;         // Device pointer to input queue head");
        builder.AppendLine("    long input_queue_tail_ptr;         // Device pointer to input queue tail");
        builder.AppendLine("    long output_queue_head_ptr;        // Device pointer to output queue head");
        builder.AppendLine("    long output_queue_tail_ptr;        // Device pointer to output queue tail");
        builder.AppendLine("};");
        builder.AppendLine();

        // Message queue structure
        builder.AppendLine("// MessageQueue struct - lock-free message passing infrastructure");
        builder.AppendLine("struct MessageQueue {");
        builder.AppendLine("    device uchar* buffer;              // Message buffer (serialized)");
        builder.AppendLine("    uint capacity;                     // Queue capacity (power of 2)");
        builder.AppendLine("    uint message_size;                 // Size of each message in bytes");
        builder.AppendLine("    device atomic_uint* head;          // Dequeue position");
        builder.AppendLine("    device atomic_uint* tail;          // Enqueue position");
        builder.AppendLine("};");
        builder.AppendLine();

        // Message queue helper functions
        builder.AppendLine("// Try to enqueue a message to the queue");
        builder.AppendLine("inline bool msg_queue_try_enqueue(device MessageQueue* queue, thread const uchar* message) {");
        builder.AppendLine("    uint current_tail = atomic_load_explicit(queue->tail, memory_order_relaxed);");
        builder.AppendLine("    uint next_tail = (current_tail + 1) & (queue->capacity - 1);");
        builder.AppendLine("    uint current_head = atomic_load_explicit(queue->head, memory_order_acquire);");
        builder.AppendLine();
        builder.AppendLine("    if (next_tail == current_head) {");
        builder.AppendLine("        return false; // Queue full");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    // Try to claim slot");
        builder.AppendLine("    uint expected = current_tail;");
        builder.AppendLine("    if (atomic_compare_exchange_weak_explicit(queue->tail, &expected, next_tail,");
        builder.AppendLine("            memory_order_release, memory_order_relaxed)) {");
        builder.AppendLine("        // Copy message to buffer");
        builder.AppendLine("        device uchar* dest = queue->buffer + (current_tail * queue->message_size);");
        builder.AppendLine("        for (uint i = 0; i < queue->message_size; ++i) {");
        builder.AppendLine("            dest[i] = message[i];");
        builder.AppendLine("        }");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine("    return false;");
        builder.AppendLine("}");
        builder.AppendLine();

        builder.AppendLine("// Try to dequeue a message from the queue");
        builder.AppendLine("inline bool msg_queue_try_dequeue(device MessageQueue* queue, thread uchar* out_message) {");
        builder.AppendLine("    uint current_head = atomic_load_explicit(queue->head, memory_order_relaxed);");
        builder.AppendLine("    uint current_tail = atomic_load_explicit(queue->tail, memory_order_acquire);");
        builder.AppendLine();
        builder.AppendLine("    if (current_head == current_tail) {");
        builder.AppendLine("        return false; // Queue empty");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    // Try to claim slot");
        builder.AppendLine("    uint next_head = (current_head + 1) & (queue->capacity - 1);");
        builder.AppendLine("    uint expected = current_head;");
        builder.AppendLine("    if (atomic_compare_exchange_weak_explicit(queue->head, &expected, next_head,");
        builder.AppendLine("            memory_order_release, memory_order_relaxed)) {");
        builder.AppendLine("        // Copy message from buffer");
        builder.AppendLine("        device const uchar* src = queue->buffer + (current_head * queue->message_size);");
        builder.AppendLine("        for (uint i = 0; i < queue->message_size; ++i) {");
        builder.AppendLine("            out_message[i] = src[i];");
        builder.AppendLine("        }");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine("    return false;");
        builder.AppendLine("}");
        builder.AppendLine();

        builder.AppendLine("// Check if queue is empty");
        builder.AppendLine("inline bool msg_queue_is_empty(device MessageQueue* queue) {");
        builder.AppendLine("    uint head = atomic_load_explicit(queue->head, memory_order_acquire);");
        builder.AppendLine("    uint tail = atomic_load_explicit(queue->tail, memory_order_acquire);");
        builder.AppendLine("    return head == tail;");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendK2KMessagingInfrastructure(StringBuilder builder)
    {
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine("// K2K (Kernel-to-Kernel) Messaging Infrastructure");
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine();

        builder.AppendLine("// Global K2K message queue registry");
        builder.AppendLine("struct K2KMessageRegistry {");
        builder.AppendLine("    device MessageQueue* queues[64];   // Max 64 kernel-to-kernel queues");
        builder.AppendLine("    int queue_count;                   // Number of registered queues");
        builder.AppendLine("};");
        builder.AppendLine();
    }

    private static void AppendHandlerFunction(StringBuilder builder, DiscoveredMetalRingKernel kernel)
    {
        var methodName = kernel.Method.Name
            .Replace("RingKernel", "", StringComparison.Ordinal)
            .Replace("Kernel", "", StringComparison.Ordinal);
        var handlerFunctionName = $"process_{ToSnakeCase(methodName)}_message";

        builder.AppendLine("/**");
        builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Message handler for {kernel.KernelId}");
        if (!string.IsNullOrEmpty(kernel.InputMessageTypeName))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $" * @param msg_buffer Input message buffer (MemoryPack serialized {kernel.InputMessageTypeName})");
        }
        builder.AppendLine(" */");
        builder.AppendLine(CultureInfo.InvariantCulture, $"inline bool {handlerFunctionName}(");
        builder.AppendLine("    thread const uchar* msg_buffer,");
        builder.AppendLine("    int msg_size,");
        builder.AppendLine("    thread uchar* output_buffer,");
        builder.AppendLine("    thread int* output_size_ptr,");
        builder.AppendLine("    device RingKernelControlBlock* control_block)");
        builder.AppendLine("{");

        // Check if we have MemoryPack message types to deserialize/serialize
        var hasInputMessage = !string.IsNullOrEmpty(kernel.InputMessageTypeName);

        if (hasInputMessage)
        {
            // MemoryPack-integrated handler
            var inputStructName = ToSnakeCase(kernel.InputMessageTypeName!);

            builder.AppendLine("    // Validate input");
            builder.AppendLine("    if (msg_buffer == nullptr || msg_size <= 0) {");
            builder.AppendLine("        return false;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    // Deserialize input message using MemoryPack");
            builder.AppendLine(CultureInfo.InvariantCulture, $"    {inputStructName} input_msg;");
            builder.AppendLine(CultureInfo.InvariantCulture, $"    if (!deserialize_{inputStructName}(msg_buffer, msg_size, &input_msg)) {{");
            builder.AppendLine("        atomic_fetch_add_explicit(&control_block->errors_encountered, 1, memory_order_relaxed);");
            builder.AppendLine("        return false;");
            builder.AppendLine("    }");
            builder.AppendLine();

            if (kernel.HasInlineHandler && !string.IsNullOrEmpty(kernel.InlineHandlerMslCode))
            {
                builder.AppendLine("    // Inline handler (translated from C# method body)");
                builder.AppendLine("    // Note: Inline handler should work with deserialized input_msg struct");
                builder.AppendLine(kernel.InlineHandlerMslCode);
            }
            else
            {
                // Default: echo the input message as output
                builder.AppendLine("    // Default handler: echo input message to output");
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {inputStructName} output_msg = input_msg;");
                builder.AppendLine();
                builder.AppendLine("    // Serialize output message using MemoryPack");
                builder.AppendLine("    int serialized_size = 0;");
                builder.AppendLine(CultureInfo.InvariantCulture, $"    if (!serialize_{inputStructName}(&output_msg, output_buffer, {kernel.MaxOutputMessageSizeBytes}, &serialized_size)) {{");
                builder.AppendLine("        atomic_fetch_add_explicit(&control_block->errors_encountered, 1, memory_order_relaxed);");
                builder.AppendLine("        return false;");
                builder.AppendLine("    }");
                builder.AppendLine();
                builder.AppendLine("    if (output_size_ptr != nullptr) {");
                builder.AppendLine("        *output_size_ptr = serialized_size;");
                builder.AppendLine("    }");
                builder.AppendLine("    return true;");
            }
        }
        else
        {
            // Fallback: raw byte echo handler (no MemoryPack types)
            builder.AppendLine("    // Default handler stub - echo/pass-through (no MemoryPack types defined)");
            builder.AppendLine("    if (msg_buffer == nullptr || msg_size <= 0) {");
            builder.AppendLine("        return false;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine(CultureInfo.InvariantCulture, $"    int output_size = min(msg_size, {kernel.MaxOutputMessageSizeBytes});");
            builder.AppendLine("    for (int i = 0; i < output_size; i++) {");
            builder.AppendLine("        output_buffer[i] = msg_buffer[i];");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (output_size_ptr != nullptr) {");
            builder.AppendLine("        *output_size_ptr = output_size;");
            builder.AppendLine("    }");
            builder.AppendLine("    return true;");
        }

        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendKernelSignature(StringBuilder builder, DiscoveredMetalRingKernel kernel)
    {
        builder.AppendLine("/**");
        builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Ring Kernel: {kernel.KernelId}");
        builder.AppendLine(CultureInfo.InvariantCulture, $" * @details Capacity: {kernel.Capacity}, Mode: {kernel.Mode}");
        builder.AppendLine(CultureInfo.InvariantCulture, $" * @details Input Queue: {kernel.InputQueueSize}, Output Queue: {kernel.OutputQueueSize}");
        builder.AppendLine(" */");
        builder.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernel.KernelId}_kernel(");
        builder.AppendLine("    device RingKernelControlBlock* control_block [[buffer(0)]],");
        builder.AppendLine("    device MessageQueue* input_queue [[buffer(1)]],");
        builder.AppendLine("    device MessageQueue* output_queue [[buffer(2)]],");
        builder.AppendLine("    uint tid [[thread_position_in_threadgroup]],");
        builder.AppendLine("    uint tgid [[threadgroup_position_in_grid]],");
        builder.AppendLine("    uint gid [[thread_position_in_grid]])");
    }

    private static void AppendKernelBody(StringBuilder builder, DiscoveredMetalRingKernel kernel)
    {
        builder.AppendLine("{");

        // GPU Timestamp capture if enabled
        if (kernel.EnableTimestamps)
        {
            builder.AppendLine("    // Note: Metal doesn't have clock64() equivalent");
            builder.AppendLine("    // Timestamps are handled by MTLCounterSet on host side");
        }

        // Processing mode setup
        var processingMode = kernel.ProcessingMode;
        if (processingMode == Abstractions.RingKernels.RingProcessingMode.Batch)
        {
            builder.AppendLine("    // Batch processing mode");
            builder.AppendLine("    const int BATCH_SIZE = 16;");
            builder.AppendLine();
        }

        // Main kernel loop
        builder.AppendLine("    // Persistent kernel main loop");
        builder.AppendLine("    while (atomic_load_explicit(&control_block->should_terminate, memory_order_acquire) == 0)");
        builder.AppendLine("    {");
        builder.AppendLine("        // Check if kernel is activated");
        builder.AppendLine("        if (atomic_load_explicit(&control_block->is_active, memory_order_acquire) == 1)");
        builder.AppendLine("        {");

        // Message processing
        AppendMessageProcessingBlock(builder, kernel, "            ");

        // Memory fence based on consistency model
        AppendMemoryFence(builder, kernel.MemoryConsistency);

        // Barrier based on scope
        if (kernel.UseBarriers)
        {
            AppendBarrier(builder, kernel.BarrierScope);
        }
        else
        {
            builder.AppendLine("            // Threadgroup barrier for synchronization");
            builder.AppendLine("            threadgroup_barrier(mem_flags::mem_device);");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();

        // Mark kernel as terminated
        builder.AppendLine("    // Mark kernel as terminated");
        builder.AppendLine("    if (tid == 0 && tgid == 0)");
        builder.AppendLine("    {");
        builder.AppendLine("        atomic_store_explicit(&control_block->has_terminated, 1, memory_order_release);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendMessageProcessingBlock(
        StringBuilder builder,
        DiscoveredMetalRingKernel kernel,
        string indent)
    {
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}// Poll input queue for incoming messages");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}if (tid == 0 && input_queue != nullptr && !msg_queue_is_empty(input_queue))");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}{{");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    // Dequeue message into thread-local buffer");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    uchar msg_buffer[{kernel.MaxInputMessageSizeBytes}];");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    if (msg_queue_try_dequeue(input_queue, msg_buffer))");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    {{");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Prepare output buffer");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        uchar response_buffer[{kernel.MaxOutputMessageSizeBytes}];");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        int output_size = 0;");
        builder.AppendLine();

        // Call handler function
        var handlerFunctionName = $"process_{ToSnakeCase(kernel.Method.Name.Replace("RingKernel", "", StringComparison.Ordinal).Replace("Kernel", "", StringComparison.Ordinal))}_message";
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Call message handler");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        bool success = {handlerFunctionName}(");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            msg_buffer,");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {kernel.MaxInputMessageSizeBytes},");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            response_buffer,");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            &output_size,");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            control_block);");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Enqueue response if successful");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        if (success && output_queue != nullptr)");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        {{");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            if (msg_queue_try_enqueue(output_queue, response_buffer))");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {{");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                atomic_fetch_add_explicit(&control_block->messages_processed, 1LL, memory_order_relaxed);");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            }}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            else");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {{");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                atomic_fetch_add_explicit(&control_block->errors_encountered, 1, memory_order_relaxed);");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            }}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        }}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        else if (!success)");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        {{");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            atomic_fetch_add_explicit(&control_block->errors_encountered, 1, memory_order_relaxed);");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        }}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    }}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}}}");
    }

    private static void AppendMemoryFence(
        StringBuilder builder,
        Abstractions.Memory.MemoryConsistencyModel consistency)
    {
        builder.AppendLine();

        switch (consistency)
        {
            case Abstractions.Memory.MemoryConsistencyModel.Sequential:
                builder.AppendLine("            // Sequential consistency: full memory barrier");
                builder.AppendLine("            threadgroup_barrier(mem_flags::mem_device | mem_flags::mem_threadgroup);");
                break;

            case Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire:
                builder.AppendLine("            // Release-acquire semantics");
                builder.AppendLine("            threadgroup_barrier(mem_flags::mem_device);");
                break;

            default: // Relaxed
                builder.AppendLine("            // Relaxed memory ordering - no explicit fence");
                break;
        }
    }

    private static void AppendBarrier(StringBuilder builder, Abstractions.Barriers.BarrierScope scope)
    {
        builder.AppendLine();

        switch (scope)
        {
            case Abstractions.Barriers.BarrierScope.Warp:
                builder.AppendLine("            // SIMD group barrier (32 threads on Apple Silicon)");
                builder.AppendLine("            simdgroup_barrier(mem_flags::mem_device);");
                break;

            case Abstractions.Barriers.BarrierScope.ThreadBlock:
            case Abstractions.Barriers.BarrierScope.Grid:
            default:
                builder.AppendLine("            // Threadgroup barrier");
                builder.AppendLine("            threadgroup_barrier(mem_flags::mem_device);");
                break;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Validates that a kernel can be compiled to MSL.
    /// </summary>
    public static bool ValidateKernelForMetal(DiscoveredMetalRingKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        // Check parameter types for Metal compatibility
        foreach (var param in kernel.Parameters)
        {
            if (!IsMetalTypeSupported(param.ParameterType))
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
    /// Checks if a .NET type can be mapped to MSL.
    /// </summary>
    private static bool IsMetalTypeSupported(Type type)
    {
        // Primitive types
        if (type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(bool))
        {
            return true;
        }

        // Buffer types
        if (type.IsArray)
        {
            return IsMetalTypeSupported(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (genericTypeDef.Name is "Span`1" or "ReadOnlySpan`1" or "Memory`1" or "ReadOnlyMemory`1")
            {
                return IsMetalTypeSupported(type.GetGenericArguments()[0]);
            }
        }

        // RingKernelContext is handled specially
        if (type.Name == "RingKernelContext")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts PascalCase to snake_case.
    /// </summary>
    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        var result = new StringBuilder(str.Length + 10);
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (char.IsUpper(c) && i > 0 && (i + 1 < str.Length && char.IsLower(str[i + 1]) || char.IsLower(str[i - 1])))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(char.ToLowerInvariant(c));
            }
        }

        return result.ToString();
    }

    #endregion
}
