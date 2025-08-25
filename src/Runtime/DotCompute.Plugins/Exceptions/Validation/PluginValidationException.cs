// <copyright file="PluginValidationException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using global::System.Runtime.Serialization;
using DotCompute.Plugins.Exceptions.Core;

namespace DotCompute.Plugins.Exceptions.Validation;

/// <summary>
/// Exception thrown when a plugin fails validation checks.
/// This occurs when a plugin does not meet required criteria such as proper signatures,
/// required interfaces, or structural requirements.
/// </summary>
[Serializable]
public class PluginValidationException : PluginException
{
    /// <summary>
    /// Gets the list of validation errors encountered during plugin validation.
    /// </summary>
    public IReadOnlyList<string>? ValidationErrors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class.
    /// </summary>
    public PluginValidationException() 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginValidationException(string message) : base(message) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginValidationException(string message, Exception innerException)
        : base(message, innerException) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class with a specified error message,
    /// plugin identifier, and list of validation errors.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="validationErrors">The list of validation errors encountered.</param>
    public PluginValidationException(string message, string pluginId, IReadOnlyList<string> validationErrors)
        : base(message, pluginId)
    {
        ValidationErrors = validationErrors;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginValidationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ValidationErrors = (string[]?)info.GetValue(nameof(ValidationErrors), typeof(string[]));
    }

    /// <summary>
    /// Sets the <see cref="SerializationInfo"/> with information about the exception.
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ValidationErrors), ValidationErrors);
    }
}