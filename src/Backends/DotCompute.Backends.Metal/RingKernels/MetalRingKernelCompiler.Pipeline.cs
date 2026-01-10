// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using DotCompute.Backends.Metal.Compilation;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// Compiles [RingKernel] C# methods to Metal compute pipelines using a 6-stage pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This partial class implements a 6-stage compilation pipeline:
/// <list type="number">
/// <item><description><b>Stage 1 (Discovery)</b>: Find [RingKernel] methods via reflection</description></item>
/// <item><description><b>Stage 2 (Analysis)</b>: Extract method signature and metadata</description></item>
/// <item><description><b>Stage 3 (MSL Generation)</b>: Generate Metal Shading Language kernel code</description></item>
/// <item><description><b>Stage 4 (Library Compilation)</b>: Compile MSL to Metal library</description></item>
/// <item><description><b>Stage 5 (Pipeline State)</b>: Create compute pipeline state</description></item>
/// <item><description><b>Stage 6 (Verification)</b>: Verify pipeline state and function retrieval</description></item>
/// </list>
/// </para>
/// <para>
/// The compiler integrates with components from Phase 2:
/// - <see cref="MetalRingKernelDiscovery"/> for kernel discovery
/// - <see cref="MetalRingKernelStubGenerator"/> for MSL code generation
/// - <see cref="MetalNative"/> for library compilation and pipeline creation
/// </para>
/// </remarks>
public sealed partial class MetalRingKernelCompiler
{
    // Fields for the 6-stage compilation pipeline
    private MetalRingKernelDiscovery? _kernelDiscovery;
    private MetalRingKernelStubGenerator? _stubGenerator;
    private MetalHandlerTranslationService? _handlerTranslator;
    private MetalMemoryPackSerializerGenerator? _serializerGenerator;
    private readonly ConcurrentDictionary<string, MetalCompiledRingKernel> _compiledKernels = new();

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, string, Exception?> _sLogCompilationStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "CompilationStarted"),
            "Starting Metal Ring Kernel compilation for '{KernelId}'");

    private static readonly Action<ILogger, string, int, Exception?> _sLogStageCompleted =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(2, "StageCompleted"),
            "Compilation stage {Stage} completed for kernel '{KernelId}'");

    private static readonly Action<ILogger, string, int, Exception?> _sLogCompilationCompleted =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(3, "CompilationCompleted"),
            "Ring Kernel '{KernelId}' compiled successfully: MSL={MslSize} bytes");

    private static readonly Action<ILogger, string, string, Exception> _sLogCompilationError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(4, "CompilationError"),
            "Failed to compile Ring Kernel '{KernelId}': {ErrorMessage}");

    private static readonly Action<ILogger, string, Exception?> _sLogCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(5, "CacheHit"),
            "Using cached compiled kernel for '{KernelId}'");

    /// <summary>
    /// Compilation stages for Ring Kernel Metal pipeline generation.
    /// </summary>
    public enum CompilationStage
    {
        /// <summary>
        /// No stage (default value).
        /// </summary>
        None = 0,

        /// <summary>
        /// Stage 1: Find [RingKernel] method via reflection.
        /// </summary>
        Discovery = 1,

        /// <summary>
        /// Stage 2: Extract method signature, parameters, and types.
        /// </summary>
        Analysis = 2,

        /// <summary>
        /// Stage 3: Generate Metal Shading Language kernel code.
        /// </summary>
        MslGeneration = 3,

        /// <summary>
        /// Stage 4: Compile MSL to Metal library.
        /// </summary>
        LibraryCompilation = 4,

        /// <summary>
        /// Stage 5: Create compute pipeline state.
        /// </summary>
        PipelineState = 5,

        /// <summary>
        /// Stage 6: Verify pipeline state and function retrieval.
        /// </summary>
        Verification = 6
    }

    /// <summary>
    /// Initializes the 6-stage compilation pipeline components.
    /// </summary>
    /// <param name="discoveryLogger">Logger for kernel discovery.</param>
    /// <param name="stubGeneratorLogger">Logger for stub generator.</param>
    public void InitializePipeline(
        ILogger<MetalRingKernelDiscovery> discoveryLogger,
        ILogger<MetalRingKernelStubGenerator> stubGeneratorLogger)
    {
        _kernelDiscovery = new MetalRingKernelDiscovery(discoveryLogger);
        _stubGenerator = new MetalRingKernelStubGenerator(stubGeneratorLogger);
        _logger.LogInformation("Metal Ring Kernel 6-stage compilation pipeline initialized");
    }

    /// <summary>
    /// Compiles a Ring Kernel to an executable Metal compute pipeline using the 6-stage pipeline.
    /// </summary>
    /// <param name="kernelId">The unique kernel identifier.</param>
    /// <param name="metalDevice">The Metal device to compile for.</param>
    /// <param name="assemblies">Assemblies to search for the kernel (optional, scans all if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compiled kernel with Metal library and pipeline state.</returns>
    /// <exception cref="ArgumentException">Thrown when kernelId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails at any stage.</exception>
    [RequiresUnreferencedCode("Ring Kernel compilation uses runtime reflection which is not compatible with trimming.")]
    public async Task<MetalCompiledRingKernel> CompileRingKernelAsync(
        string kernelId,
        IntPtr metalDevice,
        IEnumerable<Assembly>? assemblies = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        // Check cancellation early
        cancellationToken.ThrowIfCancellationRequested();

        if (metalDevice == IntPtr.Zero)
        {
            throw new InvalidOperationException("Metal device cannot be zero");
        }

        // Ensure pipeline is initialized
        EnsurePipelineInitialized();

        // Check cache first
        if (_compiledKernels.TryGetValue(kernelId, out var cachedKernel))
        {
            _sLogCacheHit(_logger, kernelId, null);
            return cachedKernel;
        }

        _sLogCompilationStarted(_logger, kernelId, null);

        try
        {
            // Stage 1: Discovery - Find [RingKernel] method
            var discoveredKernel = await DiscoverKernelAsync(kernelId, assemblies, cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.Discovery, null);

            // Stage 2: Analysis - Metadata extraction (already done by discovery)
            ValidateKernelMetadata(discoveredKernel);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.Analysis, null);

            // Stage 2.5: Handler Translation - Attempt to translate inline handler if available
            TryTranslateInlineHandler(discoveredKernel, assemblies);

            // Stage 3: MSL Generation - Generate Metal Shading Language kernel code
            var mslSource = GenerateMslSource(discoveredKernel);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.MslGeneration, null);

            // Stage 4: Library Compilation - Compile MSL to Metal library
            var libraryHandle = await CompileToLibraryAsync(mslSource, kernelId, metalDevice, cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.LibraryCompilation, null);

            // Stage 5: Pipeline State - Create compute pipeline state
            var (functionHandle, pipelineState) = await CreatePipelineStateAsync(
                libraryHandle,
                kernelId,
                metalDevice,
                cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.PipelineState, null);

            // Stage 6: Verification - Verify pipeline state
            VerifyPipelineState(pipelineState, kernelId);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.Verification, null);

            // Create compiled kernel
            var compiledKernel = new MetalCompiledRingKernel(
                discoveredKernel,
                libraryHandle,
                pipelineState,
                functionHandle,
                mslSource,
                metalDevice);

            // Cache for future use
            _compiledKernels[kernelId] = compiledKernel;

            _sLogCompilationCompleted(
                _logger,
                kernelId,
                mslSource.Length,
                null);

            return compiledKernel;
        }
        catch (Exception ex)
        {
            _sLogCompilationError(_logger, kernelId, ex.Message, ex);
            throw new InvalidOperationException(
                $"Failed to compile Ring Kernel '{kernelId}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Ensures the pipeline components are initialized.
    /// </summary>
    private void EnsurePipelineInitialized()
    {
        if (_kernelDiscovery == null || _stubGenerator == null || _handlerTranslator == null || _serializerGenerator == null)
        {
            // Use the existing logger to create typed loggers
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _kernelDiscovery ??= new MetalRingKernelDiscovery(
                loggerFactory.CreateLogger<MetalRingKernelDiscovery>());
            _stubGenerator ??= new MetalRingKernelStubGenerator(
                loggerFactory.CreateLogger<MetalRingKernelStubGenerator>());
            _handlerTranslator ??= new MetalHandlerTranslationService(
                loggerFactory.CreateLogger<MetalHandlerTranslationService>(),
                loggerFactory.CreateLogger<Translation.CSharpToMSLTranslator>());
            _serializerGenerator ??= new MetalMemoryPackSerializerGenerator(
                loggerFactory.CreateLogger<MetalMemoryPackSerializerGenerator>());
        }
    }

    /// <summary>
    /// Attempts to translate an inline handler for the kernel if one exists.
    /// </summary>
    [RequiresUnreferencedCode("Handler translation uses runtime reflection which is not compatible with trimming.")]
    private void TryTranslateInlineHandler(DiscoveredMetalRingKernel kernel, IEnumerable<Assembly>? assemblies)
    {
        if (!kernel.HasInlineHandler || _handlerTranslator == null)
        {
            return;
        }

        try
        {
            // Try to find and translate the handler based on input message type
            if (!string.IsNullOrEmpty(kernel.InputMessageTypeName))
            {
                var translatedHandler = _handlerTranslator.TranslateHandler(kernel.InputMessageTypeName, assemblies);
                if (!string.IsNullOrEmpty(translatedHandler))
                {
                    kernel.InlineHandlerMslCode = translatedHandler;
                    _logger.LogDebug("Translated inline handler for kernel '{KernelId}' from message type '{MessageType}'",
                        kernel.KernelId, kernel.InputMessageTypeName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not translate inline handler for kernel '{KernelId}', using default handler",
                kernel.KernelId);
        }
    }

    /// <summary>
    /// Stage 1: Discovers a Ring Kernel by its kernel ID using reflection.
    /// </summary>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    private Task<DiscoveredMetalRingKernel> DiscoverKernelAsync(
        string kernelId,
        IEnumerable<Assembly>? assemblies,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var kernel = _kernelDiscovery!.DiscoverKernelById(kernelId, assemblies);
            if (kernel == null)
            {
                throw new InvalidOperationException(
                    $"Ring Kernel '{kernelId}' not found. " +
                    $"Ensure the method has [RingKernel] attribute with KernelId=\"{kernelId}\" and supports Metal backend.");
            }

            return kernel;
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 2: Validates discovered kernel metadata.
    /// </summary>
    private static void ValidateKernelMetadata(DiscoveredMetalRingKernel kernel)
    {
        // Validate that all parameter types are Metal-compatible
        foreach (var param in kernel.Parameters)
        {
            if (!MetalTypeMapper.IsTypeSupported(param.ElementType))
            {
                throw new InvalidOperationException(
                    $"Parameter '{param.Name}' has unsupported type '{param.ElementType.Name}' for Metal compilation. " +
                    $"Supported types: primitives, Span<T>, arrays, and value types.");
            }
        }

        // Validate queue sizes
        if (kernel.InputQueueSize <= 0 || kernel.OutputQueueSize <= 0)
        {
            throw new InvalidOperationException(
                $"Ring Kernel '{kernel.KernelId}' has invalid queue sizes: " +
                $"Input={kernel.InputQueueSize}, Output={kernel.OutputQueueSize}");
        }
    }

    /// <summary>
    /// Stage 3: Generates MSL source code for the kernel.
    /// </summary>
    [RequiresUnreferencedCode("MSL generation uses runtime reflection to locate message types which is not compatible with trimming.")]
    private string GenerateMslSource(DiscoveredMetalRingKernel kernel)
    {
        // Check if we need to include MemoryPack serializers
        var messageTypes = new List<Type>();

        if (!string.IsNullOrEmpty(kernel.InputMessageTypeName))
        {
            // Try to find the input message type
            var inputType = FindMessageType(kernel.InputMessageTypeName);
            if (inputType != null)
            {
                messageTypes.Add(inputType);
                _logger.LogDebug("Found input message type '{TypeName}' for kernel '{KernelId}'",
                    kernel.InputMessageTypeName, kernel.KernelId);
            }
            else
            {
                _logger.LogWarning("Input message type '{TypeName}' not found for kernel '{KernelId}', " +
                    "serializers will not be generated. Ensure the type is loaded in current AppDomain.",
                    kernel.InputMessageTypeName, kernel.KernelId);
            }
        }

        // If we have message types, use combined source with MemoryPack serializers
        if (messageTypes.Count > 0)
        {
            var combinedSource = GenerateCombinedMslSource(kernel, messageTypes);

            _logger.LogDebug("Generated combined MSL source with MemoryPack serializers for kernel '{KernelId}' ({Length} bytes)",
                kernel.KernelId, combinedSource.Length);

            return combinedSource;
        }

        // Otherwise, just generate the kernel stub
        var mslSource = _stubGenerator!.GenerateKernelStub(kernel);

        _logger.LogDebug("Generated MSL source for kernel '{KernelId}' ({Length} bytes)",
            kernel.KernelId, mslSource.Length);

        return mslSource;
    }

    /// <summary>
    /// Attempts to find a message type by name in the loaded assemblies.
    /// </summary>
    /// <param name="typeName">The fully qualified type name.</param>
    /// <returns>The Type if found, otherwise null.</returns>
    [RequiresUnreferencedCode("Message type lookup uses runtime reflection which is not compatible with trimming.")]
    private Type? FindMessageType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        try
        {
            // Try to find the type in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            // If not found, try Type.GetType which handles assembly-qualified names
            return Type.GetType(typeName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find message type '{TypeName}'", typeName);
            return null;
        }
    }

    /// <summary>
    /// Generates MemoryPack serializer code for message types used by Ring Kernels.
    /// </summary>
    /// <param name="messageTypes">The message types to generate serializers for.</param>
    /// <param name="compilationUnitName">Name for the generated compilation unit.</param>
    /// <param name="skipHandlerGeneration">Whether to skip generating message handlers.</param>
    /// <returns>Generated MSL code with MemoryPack serializers.</returns>
    /// <remarks>
    /// This method can be used to pre-generate serializers for MemoryPackable types
    /// before kernel compilation, or to include them in a combined MSL source.
    /// </remarks>
    public string GenerateMemoryPackSerializers(
        IEnumerable<Type> messageTypes,
        string compilationUnitName = "MemoryPackSerializers",
        bool skipHandlerGeneration = false)
    {
        ArgumentNullException.ThrowIfNull(messageTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilationUnitName);

        EnsurePipelineInitialized();

        var serializerCode = _serializerGenerator!.GenerateBatchSerializer(
            messageTypes,
            compilationUnitName,
            skipHandlerGeneration);

        _logger.LogDebug("Generated MemoryPack serializers for {Count} types in '{Unit}'",
            messageTypes.Count(), compilationUnitName);

        return serializerCode;
    }

    /// <summary>
    /// Generates combined MSL source with both MemoryPack serializers and kernel code.
    /// </summary>
    /// <param name="kernel">The discovered kernel metadata.</param>
    /// <param name="messageTypes">Optional message types to generate serializers for.</param>
    /// <returns>Combined MSL source code.</returns>
    [RequiresUnreferencedCode("Combined MSL generation uses runtime reflection to locate message types which is not compatible with trimming.")]
    public string GenerateCombinedMslSource(
        DiscoveredMetalRingKernel kernel,
        IEnumerable<Type>? messageTypes = null)
    {
        var sb = new StringBuilder();

        // Generate MemoryPack serializers if message types are provided
        if (messageTypes != null && messageTypes.Any())
        {
            EnsurePipelineInitialized();

            var serializerCode = _serializerGenerator!.GenerateBatchSerializer(
                messageTypes,
                $"{kernel.KernelId}_Messages",
                skipHandlerGeneration: kernel.HasInlineHandler);

            _ = sb.AppendLine(serializerCode);
            _ = sb.AppendLine();

            _logger.LogDebug("Generated MemoryPack serializers for kernel '{KernelId}'",
                kernel.KernelId);
        }

        // Generate kernel code
        var kernelCode = GenerateMslSource(kernel);
        _ = sb.Append(kernelCode);

        return sb.ToString();
    }

    /// <summary>
    /// Stage 4: Compiles MSL source to a Metal library.
    /// </summary>
    private Task<IntPtr> CompileToLibraryAsync(
        string mslSource,
        string kernelId,
        IntPtr metalDevice,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var error = IntPtr.Zero;
            var library = MetalNative.CompileLibrary(metalDevice, mslSource, IntPtr.Zero, ref error);

            if (library == IntPtr.Zero)
            {
                var errorMessage = error != IntPtr.Zero
                    ? GetErrorMessage(error)
                    : "Unknown compilation error";

                if (error != IntPtr.Zero)
                {
                    MetalNative.ReleaseError(error);
                }

                throw new InvalidOperationException(
                    $"Failed to compile MSL to Metal library for kernel '{kernelId}': {errorMessage}");
            }

            _logger.LogDebug("Compiled Metal library for kernel '{KernelId}'", kernelId);
            return library;
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 5: Creates a compute pipeline state from the library.
    /// </summary>
    private Task<(IntPtr FunctionHandle, IntPtr PipelineState)> CreatePipelineStateAsync(
        IntPtr libraryHandle,
        string kernelId,
        IntPtr metalDevice,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get the kernel function from the library
            var functionName = $"{SanitizeKernelName(kernelId)}_kernel";
            var function = MetalNative.GetFunction(libraryHandle, functionName);

            if (function == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Failed to get function '{functionName}' from Metal library for kernel '{kernelId}'");
            }

            // Create compute pipeline state
            var error = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(metalDevice, function, ref error);

            if (pipelineState == IntPtr.Zero)
            {
                MetalNative.ReleaseFunction(function);

                var errorMessage = error != IntPtr.Zero
                    ? GetErrorMessage(error)
                    : "Unknown pipeline error";

                if (error != IntPtr.Zero)
                {
                    MetalNative.ReleaseError(error);
                }

                throw new InvalidOperationException(
                    $"Failed to create compute pipeline state for kernel '{kernelId}': {errorMessage}");
            }

            _logger.LogDebug("Created compute pipeline state for kernel '{KernelId}'", kernelId);
            return (function, pipelineState);
        }, cancellationToken);
    }

    /// <summary>
    /// Extracts error message from a Metal error handle.
    /// </summary>
    private static string GetErrorMessage(IntPtr error)
    {
        if (error == IntPtr.Zero)
        {
            return "Unknown error";
        }

        try
        {
            var descPtr = MetalNative.GetErrorLocalizedDescription(error);
            if (descPtr == IntPtr.Zero)
            {
                return "Unknown error";
            }

            return System.Runtime.InteropServices.Marshal.PtrToStringUTF8(descPtr) ?? "Unknown error";
        }
        catch
        {
            return "Unknown error (failed to read)";
        }
    }

    /// <summary>
    /// Stage 6: Verifies the pipeline state is valid.
    /// </summary>
    private void VerifyPipelineState(IntPtr pipelineState, string kernelId)
    {
        if (pipelineState == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Pipeline state is null for kernel '{kernelId}'");
        }

        _logger.LogDebug("Verified pipeline state for kernel '{KernelId}'", kernelId);
    }

    /// <summary>
    /// Gets a compiled kernel from the cache.
    /// </summary>
    /// <param name="kernelId">The kernel ID.</param>
    /// <returns>The compiled kernel, or null if not in cache.</returns>
    public MetalCompiledRingKernel? GetCompiledKernel(string kernelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        return _compiledKernels.TryGetValue(kernelId, out var kernel) ? kernel : null;
    }

    /// <summary>
    /// Gets all compiled kernels.
    /// </summary>
    /// <returns>A read-only collection of all compiled kernels.</returns>
    public IReadOnlyCollection<MetalCompiledRingKernel> GetAllCompiledKernels()
    {
        return _compiledKernels.Values.ToArray();
    }

    /// <summary>
    /// Clears the compiled kernel cache.
    /// </summary>
    public void ClearKernelCache()
    {
        // Dispose all cached kernels
        foreach (var kernel in _compiledKernels.Values)
        {
            kernel.Dispose();
        }

        _compiledKernels.Clear();
        _logger.LogInformation("Cleared compiled kernel cache");
    }
}

/// <summary>
/// Provides Metal type mapping for kernel parameters.
/// </summary>
internal static class MetalTypeMapper
{
    /// <summary>
    /// Determines if a type is supported for Metal kernel parameters.
    /// </summary>
    public static bool IsTypeSupported(Type type)
    {
        // Primitive types
        if (type.IsPrimitive)
        {
            return type == typeof(float) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(bool);
        }

        // Value types (structs)
        if (type.IsValueType && !type.IsEnum)
        {
            return true;
        }

        // Arrays
        if (type.IsArray)
        {
            return IsTypeSupported(type.GetElementType()!);
        }

        // Generic types (Span<T>, ReadOnlySpan<T>, Memory<T>, ReadOnlyMemory<T>)
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (genericTypeDef.Name is "Span`1" or "ReadOnlySpan`1" or "Memory`1" or "ReadOnlyMemory`1")
            {
                return IsTypeSupported(type.GetGenericArguments()[0]);
            }
        }

        return false;
    }

    /// <summary>
    /// Maps a .NET type to an MSL type name.
    /// </summary>
    public static string MapToMslType(Type type)
    {
        return type switch
        {
            _ when type == typeof(float) => "float",
            _ when type == typeof(int) => "int",
            _ when type == typeof(uint) => "uint",
            _ when type == typeof(short) => "short",
            _ when type == typeof(ushort) => "ushort",
            _ when type == typeof(byte) => "uchar",
            _ when type == typeof(sbyte) => "char",
            _ when type == typeof(bool) => "bool",
            _ when type == typeof(double) => "float", // Metal has limited double support
            _ when type == typeof(long) => "long",
            _ when type == typeof(ulong) => "ulong",
            _ => "void" // Unknown type
        };
    }
}
