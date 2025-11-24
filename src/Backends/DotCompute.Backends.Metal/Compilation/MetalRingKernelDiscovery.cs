// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Compilation;

/// <summary>
/// Discovers Ring Kernel methods via runtime reflection and builds a registry for Metal compilation.
/// </summary>
/// <remarks>
/// <para>
/// This component provides runtime discovery of methods marked with [RingKernel] attribute
/// that are compatible with the Metal backend. Unlike the source generator's compile-time
/// analysis, this performs runtime reflection to enable dynamic kernel loading and compilation.
/// </para>
/// <para>
/// Discovery process:
/// <list type="number">
/// <item><description>Scans loaded assemblies for [RingKernel] methods</description></item>
/// <item><description>Filters for Metal-compatible kernels (checks Backends flag)</description></item>
/// <item><description>Validates method signatures (static, void return, public accessibility)</description></item>
/// <item><description>Extracts attribute configuration (queue sizes, messaging strategy, etc.)</description></item>
/// <item><description>Analyzes parameter types for Metal/MSL compatibility</description></item>
/// <item><description>Builds metadata registry for compilation pipeline</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class MetalRingKernelDiscovery
{
    private readonly ILogger<MetalRingKernelDiscovery> _logger;
    private readonly ConcurrentDictionary<string, DiscoveredMetalRingKernel> _kernelRegistry = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalRingKernelDiscovery"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic messages.</param>
    public MetalRingKernelDiscovery(ILogger<MetalRingKernelDiscovery> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discovers all Ring Kernel methods compatible with Metal in the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan. If null or empty, scans all loaded assemblies.</param>
    /// <returns>A collection of discovered Ring Kernels compatible with Metal.</returns>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    public IReadOnlyCollection<DiscoveredMetalRingKernel> DiscoverKernels(IEnumerable<Assembly>? assemblies = null)
    {
        var assembliesToScan = assemblies?.ToArray() ?? AppDomain.CurrentDomain.GetAssemblies();

        _logger.LogInformation("Beginning Metal Ring Kernel discovery across {AssemblyCount} assemblies", assembliesToScan.Length);

        var discovered = new List<DiscoveredMetalRingKernel>();

        foreach (var assembly in assembliesToScan)
        {
            try
            {
                var kernels = DiscoverKernelsInAssembly(assembly);
                discovered.AddRange(kernels);
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to load types from assembly {AssemblyName}. Loader exceptions: {LoaderExceptions}",
                    assembly.FullName,
                    string.Join(", ", ex.LoaderExceptions.Select(e => e?.Message ?? "Unknown")));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover kernels in assembly {AssemblyName}", assembly.FullName);
            }
        }

        _logger.LogInformation("Metal Ring Kernel discovery completed. Found {KernelCount} kernels", discovered.Count);

        return discovered.AsReadOnly();
    }

    /// <summary>
    /// Discovers a specific Ring Kernel by its kernel ID.
    /// </summary>
    /// <param name="kernelId">The unique kernel ID to find.</param>
    /// <param name="assemblies">Optional assemblies to scan. If null, scans all loaded assemblies.</param>
    /// <returns>The discovered kernel, or null if not found.</returns>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    public DiscoveredMetalRingKernel? DiscoverKernelById(string kernelId, IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        // Check registry first
        if (_kernelRegistry.TryGetValue(kernelId, out var cachedKernel))
        {
            _logger.LogDebug("Kernel {KernelId} found in registry cache", kernelId);
            return cachedKernel;
        }

        // Discover all kernels and find the matching one
        var kernels = DiscoverKernels(assemblies);
        var kernel = kernels.FirstOrDefault(k => k.KernelId == kernelId);

        if (kernel != null)
        {
            _logger.LogInformation("Discovered kernel {KernelId}: {MethodName}", kernelId, kernel.Method.Name);
        }
        else
        {
            _logger.LogWarning("Kernel {KernelId} not found in any assembly", kernelId);
        }

        return kernel;
    }

    /// <summary>
    /// Gets a discovered kernel from the registry.
    /// </summary>
    /// <param name="kernelId">The kernel ID.</param>
    /// <returns>The discovered kernel, or null if not in registry.</returns>
    public DiscoveredMetalRingKernel? GetKernelFromRegistry(string kernelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        return _kernelRegistry.TryGetValue(kernelId, out var kernel) ? kernel : null;
    }

    /// <summary>
    /// Gets all kernels currently in the registry.
    /// </summary>
    /// <returns>A read-only collection of all registered kernels.</returns>
    public IReadOnlyCollection<DiscoveredMetalRingKernel> GetAllRegisteredKernels()
    {
        return _kernelRegistry.Values.ToArray();
    }

    /// <summary>
    /// Clears the kernel registry cache.
    /// </summary>
    public void ClearRegistry()
    {
        var count = _kernelRegistry.Count;
        _kernelRegistry.Clear();
        _logger.LogInformation("Cleared kernel registry ({KernelCount} kernels removed)", count);
    }

    /// <summary>
    /// Discovers Ring Kernels in a specific assembly.
    /// </summary>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    private List<DiscoveredMetalRingKernel> DiscoverKernelsInAssembly(Assembly assembly)
    {
        var kernels = new List<DiscoveredMetalRingKernel>();

        _logger.LogDebug("Scanning assembly {AssemblyName} for Metal Ring Kernels", assembly.GetName().Name);

        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var ringKernelAttr = method.GetCustomAttribute<RingKernelAttribute>();
                if (ringKernelAttr == null)
                {
                    continue;
                }

                // Check if kernel supports Metal backend
                if (!IsMetalCompatible(ringKernelAttr))
                {
                    _logger.LogDebug(
                        "Kernel {TypeName}.{MethodName} does not support Metal backend (Backends: {Backends})",
                        type.Name, method.Name, ringKernelAttr.Backends);
                    continue;
                }

                try
                {
                    var kernel = CreateDiscoveredKernel(method, ringKernelAttr);
                    if (kernel != null)
                    {
                        kernels.Add(kernel);

                        // Add to registry
                        if (_kernelRegistry.TryAdd(kernel.KernelId, kernel))
                        {
                            _logger.LogDebug("Registered Metal kernel {KernelId}: {TypeName}.{MethodName}",
                                kernel.KernelId, type.Name, method.Name);
                        }
                        else
                        {
                            _logger.LogWarning("Duplicate kernel ID {KernelId} detected. Method: {TypeName}.{MethodName}",
                                kernel.KernelId, type.Name, method.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze Ring Kernel method {TypeName}.{MethodName}",
                        type.Name, method.Name);
                }
            }
        }

        return kernels;
    }

    /// <summary>
    /// Checks if the kernel attribute indicates Metal backend compatibility.
    /// </summary>
    private static bool IsMetalCompatible(RingKernelAttribute attribute)
    {
        // If Backends is not specified or is All, it's compatible
        if (attribute.Backends == KernelBackends.All ||
            attribute.Backends == default)
        {
            return true;
        }

        // Check for explicit Metal flag
        return (attribute.Backends & KernelBackends.Metal) != 0;
    }

    /// <summary>
    /// Creates a discovered kernel from a method and its [RingKernel] attribute.
    /// </summary>
    private DiscoveredMetalRingKernel? CreateDiscoveredKernel(MethodInfo method, RingKernelAttribute attribute)
    {
        // Validate method signature
        if (!ValidateRingKernelMethod(method))
        {
            return null;
        }

        // Extract kernel ID (use attribute value or generate from method name)
        var kernelId = !string.IsNullOrWhiteSpace(attribute.KernelId)
            ? attribute.KernelId
            : GenerateDefaultKernelId(method);

        // Extract parameters
        var parameters = AnalyzeParameters(method);

        // Apply MessageQueueSize override if specified
        var inputQueueSize = attribute.MessageQueueSize > 0 ? attribute.MessageQueueSize : attribute.InputQueueSize;
        var outputQueueSize = attribute.MessageQueueSize > 0 ? attribute.MessageQueueSize : attribute.OutputQueueSize;

        // Detect inline handler (unified kernel API with RingKernelContext parameter)
        var hasInlineHandler = DetectInlineHandler(method);
        var inputMessageType = ExtractInputMessageType(parameters);

        // Create discovered kernel metadata
        return new DiscoveredMetalRingKernel
        {
            KernelId = kernelId,
            Method = method,
            Attribute = attribute,
            Parameters = parameters,
            ContainingType = method.DeclaringType!,
            Namespace = method.DeclaringType!.Namespace ?? string.Empty,
            // Attribute properties
            Capacity = attribute.Capacity,
            InputQueueSize = inputQueueSize,
            OutputQueueSize = outputQueueSize,
            MaxInputMessageSizeBytes = attribute.MaxInputMessageSizeBytes,
            MaxOutputMessageSizeBytes = attribute.MaxOutputMessageSizeBytes,
            Mode = attribute.Mode,
            MessagingStrategy = attribute.MessagingStrategy,
            Domain = attribute.Domain,
            // Barrier and synchronization configuration
            UseBarriers = attribute.UseBarriers,
            BarrierScope = attribute.BarrierScope,
            BarrierCapacity = attribute.BarrierCapacity,
            MemoryConsistency = attribute.MemoryConsistency,
            EnableCausalOrdering = attribute.EnableCausalOrdering,
            // Integration properties
            EnableTimestamps = attribute.EnableTimestamps,
            MessageQueueSize = attribute.MessageQueueSize,
            ProcessingMode = attribute.ProcessingMode,
            MaxMessagesPerIteration = attribute.MaxMessagesPerIteration,
            // Unified kernel API
            HasInlineHandler = hasInlineHandler,
            InputMessageTypeName = inputMessageType,
            // K2K messaging
            SubscribesToKernels = attribute.SubscribesToKernels ?? Array.Empty<string>(),
            PublishesToKernels = attribute.PublishesToKernels ?? Array.Empty<string>(),
            UsesK2KMessaging = (attribute.SubscribesToKernels?.Length > 0) || (attribute.PublishesToKernels?.Length > 0)
        };
    }

    /// <summary>
    /// Detects if the method uses the unified kernel API (RingKernelContext parameter).
    /// </summary>
    private static bool DetectInlineHandler(MethodInfo method)
    {
        foreach (var param in method.GetParameters())
        {
            if (param.ParameterType.Name == "RingKernelContext" ||
                param.ParameterType.FullName?.Contains("RingKernelContext", StringComparison.Ordinal) == true)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Extracts the input message type name from parameters.
    /// </summary>
    private static string? ExtractInputMessageType(List<MetalKernelParameterMetadata> parameters)
    {
        foreach (var param in parameters)
        {
            if (param.ParameterType.Name != "RingKernelContext" &&
                param.ParameterType.FullName?.Contains("RingKernelContext", StringComparison.Ordinal) != true)
            {
                return param.ParameterType.FullName ?? param.ParameterType.Name;
            }
        }
        return null;
    }

    /// <summary>
    /// Validates that a method meets Ring Kernel requirements.
    /// </summary>
    private bool ValidateRingKernelMethod(MethodInfo method)
    {
        if (!method.IsStatic)
        {
            _logger.LogWarning("Ring Kernel method {MethodName} must be static", method.Name);
            return false;
        }

        if (method.ReturnType != typeof(void))
        {
            _logger.LogWarning("Ring Kernel method {MethodName} must return void, not {ReturnType}",
                method.Name, method.ReturnType.Name);
            return false;
        }

        if (!method.IsPublic)
        {
            _logger.LogWarning("Ring Kernel method {MethodName} must be public", method.Name);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Analyzes method parameters and extracts type information.
    /// </summary>
    private static List<MetalKernelParameterMetadata> AnalyzeParameters(MethodInfo method)
    {
        var parameters = new List<MetalKernelParameterMetadata>();

        foreach (var param in method.GetParameters())
        {
            var paramType = param.ParameterType;
            var isBuffer = IsBufferType(paramType);
            var elementType = isBuffer ? GetBufferElementType(paramType) : paramType;

            parameters.Add(new MetalKernelParameterMetadata
            {
                Name = param.Name ?? "param",
                ParameterType = paramType,
                ElementType = elementType,
                IsBuffer = isBuffer,
                IsReadOnly = IsReadOnlyBuffer(paramType)
            });
        }

        return parameters;
    }

    /// <summary>
    /// Determines if a type is a buffer type.
    /// </summary>
    private static bool IsBufferType(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDef = type.GetGenericTypeDefinition();
        return genericTypeDef.Name is "Span`1" or "ReadOnlySpan`1" or "Memory`1" or "ReadOnlyMemory`1";
    }

    /// <summary>
    /// Determines if a buffer type is read-only.
    /// </summary>
    private static bool IsReadOnlyBuffer(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDef = type.GetGenericTypeDefinition();
        return genericTypeDef.Name is "ReadOnlySpan`1" or "ReadOnlyMemory`1";
    }

    /// <summary>
    /// Gets the element type of a buffer type.
    /// </summary>
    private static Type GetBufferElementType(Type bufferType)
    {
        if (bufferType.IsArray)
        {
            return bufferType.GetElementType()!;
        }

        if (bufferType.IsGenericType)
        {
            return bufferType.GetGenericArguments()[0];
        }

        return bufferType;
    }

    /// <summary>
    /// Generates a default kernel ID from the method name.
    /// </summary>
    private static string GenerateDefaultKernelId(MethodInfo method)
    {
        var typeName = method.DeclaringType?.Name ?? "Unknown";
        return $"{typeName}_{method.Name}";
    }
}

/// <summary>
/// Represents a discovered Ring Kernel with Metal-specific metadata.
/// </summary>
public sealed class DiscoveredMetalRingKernel
{
    public required string KernelId { get; init; }
    public required MethodInfo Method { get; init; }
    public required RingKernelAttribute Attribute { get; init; }
    public required IReadOnlyList<MetalKernelParameterMetadata> Parameters { get; init; }
    public required Type ContainingType { get; init; }
    public required string Namespace { get; init; }

    // Ring Kernel configuration
    public int Capacity { get; init; }
    public int InputQueueSize { get; init; }
    public int OutputQueueSize { get; init; }
    public int MaxInputMessageSizeBytes { get; init; }
    public int MaxOutputMessageSizeBytes { get; init; }
    public RingKernelMode Mode { get; init; }
    public MessagePassingStrategy MessagingStrategy { get; init; }
    public RingKernelDomain Domain { get; init; }

    // Barrier configuration
    public bool UseBarriers { get; init; }
    public Abstractions.Barriers.BarrierScope BarrierScope { get; init; }
    public int BarrierCapacity { get; init; }
    public Abstractions.Memory.MemoryConsistencyModel MemoryConsistency { get; init; }
    public bool EnableCausalOrdering { get; init; }

    // Integration properties
    public bool EnableTimestamps { get; init; }
    public int MessageQueueSize { get; init; }
    public RingProcessingMode ProcessingMode { get; init; }
    public int MaxMessagesPerIteration { get; init; }

    // K2K and advanced features
    public bool UsesK2KMessaging { get; init; }
    public bool HasInlineHandler { get; init; }
    public string? InputMessageTypeName { get; init; }
    public string? InlineHandlerMslCode { get; set; }
    public IReadOnlyList<string> SubscribesToKernels { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PublishesToKernels { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents metadata about a Metal kernel parameter.
/// </summary>
public sealed class MetalKernelParameterMetadata
{
    public required string Name { get; init; }
    public required Type ParameterType { get; init; }
    public required Type ElementType { get; init; }
    public bool IsBuffer { get; init; }
    public bool IsReadOnly { get; init; }
}
