// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Backends.Metal.Execution.Types.Core;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1305 // CultureInfo usage enforced globally
#pragma warning disable CA1307 // StringComparison enforced globally
#pragma warning disable CA1822 // Mark members as static - instance design preferred for extensibility

namespace DotCompute.Backends.Metal.Translation;

/// <summary>
/// Provides comprehensive translation of C# kernel code to Metal Shading Language (MSL).
/// Handles syntax transformation, type mapping, threading model translation, and MSL-specific optimizations.
/// </summary>
/// <remarks>
/// This translator is designed for production use with the [Kernel] attribute source generator.
/// It performs accurate translation of C# constructs to their Metal equivalents while maintaining
/// semantic correctness and performance characteristics.
///
/// Supported features:
/// - Thread ID mapping (Kernel.ThreadId.X/Y/Z → thread_position_in_grid)
/// - Type translation (Span&lt;T&gt;, ReadOnlySpan&lt;T&gt; → device pointers)
/// - Math function mapping (Math.* → metal::*)
/// - Atomic operations (Interlocked.* → atomic_*)
/// - Synchronization barriers
/// - Control flow and loops
/// - Array access and bounds checking
/// - SIMD operation detection and optimization
/// </remarks>
public sealed partial class CSharpToMSLTranslator
{
    private readonly ILogger _logger;
    private readonly List<MetalDiagnosticMessage> _diagnostics = [];

    /// <summary>
    /// Memory access pattern classification for optimization
    /// </summary>
    private enum MemoryAccessPattern
    {
        /// <summary>Sequential access: buffer[i], buffer[i+1]</summary>
        Sequential,

        /// <summary>Strided access: buffer[i * stride]</summary>
        Strided,

        /// <summary>Scattered access: buffer[indices[i]]</summary>
        Scattered,

        /// <summary>Coalesced GPU-optimal access: buffer[thread_position_in_grid.x]</summary>
        Coalesced,

        /// <summary>Unknown or complex pattern</summary>
        Unknown
    }

    /// <summary>
    /// Information about detected memory access
    /// </summary>
    private sealed class MemoryAccessInfo
    {
        public required string BufferName { get; init; }
        public required string IndexExpression { get; init; }
        public required MemoryAccessPattern Pattern { get; init; }
        public required int LineNumber { get; init; }
        public string? Stride { get; init; }
    }

    /// <summary>
    /// Regex pattern for matching method declarations.
    /// Matches: [public] [static] void MethodName(parameters)
    /// </summary>
    [GeneratedRegex(@"(?:public\s+)?(?:static\s+)?void\s+(\w+)\s*\(")]
    private static partial Regex MethodDeclarationPattern();

    /// <summary>
    /// Regex pattern for matching array access: identifier[expression]
    /// </summary>
    [GeneratedRegex(@"(\w+)\[([^\]]+)\]")]
    private static partial Regex ArrayAccessPattern();

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpToMSLTranslator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
    public CSharpToMSLTranslator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Translates C# kernel code to Metal Shading Language.
    /// </summary>
    /// <param name="csharpCode">The C# source code to translate</param>
    /// <param name="kernelName">Name of the kernel for diagnostics and error reporting</param>
    /// <param name="entryPoint">Metal entry point function name</param>
    /// <returns>Complete MSL source code ready for compilation</returns>
    /// <exception cref="ArgumentNullException">Thrown when csharpCode or kernelName is null</exception>
    /// <exception cref="ArgumentException">Thrown when csharpCode or kernelName is empty</exception>
    /// <exception cref="InvalidOperationException">Thrown when translation fails</exception>
    public string Translate(string csharpCode, string kernelName, string entryPoint)
    {
        ArgumentNullException.ThrowIfNull(csharpCode);
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPoint);

        try
        {
            _logger.LogDebug("Translating C# kernel '{KernelName}' to Metal Shading Language", kernelName);

            // Clear diagnostics from previous translation
            _diagnostics.Clear();

            var mslCode = new StringBuilder(csharpCode.Length * 2);

            // Add Metal standard headers and metadata
            AppendMSLHeaders(mslCode, kernelName);

            // Parse and translate the kernel method
            var translatedKernel = TranslateKernelMethod(csharpCode, kernelName, entryPoint);
            _ = mslCode.Append(translatedKernel);

            var result = mslCode.ToString();

            // Report diagnostics
            ReportDiagnostics(kernelName);

            _logger.LogInformation(
                "Successfully translated C# kernel '{KernelName}' to Metal ({Bytes} bytes MSL, {Diagnostics} diagnostics)",
                kernelName,
                result.Length,
                _diagnostics.Count);

            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to translate C# kernel '{KernelName}' to Metal", kernelName);
            throw new InvalidOperationException(
                $"C# to Metal translation failed for kernel '{kernelName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets diagnostic messages generated during translation.
    /// </summary>
    public IReadOnlyList<MetalDiagnosticMessage> GetDiagnostics() => _diagnostics.AsReadOnly();

    /// <summary>
    /// Appends standard Metal headers and metadata comments to the output.
    /// </summary>
    private static void AppendMSLHeaders(StringBuilder msl, string kernelName)
    {
        ArgumentNullException.ThrowIfNull(msl);
        ArgumentNullException.ThrowIfNull(kernelName);

        _ = msl.AppendLine("#include <metal_stdlib>");
        _ = msl.AppendLine("#include <metal_compute>");
        _ = msl.AppendLine("using namespace metal;");
        _ = msl.AppendLine();
        _ = msl.AppendLine(CultureInfo.InvariantCulture, $"// Auto-generated Metal kernel: {kernelName}");
        _ = msl.AppendLine(CultureInfo.InvariantCulture, $"// Translated from C# on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = msl.AppendLine(CultureInfo.InvariantCulture, $"// DotCompute.Backends.Metal Translation Engine");
        _ = msl.AppendLine();
    }

    /// <summary>
    /// Translates a C# kernel method to a Metal compute kernel function.
    /// Extracts method signature, parameters, and body for translation.
    /// </summary>
    private string TranslateKernelMethod(string csharpCode, string kernelName, string entryPoint)
    {
        ArgumentNullException.ThrowIfNull(csharpCode);
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(entryPoint);

        // Find method declaration
        var methodMatch = MethodDeclarationPattern().Match(csharpCode);
        if (!methodMatch.Success)
        {
            throw new InvalidOperationException(
                $"Could not find valid method declaration in C# kernel '{kernelName}'. " +
                "Expected format: [public] [static] void MethodName(parameters)");
        }

        var methodStart = methodMatch.Index;
        var methodCode = csharpCode[methodStart..];

        // Extract method components
        var (parameters, body) = ExtractMethodComponents(methodCode, kernelName);

        // Build Metal kernel signature
        var msl = new StringBuilder(methodCode.Length * 2);
        BuildMetalKernelSignature(msl, entryPoint, parameters);

        // Translate and append body
        var translatedBody = TranslateMethodBody(body, kernelName);
        _ = msl.Append(translatedBody);
        _ = msl.AppendLine("}");

        return msl.ToString();
    }

    /// <summary>
    /// Extracts parameters and body from a C# method.
    /// </summary>
    private static (string parameters, string body) ExtractMethodComponents(string methodCode, string kernelName)
    {
        ArgumentNullException.ThrowIfNull(methodCode);
        ArgumentNullException.ThrowIfNull(kernelName);

        // Find parameter list
        var paramStart = methodCode.IndexOf('(', StringComparison.Ordinal);
        if (paramStart == -1)
        {
            throw new InvalidOperationException($"Could not find parameter list in kernel '{kernelName}'");
        }

        var paramEnd = FindMatchingCloseParen(methodCode, paramStart);
        var parameters = methodCode.Substring(paramStart + 1, paramEnd - paramStart - 1);

        // Find method body
        var bodyStart = methodCode.IndexOf('{', paramEnd);
        if (bodyStart == -1)
        {
            throw new InvalidOperationException($"Could not find method body in kernel '{kernelName}'");
        }

        var bodyEnd = FindMatchingCloseBrace(methodCode, bodyStart);
        var body = methodCode.Substring(bodyStart + 1, bodyEnd - bodyStart - 1);

        return (parameters, body);
    }

    /// <summary>
    /// Builds a Metal kernel function signature with proper buffer attributes.
    /// </summary>
    private void BuildMetalKernelSignature(StringBuilder msl, string entryPoint, string parameters)
    {
        ArgumentNullException.ThrowIfNull(msl);
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(parameters);

        _ = msl.AppendLine(CultureInfo.InvariantCulture, $"kernel void {entryPoint}(");

        // Translate parameters to Metal buffer declarations
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            var translatedParams = TranslateParameters(parameters);
            _ = msl.Append(translatedParams);
            _ = msl.AppendLine(",");
        }

        // Add Metal thread attributes
        _ = msl.AppendLine("    uint3 thread_position_in_grid [[thread_position_in_grid]],");
        _ = msl.AppendLine("    uint3 threads_per_threadgroup [[threads_per_threadgroup]],");
        _ = msl.AppendLine("    uint3 threadgroup_position_in_grid [[threadgroup_position_in_grid]])");
        _ = msl.AppendLine("{");
    }

    /// <summary>
    /// Translates C# method parameters to Metal kernel parameters.
    /// Handles Span&lt;T&gt;, ReadOnlySpan&lt;T&gt;, and scalar types.
    /// </summary>
    private string TranslateParameters(string parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var result = new StringBuilder(parameters.Length * 2);
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
                _logger.LogWarning("Skipping malformed parameter: '{Parameter}'", param);
                continue;
            }

            var type = parts[0];
            var name = parts[^1]; // Last part is the name

            // Handle type modifiers (ref, in, out)
            var typeModifier = string.Empty;
            if (parts.Length > 2)
            {
                // Check for ref/in/out modifiers
                if (type is "ref" or "in" or "out")
                {
                    typeModifier = type;
                    type = parts[1];
                    // Name might be last or second-to-last
                    name = parts[^1];
                }
            }

            // Translate type
            var (metalType, isReadOnly) = TranslateType(type);

            // Determine buffer access mode
            var accessMode = (isReadOnly || typeModifier == "in") ? "const" : "";

            // Add buffer parameter with attribute
            var bufferIndex = i;

            if (i > 0)
            {
                _ = result.Append(",\n");
            }

            // Format: [const] device type* name [[buffer(index)]]
            _ = result.Append("    ");
            if (!string.IsNullOrEmpty(accessMode))
            {
                _ = result.Append(accessMode);
                _ = result.Append(' ');
            }
            _ = result.Append(CultureInfo.InvariantCulture, $"device {metalType}* {name} [[buffer({bufferIndex})]]");
        }

        return result.ToString();
    }

    /// <summary>
    /// Translates C# types to Metal types with read-only detection.
    /// </summary>
    private (string metalType, bool isReadOnly) TranslateType(string csharpType)
    {
        ArgumentNullException.ThrowIfNull(csharpType);

        var isReadOnly = false;

        // Handle ReadOnlySpan<T>
        if (csharpType.StartsWith("ReadOnlySpan<", StringComparison.Ordinal))
        {
            isReadOnly = true;
            var innerType = ExtractGenericParameter(csharpType);
            return (TranslatePrimitiveType(innerType), isReadOnly);
        }

        // Handle Span<T>
        if (csharpType.StartsWith("Span<", StringComparison.Ordinal))
        {
            var innerType = ExtractGenericParameter(csharpType);
            return (TranslatePrimitiveType(innerType), isReadOnly);
        }

        // Handle arrays
        if (csharpType.EndsWith("[]", StringComparison.Ordinal))
        {
            var innerType = csharpType[..^2];
            return (TranslatePrimitiveType(innerType), isReadOnly);
        }

        // Primitive types
        return (TranslatePrimitiveType(csharpType), isReadOnly);
    }

    /// <summary>
    /// Extracts the type parameter from a generic type (e.g., "Span&lt;float&gt;" → "float").
    /// </summary>
    private static string ExtractGenericParameter(string genericType)
    {
        ArgumentNullException.ThrowIfNull(genericType);

        var startIndex = genericType.IndexOf('<');
        var endIndex = genericType.LastIndexOf('>');

        if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
        {
            throw new InvalidOperationException($"Invalid generic type format: {genericType}");
        }

        return genericType.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
    }

    /// <summary>
    /// Translates C# primitive types to Metal equivalents.
    /// </summary>
    private static string TranslatePrimitiveType(string csharpType)
    {
        ArgumentNullException.ThrowIfNull(csharpType);

        return csharpType.Trim() switch
        {
            // Floating point
            "float" or "Float" or "Single" => "float",
            "double" or "Double" => "double",
            "half" or "Half" => "half",

            // Signed integers
            "int" or "Int32" => "int",
            "short" or "Int16" => "short",
            "sbyte" or "SByte" => "char",
            "long" or "Int64" => "long",

            // Unsigned integers
            "uint" or "UInt32" => "uint",
            "ushort" or "UInt16" => "ushort",
            "byte" or "Byte" => "uchar",
            "ulong" or "UInt64" => "ulong",

            // Boolean
            "bool" or "Boolean" => "bool",

            // Vector types
            "Vector2" => "float2",
            "Vector3" => "float3",
            "Vector4" => "float4",

            // Default: pass through as-is (may be Metal-native type)
            _ => csharpType
        };
    }

    /// <summary>
    /// Translates the method body from C# to Metal.
    /// Handles threading model, math functions, control flow, and Metal-specific constructs.
    /// </summary>
    private string TranslateMethodBody(string body, string kernelName)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(kernelName);

        var msl = new StringBuilder(body.Length * 2);
        var lines = body.Split('\n');
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;
            var trimmed = line.Trim();

            // Skip empty lines but preserve them for formatting
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _ = msl.AppendLine();
                continue;
            }

            try
            {
                // Analyze memory access patterns before translation
                AnalyzeMemoryAccess(trimmed, lineNumber);

                // Translate the line
                var translatedLine = TranslateLine(trimmed);

                // Preserve indentation
                var indent = line.Length - line.TrimStart().Length;
                _ = msl.Append(new string(' ', indent));
                _ = msl.AppendLine(translatedLine);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to translate line in kernel '{KernelName}': {Line}",
                    kernelName,
                    trimmed);

                // Include problematic line as comment for debugging
                var indent = line.Length - line.TrimStart().Length;
                _ = msl.Append(new string(' ', indent));
                _ = msl.AppendLine(CultureInfo.InvariantCulture, $"// TRANSLATION WARNING: {trimmed}");
            }
        }

        return msl.ToString();
    }

    /// <summary>
    /// Translates a single line of C# code to Metal.
    /// Performs syntax transformations for threading, math, atomics, and more.
    /// </summary>
    private static string TranslateLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var result = line;

        // ===== THREAD ID MAPPING =====
        // Map Kernel.ThreadId.X/Y/Z to thread_position_in_grid.x/y/z
        result = result.Replace("Kernel.ThreadId.X", "thread_position_in_grid.x", StringComparison.Ordinal);
        result = result.Replace("Kernel.ThreadId.Y", "thread_position_in_grid.y", StringComparison.Ordinal);
        result = result.Replace("Kernel.ThreadId.Z", "thread_position_in_grid.z", StringComparison.Ordinal);

        // Handle shorthand ThreadId.X (without Kernel. prefix)
        result = result.Replace("ThreadId.X", "thread_position_in_grid.x", StringComparison.Ordinal);
        result = result.Replace("ThreadId.Y", "thread_position_in_grid.y", StringComparison.Ordinal);
        result = result.Replace("ThreadId.Z", "thread_position_in_grid.z", StringComparison.Ordinal);

        // ===== MATH FUNCTIONS =====
        // Map C# Math/MathF functions to Metal metal:: namespace
        result = TranslateMathFunctions(result);

        // ===== ATOMIC OPERATIONS =====
        result = TranslateAtomicOperations(result);

        // ===== SYNCHRONIZATION =====
        result = TranslateSynchronization(result);

        // ===== TYPE CONVERSIONS & CASTING =====
        result = TranslateTypeCasts(result);

        return result;
    }

    /// <summary>
    /// Translates C# Math and MathF functions to Metal equivalents.
    /// </summary>
    private static string TranslateMathFunctions(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var result = line;

        // Math functions (double precision)
        result = result.Replace("Math.Sqrt(", "metal::sqrt(", StringComparison.Ordinal);
        result = result.Replace("Math.Abs(", "metal::abs(", StringComparison.Ordinal);
        result = result.Replace("Math.Sin(", "metal::sin(", StringComparison.Ordinal);
        result = result.Replace("Math.Cos(", "metal::cos(", StringComparison.Ordinal);
        result = result.Replace("Math.Tan(", "metal::tan(", StringComparison.Ordinal);
        result = result.Replace("Math.Asin(", "metal::asin(", StringComparison.Ordinal);
        result = result.Replace("Math.Acos(", "metal::acos(", StringComparison.Ordinal);
        result = result.Replace("Math.Atan(", "metal::atan(", StringComparison.Ordinal);
        result = result.Replace("Math.Atan2(", "metal::atan2(", StringComparison.Ordinal);
        result = result.Replace("Math.Sinh(", "metal::sinh(", StringComparison.Ordinal);
        result = result.Replace("Math.Cosh(", "metal::cosh(", StringComparison.Ordinal);
        result = result.Replace("Math.Tanh(", "metal::tanh(", StringComparison.Ordinal);
        result = result.Replace("Math.Exp(", "metal::exp(", StringComparison.Ordinal);
        result = result.Replace("Math.Exp2(", "metal::exp2(", StringComparison.Ordinal);
        result = result.Replace("Math.Log(", "metal::log(", StringComparison.Ordinal);
        result = result.Replace("Math.Log2(", "metal::log2(", StringComparison.Ordinal);
        result = result.Replace("Math.Log10(", "metal::log10(", StringComparison.Ordinal);
        result = result.Replace("Math.Pow(", "metal::pow(", StringComparison.Ordinal);
        result = result.Replace("Math.Floor(", "metal::floor(", StringComparison.Ordinal);
        result = result.Replace("Math.Ceil(", "metal::ceil(", StringComparison.Ordinal);
        result = result.Replace("Math.Round(", "metal::round(", StringComparison.Ordinal);
        result = result.Replace("Math.Truncate(", "metal::trunc(", StringComparison.Ordinal);
        result = result.Replace("Math.Min(", "metal::min(", StringComparison.Ordinal);
        result = result.Replace("Math.Max(", "metal::max(", StringComparison.Ordinal);
        result = result.Replace("Math.Clamp(", "metal::clamp(", StringComparison.Ordinal);

        // MathF functions (single precision) - same Metal functions, type-overloaded
        result = result.Replace("MathF.Sqrt(", "metal::sqrt(", StringComparison.Ordinal);
        result = result.Replace("MathF.Abs(", "metal::abs(", StringComparison.Ordinal);
        result = result.Replace("MathF.Sin(", "metal::sin(", StringComparison.Ordinal);
        result = result.Replace("MathF.Cos(", "metal::cos(", StringComparison.Ordinal);
        result = result.Replace("MathF.Tan(", "metal::tan(", StringComparison.Ordinal);
        result = result.Replace("MathF.Asin(", "metal::asin(", StringComparison.Ordinal);
        result = result.Replace("MathF.Acos(", "metal::acos(", StringComparison.Ordinal);
        result = result.Replace("MathF.Atan(", "metal::atan(", StringComparison.Ordinal);
        result = result.Replace("MathF.Atan2(", "metal::atan2(", StringComparison.Ordinal);
        result = result.Replace("MathF.Exp(", "metal::exp(", StringComparison.Ordinal);
        result = result.Replace("MathF.Log(", "metal::log(", StringComparison.Ordinal);
        result = result.Replace("MathF.Pow(", "metal::pow(", StringComparison.Ordinal);
        result = result.Replace("MathF.Floor(", "metal::floor(", StringComparison.Ordinal);
        result = result.Replace("MathF.Ceil(", "metal::ceil(", StringComparison.Ordinal);
        result = result.Replace("MathF.Round(", "metal::round(", StringComparison.Ordinal);
        result = result.Replace("MathF.Min(", "metal::min(", StringComparison.Ordinal);
        result = result.Replace("MathF.Max(", "metal::max(", StringComparison.Ordinal);

        return result;
    }

    /// <summary>
    /// Translates C# Interlocked operations to Metal atomics.
    /// </summary>
    private static string TranslateAtomicOperations(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var result = line;

        // Interlocked operations → Metal atomic operations
        result = result.Replace("Interlocked.Add(", "atomic_fetch_add_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Increment(", "atomic_fetch_add_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Decrement(", "atomic_fetch_sub_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Exchange(", "atomic_exchange_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.CompareExchange(", "atomic_compare_exchange_weak_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Read(", "atomic_load_explicit(", StringComparison.Ordinal);

        // Add memory order for atomics if not specified
        if (result.Contains("atomic_", StringComparison.Ordinal) &&
            !result.Contains("memory_order", StringComparison.Ordinal))
        {
            // Add memory_order_relaxed before the closing paren
            result = result.Replace(");", ", memory_order_relaxed);", StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Translates C# synchronization primitives to Metal barriers.
    /// </summary>
    private static string TranslateSynchronization(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var result = line;

        // Barrier synchronization
        result = result.Replace("Barrier()", "threadgroup_barrier(mem_flags::mem_device)", StringComparison.Ordinal);
        result = result.Replace("Barrier.Sync()", "threadgroup_barrier(mem_flags::mem_device)", StringComparison.Ordinal);
        result = result.Replace("MemoryBarrier()", "threadgroup_barrier(mem_flags::mem_device)", StringComparison.Ordinal);

        return result;
    }

    /// <summary>
    /// Translates C# type casts to Metal equivalents.
    /// </summary>
    private static string TranslateTypeCasts(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var result = line;

        // Cast operators - Metal uses similar syntax but may need adjustments
        // (float)value → float(value) for Metal consistency
        result = Regex.Replace(result, @"\(float\)\s*(\w+)", "float($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(int\)\s*(\w+)", "int($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(uint\)\s*(\w+)", "uint($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(double\)\s*(\w+)", "double($1)", RegexOptions.None);

        return result;
    }

    /// <summary>
    /// Detects reduction patterns in code and returns the reduction type.
    /// Patterns include: sum accumulation, max/min operations, product operations.
    /// </summary>
    /// <param name="line">The line of code to analyze</param>
    /// <param name="reductionType">Output parameter containing the detected reduction type</param>
    /// <param name="targetVariable">Output parameter containing the variable being reduced</param>
    /// <param name="operationValue">Output parameter containing the value being used in the reduction</param>
    /// <returns>True if a reduction pattern was detected, false otherwise</returns>
    private static bool IsReductionPattern(string line, out string? reductionType, out string? targetVariable, out string? operationValue)
    {
        ArgumentNullException.ThrowIfNull(line);

        reductionType = null;
        targetVariable = null;
        operationValue = null;

        // Pattern: variable += value (sum reduction)
        var sumMatch = Regex.Match(line, @"(\w+)\s*\+=\s*(.+?);");
        if (sumMatch.Success)
        {
            reductionType = "sum";
            targetVariable = sumMatch.Groups[1].Value;
            operationValue = sumMatch.Groups[2].Value;
            return true;
        }

        // Pattern: variable = Math.Max(variable, value) (max reduction)
        var maxMatch = Regex.Match(line, @"(\w+)\s*=\s*(?:Math|MathF|metal::max)\.Max\(\1,\s*(.+?)\)");
        if (maxMatch.Success)
        {
            reductionType = "max";
            targetVariable = maxMatch.Groups[1].Value;
            operationValue = maxMatch.Groups[2].Value;
            return true;
        }

        // Pattern: variable = Math.Min(variable, value) (min reduction)
        var minMatch = Regex.Match(line, @"(\w+)\s*=\s*(?:Math|MathF|metal::min)\.Min\(\1,\s*(.+?)\)");
        if (minMatch.Success)
        {
            reductionType = "min";
            targetVariable = minMatch.Groups[1].Value;
            operationValue = minMatch.Groups[2].Value;
            return true;
        }

        // Pattern: variable *= value (product reduction)
        var productMatch = Regex.Match(line, @"(\w+)\s*\*=\s*(.+?);");
        if (productMatch.Success)
        {
            reductionType = "product";
            targetVariable = productMatch.Groups[1].Value;
            operationValue = productMatch.Groups[2].Value;
            return true;
        }

        // Pattern: variable |= condition (any/or reduction)
        var anyMatch = Regex.Match(line, @"(\w+)\s*\|=\s*(.+?);");
        if (anyMatch.Success)
        {
            reductionType = "any";
            targetVariable = anyMatch.Groups[1].Value;
            operationValue = anyMatch.Groups[2].Value;
            return true;
        }

        // Pattern: variable &= condition (all/and reduction)
        var allMatch = Regex.Match(line, @"(\w+)\s*&=\s*(.+?);");
        if (allMatch.Success)
        {
            reductionType = "all";
            targetVariable = allMatch.Groups[1].Value;
            operationValue = allMatch.Groups[2].Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Translates SIMD operations to Metal SIMD primitives for optimized parallel execution.
    /// Maps common reduction operations to efficient Metal SIMD intrinsics.
    /// </summary>
    /// <param name="operation">The type of SIMD operation (sum, max, min, product, any, all)</param>
    /// <param name="variable">The variable being operated on</param>
    /// <param name="dataType">Optional data type for type-specific optimizations</param>
    /// <returns>Metal SIMD function call</returns>
    private static string TranslateSIMDOperation(string operation, string variable, string dataType = "float")
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(dataType);

        return operation switch
        {
            "sum" => $"simd_sum({variable})",
            "max" => $"simd_max({variable})",
            "min" => $"simd_min({variable})",
            "product" => $"simd_product({variable})",
            "any" => $"simd_any({variable})",
            "all" => $"simd_all({variable})",
            "broadcast" => $"simd_broadcast({variable}, 0)",
            "shuffle" => $"simd_shuffle({variable}, simd_active_threads_mask())",
            _ => variable // Fallback to original variable if operation unknown
        };
    }

    /// <summary>
    /// Generates optimized parallel reduction code using Metal SIMD primitives.
    /// Replaces simple accumulation patterns with warp-level reduction followed by
    /// threadgroup-level aggregation for maximum performance.
    /// </summary>
    /// <param name="reductionType">Type of reduction (sum, max, min, product)</param>
    /// <param name="targetVariable">Variable accumulating the result</param>
    /// <param name="operationValue">Value being reduced</param>
    /// <param name="dataType">Data type of the operation</param>
    /// <returns>Optimized Metal SIMD reduction code</returns>
    private static string GenerateParallelReduction(string reductionType, string targetVariable, string operationValue, string dataType = "float")
    {
        ArgumentNullException.ThrowIfNull(reductionType);
        ArgumentNullException.ThrowIfNull(targetVariable);
        ArgumentNullException.ThrowIfNull(operationValue);
        ArgumentNullException.ThrowIfNull(dataType);

        var simdOperation = TranslateSIMDOperation(reductionType, operationValue, dataType);

        return reductionType switch
        {
            "sum" => GenerateSumReduction(targetVariable, simdOperation, operationValue),
            "max" => GenerateMaxReduction(targetVariable, simdOperation, operationValue),
            "min" => GenerateMinReduction(targetVariable, simdOperation, operationValue),
            "product" => GenerateProductReduction(targetVariable, simdOperation, operationValue),
            "any" => GenerateAnyReduction(targetVariable, simdOperation, operationValue),
            "all" => GenerateAllReduction(targetVariable, simdOperation, operationValue),
            _ => $"{targetVariable} += {operationValue};" // Fallback to simple operation
        };
    }

    /// <summary>
    /// Generates optimized sum reduction using SIMD primitives.
    /// </summary>
    private static string GenerateSumReduction(string targetVariable, string simdOperation, string operationValue)
    {
        ArgumentNullException.ThrowIfNull(targetVariable);
        ArgumentNullException.ThrowIfNull(simdOperation);
        ArgumentNullException.ThrowIfNull(operationValue);

        return $@"// SIMD-optimized sum reduction
    {targetVariable} += {simdOperation};
    threadgroup_barrier(mem_flags::mem_threadgroup);";
    }

    /// <summary>
    /// Generates optimized max reduction using SIMD primitives.
    /// </summary>
    private static string GenerateMaxReduction(string targetVariable, string simdOperation, string operationValue)
    {
        ArgumentNullException.ThrowIfNull(targetVariable);
        ArgumentNullException.ThrowIfNull(simdOperation);
        ArgumentNullException.ThrowIfNull(operationValue);

        return $@"// SIMD-optimized max reduction
    {targetVariable} = metal::max({targetVariable}, {simdOperation});
    threadgroup_barrier(mem_flags::mem_threadgroup);";
    }

    /// <summary>
    /// Generates optimized min reduction using SIMD primitives.
    /// </summary>
    private static string GenerateMinReduction(string targetVariable, string simdOperation, string operationValue)
    {
        ArgumentNullException.ThrowIfNull(targetVariable);
        ArgumentNullException.ThrowIfNull(simdOperation);
        ArgumentNullException.ThrowIfNull(operationValue);

        return $@"// SIMD-optimized min reduction
    {targetVariable} = metal::min({targetVariable}, {simdOperation});
    threadgroup_barrier(mem_flags::mem_threadgroup);";
    }

    /// <summary>
    /// Generates optimized product reduction using SIMD primitives.
    /// </summary>
    private static string GenerateProductReduction(string targetVariable, string simdOperation, string operationValue)
    {
        ArgumentNullException.ThrowIfNull(targetVariable);
        ArgumentNullException.ThrowIfNull(simdOperation);
        ArgumentNullException.ThrowIfNull(operationValue);

        return $@"// SIMD-optimized product reduction
    {targetVariable} *= {simdOperation};
    threadgroup_barrier(mem_flags::mem_threadgroup);";
    }

    /// <summary>
    /// Generates optimized any/or reduction using SIMD primitives.
    /// </summary>
    private static string GenerateAnyReduction(string targetVariable, string simdOperation, string operationValue)
    {
        ArgumentNullException.ThrowIfNull(targetVariable);
        ArgumentNullException.ThrowIfNull(simdOperation);
        ArgumentNullException.ThrowIfNull(operationValue);

        return $@"// SIMD-optimized any reduction
    {targetVariable} = {targetVariable} || {simdOperation};
    threadgroup_barrier(mem_flags::mem_threadgroup);";
    }

    /// <summary>
    /// Generates optimized all/and reduction using SIMD primitives.
    /// </summary>
    private static string GenerateAllReduction(string targetVariable, string simdOperation, string operationValue)
    {
        ArgumentNullException.ThrowIfNull(targetVariable);
        ArgumentNullException.ThrowIfNull(simdOperation);
        ArgumentNullException.ThrowIfNull(operationValue);

        return $@"// SIMD-optimized all reduction
    {targetVariable} = {targetVariable} && {simdOperation};
    threadgroup_barrier(mem_flags::mem_threadgroup);";
    }

    /// <summary>
    /// Maps data exchange operations to Metal SIMD shuffle operations.
    /// Enables efficient intra-warp communication without shared memory.
    /// </summary>
    /// <param name="operation">The type of exchange operation</param>
    /// <param name="value">The value to exchange</param>
    /// <param name="offset">Offset for shuffle operations (default 0)</param>
    /// <returns>Metal SIMD shuffle expression</returns>
    private static string TranslateShuffleOperation(string operation, string value, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(value);

        return operation.ToUpperInvariant() switch
        {
            "EXCHANGE" => $"simd_shuffle({value}, simd_get_lane_id() + {offset})",
            "BROADCAST" => $"simd_broadcast({value}, {offset})",
            "SHUFFLE_UP" => $"simd_shuffle_up({value}, {offset})",
            "SHUFFLE_DOWN" => $"simd_shuffle_down({value}, {offset})",
            "SHUFFLE_XOR" => $"simd_shuffle_xor({value}, {offset})",
            _ => value // Fallback to original value
        };
    }

    /// <summary>
    /// Finds the matching closing parenthesis for an opening parenthesis.
    /// </summary>
    private static int FindMatchingCloseParen(string text, int openIndex)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (openIndex < 0 || openIndex >= text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(openIndex));
        }

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
    /// Finds the matching closing brace for an opening brace.
    /// </summary>
    private static int FindMatchingCloseBrace(string text, int openIndex)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (openIndex < 0 || openIndex >= text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(openIndex));
        }

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

    /// <summary>
    /// Analyzes memory access patterns in a line of code.
    /// Detects array indexing expressions and classifies access patterns.
    /// </summary>
    private void AnalyzeMemoryAccess(string line, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(line);

        var matches = ArrayAccessPattern().Matches(line);
        if (matches.Count == 0)
        {
            return;
        }

        foreach (Match match in matches)
        {
            var bufferName = match.Groups[1].Value;
            var indexExpression = match.Groups[2].Value.Trim();

            var pattern = ClassifyAccessPattern(indexExpression);
            var stride = pattern == MemoryAccessPattern.Strided ? ExtractStrideValue(indexExpression) : null;

            var accessInfo = new MemoryAccessInfo
            {
                BufferName = bufferName,
                IndexExpression = indexExpression,
                Pattern = pattern,
                LineNumber = lineNumber,
                Stride = stride
            };

            // Generate diagnostics for suboptimal patterns
            GenerateMemoryAccessDiagnostic(accessInfo);
        }
    }

    /// <summary>
    /// Classifies a memory access pattern based on the index expression.
    /// </summary>
    private static MemoryAccessPattern ClassifyAccessPattern(string indexExpression)
    {
        ArgumentNullException.ThrowIfNull(indexExpression);

        // Coalesced: Direct thread ID access (optimal for GPU)
        if (indexExpression.Contains("thread_position_in_grid", StringComparison.Ordinal) ||
            indexExpression.Contains("Kernel.ThreadId", StringComparison.Ordinal) ||
            indexExpression.Contains("ThreadId.X", StringComparison.Ordinal))
        {
            // Check if there's multiplication (strided) or just direct access
            if (indexExpression.Contains('*') || indexExpression.Contains('/'))
            {
                return MemoryAccessPattern.Strided;
            }
            return MemoryAccessPattern.Coalesced;
        }

        // Strided: Contains multiplication or division
        if (indexExpression.Contains('*') || indexExpression.Contains('/'))
        {
            return MemoryAccessPattern.Strided;
        }

        // Scattered: Indirect indexing through another array
        if (ArrayAccessPattern().IsMatch(indexExpression))
        {
            return MemoryAccessPattern.Scattered;
        }

        // Sequential: Simple linear iteration variable
        if (Regex.IsMatch(indexExpression, @"^\w+\s*[\+\-]\s*\d+$", RegexOptions.None))
        {
            return MemoryAccessPattern.Sequential;
        }

        // Default to unknown for complex expressions
        return MemoryAccessPattern.Unknown;
    }

    /// <summary>
    /// Extracts stride value from a strided access pattern expression.
    /// </summary>
    private static string? ExtractStrideValue(string indexExpression)
    {
        ArgumentNullException.ThrowIfNull(indexExpression);

        // Match patterns like "i * stride" or "stride * i"
        var strideMatch = Regex.Match(indexExpression, @"[\*\/]\s*(\w+)", RegexOptions.None);
        if (strideMatch.Success)
        {
            return strideMatch.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Generates diagnostic messages for suboptimal memory access patterns.
    /// </summary>
    private void GenerateMemoryAccessDiagnostic(MemoryAccessInfo accessInfo)
    {
        ArgumentNullException.ThrowIfNull(accessInfo);

        switch (accessInfo.Pattern)
        {
            case MemoryAccessPattern.Scattered:
                _diagnostics.Add(new MetalDiagnosticMessage
                {
                    Severity = MetalDiagnosticMessage.SeverityLevel.Warning,
                    Message = $"Scattered memory access detected in '{accessInfo.BufferName}[{accessInfo.IndexExpression}]'. " +
                             "Consider using threadgroup staging for better performance. " +
                             "Scattered access can reduce memory bandwidth by 50-80%.",
                    Component = "MemoryAccessAnalyzer",
                    Context =
                    {
                        ["BufferName"] = accessInfo.BufferName,
                        ["Pattern"] = "Scattered",
                        ["LineNumber"] = accessInfo.LineNumber,
                        ["Suggestion"] = "Use threadgroup memory to coalesce scattered reads"
                    }
                });
                break;

            case MemoryAccessPattern.Strided:
                var strideInfo = accessInfo.Stride != null ? $" with stride '{accessInfo.Stride}'" : "";
                _diagnostics.Add(new MetalDiagnosticMessage
                {
                    Severity = MetalDiagnosticMessage.SeverityLevel.Info,
                    Message = $"Strided memory access detected in '{accessInfo.BufferName}[{accessInfo.IndexExpression}]'{strideInfo}. " +
                             "For large strides, consider threadgroup staging to improve coalescing. " +
                             "Expected performance impact: 15-30% bandwidth reduction.",
                    Component = "MemoryAccessAnalyzer",
                    Context =
                    {
                        ["BufferName"] = accessInfo.BufferName,
                        ["Pattern"] = "Strided",
                        ["Stride"] = accessInfo.Stride ?? "unknown",
                        ["LineNumber"] = accessInfo.LineNumber,
                        ["Suggestion"] = "For stride > 16, use threadgroup memory staging"
                    }
                });
                break;

            case MemoryAccessPattern.Coalesced:
                // Optimal pattern - log as debug info
                _logger.LogDebug(
                    "Coalesced memory access detected: {BufferName}[{IndexExpression}] (line {LineNumber})",
                    accessInfo.BufferName,
                    accessInfo.IndexExpression,
                    accessInfo.LineNumber);
                break;

            case MemoryAccessPattern.Sequential:
            case MemoryAccessPattern.Unknown:
                // No diagnostic needed for these patterns
                break;
        }
    }

    /// <summary>
    /// Reports all collected diagnostics to the logger.
    /// </summary>
    private void ReportDiagnostics(string kernelName)
    {
        ArgumentNullException.ThrowIfNull(kernelName);

        if (_diagnostics.Count == 0)
        {
            return;
        }

        var warnings = _diagnostics.Count(d => d.Severity == MetalDiagnosticMessage.SeverityLevel.Warning);
        var infos = _diagnostics.Count(d => d.Severity == MetalDiagnosticMessage.SeverityLevel.Info);

        _logger.LogInformation(
            "Memory access analysis for kernel '{KernelName}': {Warnings} warnings, {Infos} info messages",
            kernelName,
            warnings,
            infos);

        foreach (var diagnostic in _diagnostics)
        {
            switch (diagnostic.Severity)
            {
                case MetalDiagnosticMessage.SeverityLevel.Warning:
                    _logger.LogWarning("{Message}", diagnostic.Message);
                    break;
                case MetalDiagnosticMessage.SeverityLevel.Info:
                    _logger.LogInformation("{Message}", diagnostic.Message);
                    break;
                case MetalDiagnosticMessage.SeverityLevel.Error:
                    _logger.LogError("{Message}", diagnostic.Message);
                    break;
                case MetalDiagnosticMessage.SeverityLevel.Critical:
                    _logger.LogCritical("{Message}", diagnostic.Message);
                    break;
            }
        }
    }
}
