// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Style.Conventions;

/// <summary>
/// Defines naming convention settings for generated code elements.
/// </summary>
/// <remarks>
/// Naming conventions are crucial for code readability, maintainability, and
/// consistency across a codebase. This class provides configuration options
/// for various naming patterns used in C# development, following established
/// .NET conventions while allowing customization for specific project needs.
/// 
/// The conventions defined here affect how identifiers are generated for
/// fields, methods, interfaces, and parameters in the generated code.
/// Consistent naming makes code more predictable and easier to understand.
/// </remarks>
public class NamingConventions
{
    /// <summary>
    /// Gets or sets the prefix used for private field names.
    /// </summary>
    /// <value>
    /// The string prefix to prepend to private field names. Defaults to "_".
    /// </value>
    /// <remarks>
    /// Private field prefixes help distinguish fields from local variables
    /// and parameters, improving code readability. Common conventions include:
    /// - "_" (underscore) - widely used in C# and .NET
    /// - "m_" - Hungarian notation style
    /// - "" (empty) - no prefix, relying on this. qualification
    /// 
    /// Example with default "_":
    /// <code>
    /// private string _userName;
    /// private int _connectionTimeout;
    /// </code>
    /// </remarks>
    public string PrivateFieldPrefix { get; set; } = "_";


    /// <summary>
    /// Gets or sets the suffix used for asynchronous method names.
    /// </summary>
    /// <value>
    /// The string suffix to append to async method names. Defaults to "Async".
    /// </value>
    /// <remarks>
    /// The async method suffix is a .NET convention that helps developers
    /// immediately identify asynchronous methods. This suffix should be
    /// applied to all methods that return Task or Task&lt;T&gt; and perform
    /// asynchronous operations.
    /// 
    /// Example with default "Async":
    /// <code>
    /// public async Task&lt;string&gt; GetUserDataAsync(int userId)
    /// public async Task SaveChangesAsync()
    /// </code>
    /// 
    /// This convention is strongly recommended by Microsoft's C# coding
    /// guidelines and is expected by most .NET developers.
    /// </remarks>
    public string AsyncMethodSuffix { get; set; } = "Async";


    /// <summary>
    /// Gets or sets the prefix used for interface names.
    /// </summary>
    /// <value>
    /// The string prefix to prepend to interface names. Defaults to "I".
    /// </value>
    /// <remarks>
    /// The interface prefix is a long-standing .NET convention that makes
    /// interfaces easily distinguishable from classes and other types.
    /// This convention is deeply ingrained in .NET development culture
    /// and is used throughout the .NET Framework and .NET ecosystem.
    /// 
    /// Example with default "I":
    /// <code>
    /// public interface IUserRepository
    /// public interface ILoggingService
    /// </code>
    /// 
    /// While some modern languages avoid Hungarian notation, the "I" prefix
    /// for interfaces remains standard and expected in C# development.
    /// </remarks>
    public string InterfacePrefix { get; set; } = "I";


    /// <summary>
    /// Gets or sets a value indicating whether to use PascalCase for public members.
    /// </summary>
    /// <value>
    /// <c>true</c> to use PascalCase for public members; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// PascalCase (also known as UpperCamelCase) is the standard .NET convention
    /// for public members including methods, properties, events, and classes.
    /// In PascalCase, the first letter of each word is capitalized.
    /// 
    /// Examples of PascalCase:
    /// <code>
    /// public string UserName { get; set; }
    /// public void ProcessUserData() { }
    /// public event EventHandler DataChanged;
    /// </code>
    /// 
    /// This setting should almost always be true for .NET code generation
    /// to maintain consistency with .NET Framework conventions and developer expectations.
    /// </remarks>
    public bool UsePascalCaseForPublic { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether to use camelCase for parameters and local variables.
    /// </summary>
    /// <value>
    /// <c>true</c> to use camelCase for parameters and local variables; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// CamelCase (also known as lowerCamelCase) is the standard .NET convention
    /// for parameters, local variables, and private fields (when not using prefixes).
    /// In camelCase, the first letter is lowercase, and subsequent words are capitalized.
    /// 
    /// Examples of camelCase:
    /// <code>
    /// public void ProcessUser(string userName, int connectionTimeout)
    /// {
    ///     var processedData = TransformData(userName);
    ///     var isValid = ValidateInput(processedData);
    /// }
    /// </code>
    /// 
    /// This convention helps distinguish between different identifier types
    /// and improves code readability by following established patterns.
    /// </remarks>
    public bool UseCamelCaseForParameters { get; set; } = true;
}