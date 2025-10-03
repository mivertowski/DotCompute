// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;

namespace DotCompute.Generators.Utils;

/// <summary>
/// Provides consistent code formatting utilities for generated code.
/// </summary>
/// <remarks>
/// This class ensures consistent formatting across all generated code,
/// handling indentation, line endings, and various formatting styles.
/// </remarks>
public static class CodeFormatter
{
    private static CodeStyle _defaultStyle = new();


    /// <summary>
    /// Sets the default code style for formatting operations.
    /// </summary>
    /// <param name="style">The code style to use as default.</param>
    public static void SetDefaultStyle(CodeStyle style) => _defaultStyle = style ?? throw new ArgumentNullException(nameof(style));


    /// <summary>
    /// Formats a block of code with proper indentation.
    /// </summary>
    /// <param name="code">The code to format.</param>
    /// <param name="indentLevel">The indentation level.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The formatted code.</returns>
    public static string FormatBlock(string code, int indentLevel, CodeStyle? style = null)
    {
        style ??= _defaultStyle;
        var indent = GetIndentation(indentLevel, style);
        var lines = code.Split('\n');
        var sb = new StringBuilder();


        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd();
            if (!string.IsNullOrWhiteSpace(trimmedLine))
            {
                _ = sb.Append(indent);
                _ = sb.AppendLine(trimmedLine);
            }
            else if (sb.Length > 0)
            {
                _ = sb.AppendLine();
            }
        }


        return sb.ToString();
    }


    /// <summary>
    /// Gets the indentation string for a given level.
    /// </summary>
    /// <param name="level">The indentation level.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The indentation string.</returns>
    public static string GetIndentation(int level, CodeStyle? style = null)
    {
        style ??= _defaultStyle;


        if (level <= 0)
        {

            return string.Empty;
        }


        var singleIndent = style.IndentationStyle == IndentationStyle.Tabs
            ? "\t"
            : new string(' ', style.IndentSize);


        return string.Concat(Enumerable.Repeat(singleIndent, level));
    }


    /// <summary>
    /// Formats a method signature with proper style.
    /// </summary>
    /// <param name="accessModifier">The access modifier (e.g., "public", "private").</param>
    /// <param name="isStatic">Whether the method is static.</param>
    /// <param name="returnType">The return type.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The formatted method signature.</returns>
    public static string FormatMethodSignature(
        string accessModifier,
        bool isStatic,
        string returnType,
        string methodName,
        string parameters,
        CodeStyle? style = null)
    {
        style ??= _defaultStyle;


        var sb = new StringBuilder();
        _ = sb.Append(accessModifier);


        if (isStatic)
        {
            _ = sb.Append(" static");
        }

        sb.Append(' ');
        _ = sb.Append(returnType);
        sb.Append(' ');
        _ = sb.Append(methodName);
        sb.Append('(');

        // Handle long parameter lists

        if (parameters.Length > style.MaxLineLength - sb.Length - 2)
        {
            _ = sb.AppendLine();
            var paramArray = parameters.Split(',');
            for (var i = 0; i < paramArray.Length; i++)
            {
                _ = sb.Append(GetIndentation(1, style));
                _ = sb.Append(paramArray[i].Trim());
                if (i < paramArray.Length - 1)
                {
                    _ = sb.Append(',');
                }

                _ = sb.AppendLine();
            }
        }
        else
        {
            _ = sb.Append(parameters);
        }

        _ = sb.Append(')');
        return sb.ToString();
    }


    /// <summary>
    /// Formats an opening brace according to the style settings.
    /// </summary>
    /// <param name="indentLevel">The current indentation level.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The formatted opening brace.</returns>
    public static string FormatOpeningBrace(int indentLevel, CodeStyle? style = null)
    {
        style ??= _defaultStyle;


        return style.BraceStyle == BraceStyle.NextLine
            ? $"{GetIndentation(indentLevel, style)}{{\n"
            : " {\n";
    }


    /// <summary>
    /// Formats a closing brace according to the style settings.
    /// </summary>
    /// <param name="indentLevel">The current indentation level.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The formatted closing brace.</returns>
    public static string FormatClosingBrace(int indentLevel, CodeStyle? style = null)
    {
        style ??= _defaultStyle;
        return $"{GetIndentation(indentLevel, style)}}}";
    }


    /// <summary>
    /// Normalizes line endings in the code.
    /// </summary>
    /// <param name="code">The code to normalize.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The code with normalized line endings.</returns>
    public static string NormalizeLineEndings(string code, CodeStyle? style = null)
    {
        style ??= _defaultStyle;

        // First normalize to \n

        code = code.Replace("\r\n", "\n").Replace("\r", "\n");

        // Then apply the desired style

        return style.LineEndings switch
        {
            LineEndingStyle.Windows => code.Replace("\n", "\r\n"),
            LineEndingStyle.Unix => code,
            LineEndingStyle.Auto => Environment.NewLine == "\r\n"

                ? code.Replace("\n", "\r\n")

                : code,
            _ => code
        };
    }


    /// <summary>
    /// Wraps a line of code if it exceeds the maximum length.
    /// </summary>
    /// <param name="line">The line to wrap.</param>
    /// <param name="indentLevel">The indentation level for wrapped lines.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The wrapped line.</returns>
    public static string WrapLine(string line, int indentLevel, CodeStyle? style = null)
    {
        style ??= _defaultStyle;


        if (line.Length <= style.MaxLineLength)
        {

            return line;
        }


        var sb = new StringBuilder();
        var currentLine = new StringBuilder();
        _ = GetIndentation(indentLevel, style);
        var continuationIndent = GetIndentation(indentLevel + 1, style);
        var words = line.Split(' ');


        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > style.MaxLineLength && currentLine.Length > 0)
            {
                _ = sb.AppendLine(currentLine.ToString());
                _ = currentLine.Clear();
                _ = currentLine.Append(continuationIndent);
            }


            if (currentLine.Length > 0 && !currentLine.ToString().EndsWith(" "))
            {
                _ = currentLine.Append(' ');
            }


            _ = currentLine.Append(word);
        }


        if (currentLine.Length > 0)
        {
            _ = sb.Append(currentLine);
        }


        return sb.ToString();
    }


    /// <summary>
    /// Formats a comment block with proper indentation and wrapping.
    /// </summary>
    /// <param name="comment">The comment text.</param>
    /// <param name="indentLevel">The indentation level.</param>
    /// <param name="isXmlDoc">Whether this is an XML documentation comment.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The formatted comment block.</returns>
    public static string FormatComment(string comment, int indentLevel, bool isXmlDoc = false, CodeStyle? style = null)
    {
        style ??= _defaultStyle;


        if (!style.Comments.IncludeMethodComments && isXmlDoc)
        {
            return string.Empty;
        }


        if (!style.Comments.IncludeInlineComments && !isXmlDoc)
        {

            return string.Empty;
        }


        var indent = GetIndentation(indentLevel, style);
        var prefix = isXmlDoc ? "/// " : "// ";
        var lines = comment.Split('\n');
        var sb = new StringBuilder();


        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length > 0)
            {
                var wrappedLine = WrapLine(trimmedLine, 0, style);
                var wrappedLines = wrappedLine.Split('\n');
                foreach (var wrapped in wrappedLines)
                {
                    _ = sb.Append(indent);
                    _ = sb.Append(prefix);
                    _ = sb.AppendLine(wrapped.Trim());
                }
            }
        }


        return sb.ToString();
    }


    /// <summary>
    /// Creates a formatted XML documentation comment.
    /// </summary>
    /// <param name="summary">The summary text.</param>
    /// <param name="remarks">Optional remarks text.</param>
    /// <param name="parameters">Optional parameter descriptions.</param>
    /// <param name="returns">Optional return value description.</param>
    /// <param name="indentLevel">The indentation level.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The formatted XML documentation comment.</returns>
    public static string CreateXmlDocComment(
        string summary,
        string? remarks = null,
        (string name, string description)[]? parameters = null,
        string? returns = null,
        int indentLevel = 0,
        CodeStyle? style = null)
    {
        style ??= _defaultStyle;


        if (!style.Comments.IncludeMethodComments)
        {

            return string.Empty;
        }


        var sb = new StringBuilder();
        var indent = GetIndentation(indentLevel, style);

        // Summary
        _ = sb.Append(indent);
        _ = sb.AppendLine("/// <summary>");
        foreach (var line in summary.Split('\n'))
        {
            _ = sb.Append(indent);
            _ = sb.Append("/// ");
            _ = sb.AppendLine(line.Trim());
        }
        _ = sb.Append(indent);
        _ = sb.AppendLine("/// </summary>");

        // Parameters

        if (parameters != null && parameters.Length > 0)
        {
            foreach (var (name, description) in parameters)
            {
                _ = sb.Append(indent);
                _ = sb.AppendLine($"/// <param name=\"{name}\">{description}</param>");
            }
        }

        // Returns

        if (!string.IsNullOrEmpty(returns))
        {
            _ = sb.Append(indent);
            _ = sb.AppendLine($"/// <returns>{returns}</returns>");
        }

        // Remarks

        if (!string.IsNullOrEmpty(remarks) && style.Comments.DetailLevel >= CommentDetailLevel.Normal)
        {
            _ = sb.Append(indent);
            _ = sb.AppendLine("/// <remarks>");
            foreach (var line in remarks!.Split('\n'))
            {
                _ = sb.Append(indent);
                _ = sb.Append("/// ");
                _ = sb.AppendLine(line.Trim());
            }
            _ = sb.Append(indent);
            _ = sb.AppendLine("/// </remarks>");
        }


        return sb.ToString();
    }


    /// <summary>
    /// Creates a standard file header for generated code.
    /// </summary>
    /// <param name="usings">The using directives to include.</param>
    /// <returns>The formatted header.</returns>
    public static string GenerateHeader(params string[] usings)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("// <auto-generated/>");
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine("#pragma warning disable CS8019 // Unnecessary using directive");
        _ = sb.AppendLine();

        foreach (var usingDirective in usings)
        {
            _ = sb.AppendLine($"using {usingDirective};");
        }

        _ = sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Generates a namespace declaration opening.
    /// </summary>
    /// <param name="namespaceName">The namespace name.</param>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The namespace declaration opening.</returns>
    public static string BeginNamespace(string namespaceName, CodeStyle? style = null)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(namespaceName);
        style ??= _defaultStyle;


        var sb = new StringBuilder();
        _ = sb.Append($"namespace {namespaceName}");


        if (style.BraceStyle == BraceStyle.NextLine)
        {
            _ = sb.AppendLine();
            _ = sb.AppendLine("{");
        }
        else
        {
            _ = sb.AppendLine(" {");
        }


        return sb.ToString();
    }

    /// <summary>
    /// Closes a namespace declaration.
    /// </summary>
    /// <param name="style">Optional code style to use.</param>
    /// <returns>The namespace closing brace.</returns>
    public static string EndNamespace(CodeStyle? style = null)
    {
        _ = style ?? _defaultStyle;
        return "}";
    }

    /// <summary>
    /// Indents code by the specified level (alias for FormatBlock for compatibility).
    /// </summary>
    /// <param name="code">The code to indent.</param>
    /// <param name="level">The indentation level.</param>
    /// <returns>The indented code.</returns>
    public static string Indent(string code, int level) => FormatBlock(code, level);
}
