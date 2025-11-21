// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Compiles [RingKernel] C# methods to CUDA PTX using a multi-stage pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This compiler implements a 6-stage compilation pipeline:
/// <list type="number">
/// <item><description><b>Stage 1 (Discovery)</b>: Find [RingKernel] methods via reflection</description></item>
/// <item><description><b>Stage 2 (Analysis)</b>: Extract method signature and metadata</description></item>
/// <item><description><b>Stage 3 (CUDA Generation)</b>: Generate CUDA C++ kernel code</description></item>
/// <item><description><b>Stage 4 (PTX Compilation)</b>: Compile CUDA → PTX with NVRTC</description></item>
/// <item><description><b>Stage 5 (Module Load)</b>: Load PTX module into CUDA context</description></item>
/// <item><description><b>Stage 6 (Verification)</b>: Verify function pointer retrieval</description></item>
/// </list>
/// </para>
/// <para>
/// The compiler integrates with components from Phase 1:
/// - <see cref="RingKernelDiscovery"/> for kernel discovery
/// - <see cref="CudaRingKernelStubGenerator"/> for CUDA code generation
/// - <see cref="PTXCompiler"/> for PTX compilation
/// - <see cref="CudaRuntime"/> for module loading and function pointer retrieval
/// </para>
/// </remarks>
public partial class CudaRingKernelCompiler
{
    // Fields for the 6-stage compilation pipeline
    private readonly RingKernelDiscovery _kernelDiscovery;
    private readonly CudaRingKernelStubGenerator _stubGenerator;
    private readonly CudaMemoryPackSerializerGenerator _serializerGenerator;
    private readonly ConcurrentDictionary<string, CudaCompiledRingKernel> _compiledKernels = new();

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, string, Exception?> _sLogCompilationStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "CompilationStarted"),
            "Starting Ring Kernel compilation for '{KernelId}'");

    private static readonly Action<ILogger, string, int, Exception?> _sLogStageCompleted =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(2, "StageCompleted"),
            "Compilation stage {Stage} completed for kernel '{KernelId}'");

    private static readonly Action<ILogger, string, long, int, long, Exception?> _sLogCompilationCompleted =
        LoggerMessage.Define<string, long, int, long>(
            LogLevel.Information,
            new EventId(3, "CompilationCompleted"),
            "Ring Kernel '{KernelId}' compiled successfully: " +
            "PTX={PtxSize} bytes, Module=0x{ModuleHandle:X16}, Function=0x{FunctionPtr:X16}");

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
    /// Compilation stages for Ring Kernel PTX generation.
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
        /// Stage 3: Generate CUDA C++ kernel code.
        /// </summary>
        CudaGeneration = 3,

        /// <summary>
        /// Stage 4: Compile CUDA → PTX with NVRTC.
        /// </summary>
        PTXCompilation = 4,

        /// <summary>
        /// Stage 5: Load PTX module into CUDA context.
        /// </summary>
        ModuleLoad = 5,

        /// <summary>
        /// Stage 6: Verify function pointer retrieval.
        /// </summary>
        Verification = 6
    }

    /// <summary>
    /// Compiles a Ring Kernel to executable PTX using the 6-stage pipeline.
    /// </summary>
    /// <param name="kernelId">The unique kernel identifier.</param>
    /// <param name="cudaContext">The CUDA context to load the module into.</param>
    /// <param name="options">Compilation options (optional).</param>
    /// <param name="assemblies">Assemblies to search for the kernel (optional, scans all if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compiled kernel with PTX module and function pointer.</returns>
    /// <exception cref="ArgumentException">Thrown when kernelId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails at any stage.</exception>
    [RequiresUnreferencedCode("Ring Kernel compilation uses runtime reflection which is not compatible with trimming.")]
    public async Task<CudaCompiledRingKernel> CompileRingKernelAsync(
        string kernelId,
        IntPtr cudaContext,
        CompilationOptions? options = null,
        IEnumerable<Assembly>? assemblies = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        // Check cancellation early
        cancellationToken.ThrowIfCancellationRequested();

        if (cudaContext == IntPtr.Zero)
        {
            throw new InvalidOperationException("CUDA context cannot be zero");
        }

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

            // Stage 3: CUDA Generation - Generate CUDA C++ kernel code
            var cudaSource = GenerateCudaSource(discoveredKernel);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.CudaGeneration, null);

            // Stage 4: PTX Compilation - Compile CUDA → PTX with NVRTC
            var ptxBytes = await CompileToPTXAsync(cudaSource, kernelId, options, cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.PTXCompilation, null);

            // Stage 5: Module Load - Load PTX module into CUDA context
            var moduleHandle = await LoadPTXModuleAsync(ptxBytes, cudaContext, cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.ModuleLoad, null);

            // Stage 6: Verification - Get kernel function pointer
            var functionPtr = await GetKernelFunctionPointerAsync(
                moduleHandle,
                kernelId,
                cudaContext,
                cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.Verification, null);

            // Create compiled kernel
            var compiledKernel = new CudaCompiledRingKernel(
                discoveredKernel,
                moduleHandle,
                functionPtr,
                ptxBytes,
                cudaContext)
            {
                Name = discoveredKernel.KernelId
            };

            // Cache for future use
            _compiledKernels[kernelId] = compiledKernel;

            _sLogCompilationCompleted(
                _logger,
                kernelId,
                ptxBytes.Length,
                (int)moduleHandle,
                (int)functionPtr,
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
    /// Stage 1: Discovers a Ring Kernel by its kernel ID using reflection.
    /// </summary>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    private Task<DiscoveredRingKernel> DiscoverKernelAsync(
        string kernelId,
        IEnumerable<Assembly>? assemblies,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var kernel = _kernelDiscovery.DiscoverKernelById(kernelId, assemblies);
            if (kernel == null)
            {
                throw new InvalidOperationException(
                    $"Ring Kernel '{kernelId}' not found. " +
                    $"Ensure the method has [RingKernel] attribute with KernelId=\"{kernelId}\".");
            }

            return kernel;
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 2: Validates discovered kernel metadata.
    /// </summary>
    private static void ValidateKernelMetadata(DiscoveredRingKernel kernel)
    {
        // Validate that all parameter types are CUDA-compatible
        foreach (var param in kernel.Parameters)
        {
            if (!CudaTypeMapper.IsTypeSupported(param.ElementType))
            {
                throw new InvalidOperationException(
                    $"Parameter '{param.Name}' has unsupported type '{param.ElementType.Name}' for CUDA compilation. " +
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
    /// Stage 3: Generates CUDA C++ source code for the kernel.
    /// </summary>
    [RequiresUnreferencedCode("Message type discovery uses runtime reflection which is not compatible with trimming.")]
    private string GenerateCudaSource(DiscoveredRingKernel kernel)
    {
        var sourceBuilder = new StringBuilder();

        // Step 1: Generate kernel stub (uses CudaRingKernelStubGenerator from Phase 1.4)
        // includeHostLauncher = false: NVRTC (Stage 4) only compiles device code, not host API calls
        var kernelStub = _stubGenerator.GenerateKernelStub(kernel, includeHostLauncher: false);

        // Step 2: Generate MemoryPack serialization code for message types
        var serializationSource = TryGenerateSerializationCode(kernel);

        // Step 3: Load/translate message handler implementation
        var handlerSource = TryLoadMessageHandler(kernel);

        // Step 4: Combine all parts - insert serialization and handler before kernel function
        var kernelMarker = "extern \"C\" __global__";
        var lines = kernelStub.Split('\n');
        var insertIndex = -1;

        // Find the line with "extern "C" __global__" - we'll insert code just before it
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(kernelMarker, StringComparison.Ordinal))
            {
                insertIndex = i; // Insert before the kernel declaration
                break;
            }
        }

        if (insertIndex > 0)
        {
            // Copy header/includes/infrastructure structs
            for (int i = 0; i < insertIndex; i++)
            {
                sourceBuilder.AppendLine(lines[i]);
            }

            // Insert MemoryPack serialization code (structs + deserialize/serialize functions)
            if (!string.IsNullOrEmpty(serializationSource))
            {
                sourceBuilder.AppendLine();
                sourceBuilder.AppendLine("// ===========================================================================");
                sourceBuilder.AppendLine("// MemoryPack Serialization Code (Auto-generated)");
                sourceBuilder.AppendLine("// ===========================================================================");
                sourceBuilder.AppendLine(serializationSource);
                sourceBuilder.AppendLine();
                _logger.LogDebug("Serialization code inserted ({Length} bytes)", serializationSource.Length);
            }

            // Insert handler implementation (business logic)
            if (!string.IsNullOrEmpty(handlerSource))
            {
                sourceBuilder.AppendLine();
                sourceBuilder.AppendLine("// ===========================================================================");
                sourceBuilder.AppendLine("// Message Handler Implementation (Auto-translated from C#)");
                sourceBuilder.AppendLine("// ===========================================================================");
                sourceBuilder.AppendLine(handlerSource);
                sourceBuilder.AppendLine();
                _logger.LogDebug("Handler inserted ({Length} bytes)", handlerSource.Length);
            }

            // Append the kernel function and rest of code
            for (int i = insertIndex; i < lines.Length; i++)
            {
                sourceBuilder.AppendLine(lines[i]);
            }

            var finalSource = sourceBuilder.ToString();
            _logger.LogDebug("Complete CUDA source generated ({Length} bytes)", finalSource.Length);
            return finalSource;
        }

        // Fallback: if insertion point not found, just use the stub as-is
        _logger.LogDebug("Using kernel stub without modifications ({Length} bytes)", kernelStub.Length);
        return kernelStub;
    }

    /// <summary>
    /// Generates MemoryPack serialization code for the kernel's message types.
    /// </summary>
    /// <param name="kernel">Ring kernel metadata.</param>
    /// <returns>CUDA serialization code if message types found, null otherwise.</returns>
    [RequiresUnreferencedCode("Message type discovery uses runtime reflection which is not compatible with trimming.")]
    private string? TryGenerateSerializationCode(DiscoveredRingKernel kernel)
    {
        try
        {
            // Build multiple search patterns for message types
            // Different naming conventions may be used:
            // 1. "VectorAddRingKernel" → "VectorAddRequest" (removes "RingKernel")
            // 2. "VectorAddProcessorRing" → "VectorAddProcessorRingRequest" (preserves "Ring")
            // 3. KernelId-based: "VectorAddProcessorRing" → "VectorAddProcessorRingRequest"

            var searchPatterns = new List<(string requestPattern, string responsePattern)>();

            // Pattern 1: Remove "RingKernel" and "Kernel" (original behavior)
            var handlerName1 = kernel.Method.Name
                .Replace("RingKernel", "", StringComparison.Ordinal)
                .Replace("Kernel", "", StringComparison.Ordinal);
            searchPatterns.Add(($"{handlerName1}Request", $"{handlerName1}Response"));

            // Pattern 2: Use KernelId directly (may include "Ring")
            var kernelId = kernel.KernelId;
            if (!string.Equals(kernelId, handlerName1, StringComparison.Ordinal))
            {
                searchPatterns.Add(($"{kernelId}Request", $"{kernelId}Response"));
            }

            // Pattern 3: Remove only "Kernel" but keep "Ring"
            var handlerName3 = kernel.Method.Name.Replace("Kernel", "", StringComparison.Ordinal);
            if (!string.Equals(handlerName3, handlerName1, StringComparison.Ordinal) &&
                !string.Equals(handlerName3, kernelId, StringComparison.Ordinal))
            {
                searchPatterns.Add(($"{handlerName3}Request", $"{handlerName3}Response"));
            }

            _logger.LogDebug("Searching for message types with {PatternCount} patterns for kernel '{KernelId}'",
                searchPatterns.Count, kernel.KernelId);
            foreach (var (req, resp) in searchPatterns)
            {
                _logger.LogDebug("  Pattern: {Request}, {Response}", req, resp);
            }

            // Search for message types in loaded assemblies
            var messageTypes = new List<Type>();
            var foundTypeNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        // Skip if already found (avoid duplicates from multiple patterns)
                        if (foundTypeNames.Contains(type.FullName ?? type.Name))
                        {
                            continue;
                        }

                        // Check against all search patterns
                        foreach (var (requestPattern, responsePattern) in searchPatterns)
                        {
                            if (string.Equals(type.Name, requestPattern, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(type.Name, responsePattern, StringComparison.OrdinalIgnoreCase))
                            {
                                messageTypes.Add(type);
                                foundTypeNames.Add(type.FullName ?? type.Name);
                                _logger.LogInformation("Found message type '{TypeName}' for kernel '{KernelId}'",
                                    type.Name, kernel.KernelId);
                                break; // Found a match, move to next type
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                    continue;
                }
            }

            if (messageTypes.Count == 0)
            {
                _logger.LogDebug("No message types found for kernel {KernelId}", kernel.KernelId);
                return null;
            }

            // Log found types before generating
            _logger.LogDebug("Found {Count} message types for kernel '{KernelId}': [{Types}]",
                messageTypes.Count, kernel.KernelId, string.Join(", ", messageTypes.Select(t => t.Name)));

            // Generate serialization code for all found message types
            var serializationCode = _serializerGenerator.GenerateBatchSerializer(
                messageTypes,
                $"{kernel.KernelId}Messages");

            _logger.LogInformation(
                "Generated MemoryPack serialization code for {Count} message types ({Length} bytes)",
                messageTypes.Count, serializationCode.Length);

            return serializationCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate serialization code for kernel {KernelId}", kernel.KernelId);
            return null;
        }
    }

    /// <summary>
    /// Attempts to generate or load the message handler CUDA implementation for the specified kernel.
    /// </summary>
    /// <param name="kernel">Ring kernel metadata.</param>
    /// <returns>Handler CUDA source code if found/generated, null otherwise.</returns>
    /// <remarks>
    /// Uses a multi-strategy approach:
    /// 1. **Automatic C# → CUDA**: Searches for C# handler, translates to CUDA
    /// 2. **Manual CUDA**: Falls back to pre-written .cu file
    /// </remarks>
    private string? TryLoadMessageHandler(DiscoveredRingKernel kernel)
    {
        var handlerName = kernel.Method.Name
            .Replace("RingKernel", "", StringComparison.Ordinal)
            .Replace("Kernel", "", StringComparison.Ordinal);

        _logger.LogDebug("Searching for message handler: {HandlerName} (from kernel method: {MethodName})",
            handlerName, kernel.Method.Name);

        // Strategy 1: Try automatic C# → CUDA translation
        _logger.LogDebug("Attempting automatic C# to CUDA translation");
        var translatedCuda = TryTranslateCSharpHandler(handlerName, kernel);
        if (!string.IsNullOrEmpty(translatedCuda))
        {
            _logger.LogInformation("Successfully translated C# handler to CUDA ({Length} bytes)", translatedCuda.Length);
            return translatedCuda;
        }
        _logger.LogDebug("Automatic C# to CUDA translation failed or no handler found");

        // Strategy 2: Fall back to manual .cu file
        _logger.LogDebug("Attempting manual CUDA file fallback");
        var manualCuda = TryLoadManualCudaFile(handlerName);
        if (!string.IsNullOrEmpty(manualCuda))
        {
            _logger.LogInformation("Loaded manual CUDA handler ({Length} bytes)", manualCuda.Length);
            return manualCuda;
        }
        _logger.LogDebug("Manual CUDA file not found");

        _logger.LogWarning("No message handler found for '{HandlerName}' (neither C# nor manual CUDA file)", handlerName);
        return null;
    }

    /// <summary>
    /// Attempts to translate a C# handler to CUDA using HandlerTranslationService.
    /// </summary>
    private string? TryTranslateCSharpHandler(string handlerName, DiscoveredRingKernel kernel)
    {
        try
        {
            // Derive message type name from handler name
            // E.g., "VectorAdd" → "VectorAddRequest"
            var messageTypeName = $"{handlerName}Request";
            _logger.LogDebug("Translating handler for message type: {MessageType}", messageTypeName);

            // Search for C# handler source file
            var handlerFileName = $"{handlerName}Handler.cs";
            var handlerSourcePath = FindHandlerSourceFile(handlerFileName);

            if (handlerSourcePath == null)
            {
                _logger.LogDebug("Handler source file not found: {FileName}", handlerFileName);
                return null;
            }

            _logger.LogDebug("Found handler source: {Path}", handlerSourcePath);

            // Load handler source code
            var handlerSource = File.ReadAllText(handlerSourcePath);
            _logger.LogDebug("Loaded handler source ({Length} characters)", handlerSource.Length);

            // Create Roslyn compilation
            var compilation = CreateCompilation(handlerSource, handlerSourcePath);
            if (compilation == null)
            {
                _logger.LogWarning("Failed to create Roslyn compilation for handler: {Path}", handlerSourcePath);
                return null;
            }

            _logger.LogDebug("Created Roslyn compilation for handler translation");

            // Use HandlerTranslationService to translate C# → CUDA
            var cudaCode = DotCompute.Generators.MemoryPack.HandlerTranslationService.TranslateHandler(
                messageTypeName,
                compilation);

            if (cudaCode != null)
            {
                _logger.LogDebug("Handler translation successful ({Length} bytes of CUDA)", cudaCode.Length);
            }
            else
            {
                _logger.LogWarning("Handler translation returned null for message type: {MessageType}", messageTypeName);
            }

            return cudaCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during C# to CUDA handler translation: {HandlerName}", handlerName);
            return null;
        }
    }

    /// <summary>
    /// Finds a C# handler source file by searching common locations.
    /// </summary>
    private string? FindHandlerSourceFile(string handlerFileName)
    {
        var baseDirectory = AppContext.BaseDirectory;

        // Try multiple filename patterns:
        // 1. VectorAddHandler.cs (standard handler pattern)
        // 2. VectorAddRingKernel.cs (ring kernel pattern)
        // 3. VectorAdd.cs (simple pattern)
        var filePatterns = new[]
        {
            handlerFileName,  // Original pattern (e.g., VectorAddHandler.cs)
            handlerFileName.Replace("Handler.cs", "RingKernel.cs", StringComparison.Ordinal),  // Ring kernel pattern
            handlerFileName.Replace("Handler.cs", ".cs", StringComparison.Ordinal),  // Simple pattern
        };

        // Search paths for C# handler files
        var searchDirectories = new[]
        {
            // Orleans.GpuBridge.Core: src/.../Temporal/
            Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "src", "Orleans.GpuBridge.Backends.DotCompute", "Temporal"),

            // Development: samples/RingKernels/MessageHandlers/
            Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "samples", "RingKernels", "MessageHandlers"),
            Path.Combine("samples", "RingKernels", "MessageHandlers"),

            // Test project: tests/.../MessageHandlers/
            Path.Combine(baseDirectory, "..", "..", "..", "MessageHandlers"),

            // Current directory
            baseDirectory,
        };

        // Try each pattern in each directory
        _logger.LogDebug("Searching for handler file: {FileName} in {DirectoryCount} directories with {PatternCount} patterns",
            handlerFileName, searchDirectories.Length, filePatterns.Length);

        foreach (var directory in searchDirectories)
        {
            foreach (var filePattern in filePatterns)
            {
                var path = Path.Combine(directory, filePattern);
                var normalizedPath = Path.GetFullPath(path);

                if (File.Exists(normalizedPath))
                {
                    _logger.LogDebug("Found handler source file at: {Path}", normalizedPath);
                    return normalizedPath;
                }
            }
        }

        _logger.LogDebug("Handler source file not found after searching all paths: {FileName}", handlerFileName);
        return null;
    }

    /// <summary>
    /// Creates a Roslyn compilation from C# source code.
    /// </summary>
    private Microsoft.CodeAnalysis.Compilation? CreateCompilation(string sourceCode, string filePath)
    {
        try
        {
            var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                sourceCode,
                path: filePath);

            // Add references to required assemblies (Native AOT compatible)
            // Use AppContext.BaseDirectory instead of Assembly.Location for Native AOT compatibility
            var runtimePath = AppContext.BaseDirectory;
            var references = new List<Microsoft.CodeAnalysis.MetadataReference>();

            // Core assemblies needed for handler compilation
            var coreAssemblies = new[]
            {
                "System.Runtime.dll",
                "System.Private.CoreLib.dll",
                "System.Memory.dll",
                "System.Linq.dll",
                "System.Collections.dll"
            };

            // Try to find assemblies in application directory first
            foreach (var assemblyName in coreAssemblies)
            {
                var assemblyPath = Path.Combine(runtimePath, assemblyName);
                if (File.Exists(assemblyPath))
                {
                    references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(assemblyPath));
                }
            }

            // If we didn't find the assemblies in the base directory, try the .NET shared runtime
            if (references.Count == 0)
            {
                // Try multiple possible .NET runtime locations (cross-platform)
                var possibleDotnetPaths = new[]
                {
                    // Windows: Program Files
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.NETCore.App"),
                    // Linux common locations
                    "/usr/share/dotnet/shared/Microsoft.NETCore.App",
                    "/usr/lib/dotnet/shared/Microsoft.NETCore.App",
                    // macOS
                    "/usr/local/share/dotnet/shared/Microsoft.NETCore.App"
                };

                foreach (var dotnetSharedPath in possibleDotnetPaths)
                {
                    if (Directory.Exists(dotnetSharedPath))
                    {
                        // Find the latest runtime version directory
                        var versionDirs = Directory.GetDirectories(dotnetSharedPath)
                            .OrderByDescending(d => d)
                            .FirstOrDefault();

                        if (versionDirs != null)
                        {
                            foreach (var assemblyName in coreAssemblies)
                            {
                                var assemblyPath = Path.Combine(versionDirs, assemblyName);
                                if (File.Exists(assemblyPath))
                                {
                                    references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(assemblyPath));
                                }
                            }

                            // If we found assemblies, break out of the loop
                            if (references.Count > 0)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            if (references.Count == 0)
            {
                _logger.LogWarning("Failed to locate required .NET assemblies for Roslyn compilation");
                return null;
            }

            _logger.LogDebug("Creating Roslyn compilation with {ReferenceCount} assembly references", references.Count);

            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                assemblyName: "HandlerCompilation",
                syntaxTrees: [syntaxTree],
                references: references,
                options: new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                    Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

            return compilation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Roslyn compilation");
            return null;
        }
    }

    /// <summary>
    /// Attempts to load a manually written CUDA .cu file.
    /// </summary>
    private string? TryLoadManualCudaFile(string handlerName)
    {
        try
        {
            var cudaFileName = $"{handlerName}Serialization.cu";
            _logger.LogDebug("Searching for manual CUDA file: {FileName}", cudaFileName);

            var baseDirectory = AppContext.BaseDirectory;

            // Search paths for manual .cu files
            var searchPaths = new[]
            {
                Path.Combine(baseDirectory, "..", "..", "..", "Messaging", cudaFileName),
                Path.Combine(baseDirectory, "Messaging", cudaFileName),
                Path.Combine("src", "Backends", "DotCompute.Backends.CUDA", "Messaging", cudaFileName),
                Path.Combine("Messaging", cudaFileName)
            };

            foreach (var path in searchPaths)
            {
                var normalizedPath = Path.GetFullPath(path);

                if (File.Exists(normalizedPath))
                {
                    var content = File.ReadAllText(normalizedPath);
                    _logger.LogDebug("Found manual CUDA file at: {Path} ({Length} bytes)", normalizedPath, content.Length);
                    return content;
                }
            }

            _logger.LogDebug("Manual CUDA file not found: {FileName}", cudaFileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while loading manual CUDA file for handler: {HandlerName}", handlerName);
            return null;
        }
    }

    /// <summary>
    /// Stage 4: Compiles CUDA source to PTX using NVRTC.
    /// </summary>
    private async Task<ReadOnlyMemory<byte>> CompileToPTXAsync(
        string cudaSource,
        string kernelId,
        CompilationOptions? options,
        CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build compilation options
            var compilationOptions = options?.Clone() ?? new CompilationOptions();

            // Ensure cooperative groups are enabled for Ring Kernels
            if (!compilationOptions.AdditionalFlags.Contains("-rdc=true"))
            {
                compilationOptions.AdditionalFlags.Add("-rdc=true");
            }
            if (!compilationOptions.AdditionalFlags.Contains("--device-c"))
            {
                compilationOptions.AdditionalFlags.Add("--device-c");
            }

            // Use PTXCompiler from existing infrastructure
            var ptxBytes = await PTXCompiler.CompileToPtxAsync(
                cudaSource,
                kernelId,
                compilationOptions,
                _logger);

            return new ReadOnlyMemory<byte>(ptxBytes);
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 5: Loads the PTX module into the CUDA context.
    /// </summary>
    private async Task<IntPtr> LoadPTXModuleAsync(
        ReadOnlyMemory<byte> ptxBytes,
        IntPtr cudaContext,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Set current CUDA context
            var setContextResult = CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to set CUDA context: {setContextResult}");
            }

            // Load PTX module - pin memory for native call
            var moduleHandle = IntPtr.Zero;
            unsafe
            {
                fixed (byte* ptxPtr = ptxBytes.Span)
                {
                    var loadResult = CudaRuntime.cuModuleLoadData(ref moduleHandle, (IntPtr)ptxPtr);

                    if (loadResult != Types.Native.CudaError.Success)
                    {
                        throw new InvalidOperationException(
                            $"Failed to load PTX module: {loadResult}");
                    }
                }
            }

            if (moduleHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "PTX module loaded but handle is null");
            }

            return moduleHandle;
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 6: Retrieves the kernel function pointer from the loaded module.
    /// </summary>
    private static async Task<IntPtr> GetKernelFunctionPointerAsync(
        IntPtr moduleHandle,
        string kernelId,
        IntPtr cudaContext,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Set current CUDA context
            var setContextResult = CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to set CUDA context: {setContextResult}");
            }

            // Get function pointer for the kernel
            // The kernel function name is "{kernelId}_kernel" (from stub generator)
            var functionName = $"{SanitizeKernelName(kernelId)}_kernel";
            var functionPtr = IntPtr.Zero;

            var getResult = CudaRuntime.cuModuleGetFunction(
                ref functionPtr,
                moduleHandle,
                functionName);

            if (getResult != Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to get kernel function pointer for '{functionName}': {getResult}");
            }

            if (functionPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Kernel function '{functionName}' not found in module");
            }

            return functionPtr;
        }, cancellationToken);
    }

    /// <summary>
    /// Gets a compiled kernel from the cache.
    /// </summary>
    /// <param name="kernelId">The kernel ID.</param>
    /// <returns>The compiled kernel, or null if not in cache.</returns>
    public CudaCompiledRingKernel? GetCompiledKernel(string kernelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        return _compiledKernels.TryGetValue(kernelId, out var kernel) ? kernel : null;
    }

    /// <summary>
    /// Gets all compiled kernels.
    /// </summary>
    /// <returns>A read-only collection of all compiled kernels.</returns>
    public IReadOnlyCollection<CudaCompiledRingKernel> GetAllCompiledKernels()
    {
        return _compiledKernels.Values.ToArray();
    }

    /// <summary>
    /// Clears the compiled kernel cache.
    /// </summary>
    public void ClearCache()
    {
        // Dispose all cached kernels
        foreach (var kernel in _compiledKernels.Values)
        {
            kernel.Dispose();
        }

        _compiledKernels.Clear();
    }
}
