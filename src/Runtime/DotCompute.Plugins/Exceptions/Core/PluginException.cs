// <copyright file="PluginException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using global::System.Runtime.Serialization;

namespace DotCompute.Plugins.Exceptions.Core;

/// <summary>
/// Base exception for all plugin-related errors.
/// Provides a foundation for specialized plugin exception types.
/// </summary>
[Serializable]
public class PluginException : Exception
{
    /// <summary>
    /// Gets the unique identifier of the plugin that caused the exception.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    public PluginException()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginException(string message) : base(message)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginException(string message, Exception innerException)
        : base(message, innerException)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class with a specified error message
    /// and plugin identifier.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    public PluginException(string message, string pluginId) : base(message)
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class with a specified error message,
    /// plugin identifier, and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginException(string message, string pluginId, Exception innerException)
        : base(message, innerException)
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        PluginId = info.GetString(nameof(PluginId));
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
        info.AddValue(nameof(PluginId), PluginId);
    }
}