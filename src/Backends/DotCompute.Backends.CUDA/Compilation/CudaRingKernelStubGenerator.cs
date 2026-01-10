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
        // Note: printf is built-in for CUDA device code with NVRTC, no include needed
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
            for (var i = 0; i < kernel.PublishesToKernels.Count; i++)
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
            for (var i = 0; i < kernel.SubscribesToKernels.Count; i++)
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
        _ = builder.AppendLine("// Volatile load/store helpers for cross-CPU/GPU visibility of control flags");
        _ = builder.AppendLine("// CRITICAL: In WSL2, we need system-wide memory barriers for host visibility");
        _ = builder.AppendLine("__device__ __forceinline__ int volatile_load_int(volatile int* ptr) {");
        _ = builder.AppendLine("    // System-wide fence ensures all prior memory operations from host are visible");
        _ = builder.AppendLine("    __threadfence_system();");
        _ = builder.AppendLine("    // Use volatile load to bypass L1 cache");
        _ = builder.AppendLine("    int value = *ptr;");
        _ = builder.AppendLine("    return value;");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
        _ = builder.AppendLine("__device__ __forceinline__ void volatile_store_int(volatile int* ptr, int value) {");
        _ = builder.AppendLine("    *ptr = value;");
        _ = builder.AppendLine("    // System-wide fence ensures the store is visible to host");
        _ = builder.AppendLine("    __threadfence_system();");
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// RingKernelControlBlock struct - matches C# layout (128 bytes, 4-byte aligned)");
        _ = builder.AppendLine("// Memory layout:");
        _ = builder.AppendLine("// - Flags: is_active (0), should_terminate (4), has_terminated (8), errors_encountered (12)");
        _ = builder.AppendLine("// - Counters: messages_processed (16)");
        _ = builder.AppendLine("// - Timestamp: last_activity_ticks (24)");
        _ = builder.AppendLine("// - Input Queue: head_ptr (32), tail_ptr (40), buffer_ptr (48), capacity (56), message_size (60)");
        _ = builder.AppendLine("// - Output Queue: head_ptr (64), tail_ptr (72), buffer_ptr (80), capacity (88), message_size (92)");
        _ = builder.AppendLine("// - Reserved: padding to 128 bytes (96-127)");
        _ = builder.AppendLine("struct RingKernelControlBlock {");
        _ = builder.AppendLine("    volatile int is_active;            // Atomic flag: 1 = active, 0 = inactive (volatile for CPU/GPU visibility)");
        _ = builder.AppendLine("    volatile int should_terminate;     // Atomic flag: 1 = terminate, 0 = continue (volatile for CPU/GPU visibility)");
        _ = builder.AppendLine("    int has_terminated;               // Atomic flag: 1 = terminated, 0 = running, 2 = relaunchable");
        _ = builder.AppendLine("    int errors_encountered;           // Atomic error counter");
        _ = builder.AppendLine("    long long messages_processed;     // Atomic message counter");
        _ = builder.AppendLine("    long long last_activity_ticks;    // Atomic timestamp");
        _ = builder.AppendLine("    ");
        _ = builder.AppendLine("    // Input queue pointers and metadata");
        _ = builder.AppendLine("    long long input_queue_head_ptr;   // Device pointer to input queue head atomic");
        _ = builder.AppendLine("    long long input_queue_tail_ptr;   // Device pointer to input queue tail atomic");
        _ = builder.AppendLine("    long long input_queue_buffer_ptr; // Device pointer to input queue data buffer");
        _ = builder.AppendLine("    int input_queue_capacity;         // Input queue capacity (power of 2)");
        _ = builder.AppendLine("    int input_queue_message_size;     // Input queue message size in bytes");
        _ = builder.AppendLine("    ");
        _ = builder.AppendLine("    // Output queue pointers and metadata");
        _ = builder.AppendLine("    long long output_queue_head_ptr;  // Device pointer to output queue head atomic");
        _ = builder.AppendLine("    long long output_queue_tail_ptr;  // Device pointer to output queue tail atomic");
        _ = builder.AppendLine("    long long output_queue_buffer_ptr;// Device pointer to output queue data buffer");
        _ = builder.AppendLine("    int output_queue_capacity;        // Output queue capacity (power of 2)");
        _ = builder.AppendLine("    int output_queue_message_size;    // Output queue message size in bytes");
        _ = builder.AppendLine("    ");
        _ = builder.AppendLine("    // Reserved padding to 128 bytes");
        _ = builder.AppendLine("    long long _reserved1;");
        _ = builder.AppendLine("    long long _reserved2;");
        _ = builder.AppendLine("    long long _reserved3;");
        _ = builder.AppendLine("    long long _reserved4;");
        _ = builder.AppendLine("};");
        _ = builder.AppendLine();

        _ = builder.AppendLine("// System-scope atomic type for CPU-GPU coherence");
        _ = builder.AppendLine("// This enables true fine-grained memory coherence where CPU writes are");
        _ = builder.AppendLine("// immediately visible to GPU and vice versa (requires CC 6.0+ and HMM support)");
        _ = builder.AppendLine("using system_atomic_uint = cuda::atomic<unsigned int, cuda::thread_scope_system>;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("// MessageQueue struct - lock-free message passing infrastructure");
        _ = builder.AppendLine("struct MessageQueue {");
        _ = builder.AppendLine("    unsigned char* buffer;            // Message buffer (serialized)");
        _ = builder.AppendLine("    unsigned int capacity;            // Queue capacity (power of 2)");
        _ = builder.AppendLine("    unsigned int message_size;        // Size of each message in bytes");
        _ = builder.AppendLine("    system_atomic_uint* head;         // Dequeue position (system-scope for CPU-GPU)");
        _ = builder.AppendLine("    system_atomic_uint* tail;         // Enqueue position (system-scope for CPU-GPU)");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    /// Try to enqueue a message (SPSC optimized - single producer per queue)");
        _ = builder.AppendLine("    __device__ bool try_enqueue(const unsigned char* message) {");
        _ = builder.AppendLine("        unsigned int current_tail = tail->load(cuda::memory_order_relaxed);");
        _ = builder.AppendLine("        unsigned int next_tail = (current_tail + 1) & (capacity - 1);");
        _ = builder.AppendLine("        unsigned int current_head = head->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        if (next_tail == current_head) {");
        _ = builder.AppendLine("            return false; // Queue full");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // CRITICAL: Write message BEFORE advancing tail to prevent race condition");
        _ = builder.AppendLine("        // Without this order, consumer might see advanced tail and read garbage data");
        _ = builder.AppendLine("        unsigned char* dest = buffer + (current_tail * message_size);");
        _ = builder.AppendLine("        for (unsigned int i = 0; i < message_size; ++i) {");
        _ = builder.AppendLine("            dest[i] = message[i];");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Memory fence to ensure message write is visible before tail advance");
        _ = builder.AppendLine("        __threadfence();");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Advance tail with release semantics (SPSC: simple store is sufficient)");
        _ = builder.AppendLine("        tail->store(next_tail, cuda::memory_order_release);");
        _ = builder.AppendLine("        return true;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    /// Try to dequeue a message (SPSC optimized - single consumer per queue)");
        _ = builder.AppendLine("    __device__ bool try_dequeue(unsigned char* out_message) {");
        _ = builder.AppendLine("        // System-wide fence ensures CPU writes to tail are visible to GPU");
        _ = builder.AppendLine("        __threadfence_system();");
        _ = builder.AppendLine("        unsigned int current_head = head->load(cuda::memory_order_relaxed);");
        _ = builder.AppendLine("        unsigned int current_tail = tail->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine("        printf(\"[DEQUEUE] head=%u, tail=%u, capacity=%u, msg_size=%u\\n\", current_head, current_tail, capacity, message_size);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        if (current_head == current_tail) {");
        _ = builder.AppendLine("            printf(\"[DEQUEUE] EMPTY! returning false\\n\");");
        _ = builder.AppendLine("            return false; // Queue empty");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // CRITICAL: Read message BEFORE advancing head to prevent race condition");
        _ = builder.AppendLine("        // Without this order, producer might overwrite slot before we finish reading");
        _ = builder.AppendLine("        const unsigned char* src = buffer + (current_head * message_size);");
        _ = builder.AppendLine("        for (unsigned int i = 0; i < message_size; ++i) {");
        _ = builder.AppendLine("            out_message[i] = src[i];");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Memory fence to ensure message read completes before head advance");
        _ = builder.AppendLine("        __threadfence();");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // Advance head with release semantics (SPSC: simple store is sufficient)");
        _ = builder.AppendLine("        unsigned int next_head = (current_head + 1) & (capacity - 1);");
        _ = builder.AppendLine("        head->store(next_head, cuda::memory_order_release);");
        _ = builder.AppendLine("        return true;");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    /// Check if queue is empty");
        _ = builder.AppendLine("    __device__ bool is_empty() const {");
        _ = builder.AppendLine("        // System-wide fence ensures CPU writes to head/tail are visible to GPU");
        _ = builder.AppendLine("        __threadfence_system();");
        _ = builder.AppendLine("        unsigned int h = head->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine("        unsigned int t = tail->load(cuda::memory_order_acquire);");
        _ = builder.AppendLine("        bool empty = (h == t);");
        _ = builder.AppendLine("        printf(\"[IS_EMPTY] head=%u, tail=%u, result=%d\\n\", h, t, empty ? 1 : 0);");
        _ = builder.AppendLine("        return empty;");
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

        _ = builder.AppendLine("    // Construct message queues from control block fields");
        _ = builder.AppendLine("    // The host runtime populates these pointers during kernel activation");
        _ = builder.AppendLine("    __shared__ MessageQueue input_queue_storage;");
        _ = builder.AppendLine("    __shared__ MessageQueue output_queue_storage;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // Shared memory buffers for message processing (avoids per-thread stack allocation)");
        _ = builder.AppendLine("    // These are used only by thread 0, but shared to avoid stack overflow");

        // Cap message buffer sizes to fit in shared memory (typically 48KB available)
        // Use 16KB max per buffer to leave room for other shared variables
        const int maxSharedBufferSize = 16384;
        var inputBufferSize = Math.Min(kernel.MaxInputMessageSizeBytes, maxSharedBufferSize);
        var outputBufferSize = Math.Min(kernel.MaxOutputMessageSizeBytes, maxSharedBufferSize);

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    __shared__ unsigned char shared_msg_buffer[{inputBufferSize}];");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    __shared__ unsigned char shared_response_buffer[{outputBufferSize}];");
        _ = builder.AppendLine("    ");
        _ = builder.AppendLine("    // Thread 0 initializes the shared queue structures");
        _ = builder.AppendLine("    if (tid == 0) {");
        _ = builder.AppendLine("        // Initialize input queue from control block");
        _ = builder.AppendLine("        input_queue_storage.buffer = reinterpret_cast<unsigned char*>(control_block->input_queue_buffer_ptr);");
        _ = builder.AppendLine("        input_queue_storage.capacity = static_cast<unsigned int>(control_block->input_queue_capacity);");
        _ = builder.AppendLine("        input_queue_storage.message_size = static_cast<unsigned int>(control_block->input_queue_message_size);");
        _ = builder.AppendLine("        input_queue_storage.head = reinterpret_cast<system_atomic_uint*>(control_block->input_queue_head_ptr);");
        _ = builder.AppendLine("        input_queue_storage.tail = reinterpret_cast<system_atomic_uint*>(control_block->input_queue_tail_ptr);");
        _ = builder.AppendLine("        ");
        _ = builder.AppendLine("        // Initialize output queue from control block");
        _ = builder.AppendLine("        output_queue_storage.buffer = reinterpret_cast<unsigned char*>(control_block->output_queue_buffer_ptr);");
        _ = builder.AppendLine("        output_queue_storage.capacity = static_cast<unsigned int>(control_block->output_queue_capacity);");
        _ = builder.AppendLine("        output_queue_storage.message_size = static_cast<unsigned int>(control_block->output_queue_message_size);");
        _ = builder.AppendLine("        output_queue_storage.head = reinterpret_cast<system_atomic_uint*>(control_block->output_queue_head_ptr);");
        _ = builder.AppendLine("        output_queue_storage.tail = reinterpret_cast<system_atomic_uint*>(control_block->output_queue_tail_ptr);");
        _ = builder.AppendLine();
        _ = builder.AppendLine("        // DEBUG: Print queue initialization values");
        _ = builder.AppendLine("        printf(\"[KERNEL] Queue init: input_buf=0x%llx, input_head=0x%llx, input_tail=0x%llx, capacity=%u, msg_size=%u\\n\",");
        _ = builder.AppendLine("               (unsigned long long)control_block->input_queue_buffer_ptr,");
        _ = builder.AppendLine("               (unsigned long long)control_block->input_queue_head_ptr,");
        _ = builder.AppendLine("               (unsigned long long)control_block->input_queue_tail_ptr,");
        _ = builder.AppendLine("               input_queue_storage.capacity, input_queue_storage.message_size);");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine("    ");
        _ = builder.AppendLine("    // Synchronize to ensure all threads see initialized queues");
        _ = builder.AppendLine("    block.sync();");
        _ = builder.AppendLine("    ");
        _ = builder.AppendLine("    // Create pointers to the shared queue structures");
        _ = builder.AppendLine("    MessageQueue* input_queue = (control_block->input_queue_buffer_ptr != 0) ? &input_queue_storage : nullptr;");
        _ = builder.AppendLine("    MessageQueue* output_queue = (control_block->output_queue_buffer_ptr != 0) ? &output_queue_storage : nullptr;");
        _ = builder.AppendLine();
        _ = builder.AppendLine("    // DEBUG: Print queue pointer status");
        _ = builder.AppendLine("    if (tid == 0 && bid == 0) {");
        _ = builder.AppendLine("        printf(\"[KERNEL] Queue ptrs: input_queue=0x%llx, output_queue=0x%llx\\n\",");
        _ = builder.AppendLine("               (unsigned long long)(void*)input_queue, (unsigned long long)(void*)output_queue);");
        _ = builder.AppendLine("    }");
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
        // AUTO-DETECTION: Force EventDriven mode when:
        // 1. WSL2 - GPU-PV layer doesn't support CPU-GPU atomic coherence
        // 2. GPU doesn't support host native atomics (HostNativeAtomicSupported=0)
        // Most consumer/laptop GPUs (RTX 2000/3000/4000 series) don't support this feature
        var isWsl2 = RingKernels.RingKernelControlBlockHelper.IsRunningInWsl2();

        // Check GPU capabilities for atomic coherence support
        var supportsHostAtomics = true;
        var atomicCheckProps = new Types.Native.CudaDeviceProperties();
        var atomicCheckResult = Native.CudaRuntime.cudaGetDeviceProperties(ref atomicCheckProps, 0);
        if (atomicCheckResult == Types.Native.CudaError.Success)
        {
            supportsHostAtomics = atomicCheckProps.HostNativeAtomicSupported != 0;
        }

        var isEventDriven = kernel.Mode == Abstractions.RingKernels.RingKernelMode.EventDriven || isWsl2 || !supportsHostAtomics;
        var eventDrivenMaxIterations = kernel.EventDrivenMaxIterations > 0 ? kernel.EventDrivenMaxIterations : 1000;

        if ((isWsl2 || !supportsHostAtomics) && kernel.Mode != Abstractions.RingKernels.RingKernelMode.EventDriven)
        {
            var reason = isWsl2 ? "WSL2 compatibility" : "GPU lacks HostNativeAtomicSupported";
            Console.WriteLine($"[EventDriven] Overriding {kernel.Mode} mode to EventDriven mode for {reason} (kernel: {kernel.KernelId})");
        }

        if (isEventDriven)
        {
            _ = builder.AppendLine("    // EventDriven kernel loop - runs for limited iterations then exits");
            _ = builder.AppendLine("    // WSL2 COMPATIBILITY: This mode allows host to update control block between launches");
            _ = builder.AppendLine("    // The kernel can be relaunched after it exits to continue processing");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    const int EVENT_DRIVEN_MAX_ITERATIONS = {eventDrivenMaxIterations};");
            _ = builder.AppendLine("    int dispatch_iteration = 0;");
            _ = builder.AppendLine("    int total_loops = 0;");
            _ = builder.AppendLine("    // Wait up to ~100ms for activation (GPU loops are nanoseconds, need millions)");
            _ = builder.AppendLine("    // 100M loops @ 1ns/loop = 100ms warmup window");
            _ = builder.AppendLine("    const int MAX_WARMUP_LOOPS = 100000000;");
            _ = builder.AppendLine();
            _ = builder.AppendLine("    // CRITICAL: Use volatile loads for should_terminate and is_active to ensure");
            _ = builder.AppendLine("    // we see updates from the host. Regular loads may be cached in L1/L2.");
            _ = builder.AppendLine("    // NOTE: dispatch_iteration only counts ACTUAL MESSAGE PROCESSING, not empty loops.");
            _ = builder.AppendLine("    // This prevents the kernel from burning through iterations when queue is empty.");
            _ = builder.AppendLine("    while (volatile_load_int(&control_block->should_terminate) == 0 && dispatch_iteration < EVENT_DRIVEN_MAX_ITERATIONS)");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        total_loops++;");
            _ = builder.AppendLine("        // Check activation state (used for message processing gate)");
            _ = builder.AppendLine("        int is_active_val = volatile_load_int(&control_block->is_active);");
            _ = builder.AppendLine("        // Don't increment dispatch_iteration here - only increment when message is processed");
            _ = builder.AppendLine("        // This allows kernel to wait for messages without burning through iterations");
            _ = builder.AppendLine("        if (is_active_val != 1 && total_loops > MAX_WARMUP_LOOPS) {");
            _ = builder.AppendLine("            // Prevent infinite spin if never activated");
            _ = builder.AppendLine("            break;");
            _ = builder.AppendLine("        }");
        }
        else
        {
            _ = builder.AppendLine("    // Persistent kernel main loop - runs until termination flag is set");
            _ = builder.AppendLine("    // The kernel starts inactive (is_active=0) and waits for activation from the host");
            _ = builder.AppendLine("    while (volatile_load_int(&control_block->should_terminate) == 0)");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        // Load is_active with volatile semantics for CPU/GPU visibility");
            _ = builder.AppendLine("        int is_active_val = volatile_load_int(&control_block->is_active);");
        }

        // Check activation - continue after loop header
        if (isEventDriven)
        {
            _ = builder.AppendLine("        // DEBUG: Trace activation and queue state");
            _ = builder.AppendLine("        if (tid == 0 && bid == 0) {");
            _ = builder.AppendLine("            // Print first 3 total loops, then print when becoming active");
            _ = builder.AppendLine("            if (total_loops <= 3 || (is_active_val == 1 && dispatch_iteration <= 3)) {");
            _ = builder.AppendLine("                unsigned int h = input_queue ? input_queue->head->load(cuda::memory_order_relaxed) : 0;");
            _ = builder.AppendLine("                unsigned int t = input_queue ? input_queue->tail->load(cuda::memory_order_relaxed) : 0;");
            _ = builder.AppendLine("                printf(\"[KERNEL] Loop %d (active_iter=%d): is_active=%d, head=%u, tail=%u, empty=%d\\n\",");
            _ = builder.AppendLine("                       total_loops, dispatch_iteration, is_active_val, h, t, (h == t) ? 1 : 0);");
            _ = builder.AppendLine("            }");
            _ = builder.AppendLine("        }");
            _ = builder.AppendLine();
        }
        _ = builder.AppendLine("        // Check if kernel is activated by the host (message processing only happens when active)");
        _ = builder.AppendLine("        if (is_active_val == 1)");
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
            AppendMessageProcessingBlock(builder, kernel, "                ", hasIterationLimit, isEventDriven);
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
            AppendMessageProcessingBlock(builder, kernel, "                ", hasIterationLimit, isEventDriven);
            _ = builder.AppendLine("            }");
        }
        else // Continuous
        {
            _ = builder.AppendLine("            // Continuous mode: process single message per iteration for min latency");
            AppendMessageProcessingBlock(builder, kernel, "            ", hasIterationLimit, isEventDriven);
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
            _ = builder.AppendLine("        printf(\"[KERNEL] EXIT: dispatch_iteration=%d, should_terminate=%d, messages_processed=%llu\\n\",");
            _ = builder.AppendLine("               dispatch_iteration, control_block->should_terminate, control_block->messages_processed);");
            _ = builder.AppendLine("        // System-wide memory fence to ensure writes are visible to CPU");
            _ = builder.AppendLine("        __threadfence_system();");
            _ = builder.AppendLine("        ");
            _ = builder.AppendLine("        if (control_block->should_terminate != 0)");
            _ = builder.AppendLine("        {");
            _ = builder.AppendLine("            // Permanent termination requested");
            _ = builder.AppendLine("            control_block->has_terminated = 1;");
            _ = builder.AppendLine("            printf(\"[KERNEL] Setting has_terminated=1 (permanent)\\n\");");
            _ = builder.AppendLine("        }");
            _ = builder.AppendLine("        else");
            _ = builder.AppendLine("        {");
            _ = builder.AppendLine("            // Iteration limit reached - kernel can be relaunched");
            _ = builder.AppendLine("            // Set has_terminated = 2 to indicate relaunchable exit (not permanent termination)");
            _ = builder.AppendLine("            control_block->has_terminated = 2;");
            _ = builder.AppendLine("            printf(\"[KERNEL] Setting has_terminated=2 (relaunchable)\\n\");");
            _ = builder.AppendLine("        }");
            _ = builder.AppendLine("        ");
            _ = builder.AppendLine("        // Final fence to ensure has_terminated write is visible");
            _ = builder.AppendLine("        __threadfence_system();");
            _ = builder.AppendLine("    }");
        }
        else
        {
            _ = builder.AppendLine("    // Mark kernel as terminated");
            _ = builder.AppendLine("    if (tid == 0 && bid == 0)");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        // System-wide memory fence to ensure writes are visible to CPU");
            _ = builder.AppendLine("        __threadfence_system();");
            _ = builder.AppendLine("        control_block->has_terminated = 1;");
            _ = builder.AppendLine("        __threadfence_system();");
            _ = builder.AppendLine("    }");
        }
        _ = builder.AppendLine("}");
        _ = builder.AppendLine();
    }

    /// <summary>
    /// Appends the message processing block (reused for all processing modes).
    /// </summary>
    /// <param name="builder">The StringBuilder to append to.</param>
    /// <param name="kernel">The discovered ring kernel metadata.</param>
    /// <param name="indent">Indentation string for generated code.</param>
    /// <param name="hasIterationLimit">True if iteration limiting is enabled.</param>
    /// <param name="isEventDriven">True if kernel is in EventDriven mode (has dispatch_iteration counter).</param>
    private static void AppendMessageProcessingBlock(
        StringBuilder builder,
        DiscoveredRingKernel kernel,
        string indent,
        bool hasIterationLimit,
        bool isEventDriven = false)
    {
        var iterationCheck = hasIterationLimit ? $" && messages_this_iteration < {kernel.MaxMessagesPerIteration}" : "";

        // Calculate capped buffer size (must match shared memory declarations)
        const int maxSharedBufferSizeForProcessing = 16384;
        var cappedInputBufferSize = Math.Min(kernel.MaxInputMessageSizeBytes, maxSharedBufferSizeForProcessing);

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}// Poll input queue for incoming messages");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}// Only thread 0 in each block handles message dequeuing to avoid races");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}if (tid == 0 && input_queue != nullptr && !input_queue->is_empty(){iterationCheck})");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}{{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    // DEBUG: Entered dequeue condition - queue is not empty");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    printf(\"[KERNEL] DEQUEUE PATH: Entering dequeue. capacity=%u, msg_size=%u, buffer=0x%llx\\n\",");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}           input_queue->capacity, input_queue->message_size, (unsigned long long)input_queue->buffer);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    ");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    // Use shared memory buffer for message (avoids stack overflow)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    unsigned char* msg_buffer = shared_msg_buffer;");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    bool dequeue_result = input_queue->try_dequeue(msg_buffer);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    printf(\"[KERNEL] DEQUEUE RESULT: %d\\n\", dequeue_result ? 1 : 0);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    if (dequeue_result)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // DEBUG: Message dequeued successfully");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        printf(\"[KERNEL] MESSAGE DEQUEUED! First 4 bytes: %02x %02x %02x %02x\\n\", msg_buffer[0], msg_buffer[1], msg_buffer[2], msg_buffer[3]);");

        if (hasIterationLimit)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        messages_this_iteration++;");
        }

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // ====================================================================");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Message Processing with Handler Function");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // ====================================================================");
        _ = builder.AppendLine();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Use shared memory buffer for response (avoids stack overflow)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        unsigned char* response_buffer = shared_response_buffer;");
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
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {cappedInputBufferSize},");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            response_buffer,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            &output_size,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            control_block);");
        _ = builder.AppendLine();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // Track message processing result");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        if (success)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            // Atomically increment messages processed counter (tracks successful input processing)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            atomicAdd((unsigned long long*)&control_block->messages_processed, 1ULL);");

        // Only increment dispatch_iteration in EventDriven mode (variable only exists in that mode)
        if (isEventDriven)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            // EventDriven mode: Increment dispatch_iteration ONLY when message is actually processed");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            // This prevents the kernel from burning through iterations when queue is empty");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            dispatch_iteration++;");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            printf(\"[KERNEL] MESSAGE PROCESSED! Count now: %llu, dispatch_iteration=%d\\n\", control_block->messages_processed, dispatch_iteration);");
        }
        else
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            printf(\"[KERNEL] MESSAGE PROCESSED! Count now: %llu\\n\", control_block->messages_processed);");
        }
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            ");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            // If output queue exists, try to enqueue response");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            printf(\"[KERNEL] OUTPUT_QUEUE: ptr=%p, output_size=%d\\n\", (void*)output_queue, output_size);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            if (output_queue != nullptr && output_size > 0)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                printf(\"[KERNEL] OUTPUT_QUEUE ENQUEUE: buf=%p, capacity=%u, msg_size=%u, head=%u, tail=%u\\n\",");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                       (void*)output_queue->buffer, output_queue->capacity, output_queue->message_size,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                       output_queue->head->load(cuda::memory_order_relaxed), output_queue->tail->load(cuda::memory_order_relaxed));");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                bool enqueue_result = output_queue->try_enqueue(response_buffer);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                printf(\"[KERNEL] OUTPUT_QUEUE ENQUEUE RESULT: %d, new_head=%u, new_tail=%u\\n\",");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                       enqueue_result ? 1 : 0,");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                       output_queue->head->load(cuda::memory_order_relaxed), output_queue->tail->load(cuda::memory_order_relaxed));");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                if (!enqueue_result)");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                    // Output queue full - increment error counter");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                    printf(\"[KERNEL] OUTPUT_QUEUE FULL! Incrementing error counter\\n\");");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                    atomicAdd((unsigned*)&control_block->errors_encountered, 1);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                else");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                    // CRITICAL: System-wide memory fence to ensure CPU sees the enqueued message");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                    // Without this, the CPU (bridge) may not see the tail update in time");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                    __threadfence_system();");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                    printf(\"[KERNEL] OUTPUT_QUEUE: Fence issued, tail now visible to CPU\\n\");");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}                }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        else");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            // Message processing failed - increment error counter");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            printf(\"[KERNEL] HANDLER FAILED! Incrementing error counter\\n\");");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}            atomicAdd((unsigned*)&control_block->errors_encountered, 1);");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    else");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    {{");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        // DEBUG: try_dequeue failed even though is_empty() returned false");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}        printf(\"[KERNEL] DEQUEUE FAILED! head=%u, tail=%u (race condition?)\\n\",");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}               input_queue->head->load(cuda::memory_order_relaxed),");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}               input_queue->tail->load(cuda::memory_order_relaxed));");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    }}");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}}}");

        // NOTE: Do NOT add an else-break here. The original code broke when queue was empty,
        // but that caused immediate exit since ALL threads (tid != 0) would hit the else branch.
        // The loop is bounded by EVENT_DRIVEN_MAX_ITERATIONS, so it will naturally exit.
        // Continuous polling allows messages to arrive while the kernel is running.
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
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
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
