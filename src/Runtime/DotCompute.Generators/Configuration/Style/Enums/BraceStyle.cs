// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Style.Enums;

/// <summary>
/// Specifies the brace placement style options for generated code formatting.
/// </summary>
/// <remarks>
/// Brace placement style affects code readability and follows different conventions
/// across programming languages and development teams. The choice between same-line
/// and next-line placement often depends on language traditions and team preferences.
/// </remarks>
public enum BraceStyle
{
    /// <summary>
    /// Place the opening brace on the same line as the associated statement.
    /// </summary>
    /// <remarks>
    /// This style places the opening brace immediately after the statement,
    /// separated by a space. It's commonly used in JavaScript, Java, and some
    /// C# codebases. This approach can save vertical space and create a more
    /// compact code appearance.
    ///
    /// Example:
    /// <code>
    /// if (condition) {
    ///     // code here
    /// }
    /// </code>
    /// </remarks>
    SameLine,


    /// <summary>
    /// Place the opening brace on the next line, aligned with the statement.
    /// </summary>
    /// <remarks>
    /// This style places the opening brace on its own line, typically aligned
    /// with the beginning of the associated statement. It's the traditional
    /// C# convention and is widely used in .NET development. This approach
    /// can improve readability by clearly delineating code blocks.
    ///
    /// Example:
    /// <code>
    /// if (condition)
    /// {
    ///     // code here
    /// }
    /// </code>
    /// </remarks>
    NextLine
}