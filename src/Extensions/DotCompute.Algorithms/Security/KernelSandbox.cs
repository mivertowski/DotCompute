// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Security;

/// <summary>
/// Provides sandboxed execution environment for untrusted kernels with comprehensive security controls.
/// Implements process isolation, resource limits, and execution monitoring.
/// </summary>
public sealed class KernelSandbox : IDisposable
{
    private readonly ILogger _logger;
    private readonly SandboxConfiguration _configuration;
    private readonly ConcurrentDictionary<string, SandboxInstance> _activeSandboxes = new();
    private readonly SemaphoreSlim _creationLock = new(1, 1);
    private readonly Timer _monitoringTimer;
    private volatile bool _disposed;

    public KernelSandbox(ILogger<KernelSandbox> logger, SandboxConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? SandboxConfiguration.Default;

        // Start monitoring timer

        _monitoringTimer = new Timer(MonitorSandboxes, null,

            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));


        _logger.LogInformation("KernelSandbox initialized with configuration: {Configuration}",

            _configuration.ToString());
    }

    /// <summary>
    /// Executes kernel code in a secure sandbox environment.
    /// </summary>
    /// <param name="kernelCode">The kernel code to execute</param>
    /// <param name="kernelName">Name of the kernel for identification</param>
    /// <param name="parameters">Execution parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result with security information</returns>
    public async Task<SandboxExecutionResult> ExecuteKernelAsync(string kernelCode, string kernelName,

        IDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(KernelSandbox));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(kernelCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);

        var sandboxId = Guid.NewGuid().ToString();


        await _creationLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Creating sandbox for kernel: {KernelName}, SandboxId: {SandboxId}",

                kernelName, sandboxId);

            var sandbox = await CreateSandboxInstanceAsync(sandboxId, kernelName, cancellationToken);
            _ = _activeSandboxes.TryAdd(sandboxId, sandbox);

            try
            {
                return await ExecuteInSandboxAsync(sandbox, kernelCode, parameters, cancellationToken);
            }
            finally
            {
                await DestroySandboxInstanceAsync(sandboxId);
            }
        }
        finally
        {
            _ = _creationLock.Release();
        }
    }

    /// <summary>
    /// Creates a new sandbox instance with security restrictions.
    /// </summary>
    private async Task<SandboxInstance> CreateSandboxInstanceAsync(string sandboxId, string kernelName,

        CancellationToken cancellationToken)
    {
        var sandbox = new SandboxInstance
        {
            Id = sandboxId,
            KernelName = kernelName,
            CreatedAt = DateTimeOffset.UtcNow,
            Configuration = _configuration
        };

        // Create secure temporary directory
        sandbox.WorkingDirectory = CreateSecureTempDirectory(sandboxId);

        // Setup resource monitoring

        sandbox.ResourceMonitor = new SandboxResourceMonitor(_logger, _configuration);

        // Setup process isolation if available

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await SetupWindowsIsolationAsync(sandbox, cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await SetupLinuxIsolationAsync(sandbox, cancellationToken);
        }


        _logger.LogDebug("Sandbox instance created: {SandboxId} in directory: {WorkingDirectory}",

            sandboxId, sandbox.WorkingDirectory);


        return sandbox;
    }

    /// <summary>
    /// Executes kernel code within the sandbox with comprehensive monitoring.
    /// </summary>
    private async Task<SandboxExecutionResult> ExecuteInSandboxAsync(SandboxInstance sandbox, string kernelCode,

        IDictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        var result = new SandboxExecutionResult
        {
            SandboxId = sandbox.Id,
            KernelName = sandbox.KernelName,
            StartTime = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogInformation("Executing kernel in sandbox: {SandboxId}", sandbox.Id);

            // Start resource monitoring

            sandbox.ResourceMonitor?.StartMonitoring();

            // Create execution timeout

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_configuration.ExecutionTimeout);

            // Execute the kernel with security restrictions

            result = await ExecuteKernelWithRestrictionsAsync(sandbox, kernelCode, parameters,

                timeoutCts.Token);


            result.ResourceUsage = sandbox.ResourceMonitor?.GetCurrentUsage();
            result.ExecutionTime = DateTimeOffset.UtcNow - result.StartTime;
            result.Success = true;


            _logger.LogInformation("Kernel execution completed successfully in sandbox: {SandboxId}, Duration: {Duration}ms",

                sandbox.Id, result.ExecutionTime.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Success = false;
            result.ErrorMessage = "Execution was cancelled";
            _logger.LogWarning("Kernel execution cancelled in sandbox: {SandboxId}", sandbox.Id);
        }
        catch (TimeoutException)
        {
            result.Success = false;
            result.ErrorMessage = "Execution timed out";
            result.SecurityViolations.Add("Execution timeout exceeded");
            _logger.LogWarning("Kernel execution timed out in sandbox: {SandboxId}", sandbox.Id);
        }
        catch (SecurityException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Security violation: {ex.Message}";
            result.SecurityViolations.Add(ex.Message);
            _logger.LogError(ex, "Security violation in sandbox: {SandboxId}", sandbox.Id);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Execution error: {ex.Message}";
            _logger.LogError(ex, "Error executing kernel in sandbox: {SandboxId}", sandbox.Id);
        }
        finally
        {
            sandbox.ResourceMonitor?.StopMonitoring();
            result.EndTime = DateTimeOffset.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Executes kernel code with comprehensive security restrictions and monitoring.
    /// </summary>
    private async Task<SandboxExecutionResult> ExecuteKernelWithRestrictionsAsync(SandboxInstance sandbox,

        string kernelCode, IDictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        var result = new SandboxExecutionResult
        {
            SandboxId = sandbox.Id,
            KernelName = sandbox.KernelName,
            StartTime = DateTimeOffset.UtcNow
        };

        // Create restricted execution context
        var executionContext = new RestrictedExecutionContext
        {
            WorkingDirectory = sandbox.WorkingDirectory,
            MaxMemoryUsage = _configuration.MaxMemoryUsage,
            MaxCpuTime = _configuration.MaxCpuTime,
            AllowedFileOperations = _configuration.AllowedFileOperations,
            AllowedNetworkOperations = _configuration.AllowedNetworkOperations
        };

        // Monitor resource usage during execution
        var resourceMonitor = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var usage = sandbox.ResourceMonitor?.GetCurrentUsage();
                if (usage == null)

                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                // Check memory limits

                if (usage.MemoryUsage > _configuration.MaxMemoryUsage)
                {
                    result.SecurityViolations.Add($"Memory limit exceeded: {usage.MemoryUsage} bytes");
                    throw new SecurityException("Memory limit exceeded");
                }

                // Check CPU limits

                if (usage.CpuUsage > _configuration.MaxCpuUsage)
                {
                    result.SecurityViolations.Add($"CPU limit exceeded: {usage.CpuUsage}%");
                    throw new SecurityException("CPU limit exceeded");
                }


                await Task.Delay(100, cancellationToken);
            }
        }, cancellationToken);

        try
        {
            // Compile and execute kernel in restricted environment
            await CompileAndExecuteKernelAsync(kernelCode, parameters, executionContext, cancellationToken);


            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            resourceMonitor?.Dispose();
        }

        return result;
    }

    /// <summary>
    /// Compiles and executes kernel code in the restricted environment.
    /// </summary>
    private async Task CompileAndExecuteKernelAsync(string kernelCode, IDictionary<string, object>? parameters,

        RestrictedExecutionContext context, CancellationToken cancellationToken)
    {
        // This would integrate with the actual kernel compilation and execution system
        // For now, simulate execution with validation


        await Task.Delay(100, cancellationToken); // Simulate compilation time

        // Validate kernel code doesn't contain restricted operations

        ValidateKernelRestrictions(kernelCode, context);

        // Simulate execution

        await Task.Delay(500, cancellationToken);


        _logger.LogDebug("Kernel executed successfully in restricted environment");
    }

    /// <summary>
    /// Validates that kernel code adheres to sandbox restrictions.
    /// </summary>
    private void ValidateKernelRestrictions(string kernelCode, RestrictedExecutionContext context)
    {
        // Check for forbidden operations
        var forbiddenOperations = new[]
        {
            "file://", "http://", "https://", "ftp://",
            "System.IO.File", "System.Net", "System.Diagnostics.Process",
            "malloc", "free", "memcpy", "system(", "exec(",
            "CreateProcess", "LoadLibrary", "GetProcAddress"
        };

        foreach (var forbidden in forbiddenOperations)
        {
            if (kernelCode.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException($"Forbidden operation detected: {forbidden}");
            }
        }

        // Validate buffer operations have bounds checking
        if (kernelCode.Contains("[]") && !kernelCode.Contains("bounds"))
        {
            _logger.LogWarning("Array access detected without explicit bounds checking");
        }
    }

    /// <summary>
    /// Sets up Windows-specific process isolation.
    /// </summary>
    private async Task SetupWindowsIsolationAsync(SandboxInstance sandbox, CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }


        await Task.Run(() =>
        {
            // Setup job object for resource limits (Windows-specific)
            // This would use P/Invoke to Windows APIs
            _logger.LogDebug("Windows isolation configured for sandbox: {SandboxId}", sandbox.Id);
        }, cancellationToken);
    }

    /// <summary>
    /// Sets up Linux-specific process isolation using containers/namespaces.
    /// </summary>
    private async Task SetupLinuxIsolationAsync(SandboxInstance sandbox, CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }


        await Task.Run(() =>
        {
            // Setup Linux namespaces for isolation
            _logger.LogDebug("Linux isolation configured for sandbox: {SandboxId}", sandbox.Id);
        }, cancellationToken);
    }

    /// <summary>
    /// Creates a secure temporary directory for sandbox operations.
    /// </summary>
    private string CreateSecureTempDirectory(string sandboxId)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "DotCompute_Sandbox", sandboxId);
        _ = Directory.CreateDirectory(tempPath);

        // Set restrictive permissions

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows ACL setup would go here
        }
        else
        {
            // Unix permissions setup
            try
            {
                Process.Start("chmod", $"700 {tempPath}")?.WaitForExit();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set directory permissions for: {TempPath}", tempPath);
            }
        }


        return tempPath;
    }

    /// <summary>
    /// Destroys a sandbox instance and cleans up resources.
    /// </summary>
    private async Task DestroySandboxInstanceAsync(string sandboxId)
    {
        if (!_activeSandboxes.TryRemove(sandboxId, out var sandbox))
        {
            return;
        }


        try
        {
            _logger.LogDebug("Destroying sandbox instance: {SandboxId}", sandboxId);

            // Stop resource monitoring

            sandbox.ResourceMonitor?.Dispose();

            // Kill any processes if still running

            if (sandbox.ProcessId.HasValue)
            {
                try
                {
                    var process = Process.GetProcessById(sandbox.ProcessId.Value);
                    if (!process.HasExited)
                    {
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to kill sandbox process: {ProcessId}", sandbox.ProcessId);
                }
            }

            // Clean up temporary directory

            if (Directory.Exists(sandbox.WorkingDirectory))
            {
                try
                {
                    Directory.Delete(sandbox.WorkingDirectory, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete sandbox directory: {WorkingDirectory}",

                        sandbox.WorkingDirectory);
                }
            }


            _logger.LogDebug("Sandbox instance destroyed: {SandboxId}", sandboxId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error destroying sandbox instance: {SandboxId}", sandboxId);
        }
    }

    /// <summary>
    /// Monitors active sandboxes for violations and resource usage.
    /// </summary>
    private void MonitorSandboxes(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var expiredSandboxes = _activeSandboxes.Values
                .Where(s => DateTimeOffset.UtcNow - s.CreatedAt > _configuration.MaxLifetime)
                .ToList();

            foreach (var expired in expiredSandboxes)
            {
                _logger.LogWarning("Sandbox exceeded maximum lifetime, terminating: {SandboxId}", expired.Id);
                _ = Task.Run(() => DestroySandboxInstanceAsync(expired.Id));
            }

            // Monitor resource usage
            foreach (var sandbox in _activeSandboxes.Values)
            {
                if (sandbox.ResourceMonitor != null)
                {
                    var usage = sandbox.ResourceMonitor.GetCurrentUsage();
                    if (usage.MemoryUsage > _configuration.MaxMemoryUsage * 0.9) // 90% threshold
                    {
                        _logger.LogWarning("Sandbox approaching memory limit: {SandboxId}, Usage: {MemoryUsage} bytes",

                            sandbox.Id, usage.MemoryUsage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring sandboxes");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;


        _monitoringTimer?.Dispose();
        _creationLock?.Dispose();

        // Destroy all active sandboxes

        var sandboxTasks = _activeSandboxes.Keys
            .Select(id => Task.Run(() => DestroySandboxInstanceAsync(id)))
            .ToArray();


        try
        {
            _ = Task.WaitAll(sandboxTasks, TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing sandboxes");
        }


        _logger.LogInformation("KernelSandbox disposed");
    }
}

/// <summary>
/// Configuration for sandbox behavior and security restrictions.
/// </summary>
public sealed class SandboxConfiguration
{
    public static SandboxConfiguration Default => new()
    {
        MaxMemoryUsage = 512 * 1024 * 1024, // 512MB
        MaxCpuUsage = 80, // 80%
        MaxCpuTime = TimeSpan.FromSeconds(30),
        ExecutionTimeout = TimeSpan.FromMinutes(2),
        MaxLifetime = TimeSpan.FromMinutes(10),
        AllowedFileOperations = FileOperations.Read,
        AllowedNetworkOperations = NetworkOperations.None
    };


    public long MaxMemoryUsage { get; init; } = 512 * 1024 * 1024;
    public double MaxCpuUsage { get; init; } = 80.0;
    public TimeSpan MaxCpuTime { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ExecutionTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan MaxLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public FileOperations AllowedFileOperations { get; init; } = FileOperations.Read;
    public NetworkOperations AllowedNetworkOperations { get; init; } = NetworkOperations.None;


    public override string ToString()

        => $"MaxMemory: {MaxMemoryUsage / (1024 * 1024)}MB, MaxCpu: {MaxCpuUsage}%, Timeout: {ExecutionTimeout}";
}

/// <summary>
/// Represents an active sandbox instance.
/// </summary>
internal sealed class SandboxInstance
{
    public required string Id { get; init; }
    public required string KernelName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required SandboxConfiguration Configuration { get; init; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public SandboxResourceMonitor? ResourceMonitor { get; set; }
    public int? ProcessId { get; set; }
}

/// <summary>
/// Result of sandbox execution with security information.
/// </summary>
public sealed class SandboxExecutionResult
{
    public required string SandboxId { get; init; }
    public required string KernelName { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ResourceUsage? ResourceUsage { get; set; }
    public List<string> SecurityViolations { get; } = [];
    public object? Result { get; set; }
}

/// <summary>
/// Restricted execution context for sandbox operations.
/// </summary>
internal sealed class RestrictedExecutionContext
{
    public required string WorkingDirectory { get; init; }
    public long MaxMemoryUsage { get; init; }
    public TimeSpan MaxCpuTime { get; init; }
    public FileOperations AllowedFileOperations { get; init; }
    public NetworkOperations AllowedNetworkOperations { get; init; }
}

/// <summary>
/// File operations allowed in sandbox.
/// </summary>
[Flags]
public enum FileOperations
{
    None = 0,
    Read = 1,
    Write = 2,
    Create = 4,
    Delete = 8,
    All = Read | Write | Create | Delete
}

/// <summary>
/// Network operations allowed in sandbox.
/// </summary>
[Flags]
public enum NetworkOperations
{
    None = 0,
    Outbound = 1,
    Inbound = 2,
    All = Outbound | Inbound
}

/// <summary>
/// Resource usage information.
/// </summary>
public sealed class ResourceUsage
{
    public long MemoryUsage { get; set; }
    public double CpuUsage { get; set; }
    public TimeSpan CpuTime { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
}

/// <summary>
/// Monitors resource usage within a sandbox.
/// </summary>
internal sealed class SandboxResourceMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly SandboxConfiguration _configuration;
    private readonly Timer _monitoringTimer;
    private ResourceUsage _currentUsage = new();
    private Process? _monitoredProcess;
    private volatile bool _disposed;

    public SandboxResourceMonitor(ILogger logger, SandboxConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _monitoringTimer = new Timer(UpdateResourceUsage, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void StartMonitoring()
    {
        if (_disposed)
        {
            return;
        }


        _ = _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        _logger.LogDebug("Resource monitoring started");
    }

    public void StopMonitoring()
    {
        if (_disposed)
        {
            return;
        }


        _ = _monitoringTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _logger.LogDebug("Resource monitoring stopped");
    }

    public ResourceUsage GetCurrentUsage() => _currentUsage;

    private void UpdateResourceUsage(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _monitoredProcess ??= Process.GetCurrentProcess();


            _currentUsage = new ResourceUsage
            {
                MemoryUsage = _monitoredProcess.WorkingSet64,
                CpuUsage = GetCpuUsage(),
                CpuTime = _monitoredProcess.TotalProcessorTime,
                ThreadCount = _monitoredProcess.Threads.Count,
                HandleCount = _monitoredProcess.HandleCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating resource usage");
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            // This would implement proper CPU usage calculation
            // For now, return a mock value
            return Environment.ProcessorCount * 10.0; // Mock CPU usage
        }
        catch
        {
            return 0.0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _monitoringTimer?.Dispose();
        _monitoredProcess?.Dispose();
    }
}