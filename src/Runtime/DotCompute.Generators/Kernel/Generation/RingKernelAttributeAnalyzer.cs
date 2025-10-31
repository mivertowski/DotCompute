// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel.Enums;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Analyzes Ring Kernel attributes to extract configuration and messaging specifications.
/// Processes [RingKernel] attribute data to determine persistent kernel settings,
/// message queue configuration, and execution parameters.
/// </summary>
/// <remarks>
/// Ring Kernels are persistent kernels with message-passing capabilities designed for:
/// - Real-time data processing pipelines
/// - Distributed computing with inter-kernel communication
/// - Stream processing with backpressure handling
/// - Event-driven compute workflows
///
/// This analyzer extracts configuration including:
/// - Kernel lifecycle properties (ID, capacity, queue sizes)
/// - Execution mode (Persistent vs EventDriven)
/// - Messaging strategy (SharedMemory, AtomicQueue, P2P, NCCL)
/// - Domain-specific optimizations
/// - Backend support and memory configuration
/// </remarks>
public sealed class RingKernelAttributeAnalyzer
{
    /// <summary>
    /// Analyzes a Ring Kernel attribute and extracts complete configuration data.
    /// </summary>
    /// <param name="ringKernelAttribute">The [RingKernel] attribute to analyze.</param>
    /// <returns>A RingKernelMethodInfo object containing all extracted settings.</returns>
    /// <remarks>
    /// This method processes the Ring Kernel attribute to extract all configuration:
    /// - Kernel identity and lifecycle properties
    /// - Message queue capacities and sizes
    /// - Execution mode and messaging strategy
    /// - Backend accelerator support
    /// - GPU execution dimensions
    /// - Shared memory configuration
    /// </remarks>
    public static RingKernelMethodInfo AnalyzeRingKernelConfiguration(AttributeData ringKernelAttribute)
    {
        var info = new RingKernelMethodInfo
        {
            KernelId = ExtractKernelId(ringKernelAttribute),
            Capacity = ExtractCapacity(ringKernelAttribute),
            InputQueueSize = ExtractInputQueueSize(ringKernelAttribute),
            OutputQueueSize = ExtractOutputQueueSize(ringKernelAttribute),
            Mode = ExtractMode(ringKernelAttribute),
            MessagingStrategy = ExtractMessagingStrategy(ringKernelAttribute),
            Domain = ExtractDomain(ringKernelAttribute),
            GridDimensions = ExtractGridDimensions(ringKernelAttribute),
            BlockDimensions = ExtractBlockDimensions(ringKernelAttribute),
            UseSharedMemory = ExtractUseSharedMemory(ringKernelAttribute),
            SharedMemorySize = ExtractSharedMemorySize(ringKernelAttribute),
            IsParallel = ExtractIsParallel(ringKernelAttribute),
            VectorSize = ExtractVectorSize(ringKernelAttribute)
        };

        var backends = ExtractSupportedBackends(ringKernelAttribute);
        foreach (var backend in backends)
        {
            info.Backends.Add(backend);
        }

        return info;
    }

    /// <summary>
    /// Extracts the kernel identifier from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The kernel ID, or empty string if not specified.</returns>
    private static string ExtractKernelId(AttributeData attribute)
    {
        var kernelIdArgument = GetNamedArgument(attribute, "KernelId");
        if (kernelIdArgument.HasValue && kernelIdArgument.Value.Value is string kernelId)
        {
            return kernelId;
        }
        return string.Empty;
    }

    /// <summary>
    /// Extracts the ring buffer capacity from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The capacity in number of messages.</returns>
    /// <remarks>
    /// Capacity must be a power of 2 for efficient modulo operations.
    /// Default: 1024 messages.
    /// </remarks>
    private static int ExtractCapacity(AttributeData attribute)
    {
        var capacityArgument = GetNamedArgument(attribute, "Capacity");
        if (capacityArgument.HasValue && capacityArgument.Value.Value is int capacity)
        {
            return capacity;
        }
        return 1024;
    }

    /// <summary>
    /// Extracts the input queue size from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The input queue size in number of messages.</returns>
    private static int ExtractInputQueueSize(AttributeData attribute)
    {
        var sizeArgument = GetNamedArgument(attribute, "InputQueueSize");
        if (sizeArgument.HasValue && sizeArgument.Value.Value is int size)
        {
            return size;
        }
        return 256;
    }

    /// <summary>
    /// Extracts the output queue size from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The output queue size in number of messages.</returns>
    private static int ExtractOutputQueueSize(AttributeData attribute)
    {
        var sizeArgument = GetNamedArgument(attribute, "OutputQueueSize");
        if (sizeArgument.HasValue && sizeArgument.Value.Value is int size)
        {
            return size;
        }
        return 256;
    }

    /// <summary>
    /// Extracts the execution mode from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The execution mode (Persistent or EventDriven).</returns>
    private static string ExtractMode(AttributeData attribute)
    {
        var modeArgument = GetNamedArgument(attribute, "Mode");
        if (modeArgument.HasValue)
        {
            // Try to get enum member name from Roslyn
            if (modeArgument.Value.Type?.TypeKind == TypeKind.Enum && modeArgument.Value.Value is int enumValue)
            {
                var enumType = modeArgument.Value.Type as INamedTypeSymbol;
                var member = enumType?.GetMembers()
                    .OfType<IFieldSymbol>()
                    .FirstOrDefault(f => f.IsConst && f.ConstantValue is int value && value == enumValue);

                if (member != null)
                {
                    return member.Name;
                }
            }

            // Fallback to int value if enum name cannot be determined
            if (modeArgument.Value.Value is int modeValue)
            {
                return modeValue switch
                {
                    0 => "Persistent",
                    1 => "EventDriven",
                    _ => "Persistent"
                };
            }
        }
        return "Persistent";
    }

    /// <summary>
    /// Extracts the messaging strategy from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The messaging strategy.</returns>
    private static string ExtractMessagingStrategy(AttributeData attribute)
    {
        var strategyArgument = GetNamedArgument(attribute, "MessagingStrategy");
        if (strategyArgument.HasValue)
        {
            // Try to get enum member name from Roslyn
            if (strategyArgument.Value.Type?.TypeKind == TypeKind.Enum && strategyArgument.Value.Value is int enumValue)
            {
                var enumType = strategyArgument.Value.Type as INamedTypeSymbol;
                var member = enumType?.GetMembers()
                    .OfType<IFieldSymbol>()
                    .FirstOrDefault(f => f.IsConst && f.ConstantValue is int value && value == enumValue);

                if (member != null)
                {
                    return member.Name;
                }
            }

            // Fallback to int value if enum name cannot be determined
            if (strategyArgument.Value.Value is int strategyValue)
            {
                return strategyValue switch
                {
                    0 => "SharedMemory",
                    1 => "AtomicQueue",
                    2 => "P2P",
                    3 => "NCCL",
                    _ => "SharedMemory"
                };
            }
        }
        return "SharedMemory";
    }

    /// <summary>
    /// Extracts the domain-specific optimization hint from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The domain type for specialized optimizations.</returns>
    private static string ExtractDomain(AttributeData attribute)
    {
        var domainArgument = GetNamedArgument(attribute, "Domain");
        if (domainArgument.HasValue)
        {
            // Try to get enum member name from Roslyn
            if (domainArgument.Value.Type?.TypeKind == TypeKind.Enum && domainArgument.Value.Value is int enumValue)
            {
                var enumType = domainArgument.Value.Type as INamedTypeSymbol;
                var member = enumType?.GetMembers()
                    .OfType<IFieldSymbol>()
                    .FirstOrDefault(f => f.IsConst && f.ConstantValue is int value && value == enumValue);

                if (member != null)
                {
                    return member.Name;
                }
            }

            // Fallback to int value if enum name cannot be determined
            if (domainArgument.Value.Value is int domainValue)
            {
                return domainValue switch
                {
                    0 => "General",
                    1 => "VideoProcessing",
                    2 => "AudioProcessing",
                    3 => "MachineLearning",
                    4 => "ImageProcessing",
                    5 => "SignalProcessing",
                    6 => "ScientificComputing",
                    7 => "Financial",
                    8 => "Cryptography",
                    9 => "DataAnalytics",
                    _ => "General"
                };
            }
        }
        return "General";
    }

    /// <summary>
    /// Extracts the grid dimensions from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>Grid dimensions [x, y, z] or null if not specified.</returns>
    private static int[]? ExtractGridDimensions(AttributeData attribute)
    {
        var gridArgument = GetNamedArgument(attribute, "GridDimensions");
        if (gridArgument.HasValue && gridArgument.Value.Kind == TypedConstantKind.Array)
        {
            var dimensions = gridArgument.Value.Values
                .Where(tc => tc.Value is int)
                .Select(tc => (int)tc.Value!)
                .ToArray();
            return dimensions.Length > 0 ? dimensions : null;
        }
        return null;
    }

    /// <summary>
    /// Extracts the block dimensions from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>Block dimensions [x, y, z] or null if not specified.</returns>
    private static int[]? ExtractBlockDimensions(AttributeData attribute)
    {
        var blockArgument = GetNamedArgument(attribute, "BlockDimensions");
        if (blockArgument.HasValue && blockArgument.Value.Kind == TypedConstantKind.Array)
        {
            var dimensions = blockArgument.Value.Values
                .Where(tc => tc.Value is int)
                .Select(tc => (int)tc.Value!)
                .ToArray();
            return dimensions.Length > 0 ? dimensions : null;
        }
        return null;
    }

    /// <summary>
    /// Extracts the UseSharedMemory flag from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>True if shared memory should be used; otherwise, false.</returns>
    private static bool ExtractUseSharedMemory(AttributeData attribute)
    {
        var sharedMemArgument = GetNamedArgument(attribute, "UseSharedMemory");
        if (sharedMemArgument.HasValue && sharedMemArgument.Value.Value is bool useSharedMemory)
        {
            return useSharedMemory;
        }
        return false;
    }

    /// <summary>
    /// Extracts the shared memory size from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The shared memory size in bytes.</returns>
    private static int ExtractSharedMemorySize(AttributeData attribute)
    {
        var sizeArgument = GetNamedArgument(attribute, "SharedMemorySize");
        if (sizeArgument.HasValue && sizeArgument.Value.Value is int size)
        {
            return size;
        }
        return 0; // Auto-calculate
    }

    /// <summary>
    /// Extracts the list of supported backends from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>A list of supported backend names.</returns>
    private static List<string> ExtractSupportedBackends(AttributeData attribute)
    {
        var backends = new List<string>();

        var backendsArgument = GetNamedArgument(attribute, "Backends");
        if (backendsArgument.HasValue && backendsArgument.Value.Value is int backendsValue)
        {
            // Process backend flags (bitwise)
            if ((backendsValue & 1) != 0) // CPU flag
            {
                backends.Add("CPU");
            }
            if ((backendsValue & 2) != 0) // CUDA flag
            {
                backends.Add("CUDA");
            }
            if ((backendsValue & 4) != 0) // Metal flag
            {
                backends.Add("Metal");
            }
            if ((backendsValue & 8) != 0) // OpenCL flag
            {
                backends.Add("OpenCL");
            }
            if ((backendsValue & 16) != 0) // Vulkan flag
            {
                backends.Add("Vulkan");
            }
            if ((backendsValue & 32) != 0) // ROCm flag
            {
                backends.Add("ROCm");
            }
        }

        // Default to CUDA + OpenCL + Metal if no backends specified
        if (backends.Count == 0)
        {
            backends.AddRange(["CUDA", "OpenCL", "Metal"]);
        }

        return backends;
    }

    /// <summary>
    /// Extracts the parallel execution setting from the Ring Kernel attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>True if parallel execution is enabled; otherwise, false.</returns>
    private static bool ExtractIsParallel(AttributeData attribute)
    {
        var isParallelArgument = GetNamedArgument(attribute, "IsParallel");
        if (isParallelArgument.HasValue && isParallelArgument.Value.Value is bool isParallel)
        {
            return isParallel;
        }
        return true;
    }

    /// <summary>
    /// Extracts the vector size from the Ring Kernel attribute (CPU backend only).
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <returns>The vector size for SIMD operations.</returns>
    private static int ExtractVectorSize(AttributeData attribute)
    {
        var vectorSizeArgument = GetNamedArgument(attribute, "VectorSize");
        if (vectorSizeArgument.HasValue && vectorSizeArgument.Value.Value is int vectorSize)
        {
            return vectorSize;
        }
        return 8; // Default to 256-bit AVX2
    }

    /// <summary>
    /// Validates that the Ring Kernel configuration is coherent and supported.
    /// </summary>
    /// <param name="info">The Ring Kernel info to validate.</param>
    /// <returns>A list of validation errors, or empty if valid.</returns>
    public static IReadOnlyList<string> ValidateConfiguration(RingKernelMethodInfo info)
    {
        var errors = new List<string>();

        // Validate kernel ID is not empty
        if (string.IsNullOrWhiteSpace(info.KernelId))
        {
            errors.Add("Ring Kernel must have a non-empty KernelId.");
        }

        // Validate capacity is power of 2
        if (info.Capacity <= 0 || (info.Capacity & (info.Capacity - 1)) != 0)
        {
            errors.Add($"Ring Kernel capacity must be a power of 2. Got: {info.Capacity}");
        }

        // Validate queue sizes are positive
        if (info.InputQueueSize <= 0)
        {
            errors.Add($"InputQueueSize must be positive. Got: {info.InputQueueSize}");
        }
        if (info.OutputQueueSize <= 0)
        {
            errors.Add($"OutputQueueSize must be positive. Got: {info.OutputQueueSize}");
        }

        // Validate at least one backend is supported
        if (info.Backends.Count == 0)
        {
            errors.Add("At least one backend must be supported.");
        }

        // Validate shared memory configuration
        if (info.UseSharedMemory && info.MessagingStrategy != "SharedMemory")
        {
            errors.Add("UseSharedMemory=true requires MessagingStrategy=SharedMemory.");
        }

        // Validate shared memory size if specified
        if (info.SharedMemorySize < 0)
        {
            errors.Add($"SharedMemorySize cannot be negative. Got: {info.SharedMemorySize}");
        }

        // Validate grid/block dimensions if specified
        if (info.GridDimensions != null && info.GridDimensions.Any(d => d <= 0))
        {
            errors.Add("All GridDimensions must be positive.");
        }
        if (info.BlockDimensions != null && info.BlockDimensions.Any(d => d <= 0))
        {
            errors.Add("All BlockDimensions must be positive.");
        }

        return errors;
    }

    /// <summary>
    /// Gets a named argument from an attribute.
    /// </summary>
    /// <param name="attribute">The attribute to examine.</param>
    /// <param name="argumentName">The name of the argument to find.</param>
    /// <returns>The typed constant value if found; otherwise, null.</returns>
    private static TypedConstant? GetNamedArgument(AttributeData attribute, string argumentName)
    {
        var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == argumentName);
        return namedArg.Value;
    }
}
