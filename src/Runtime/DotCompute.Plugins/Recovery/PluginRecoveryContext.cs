// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Context information for plugin recovery operations
/// </summary>
/// <remarks>
/// Creates a new recovery context
/// </remarks>
public sealed class PluginRecoveryContext(string pluginId)
{
    /// <summary>
    /// Gets or sets the plugin identifier
    /// </summary>
    public string PluginId { get; set; } = pluginId;

    /// <summary>
    /// Gets or sets the failure exception that triggered recovery
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the number of recovery attempts made
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when recovery started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the accelerator type associated with the plugin
    /// </summary>
    public string? AcceleratorType { get; set; }

    /// <summary>
    /// Gets or sets the operation type that caused the failure
    /// </summary>
    public string? OperationType { get; set; }

    /// <summary>
    /// Gets or sets the recovery strategy being used
    /// </summary>
    public PluginRecoveryStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for recovery
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <summary>
    /// Gets or sets whether this is a critical recovery operation
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Gets or sets the maximum timeout for recovery
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the elapsed time since recovery started
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;

    /// <summary>
    /// Gets whether the recovery has timed out
    /// </summary>
    public bool HasTimedOut => ElapsedTime > Timeout;

    /// <summary>
    /// Gets or sets the plugin instance being recovered
    /// </summary>
    public object? Plugin { get; set; }

    /// <summary>
    /// Gets or sets the path to the plugin
    /// </summary>
    public string? PluginPath { get; set; }

    /// <summary>
    /// Creates a new recovery context with plugin instance
    /// </summary>
    public PluginRecoveryContext(string pluginId, object plugin) : this(pluginId)
    {
        Plugin = plugin;
    }

    /// <summary>
    /// Creates a recovery context from an exception
    /// </summary>
    public static PluginRecoveryContext FromException(string pluginId, Exception exception)
    {
        return new PluginRecoveryContext(pluginId)
        {
            Exception = exception,
            OperationType = exception.GetType().Name
        };
    }
}
