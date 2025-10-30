// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.RegularExpressions;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Provides consistent formatting and naming conventions for generated kernel code.
/// Ensures all generated code follows established patterns and maintains
/// consistency across different generation components.
/// </summary>
/// <remarks>
/// This class centralizes all formatting logic to ensure:
/// - Consistent naming conventions across all generated code
/// - Proper sanitization of user input for code generation
/// - Standard formatting patterns for different code elements
/// - Cross-platform compatibility for generated identifiers
/// </remarks>
public sealed class KernelFormatters
{
    private static readonly Regex _invalidCharacters = new(@"[^\w\d_]", RegexOptions.Compiled);
    private static readonly Regex _multipleUnderscores = new(@"_{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Formats a kernel name for use as a class name.
    /// </summary>
    /// <param name="kernelName">The original kernel name.</param>
    /// <returns>A properly formatted class name.</returns>
    /// <remarks>
    /// This method ensures the resulting name:
    /// - Starts with an uppercase letter
    /// - Contains only valid identifier characters
    /// - Follows PascalCase convention
    /// - Is unique and collision-free
    /// </remarks>
    public static string FormatKernelExecutorClassName(string kernelName)
    {
        var sanitized = SanitizeIdentifier(kernelName);
        var pascalCase = ToPascalCase(sanitized);
        return $"{pascalCase}KernelExecutor";
    }

    /// <summary>
    /// Formats a kernel name for use as an invoker class name.
    /// </summary>
    /// <param name="className">The original class name.</param>
    /// <returns>A properly formatted invoker class name.</returns>
    public static string FormatInvokerClassName(string className)
    {
        var sanitized = SanitizeIdentifier(className);
        var pascalCase = ToPascalCase(sanitized);
        return $"{pascalCase}Invoker";
    }

    /// <summary>
    /// Formats a namespace for generated code.
    /// </summary>
    /// <param name="originalNamespace">The original namespace.</param>
    /// <returns>A formatted namespace with .Generated suffix.</returns>
    public static string FormatGeneratedNamespace(string originalNamespace)
    {
        if (string.IsNullOrEmpty(originalNamespace))
        {
            return "DotCompute.Generated";
        }

        return $"{originalNamespace}.Generated";
    }

    /// <summary>
    /// Formats a namespace for backend-specific generated code.
    /// </summary>
    /// <param name="originalNamespace">The original namespace.</param>
    /// <param name="backend">The target backend (CPU, CUDA, Metal, etc.).</param>
    /// <returns>A formatted namespace with backend suffix.</returns>
    public static string FormatBackendNamespace(string originalNamespace, string backend)
    {
        var generatedNamespace = FormatGeneratedNamespace(originalNamespace);
        return $"{generatedNamespace}.{backend}";
    }

    /// <summary>
    /// Formats a method name for generated implementations.
    /// </summary>
    /// <param name="methodName">The original method name.</param>
    /// <param name="backend">The target backend.</param>
    /// <param name="variant">The implementation variant (SIMD, Scalar, Parallel, etc.).</param>
    /// <returns>A formatted method name.</returns>
    public static string FormatImplementationMethodName(string methodName, string backend, string variant)
    {
        var sanitizedMethod = SanitizeIdentifier(methodName);
        _ = ToPascalCase(sanitizedMethod);
        var pascalVariant = ToPascalCase(variant);

        return $"Execute{pascalVariant}";
    }

    /// <summary>
    /// Formats a file name for generated source files.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="backend">The backend name.</param>
    /// <param name="variant">The variant name (optional).</param>
    /// <returns>A formatted file name with .g.cs extension.</returns>
    public static string FormatGeneratedFileName(string typeName, string methodName, string backend, string? variant = null)
    {
        var sanitizedType = SanitizeFileName(typeName);
        var sanitizedMethod = SanitizeFileName(methodName);
        var sanitizedBackend = SanitizeFileName(backend);

        var fileName = $"{sanitizedType}_{sanitizedMethod}_{sanitizedBackend}";

        if (!string.IsNullOrEmpty(variant))
        {
            var sanitizedVariant = SanitizeFileName(variant!);
            fileName += $"_{sanitizedVariant}";
        }

        return $"{fileName}.g.cs";
    }

    /// <summary>
    /// Formats a parameter list for method declarations.
    /// </summary>
    /// <param name="parameters">The parameters to format.</param>
    /// <param name="includeTypes">Whether to include type information.</param>
    /// <param name="includeModifiers">Whether to include parameter modifiers.</param>
    /// <returns>A formatted parameter list.</returns>
    public string FormatParameterList(
        IEnumerable<Models.Kernel.ParameterInfo> parameters,
        bool includeTypes = true,
        bool includeModifiers = false)
    {
        var formattedParams = parameters.Select(p =>
        {
            var parts = new List<string>();

            if (includeModifiers && p.IsReadOnly && !p.Type.Contains("ReadOnly"))
            {
                parts.Add("in");
            }

            if (includeTypes)
            {
                parts.Add(FormatTypeName(p.Type));
            }

            parts.Add(FormatParameterName(p.Name));

            return string.Join(" ", parts);
        });

        return string.Join(", ", formattedParams);
    }

    /// <summary>
    /// Formats a type name for code generation.
    /// </summary>
    /// <param name="typeName">The original type name.</param>
    /// <returns>A formatted type name suitable for code generation.</returns>
    public string FormatTypeName(string typeName)
    {
        // Handle generic types with proper formatting
        if (typeName.Contains('<') && typeName.Contains('>'))
        {
            return FormatGenericTypeName(typeName);
        }

        // Handle array types
        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = typeName.Substring(0, typeName.Length - 2);
            return $"{FormatTypeName(elementType)}[]";
        }

        // Handle pointer types
        if (typeName.EndsWith("*", StringComparison.Ordinal))
        {
            var baseType = typeName.Substring(0, typeName.Length - 1);
            return $"{FormatTypeName(baseType)}*";
        }

        // Apply standard type aliases
        return ApplyTypeAliases(typeName);
    }

    /// <summary>
    /// Formats a parameter name for code generation.
    /// </summary>
    /// <param name="parameterName">The original parameter name.</param>
    /// <returns>A formatted parameter name.</returns>
    public static string FormatParameterName(string parameterName)
    {
        var sanitized = SanitizeIdentifier(parameterName);
        return ToCamelCase(sanitized);
    }

    /// <summary>
    /// Formats a constant name for code generation.
    /// </summary>
    /// <param name="constantName">The original constant name.</param>
    /// <returns>A formatted constant name in UPPER_CASE.</returns>
    public static string FormatConstantName(string constantName)
    {
        var sanitized = SanitizeIdentifier(constantName);
        return ToUpperSnakeCase(sanitized);
    }

    /// <summary>
    /// Formats an enum value name for code generation.
    /// </summary>
    /// <param name="enumValue">The original enum value.</param>
    /// <returns>A formatted enum value name.</returns>
    public static string FormatEnumValueName(string enumValue)
    {
        var sanitized = SanitizeIdentifier(enumValue);
        return ToPascalCase(sanitized);
    }

    /// <summary>
    /// Formats code indentation with consistent spacing.
    /// </summary>
    /// <param name="code">The code to indent.</param>
    /// <param name="indentLevel">The indentation level (0-based).</param>
    /// <param name="indentSize">The number of spaces per indent level.</param>
    /// <returns>Properly indented code.</returns>
    public static string FormatIndentation(string code, int indentLevel, int indentSize = 4)
    {
        if (string.IsNullOrEmpty(code) || indentLevel <= 0)
        {
            return code;
        }

        var indent = new string(' ', indentLevel * indentSize);
        var lines = code.Split('\n');

        return string.Join("\n", lines.Select(line =>
            string.IsNullOrWhiteSpace(line) ? line : indent + line.TrimStart()));
    }

    /// <summary>
    /// Formats a comment block with consistent styling.
    /// </summary>
    /// <param name="comment">The comment text.</param>
    /// <param name="style">The comment style (single-line or block).</param>
    /// <param name="maxLineLength">Maximum length per line.</param>
    /// <returns>A formatted comment block.</returns>
    public static string FormatComment(string comment, CommentStyle style = CommentStyle.SingleLine, int maxLineLength = 80)
    {
        if (string.IsNullOrEmpty(comment))
        {
            return string.Empty;
        }

        var lines = WrapText(comment, maxLineLength);

        return style switch
        {
            CommentStyle.SingleLine => string.Join("\n", lines.Select(line => $"// {line}")),
            CommentStyle.Block => $"/* {string.Join("\n   ", lines)} */",
            CommentStyle.XmlDoc => string.Join("\n", lines.Select(line => $"/// {line}")),
            _ => comment
        };
    }

    /// <summary>
    /// Sanitizes an identifier for use in generated code.
    /// </summary>
    /// <param name="identifier">The original identifier.</param>
    /// <returns>A sanitized identifier safe for code generation.</returns>
    private static string SanitizeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return "DefaultName";
        }

        // Remove invalid characters
        var sanitized = _invalidCharacters.Replace(identifier, "_");

        // Collapse multiple underscores
        sanitized = _multipleUnderscores.Replace(sanitized, "_");

        // Ensure it starts with a letter or underscore
        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = "_" + sanitized;
        }

        // Remove leading/trailing underscores
        sanitized = sanitized.Trim('_');

        // Ensure we have a valid identifier
        return string.IsNullOrEmpty(sanitized) ? "DefaultName" : sanitized;
    }

    /// <summary>
    /// Sanitizes a filename for cross-platform compatibility.
    /// </summary>
    /// <param name="fileName">The original filename.</param>
    /// <returns>A sanitized filename.</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return "DefaultFile";
        }

        // Replace problematic characters with underscores
        var invalidChars = new[] { '<', '>', ':', '"', '|', '?', '*', '/', '\\', '.' };
        var sanitized = fileName;

        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Collapse multiple underscores
        sanitized = _multipleUnderscores.Replace(sanitized, "_");

        return sanitized.Trim('_');
    }

    /// <summary>
    /// Converts a string to PascalCase.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>A PascalCase string.</returns>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var words = input.Split(['_'], StringSplitOptions.RemoveEmptyEntries);
#pragma warning disable CA1308 // Lowercase required for PascalCase formatting
        return string.Join("", words.Select(word =>
            char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant()));
#pragma warning restore CA1308
    }

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>A camelCase string.</returns>
    private static string ToCamelCase(string input)
    {
        var pascalCase = ToPascalCase(input);
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
    }

    /// <summary>
    /// Converts a string to UPPER_SNAKE_CASE.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>An UPPER_SNAKE_CASE string.</returns>
    private static string ToUpperSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Split on camelCase boundaries and existing underscores
        var words = Regex.Split(input, @"(?<!^)(?=[A-Z])|_")
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w.ToUpperInvariant());

        return string.Join("_", words);
    }

    /// <summary>
    /// Formats a generic type name with proper syntax.
    /// </summary>
    /// <param name="typeName">The generic type name.</param>
    /// <returns>A properly formatted generic type name.</returns>
    private string FormatGenericTypeName(string typeName)
    {
        var openIndex = typeName.IndexOf('<');
        var closeIndex = typeName.LastIndexOf('>');

        if (openIndex == -1 || closeIndex == -1 || closeIndex <= openIndex)
        {
            return typeName;
        }

        var genericPart = typeName.Substring(0, openIndex);
        var typeArgs = typeName.Substring(openIndex + 1, closeIndex - openIndex - 1);

        // Format the type arguments
        var formattedArgs = typeArgs.Split(',')
            .Select(arg => FormatTypeName(arg.Trim()))
            .ToArray();

        return $"{FormatTypeName(genericPart)}<{string.Join(", ", formattedArgs)}>";
    }

    /// <summary>
    /// Applies standard type aliases for common types.
    /// </summary>
    /// <param name="typeName">The original type name.</param>
    /// <returns>The type name with aliases applied.</returns>
    private static string ApplyTypeAliases(string typeName)
    {
        return typeName switch
        {
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Boolean" => "bool",
            "System.Char" => "char",
            "System.String" => "string",
            "System.Object" => "object",
            "System.Void" => "void",
            _ => typeName
        };
    }

    /// <summary>
    /// Wraps text to fit within specified line length.
    /// </summary>
    /// <param name="text">The text to wrap.</param>
    /// <param name="maxLength">The maximum line length.</param>
    /// <returns>An array of wrapped lines.</returns>
    private static string[] WrapText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return [text];
        }

        var words = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = new System.Text.StringBuilder();

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxLength && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                _ = currentLine.Clear();
            }

            if (currentLine.Length > 0)
            {
                _ = currentLine.Append(' ');
            }

            _ = currentLine.Append(word);
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return [.. lines];
    }
}

/// <summary>
/// Defines the available comment styles for formatting.
/// </summary>
public enum CommentStyle
{
    /// <summary>
    /// Single-line comments using // syntax.
    /// </summary>
    SingleLine,

    /// <summary>
    /// Block comments using /* */ syntax.
    /// </summary>
    Block,

    /// <summary>
    /// XML documentation comments using /// syntax.
    /// </summary>
    XmlDoc
}
