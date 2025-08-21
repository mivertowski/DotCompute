// <copyright file="PluginLoadException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.Serialization;
using DotCompute.Plugins.Exceptions.Core;

namespace DotCompute.Plugins.Exceptions.Loading;

/// <summary>
/// Exception thrown when a plugin cannot be loaded from the specified location.
/// This typically occurs when the plugin assembly is missing, corrupted, or incompatible.
/// </summary>
[Serializable]
public class PluginLoadException : PluginException
{
    /// <summary>
    /// Gets the file path from which the plugin was attempted to be loaded.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class.
    /// </summary>
    public PluginLoadException() 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginLoadException(string message) : base(message) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginLoadException(string message, Exception innerException)
        : base(message, innerException) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class with a specified error message,
    /// plugin identifier, and file path.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="filePath">The file path from which the plugin was attempted to be loaded.</param>
    public PluginLoadException(string message, string pluginId, string filePath)
        : base(message, pluginId)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class with a specified error message,
    /// plugin identifier, file path, and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="filePath">The file path from which the plugin was attempted to be loaded.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginLoadException(string message, string pluginId, string filePath, Exception innerException)
        : base(message, pluginId, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginLoadException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        FilePath = info.GetString(nameof(FilePath));
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
        info.AddValue(nameof(FilePath), FilePath);
    }
}