// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using System.Security;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Provides secure sandboxing and isolation for plugin execution with controlled permissions.
/// </summary>
public class PluginSandbox : IDisposable
{
    private readonly ILogger<PluginSandbox> _logger;
    private readonly SandboxConfiguration _configuration;
    private readonly ConcurrentDictionary<Guid, SandboxedPlugin> _sandboxedPlugins = new();
    private readonly SecurityManager _securityManager;
    private readonly ResourceMonitor _resourceMonitor;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSandbox"/> class.
    /// </summary>
    public PluginSandbox(
        ILogger<PluginSandbox> logger,
        SandboxConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? new SandboxConfiguration();
        _securityManager = new SecurityManager(_logger);
        _resourceMonitor = new ResourceMonitor(_logger, _configuration.ResourceLimits);
    }

    /// <summary>
    /// Creates a sandboxed instance of a plugin with restricted permissions.
    /// </summary>
    public async Task<SandboxedPlugin> CreateSandboxedPluginAsync<T>(
        string assemblyPath,
        string typeName,
        SandboxPermissions permissions,
        CancellationToken cancellationToken = default) where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(permissions);

        var pluginId = Guid.NewGuid();
        _logger.LogInfoMessage("Creating sandboxed plugin {PluginId} from {pluginId, assemblyPath}");

        try
        {
            // Create isolated load context with restricted permissions
            var isolatedContext = new IsolatedPluginLoadContext(
                $"SandboxedPlugin_{pluginId}",
                assemblyPath,
                permissions,
                _logger);

            // Load the assembly in the isolated context
            var assembly = await LoadAssemblySecurelyAsync(isolatedContext, assemblyPath, cancellationToken).ConfigureAwait(false);

            // Get the plugin type

            var pluginType = assembly.GetType(typeName);
            if (pluginType == null)
            {
                throw new InvalidOperationException($"Plugin type '{typeName}' not found in assembly");
            }

            // Validate the plugin type implements required interfaces
            if (!typeof(T).IsAssignableFrom(pluginType))
            {
                throw new InvalidOperationException($"Plugin type '{typeName}' does not implement required interface '{typeof(T).Name}'");
            }

            // Create security context
            var securityContext = await CreateSecurityContextAsync(pluginType, permissions, cancellationToken).ConfigureAwait(false);

            // Create the plugin instance with security restrictions
            var pluginInstance = await CreateSecurePluginInstanceAsync(pluginType, securityContext, cancellationToken).ConfigureAwait(false);

            var sandboxedPlugin = new SandboxedPlugin(
                pluginId,
                pluginInstance,
                isolatedContext,
                permissions,
                securityContext,
                _resourceMonitor);

            // Register for monitoring
            _ = _sandboxedPlugins.TryAdd(pluginId, sandboxedPlugin);
            _resourceMonitor.RegisterPlugin(sandboxedPlugin);

            _logger.LogInfoMessage("Successfully created sandboxed plugin {pluginId}");
            return sandboxedPlugin;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create sandboxed plugin {pluginId}");
            throw;
        }
    }

    /// <summary>
    /// Executes a method on a sandboxed plugin with security monitoring.
    /// </summary>
    public async Task<TResult> ExecuteSecurelyAsync<TResult>(
        SandboxedPlugin plugin,
        Func<object, Task<TResult>> operation,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(operation);

        var actualTimeout = timeout ?? _configuration.DefaultExecutionTimeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(actualTimeout);

        _logger.LogDebugMessage($"Executing operation on sandboxed plugin {plugin.Id} with timeout {actualTimeout}");

        try
        {
            // Start resource monitoring
            var monitoringTask = _resourceMonitor.MonitorPluginAsync(plugin, cts.Token);

            // Execute the operation in the security context
            var executionTask = ExecuteInSecurityContextAsync(plugin, operation, cts.Token);

            // Wait for either completion or timeout
            var completedTask = await Task.WhenAny(executionTask, monitoringTask).ConfigureAwait(false);

            if (completedTask == monitoringTask)
            {
                // Resource monitoring detected a violation
                var violation = await monitoringTask.ConfigureAwait(false);
                throw new SecurityException($"Resource violation detected: {violation}");
            }

            return await executionTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarningMessage($"Plugin execution timed out after {actualTimeout} for plugin {plugin.Id}");
            throw new TimeoutException($"Plugin execution timed out after {actualTimeout}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error executing operation on sandboxed plugin {plugin.Id}");

            // Check if this was a security violation

            if (ex is SecurityException || ex.Message.Contains("security", StringComparison.OrdinalIgnoreCase))
            {
                await HandleSecurityViolationAsync(plugin, ex).ConfigureAwait(false);
            }


            throw;
        }
    }

    /// <summary>
    /// Loads an assembly with security validation.
    /// </summary>
    private async Task<Assembly> LoadAssemblySecurelyAsync(
        IsolatedPluginLoadContext context,
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        // Verify assembly integrity
        if (!await _securityManager.ValidateAssemblyIntegrityAsync(assemblyPath, cancellationToken))
        {
            throw new SecurityException("Assembly integrity validation failed");
        }

        // Check assembly metadata for suspicious patterns
        var metadata = await _securityManager.AnalyzeAssemblyMetadataAsync(assemblyPath, cancellationToken);
        if (metadata.HasSuspiciousPatterns)
        {
            var patterns = string.Join(", ", metadata.SuspiciousPatterns);
            throw new SecurityException($"Assembly contains suspicious patterns: {patterns}");
        }

        // Load the assembly
        return context.LoadFromAssemblyPath(assemblyPath);
    }

    /// <summary>
    /// Creates a security context for the plugin.
    /// </summary>
    private static async Task<SecurityContext> CreateSecurityContextAsync(
        Type pluginType,
        SandboxPermissions permissions,
        CancellationToken cancellationToken)
    {
        var context = new SecurityContext
        {
            AllowedPermissions = [.. permissions.AllowedPermissions],
            DeniedPermissions = [.. permissions.DeniedPermissions],
            ResourceLimits = permissions.ResourceLimits,
            NetworkAccess = permissions.NetworkAccess,
            FileSystemAccess = permissions.FileSystemAccess
        };

        // Analyze plugin type for required permissions
        var requiredPermissions = await AnalyzePluginPermissionsAsync(pluginType, cancellationToken);

        // Validate that all required permissions are allowed

        var unauthorizedPermissions = requiredPermissions.Except(context.AllowedPermissions);
        if (unauthorizedPermissions.Any())
        {
            throw new UnauthorizedAccessException(
                $"Plugin requires unauthorized permissions: {string.Join(", ", unauthorizedPermissions)}");
        }

        return context;
    }

    /// <summary>
    /// Creates a plugin instance with security restrictions.
    /// </summary>
    private async Task<object> CreateSecurePluginInstanceAsync(
        Type pluginType,
        SecurityContext securityContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check for parameterless constructor
            var constructor = pluginType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new InvalidOperationException($"Plugin type '{pluginType.Name}' must have a parameterless constructor for sandboxing");
            }

            // Create instance with security monitoring
            var instance = Activator.CreateInstance(pluginType);
            if (instance == null)
            {
                throw new InvalidOperationException($"Failed to create instance of plugin type '{pluginType.Name}'");
            }

            // Apply security wrapper if needed
            return await ApplySecurityWrapperAsync(instance, securityContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create secure plugin instance of type {pluginType.Name}");
            throw;
        }
    }

    /// <summary>
    /// Executes an operation in the plugin's security context.
    /// </summary>
    private static async Task<TResult> ExecuteInSecurityContextAsync<TResult>(
        SandboxedPlugin plugin,
        Func<object, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        // Set up security context
        var originalContext = Thread.CurrentPrincipal;
        var securityPrincipal = CreateSecurityPrincipal(plugin.SecurityContext);


        try
        {
            // Switch to sandboxed security context
            Thread.CurrentPrincipal = securityPrincipal;

            // Execute the operation

            return await operation(plugin.Instance);
        }
        finally
        {
            // Restore original context
            Thread.CurrentPrincipal = originalContext;
        }
    }

    /// <summary>
    /// Analyzes a plugin type to determine required permissions.
    /// </summary>
    private static async Task<HashSet<string>> AnalyzePluginPermissionsAsync(Type pluginType, CancellationToken cancellationToken)
    {
        var requiredPermissions = new HashSet<string>();


        await Task.Run(() =>
        {
            // Analyze methods for required permissions
            var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);


            foreach (var method in methods)
            {
                // Check for file I/O operations
                if (method.Name.Contains("File", StringComparison.OrdinalIgnoreCase) ||
                    method.GetParameters().Any(p => p.ParameterType == typeof(Stream) || p.ParameterType.Name.Contains("Stream")))
                {
                    _ = requiredPermissions.Add("FileIO");
                }

                // Check for network operations
                if (method.Name.Contains("Http", StringComparison.OrdinalIgnoreCase) ||
                    method.Name.Contains("Network", StringComparison.OrdinalIgnoreCase) ||
                    method.GetParameters().Any(p => p.ParameterType.Namespace?.Contains("System.Net") == true))
                {
                    _ = requiredPermissions.Add("NetworkAccess");
                }

                // Check for registry operations
                if (method.Name.Contains("Registry", StringComparison.OrdinalIgnoreCase))
                {
                    _ = requiredPermissions.Add("RegistryAccess");
                }

                // Check for process operations
                if (method.Name.Contains("Process", StringComparison.OrdinalIgnoreCase))
                {
                    _ = requiredPermissions.Add("ProcessControl");
                }
            }

            // Analyze attributes
            var attributes = pluginType.GetCustomAttributes();
            foreach (var attr in attributes)
            {
                if (attr.GetType().Name.Contains("Permission"))
                {
                    _ = requiredPermissions.Add(attr.GetType().Name);
                }
            }
        }, cancellationToken);

        return requiredPermissions;
    }

    /// <summary>
    /// Applies security wrapper to plugin instance if needed.
    /// </summary>
    private static async Task<object> ApplySecurityWrapperAsync(
        object instance,
        SecurityContext securityContext,
        CancellationToken cancellationToken)
    {
        // For now, return the instance as-is
        // In a full implementation, this would wrap the instance with a security proxy
        await Task.CompletedTask;
        return instance;
    }

    /// <summary>
    /// Creates a security principal for the sandbox context.
    /// </summary>
    private static global::System.Security.Principal.IPrincipal CreateSecurityPrincipal(SecurityContext context)
    {
        var identity = new global::System.Security.Principal.GenericIdentity("SandboxedPlugin", "Custom");
        var principal = new global::System.Security.Principal.GenericPrincipal(identity, [.. context.AllowedPermissions]);
        return principal;
    }

    /// <summary>
    /// Handles security violations by isolating and terminating the plugin.
    /// </summary>
    private async Task HandleSecurityViolationAsync(SandboxedPlugin plugin, Exception violation)
    {
        _logger.LogCritical(violation, "Security violation detected for plugin {PluginId}. Terminating plugin.", plugin.Id);

        try
        {
            // Immediately isolate the plugin
            await plugin.IsolateAsync();

            // Remove from active plugins

            _ = _sandboxedPlugins.TryRemove(plugin.Id, out _);

            // Unload the plugin context

            plugin.LoadContext.Unload();

            // Log security incident

            LogSecurityIncident(plugin, violation);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error handling security violation for plugin {plugin.Id}");
        }
    }

    /// <summary>
    /// Logs a security incident for audit trails.
    /// </summary>
    private void LogSecurityIncident(SandboxedPlugin plugin, Exception violation)
    {
        var incident = new
        {
            PluginId = plugin.Id,
            Timestamp = DateTimeOffset.UtcNow,
            ViolationType = violation.GetType().Name,
            ViolationMessage = violation.Message,
            StackTrace = violation.StackTrace,
            Permissions = plugin.Permissions.AllowedPermissions
        };

        _logger.LogCritical("SECURITY INCIDENT: {Incident}", System.Text.Json.JsonSerializer.Serialize(incident));
    }

    /// <summary>
    /// Gets all active sandboxed plugins.
    /// </summary>
    public IReadOnlyList<SandboxedPlugin> GetActiveSandboxedPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sandboxedPlugins.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Terminates a sandboxed plugin.
    /// </summary>
    public async Task TerminatePluginAsync(Guid pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_sandboxedPlugins.TryRemove(pluginId, out var plugin))
        {
            _logger.LogInfoMessage("Terminating sandboxed plugin {pluginId}");


            try
            {
                await plugin.TerminateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error terminating plugin {pluginId}");
            }
        }
    }

    /// <summary>
    /// Disposes the sandbox and all managed resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        // Terminate all active plugins
        foreach (var plugin in _sandboxedPlugins.Values)
        {
            try
            {
                plugin.TerminateAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error disposing sandboxed plugin {plugin.Id}");
            }
        }

        _sandboxedPlugins.Clear();
        _resourceMonitor?.Dispose();
        _securityManager?.Dispose();
    }
}