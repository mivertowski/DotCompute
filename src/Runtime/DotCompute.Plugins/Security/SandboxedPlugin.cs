// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Security;

/// <summary>
/// Represents a plugin running in a secure sandbox with restricted permissions.
/// </summary>
public class SandboxedPlugin : IDisposable
{
    private readonly ResourceMonitor _resourceMonitor;
    private bool _disposed;
    private bool _isolated;

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxedPlugin"/> class.
    /// </summary>
    internal SandboxedPlugin(
        Guid id,
        object instance,
        IsolatedPluginLoadContext loadContext,
        SandboxPermissions permissions,
        SecurityContext securityContext,
        ResourceMonitor resourceMonitor)
    {
        Id = id;
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        LoadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        SecurityContext = securityContext ?? throw new ArgumentNullException(nameof(securityContext));
        _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the unique identifier for this sandboxed plugin.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public object Instance { get; }

    /// <summary>
    /// Gets the isolated load context for this plugin.
    /// </summary>
    public IsolatedPluginLoadContext LoadContext { get; }

    /// <summary>
    /// Gets the permissions granted to this plugin.
    /// </summary>
    public SandboxPermissions Permissions { get; }

    /// <summary>
    /// Gets the security context for this plugin.
    /// </summary>
    public SecurityContext SecurityContext { get; }

    /// <summary>
    /// Gets when this plugin was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets whether this plugin has been isolated due to security violations.
    /// </summary>
    public bool IsIsolated => _isolated;

    /// <summary>
    /// Gets whether this plugin has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets current resource usage statistics for this plugin.
    /// </summary>
    public ResourceUsage GetResourceUsage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _resourceMonitor.GetPluginUsage(Id);
    }

    /// <summary>
    /// Isolates the plugin due to security violation or suspicious behavior.
    /// </summary>
    internal async Task IsolateAsync()
    {
        if (_isolated)
        {
            return;
        }


        _isolated = true;

        // Stop any ongoing operations

        await TerminateOperationsAsync().ConfigureAwait(false);

        // Clear sensitive data

        ClearSensitiveData();
    }

    /// <summary>
    /// Terminates the plugin and cleans up resources.
    /// </summary>
    internal async Task TerminateAsync()
    {
        if (_disposed)
        {
            return;
        }


        await IsolateAsync().ConfigureAwait(false);

        // Unregister from monitoring

        _resourceMonitor.UnregisterPlugin(this);

        // Dispose the plugin instance if it implements IDisposable

        if (Instance is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // Ignore disposal errors during termination
            }
        }

        // Unload the assembly load context
        LoadContext.Unload();
    }

    /// <summary>
    /// Terminates any ongoing operations in the plugin.
    /// </summary>
    private static async Task TerminateOperationsAsync()
        // This would terminate any active threads or tasks within the plugin
        // Implementation would depend on the specific threading model used





        => await Task.CompletedTask.ConfigureAwait(false);

    /// <summary>
    /// Clears any sensitive data that might be held by the plugin.
    /// </summary>
    private static void ClearSensitiveData()
    {
        // This would clear any cached sensitive data
        // Implementation would depend on the plugin's data structures
    }

    /// <summary>
    /// Disposes the sandboxed plugin and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        TerminateAsync().GetAwaiter().GetResult();
    }
}

/// <summary>
/// Configuration for sandbox permissions and restrictions.
/// </summary>
public class SandboxPermissions
{
    /// <summary>
    /// Gets or sets the list of allowed permissions.
    /// </summary>
    public IList<string> AllowedPermissions { get; init; } = [];

    /// <summary>
    /// Gets or sets the list of explicitly denied permissions.
    /// </summary>
    public IList<string> DeniedPermissions { get; init; } = [];

    /// <summary>
    /// Gets or sets resource limits for the plugin.
    /// </summary>
    public ResourceLimits ResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets network access permissions.
    /// </summary>
    public NetworkAccessPermissions NetworkAccess { get; set; } = NetworkAccessPermissions.None;

    /// <summary>
    /// Gets or sets file system access permissions.
    /// </summary>
    public FileSystemAccessPermissions FileSystemAccess { get; set; } = FileSystemAccessPermissions.None;

    /// <summary>
    /// Creates a default restrictive sandbox permission set.
    /// </summary>
    public static SandboxPermissions CreateRestrictive()
    {
        return new SandboxPermissions
        {
            AllowedPermissions = ["Execution"],
            NetworkAccess = NetworkAccessPermissions.None,
            FileSystemAccess = FileSystemAccessPermissions.None,
            ResourceLimits = new ResourceLimits
            {
                MaxMemoryMB = 100,
                MaxCpuUsagePercent = 50,
                MaxExecutionTimeSeconds = 30
            }
        };
    }

    /// <summary>
    /// Creates a moderate sandbox permission set with limited I/O.
    /// </summary>
    public static SandboxPermissions CreateModerate()
    {
        return new SandboxPermissions
        {
            AllowedPermissions = ["Execution", "FileIO", "NetworkAccess"],
            NetworkAccess = NetworkAccessPermissions.HttpClient,
            FileSystemAccess = FileSystemAccessPermissions.TempDirectory,
            ResourceLimits = new ResourceLimits
            {
                MaxMemoryMB = 256,
                MaxCpuUsagePercent = 75,
                MaxExecutionTimeSeconds = 120
            }
        };
    }
}
/// <summary>
/// An network access permissions enumeration.
/// </summary>

/// <summary>
/// Network access permission levels.
/// </summary>
public enum NetworkAccessPermissions
{
    None = 0,
    HttpClient = 1,
    FullNetwork = 2
}
/// <summary>
/// An file system access permissions enumeration.
/// </summary>

/// <summary>
/// File system access permission levels.
/// </summary>
public enum FileSystemAccessPermissions
{
    None = 0,
    TempDirectory = 1,
    ReadOnly = 2,
    Limited = 3,
    Full = 4
}

/// <summary>
/// Resource usage limits for sandboxed plugins.
/// </summary>
public class ResourceLimits
{
    /// <summary>
    /// Gets or sets the maximum memory usage in MB.
    /// </summary>
    public int MaxMemoryMB { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum CPU usage percentage.
    /// </summary>
    public int MaxCpuUsagePercent { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum execution time in seconds.
    /// </summary>
    public int MaxExecutionTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of threads.
    /// </summary>
    public int MaxThreads { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum file I/O operations per second.
    /// </summary>
    public int MaxFileIOPerSecond { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum network I/O operations per second.
    /// </summary>
    public int MaxNetworkIOPerSecond { get; set; } = 50;
}

/// <summary>
/// Security context for plugin execution.
/// </summary>
public class SecurityContext
{
    /// <summary>
    /// Gets or sets the allowed permissions.
    /// </summary>
    public HashSet<string> AllowedPermissions { get; init; } = [];

    /// <summary>
    /// Gets or sets the denied permissions.
    /// </summary>
    public HashSet<string> DeniedPermissions { get; init; } = [];

    /// <summary>
    /// Gets or sets the resource limits.
    /// </summary>
    public ResourceLimits ResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets the network access level.
    /// </summary>
    public NetworkAccessPermissions NetworkAccess { get; set; } = NetworkAccessPermissions.None;

    /// <summary>
    /// Gets or sets the file system access level.
    /// </summary>
    public FileSystemAccessPermissions FileSystemAccess { get; set; } = FileSystemAccessPermissions.None;
}

/// <summary>
/// Current resource usage statistics for a plugin.
/// </summary>
public class ResourceUsage
{
    /// <summary>
    /// Gets or sets the current memory usage in MB.
    /// </summary>
    public long MemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets the current CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Gets or sets the number of active threads.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the number of file I/O operations performed.
    /// </summary>
    public long FileIOOperations { get; set; }

    /// <summary>
    /// Gets or sets the number of network I/O operations performed.
    /// </summary>
    public long NetworkIOOperations { get; set; }

    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets whether any resource limits are being exceeded.
    /// </summary>
    public bool IsExceedingLimits { get; set; }

    /// <summary>
    /// Gets the list of violated resource limits.
    /// </summary>
    public IList<string> ViolatedLimits { get; init; } = [];
}