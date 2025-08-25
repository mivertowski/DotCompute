// <copyright file="PluginNotFoundException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using global::System.Runtime.Serialization;
using DotCompute.Plugins.Exceptions.Core;

namespace DotCompute.Plugins.Exceptions.Loading;

/// <summary>
/// Exception thrown when a requested plugin cannot be found in the plugin registry or repository.
/// This typically occurs when attempting to load or access a plugin that has not been registered or installed.
/// </summary>
[Serializable]
public class PluginNotFoundException : PluginException
{
    /// <summary>
    /// Gets the name of the plugin that could not be found.
    /// </summary>
    public string? PluginName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNotFoundException"/> class.
    /// </summary>
    public PluginNotFoundException() 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginNotFoundException(string message) : base(message) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNotFoundException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginNotFoundException(string message, Exception innerException)
        : base(message, innerException) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNotFoundException"/> class with a specified error message
    /// and plugin identifier.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    public PluginNotFoundException(string message, string pluginId)
        : base(message, pluginId) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNotFoundException"/> class with a specified error message,
    /// plugin identifier, and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginNotFoundException(string message, string pluginId, Exception innerException)
        : base(message, pluginId, innerException) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNotFoundException"/> class with a specified error message,
    /// plugin identifier, and plugin name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="pluginName">The name of the plugin that could not be found.</param>
    public PluginNotFoundException(string message, string pluginId, string pluginName)
        : base(message, pluginId)
    {
        PluginName = pluginName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNotFoundException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        PluginName = info.GetString(nameof(PluginName));
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
        info.AddValue(nameof(PluginName), PluginName);
    }
}