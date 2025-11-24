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

            // 2.5. Add K2K infrastructure if kernel uses K2K messaging
            if (kernel.UsesK2KMessaging)
            {
                AppendK2KMessagingInfrastructure(sourceBuilder);
            }

            // 2.6. Generate message handler function (device function called from kernel)
            AppendHandlerFunction(sourceBuilder, kernel);

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

        // Analyze all kernels to determine required infrastructure
        var needsK2K = kernelList.Any(k => k.UsesK2KMessaging);
        var needsHlc = kernelList.Any(k => k.EnableTimestamps || k.UsesTemporalApis);
        var needsWarpReduction = kernelList.Any(k => k.UsesWarpPrimitives);

        // Add struct definitions (needed by all)
        AppendStructDefinitions(sourceBuilder);

        // Add K2K infrastructure if any kernel uses kernel-to-kernel messaging
        if (needsK2K)
        {
            AppendK2KMessagingInfrastructure(sourceBuilder);
        }

        // Add HLC infrastructure if any kernel uses temporal APIs
        if (needsHlc)
        {
            AppendHlcInfrastructure(sourceBuilder);
        }

        // Add warp reduction helpers if needed
        if (needsWarpReduction)
        {
            AppendWarpReductionHelpers(sourceBuilder);
        }

        // Generate each kernel
        foreach (var kernel in kernelList)
        {
            _ = sourceBuilder.AppendLine();
            _ = sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// ============================================================================");
            _ = sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// Ring Kernel: {kernel.KernelId}");
            _ = sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// ============================================================================");
            _ = sourceBuilder.AppendLine();

            // Generate handler function first (called from kernel)
            AppendHandlerFunction(sourceBuilder, kernel);

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
    /// Appends K2K (Kernel-to-Kernel) messaging infrastructure.
    /// </summary>
    private static void AppendK2KMessagingInfrastructure(StringBuilder builder)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// K2K (Kernel-to-Kernel) Messaging Infrastructure");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();

        // K2K Message Queue Registry
        _ = builder.AppendLine("// Global K2K message queue registry for actor-style messaging");
        _ = builder.AppendLine("struct K2KMessageRegistry {");
        _ = builder.AppendLine("    MessageQueue* queues[64];      // Max 64 kernel-to-kernel queues");
        _ = builder.AppendLine("    const char* kernel_ids[64];    // Kernel ID lookup table");
        _ = builder.AppendLine("    int queue_count;               // Number of registered queues");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();

        // K2K Send function
        _ = builder.AppendLine("// Send message to another kernel by ID");
        _ = builder.AppendLine("__device__ bool k2k_send(");
        _ = builder.AppendLine("    K2KMessageRegistry* registry,");
        _ = builder.AppendLine("    const char* target_kernel_id,");
        _ = builder.AppendLine("    const unsigned char* message,");
        _ = builder.AppendLine("    unsigned int message_size)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine("    // Find target kernel's queue");
        _ = builder.AppendLine("    for (int i = 0; i < registry->queue_count; i++) {");
        _ = builder.AppendLine("        // Simple string comparison (device-side)");
        _ = builder.AppendLine("        const char* id = registry->kernel_ids[i];");
        _ = builder.AppendLine("        const char* target = target_kernel_id;");
        _ = builder.AppendLine("        bool match = true;");
        _ = builder.AppendLine("        while (*id && *target) {");
        _ = builder.AppendLine("            if (*id++ != *target++) { match = false; break; }");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("        if (match && *id == *target) {");
        _ = builder.AppendLine("            return registry->queues[i]->try_enqueue(message);");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return false; // Target not found");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();

        // K2K Receive function
        _ = builder.AppendLine("// Try to receive message from another kernel by ID");
        _ = builder.AppendLine("__device__ bool k2k_try_receive(");
        _ = builder.AppendLine("    K2KMessageRegistry* registry,");
        _ = builder.AppendLine("    const char* source_kernel_id,");
        _ = builder.AppendLine("    unsigned char* out_message,");
        _ = builder.AppendLine("    unsigned int message_size)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine("    // Find source kernel's queue");
        _ = builder.AppendLine("    for (int i = 0; i < registry->queue_count; i++) {");
        _ = builder.AppendLine("        const char* id = registry->kernel_ids[i];");
        _ = builder.AppendLine("        const char* source = source_kernel_id;");
        _ = builder.AppendLine("        bool match = true;");
        _ = builder.AppendLine("        while (*id && *source) {");
        _ = builder.AppendLine("            if (*id++ != *source++) { match = false; break; }");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("        if (match && *id == *source) {");
        _ = builder.AppendLine("            return registry->queues[i]->try_dequeue(out_message);");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return false; // Source not found");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();

        // K2K Pending count function
        _ = builder.AppendLine("// Get pending message count from another kernel");
        _ = builder.AppendLine("__device__ int k2k_pending_count(");
        _ = builder.AppendLine("    K2KMessageRegistry* registry,");
        _ = builder.AppendLine("    const char* kernel_id)");
        _ = builder.AppendLine("{");
        _ = builder.AppendLine("    for (int i = 0; i < registry->queue_count; i++) {");
        _ = builder.AppendLine("        const char* id = registry->kernel_ids[i];");
        _ = builder.AppendLine("        const char* target = kernel_id;");
        _ = builder.AppendLine("        bool match = true;");
        _ = builder.AppendLine("        while (*id && *target) {");
        _ = builder.AppendLine("            if (*id++ != *target++) { match = false; break; }");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("        if (match && *id == *target) {");
        _ = builder.AppendLine("            MessageQueue* q = registry->queues[i];");
        _ = builder.AppendLine("            unsigned int head = q->head->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine("            unsigned int tail = q->tail->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine("            return (tail >= head) ? (tail - head) : (q->capacity - head + tail);");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return 0;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends K2K queue declarations for a specific kernel.
    /// Generates k2k_send_queue for kernels that publish, and k2k_receive_channels for kernels that subscribe.
    /// </summary>
    private static void AppendK2KQueueDeclarations(StringBuilder builder, DiscoveredRingKernel kernel)
    {
        // Generate K2K send queue declarations for PublishesToKernels
        if (kernel.PublishesToKernels.Count > 0)
        {
            _ = builder.AppendLine("    // ===========================================================");
            _ = builder.AppendLine("    // K2K Send Queue Declarations (kernel-to-kernel messaging)");
            _ = builder.AppendLine("    // ===========================================================");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // This kernel publishes to: {string.Join(", ", kernel.PublishesToKernels)}");
            _ = builder.AppendLine();
            _ = builder.AppendLine("    // K2K send queue array - indices correspond to PublishesToKernels array");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    MessageQueue* k2k_send_queue[{kernel.PublishesToKernels.Count}];");
            _ = builder.AppendLine();

            // Generate comments showing which index corresponds to which kernel
            for (int i = 0; i < kernel.PublishesToKernels.Count; i++)
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // k2k_send_queue[{i}] -> \"{kernel.PublishesToKernels[i]}\"");
            }
            _ = builder.AppendLine();
        }

        // Generate K2K receive channel declarations for SubscribesToKernels
        if (kernel.SubscribesToKernels.Count > 0)
        {
            _ = builder.AppendLine("    // ===========================================================");
            _ = builder.AppendLine("    // K2K Receive Channel Declarations (kernel-to-kernel messaging)");
            _ = builder.AppendLine("    // ===========================================================");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // This kernel subscribes to: {string.Join(", ", kernel.SubscribesToKernels)}");
            _ = builder.AppendLine();
            _ = builder.AppendLine("    // K2K receive channels array - indices correspond to SubscribesToKernels array");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    MessageQueue* k2k_receive_channels[{kernel.SubscribesToKernels.Count}];");
            _ = builder.AppendLine();

            // Generate comments showing which index corresponds to which kernel
            for (int i = 0; i < kernel.SubscribesToKernels.Count; i++)
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    // k2k_receive_channels[{i}] <- \"{kernel.SubscribesToKernels[i]}\"");
            }
            _ = builder.AppendLine();
        }

        // Add K2K registry pointer (set by host via extended control block)
        if (kernel.PublishesToKernels.Count > 0 || kernel.SubscribesToKernels.Count > 0)
        {
            _ = builder.AppendLine("    // K2K registry pointer - initialized by host runtime");
            _ = builder.AppendLine("    // The registry maps kernel IDs to their message queues");
            _ = builder.AppendLine("    K2KMessageRegistry* k2k_registry = nullptr;");
            _ = builder.AppendLine("    // TODO: Extract k2k_registry from extended control block");
            _ = builder.AppendLine();
        }
    }

    /// <summary>
    /// Appends Hybrid Logical Clock (HLC) infrastructure for temporal consistency.
    /// </summary>
    private static void AppendHlcInfrastructure(StringBuilder builder)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Hybrid Logical Clock (HLC) Infrastructure");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// HLC timestamp structure (128-bit)");
        _ = builder.AppendLine("struct HlcTimestamp {");
        _ = builder.AppendLine("    long long physical;    // Physical time (clock64())");
        _ = builder.AppendLine("    int logical;           // Logical counter");
        _ = builder.AppendLine("    int node_id;           // Node/GPU identifier");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// Global HLC state (per block)");
        _ = builder.AppendLine("__shared__ HlcTimestamp hlc_state;");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// Get current HLC timestamp");
        _ = builder.AppendLine("__device__ HlcTimestamp hlc_now() {");
        _ = builder.AppendLine("    HlcTimestamp ts;");
        _ = builder.AppendLine("    ts.physical = clock64();");
        _ = builder.AppendLine("    ts.logical = hlc_state.logical;");
        _ = builder.AppendLine("    ts.node_id = blockIdx.x;");
        _ = builder.AppendLine("    return ts;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// Advance HLC clock");
        _ = builder.AppendLine("__device__ HlcTimestamp hlc_tick() {");
        _ = builder.AppendLine("    long long now = clock64();");
        _ = builder.AppendLine("    if (now > hlc_state.physical) {");
        _ = builder.AppendLine("        hlc_state.physical = now;");
        _ = builder.AppendLine("        hlc_state.logical = 0;");
        _ = builder.AppendLine("    } else {");
        _ = builder.AppendLine("        hlc_state.logical++;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return hlc_now();");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// Update HLC from received message timestamp");
        _ = builder.AppendLine("__device__ HlcTimestamp hlc_update(HlcTimestamp received) {");
        _ = builder.AppendLine("    long long now = clock64();");
        _ = builder.AppendLine("    if (now > hlc_state.physical && now > received.physical) {");
        _ = builder.AppendLine("        hlc_state.physical = now;");
        _ = builder.AppendLine("        hlc_state.logical = 0;");
        _ = builder.AppendLine("    } else if (received.physical > hlc_state.physical) {");
        _ = builder.AppendLine("        hlc_state.physical = received.physical;");
        _ = builder.AppendLine("        hlc_state.logical = received.logical + 1;");
        _ = builder.AppendLine("    } else if (hlc_state.physical > received.physical) {");
        _ = builder.AppendLine("        hlc_state.logical++;");
        _ = builder.AppendLine("    } else {");
        _ = builder.AppendLine("        hlc_state.logical = max(hlc_state.logical, received.logical) + 1;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return hlc_now();");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends warp reduction helper functions.
    /// </summary>
    private static void AppendWarpReductionHelpers(StringBuilder builder)
    {
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine("// Warp Reduction Helpers");
        _ = builder.AppendLine("// ===========================================================================");
        _ = builder.AppendLine();

        // Sum reduction
        _ = builder.AppendLine("// Warp-level sum reduction");
        _ = builder.AppendLine("__device__ float warp_reduce_sum(float val) {");
        _ = builder.AppendLine("    for (int offset = 16; offset > 0; offset >>= 1) {");
        _ = builder.AppendLine("        val += __shfl_down_sync(0xFFFFFFFF, val, offset);");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return val;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();

        // Max reduction
        _ = builder.AppendLine("// Warp-level max reduction");
        _ = builder.AppendLine("__device__ float warp_reduce_max(float val) {");
        _ = builder.AppendLine("    for (int offset = 16; offset > 0; offset >>= 1) {");
        _ = builder.AppendLine("        val = fmaxf(val, __shfl_down_sync(0xFFFFFFFF, val, offset));");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return val;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();

        // Min reduction
        _ = builder.AppendLine("// Warp-level min reduction");
        _ = builder.AppendLine("__device__ float warp_reduce_min(float val) {");
        _ = builder.AppendLine("    for (int offset = 16; offset > 0; offset >>= 1) {");
        _ = builder.AppendLine("        val = fminf(val, __shfl_down_sync(0xFFFFFFFF, val, offset));");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    return val;");
        _ = builder.AppendLine("}");
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
    /// Appends the message handler device function that processes individual messages.
    /// </summary>
    /// <remarks>
    /// The handler function is called from within the persistent kernel loop.
    /// It deserializes the input message, executes the business logic, and serializes the output.
    /// </remarks>
    private static void AppendHandlerFunction(StringBuilder builder, DiscoveredRingKernel kernel)
    {
        // Generate handler function name from kernel method
        var methodName = kernel.Method.Name
            .Replace("RingKernel", "", StringComparison.Ordinal)
            .Replace("Kernel", "", StringComparison.Ordinal);
        var handlerFunctionName = $"process_{ToSnakeCase(methodName)}_message";

        _ = builder.AppendLine("/**");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $" * @brief Message handler for {kernel.KernelId}");
        _ = builder.AppendLine(" * @param msg_buffer Pointer to serialized input message");
        _ = builder.AppendLine(" * @param msg_size Size of input message in bytes");
        _ = builder.AppendLine(" * @param output_buffer Pointer to output buffer");
        _ = builder.AppendLine(" * @param output_size_ptr Pointer to store output size");
        _ = builder.AppendLine(" * @param control_block Ring kernel control block");
        _ = builder.AppendLine(" * @return true if message processed successfully, false otherwise");
        _ = builder.AppendLine(" */");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"__device__ bool {handlerFunctionName}(");
        _ = builder.AppendLine("    const unsigned char* msg_buffer,");
        _ = builder.AppendLine("    int msg_size,");
        _ = builder.AppendLine("    unsigned char* output_buffer,");
        _ = builder.AppendLine("    int* output_size_ptr,");
        _ = builder.AppendLine("    RingKernelControlBlock* control_block)");
        _ = builder.AppendLine("{");

        // Check if this kernel has inline handler code (unified kernel)
        if (kernel.HasInlineHandler && !string.IsNullOrEmpty(kernel.InlineHandlerCudaCode))
        {
            // Use the translated CUDA code from C# method body
            _ = builder.AppendLine("    // Inline handler (translated from C# method body)");
            _ = builder.AppendLine(kernel.InlineHandlerCudaCode);
        }
        else
        {
            // Generate a stub handler that processes messages
            // This is a basic implementation - more sophisticated translation can be added
            _ = builder.AppendLine("    // Default handler stub - processes messages and produces output");
            _ = builder.AppendLine("    ");
            _ = builder.AppendLine("    // Validate input");
            _ = builder.AppendLine("    if (msg_buffer == nullptr || msg_size <= 0) {");
            _ = builder.AppendLine("        return false;");
            _ = builder.AppendLine("    }");
            _ = builder.AppendLine("    ");
            _ = builder.AppendLine("    // Process the message (echo/pass-through by default)");
            _ = builder.AppendLine("    // Copy input to output for testing");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    int output_size = min(msg_size, {kernel.MaxOutputMessageSizeBytes});");
            _ = builder.AppendLine("    for (int i = 0; i < output_size; i++) {");
            _ = builder.AppendLine("        output_buffer[i] = msg_buffer[i];");
            _ = builder.AppendLine("    }");
            _ = builder.AppendLine("    ");
            _ = builder.AppendLine("    // Set output size");
            _ = builder.AppendLine("    if (output_size_ptr != nullptr) {");
            _ = builder.AppendLine("        *output_size_ptr = output_size;");
            _ = builder.AppendLine("    }");
            _ = builder.AppendLine("    ");
            _ = builder.AppendLine("    return true;");
        }

        _ = builder.AppendLine("}");
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
        _ = builder.AppendLine("#ifndef RING_KERNEL_NON_COOPERATIVE");
        _ = builder.AppendLine("    // Full cooperative groups for grid-wide synchronization");
        _ = builder.AppendLine("    cooperative_groups::grid_group grid = cooperative_groups::this_grid();");
        _ = builder.AppendLine("    cooperative_groups::thread_block block = cooperative_groups::this_thread_block();");
        _ = builder.AppendLine("#else");
        _ = builder.AppendLine("    // Non-cooperative mode (WSL2 compatibility) - block-level sync only");
        _ = builder.AppendLine("    cooperative_groups::thread_block block = cooperative_groups::this_thread_block();");
        _ = builder.AppendLine("#define grid block  // Redirect grid sync to block sync for non-cooperative");
        _ = builder.AppendLine("#endif");
        _ = builder.AppendLine();

        _ = builder.AppendLine("    // Thread/block identification");
        _ = builder.AppendLine("    const int tid = threadIdx.x;");
        _ = builder.AppendLine("    const int bid = blockIdx.x;");
        _ = builder.AppendLine("    const int gid = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = builder.AppendLine();

        // GPU Timestamp capture (if enabled)
        if (kernel.EnableTimestamps)
        {
            _ = builder.AppendLine("    // GPU hardware timestamp tracking (for temporal consistency)");
            _ = builder.AppendLine("    long long kernel_start_time = clock64();");
            _ = builder.AppendLine();
        }

        _ = builder.AppendLine("    // Extract message queue pointers from control block");
        _ = builder.AppendLine("    // These pointers are set by the host during kernel activation");
        _ = builder.AppendLine("    MessageQueue* input_queue = reinterpret_cast<MessageQueue*>(control_block->input_queue_head_ptr);");
        _ = builder.AppendLine("    MessageQueue* output_queue = reinterpret_cast<MessageQueue*>(control_block->output_queue_head_ptr);");
        _ = builder.AppendLine();

        // K2K infrastructure declarations (if kernel uses K2K messaging)
        if (kernel.UsesK2KMessaging)
        {
            AppendK2KQueueDeclarations(builder, kernel);
        }

        // Processing mode setup
        var processingMode = kernel.ProcessingMode;
        var maxMessages = kernel.MaxMessagesPerIteration;
        var hasIterationLimit = maxMessages > 0;

        if (processingMode == Abstractions.RingKernels.RingProcessingMode.Adaptive)
        {
            _ = builder.AppendLine("    // Adaptive processing mode: dynamically adjust batch size based on queue depth");
            _ = builder.AppendLine("    const int ADAPTIVE_THRESHOLD = 10;  // Switch to batch mode if queue depth > threshold");
            _ = builder.AppendLine("    const int MAX_BATCH_SIZE = 16;");
            _ = builder.AppendLine();
        }
        else if (processingMode == Abstractions.RingKernels.RingProcessingMode.Batch)
        {
            _ = builder.AppendLine("    // Batch processing mode: process multiple messages per iteration for max throughput");
            _ = builder.AppendLine("    const int BATCH_SIZE = 16;");
            _ = builder.AppendLine();
        }

        // Generate main dispatch loop based on kernel mode
        // WSL2 AUTO-DETECTION: In WSL2, persistent kernels block CUDA API calls, so force EventDriven mode
        var isWsl2 = RingKernels.RingKernelControlBlockHelper.IsRunningInWsl2();
        var isEventDriven = kernel.Mode == Abstractions.RingKernels.RingKernelMode.EventDriven || isWsl2;
        var eventDrivenMaxIterations = kernel.EventDrivenMaxIterations > 0 ? kernel.EventDrivenMaxIterations : 1000;

        if (isWsl2 && kernel.Mode != Abstractions.RingKernels.RingKernelMode.EventDriven)
        {
            Console.WriteLine($"[WSL2] Overriding {kernel.Mode} mode to EventDriven mode for WSL2 compatibility (kernel: {kernel.KernelId})");
        }

        if (isEventDriven)
        {
            _ = builder.AppendLine("    // EventDriven kernel loop - runs for limited iterations then exits");
            _ = builder.AppendLine("    // WSL2 COMPATIBILITY: This mode allows host to update control block between launches");
            _ = builder.AppendLine("    // The kernel can be relaunched after it exits to continue processing");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    const int EVENT_DRIVEN_MAX_ITERATIONS = {eventDrivenMaxIterations};");
            _ = builder.AppendLine("    int dispatch_iteration = 0;");
            _ = builder.AppendLine();
            _ = builder.AppendLine("    while (control_block->should_terminate == 0 && dispatch_iteration < EVENT_DRIVEN_MAX_ITERATIONS)");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        dispatch_iteration++;");
        }
        else
        {
            _ = builder.AppendLine("    // Persistent kernel main loop - runs until termination flag is set");
            _ = builder.AppendLine("    // The kernel starts inactive (is_active=0) and waits for activation from the host");
            _ = builder.AppendLine("    while (control_block->should_terminate == 0)");
            _ = builder.AppendLine("    {");
        }

        // Check activation - continue after loop header
        _ = builder.AppendLine("        // Check if kernel is activated by the host");
        _ = builder.AppendLine("        if (control_block->is_active == 1)");
        _ = builder.AppendLine("        {");

        if (hasIterationLimit)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            // Iteration limiting: process max {maxMessages} messages per dispatch loop iteration");
            _ = builder.AppendLine("            int messages_this_iteration = 0;");
            _ = builder.AppendLine();
        }

        // Generate processing loop based on mode
        if (processingMode == Abstractions.RingKernels.RingProcessingMode.Batch)
        {
            _ = builder.AppendLine("            // Batch mode: process up to BATCH_SIZE messages");
            _ = builder.AppendLine("            for (int batch_idx = 0; batch_idx < BATCH_SIZE; batch_idx++)");
            _ = builder.AppendLine("            {");
            AppendMessageProcessingBlock(builder, kernel, "                ", hasIterationLimit);
            _ = builder.AppendLine("            }");
        }
        else if (processingMode == Abstractions.RingKernels.RingProcessingMode.Adaptive)
        {
            _ = builder.AppendLine("            // Adaptive mode: adjust batch size based on queue depth");
            _ = builder.AppendLine("            int queue_depth = 0;");
            _ = builder.AppendLine("            if (input_queue != nullptr && input_queue->head != nullptr && input_queue->tail != nullptr) {");
            _ = builder.AppendLine("                unsigned int h = input_queue->head->load(cuda::memory_order_relaxed);");
            _ = builder.AppendLine("                unsigned int t = input_queue->tail->load(cuda::memory_order_relaxed);");
            _ = builder.AppendLine("                queue_depth = static_cast<int>((t - h) & (input_queue->capacity - 1));");
            _ = builder.AppendLine("            }");
            _ = builder.AppendLine("            int batch_size = (queue_depth > ADAPTIVE_THRESHOLD) ? MAX_BATCH_SIZE : 1;");
            _ = builder.AppendLine("            for (int batch_idx = 0; batch_idx < batch_size; batch_idx++)");
            _ = builder.AppendLine("            {");
            AppendMessageProcessingBlock(builder, kernel, "                ", hasIterationLimit);
            _ = builder.AppendLine("            }");
        }
        else // Continuous
        {
            _ = builder.AppendLine("            // Continuous mode: process single message per iteration for min latency");
            AppendMessageProcessingBlock(builder, kernel, "            ", hasIterationLimit);
        }

        // Memory fence generation (based on MemoryConsistency)
        AppendMemoryFence(builder, kernel.MemoryConsistency, kernel.EnableCausalOrdering);

        // Barrier generation (based on UseBarriers and BarrierScope)
        if (kernel.UseBarriers)
        {
            AppendBarrier(builder, kernel.BarrierScope);
        }
        else
        {
            _ = builder.AppendLine();
            _ = builder.AppendLine("        // Cooperative grid-wide synchronization");
            _ = builder.AppendLine("        // Ensures all threads in the grid reach this point before continuing");
            _ = builder.AppendLine("        grid.sync();");
        }

        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();

        // Timestamp logging (if enabled)
        if (kernel.EnableTimestamps)
        {
            _ = builder.AppendLine("    // Log kernel execution time");
            _ = builder.AppendLine("    if (tid == 0 && bid == 0)");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        long long kernel_end_time = clock64();");
            _ = builder.AppendLine("        // Store total execution cycles in last_activity_ticks for timing metrics");
            _ = builder.AppendLine("        control_block->last_activity_ticks = kernel_end_time - kernel_start_time;");
            _ = builder.AppendLine("    }");
            _ = builder.AppendLine();
        }

        // Mark kernel as terminated (distinguish between permanent termination and EventDriven exit)
        if (isEventDriven)
        {
            _ = builder.AppendLine("    // EventDriven kernel exit handling");
            _ = builder.AppendLine("    if (tid == 0 && bid == 0)");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        if (control_block->should_terminate != 0)");
            _ = builder.AppendLine("        {");
            _ = builder.AppendLine("            // Permanent termination requested");
            _ = builder.AppendLine("            control_block->has_terminated = 1;");
            _ = builder.AppendLine("        }");
            _ = builder.AppendLine("        else");
            _ = builder.AppendLine("        {");
            _ = builder.AppendLine("            // Iteration limit reached - kernel can be relaunched");
            _ = builder.AppendLine("            // Set has_terminated = 2 to indicate relaunchable exit (not permanent termination)");
            _ = builder.AppendLine("            control_block->has_terminated = 2;");
            _ = builder.AppendLine("        }");
            _ = builder.AppendLine("    }");
        }
        else
        {
            _ = builder.AppendLine("    // Mark kernel as terminated");
            _ = builder.AppendLine("    if (tid == 0 && bid == 0)");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        control_block->has_terminated = 1;");
            _ = builder.AppendLine("    }");
        }
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the message processing block (reused for all processing modes).
    /// </summary>
    private static void AppendMessageProcessingBlock(
        StringBuilder builder,
        DiscoveredRingKernel kernel,
        string indent,
        bool hasIterationLimit)
    {
        var iterationCheck = hasIterationLimit ? $" && messages_this_iteration < {kernel.MaxMessagesPerIteration}" : "";

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}// Poll input queue for incoming messages");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}// Only thread 0 in each block handles message dequeuing to avoid races");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}if (tid == 0 && input_queue != nullptr && !input_queue->is_empty(){iterationCheck})");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}{{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    // Dequeue message into byte buffer");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    unsigned char msg_buffer[{kernel.MaxInputMessageSizeBytes}];");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    if (input_queue->try_dequeue(msg_buffer))");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    {{");

        if (hasIterationLimit)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        messages_this_iteration++;");
        }

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // ====================================================================");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Message Processing with Handler Function");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // ====================================================================");
        _ = builder.AppendLine();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Prepare output buffer for response");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        unsigned char response_buffer[{kernel.MaxOutputMessageSizeBytes}];");
        _ = builder.AppendLine();

        if (kernel.EnableTimestamps)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Capture message timestamp");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        long long message_timestamp = clock64();");
            _ = builder.AppendLine();
        }

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Call message handler to process the message");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // The handler deserializes input, executes logic, and serializes output");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        int output_size = 0;");

        // Generate handler function call based on kernel method name
        var handlerFunctionName = $"process_{ToSnakeCase(kernel.Method.Name.Replace("RingKernel", "", StringComparison.Ordinal).Replace("Kernel", "", StringComparison.Ordinal))}_message";
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        bool success = {handlerFunctionName}(");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            msg_buffer,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {kernel.MaxInputMessageSizeBytes},");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            response_buffer,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            &output_size,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            control_block);");
        _ = builder.AppendLine();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Enqueue response if processing succeeded");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        if (success && output_queue != nullptr)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            if (output_queue->try_enqueue(response_buffer))");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                // Atomically increment messages processed counter");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                atomicAdd((unsigned long long*)&control_block->messages_processed, 1ULL);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            else");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                // Output queue full - increment error counter");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                atomicAdd((unsigned*)&control_block->errors_encountered, 1);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        else if (!success)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            // Message processing failed - increment error counter");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            atomicAdd((unsigned*)&control_block->errors_encountered, 1);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}}}");

        if (!hasIterationLimit)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}else");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}{{");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    // Queue empty - break from batch processing");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    break;");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}}}");
        }
    }

    /// <summary>
    /// Appends memory fence instructions based on consistency model.
    /// </summary>
    private static void AppendMemoryFence(
        StringBuilder builder,
        Abstractions.Memory.MemoryConsistencyModel consistency,
        bool enableCausalOrdering)
    {
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();

        if (enableCausalOrdering || consistency != Abstractions.Memory.MemoryConsistencyModel.Relaxed)
        {
            _ = builder.AppendLine("        // Memory fence for consistency guarantees");

            switch (consistency)
            {
                case Abstractions.Memory.MemoryConsistencyModel.Sequential:
                    _ = builder.AppendLine("        // Sequential consistency: full memory barrier");
                    _ = builder.AppendLine("        __threadfence_system();");
                    break;

                case Abstractions.Memory.MemoryConsistencyModel.ReleaseAcquire:
                    _ = builder.AppendLine("        // Release-acquire semantics: ensure message writes visible before queue updates");
                    _ = builder.AppendLine("        __threadfence();");
                    break;

                default: // Relaxed
                    if (enableCausalOrdering)
                    {
                        _ = builder.AppendLine("        // Causal ordering enabled: lightweight fence");
                        _ = builder.AppendLine("        __threadfence_block();");
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Appends barrier synchronization based on scope.
    /// </summary>
    private static void AppendBarrier(StringBuilder builder, Abstractions.Barriers.BarrierScope scope)
    {
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Barrier synchronization");

        switch (scope)
        {
            case Abstractions.Barriers.BarrierScope.Warp:
                _ = builder.AppendLine("        // Warp-level barrier (32 threads)");
                _ = builder.AppendLine("        __syncwarp();");
                break;

            case Abstractions.Barriers.BarrierScope.ThreadBlock:
                _ = builder.AppendLine("        // Thread-block barrier (all threads in block)");
                _ = builder.AppendLine("        __syncthreads();");
                break;

            case Abstractions.Barriers.BarrierScope.Grid:
                _ = builder.AppendLine("        // Grid-wide barrier (cooperative launch required)");
                _ = builder.AppendLine("        grid.sync();");
                break;

            default:
                _ = builder.AppendLine("        // Default: grid synchronization");
                _ = builder.AppendLine("        grid.sync();");
                break;
        }
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
