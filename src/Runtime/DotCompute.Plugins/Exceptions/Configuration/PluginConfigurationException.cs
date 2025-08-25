// <copyright file="PluginConfigurationException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using global::System.Runtime.Serialization;
using DotCompute.Plugins.Exceptions.Core;

namespace DotCompute.Plugins.Exceptions.Configuration;

/// <summary>
/// Exception thrown when a plugin configuration is invalid or cannot be processed.
/// This includes missing required settings, invalid values, or incompatible configuration combinations.
/// </summary>
[Serializable]
public class PluginConfigurationException : PluginException
{
    /// <summary>
    /// Gets the configuration key that caused the exception.
    /// </summary>
    public string? ConfigurationKey { get; }

    /// <summary>
    /// Gets the invalid value that was provided for the configuration.
    /// </summary>
    public object? InvalidValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationException"/> class.
    /// </summary>
    public PluginConfigurationException() 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginConfigurationException(string message) : base(message) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginConfigurationException(string message, Exception innerException)
        : base(message, innerException) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationException"/> class with a specified error message,
    /// plugin identifier, and configuration key.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="configurationKey">The configuration key that caused the exception.</param>
    public PluginConfigurationException(string message, string pluginId, string configurationKey)
        : base(message, pluginId)
    {
        ConfigurationKey = configurationKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationException"/> class with a specified error message,
    /// plugin identifier, configuration key, and invalid value.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="configurationKey">The configuration key that caused the exception.</param>
    /// <param name="invalidValue">The invalid value that was provided for the configuration.</param>
    public PluginConfigurationException(string message, string pluginId, string configurationKey, object invalidValue)
        : base(message, pluginId)
    {
        ConfigurationKey = configurationKey;
        InvalidValue = invalidValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginConfigurationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ConfigurationKey = info.GetString(nameof(ConfigurationKey));
        InvalidValue = info.GetValue(nameof(InvalidValue), typeof(object));
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
        info.AddValue(nameof(ConfigurationKey), ConfigurationKey);
        info.AddValue(nameof(InvalidValue), InvalidValue);
    }
}