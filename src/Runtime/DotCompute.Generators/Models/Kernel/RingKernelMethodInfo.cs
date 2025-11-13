// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Models.Kernel;

/// <summary>
/// Represents metadata about a Ring Kernel method for code generation.
/// Ring Kernels are persistent kernels with message-passing capabilities for
/// distributed computing and real-time data processing workloads.
/// </summary>
/// <remarks>
/// This class captures all configuration from the [RingKernel] attribute including:
/// - Kernel lifecycle properties (ID, capacity, queue sizes)
/// - Execution mode (Persistent vs EventDriven)
/// - Messaging strategy (SharedMemory, AtomicQueue, P2P, NCCL)
/// - Domain-specific optimizations
/// - Backend support and memory configuration
/// </remarks>
public sealed class RingKernelMethodInfo
{
    private readonly Collection<ParameterInfo> _parameters = [];
    private readonly Collection<string> _backends = [];

    /// <summary>
    /// Gets or sets the method name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully qualified name of the containing type.
    /// </summary>
    public string ContainingType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace containing the method.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets the method parameters.
    /// </summary>
    public Collection<ParameterInfo> Parameters => _parameters;

    /// <summary>
    /// Gets or sets the return type of the method.
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of supported backend accelerators.
    /// </summary>
    public Collection<string> Backends => _backends;

    /// <summary>
    /// Gets or sets the original method declaration syntax.
    /// </summary>
    public MethodDeclarationSyntax? MethodDeclaration { get; set; }

    // RingKernel-specific properties

    /// <summary>
    /// Gets or sets the unique identifier for this Ring Kernel.
    /// Used for kernel registration and runtime lookup.
    /// </summary>
    public string KernelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ring buffer capacity (number of messages).
    /// Must be a power of 2 for efficient modulo operations.
    /// Default: 1024 messages.
    /// </summary>
    public int Capacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the size of the input message queue.
    /// Determines how many incoming messages can be buffered before processing.
    /// Default: 256 messages.
    /// </summary>
    public int InputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the size of the output message queue.
    /// Determines how many outgoing messages can be buffered before sending.
    /// Default: 256 messages.
    /// </summary>
    public int OutputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the execution mode of the Ring Kernel.
    /// Persistent: Continuously running kernel with message processing loops.
    /// EventDriven: Kernel activates only when messages arrive.
    /// Default: Persistent.
    /// </summary>
    public string Mode { get; set; } = "Persistent";

    /// <summary>
    /// Gets or sets the message-passing strategy.
    /// SharedMemory: Uses shared memory for message passing (fastest, single-device).
    /// AtomicQueue: Uses atomic operations for thread-safe queues (good for multi-threading).
    /// P2P: Peer-to-peer transfers between GPUs (multi-GPU).
    /// NCCL: NVIDIA Collective Communications Library for distributed operations.
    /// Default: SharedMemory.
    /// </summary>
    public string MessagingStrategy { get; set; } = "SharedMemory";

    /// <summary>
    /// Gets or sets the domain-specific optimization hint.
    /// Enables specialized optimizations for specific workload patterns.
    /// Default: General (no domain-specific optimizations).
    /// </summary>
    public string Domain { get; set; } = "General";

    /// <summary>
    /// Gets or sets the grid dimensions for GPU kernel launch.
    /// Format: [x, y, z]. If not specified, runtime calculates optimal dimensions.
    /// </summary>
    public IReadOnlyList<int>? GridDimensions { get; set; }

    /// <summary>
    /// Gets or sets the block dimensions for GPU kernel launch.
    /// Format: [x, y, z]. If not specified, uses backend-specific defaults.
    /// Common: [256, 1, 1] for 1D workloads, [16, 16, 1] for 2D workloads.
    /// </summary>
    public IReadOnlyList<int>? BlockDimensions { get; set; }

    /// <summary>
    /// Gets or sets whether to use shared memory for message passing.
    /// Shared memory provides ultra-low latency but limited capacity.
    /// Only applicable for SharedMemory messaging strategy.
    /// Default: false (uses global memory).
    /// </summary>
    public bool UseSharedMemory { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// Only used when UseSharedMemory is true.
    /// Must fit within device shared memory limits (typically 48-96 KB per block).
    /// Default: 0 (auto-calculate based on queue sizes).
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether parallel execution is enabled.
    /// For CPU fallback, controls multi-threaded vs single-threaded execution.
    /// Default: true.
    /// </summary>
    public bool IsParallel { get; set; } = true;

    /// <summary>
    /// Gets or sets the vector size for SIMD operations (CPU backend only).
    /// Default: 8 (256-bit AVX2 vectors).
    /// </summary>
    public int VectorSize { get; set; } = 8;

    // Telemetry configuration

    /// <summary>
    /// Gets or sets a value indicating whether telemetry collection is enabled for this Ring Kernel.
    /// When true, the source generator injects performance telemetry code.
    /// </summary>
    public bool HasEnableTelemetry { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to collect detailed per-message metrics.
    /// Only applicable when HasEnableTelemetry is true.
    /// </summary>
    public bool TelemetryCollectDetailedMetrics { get; set; }

    /// <summary>
    /// Gets or sets the telemetry sampling rate (0.0 to 1.0).
    /// Only applicable when HasEnableTelemetry is true.
    /// Default: 1.0 (100% sampling).
    /// </summary>
    public double TelemetrySamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets a value indicating whether to track memory allocations.
    /// Only applicable when HasEnableTelemetry is true.
    /// Default: true.
    /// </summary>
    public bool TelemetryTrackMemory { get; set; } = true;

    /// <summary>
    /// Gets or sets the custom telemetry provider type name.
    /// Only applicable when HasEnableTelemetry is true.
    /// If null, uses default IKernelTelemetryProvider from DI.
    /// </summary>
    public string? TelemetryCustomProviderType { get; set; }
}
