// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Style;

/// <summary>
/// Defines code style preferences for generated code.
/// This class serves as the main configuration container for all code style settings,
/// including indentation, braces, line endings, and naming conventions.
/// </summary>
/// <remarks>
/// The DotComputeCodeStyle class provides a comprehensive set of configuration options
/// to ensure generated code follows consistent formatting and naming standards.
/// These settings are applied during the code generation process to produce
/// clean, readable, and maintainable code output.
/// </remarks>
public class DotComputeCodeStyle
{
    /// <summary>
    /// Gets or sets the indentation style for generated code.
    /// </summary>
    /// <value>
    /// The indentation style to use. Defaults to <see cref="IndentationStyle.Spaces"/>.
    /// </value>
    /// <remarks>
    /// This setting determines whether spaces or tabs are used for indentation
    /// in the generated code. The choice affects code readability and consistency
    /// across different editors and environments.
    /// </remarks>
    public IndentationStyle IndentationStyle { get; set; } = IndentationStyle.Spaces;


    /// <summary>
    /// Gets or sets the number of spaces or tabs per indentation level.
    /// </summary>
    /// <value>
    /// The number of indentation units per level. Defaults to 4.
    /// </value>
    /// <remarks>
    /// When using spaces, this represents the number of space characters.
    /// When using tabs, this typically represents the visual width equivalent
    /// though actual tab rendering depends on editor settings.
    /// Common values are 2, 4, or 8.
    /// </remarks>
    public int IndentSize { get; set; } = 4;


    /// <summary>
    /// Gets or sets the brace placement style for generated code.
    /// </summary>
    /// <value>
    /// The brace style to use. Defaults to <see cref="BraceStyle.NextLine"/>.
    /// </value>
    /// <remarks>
    /// This setting controls where opening braces are placed relative to
    /// the associated statement. NextLine follows C# conventions, while
    /// SameLine follows JavaScript/Java conventions.
    /// </remarks>
    public BraceStyle BraceStyle { get; set; } = BraceStyle.NextLine;


    /// <summary>
    /// Gets or sets the line ending style for generated files.
    /// </summary>
    /// <value>
    /// The line ending style to use. Defaults to <see cref="LineEndingStyle.Auto"/>.
    /// </value>
    /// <remarks>
    /// Auto detection uses the platform default (CRLF on Windows, LF on Unix).
    /// Explicit settings ensure consistent line endings across all environments,
    /// which is important for version control and cross-platform development.
    /// </remarks>
    public LineEndingStyle LineEndings { get; set; } = LineEndingStyle.Auto;


    /// <summary>
    /// Gets or sets the maximum line length before automatic wrapping is applied.
    /// </summary>
    /// <value>
    /// The maximum number of characters per line. Defaults to 120.
    /// </value>
    /// <remarks>
    /// Lines exceeding this length will be automatically wrapped where possible
    /// to maintain readability. Common values are 80, 100, 120, or 140 characters.
    /// The choice depends on team preferences and display constraints.
    /// </remarks>
    public int MaxLineLength { get; set; } = 120;


    /// <summary>
    /// Gets or sets a value indicating whether to use the 'var' keyword for type inference.
    /// </summary>
    /// <value>
    /// <c>true</c> to use 'var' where type can be inferred; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, the generator will use 'var' for local variable declarations
    /// where the type is obvious from the right-hand side of the assignment.
    /// This can improve readability by reducing redundant type information.
    /// </remarks>
    public bool UseVarKeyword { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether to use expression-bodied members for simple methods.
    /// </summary>
    /// <value>
    /// <c>true</c> to use expression body syntax where appropriate; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// Expression-bodied members (=>) provide a concise syntax for simple methods,
    /// properties, and other members that contain only a single expression.
    /// This can improve code density and readability for straightforward implementations.
    /// </remarks>
    public bool UseExpressionBody { get; set; } = true;


    /// <summary>
    /// Gets or sets the naming convention settings for generated code elements.
    /// </summary>
    /// <value>
    /// The naming conventions configuration. Never null.
    /// </value>
    /// <remarks>
    /// This property provides access to detailed naming rules for different
    /// code elements such as fields, methods, interfaces, and parameters.
    /// Consistent naming conventions improve code readability and maintainability.
    /// </remarks>
    public NamingConventions Naming { get; set; } = new();


    /// <summary>
    /// Gets or sets comment style preferences for generated code.
    /// </summary>
    /// <value>
    /// The comment style configuration. Never null.
    /// </value>
    /// <remarks>
    /// Controls the generation of various types of comments including method
    /// documentation, inline comments, and TODO markers. Proper commenting
    /// improves code maintainability and developer understanding.
    /// </remarks>
    public CommentStyle Comments { get; set; } = new();
}