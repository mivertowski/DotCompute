// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Style.Enums;

/// <summary>
/// Specifies the level of detail for generated comments in code output.
/// </summary>
/// <remarks>
/// The comment detail level controls how much explanatory information is
/// included in generated code comments. This affects code readability,
/// maintainability, and file size. The appropriate level depends on the
/// target audience and the complexity of the generated code.
/// </remarks>
public enum CommentDetailLevel
{
    /// <summary>
    /// Generate minimal comments with only essential information.
    /// </summary>
    /// <remarks>
    /// Minimal commenting includes only the most critical information needed
    /// to understand the code's purpose. This typically includes:
    /// - Brief method summaries for public APIs
    /// - Critical warnings or important constraints
    /// - Essential parameter descriptions for complex methods
    /// 
    /// This level produces the most compact code output while maintaining
    /// basic documentation for maintainability. Best suited for performance-
    /// critical scenarios or when code size is a concern.
    /// </remarks>
    Minimal,

    /// <summary>
    /// Generate a normal level of comments with standard documentation.
    /// </summary>
    /// <remarks>
    /// Normal commenting provides comprehensive documentation without being
    /// overly verbose. This typically includes:
    /// - Complete XML documentation for public methods and properties
    /// - Parameter and return value descriptions
    /// - Brief inline comments for complex logic
    /// - Exception documentation where relevant
    /// 
    /// This level balances code readability with documentation completeness
    /// and is suitable for most generated code scenarios. It provides good
    /// IntelliSense support and maintains professional code standards.
    /// </remarks>
    Normal,


    /// <summary>
    /// Generate verbose comments with detailed explanations and examples.
    /// </summary>
    /// <remarks>
    /// Verbose commenting includes comprehensive documentation with detailed
    /// explanations and examples. This typically includes:
    /// - Complete XML documentation with detailed descriptions
    /// - Usage examples and code samples in remarks sections
    /// - Detailed parameter validation and behavior explanations
    /// - Performance considerations and implementation notes
    /// - Cross-references to related methods and types
    /// 
    /// This level produces the most documentation-rich code output and is
    /// ideal for educational purposes, complex APIs, or when the generated
    /// code will be extensively used by other developers who need detailed
    /// guidance on proper usage.
    /// </remarks>
    Verbose
}