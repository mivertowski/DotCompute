// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Configuration.Style.Enums;

namespace DotCompute.Generators.Configuration.Style;

/// <summary>
/// Defines comment style preferences for generated code documentation.
/// </summary>
/// <remarks>
/// Comment style configuration controls the generation of various types of
/// documentation and explanatory comments in generated code. Proper commenting
/// improves code maintainability, developer understanding, and provides better
/// IntelliSense support in development environments.
/// 
/// The settings in this class allow fine-grained control over comment generation,
/// balancing code readability with file size and generation performance.
/// Different projects may require different levels of commenting based on
/// their audience, complexity, and maintenance requirements.
/// </remarks>
public class CommentStyle
{
    /// <summary>
    /// Gets or sets a value indicating whether to include XML documentation comments for methods.
    /// </summary>
    /// <value>
    /// <c>true</c> to generate method documentation comments; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// Method comments provide essential documentation for generated methods,
    /// including summaries, parameter descriptions, return values, and exceptions.
    /// These comments enable IntelliSense support and improve code discoverability.
    /// 
    /// When enabled, generates comments like:
    /// <code>
    /// /// &lt;summary&gt;
    /// /// Processes the user data and returns validation results.
    /// /// &lt;/summary&gt;
    /// /// &lt;param name="userData"&gt;The user data to process.&lt;/param&gt;
    /// /// &lt;returns&gt;A validation result indicating success or failure.&lt;/returns&gt;
    /// public UnifiedValidationResult ProcessUserData(UserData userData)
    /// </code>
    /// 
    /// Disabling this option can significantly reduce generated code size but
    /// at the cost of reduced documentation and IntelliSense support.
    /// </remarks>
    public bool IncludeMethodComments { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether to include inline explanatory comments.
    /// </summary>
    /// <value>
    /// <c>true</c> to generate inline comments for complex logic; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// Inline comments provide explanations for complex logic, algorithms, or
    /// non-obvious code sections within method implementations. These comments
    /// help developers understand the reasoning behind specific code patterns.
    /// 
    /// When enabled, generates comments like:
    /// <code>
    /// // Calculate the optimal buffer size based on input length
    /// var bufferSize = Math.Min(inputLength * 2, MaxBufferSize);
    /// 
    /// // Apply vectorization if the data size exceeds the threshold
    /// if (dataSize > VectorizationThreshold)
    /// {
    ///     return ProcessWithSimd(data);
    /// }
    /// </code>
    /// 
    /// These comments are particularly valuable for generated code that implements
    /// complex algorithms or optimizations that may not be immediately obvious.
    /// </remarks>
    public bool IncludeInlineComments { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether to include TODO comments for incomplete or future work.
    /// </summary>
    /// <value>
    /// <c>true</c> to generate TODO comments; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// TODO comments mark areas of code that require future attention, such as
    /// optimizations, missing implementations, or known limitations. In generated
    /// code, these might indicate areas where manual intervention is needed.
    /// 
    /// When enabled, generates comments like:
    /// <code>
    /// // TODO: Implement error handling for edge case scenarios
    /// // TODO: Optimize memory allocation for large datasets
    /// // TODO: Add validation for null or empty input parameters
    /// </code>
    /// 
    /// TODO comments can be useful during development and debugging phases
    /// but are generally disabled for production code generation as they
    /// may indicate incomplete implementations. They can be helpful for
    /// development tools and code analysis scenarios.
    /// </remarks>
    public bool IncludeTodoComments { get; set; }


    /// <summary>
    /// Gets or sets the level of detail for generated comments.
    /// </summary>
    /// <value>
    /// The comment detail level. Defaults to <see cref="CommentDetailLevel.Normal"/>.
    /// </value>
    /// <remarks>
    /// The detail level controls the comprehensiveness and verbosity of generated
    /// comments across all comment types. This setting provides a global way to
    /// adjust comment density based on project requirements:
    /// 
    /// - <see cref="CommentDetailLevel.Minimal"/>: Only essential information
    /// - <see cref="CommentDetailLevel.Normal"/>: Standard professional documentation
    /// - <see cref="CommentDetailLevel.Verbose"/>: Comprehensive with examples
    /// 
    /// Higher detail levels improve code understanding and maintainability but
    /// increase file size and generation time. The appropriate level depends on
    /// the target audience and the complexity of the generated code.
    /// </remarks>
    public CommentDetailLevel DetailLevel { get; set; } = CommentDetailLevel.Normal;
}