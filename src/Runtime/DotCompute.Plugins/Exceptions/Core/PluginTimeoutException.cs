// <copyright file="PluginTimeoutException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using global::System.Runtime.Serialization;

namespace DotCompute.Plugins.Exceptions.Core;

/// <summary>
/// Exception thrown when a plugin operation exceeds the allowed timeout period.
/// This ensures that plugin operations do not block the system indefinitely.
/// </summary>
[Serializable]
public class PluginTimeoutException : PluginException
{
    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the name of the operation that timed out.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTimeoutException"/> class.
    /// </summary>
    public PluginTimeoutException() 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTimeoutException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginTimeoutException(string message) : base(message) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTimeoutException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginTimeoutException(string message, Exception innerException)
        : base(message, innerException) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTimeoutException"/> class with a specified error message,
    /// plugin identifier, timeout duration, and operation name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="timeout">The timeout duration that was exceeded.</param>
    /// <param name="operation">The name of the operation that timed out.</param>
    public PluginTimeoutException(string message, string pluginId, TimeSpan timeout, string operation)
        : base(message, pluginId)
    {
        Timeout = timeout;
        Operation = operation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTimeoutException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginTimeoutException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Timeout = (TimeSpan)info.GetValue(nameof(Timeout), typeof(TimeSpan))!;
        Operation = info.GetString(nameof(Operation));
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
        info.AddValue(nameof(Timeout), Timeout);
        info.AddValue(nameof(Operation), Operation);
    }
}