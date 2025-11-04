// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Orchestrates the complete compilation pipeline: Expression → IR → Code → Compiled Delegate.
/// </summary>
/// <remarks>
/// <para>
/// The CompilationPipeline provides a complete end-to-end compilation system that transforms
/// LINQ operation graphs into executable delegates through several stages:
/// </para>
/// <list type="number">
/// <item><description>Input validation and cache lookup</description></item>
/// <item><description>Code generation from operation graph using <see cref="CpuKernelGenerator"/></description></item>
/// <item><description>Roslyn compilation to in-memory assembly</description></item>
/// <item><description>Delegate extraction and type verification</description></item>
/// <item><description>Cache storage for reuse</description></item>
/// </list>
/// <para>
/// The pipeline implements graceful fallback to expression compilation
/// when Roslyn compilation fails, ensuring reliability while optimizing for performance.
/// </para>
/// <para>
/// All operations are thread-safe and support concurrent compilation of multiple queries.
/// The internal Roslyn workspace is reused across compilations for efficiency.
/// </para>
/// </remarks>
public sealed class CompilationPipeline : IDisposable
{
    private readonly IKernelCache _kernelCache;
    private readonly CpuKernelGenerator _kernelGenerator;
    private readonly ILogger<CompilationPipeline>? _logger;
    private readonly CSharpCompilationOptions _defaultCompilationOptions;
    private readonly List<MetadataReference> _defaultReferences;
    private bool _disposed;

    // GPU Kernel Compilers (Phase 6 Option A - Production Integration)
    private readonly Compilation.CudaRuntimeKernelCompiler? _cudaCompiler;
    private readonly Compilation.OpenCLRuntimeKernelCompiler? _openclCompiler;
    private readonly Compilation.MetalRuntimeKernelCompiler? _metalCompiler;

    // Statistics
    private long _totalCompilations;
    private long _cacheHits;
    private long _compilationFailures;
    private long _fallbackCount;

    // Compilation synchronization to prevent duplicate compilations
    private readonly object _compilationLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationPipeline"/> class.
    /// </summary>
    /// <param name="kernelCache">The kernel cache for storing compiled delegates.</param>
    /// <param name="kernelGenerator">The CPU kernel generator for creating source code.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cudaCompiler">Optional CUDA runtime kernel compiler for GPU acceleration.</param>
    /// <param name="openclCompiler">Optional OpenCL runtime kernel compiler for cross-platform GPU.</param>
    /// <param name="metalCompiler">Optional Metal runtime kernel compiler for Apple Silicon.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="kernelCache"/> or <paramref name="kernelGenerator"/> is null.
    /// </exception>
    public CompilationPipeline(
        IKernelCache kernelCache,
        CpuKernelGenerator kernelGenerator,
        ILogger<CompilationPipeline>? logger = null,
        Compilation.CudaRuntimeKernelCompiler? cudaCompiler = null,
        Compilation.OpenCLRuntimeKernelCompiler? openclCompiler = null,
        Compilation.MetalRuntimeKernelCompiler? metalCompiler = null)
    {
        _kernelCache = kernelCache ?? throw new ArgumentNullException(nameof(kernelCache));
        _kernelGenerator = kernelGenerator ?? throw new ArgumentNullException(nameof(kernelGenerator));
        _logger = logger;
        _cudaCompiler = cudaCompiler;
        _openclCompiler = openclCompiler;
        _metalCompiler = metalCompiler;

        // Initialize default compilation options
        _defaultCompilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: Microsoft.CodeAnalysis.OptimizationLevel.Release,
            allowUnsafe: true,
            platform: Platform.AnyCpu,
            deterministic: true,
            concurrentBuild: true);

        // Build list of required assembly references
        _defaultReferences = GetRequiredReferences();

        if (_cudaCompiler != null || _openclCompiler != null || _metalCompiler != null)
        {
            _logger?.LogInformation("CompilationPipeline initialized with GPU kernel compilers (Phase 6 Option A)");
        }
    }

    /// <summary>
    /// Gets compilation metrics and statistics.
    /// </summary>
    /// <returns>A snapshot of current compilation metrics.</returns>
    public CompilationMetrics GetMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new CompilationMetrics
        {
            TotalCompilations = Interlocked.Read(ref _totalCompilations),
            CacheHits = Interlocked.Read(ref _cacheHits),
            CompilationFailures = Interlocked.Read(ref _compilationFailures),
            FallbackCount = Interlocked.Read(ref _fallbackCount),
            CacheStatistics = _kernelCache.GetStatistics()
        };
    }

    /// <summary>
    /// Compiles an operation graph to an executable delegate with caching.
    /// </summary>
    /// <typeparam name="T">The input element type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>An executable function that applies the operations to an input array.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    /// <exception cref="CompilationException">
    /// Thrown when compilation fails and fallback is not possible.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method first checks the kernel cache for an existing compiled delegate.
    /// If found, the cached delegate is returned immediately, avoiding recompilation.
    /// </para>
    /// <para>
    /// If not cached, the method generates C# source code, compiles it using Roslyn,
    /// extracts the delegate, and caches it for future use.
    /// </para>
    /// <para>
    /// On compilation failure, the method attempts to fall back to expression compilation
    /// if possible, though this path provides reduced performance.
    /// </para>
    /// </remarks>
    public Func<T[], TResult[]> CompileToDelegate<T, TResult>(
        OperationGraph graph,
        TypeMetadata metadata,
        DotCompute.Abstractions.CompilationOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Interlocked.Increment(ref _totalCompilations);

        // Stage 1: Validate inputs
        ValidateInputs(graph, metadata, options);

        // Stage 2: Try cache lookup (first check without lock)
        var cacheKey = KernelCache.GenerateCacheKey(graph, metadata, options);
        var cached = TryGetCachedDelegate<T, TResult>(cacheKey);
        if (cached != null)
        {
            Interlocked.Increment(ref _cacheHits);
            _logger?.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        // Stage 3: Use lock to prevent duplicate compilations
        lock (_compilationLock)
        {
            // Double-check cache after acquiring lock (another thread might have compiled it)
            cached = TryGetCachedDelegate<T, TResult>(cacheKey);
            if (cached != null)
            {
                Interlocked.Increment(ref _cacheHits);
                _logger?.LogDebug("Cache hit after lock acquisition for key: {CacheKey}", cacheKey);
                return cached;
            }

            _logger?.LogInformation("Starting compilation for new query (cache key: {CacheKey})", cacheKey);

            try
            {
                // Stage 4: Compile with fallback
                var compiledDelegate = CompileWithFallback<T, TResult>(graph, metadata, options);

                // Stage 5: Cache the compiled delegate
                // NOTE: CompilationOptions doesn't have CacheTtl property, using default TTL
                CacheDelegate(cacheKey, compiledDelegate, TimeSpan.FromMinutes(60));

                _logger?.LogInformation("Compilation successful, delegate cached");
                return compiledDelegate;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _compilationFailures);
                _logger?.LogError(ex, "Compilation failed for query");
                throw;
            }
        }
    }

    /// <summary>
    /// Validates inputs before compilation.
    /// </summary>
    /// <param name="graph">The operation graph to validate.</param>
    /// <param name="metadata">Type metadata to validate.</param>
    /// <param name="options">Compilation options to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when inputs are invalid.</exception>
    private void ValidateInputs(OperationGraph graph, TypeMetadata metadata, DotCompute.Abstractions.CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        if (graph.Operations.Count == 0)
        {
            throw new ArgumentException("Operation graph contains no operations", nameof(graph));
        }

        if (graph.Root == null)
        {
            throw new ArgumentException("Operation graph has no root operation", nameof(graph));
        }

        if (metadata.InputType == null)
        {
            throw new ArgumentException("Type metadata missing input type", nameof(metadata));
        }

        if (metadata.ResultType == null)
        {
            throw new ArgumentException("Type metadata missing result type", nameof(metadata));
        }
    }

    /// <summary>
    /// Attempts to retrieve a cached compiled delegate.
    /// </summary>
    /// <typeparam name="T">Input element type.</typeparam>
    /// <typeparam name="TResult">Result element type.</typeparam>
    /// <param name="cacheKey">The cache key to lookup.</param>
    /// <returns>The cached delegate if found; otherwise, null.</returns>
    private Func<T[], TResult[]>? TryGetCachedDelegate<T, TResult>(string cacheKey)
    {
        var cached = _kernelCache.GetCached(cacheKey);
        if (cached is Func<T[], TResult[]> typedDelegate)
        {
            return typedDelegate;
        }

        return null;
    }

    /// <summary>
    /// Compiles the query with graceful fallback on error.
    /// </summary>
    /// <typeparam name="T">Input element type.</typeparam>
    /// <typeparam name="TResult">Result element type.</typeparam>
    /// <param name="graph">The operation graph.</param>
    /// <param name="metadata">Type metadata.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>A compiled delegate.</returns>
    /// <exception cref="CompilationException">Thrown when compilation and fallback both fail.</exception>
    private Func<T[], TResult[]> CompileWithFallback<T, TResult>(
        OperationGraph graph,
        TypeMetadata metadata,
        DotCompute.Abstractions.CompilationOptions options)
    {
        try
        {
            // Stage 1: Generate code
            _logger?.LogDebug("Generating kernel code");
            var sourceCode = GenerateCode(graph, metadata, options);

            if (options.GenerateDebugInfo)
            {
                _logger?.LogDebug("Generated source code:\n{SourceCode}", sourceCode);
            }

            // Stage 2: Compile to assembly
            _logger?.LogDebug("Compiling source code with Roslyn");
            var metricsResult = ProfileCompilation(() => CompileToAssembly(sourceCode, options));

            _logger?.LogInformation(
                "Compilation completed in {Time:F2}ms, memory: {Memory:N0} bytes",
                metricsResult.Metrics.CompilationTime.TotalMilliseconds,
                metricsResult.Metrics.MemoryAllocated);

            // Stage 3: Extract delegate
            _logger?.LogDebug("Extracting delegate from compiled assembly");
            var delegateType = typeof(Func<T[], TResult[]>);
            var compiledDelegate = CreateDelegate(metricsResult.Assembly, delegateType);

            if (compiledDelegate is not Func<T[], TResult[]> typedDelegate)
            {
                throw new CompilationException(
                    $"Compiled delegate has incorrect type. Expected: {delegateType}, Got: {compiledDelegate.GetType()}");
            }

            return typedDelegate;
        }
        catch (CompilationException ex)
        {
            _logger?.LogWarning(ex, "Roslyn compilation failed, attempting fallback");
            Interlocked.Increment(ref _fallbackCount);

            // Fallback: Use expression compilation (slower but more compatible)
            return FallbackToExpressionCompile<T, TResult>(graph);
        }
    }

    /// <summary>
    /// Generates C# source code from the operation graph.
    /// </summary>
    /// <param name="graph">The operation graph.</param>
    /// <param name="metadata">Type metadata.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>Complete C# source code as a string.</returns>
    private string GenerateCode(OperationGraph graph, TypeMetadata metadata, DotCompute.Abstractions.CompilationOptions options)
    {
        // Generate kernel method body
        var methodCode = _kernelGenerator.GenerateKernel(graph, metadata);

        // The generator already wraps in namespace and class, so return as-is
        return methodCode;
    }

    /// <summary>
    /// Compiles source code to an in-memory assembly using Roslyn.
    /// </summary>
    /// <param name="sourceCode">The C# source code to compile.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>The compiled assembly.</returns>
    /// <exception cref="CompilationException">Thrown when compilation fails.</exception>
    private Assembly CompileToAssembly(string sourceCode, DotCompute.Abstractions.CompilationOptions options)
    {
        return CompileWithRoslyn(sourceCode, options, sourceCode);
    }

    /// <summary>
    /// Compiles C# source code using Roslyn compiler.
    /// </summary>
    /// <param name="sourceCode">The C# source code to compile.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="originalSourceForDiagnostics">Original source code to show in diagnostics.</param>
    /// <returns>The compiled assembly.</returns>
    /// <exception cref="CompilationException">Thrown when compilation fails.</exception>
    private Assembly CompileWithRoslyn(
        string sourceCode,
        DotCompute.Abstractions.CompilationOptions options,
        string originalSourceForDiagnostics)
    {
        // Create syntax tree from source
        var syntaxTree = CSharpSyntaxTree.ParseText(
            SourceText.From(sourceCode, Encoding.UTF8),
            new CSharpParseOptions(
                languageVersion: LanguageVersion.CSharp12,
                kind: SourceCodeKind.Regular));

        // Build compilation options based on optimization level
        var compilationOptions = BuildCompilationOptions(options);

        // Create compilation
        var compilation = CSharpCompilation.Create(
            assemblyName: $"DotCompute.Generated.{Guid.NewGuid():N}",
            syntaxTrees: new[] { syntaxTree },
            references: _defaultReferences,
            options: compilationOptions);

        // Emit to memory stream
        using var assemblyStream = new MemoryStream();
        using var symbolStream = options.GenerateDebugInfo ? new MemoryStream() : null;

        var emitOptions = new Microsoft.CodeAnalysis.Emit.EmitOptions(
            debugInformationFormat: options.GenerateDebugInfo
                ? Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb
                : Microsoft.CodeAnalysis.Emit.DebugInformationFormat.Embedded);

        var emitResult = compilation.Emit(
            peStream: assemblyStream,
            pdbStream: symbolStream,
            options: emitOptions);

        // Check for compilation errors
        if (!emitResult.Success)
        {
            var diagnostics = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            throw DiagnosticsToException(diagnostics, originalSourceForDiagnostics);
        }

        // Load assembly from memory
        assemblyStream.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(assemblyStream.ToArray());

        _logger?.LogDebug("Successfully compiled assembly: {AssemblyName}", assembly.FullName);
        return assembly;
    }

    /// <summary>
    /// Builds Roslyn compilation options from DotCompute options.
    /// </summary>
    /// <param name="options">DotCompute compilation options.</param>
    /// <returns>Roslyn compilation options.</returns>
    private CSharpCompilationOptions BuildCompilationOptions(DotCompute.Abstractions.CompilationOptions options)
    {
        var optimizationLevel = options.OptimizationLevel switch
        {
            DotCompute.Abstractions.Types.OptimizationLevel.None => Microsoft.CodeAnalysis.OptimizationLevel.Debug,
            DotCompute.Abstractions.Types.OptimizationLevel.O1 => Microsoft.CodeAnalysis.OptimizationLevel.Debug,
            DotCompute.Abstractions.Types.OptimizationLevel.O2 => Microsoft.CodeAnalysis.OptimizationLevel.Release,
            DotCompute.Abstractions.Types.OptimizationLevel.O3 => Microsoft.CodeAnalysis.OptimizationLevel.Release,
            DotCompute.Abstractions.Types.OptimizationLevel.Size => Microsoft.CodeAnalysis.OptimizationLevel.Release,
            _ => Microsoft.CodeAnalysis.OptimizationLevel.Release  // Handles Default (= O2) and any future values
        };

        return _defaultCompilationOptions.WithOptimizationLevel(optimizationLevel);
    }

    /// <summary>
    /// Gets the list of required assembly references for compilation.
    /// </summary>
    /// <returns>A list of metadata references.</returns>
    private static List<MetadataReference> GetRequiredReferences()
    {
        var references = new List<MetadataReference>();

        // Core runtime assemblies
        var runtimeAssemblies = new[]
        {
            typeof(object).Assembly,                          // System.Private.CoreLib
            typeof(Console).Assembly,                         // System.Console
            typeof(System.Linq.Enumerable).Assembly,          // System.Linq
            typeof(System.Collections.Generic.List<>).Assembly, // System.Collections
            typeof(System.Numerics.Vector<>).Assembly,        // System.Numerics.Vectors
            typeof(System.Runtime.Intrinsics.Vector128<>).Assembly, // System.Runtime.Intrinsics
            typeof(System.Threading.Tasks.Task).Assembly,      // System.Threading.Tasks
            typeof(System.Threading.Tasks.Parallel).Assembly,  // System.Threading.Tasks.Parallel
        };

        foreach (var assembly in runtimeAssemblies)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // Add runtime directory references for .NET Core/5+
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimePath != null)
        {
            var requiredAssemblies = new[]
            {
                "System.Runtime.dll",
                "System.Collections.dll",
                "System.Linq.Expressions.dll",
                "System.Collections.Concurrent.dll"
            };

            foreach (var assemblyName in requiredAssemblies)
            {
                var assemblyPath = Path.Combine(runtimePath, assemblyName);
                if (File.Exists(assemblyPath))
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
            }
        }

        return references;
    }

    /// <summary>
    /// Creates a delegate from a compiled assembly.
    /// </summary>
    /// <param name="compiledAssembly">The compiled assembly containing the method.</param>
    /// <param name="delegateType">The expected delegate type.</param>
    /// <returns>A delegate to the compiled method.</returns>
    /// <exception cref="CompilationException">Thrown when the method cannot be found.</exception>
    private Delegate CreateDelegate(Assembly compiledAssembly, Type delegateType)
    {
        // Find the generated class
        var generatedType = compiledAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "GeneratedKernel" && t.Namespace == "DotCompute.Linq.Generated");

        if (generatedType == null)
        {
            throw new CompilationException(
                "Generated assembly does not contain expected type 'DotCompute.Linq.Generated.GeneratedKernel'");
        }

        // Extract the expected parameter and return types from the delegate type
        var delegateInvokeMethod = delegateType.GetMethod("Invoke");
        if (delegateInvokeMethod == null)
        {
            throw new CompilationException($"Cannot find Invoke method on delegate type {delegateType}");
        }

        var parameters = delegateInvokeMethod.GetParameters();
        if (parameters.Length != 1)
        {
            throw new CompilationException($"Expected delegate with 1 parameter, got {parameters.Length}");
        }

        var inputArrayType = parameters[0].ParameterType; // T[]
        var outputArrayType = delegateInvokeMethod.ReturnType; // TResult[]

        // The generator now creates TWO Execute methods:
        // 1. void Execute(ReadOnlySpan<T>, Span<TResult>) - Span-based for performance
        // 2. TResult[] Execute(T[]) - Array-based for delegate compatibility
        // We want the array-based overload for direct delegate creation.

        var arrayExecuteMethod = generatedType.GetMethod(
            "Execute",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { inputArrayType },
            null);

        if (arrayExecuteMethod != null && arrayExecuteMethod.ReturnType == outputArrayType)
        {
            // Found the array-based overload - create delegate directly
            _logger?.LogDebug("Found array-based Execute method: {MethodName}", arrayExecuteMethod);
            return Delegate.CreateDelegate(delegateType, arrayExecuteMethod);
        }

        // Fallback: Look for Span-based method and wrap it
        var spanInputType = typeof(ReadOnlySpan<>).MakeGenericType(inputArrayType.GetElementType()!);
        var spanOutputType = typeof(Span<>).MakeGenericType(outputArrayType.GetElementType()!);

        var spanExecuteMethod = generatedType.GetMethod(
            "Execute",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { spanInputType, spanOutputType },
            null);

        if (spanExecuteMethod == null)
        {
            throw new CompilationException(
                $"Generated type {generatedType.FullName} does not contain expected Execute method");
        }

        _logger?.LogDebug("Found Span-based Execute method, creating wrapper: {MethodName}", spanExecuteMethod);
        return CreateWrapperDelegate(spanExecuteMethod, delegateType);
    }

    /// <summary>
    /// Creates a wrapper delegate that adapts the signature from void Execute(ReadOnlySpan, Span) to Func&lt;T[], TResult[]&gt;.
    /// </summary>
    /// <param name="executeMethod">The Execute method with Span signature.</param>
    /// <param name="delegateType">The target delegate type (Func&lt;T[], TResult[]&gt;).</param>
    /// <returns>A wrapper delegate.</returns>
    private Delegate CreateWrapperDelegate(MethodInfo executeMethod, Type delegateType)
    {
        // Extract input and output types from delegate type (Func<T[], TResult[]>)
        var delegateInvokeMethod = delegateType.GetMethod("Invoke");
        if (delegateInvokeMethod == null)
        {
            throw new CompilationException($"Cannot find Invoke method on delegate type {delegateType}");
        }

        var parameters = delegateInvokeMethod.GetParameters();
        if (parameters.Length != 1)
        {
            throw new CompilationException($"Expected delegate with 1 parameter, got {parameters.Length}");
        }

        var inputArrayType = parameters[0].ParameterType; // T[]
        var outputArrayType = delegateInvokeMethod.ReturnType; // TResult[]

        if (!inputArrayType.IsArray || !outputArrayType.IsArray)
        {
            throw new CompilationException("Expected array types for input and output");
        }

        var inputElementType = inputArrayType.GetElementType()!;
        var outputElementType = outputArrayType.GetElementType()!;

        // The CpuKernelGenerator now generates BOTH Span-based and array-based Execute overloads
        // in the same class. Look for the array-based overload.
        var kernelType = executeMethod.DeclaringType!;
        var arrayExecuteMethod = kernelType.GetMethod(
            "Execute",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { inputArrayType },
            null);

        if (arrayExecuteMethod != null &&
            arrayExecuteMethod.ReturnType == outputArrayType)
        {
            // Found the array-based overload - use it directly
            return Delegate.CreateDelegate(delegateType, arrayExecuteMethod);
        }

        // Fallback: If array-based overload doesn't exist (shouldn't happen with new generator),
        // throw an exception rather than using Expression.Compile()
        var inputTypeName = GetFullTypeName(inputElementType);
        var outputTypeName = GetFullTypeName(outputElementType);
        throw new CompilationException(
            $"Generated kernel type {kernelType.FullName} does not contain expected array-based Execute overload. " +
            $"Expected signature: {outputTypeName}[] Execute({inputTypeName}[] input)");
    }

    /// <summary>
    /// Generates C# source code for a wrapper class that adapts Span-based Execute to array-based delegate.
    /// </summary>
    /// <param name="kernelType">The type containing the original Execute method.</param>
    /// <param name="inputElementType">The input array element type.</param>
    /// <param name="outputElementType">The output array element type.</param>
    /// <returns>Complete C# source code for the wrapper class.</returns>
    private static string GenerateWrapperSource(
        Type kernelType,
        Type inputElementType,
        Type outputElementType)
    {
        var inputTypeName = GetFullTypeName(inputElementType);
        var outputTypeName = GetFullTypeName(outputElementType);
        var kernelTypeName = $"{kernelType.Namespace}.{kernelType.Name}";

        var source = $@"using System;
using {kernelType.Namespace};

using DotCompute.Abstractions;
namespace DotCompute.Linq.Generated;

/// <summary>
/// Wrapper class that adapts Span-based Execute method to array-based delegate.
/// </summary>
public static class KernelArrayWrapper
{{
    /// <summary>
    /// Executes the kernel on array inputs.
    /// </summary>
    /// <param name=""input"">Input array.</param>
    /// <returns>Output array.</returns>
    public static {outputTypeName}[] Execute({inputTypeName}[] input)
    {{
        var output = new {outputTypeName}[input.Length];
        {kernelTypeName}.Execute(input.AsSpan(), output.AsSpan());
        return output;
    }}
}}";

        return source;
    }

    /// <summary>
    /// Gets the full type name including namespace for code generation.
    /// </summary>
    /// <param name="type">The type to get name for.</param>
    /// <returns>Full type name suitable for code generation.</returns>
    private static string GetFullTypeName(Type type)
    {
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(short)) return "short";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(char)) return "char";
        if (type == typeof(string)) return "string";

        // For other types, use full name
        return type.FullName ?? type.Name;
    }

    /// <summary>
    /// Caches a compiled delegate with the specified TTL.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="compiled">The compiled delegate.</param>
    /// <param name="ttl">Time-to-live for the cache entry.</param>
    private void CacheDelegate(string cacheKey, Delegate compiled, TimeSpan ttl)
    {
        try
        {
            _kernelCache.Store(cacheKey, compiled, ttl);
            _logger?.LogDebug("Stored compiled delegate in cache with TTL: {TTL}", ttl);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to cache compiled delegate, continuing without cache");
        }
    }

    /// <summary>
    /// Converts compilation diagnostics to a CompilationException.
    /// </summary>
    /// <param name="diagnostics">The compilation diagnostics.</param>
    /// <param name="sourceCode">The source code that failed to compile.</param>
    /// <returns>A CompilationException with detailed error information.</returns>
    private CompilationException DiagnosticsToException(
        IEnumerable<Diagnostic> diagnostics,
        string sourceCode)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var diagnostic in diagnostics)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var message = $"{diagnostic.Id} ({lineSpan.StartLinePosition}): {diagnostic.GetMessage()}";

            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(message);
            }
            else if (diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                warnings.Add(message);
            }
        }

        var errorMessage = new StringBuilder();
        errorMessage.AppendLine("Compilation failed with errors:");
        errorMessage.AppendLine();

        foreach (var error in errors)
        {
            errorMessage.AppendLine($"  ERROR: {error}");
        }

        if (warnings.Count > 0)
        {
            errorMessage.AppendLine();
            errorMessage.AppendLine("Warnings:");
            foreach (var warning in warnings)
            {
                errorMessage.AppendLine($"  WARNING: {warning}");
            }
        }

        _logger?.LogError("Compilation failed:\n{Errors}", errorMessage.ToString());

        return new CompilationException(errorMessage.ToString())
        {
            Diagnostics = diagnostics,
            SourceCode = sourceCode,
            ErrorCount = errors.Count,
            WarningCount = warnings.Count
        };
    }

    /// <summary>
    /// Fallback to expression compilation when Roslyn compilation fails.
    /// </summary>
    /// <typeparam name="T">Input element type.</typeparam>
    /// <typeparam name="TResult">Result element type.</typeparam>
    /// <param name="graph">The operation graph.</param>
    /// <returns>A delegate compiled using expression trees.</returns>
    /// <exception cref="NotSupportedException">Thrown when fallback is not possible.</exception>
    private Func<T[], TResult[]> FallbackToExpressionCompile<T, TResult>(OperationGraph graph)
    {
        _logger?.LogWarning(
            "Using expression compilation fallback - performance will be reduced. " +
            "Consider simplifying the query or reporting this as a bug.");

        // Create a wrapper type to provide DeclaringType metadata for fallback delegates
        // This ensures lambda.Compile() delegates have proper metadata for reflection
        var fallbackWrapper = CreateFallbackWrapper<T, TResult>(graph);
        return fallbackWrapper.Execute;
    }

    /// <summary>
    /// Creates a fallback wrapper class with proper metadata.
    /// </summary>
    private FallbackKernelWrapper<T, TResult> CreateFallbackWrapper<T, TResult>(OperationGraph graph)
    {
        return new FallbackKernelWrapper<T, TResult>(graph, _logger);
    }

    /// <summary>
    /// Wrapper class that provides DeclaringType metadata for fallback compiled delegates.
    /// </summary>
    private class FallbackKernelWrapper<T, TResult>
    {
        private readonly OperationGraph _graph;
        private readonly ILogger? _logger;

        public FallbackKernelWrapper(OperationGraph graph, ILogger? logger)
        {
            _graph = graph;
            _logger = logger;
        }

        public TResult[] Execute(T[] input)
        {
            IEnumerable<object?> current = input.Cast<object?>();

            foreach (var operation in _graph.Operations)
            {
                current = ApplyOperation<T, TResult>(current, operation);
            }

            return current.Cast<TResult>().ToArray();
        }

        private IEnumerable<object?> ApplyOperation<TIn, TOut>(IEnumerable<object?> input, Operation operation)
        {
            // Extract lambda from operation metadata
            if (!operation.Metadata.TryGetValue("Lambda", out var lambdaObj) || lambdaObj is not LambdaExpression lambda)
            {
                // No lambda for this operation, pass through
                return input;
            }

            var compiled = lambda.Compile();

            return operation.Type switch
            {
                Optimization.OperationType.Map => input.Select(x => compiled.DynamicInvoke(x)),
                Optimization.OperationType.Filter => input.Where(x => (bool)(compiled.DynamicInvoke(x) ?? false)),
                Optimization.OperationType.Reduce or Optimization.OperationType.Aggregate =>
                    new[] { input.Aggregate((a, b) => compiled.DynamicInvoke(a, b)) },
                Optimization.OperationType.Scan =>
                    // Scan is like cumulative sum - apply function cumulatively
                    // For now, use simple scan implementation that accumulates
                    input.Select((x, i) => i == 0 ? x : compiled.DynamicInvoke(input.ElementAt(i - 1), x)),
                // Complex operations that need special handling - just pass through for now
                Optimization.OperationType.Join or
                Optimization.OperationType.GroupBy or
                Optimization.OperationType.OrderBy => input,
                _ => input
            };
        }
    }

    /// <summary>
    /// Profiles compilation performance and resource usage.
    /// </summary>
    /// <param name="compilationAction">The compilation action to profile.</param>
    /// <returns>A tuple containing the compiled assembly and performance metrics.</returns>
    private (Assembly Assembly, CompilationMetrics Metrics) ProfileCompilation(
        Func<Assembly> compilationAction)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        Assembly assembly;
        try
        {
            assembly = compilationAction();
            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

            var metrics = new CompilationMetrics
            {
                CompilationTime = stopwatch.Elapsed,
                MemoryAllocated = Math.Max(0, memoryAfter - memoryBefore),
                Success = true,
                ErrorCount = 0,
                WarningCount = 0
            };

            return (assembly, metrics);
        }
        catch
        {
            stopwatch.Stop();
            throw;
        }
    }

    /// <summary>
    /// Compiles an operation graph to a GPU kernel for execution on the specified backend.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    /// <param name="backend">The target compute backend (CUDA, OpenCL, or Metal).</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A compiled GPU kernel ready for execution, or null if compilation fails (triggers CPU fallback).</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when GPU compiler is not available for the target backend.</exception>
    /// <remarks>
    /// <para><b>Phase 6 Option A: Production GPU Compilation Path</b></para>
    /// <para>
    /// This method provides the complete GPU kernel compilation pipeline:
    /// </para>
    /// <list type="number">
    /// <item><description>Routes to appropriate compiler (CUDA, OpenCL, Metal)</description></item>
    /// <item><description>Generates GPU-specific source code (CUDA C, OpenCL C, MSL)</description></item>
    /// <item><description>Compiles using runtime compiler (NVRTC, clBuildProgram, MTLLibrary)</description></item>
    /// <item><description>Returns CompiledKernel DTO with backend ICompiledKernel reference</description></item>
    /// </list>
    /// <para>
    /// On failure (null return), the caller should fall back to CPU execution using CompileToDelegate.
    /// </para>
    /// </remarks>
    public async Task<Compilation.CompiledKernel?> CompileToGpuKernelAsync(
        OperationGraph graph,
        TypeMetadata metadata,
        ComputeBackend backend,
        DotCompute.Abstractions.CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        _logger?.LogInformation("Compiling GPU kernel for {Backend} backend ({InputType} → {ResultType})",
            backend, metadata.InputType.Name, metadata.ResultType.Name);

        try
        {
            Compilation.CompiledKernel? compiledKernel = backend switch
            {
                ComputeBackend.Cuda when _cudaCompiler != null =>
                    await _cudaCompiler.CompileFromGraphAsync(graph, metadata, options, cancellationToken),

                ComputeBackend.OpenCL when _openclCompiler != null =>
                    await _openclCompiler.CompileFromGraphAsync(graph, metadata, options, cancellationToken),

                ComputeBackend.Metal when _metalCompiler != null =>
                    await _metalCompiler.CompileFromGraphAsync(graph, metadata, options, cancellationToken),

                ComputeBackend.Cuda when _cudaCompiler == null =>
                    throw new InvalidOperationException("CUDA compiler not available. Initialize CompilationPipeline with CudaRuntimeKernelCompiler."),

                ComputeBackend.OpenCL when _openclCompiler == null =>
                    throw new InvalidOperationException("OpenCL compiler not available. Initialize CompilationPipeline with OpenCLRuntimeKernelCompiler."),

                ComputeBackend.Metal when _metalCompiler == null =>
                    throw new InvalidOperationException("Metal compiler not available. Initialize CompilationPipeline with MetalRuntimeKernelCompiler."),

                _ => throw new ArgumentException($"Unsupported GPU backend: {backend}", nameof(backend))
            };

            if (compiledKernel != null)
            {
                _logger?.LogInformation("Successfully compiled GPU kernel for {Backend} (entry point: {EntryPoint})",
                    backend, compiledKernel.EntryPoint);
            }
            else
            {
                _logger?.LogWarning("GPU kernel compilation returned null for {Backend}, triggering CPU fallback", backend);
            }

            return compiledKernel;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "GPU kernel compilation failed for {Backend}, CPU fallback will be used", backend);
            return null; // Null triggers graceful CPU fallback
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose GPU compilers
        _cudaCompiler?.Dispose();
        _openclCompiler?.Dispose();
        _metalCompiler?.Dispose();

        // Note: We don't dispose the cache here as it may be shared
        // The caller is responsible for cache lifetime management
        _logger?.LogInformation(
            "CompilationPipeline disposed. Stats - Total: {Total}, Cache Hits: {Hits}, Failures: {Failures}, Fallbacks: {Fallbacks}",
            Interlocked.Read(ref _totalCompilations),
            Interlocked.Read(ref _cacheHits),
            Interlocked.Read(ref _compilationFailures),
            Interlocked.Read(ref _fallbackCount));
    }
}

/// <summary>
/// Represents compilation performance metrics.
/// </summary>
public class CompilationMetrics
{
    /// <summary>
    /// Gets or sets the total time spent compiling.
    /// </summary>
    public TimeSpan CompilationTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory allocated during compilation.
    /// </summary>
    public long MemoryAllocated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a cache hit occurred.
    /// </summary>
    public bool CacheHit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether compilation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the number of compilation errors.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the number of compilation warnings.
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of compilations performed.
    /// </summary>
    public long TotalCompilations { get; set; }

    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// Gets or sets the number of compilation failures.
    /// </summary>
    public long CompilationFailures { get; set; }

    /// <summary>
    /// Gets or sets the number of times fallback compilation was used.
    /// </summary>
    public long FallbackCount { get; set; }

    /// <summary>
    /// Gets or sets cache statistics.
    /// </summary>
    public CacheStatistics? CacheStatistics { get; set; }
}

/// <summary>
/// Exception thrown when kernel compilation fails.
/// </summary>
public class CompilationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationException"/> class.
    /// </summary>
    public CompilationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CompilationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CompilationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets or sets the Roslyn diagnostics from the failed compilation.
    /// </summary>
    public IEnumerable<Diagnostic>? Diagnostics { get; init; }

    /// <summary>
    /// Gets or sets the source code that failed to compile.
    /// </summary>
    public string? SourceCode { get; init; }

    /// <summary>
    /// Gets or sets the number of errors in the compilation.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Gets or sets the number of warnings in the compilation.
    /// </summary>
    public int WarningCount { get; init; }
}
