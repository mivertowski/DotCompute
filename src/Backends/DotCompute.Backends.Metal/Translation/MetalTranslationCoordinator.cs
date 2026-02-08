// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Translation;

/// <summary>
/// Unified Metal translation coordinator that integrates all specialized translators
/// for comprehensive C# to MSL translation.
/// </summary>
/// <remarks>
/// <para>
/// This coordinator orchestrates multiple specialized translators:
/// </para>
/// <list type="bullet">
/// <item><description>MetalAtomicTranslator - Atomic operations and memory ordering</description></item>
/// <item><description>MetalSharedMemoryTranslator - Threadgroup memory declarations</description></item>
/// <item><description>Control flow, math functions, and type translations</description></item>
/// </list>
/// </remarks>
public sealed class MetalTranslationCoordinator
{
    private readonly ILogger _logger;
    private readonly MetalAtomicTranslator _atomicTranslator;
    private readonly MetalSharedMemoryTranslator _sharedMemoryTranslator;

    /// <summary>
    /// Initializes a new instance of the MetalTranslationCoordinator.
    /// </summary>
    public MetalTranslationCoordinator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _atomicTranslator = new MetalAtomicTranslator();
        _sharedMemoryTranslator = new MetalSharedMemoryTranslator();
    }

    /// <summary>
    /// Performs comprehensive C# to MSL translation.
    /// </summary>
    /// <param name="csharpCode">The C# kernel source code.</param>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="entryPoint">The MSL entry point name.</param>
    /// <returns>Complete MSL source code.</returns>
    public string TranslateKernel(string csharpCode, string kernelName, string entryPoint)
    {
        ArgumentNullException.ThrowIfNull(csharpCode);
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(entryPoint);

        _logger.LogDebug("Starting comprehensive Metal translation for kernel '{KernelName}'", kernelName);

        var msl = new StringBuilder(csharpCode.Length * 2);

        // 1. Generate MSL headers
        GenerateMslHeaders(msl, kernelName);

        // 2. Extract and translate shared memory declarations
        var sharedMemoryDecls = _sharedMemoryTranslator.ExtractDeclarations(csharpCode);

        // 3. Extract method signature and body
        var (parameters, body) = ExtractMethodComponents(csharpCode);

        // 4. Generate kernel signature with all parameters
        GenerateKernelSignature(msl, entryPoint, parameters, sharedMemoryDecls);

        // 5. Generate shared memory initialization if needed
        if (sharedMemoryDecls.Count > 0)
        {
            msl.Append(_sharedMemoryTranslator.GenerateInitializationCode(sharedMemoryDecls));
        }

        // 6. Translate the kernel body
        var translatedBody = TranslateBody(body);
        msl.AppendLine(translatedBody);

        msl.AppendLine("}");

        _logger.LogInformation("Metal translation complete for kernel '{KernelName}' ({Size} bytes)",
            kernelName, msl.Length);

        return msl.ToString();
    }

    private static void GenerateMslHeaders(StringBuilder msl, string kernelName)
    {
        msl.AppendLine("#include <metal_stdlib>");
        msl.AppendLine("#include <metal_compute>");
        msl.AppendLine("#include <metal_atomic>");
        msl.AppendLine("#include <metal_simdgroup>");
        msl.AppendLine("using namespace metal;");
        msl.AppendLine();
        msl.AppendLine(CultureInfo.InvariantCulture, $"// Auto-generated Metal kernel: {kernelName}");
        msl.AppendLine(CultureInfo.InvariantCulture, $"// Translated from C# by DotCompute.Backends.Metal");
        msl.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        msl.AppendLine();
    }

    private void GenerateKernelSignature(
        StringBuilder msl,
        string entryPoint,
        string parameters,
        IReadOnlyList<MetalSharedMemoryTranslator.SharedMemoryDeclaration> sharedMemoryDecls)
    {
        msl.AppendLine(CultureInfo.InvariantCulture, $"kernel void {entryPoint}(");

        // Translate buffer parameters
        var bufferIndex = 0;
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            var translatedParams = TranslateParameters(parameters, ref bufferIndex);
            msl.Append(translatedParams);
        }

        // Add threadgroup memory parameters
        if (sharedMemoryDecls.Count > 0)
        {
            if (bufferIndex > 0) msl.AppendLine(",");
            msl.Append(_sharedMemoryTranslator.GenerateThreadgroupParameters(sharedMemoryDecls, 0));
        }

        // Add Metal thread attributes
        if (bufferIndex > 0 || sharedMemoryDecls.Count > 0) msl.AppendLine(",");
        msl.AppendLine("    uint3 thread_position_in_grid [[thread_position_in_grid]],");
        msl.AppendLine("    uint3 thread_position_in_threadgroup [[thread_position_in_threadgroup]],");
        msl.AppendLine("    uint3 threads_per_threadgroup [[threads_per_threadgroup]],");
        msl.AppendLine("    uint3 threadgroup_position_in_grid [[threadgroup_position_in_grid]],");
        msl.AppendLine("    uint thread_index_in_simdgroup [[thread_index_in_simdgroup]],");
        msl.AppendLine("    uint simdgroup_index_in_threadgroup [[simdgroup_index_in_threadgroup]])");
        msl.AppendLine("{");
    }

    private string TranslateParameters(string parameters, ref int bufferIndex)
    {
        var result = new StringBuilder();
        var paramList = parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var param in paramList)
        {
            if (string.IsNullOrWhiteSpace(param)) continue;

            var parts = param.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var type = parts[0];
            var name = parts[^1];

            // Handle type modifiers
            var isConst = type is "in" || parts.Any(p => p == "in");
            if (type is "ref" or "in" or "out")
            {
                type = parts.Length > 2 ? parts[1] : parts[0];
                name = parts[^1];
            }

            var (metalType, isReadOnly) = TranslateType(type);
            isConst = isConst || isReadOnly;

            if (bufferIndex > 0) result.Append(",\n");
            result.Append("    ");
            if (isConst) result.Append("const ");
            result.Append(CultureInfo.InvariantCulture, $"device {metalType}* {name} [[buffer({bufferIndex})]]");
            bufferIndex++;
        }

        return result.ToString();
    }

    private static (string metalType, bool isReadOnly) TranslateType(string csharpType)
    {
        var isReadOnly = csharpType.StartsWith("ReadOnlySpan<", StringComparison.Ordinal);

        // Extract generic type parameter
        if (csharpType.StartsWith("Span<", StringComparison.Ordinal) ||
            csharpType.StartsWith("ReadOnlySpan<", StringComparison.Ordinal))
        {
            var start = csharpType.IndexOf('<') + 1;
            var end = csharpType.LastIndexOf('>');
            csharpType = csharpType[start..end];
        }

        var metalType = csharpType.ToUpperInvariant() switch
        {
            "FLOAT" or "SINGLE" => "float",
            "DOUBLE" => "float", // Metal prefers float
            "INT" or "INT32" => "int",
            "UINT" or "UINT32" => "uint",
            "SHORT" or "INT16" => "short",
            "USHORT" or "UINT16" => "ushort",
            "LONG" or "INT64" => "long",
            "ULONG" or "UINT64" => "ulong",
            "BYTE" => "uchar",
            "SBYTE" => "char",
            "BOOL" or "BOOLEAN" => "bool",
            "HALF" => "half",
            "VECTOR2" => "float2",
            "VECTOR3" => "float3",
            "VECTOR4" => "float4",
            "INT2" => "int2",
            "INT3" => "int3",
            "INT4" => "int4",
            "UINT2" => "uint2",
            "UINT3" => "uint3",
            "UINT4" => "uint4",
#pragma warning disable CA1308 // Metal/C types are lowercase by convention
            _ => csharpType.ToLowerInvariant()
#pragma warning restore CA1308
        };

        return (metalType, isReadOnly);
    }

    private static (string parameters, string body) ExtractMethodComponents(string code)
    {
        var paramStart = code.IndexOf('(');
        if (paramStart == -1) throw new InvalidOperationException("Could not find method parameters");

        var paramEnd = FindMatchingParen(code, paramStart);
        var parameters = code[(paramStart + 1)..paramEnd];

        var bodyStart = code.IndexOf('{', paramEnd);
        if (bodyStart == -1) throw new InvalidOperationException("Could not find method body");

        var bodyEnd = FindMatchingBrace(code, bodyStart);
        var body = code[(bodyStart + 1)..bodyEnd];

        return (parameters, body);
    }

    private string TranslateBody(string body)
    {
        var lines = body.Split('\n');
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var translated = TranslateLine(line);
            result.AppendLine(translated);
        }

        return result.ToString();
    }

    private string TranslateLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        var result = line;

        // 1. Translate thread IDs
        result = TranslateThreadIds(result);

        // 2. Translate atomic operations (using specialized translator)
        if (MetalAtomicTranslator.ContainsAtomicOperations(result))
        {
            result = _atomicTranslator.TranslateAtomicOperations(result);
        }

        // 3. Translate shared memory access
        result = _sharedMemoryTranslator.TranslateSharedMemoryAccess(result);

        // 4. Translate math functions
        result = TranslateMathFunctions(result);

        // 5. Translate vector operations
        result = TranslateVectorOperations(result);

        // 6. Translate SIMD operations
        result = TranslateSimdOperations(result);

        // 7. Translate barriers and synchronization
        result = TranslateSynchronization(result);

        // 8. Translate type casts
        result = TranslateTypeCasts(result);

        // 9. Translate control flow keywords (clean up C# specifics)
        result = TranslateControlFlow(result);

        return result;
    }

    private static string TranslateThreadIds(string line)
    {
        var result = line;

        // Kernel.ThreadId → thread_position_in_grid
        result = result.Replace("Kernel.ThreadId.X", "thread_position_in_grid.x", StringComparison.Ordinal);
        result = result.Replace("Kernel.ThreadId.Y", "thread_position_in_grid.y", StringComparison.Ordinal);
        result = result.Replace("Kernel.ThreadId.Z", "thread_position_in_grid.z", StringComparison.Ordinal);

        // Kernel.BlockId → threadgroup_position_in_grid
        result = result.Replace("Kernel.BlockId.X", "threadgroup_position_in_grid.x", StringComparison.Ordinal);
        result = result.Replace("Kernel.BlockId.Y", "threadgroup_position_in_grid.y", StringComparison.Ordinal);
        result = result.Replace("Kernel.BlockId.Z", "threadgroup_position_in_grid.z", StringComparison.Ordinal);

        // Kernel.LocalId → thread_position_in_threadgroup
        result = result.Replace("Kernel.LocalId.X", "thread_position_in_threadgroup.x", StringComparison.Ordinal);
        result = result.Replace("Kernel.LocalId.Y", "thread_position_in_threadgroup.y", StringComparison.Ordinal);
        result = result.Replace("Kernel.LocalId.Z", "thread_position_in_threadgroup.z", StringComparison.Ordinal);

        // Kernel.BlockSize → threads_per_threadgroup
        result = result.Replace("Kernel.BlockSize.X", "threads_per_threadgroup.x", StringComparison.Ordinal);
        result = result.Replace("Kernel.BlockSize.Y", "threads_per_threadgroup.y", StringComparison.Ordinal);
        result = result.Replace("Kernel.BlockSize.Z", "threads_per_threadgroup.z", StringComparison.Ordinal);

        // Kernel.GlobalId (computed)
        result = result.Replace("Kernel.GlobalId.X",
            "(threadgroup_position_in_grid.x * threads_per_threadgroup.x + thread_position_in_threadgroup.x)",
            StringComparison.Ordinal);
        result = result.Replace("Kernel.GlobalId.Y",
            "(threadgroup_position_in_grid.y * threads_per_threadgroup.y + thread_position_in_threadgroup.y)",
            StringComparison.Ordinal);
        result = result.Replace("Kernel.GlobalId.Z",
            "(threadgroup_position_in_grid.z * threads_per_threadgroup.z + thread_position_in_threadgroup.z)",
            StringComparison.Ordinal);

        // SIMD lane/group
        result = result.Replace("Kernel.LaneId", "thread_index_in_simdgroup", StringComparison.Ordinal);
        result = result.Replace("Kernel.WarpId", "simdgroup_index_in_threadgroup", StringComparison.Ordinal);

        return result;
    }

    private static string TranslateMathFunctions(string line)
    {
        var result = line;

        // Math.* → metal::*
        result = result.Replace("Math.Abs(", "metal::abs(", StringComparison.Ordinal);
        result = result.Replace("Math.Sqrt(", "metal::sqrt(", StringComparison.Ordinal);
        result = result.Replace("Math.Pow(", "metal::pow(", StringComparison.Ordinal);
        result = result.Replace("Math.Exp(", "metal::exp(", StringComparison.Ordinal);
        result = result.Replace("Math.Log(", "metal::log(", StringComparison.Ordinal);
        result = result.Replace("Math.Log10(", "metal::log10(", StringComparison.Ordinal);
        result = result.Replace("Math.Log2(", "metal::log2(", StringComparison.Ordinal);
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
        result = result.Replace("Math.Floor(", "metal::floor(", StringComparison.Ordinal);
        result = result.Replace("Math.Ceiling(", "metal::ceil(", StringComparison.Ordinal);
        result = result.Replace("Math.Round(", "metal::round(", StringComparison.Ordinal);
        result = result.Replace("Math.Truncate(", "metal::trunc(", StringComparison.Ordinal);
        result = result.Replace("Math.Min(", "metal::min(", StringComparison.Ordinal);
        result = result.Replace("Math.Max(", "metal::max(", StringComparison.Ordinal);
        result = result.Replace("Math.Clamp(", "metal::clamp(", StringComparison.Ordinal);
        result = result.Replace("Math.Sign(", "metal::sign(", StringComparison.Ordinal);
        result = result.Replace("Math.FusedMultiplyAdd(", "metal::fma(", StringComparison.Ordinal);
        result = result.Replace("Math.Cbrt(", "metal::cbrt(", StringComparison.Ordinal);
        result = result.Replace("Math.Hypot(", "metal::hypot(", StringComparison.Ordinal);

        // MathF.* → metal::* (single precision)
        result = result.Replace("MathF.Abs(", "metal::abs(", StringComparison.Ordinal);
        result = result.Replace("MathF.Sqrt(", "metal::sqrt(", StringComparison.Ordinal);
        result = result.Replace("MathF.Pow(", "metal::pow(", StringComparison.Ordinal);
        result = result.Replace("MathF.Exp(", "metal::exp(", StringComparison.Ordinal);
        result = result.Replace("MathF.Log(", "metal::log(", StringComparison.Ordinal);
        result = result.Replace("MathF.Sin(", "metal::sin(", StringComparison.Ordinal);
        result = result.Replace("MathF.Cos(", "metal::cos(", StringComparison.Ordinal);
        result = result.Replace("MathF.Tan(", "metal::tan(", StringComparison.Ordinal);
        result = result.Replace("MathF.Floor(", "metal::floor(", StringComparison.Ordinal);
        result = result.Replace("MathF.Ceiling(", "metal::ceil(", StringComparison.Ordinal);
        result = result.Replace("MathF.Min(", "metal::min(", StringComparison.Ordinal);
        result = result.Replace("MathF.Max(", "metal::max(", StringComparison.Ordinal);
        result = result.Replace("MathF.FusedMultiplyAdd(", "metal::fma(", StringComparison.Ordinal);

        return result;
    }

    private static string TranslateVectorOperations(string line)
    {
        var result = line;

        // Vector construction: new Vector4(...) → float4(...)
        result = Regex.Replace(result, @"new\s+Vector2\s*\(", "float2(", RegexOptions.None);
        result = Regex.Replace(result, @"new\s+Vector3\s*\(", "float3(", RegexOptions.None);
        result = Regex.Replace(result, @"new\s+Vector4\s*\(", "float4(", RegexOptions.None);

        // Vector math methods
        result = result.Replace("Vector2.Dot(", "metal::dot(", StringComparison.Ordinal);
        result = result.Replace("Vector3.Dot(", "metal::dot(", StringComparison.Ordinal);
        result = result.Replace("Vector4.Dot(", "metal::dot(", StringComparison.Ordinal);
        result = result.Replace("Vector3.Cross(", "metal::cross(", StringComparison.Ordinal);
        result = result.Replace("Vector2.Normalize(", "metal::normalize(", StringComparison.Ordinal);
        result = result.Replace("Vector3.Normalize(", "metal::normalize(", StringComparison.Ordinal);
        result = result.Replace("Vector4.Normalize(", "metal::normalize(", StringComparison.Ordinal);
        result = result.Replace("Vector2.Length(", "metal::length(", StringComparison.Ordinal);
        result = result.Replace("Vector3.Length(", "metal::length(", StringComparison.Ordinal);
        result = result.Replace("Vector4.Length(", "metal::length(", StringComparison.Ordinal);
        result = result.Replace("Vector2.Distance(", "metal::distance(", StringComparison.Ordinal);
        result = result.Replace("Vector3.Distance(", "metal::distance(", StringComparison.Ordinal);
        result = result.Replace("Vector4.Distance(", "metal::distance(", StringComparison.Ordinal);
        result = result.Replace("Vector2.Lerp(", "metal::mix(", StringComparison.Ordinal);
        result = result.Replace("Vector3.Lerp(", "metal::mix(", StringComparison.Ordinal);
        result = result.Replace("Vector4.Lerp(", "metal::mix(", StringComparison.Ordinal);
        result = result.Replace("Vector3.Reflect(", "metal::reflect(", StringComparison.Ordinal);

        return result;
    }

    private static string TranslateSimdOperations(string line)
    {
        var result = line;

        // SIMD operations
        result = result.Replace("Kernel.SimdSum(", "simd_sum(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdMax(", "simd_max(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdMin(", "simd_min(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdProduct(", "simd_product(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdPrefixSum(", "simd_prefix_inclusive_sum(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdPrefixExclusiveSum(", "simd_prefix_exclusive_sum(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdBroadcast(", "simd_broadcast(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdShuffle(", "simd_shuffle(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdShuffleUp(", "simd_shuffle_up(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdShuffleDown(", "simd_shuffle_down(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdShuffleXor(", "simd_shuffle_xor(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdAll(", "simd_all(", StringComparison.Ordinal);
        result = result.Replace("Kernel.SimdAny(", "simd_any(", StringComparison.Ordinal);

        return result;
    }

    private static string TranslateSynchronization(string line)
    {
        var result = line;

        // Kernel barriers
        result = result.Replace("Kernel.Barrier()", "threadgroup_barrier(mem_flags::mem_threadgroup)", StringComparison.Ordinal);
        result = result.Replace("Kernel.DeviceBarrier()", "threadgroup_barrier(mem_flags::mem_device)", StringComparison.Ordinal);
        result = result.Replace("Kernel.FullBarrier()", "threadgroup_barrier(mem_flags::mem_device_and_threadgroup)", StringComparison.Ordinal);

        // Simdgroup barrier
        result = result.Replace("Kernel.SimdBarrier()", "simdgroup_barrier(mem_flags::mem_none)", StringComparison.Ordinal);

        return result;
    }

    private static string TranslateTypeCasts(string line)
    {
        var result = line;

        // Scalar casts
        result = Regex.Replace(result, @"\(float\)\s*(\w+)", "float($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(int\)\s*(\w+)", "int($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(uint\)\s*(\w+)", "uint($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(double\)\s*(\w+)", "float($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(half\)\s*(\w+)", "half($1)", RegexOptions.None);

        // Vector casts
        result = Regex.Replace(result, @"\(float2\)\s*(\w+)", "float2($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(float3\)\s*(\w+)", "float3($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(float4\)\s*(\w+)", "float4($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(int2\)\s*(\w+)", "int2($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(int3\)\s*(\w+)", "int3($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(int4\)\s*(\w+)", "int4($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(uint2\)\s*(\w+)", "uint2($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(uint3\)\s*(\w+)", "uint3($1)", RegexOptions.None);
        result = Regex.Replace(result, @"\(uint4\)\s*(\w+)", "uint4($1)", RegexOptions.None);

        return result;
    }

    private static string TranslateControlFlow(string line)
    {
        var result = line;

        // Remove C# specific keywords that aren't needed in MSL
        result = result.Replace("readonly ", "", StringComparison.Ordinal);
        result = result.Replace("unsafe ", "", StringComparison.Ordinal);

        // Fix variable declarations
        result = Regex.Replace(result, @"\bvar\s+", "auto ", RegexOptions.None);

        return result;
    }

    private static int FindMatchingParen(string code, int openIndex)
    {
        int depth = 1;
        for (int i = openIndex + 1; i < code.Length; i++)
        {
            if (code[i] == '(') depth++;
            else if (code[i] == ')') depth--;
            if (depth == 0) return i;
        }
        throw new InvalidOperationException("Unmatched parenthesis");
    }

    private static int FindMatchingBrace(string code, int openIndex)
    {
        int depth = 1;
        for (int i = openIndex + 1; i < code.Length; i++)
        {
            if (code[i] == '{') depth++;
            else if (code[i] == '}') depth--;
            if (depth == 0) return i;
        }
        throw new InvalidOperationException("Unmatched brace");
    }
}
