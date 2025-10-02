// <copyright file="PluginDependencyException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.Serialization;
using DotCompute.Plugins.Exceptions.Core;

namespace DotCompute.Plugins.Exceptions.Loading;

/// <summary>
/// Exception thrown when a plugin dependency is not satisfied.
/// This occurs when a plugin requires another plugin or library that is either missing,
/// has an incompatible version, or cannot be resolved.
/// </summary>
[Serializable]
public class PluginDependencyException : PluginException
{
    /// <summary>
    /// Gets the identifier of the dependency that could not be satisfied.
    /// </summary>
    public string? DependencyId { get; }

    /// <summary>
    /// Gets the required version of the dependency.
    /// </summary>
    public string? RequiredVersion { get; }

    /// <summary>
    /// Gets the actual version of the dependency found (if any).
    /// </summary>
    public string? ActualVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDependencyException"/> class.
    /// </summary>
    public PluginDependencyException()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDependencyException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginDependencyException(string message) : base(message)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDependencyException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PluginDependencyException(string message, Exception innerException)
        : base(message, innerException)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDependencyException"/> class with a specified error message,
    /// plugin identifier, and dependency identifier.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="dependencyId">The identifier of the dependency that could not be satisfied.</param>
    public PluginDependencyException(string message, string pluginId, string dependencyId)
        : base(message, pluginId)
    {
        DependencyId = dependencyId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDependencyException"/> class with a specified error message,
    /// plugin identifier, dependency identifier, and version information.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="dependencyId">The identifier of the dependency that could not be satisfied.</param>
    /// <param name="requiredVersion">The required version of the dependency.</param>
    /// <param name="actualVersion">The actual version of the dependency found (if any).</param>
    public PluginDependencyException(string message, string pluginId, string dependencyId,
        string requiredVersion, string? actualVersion = null)
        : base(message, pluginId)
    {
        DependencyId = dependencyId;
        RequiredVersion = requiredVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDependencyException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">The contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginDependencyException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        DependencyId = info.GetString(nameof(DependencyId));
        RequiredVersion = info.GetString(nameof(RequiredVersion));
        ActualVersion = info.GetString(nameof(ActualVersion));
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
        info.AddValue(nameof(DependencyId), DependencyId);
        info.AddValue(nameof(RequiredVersion), RequiredVersion);
        info.AddValue(nameof(ActualVersion), ActualVersion);
    }
}