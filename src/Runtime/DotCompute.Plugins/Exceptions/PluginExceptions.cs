// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.Serialization;

namespace DotCompute.Plugins.Exceptions;

/// <summary>
/// Base exception for plugin-related errors.
/// </summary>
[Serializable]
public class PluginException : Exception
{
    public string? PluginId { get; }

    public PluginException() { }

    public PluginException(string message) : base(message) { }

    public PluginException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginException(string message, string pluginId) : base(message)
    {
        PluginId = pluginId;
    }

    public PluginException(string message, string pluginId, Exception innerException)
        : base(message, innerException)
    {
        PluginId = pluginId;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        PluginId = info.GetString(nameof(PluginId));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(PluginId), PluginId);
    }
}

/// <summary>
/// Exception thrown when a plugin cannot be loaded.
/// </summary>
[Serializable]
public class PluginLoadException : PluginException
{
    public string? FilePath { get; }

    public PluginLoadException() { }

    public PluginLoadException(string message) : base(message) { }

    public PluginLoadException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginLoadException(string message, string pluginId, string filePath)
        : base(message, pluginId)
    {
        FilePath = filePath;
    }

    public PluginLoadException(string message, string pluginId, string filePath, Exception innerException)
        : base(message, pluginId, innerException)
    {
        FilePath = filePath;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginLoadException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        FilePath = info.GetString(nameof(FilePath));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(FilePath), FilePath);
    }
}

/// <summary>
/// Exception thrown when a plugin initialization fails.
/// </summary>
[Serializable]
public class PluginInitializationException : PluginException
{
    public PluginInitializationException() { }

    public PluginInitializationException(string message) : base(message) { }

    public PluginInitializationException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginInitializationException(string message, string pluginId)
        : base(message, pluginId) { }

    public PluginInitializationException(string message, string pluginId, Exception innerException)
        : base(message, pluginId, innerException) { }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginInitializationException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}

/// <summary>
/// Exception thrown when a plugin dependency is not satisfied.
/// </summary>
[Serializable]
public class PluginDependencyException : PluginException
{
    public string? DependencyId { get; }
    public string? RequiredVersion { get; }
    public string? ActualVersion { get; }

    public PluginDependencyException() { }

    public PluginDependencyException(string message) : base(message) { }

    public PluginDependencyException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginDependencyException(string message, string pluginId, string dependencyId)
        : base(message, pluginId)
    {
        DependencyId = dependencyId;
    }

    public PluginDependencyException(string message, string pluginId, string dependencyId,
        string requiredVersion, string? actualVersion = null)
        : base(message, pluginId)
    {
        DependencyId = dependencyId;
        RequiredVersion = requiredVersion;
        ActualVersion = actualVersion;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginDependencyException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        DependencyId = info.GetString(nameof(DependencyId));
        RequiredVersion = info.GetString(nameof(RequiredVersion));
        ActualVersion = info.GetString(nameof(ActualVersion));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(DependencyId), DependencyId);
        info.AddValue(nameof(RequiredVersion), RequiredVersion);
        info.AddValue(nameof(ActualVersion), ActualVersion);
    }
}

/// <summary>
/// Exception thrown when a plugin validation fails.
/// </summary>
[Serializable]
public class PluginValidationException : PluginException
{
    public IReadOnlyList<string>? ValidationErrors { get; }

    public PluginValidationException() { }

    public PluginValidationException(string message) : base(message) { }

    public PluginValidationException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginValidationException(string message, string pluginId, IReadOnlyList<string> validationErrors)
        : base(message, pluginId)
    {
        ValidationErrors = validationErrors;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginValidationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ValidationErrors = (string[]?)info.GetValue(nameof(ValidationErrors), typeof(string[]));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ValidationErrors), ValidationErrors);
    }
}

/// <summary>
/// Exception thrown when a plugin operation times out.
/// </summary>
[Serializable]
public class PluginTimeoutException : PluginException
{
    public TimeSpan Timeout { get; }
    public string? Operation { get; }

    public PluginTimeoutException() { }

    public PluginTimeoutException(string message) : base(message) { }

    public PluginTimeoutException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginTimeoutException(string message, string pluginId, TimeSpan timeout, string operation)
        : base(message, pluginId)
    {
        Timeout = timeout;
        Operation = operation;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginTimeoutException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Timeout = (TimeSpan)info.GetValue(nameof(Timeout), typeof(TimeSpan))!;
        Operation = info.GetString(nameof(Operation));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Timeout), Timeout);
        info.AddValue(nameof(Operation), Operation);
    }
}

/// <summary>
/// Exception thrown when a plugin security constraint is violated.
/// </summary>
[Serializable]
public class PluginSecurityException : PluginException
{
    public string? SecurityViolation { get; }

    public PluginSecurityException() { }

    public PluginSecurityException(string message) : base(message) { }

    public PluginSecurityException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginSecurityException(string message, string pluginId, string securityViolation)
        : base(message, pluginId)
    {
        SecurityViolation = securityViolation;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginSecurityException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        SecurityViolation = info.GetString(nameof(SecurityViolation));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(SecurityViolation), SecurityViolation);
    }
}

/// <summary>
/// Exception thrown when a plugin configuration is invalid.
/// </summary>
[Serializable]
public class PluginConfigurationException : PluginException
{
    public string? ConfigurationKey { get; }
    public object? InvalidValue { get; }

    public PluginConfigurationException() { }

    public PluginConfigurationException(string message) : base(message) { }

    public PluginConfigurationException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginConfigurationException(string message, string pluginId, string configurationKey)
        : base(message, pluginId)
    {
        ConfigurationKey = configurationKey;
    }

    public PluginConfigurationException(string message, string pluginId, string configurationKey, object invalidValue)
        : base(message, pluginId)
    {
        ConfigurationKey = configurationKey;
        InvalidValue = invalidValue;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginConfigurationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ConfigurationKey = info.GetString(nameof(ConfigurationKey));
        InvalidValue = info.GetValue(nameof(InvalidValue), typeof(object));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ConfigurationKey), ConfigurationKey);
        info.AddValue(nameof(InvalidValue), InvalidValue);
    }
}

/// <summary>
/// Exception thrown when a plugin cannot be found.
/// </summary>
[Serializable]
public class PluginNotFoundException : PluginException
{
    public string? PluginName { get; }

    public PluginNotFoundException() { }

    public PluginNotFoundException(string message) : base(message) { }

    public PluginNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }

    public PluginNotFoundException(string message, string pluginId)
        : base(message, pluginId) { }

    public PluginNotFoundException(string message, string pluginId, Exception innerException)
        : base(message, pluginId, innerException) { }

    public PluginNotFoundException(string message, string pluginId, string pluginName)
        : base(message, pluginId)
    {
        PluginName = pluginName;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PluginNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        PluginName = info.GetString(nameof(PluginName));
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(PluginName), PluginName);
    }
}
