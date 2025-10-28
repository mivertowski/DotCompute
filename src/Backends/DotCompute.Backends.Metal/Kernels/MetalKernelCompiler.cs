// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Types;
using ValidationResult = DotCompute.Abstractions.Validation.UnifiedValidationResult;
// Fully qualified type names used to avoid ambiguity with Metal-specific UnifiedValidationResult

using DotCompute.Abstractions.Kernels;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using DotCompute.Abstractions.Kernels.Types;
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Metal backend has dynamic logging requirements

namespace DotCompute.Backends.Metal.Kernels;


/// <summary>
/// Compiles kernels to Metal Shading Language and creates compute pipeline states.
/// </summary>
public sealed partial class MetalKernelCompiler : IUnifiedKernelCompiler, IDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly ILogger _logger;
    private readonly MetalCommandBufferPool? _commandBufferPool;
    private readonly MetalKernelCache _kernelCache;
    private readonly SemaphoreSlim _compilationSemaphore = new(1, 1);
    private int _disposed;

    [GeneratedRegex(@"kernel\s+void\s+(\w+)\s*[<(]", RegexOptions.Multiline)]
    private static partial Regex KernelFunctionNamePattern();


    public MetalKernelCompiler(
        IntPtr device,

        IntPtr commandQueue,

        ILogger logger,

        MetalCommandBufferPool? commandBufferPool = null,
        MetalKernelCache? kernelCache = null)
    {
        _device = device;
        _commandQueue = commandQueue;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commandBufferPool = commandBufferPool;

        // Create or use provided kernel cache

        if (kernelCache != null)
        {
            _kernelCache = kernelCache;
        }
        else
        {
            var cacheLogger = logger is ILogger<MetalKernelCache> cacheTypedLogger
                ? cacheTypedLogger

                : new LoggerFactory().CreateLogger<MetalKernelCache>();


            _kernelCache = new MetalKernelCache(
                cacheLogger,
                maxCacheSize: 500,
                defaultTtl: TimeSpan.FromHours(2),
                persistentCachePath: Path.Combine(Path.GetTempPath(), "DotCompute", "MetalCache"));
        }
    }

    /// <inheritdoc/>
    public string Name => "Metal Shader Compiler";





    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays - Required by IUnifiedKernelCompiler interface
    public IReadOnlyList<KernelLanguage> SupportedSourceTypes { get; } = new List<KernelLanguage>
    {
        KernelLanguage.Metal,
        KernelLanguage.Binary
    }.AsReadOnly();
#pragma warning restore CA1819

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Capabilities { get; } = new Dictionary<string, object>
    {
        ["SupportsAsync"] = true,
        ["SupportsOptimization"] = true,
        ["SupportsCaching"] = true,
        ["SupportsValidation"] = true,
        ["SupportedLanguageVersions"] = new[] { "Metal 2.0", "Metal 2.1", "Metal 2.2", "Metal 2.3", "Metal 2.4" }
    }.AsReadOnly();

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        var validation = Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Kernel validation failed: {validation.ErrorMessage ?? "Unknown validation error"}");
        }

        // Check the cache first

        if (_kernelCache.TryGetKernel(definition, options, out _, out _, out var cachedPipelineState))
        {
            _logger.LogDebug("Cache hit for kernel '{Name}' - using cached pipeline state", definition.Name);

            // Get metadata from cached pipeline state

            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(cachedPipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidthTuple(cachedPipelineState);


            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = 0, // No compilation time for cached kernel
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { "Kernel loaded from cache" }
            };


            return (ICompiledKernel)new MetalCompiledKernel(
                definition,
                cachedPipelineState,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger,
                _commandBufferPool);
        }

        await _compilationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Generate or extract Metal code
            var metalCode = ExtractMetalCode(definition);

            // Determine the actual function name to use
            string functionName;
            if (definition.EntryPoint == "main")
            {
                // EntryPoint is the default "main", try to extract actual function name from Metal code
                var extractedName = ExtractKernelFunctionName(metalCode);
                functionName = extractedName ?? definition.Name;
                _logger.LogDebug("Extracted kernel function name '{FunctionName}' from Metal code", functionName);
            }
            else
            {
                // Use explicitly set EntryPoint or fallback to Name
                functionName = definition.EntryPoint ?? definition.Name;
            }

            // Compile Metal code
            _logger.LogDebug("Compiling kernel '{Name}' from source", definition.Name);
            var library = await CompileMetalCodeAsync(metalCode, definition.Name, options, cancellationToken).ConfigureAwait(false);

            // Create function and pipeline state
            var function = MetalNative.GetFunction(library, functionName);
            if (function == IntPtr.Zero)
            {
                MetalNative.ReleaseLibrary(library);
                throw new InvalidOperationException($"Failed to get function '{functionName}' from Metal library");
            }


            var pipelineState = MetalNative.CreateComputePipelineState(_device, function);
            if (pipelineState == IntPtr.Zero)
            {
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                throw new InvalidOperationException($"Failed to create compute pipeline state for kernel '{definition.Name}'");
            }


            stopwatch.Stop();
            var compilationTimeMs = (long)stopwatch.Elapsed.TotalMilliseconds;

            // Add to cache

            byte[]? binaryData = null;
            try
            {
                // Try to get binary data for persistent caching
                var dataSize = MetalNative.GetLibraryDataSize(library);
                if (dataSize > 0)
                {
                    binaryData = new byte[dataSize];
                    var handle = GCHandle.Alloc(binaryData, GCHandleType.Pinned);
                    try
                    {
                        _ = MetalNative.GetLibraryData(library, handle.AddrOfPinnedObject(), dataSize);
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract binary data for kernel '{Name}'", definition.Name);
            }


            _kernelCache.AddKernel(definition, options, library, function, pipelineState, binaryData, compilationTimeMs);
            _logger.LogInformation("Compiled and cached kernel '{Name}' in {Time}ms", definition.Name, compilationTimeMs);

            // Get metadata

            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(pipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidthTuple(pipelineState);


            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = compilationTimeMs,
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { $"Max threads per threadgroup: {maxThreadsPerThreadgroup}" }
            };

            // Release function reference (pipeline state retains it)

            MetalNative.ReleaseFunction(function);


            return (ICompiledKernel)new MetalCompiledKernel(
                definition,
                pipelineState,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger,
                _commandBufferPool);
        }
        finally
        {
            _ = _compilationSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public ValidationResult Validate(KernelDefinition definition)
    {
        if (definition == null)
        {
            return ValidationResult.Failure("Kernel definition cannot be null");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return ValidationResult.Failure("Kernel name cannot be empty");
        }

        if (definition.Code == null || definition.Code.Length == 0)
        {
            return ValidationResult.Failure("Kernel code cannot be empty");
        }

        // Check if this looks like Metal code or binary
        var codeString = definition.Code ?? string.Empty;
        if (!codeString.Contains("kernel", StringComparison.Ordinal) && !codeString.Contains("metal", StringComparison.Ordinal) && !IsBinaryCode(Encoding.UTF8.GetBytes(codeString)))
        {
            return ValidationResult.Failure("Code does not appear to be valid Metal shader language or compiled binary");
        }

        return ValidationResult.Success();
    }

    private static string? ExtractKernelFunctionName(string metalCode)
    {
        // Extract kernel function name from Metal code using regex
        // Matches patterns like: "kernel void function_name(" or "kernel void function_name<T>("
        var match = KernelFunctionNamePattern().Match(metalCode);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ExtractMetalCode(KernelDefinition definition)
    {
        // If it's binary code, we'll need to handle it differently
        if (definition.Code != null && IsBinaryCode(Encoding.UTF8.GetBytes(definition.Code)))
        {
            throw new NotSupportedException("Pre-compiled Metal binaries are not yet supported");
        }

        // Get the source code
        var code = definition.Code ?? string.Empty;

        // Check if this is already MSL code (generated from C# or handwritten)
        if (code.Contains("#include <metal_stdlib>", StringComparison.Ordinal) ||
            code.Contains("kernel void", StringComparison.Ordinal))
        {
            // Already Metal code, return as-is
            return code;
        }

        // Check if the language is specified as Metal

        if (definition.Language == KernelLanguage.Metal)
        {
            // If Metal is specified but headers are missing, add them
            if (!code.Contains("#include <metal_stdlib>", StringComparison.Ordinal))
            {
                var sb = new StringBuilder();
                _ = sb.AppendLine("#include <metal_stdlib>");
                _ = sb.AppendLine("#include <metal_compute>");
                _ = sb.AppendLine("using namespace metal;");
                _ = sb.AppendLine();
                _ = sb.Append(code);
                return sb.ToString();
            }
            return code;
        }

        // If it's OpenCL C or unspecified, we would need translation
        // For now, throw an error indicating MSL is required
        if (code.Contains("__kernel", StringComparison.Ordinal) ||
            code.Contains("__global", StringComparison.Ordinal) ||
            definition.Language == KernelLanguage.OpenCL)
        {
            throw new NotSupportedException(
                "OpenCL C to Metal Shading Language translation is not implemented. " +
                "Please provide Metal Shading Language code directly or use the [Kernel] attribute " +
                "to generate Metal kernels from C# code.");
        }

        // Check if this looks like C# kernel code (from [Kernel] attribute)
        if (code.Contains("Kernel.ThreadId", StringComparison.Ordinal) ||
            code.Contains("ThreadId.X", StringComparison.Ordinal) ||
            (code.Contains("ReadOnlySpan<", StringComparison.Ordinal) && code.Contains("Span<", StringComparison.Ordinal)))
        {
            // Translate C# to MSL (this is an instance method)
            throw new NotSupportedException(
                "C# to Metal Shading Language translation is not fully implemented. " +
                "Please provide Metal Shading Language code directly or use a pre-compiled kernel.");
        }

        // Try to add Metal headers if the code looks like it might be Metal
        var sb2 = new StringBuilder();
        _ = sb2.AppendLine("#include <metal_stdlib>");
        _ = sb2.AppendLine("#include <metal_compute>");
        _ = sb2.AppendLine("using namespace metal;");
        _ = sb2.AppendLine();
        _ = sb2.Append(code);
        return sb2.ToString();
    }

    /// <summary>
    /// Translates C# kernel code (from [Kernel] attribute) to Metal Shading Language.
    /// Maps C# constructs to their Metal equivalents for GPU execution.
    /// </summary>
    /// <param name="csharpCode">The C# kernel code to translate</param>
    /// <param name="kernelName">Name of the kernel for error reporting</param>
    /// <param name="entryPoint">Entry point function name</param>
    /// <returns>Metal Shading Language code ready for compilation</returns>
    private string TranslateFromCSharp(string csharpCode, string kernelName, string entryPoint)
    {
        try
        {
            _logger.LogDebug("Translating C# kernel '{Name}' to Metal Shading Language", kernelName);

            var mslCode = new StringBuilder();

            // Add Metal standard headers
            _ = mslCode.AppendLine("#include <metal_stdlib>");
            _ = mslCode.AppendLine("#include <metal_compute>");
            _ = mslCode.AppendLine("using namespace metal;");
            _ = mslCode.AppendLine();
            _ = mslCode.AppendLine(CultureInfo.InvariantCulture, $"// Auto-generated Metal kernel: {kernelName}");
            _ = mslCode.AppendLine(CultureInfo.InvariantCulture, $"// Translated from C# on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            _ = mslCode.AppendLine();

            // Parse and translate the C# code
            var translatedBody = TranslateCSharpToMetal(csharpCode, kernelName, entryPoint);
            _ = mslCode.Append(translatedBody);

            var result = mslCode.ToString();
            _logger.LogInformation("Successfully translated C# kernel '{Name}' to Metal ({Bytes} bytes)",
                kernelName, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate C# kernel '{Name}' to Metal", kernelName);
            throw new InvalidOperationException(
                $"C# to Metal translation failed for kernel '{kernelName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Core translation logic that converts C# syntax to Metal Shading Language.
    /// Handles type mapping, threading model, and Metal-specific constructs.
    /// </summary>
    private static string TranslateCSharpToMetal(string csharpCode, string kernelName, string entryPoint)
    {
        var msl = new StringBuilder();

        // Extract method signature and body
        var methodStart = csharpCode.IndexOf("public static void", StringComparison.Ordinal);
        if (methodStart == -1)
        {
            methodStart = csharpCode.IndexOf("static void", StringComparison.Ordinal);
        }

        if (methodStart == -1)
        {
            throw new InvalidOperationException("Could not find method declaration in C# code");
        }

        // Extract everything from method declaration to the end
        var methodCode = csharpCode[methodStart..];

        // Parse parameters
        var paramStart = methodCode.IndexOf('(', StringComparison.Ordinal);
        var paramEnd = FindMatchingCloseParen(methodCode, paramStart);
        var parameters = methodCode.Substring(paramStart + 1, paramEnd - paramStart - 1);

        // Parse body
        var bodyStart = methodCode.IndexOf('{', paramEnd);
        var bodyEnd = FindMatchingCloseBrace(methodCode, bodyStart);
        var body = methodCode.Substring(bodyStart + 1, bodyEnd - bodyStart - 1);

        // Translate parameters
        var metalParams = TranslateParameters(parameters);

        // Build Metal kernel signature
        _ = msl.AppendLine(CultureInfo.InvariantCulture, $"kernel void {entryPoint}(");
        _ = msl.Append(metalParams);
        _ = msl.AppendLine(",");
        _ = msl.AppendLine("    uint3 thread_position_in_grid [[thread_position_in_grid]],");
        _ = msl.AppendLine("    uint3 threads_per_threadgroup [[threads_per_threadgroup]],");
        _ = msl.AppendLine("    uint3 threadgroup_position_in_grid [[threadgroup_position_in_grid]])");
        _ = msl.AppendLine("{");

        // Translate body
        var translatedBody = TranslateMethodBody(body);
        _ = msl.Append(translatedBody);

        _ = msl.AppendLine("}");

        return msl.ToString();
    }

    /// <summary>
    /// Translates C# parameters to Metal kernel parameters.
    /// Maps Span&lt;T&gt; and ReadOnlySpan&lt;T&gt; to Metal device pointers.
    /// </summary>
    private static string TranslateParameters(string parameters)
    {
        var result = new StringBuilder();
        var paramList = parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < paramList.Length; i++)
        {
            var param = paramList[i].Trim();
            if (string.IsNullOrWhiteSpace(param))
            {
                continue;
            }

            // Parse parameter type and name
            var parts = param.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var type = parts[0];
            var name = parts[^1]; // Last part is the name

            // Translate type
            var metalType = TranslateType(type, out var isReadOnly);

            // Add buffer attribute and parameter
            var bufferIndex = i;
            var accessMode = isReadOnly ? "const" : "";

            if (i > 0)
            {
                _ = result.Append(",\n");
            }

            _ = result.Append(CultureInfo.InvariantCulture, $"    {accessMode} device {metalType}* {name} [[buffer({bufferIndex})]]");
        }

        return result.ToString();
    }

    /// <summary>
    /// Translates C# types to Metal types.
    /// Handles Span&lt;T&gt;, ReadOnlySpan&lt;T&gt;, and primitive types.
    /// </summary>
    private static string TranslateType(string csharpType, out bool isReadOnly)
    {
        isReadOnly = false;

        // Handle Span<T> and ReadOnlySpan<T>
        if (csharpType.StartsWith("ReadOnlySpan<", StringComparison.Ordinal))
        {
            isReadOnly = true;
            var innerType = csharpType.Substring(13, csharpType.Length - 14); // Extract T from ReadOnlySpan<T>
            return TranslatePrimitiveType(innerType);
        }

        if (csharpType.StartsWith("Span<", StringComparison.Ordinal))
        {
            var innerType = csharpType.Substring(5, csharpType.Length - 6); // Extract T from Span<T>
            return TranslatePrimitiveType(innerType);
        }

        // Handle arrays
        if (csharpType.EndsWith("[]", StringComparison.Ordinal))
        {
            var innerType = csharpType[..^2];
            return TranslatePrimitiveType(innerType);
        }

        // Primitive types
        return TranslatePrimitiveType(csharpType);
    }

    /// <summary>
    /// Maps C# primitive types to Metal types.
    /// </summary>
    private static string TranslatePrimitiveType(string csharpType)
    {
        return csharpType.Trim() switch
        {
            "float" => "float",
            "double" => "double",
            "int" => "int",
            "uint" => "uint",
            "short" => "short",
            "ushort" => "ushort",
            "byte" => "uchar",
            "sbyte" => "char",
            "long" => "long",
            "ulong" => "ulong",
            "bool" => "bool",
            "half" => "half",  // Metal-specific half precision
            _ => csharpType // Pass through unknown types
        };
    }

    /// <summary>
    /// Translates the C# method body to Metal Shading Language.
    /// Handles threading model, math functions, and control flow.
    /// </summary>
    private static string TranslateMethodBody(string body)
    {
        var msl = new StringBuilder();
        var lines = body.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _ = msl.AppendLine();
                continue;
            }

            // Translate the line
            var translatedLine = TranslateLine(trimmed);

            // Preserve indentation
            var indent = line.Length - line.TrimStart().Length;
            _ = msl.Append(new string(' ', indent));
            _ = msl.AppendLine(translatedLine);
        }

        return msl.ToString();
    }

    /// <summary>
    /// Translates a single line of C# code to Metal.
    /// Handles thread ID mapping, math functions, and atomics.
    /// </summary>
    private static string TranslateLine(string line)
    {
        var result = line;

        // ===== THREAD ID MAPPING =====
        // Map Kernel.ThreadId.X/Y/Z to thread_position_in_grid.x/y/z
        result = result.Replace("Kernel.ThreadId.X", "thread_position_in_grid.x", StringComparison.Ordinal);
        result = result.Replace("Kernel.ThreadId.Y", "thread_position_in_grid.y", StringComparison.Ordinal);
        result = result.Replace("Kernel.ThreadId.Z", "thread_position_in_grid.z", StringComparison.Ordinal);

        // Also handle shorthand ThreadId.X (without Kernel. prefix)
        result = result.Replace("ThreadId.X", "thread_position_in_grid.x", StringComparison.Ordinal);
        result = result.Replace("ThreadId.Y", "thread_position_in_grid.y", StringComparison.Ordinal);
        result = result.Replace("ThreadId.Z", "thread_position_in_grid.z", StringComparison.Ordinal);

        // ===== MATH FUNCTIONS =====
        // Map C# Math functions to Metal metal:: namespace
        result = result.Replace("Math.Sqrt(", "metal::sqrt(", StringComparison.Ordinal);
        result = result.Replace("Math.Abs(", "metal::abs(", StringComparison.Ordinal);
        result = result.Replace("Math.Sin(", "metal::sin(", StringComparison.Ordinal);
        result = result.Replace("Math.Cos(", "metal::cos(", StringComparison.Ordinal);
        result = result.Replace("Math.Tan(", "metal::tan(", StringComparison.Ordinal);
        result = result.Replace("Math.Exp(", "metal::exp(", StringComparison.Ordinal);
        result = result.Replace("Math.Log(", "metal::log(", StringComparison.Ordinal);
        result = result.Replace("Math.Pow(", "metal::pow(", StringComparison.Ordinal);
        result = result.Replace("Math.Floor(", "metal::floor(", StringComparison.Ordinal);
        result = result.Replace("Math.Ceil(", "metal::ceil(", StringComparison.Ordinal);
        result = result.Replace("Math.Round(", "metal::round(", StringComparison.Ordinal);
        result = result.Replace("Math.Min(", "metal::min(", StringComparison.Ordinal);
        result = result.Replace("Math.Max(", "metal::max(", StringComparison.Ordinal);

        // MathF functions (single precision)
        result = result.Replace("MathF.Sqrt(", "metal::sqrt(", StringComparison.Ordinal);
        result = result.Replace("MathF.Abs(", "metal::abs(", StringComparison.Ordinal);
        result = result.Replace("MathF.Sin(", "metal::sin(", StringComparison.Ordinal);
        result = result.Replace("MathF.Cos(", "metal::cos(", StringComparison.Ordinal);
        result = result.Replace("MathF.Tan(", "metal::tan(", StringComparison.Ordinal);
        result = result.Replace("MathF.Exp(", "metal::exp(", StringComparison.Ordinal);
        result = result.Replace("MathF.Log(", "metal::log(", StringComparison.Ordinal);
        result = result.Replace("MathF.Pow(", "metal::pow(", StringComparison.Ordinal);
        result = result.Replace("MathF.Floor(", "metal::floor(", StringComparison.Ordinal);
        result = result.Replace("MathF.Ceil(", "metal::ceil(", StringComparison.Ordinal);
        result = result.Replace("MathF.Round(", "metal::round(", StringComparison.Ordinal);
        result = result.Replace("MathF.Min(", "metal::min(", StringComparison.Ordinal);
        result = result.Replace("MathF.Max(", "metal::max(", StringComparison.Ordinal);

        // ===== ATOMIC OPERATIONS =====
        // Map C# Interlocked to Metal atomic operations
        result = result.Replace("Interlocked.Add(", "atomic_fetch_add_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Increment(", "atomic_fetch_add_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Decrement(", "atomic_fetch_sub_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Exchange(", "atomic_exchange_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.CompareExchange(", "atomic_compare_exchange_weak_explicit(", StringComparison.Ordinal);

        // Add memory order for atomics (relaxed by default, can be configured)
        if (result.Contains("atomic_", StringComparison.Ordinal))
        {
            // Add memory_order_relaxed if not already specified
            if (!result.Contains("memory_order", StringComparison.Ordinal))
            {
                result = result.Replace(");", ", memory_order_relaxed);", StringComparison.Ordinal);
            }
        }

        // ===== SYNCHRONIZATION =====
        // Map C# barrier to Metal threadgroup_barrier
        result = result.Replace("Barrier()", "threadgroup_barrier(mem_flags::mem_device)", StringComparison.Ordinal);
        result = result.Replace("Barrier.Sync()", "threadgroup_barrier(mem_flags::mem_device)", StringComparison.Ordinal);

        // ===== TYPE CONVERSIONS =====
        // Remove .Length property access (handled by parameters)
        // Keep array indexing but note that bounds are passed separately

        return result;
    }

    /// <summary>
    /// Finds the matching closing parenthesis for an opening one.
    /// </summary>
    private static int FindMatchingCloseParen(string text, int openIndex)
    {
        var count = 1;
        for (var i = openIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                count++;
            }
            else if (text[i] == ')')
            {
                count--;
                if (count == 0)
                {
                    return i;
                }
            }
        }
        throw new InvalidOperationException("No matching closing parenthesis found");
    }

    /// <summary>
    /// Finds the matching closing brace for an opening one.
    /// </summary>
    private static int FindMatchingCloseBrace(string text, int openIndex)
    {
        var count = 1;
        for (var i = openIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                count++;
            }
            else if (text[i] == '}')
            {
                count--;
                if (count == 0)
                {
                    return i;
                }
            }
        }
        throw new InvalidOperationException("No matching closing brace found");
    }

    private static bool IsBinaryCode(byte[] code)
    {
        // Check for common binary signatures
        if (code.Length < 4)
        {
            return false;
        }

        // Metal library magic number
        return code[0] == 0x4D && code[1] == 0x54 && code[2] == 0x4C && code[3] == 0x42;
    }

    private async Task<IntPtr> CompileMetalCodeAsync(string code, string kernelName, CompilationOptions options, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            // Create compile options
            var compileOptions = MetalNative.CreateCompileOptions();

            // Configure optimization settings
            ConfigureOptimizationOptions(compileOptions, options, kernelName);

            // Set language version based on system capabilities
            var languageVersion = GetOptimalLanguageVersion();
            MetalNative.SetCompileOptionsLanguageVersion(compileOptions, languageVersion);

            try
            {
                // Compile the code
                var error = IntPtr.Zero;
                var library = MetalNative.CompileLibrary(_device, code, compileOptions, ref error);

                if (library == IntPtr.Zero)
                {
                    var errorMessage = error != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(error)) ?? "Unknown error"
                        : "Failed to compile Metal library";

                    if (error != IntPtr.Zero)
                    {
                        MetalNative.ReleaseError(error);
                    }

                    throw new InvalidOperationException($"Metal compilation failed: {errorMessage}");
                }

                _logger.LogDebug("Compiler kernel compiled: {Name}", kernelName);
                return library;
            }
            finally
            {
                MetalNative.ReleaseCompileOptions(compileOptions);
            }
        }, cancellationToken).ConfigureAwait(false);
    }


    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Dispose the kernel cache (logs statistics)
        _kernelCache?.Dispose();

        _compilationSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ConfigureOptimizationOptions(IntPtr compileOptions, CompilationOptions options, string kernelName)
    {
        // Configure fast math based on optimization level
        var enableFastMath = options.OptimizationLevel >= OptimizationLevel.Default;
        if (options.FastMath || enableFastMath)
        {
            MetalNative.SetCompileOptionsFastMath(compileOptions, true);
            _logger.LogDebug("Fast math optimization enabled for kernel: {Name}", kernelName);
        }

        // Additional optimization hints could be set here based on the options
        if (options.OptimizationLevel == OptimizationLevel.O3)
        {
            _logger.LogDebug("Maximum optimization requested for kernel: {Name}", kernelName);
            // Metal doesn't expose many additional optimization flags through the public API
            // but we can log this for debugging purposes
        }
    }

    private static MetalLanguageVersion GetOptimalLanguageVersion()
    {
        // Determine the best Metal language version based on system capabilities
        try
        {
            var osVersion = Environment.OSVersion.Version;

            // macOS 13.0+ supports Metal 3.1
            if (osVersion.Major >= 13 || (osVersion.Major == 13 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal31;
            }

            // macOS 12.0+ supports Metal 3.0
            if (osVersion.Major >= 12 || (osVersion.Major == 12 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal30;
            }

            // macOS 11.0+ supports Metal 2.4
            if (osVersion.Major >= 11 || (osVersion.Major == 11 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal24;
            }

            // macOS 10.15+ supports Metal 2.3
            if (osVersion.Major >= 10 && osVersion.Minor >= 15)
            {
                return MetalLanguageVersion.Metal23;
            }

            // macOS 10.14+ supports Metal 2.1
            if (osVersion.Major >= 10 && osVersion.Minor >= 14)
            {
                return MetalLanguageVersion.Metal21;
            }

            // macOS 10.13+ supports Metal 2.0
            if (osVersion.Major >= 10 && osVersion.Minor >= 13)
            {
                return MetalLanguageVersion.Metal20;
            }

            // Fallback to Metal 1.2 for older systems
            return MetalLanguageVersion.Metal12;
        }
        catch
        {
            // If we can't determine OS version, use a safe default
            return MetalLanguageVersion.Metal20;
        }
    }


    /// <inheritdoc/>
    public ValueTask<ValidationResult> ValidateAsync(KernelDefinition kernel, CancellationToken cancellationToken = default) => ValueTask.FromResult(Validate(kernel));

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> OptimizeAsync(ICompiledKernel kernel, OptimizationLevel level, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        // Only optimize Metal compiled kernels
        if (kernel is not MetalCompiledKernel metalKernel)
        {
            _logger.LogWarning("Cannot optimize non-Metal kernel '{Name}'", kernel.Name);
            return kernel;
        }

        // Skip optimization if level is None
        if (level == OptimizationLevel.None)
        {
            _logger.LogDebug("Skipping optimization for kernel '{Name}' (level: None)", kernel.Name);
            return kernel;
        }

        try
        {
            _logger.LogInformation("Optimizing Metal kernel '{Name}' with level {Level}", kernel.Name, level);

            // Create kernel definition from the compiled kernel
            var definition = new KernelDefinition(kernel.Name, metalKernel.SourceCode ?? string.Empty)
            {
                EntryPoint = kernel.Name,
                Language = KernelLanguage.Metal
            };

            // Create optimizer instance
            var optimizer = new MetalKernelOptimizer(_device, _logger);

            // Perform optimization
            var (optimizedDefinition, telemetry) = await optimizer.OptimizeAsync(definition, level, cancellationToken).ConfigureAwait(false);

            // Log optimization results
            _logger.LogInformation(
                "Kernel '{Name}' optimization completed in {Time}ms. Applied {Count} optimizations. " +
                "Threadgroup size: {Original} -> {Optimized}",
                telemetry.KernelName,
                telemetry.OptimizationTimeMs,
                telemetry.AppliedOptimizations.Count,
                telemetry.OriginalThreadgroupSize,
                telemetry.OptimizedThreadgroupSize);

            // Recompile the optimized kernel
            var compilationOptions = new CompilationOptions
            {
                OptimizationLevel = level,
                EnableDebugInfo = level == OptimizationLevel.None,
                FastMath = level >= OptimizationLevel.Default
            };

            var optimizedKernel = await CompileAsync(optimizedDefinition, compilationOptions, cancellationToken).ConfigureAwait(false);

            // Add optimization metadata to the compiled kernel
            if (optimizedKernel is MetalCompiledKernel optimizedMetalKernel)
            {
                var metadata = optimizedMetalKernel.CompilationMetadata;
                metadata.Warnings.Add($"Optimization profile: {telemetry.Profile}");
                metadata.Warnings.Add($"Applied {telemetry.AppliedOptimizations.Count} optimizations");

                if (telemetry.HasThreadgroupOptimization)
                {
                    metadata.Warnings.Add($"Threadgroup size optimized: {telemetry.OriginalThreadgroupSize} -> {telemetry.OptimizedThreadgroupSize}");
                }

                if (telemetry.HasMemoryCoalescing)
                {
                    metadata.Warnings.Add("Memory coalescing optimizations applied");
                }
            }

            return optimizedKernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize Metal kernel '{Name}'", kernel.Name);

            // Return original kernel on optimization failure
            return kernel;
        }
    }

    // Backward compatibility methods for legacy IKernelCompiler interface

    /// <inheritdoc />
    public async Task<ICompiledKernel> CompileAsync(
        KernelDefinition kernelDefinition,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinition);
        ArgumentNullException.ThrowIfNull(accelerator);

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType
        };

        return await CompileAsync(kernelDefinition, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> CanCompileAsync(KernelDefinition kernelDefinition, IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinition);
        ArgumentNullException.ThrowIfNull(accelerator);

        try
        {
            var validationResult = Validate(kernelDefinition);
            return Task.FromResult(validationResult.IsValid);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public CompilationOptions GetSupportedOptions(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);

        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType,
            AllowUnsafeCode = false, // Metal doesn't support unsafe code
            PreferredBlockSize = new Dim3(64, 1, 1), // Good default for Metal
            SharedMemorySize = 32 * 1024 // 32KB default for Metal threadgroup memory
        };
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, ICompiledKernel>> BatchCompileAsync(
        IEnumerable<KernelDefinition> kernelDefinitions,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinitions);
        ArgumentNullException.ThrowIfNull(accelerator);

        var results = new Dictionary<string, ICompiledKernel>();

        // Metal can benefit from batch compilation by building a single library
        foreach (var kernelDef in kernelDefinitions)
        {
            try
            {
                var compiled = await CompileAsync(kernelDef, accelerator, cancellationToken);
                results[kernelDef.Name] = compiled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile kernel {KernelName} in batch operation", kernelDef.Name);
                throw new InvalidOperationException($"Batch compilation failed for kernel {kernelDef.Name}: {ex.Message}", ex);
            }
        }

        return results;
    }
}
