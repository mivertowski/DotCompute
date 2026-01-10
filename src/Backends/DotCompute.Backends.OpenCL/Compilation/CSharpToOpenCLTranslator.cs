// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Compilation;

/// <summary>
/// Translates C# kernel definitions to OpenCL C source code.
/// Supports the DotCompute [Kernel] attribute pattern with full type mapping and intrinsics.
/// </summary>
/// <remarks>
/// This translator converts C# kernel methods annotated with [Kernel] attribute to OpenCL C code.
/// It handles:
/// - Type mappings (Span&lt;T&gt; to __global T*, etc.)
/// - Threading model (Kernel.ThreadId.X to get_global_id(0))
/// - Operations and expressions
/// - Memory qualifiers (__global, __local, __constant)
/// - Bounds checking and safety
/// </remarks>
public sealed partial class CSharpToOpenCLTranslator
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpToOpenCLTranslator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public CSharpToOpenCLTranslator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Translates a C# kernel definition to OpenCL C source code.
    /// </summary>
    /// <param name="definition">The kernel definition containing C# source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OpenCL C source code ready for compilation.</returns>
    public async Task<string> TranslateAsync(KernelDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Source))
        {
            throw new ArgumentException("Kernel source cannot be null or empty", nameof(definition));
        }

        LogTranslationStarted(_logger, definition.Name);

        await Task.Yield(); // Ensure async execution

        try
        {
            // Parse kernel metadata from definition
            var kernelInfo = ParseKernelInfo(definition);

            // Generate OpenCL C code
            var openclCode = GenerateOpenCLKernel(kernelInfo);

            LogTranslationCompleted(_logger, definition.Name, openclCode.Length);

            return openclCode;
        }
        catch (Exception ex)
        {
            LogTranslationFailed(_logger, ex, definition.Name);
            throw new InvalidOperationException($"Failed to translate kernel '{definition.Name}' to OpenCL C", ex);
        }
    }

    /// <summary>
    /// Parses kernel information from the kernel definition.
    /// </summary>
    private static KernelTranslationInfo ParseKernelInfo(KernelDefinition definition)
    {
        var parameters = new List<KernelParameter>();

        // Extract parameters from metadata
        if (definition.Metadata?.TryGetValue("Parameters", out var parametersObj) == true &&
            parametersObj is IReadOnlyList<object> parametersArray)
        {
            foreach (var param in parametersArray)
            {
                if (param is not IDictionary<string, object> paramDict)
                {
                    continue;
                }

                var paramName = paramDict.TryGetValue("Name", out var name) ? name?.ToString() ?? "unknown" : "unknown";
                var paramType = paramDict.TryGetValue("Type", out var type) ? type?.ToString() ?? "void" : "void";
                var isInput = paramDict.TryGetValue("IsInput", out var isInputVal) && Convert.ToBoolean(isInputVal, System.Globalization.CultureInfo.InvariantCulture);
                var isOutput = paramDict.TryGetValue("IsOutput", out var isOutputVal) && Convert.ToBoolean(isOutputVal, System.Globalization.CultureInfo.InvariantCulture);

                var parameter = new KernelParameter
                {
                    Name = paramName,
                    Type = paramType,
                    OpenCLType = MapCSharpTypeToOpenCL(paramType, isInput, isOutput),
                    IsInput = isInput,
                    IsOutput = isOutput
                };

                parameters.Add(parameter);
            }
        }

        // Extract body from source
        var body = ExtractKernelBody(definition.Source!);

        var info = new KernelTranslationInfo
        {
            Name = definition.EntryPoint ?? definition.Name,
            Source = definition.Source!,
            Parameters = parameters,
            Body = body
        };

        return info;
    }

    /// <summary>
    /// Extracts the kernel body from C# source code.
    /// </summary>
    private static string ExtractKernelBody(string source)
    {
        // Find the method body between { and }
#pragma warning disable CA1307 // Specify StringComparison for clarity - Not applicable for char overloads
        var startIndex = source.IndexOf('{');
        var endIndex = source.LastIndexOf('}');
#pragma warning restore CA1307

        if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
        {
            return source; // Return as-is if we can't extract
        }

        return source.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
    }

    /// <summary>
    /// Maps C# types to OpenCL C types with appropriate memory qualifiers.
    /// </summary>
    private static string MapCSharpTypeToOpenCL(string csharpType, bool isInput, bool isOutput)
    {
        // Handle generic types like Span<T>, ReadOnlySpan<T>
        if (csharpType.StartsWith("System.Span<", StringComparison.Ordinal) ||
            csharpType.StartsWith("Span<", StringComparison.Ordinal))
        {
            var elementType = ExtractGenericType(csharpType);
            var openclType = MapPrimitiveType(elementType);
            return isOutput ? $"__global {openclType}*" : $"__global {openclType}*";
        }

        if (csharpType.StartsWith("System.ReadOnlySpan<", StringComparison.Ordinal) ||
            csharpType.StartsWith("ReadOnlySpan<", StringComparison.Ordinal))
        {
            var elementType = ExtractGenericType(csharpType);
            var openclType = MapPrimitiveType(elementType);
            return $"__global const {openclType}*";
        }

        // Handle primitive types
        return MapPrimitiveType(csharpType);
    }

    /// <summary>
    /// Extracts the generic type parameter from a generic type name.
    /// </summary>
    private static string ExtractGenericType(string genericType)
    {
#pragma warning disable CA1307 // Specify StringComparison for clarity - Not applicable for char overloads
        var startIndex = genericType.IndexOf('<');
        var endIndex = genericType.LastIndexOf('>');
#pragma warning restore CA1307

        if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
        {
            return "float"; // Default to float
        }

        return genericType.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
    }

    /// <summary>
    /// Maps C# primitive types to OpenCL C primitive types.
    /// </summary>
    private static string MapPrimitiveType(string csharpType)
    {
        return csharpType switch
        {
            "System.Single" or "float" or "Float" => "float",
            "System.Double" or "double" or "Double" => "double",
            "System.Int32" or "int" or "Int32" => "int",
            "System.UInt32" or "uint" or "UInt32" => "uint",
            "System.Int64" or "long" or "Int64" => "long",
            "System.UInt64" or "ulong" or "UInt64" => "ulong",
            "System.Int16" or "short" or "Int16" => "short",
            "System.UInt16" or "ushort" or "UInt16" => "ushort",
            "System.Byte" or "byte" or "Byte" => "uchar",
            "System.SByte" or "sbyte" or "SByte" => "char",
            _ => "float" // Default to float for unknown types
        };
    }

    /// <summary>
    /// Generates OpenCL C kernel source code from kernel information.
    /// </summary>
    private static string GenerateOpenCLKernel(KernelTranslationInfo info)
    {
        var sb = new StringBuilder();

        // Add header comment
        sb.AppendLine("// Auto-generated OpenCL C kernel from C# source");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"// Kernel: {info.Name}");
        sb.AppendLine();

        // Generate kernel signature
        sb.Append("__kernel void ");
        sb.Append(info.Name);
        sb.Append('(');

        // Add parameters
        for (var i = 0; i < info.Parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var param = info.Parameters[i];
            sb.Append(param.OpenCLType);
            sb.Append(' ');
            sb.Append(param.Name);
        }

        sb.AppendLine(")");
        sb.AppendLine("{");

        // Translate body
        var translatedBody = TranslateKernelBody(info.Body);
        sb.AppendLine(translatedBody);

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Translates C# kernel body to OpenCL C.
    /// </summary>
    private static string TranslateKernelBody(string body)
    {
        var sb = new StringBuilder();
        var lines = body.Split('\n', StringSplitOptions.None);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                sb.AppendLine();
                continue;
            }

            // Translate line
            var translatedLine = TranslateLine(trimmedLine);
            sb.AppendLine("    " + translatedLine); // Add indentation
        }

        return sb.ToString();
    }

    /// <summary>
    /// Translates a single line of C# code to OpenCL C.
    /// </summary>
    private static string TranslateLine(string line)
    {
        // Handle Kernel.ThreadId.X/Y/Z
        line = line.Replace("Kernel.ThreadId.X", "get_global_id(0)", StringComparison.Ordinal);
        line = line.Replace("Kernel.ThreadId.Y", "get_global_id(1)", StringComparison.Ordinal);
        line = line.Replace("Kernel.ThreadId.Z", "get_global_id(2)", StringComparison.Ordinal);

        // Handle Kernel.GroupId.X/Y/Z
        line = line.Replace("Kernel.GroupId.X", "get_group_id(0)", StringComparison.Ordinal);
        line = line.Replace("Kernel.GroupId.Y", "get_group_id(1)", StringComparison.Ordinal);
        line = line.Replace("Kernel.GroupId.Z", "get_group_id(2)", StringComparison.Ordinal);

        // Handle Kernel.LocalThreadId.X/Y/Z
        line = line.Replace("Kernel.LocalThreadId.X", "get_local_id(0)", StringComparison.Ordinal);
        line = line.Replace("Kernel.LocalThreadId.Y", "get_local_id(1)", StringComparison.Ordinal);
        line = line.Replace("Kernel.LocalThreadId.Z", "get_local_id(2)", StringComparison.Ordinal);

        // Handle atomic operations (RingKernelContext and AtomicOps)
        line = TranslateAtomicOperations(line);

        // Handle memory fences and barriers
        line = TranslateMemoryFences(line);

        // Handle array indexing - Span<T> access remains the same in OpenCL
        // e.g., result[idx] stays as result[idx] since we map Span<T> to __global T*

        // Handle type conversions
        line = line.Replace("(float)", "(float)", StringComparison.Ordinal);
        line = line.Replace("(int)", "(int)", StringComparison.Ordinal);
        line = line.Replace("(uint)", "(uint)", StringComparison.Ordinal);

        // Handle common functions
        line = line.Replace("Math.Sqrt", "sqrt", StringComparison.Ordinal);
        line = line.Replace("Math.Sin", "sin", StringComparison.Ordinal);
        line = line.Replace("Math.Cos", "cos", StringComparison.Ordinal);
        line = line.Replace("Math.Tan", "tan", StringComparison.Ordinal);
        line = line.Replace("Math.Pow", "pow", StringComparison.Ordinal);
        line = line.Replace("Math.Exp", "exp", StringComparison.Ordinal);
        line = line.Replace("Math.Log", "log", StringComparison.Ordinal);
        line = line.Replace("Math.Abs", "fabs", StringComparison.Ordinal);
        line = line.Replace("Math.Floor", "floor", StringComparison.Ordinal);
        line = line.Replace("Math.Ceil", "ceil", StringComparison.Ordinal);
        line = line.Replace("Math.Min", "min", StringComparison.Ordinal);
        line = line.Replace("Math.Max", "max", StringComparison.Ordinal);

        // Handle Length property for bounds checking
        line = line.Replace(".Length", "_length", StringComparison.Ordinal);

        return line;
    }

    /// <summary>
    /// Translates atomic operations from C# to OpenCL C.
    /// </summary>
    /// <remarks>
    /// Supports:
    /// - RingKernelContext atomics (ctx.AtomicAdd, ctx.AtomicSub, etc.)
    /// - Static AtomicOps class methods (AtomicOps.Add, AtomicOps.Sub, etc.)
    /// </remarks>
    private static string TranslateAtomicOperations(string line)
    {
        // RingKernelContext instance methods (ctx.AtomicXxx)
        // AtomicAdd(ref x, v) → atomic_add(&x, v)
        line = CtxAtomicAddRegex().Replace(line, "atomic_add(&$1, $2)");
        line = CtxAtomicSubRegex().Replace(line, "atomic_sub(&$1, $2)");
        line = CtxAtomicCasRegex().Replace(line, "atomic_cmpxchg(&$1, $2, $3)");
        line = CtxAtomicExchRegex().Replace(line, "atomic_xchg(&$1, $2)");
        line = CtxAtomicMinRegex().Replace(line, "atomic_min(&$1, $2)");
        line = CtxAtomicMaxRegex().Replace(line, "atomic_max(&$1, $2)");
        line = CtxAtomicAndRegex().Replace(line, "atomic_and(&$1, $2)");
        line = CtxAtomicOrRegex().Replace(line, "atomic_or(&$1, $2)");
        line = CtxAtomicXorRegex().Replace(line, "atomic_xor(&$1, $2)");

        // Static AtomicOps class methods (AtomicOps.Xxx)
        // AtomicOps.Add(ref x, v) → atomic_add(&x, v)
        line = AtomicOpsAddRegex().Replace(line, "atomic_add(&$1, $2)");
        line = AtomicOpsSubRegex().Replace(line, "atomic_sub(&$1, $2)");
        line = AtomicOpsExchangeRegex().Replace(line, "atomic_xchg(&$1, $2)");
        line = AtomicOpsCompareExchangeRegex().Replace(line, "atomic_cmpxchg(&$1, $2, $3)");
        line = AtomicOpsMinRegex().Replace(line, "atomic_min(&$1, $2)");
        line = AtomicOpsMaxRegex().Replace(line, "atomic_max(&$1, $2)");
        line = AtomicOpsAndRegex().Replace(line, "atomic_and(&$1, $2)");
        line = AtomicOpsOrRegex().Replace(line, "atomic_or(&$1, $2)");
        line = AtomicOpsXorRegex().Replace(line, "atomic_xor(&$1, $2)");

        // AtomicOps.Load/Store with memory ordering
        // AtomicOps.Load(ref x, MemoryOrder.Xxx) → atomic_load(&x)
        line = AtomicOpsLoadRegex().Replace(line, "atomic_load(&$1)");

        // AtomicOps.Store(ref x, v, MemoryOrder.Xxx) → atomic_store(&x, v)
        line = AtomicOpsStoreRegex().Replace(line, "atomic_store(&$1, $2)");

        return line;
    }

    /// <summary>
    /// Translates memory fences and barriers from C# to OpenCL C.
    /// </summary>
    private static string TranslateMemoryFences(string line)
    {
        // RingKernelContext fences
        line = line.Replace("ctx.ThreadFence()", "mem_fence(CLK_GLOBAL_MEM_FENCE)", StringComparison.Ordinal);
        line = line.Replace("ctx.ThreadFenceBlock()", "mem_fence(CLK_LOCAL_MEM_FENCE)", StringComparison.Ordinal);
        line = line.Replace("ctx.ThreadFenceSystem()", "mem_fence(CLK_GLOBAL_MEM_FENCE)", StringComparison.Ordinal);
        line = line.Replace("ctx.SyncThreads()", "barrier(CLK_LOCAL_MEM_FENCE)", StringComparison.Ordinal);

        // AtomicOps.ThreadFence with scope
        // Workgroup → CLK_LOCAL_MEM_FENCE, Device/System → CLK_GLOBAL_MEM_FENCE
        line = AtomicOpsFenceWorkgroupRegex().Replace(line, "mem_fence(CLK_LOCAL_MEM_FENCE)");
        line = AtomicOpsFenceDeviceRegex().Replace(line, "mem_fence(CLK_GLOBAL_MEM_FENCE)");
        line = AtomicOpsFenceSystemRegex().Replace(line, "mem_fence(CLK_GLOBAL_MEM_FENCE)");

        // AtomicOps.MemoryBarrier() → mem_fence(CLK_GLOBAL_MEM_FENCE)
        line = line.Replace("AtomicOps.MemoryBarrier()", "mem_fence(CLK_GLOBAL_MEM_FENCE)", StringComparison.Ordinal);

        return line;
    }

    #region Generated Regex for Atomic Operations

    // RingKernelContext atomics
    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicAdd\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicAddRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicSub\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicSubRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicCAS\(ref\s+(\w+),\s*([^,]+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicCasRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicExch\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicExchRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicMin\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicMinRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicMax\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicMaxRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicAnd\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicAndRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicOr\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicOrRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"ctx\.AtomicXor\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex CtxAtomicXorRegex();

    // AtomicOps static methods
    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Add\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsAddRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Sub\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsSubRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Exchange\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsExchangeRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.CompareExchange\(ref\s+(\w+),\s*([^,]+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsCompareExchangeRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Min\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsMinRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Max\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsMaxRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.And\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsAndRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Or\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsOrRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Xor\(ref\s+(\w+),\s*([^)]+)\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsXorRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Load\(ref\s+(\w+),\s*MemoryOrder\.\w+\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsLoadRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.Store\(ref\s+(\w+),\s*([^,]+),\s*MemoryOrder\.\w+\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsStoreRegex();

    // Memory fences
    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.ThreadFence\(MemoryScope\.Workgroup\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsFenceWorkgroupRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.ThreadFence\(MemoryScope\.Device\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsFenceDeviceRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"AtomicOps\.ThreadFence\(MemoryScope\.System\)")]
    private static partial System.Text.RegularExpressions.Regex AtomicOpsFenceSystemRegex();

    #endregion

    #region Logging

    [LoggerMessage(
        EventId = 10000,
        Level = LogLevel.Information,
        Message = "Starting translation of C# kernel '{KernelName}' to OpenCL C")]
    private static partial void LogTranslationStarted(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 10001,
        Level = LogLevel.Information,
        Message = "Successfully translated kernel '{KernelName}' to OpenCL C ({CodeLength} characters)")]
    private static partial void LogTranslationCompleted(ILogger logger, string kernelName, int codeLength);

    [LoggerMessage(
        EventId = 10002,
        Level = LogLevel.Error,
        Message = "Failed to translate kernel '{KernelName}' to OpenCL C")]
    private static partial void LogTranslationFailed(ILogger logger, Exception exception, string kernelName);

    #endregion

    /// <summary>
    /// Information about a kernel being translated.
    /// </summary>
    private sealed class KernelTranslationInfo
    {
        public required string Name { get; init; }
        public required string Source { get; init; }
        public required List<KernelParameter> Parameters { get; init; }
        public string Body { get; init; } = string.Empty;
    }

    /// <summary>
    /// Information about a kernel parameter.
    /// </summary>
    private sealed class KernelParameter
    {
        public required string Name { get; init; }
        public required string Type { get; init; }
        public string OpenCLType { get; init; } = "float";
        public required bool IsInput { get; init; }
        public required bool IsOutput { get; init; }
    }
}
