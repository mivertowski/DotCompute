// <copyright file="PluginSecurityException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.Serialization;
using DotCompute.Plugins.Exceptions.Core;

namespace DotCompute.Plugins.Exceptions.Security;

/// <summary>
/// Exception thrown when a plugin violates security constraints or policies.
/// This includes unauthorized access attempts, permission violations, or attempts to execute restricted operations.
/// </summary>
[Serializable]
public class PluginSecurityException : PluginException
{
    /// <summary>
    /// Gets a description of the security violation that occurred.
    /// </summary>
    public string? SecurityViolation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSecurityException"/> class.
    /// </summary>
    public PluginSecurityException()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSecurityException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginSecurityException(string message) : base(message)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSecurityException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginSecurityException(string message, Exception innerException)
        : base(message, innerException)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSecurityException"/> class with a specified error message,
    /// plugin identifier, and security violation description.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="securityViolation">A description of the security violation that occurred.</param>
    public PluginSecurityException(string message, string pluginId, string securityViolation)
        : base(message, pluginId)
    {
        SecurityViolation = securityViolation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSecurityException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginSecurityException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        SecurityViolation = info.GetString(nameof(SecurityViolation));
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
        info.AddValue(nameof(SecurityViolation), SecurityViolation);
    }
}
