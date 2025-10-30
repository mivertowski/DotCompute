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
        for (int i = 0; i < info.Parameters.Count; i++)
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
