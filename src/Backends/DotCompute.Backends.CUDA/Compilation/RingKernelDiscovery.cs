// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1002 // Using List<T> for internal implementation is acceptable here

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Discovers Ring Kernel methods via runtime reflection and builds a registry for CUDA compilation.
/// </summary>
/// <remarks>
/// <para>
/// This component provides runtime discovery of methods marked with [RingKernel] attribute,
/// which is required for the Ring Kernel compilation pipeline. Unlike the source generator's
/// compile-time analysis, this performs runtime reflection to enable dynamic kernel loading
/// and compilation.
/// </para>
/// <para>
/// Discovery process:
/// <list type="number">
/// <item><description>Scans loaded assemblies for [RingKernel] methods</description></item>
/// <item><description>Validates method signatures (static, void return, public accessibility)</description></item>
/// <item><description>Extracts attribute configuration (queue sizes, messaging strategy, etc.)</description></item>
/// <item><description>Analyzes parameter types for CUDA compatibility</description></item>
/// <item><description>Builds metadata registry for compilation pipeline</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class RingKernelDiscovery
{
    private readonly ILogger<RingKernelDiscovery> _logger;
    private readonly ConcurrentDictionary<string, DiscoveredRingKernel> _kernelRegistry = new();

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, int, Exception?> s_logDiscoveryStarted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(DiscoverKernels)),
            "Beginning Ring Kernel discovery across {AssemblyCount} assemblies");

    private static readonly Action<ILogger, string?, string, Exception> s_logAssemblyLoadError =
        LoggerMessage.Define<string?, string>(
            LogLevel.Warning,
            new EventId(2, nameof(DiscoverKernels)),
            "Failed to load types from assembly {AssemblyName}. Loader exceptions: {LoaderExceptions}");

    private static readonly Action<ILogger, string?, Exception> s_logAssemblyDiscoveryError =
        LoggerMessage.Define<string?>(
            LogLevel.Warning,
            new EventId(3, nameof(DiscoverKernels)),
            "Failed to discover kernels in assembly {AssemblyName}");

    private static readonly Action<ILogger, int, Exception?> s_logDiscoveryCompleted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4, nameof(DiscoverKernels)),
            "Ring Kernel discovery completed. Found {KernelCount} kernels");

    private static readonly Action<ILogger, string, Exception?> s_logKernelFoundInCache =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(5, nameof(DiscoverKernelById)),
            "Kernel {KernelId} found in registry cache");

    private static readonly Action<ILogger, string, string, Exception?> s_logKernelDiscovered =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(6, nameof(DiscoverKernelById)),
            "Discovered kernel {KernelId}: {MethodName}");

    private static readonly Action<ILogger, string, Exception?> s_logKernelNotFound =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(7, nameof(DiscoverKernelById)),
            "Kernel {KernelId} not found in any assembly");

    private static readonly Action<ILogger, int, Exception?> s_logRegistryCleared =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(8, nameof(ClearRegistry)),
            "Cleared kernel registry ({KernelCount} kernels removed)");

    private static readonly Action<ILogger, string?, Exception?> s_logScanningAssembly =
        LoggerMessage.Define<string?>(
            LogLevel.Debug,
            new EventId(9, nameof(DiscoverKernelsInAssembly)),
            "Scanning assembly {AssemblyName} for Ring Kernels");

    private static readonly Action<ILogger, string, string, string, Exception?> s_logKernelRegistered =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Debug,
            new EventId(10, nameof(DiscoverKernelsInAssembly)),
            "Registered kernel {KernelId}: {TypeName}.{MethodName}");

    private static readonly Action<ILogger, string, string, string, Exception?> s_logDuplicateKernelId =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(11, nameof(DiscoverKernelsInAssembly)),
            "Duplicate kernel ID {KernelId} detected. Method: {TypeName}.{MethodName}");

    private static readonly Action<ILogger, string, string, Exception> s_logMethodAnalysisError =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(12, nameof(DiscoverKernelsInAssembly)),
            "Failed to analyze Ring Kernel method {TypeName}.{MethodName}");

    private static readonly Action<ILogger, string, Exception?> s_logMethodNotStatic =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(13, nameof(ValidateRingKernelMethod)),
            "Ring Kernel method {MethodName} must be static");

    private static readonly Action<ILogger, string, string, Exception?> s_logMethodNotVoid =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(14, nameof(ValidateRingKernelMethod)),
            "Ring Kernel method {MethodName} must return void, not {ReturnType}");

    private static readonly Action<ILogger, string, Exception?> s_logMethodNotPublic =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(15, nameof(ValidateRingKernelMethod)),
            "Ring Kernel method {MethodName} must be public");

    /// <summary>
    /// Initializes a new instance of the <see cref="RingKernelDiscovery"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic messages.</param>
    public RingKernelDiscovery(ILogger<RingKernelDiscovery> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discovers all Ring Kernel methods in the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan. If null or empty, scans all loaded assemblies.</param>
    /// <returns>A collection of discovered Ring Kernels.</returns>
    /// <remarks>
    /// This method scans all types in the provided assemblies looking for methods marked with
    /// [RingKernel] attribute. Discovered kernels are validated and added to the registry.
    /// </remarks>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    public IReadOnlyCollection<DiscoveredRingKernel> DiscoverKernels(IEnumerable<Assembly>? assemblies = null)
    {
        var assembliesToScan = assemblies?.ToArray() ?? AppDomain.CurrentDomain.GetAssemblies();

        s_logDiscoveryStarted(_logger, assembliesToScan.Length, null);

        var discovered = new List<DiscoveredRingKernel>();

        foreach (var assembly in assembliesToScan)
        {
            try
            {
                var kernels = DiscoverKernelsInAssembly(assembly);
                discovered.AddRange(kernels);
            }
            catch (ReflectionTypeLoadException ex)
            {
                s_logAssemblyLoadError(
                    _logger,
                    assembly.FullName,
                    string.Join(", ", ex.LoaderExceptions.Select(e => e?.Message ?? "Unknown")),
                    ex);
            }
            catch (Exception ex)
            {
                s_logAssemblyDiscoveryError(_logger, assembly.FullName, ex);
            }
        }

        s_logDiscoveryCompleted(_logger, discovered.Count, null);

        return discovered.AsReadOnly();
    }

    /// <summary>
    /// Discovers a specific Ring Kernel by its kernel ID.
    /// </summary>
    /// <param name="kernelId">The unique kernel ID to find.</param>
    /// <param name="assemblies">Optional assemblies to scan. If null, scans all loaded assemblies.</param>
    /// <returns>The discovered kernel, or null if not found.</returns>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    public DiscoveredRingKernel? DiscoverKernelById(string kernelId, IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        // Check registry first
        if (_kernelRegistry.TryGetValue(kernelId, out var cachedKernel))
        {
            s_logKernelFoundInCache(_logger, kernelId, null);
            return cachedKernel;
        }

        // Discover all kernels and find the matching one
        var kernels = DiscoverKernels(assemblies);
        var kernel = kernels.FirstOrDefault(k => k.KernelId == kernelId);

        if (kernel != null)
        {
            s_logKernelDiscovered(_logger, kernelId, kernel.Method.Name, null);
        }
        else
        {
            s_logKernelNotFound(_logger, kernelId, null);
        }

        return kernel;
    }

    /// <summary>
    /// Gets a discovered kernel from the registry.
    /// </summary>
    /// <param name="kernelId">The kernel ID.</param>
    /// <returns>The discovered kernel, or null if not in registry.</returns>
    public DiscoveredRingKernel? GetKernelFromRegistry(string kernelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        return _kernelRegistry.TryGetValue(kernelId, out var kernel) ? kernel : null;
    }

    /// <summary>
    /// Gets all kernels currently in the registry.
    /// </summary>
    /// <returns>A read-only collection of all registered kernels.</returns>
    public IReadOnlyCollection<DiscoveredRingKernel> GetAllRegisteredKernels()
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
        s_logRegistryCleared(_logger, count, null);
    }

    /// <summary>
    /// Discovers Ring Kernels in a specific assembly.
    /// </summary>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    private List<DiscoveredRingKernel> DiscoverKernelsInAssembly(Assembly assembly)
    {
        var kernels = new List<DiscoveredRingKernel>();

        s_logScanningAssembly(_logger, assembly.GetName().Name, null);

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

                try
                {
                    var kernel = CreateDiscoveredKernel(method, ringKernelAttr);
                    if (kernel != null)
                    {
                        kernels.Add(kernel);

                        // Add to registry
                        if (_kernelRegistry.TryAdd(kernel.KernelId, kernel))
                        {
                            s_logKernelRegistered(_logger, kernel.KernelId, type.Name, method.Name, null);
                        }
                        else
                        {
                            s_logDuplicateKernelId(_logger, kernel.KernelId, type.Name, method.Name, null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    s_logMethodAnalysisError(_logger, type.Name, method.Name, ex);
                }
            }
        }

        return kernels;
    }

    /// <summary>
    /// Creates a discovered kernel from a method and its [RingKernel] attribute.
    /// </summary>
    private DiscoveredRingKernel? CreateDiscoveredKernel(MethodInfo method, RingKernelAttribute attribute)
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

        // Create discovered kernel metadata
        return new DiscoveredRingKernel
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
            Backends = attribute.Backends,
            // Barrier and synchronization configuration
            UseBarriers = attribute.UseBarriers,
            BarrierScope = attribute.BarrierScope,
            BarrierCapacity = attribute.BarrierCapacity,
            MemoryConsistency = attribute.MemoryConsistency,
            EnableCausalOrdering = attribute.EnableCausalOrdering,
            // Orleans.GpuBridge.Core integration properties
            EnableTimestamps = attribute.EnableTimestamps,
            MessageQueueSize = attribute.MessageQueueSize,
            ProcessingMode = attribute.ProcessingMode,
            MaxMessagesPerIteration = attribute.MaxMessagesPerIteration
        };
    }

    /// <summary>
    /// Validates that a method meets Ring Kernel requirements.
    /// </summary>
    private bool ValidateRingKernelMethod(MethodInfo method)
    {
        // Must be static
        if (!method.IsStatic)
        {
            s_logMethodNotStatic(_logger, method.Name, null);
            return false;
        }

        // Must return void
        if (method.ReturnType != typeof(void))
        {
            s_logMethodNotVoid(_logger, method.Name, method.ReturnType.Name, null);
            return false;
        }

        // Must be public
        if (!method.IsPublic)
        {
            s_logMethodNotPublic(_logger, method.Name, null);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Analyzes method parameters and extracts type information.
    /// </summary>
    private static List<KernelParameterMetadata> AnalyzeParameters(MethodInfo method)
    {
        var parameters = new List<KernelParameterMetadata>();

        foreach (var param in method.GetParameters())
        {
            var paramType = param.ParameterType;
            var isBuffer = IsBufferType(paramType);
            var elementType = isBuffer ? GetBufferElementType(paramType) : paramType;

            parameters.Add(new KernelParameterMetadata
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
    /// Determines if a type is a buffer type (Span, ReadOnlySpan, array, etc.).
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
/// Represents a discovered Ring Kernel with its metadata.
/// </summary>
public sealed class DiscoveredRingKernel
{
    /// <summary>
    /// Gets or sets the unique kernel identifier.
    /// </summary>
    public required string KernelId { get; init; }

    /// <summary>
    /// Gets or sets the reflected method info.
    /// </summary>
    public required MethodInfo Method { get; init; }

    /// <summary>
    /// Gets or sets the [RingKernel] attribute instance.
    /// </summary>
    public required RingKernelAttribute Attribute { get; init; }

    /// <summary>
    /// Gets or sets the method parameters metadata.
    /// </summary>
    public required List<KernelParameterMetadata> Parameters { get; init; }

    /// <summary>
    /// Gets or sets the type containing the kernel method.
    /// </summary>
    public required Type ContainingType { get; init; }

    /// <summary>
    /// Gets or sets the namespace containing the kernel.
    /// </summary>
    public required string Namespace { get; init; }

    // Ring Kernel configuration (from attribute)

    /// <summary>
    /// Gets or sets the ring buffer capacity.
    /// </summary>
    public int Capacity { get; init; }

    /// <summary>
    /// Gets or sets the input queue size.
    /// </summary>
    public int InputQueueSize { get; init; }

    /// <summary>
    /// Gets or sets the output queue size.
    /// </summary>
    public int OutputQueueSize { get; init; }

    /// <summary>
    /// Gets or sets the maximum input message size in bytes.
    /// </summary>
    public int MaxInputMessageSizeBytes { get; init; }

    /// <summary>
    /// Gets or sets the maximum output message size in bytes.
    /// </summary>
    public int MaxOutputMessageSizeBytes { get; init; }

    /// <summary>
    /// Gets or sets the execution mode (Persistent or EventDriven).
    /// </summary>
    public Abstractions.RingKernels.RingKernelMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the message passing strategy.
    /// </summary>
    public Abstractions.RingKernels.MessagePassingStrategy MessagingStrategy { get; init; }

    /// <summary>
    /// Gets or sets the domain-specific optimization hint.
    /// </summary>
    public Abstractions.RingKernels.RingKernelDomain Domain { get; init; }

    /// <summary>
    /// Gets or sets the supported backend flags.
    /// </summary>
    public KernelBackends Backends { get; init; }

    // Barrier and synchronization configuration

    /// <summary>
    /// Gets or sets whether this ring kernel uses GPU thread barriers for synchronization.
    /// </summary>
    public bool UseBarriers { get; init; }

    /// <summary>
    /// Gets or sets the synchronization scope for barriers.
    /// </summary>
    public Abstractions.Barriers.BarrierScope BarrierScope { get; init; }

    /// <summary>
    /// Gets or sets the expected number of threads participating in barrier synchronization.
    /// </summary>
    public int BarrierCapacity { get; init; }

    /// <summary>
    /// Gets or sets the memory consistency model for this ring kernel's memory operations.
    /// </summary>
    public Abstractions.Memory.MemoryConsistencyModel MemoryConsistency { get; init; }

    /// <summary>
    /// Gets or sets whether to enable causal memory ordering (release-acquire semantics).
    /// </summary>
    public bool EnableCausalOrdering { get; init; }

    // Orleans.GpuBridge.Core integration properties

    /// <summary>
    /// Gets or sets whether to enable GPU hardware timestamp tracking for temporal consistency.
    /// </summary>
    public bool EnableTimestamps { get; init; }

    /// <summary>
    /// Gets or sets a unified message queue size that overrides both InputQueueSize and OutputQueueSize.
    /// </summary>
    public int MessageQueueSize { get; init; }

    /// <summary>
    /// Gets or sets how the ring kernel processes messages from its input queue.
    /// </summary>
    public Abstractions.RingKernels.RingProcessingMode ProcessingMode { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of messages processed per dispatch loop iteration.
    /// </summary>
    public int MaxMessagesPerIteration { get; init; }
}

/// <summary>
/// Represents metadata about a kernel parameter.
/// </summary>
public sealed class KernelParameterMetadata
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the parameter type.
    /// </summary>
    public required Type ParameterType { get; init; }

    /// <summary>
    /// Gets or sets the element type (for buffer types).
    /// </summary>
    public required Type ElementType { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a buffer parameter (Span, array, etc.).
    /// </summary>
    public bool IsBuffer { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a read-only buffer.
    /// </summary>
    public bool IsReadOnly { get; init; }
}
