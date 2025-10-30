// <copyright file="PluginInitializationException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.Serialization;
using DotCompute.Plugins.Exceptions.Core;

namespace DotCompute.Plugins.Exceptions.Loading;

/// <summary>
/// Exception thrown when a plugin fails to initialize properly.
/// This typically occurs when the plugin's initialization method encounters an error,
/// required resources are unavailable, or the plugin state cannot be properly established.
/// </summary>
[Serializable]
public class PluginInitializationException : PluginException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class.
    /// </summary>
    public PluginInitializationException()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginInitializationException(string message) : base(message)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class with a specified error message
    /// and plugin identifier.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    public PluginInitializationException(string message, string pluginId)
        : base(message, pluginId)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class with a specified error message,
    /// plugin identifier, and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginInitializationException(string message, string pluginId, Exception innerException)
        : base(message, pluginId, innerException)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginInitializationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {

    }
}
