// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using DotCompute.Backends.Metal.Translation;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Compilation;

/// <summary>
/// Service for discovering and translating C# message handlers to MSL device functions for Ring Kernels.
/// </summary>
/// <remarks>
/// <para>
/// This service implements the C# to MSL translation pipeline for Ring Kernel message handlers:
/// <list type="number">
/// <item><description>Discovers handler classes by naming convention (e.g., VectorAddHandler for VectorAddRequest)</description></item>
/// <item><description>Validates handler method signatures (ProcessMessage with correct parameters)</description></item>
/// <item><description>Uses CSharpToMSLTranslator to convert C# method body to MSL</description></item>
/// <item><description>Wraps translated code in Metal function with proper signature</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Handler Convention:</b>
/// <list type="bullet">
/// <item><description>Handler class name: {MessageType}Handler (e.g., VectorAddHandler)</description></item>
/// <item><description>Handler method: static bool ProcessMessage(Span&lt;byte&gt; inputBuffer, int inputSize, Span&lt;byte&gt; outputBuffer, int outputSize)</description></item>
/// <item><description>Returns: true on success, false on failure</description></item>
/// </list>
/// </para>
/// <para>
/// <b>MSL Output:</b>
/// <code>
/// bool process_vector_add_message(
///     device uchar* input_buffer,
///     int input_size,
///     device uchar* output_buffer,
///     int output_size)
/// {
///     // Translated C# code...
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class MetalHandlerTranslationService
{
    private readonly ILogger<MetalHandlerTranslationService> _logger;
    private readonly CSharpToMSLTranslator _translator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalHandlerTranslationService"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <param name="translatorLogger">Logger for the translator.</param>
    public MetalHandlerTranslationService(
        ILogger<MetalHandlerTranslationService> logger,
        ILogger translatorLogger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _translator = new CSharpToMSLTranslator(translatorLogger ?? throw new ArgumentNullException(nameof(translatorLogger)));
    }

    /// <summary>
    /// Discovers and translates a message handler for the specified message type using runtime reflection.
    /// </summary>
    /// <param name="messageTypeName">Simple name of the message type (e.g., "VectorAddRequest").</param>
    /// <param name="assemblies">Assemblies to search for handler classes.</param>
    /// <returns>Translated MSL function code, or null if no handler found.</returns>
    [RequiresUnreferencedCode("Handler discovery uses runtime reflection which is not compatible with trimming.")]
    public string? TranslateHandler(string messageTypeName, IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageTypeName);

        // Derive handler class name by convention
        var baseName = DeriveBaseName(messageTypeName);
        var handlerClassName = $"{baseName}Handler";

        _logger.LogDebug("Searching for handler class '{HandlerClassName}' for message type '{MessageType}'",
            handlerClassName, messageTypeName);

        // Search for handler class in assemblies
        var assembliesArray = assemblies?.ToArray() ?? AppDomain.CurrentDomain.GetAssemblies();
        var handlerType = FindHandlerClass(assembliesArray, handlerClassName);

        if (handlerType == null)
        {
            _logger.LogDebug("No handler class found for message type '{MessageType}'", messageTypeName);
            return null; // No handler found - this is optional
        }

        // Find ProcessMessage method
        var processMessageMethod = FindProcessMessageMethod(handlerType);
        if (processMessageMethod == null)
        {
            _logger.LogWarning(
                "Handler class '{HandlerClassName}' found but no valid ProcessMessage method. " +
                "Expected: static bool ProcessMessage(Span<byte> inputBuffer, int inputSize, Span<byte> outputBuffer, int outputSize)",
                handlerClassName);
            return null;
        }

        try
        {
            // Get the source code for translation
            var sourceCode = ExtractMethodSourceCode(processMessageMethod);
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                _logger.LogWarning("Could not extract source code for ProcessMessage in '{HandlerClassName}'",
                    handlerClassName);
                return GenerateDefaultHandler(baseName);
            }

            // Translate method body using CSharpToMSLTranslator
            var translatedBody = _translator.Translate(
                sourceCode,
                $"{baseName}Handler",
                $"process_{ToSnakeCase(baseName)}_message");

            // Generate MSL function wrapper
            var mslFunctionName = $"process_{ToSnakeCase(baseName)}_message";
            return WrapInMslFunction(mslFunctionName, translatedBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to translate handler for '{MessageType}', using default handler",
                messageTypeName);
            return GenerateDefaultHandler(baseName);
        }
    }

    /// <summary>
    /// Generates MSL handler stubs for multiple message types.
    /// </summary>
    /// <param name="messageTypeNames">Collection of message type names.</param>
    /// <param name="assemblies">Assemblies to search for handler classes.</param>
    /// <returns>Combined MSL code with all translated handlers.</returns>
    [RequiresUnreferencedCode("Handler discovery uses runtime reflection which is not compatible with trimming.")]
    public string TranslateHandlers(IEnumerable<string> messageTypeNames, IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(messageTypeNames);

        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated Ring Kernel message handlers");
        sb.AppendLine("// Generated by MetalHandlerTranslationService");
        sb.AppendLine();

        foreach (var messageTypeName in messageTypeNames)
        {
            var handler = TranslateHandler(messageTypeName, assemblies);
            if (handler != null)
            {
                sb.AppendLine(handler);
            }
            else
            {
                // Generate a default passthrough handler
                var baseName = DeriveBaseName(messageTypeName);
                sb.AppendLine(GenerateDefaultHandler(baseName));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Derives the base name from a message type name by removing common suffixes.
    /// </summary>
    private static string DeriveBaseName(string messageTypeName)
    {
        return messageTypeName
            .Replace("Request", "", StringComparison.Ordinal)
            .Replace("Response", "", StringComparison.Ordinal)
            .Replace("Message", "", StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds a handler class by name in the specified assemblies using reflection.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection to find types.")]
    private Type? FindHandlerClass(Assembly[] assemblies, string className)
    {
        foreach (var assembly in assemblies)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == className && type.IsClass && type.IsAbstract && type.IsSealed) // static class
                    {
                        _logger.LogDebug("Found handler class '{ClassName}' in assembly '{Assembly}'",
                            className, assembly.GetName().Name);
                        return type;
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.LogDebug("Could not load types from assembly '{Assembly}': {Error}",
                    assembly.GetName().Name, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error scanning assembly '{Assembly}': {Error}",
                    assembly.GetName().Name, ex.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the ProcessMessage method in a handler class.
    /// </summary>
    private static MethodInfo? FindProcessMessageMethod(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type handlerClass)
    {
        var methods = handlerClass.GetMethods(BindingFlags.Public | BindingFlags.Static);

        return methods.FirstOrDefault(m =>
            m.Name == "ProcessMessage" &&
            m.IsStatic &&
            m.ReturnType == typeof(bool) &&
            m.GetParameters().Length == 4);
    }

    /// <summary>
    /// Attempts to extract source code from a method for translation.
    /// </summary>
    /// <remarks>
    /// Note: Runtime reflection cannot access source code directly.
    /// This method returns a stub that can be enhanced with source generators or IL decompilation.
    /// </remarks>
    private static string? ExtractMethodSourceCode(MethodInfo method)
    {
        // Runtime reflection cannot access source code
        // Return null to indicate we need to generate a default handler
        // In production, this would integrate with source generators or Roslyn at compile time
        _ = method; // Suppress unused warning
        return null;
    }

    /// <summary>
    /// Generates a default passthrough handler when source translation is not available.
    /// </summary>
    private static string GenerateDefaultHandler(string baseName)
    {
        var functionName = $"process_{ToSnakeCase(baseName)}_message";

        var sb = new StringBuilder();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Default message handler: {functionName}");
        sb.AppendLine("/// Source translation not available - passthrough implementation");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"bool {functionName}(");
        sb.AppendLine("    device uchar* input_buffer,");
        sb.AppendLine("    int input_size,");
        sb.AppendLine("    device uchar* output_buffer,");
        sb.AppendLine("    int output_size)");
        sb.AppendLine("{");
        sb.AppendLine("    // Default implementation: copy input to output if sizes match");
        sb.AppendLine("    if (input_size <= output_size) {");
        sb.AppendLine("        for (int i = 0; i < input_size; i++) {");
        sb.AppendLine("            output_buffer[i] = input_buffer[i];");
        sb.AppendLine("        }");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine("    return false;");
        sb.AppendLine("}");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Wraps translated C# code in an MSL function.
    /// </summary>
    private static string WrapInMslFunction(string functionName, string translatedBody)
    {
        // If the translator already generated a complete function, return as-is
        if (translatedBody.Contains($"bool {functionName}(", StringComparison.Ordinal) ||
            translatedBody.Contains("kernel void", StringComparison.Ordinal))
        {
            return translatedBody;
        }

        var sb = new StringBuilder();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Auto-translated message handler: {functionName}");
        sb.AppendLine("/// Original C# method translated by CSharpToMSLTranslator");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"bool {functionName}(");
        sb.AppendLine("    device uchar* input_buffer,");
        sb.AppendLine("    int input_size,");
        sb.AppendLine("    device uchar* output_buffer,");
        sb.AppendLine("    int output_size)");
        sb.AppendLine("{");

        // Add translated body with proper indentation
        foreach (var line in translatedBody.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                sb.Append("    ");
                sb.AppendLine(line.TrimEnd());
            }
            else
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Converts PascalCase to snake_case.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            if (i > 0 && char.IsUpper(input[i]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(input[i]));
        }

        return sb.ToString();
    }
}
