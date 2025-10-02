// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Simplified kernel execution service that demonstrates the integration pattern.
/// This bridges generated kernel code with the runtime infrastructure.
/// </summary>
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Service layer logging")]
public class KernelExecutionServiceSimplified(
    AcceleratorRuntime runtime,
    ILogger<KernelExecutionServiceSimplified> logger) : Abstractions.Interfaces.IComputeOrchestrator, IDisposable
{
    private readonly AcceleratorRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly ILogger<KernelExecutionServiceSimplified> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<string, KernelRegistrationInfo> _kernelRegistry = [];
    private bool _disposed;

    /// <summary>
    /// Registers kernels from the generated kernel registry.
    /// </summary>
    public void RegisterKernels(IEnumerable<KernelRegistrationInfo> kernelRegistrations)
    {
        foreach (var registration in kernelRegistrations)
        {
            _kernelRegistry[registration.FullName] = registration;
            _logger.LogDebugMessage($"Registered kernel: {registration.FullName} with backends: {string.Join(", ", registration.SupportedBackends)}");
        }

        _logger.LogInfoMessage($"Registered {_kernelRegistry.Count} kernels from generated registry");
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var accelerator = await GetOptimalAcceleratorAsync(kernelName) ?? throw new InvalidOperationException($"No suitable accelerator found for kernel: {kernelName}");
        return await ExecuteAsync<T>(kernelName, accelerator, args);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args)
    {
        var accelerators = _runtime.GetAccelerators()
            .Where(a => a.Info.DeviceType.Equals(preferredBackend, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (accelerators.Count == 0)
        {
            _logger.LogWarningMessage($"Preferred backend {preferredBackend} not available, falling back to optimal selection");
            return await ExecuteAsync<T>(kernelName, args);
        }

        var accelerator = accelerators.FirstOrDefault() ?? throw new InvalidOperationException($"No suitable {preferredBackend} accelerator found");
        return await ExecuteAsync<T>(kernelName, accelerator, args);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_kernelRegistry.TryGetValue(kernelName, out _))
        {
            throw new ArgumentException($"Kernel not found: {kernelName}", nameof(kernelName));
        }

        _logger.LogInfoMessage($"Executing kernel {kernelName} on {accelerator.Info.DeviceType} (placeholder implementation)");

        // This is a placeholder implementation that demonstrates the integration pattern
        // The actual implementation would:
        // 1. Compile the kernel for the target accelerator
        // 2. Marshal the arguments to device-appropriate format  
        // 3. Execute the kernel on the accelerator
        // 4. Return the results

        await Task.Yield(); // Allow other tasks to execute


        _logger.LogDebugMessage($"Kernel {kernelName} execution completed on {accelerator.Info.DeviceType}");

        return default!; // Placeholder return
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs)
    {
        var accelerator = await GetOptimalAcceleratorAsync(kernelName) ?? throw new InvalidOperationException($"No suitable accelerator found for kernel: {kernelName}");

        // Combine unified buffers with scalar arguments
        var allArgs = buffers.Cast<object>().Concat(scalarArgs).ToArray();
        return await ExecuteAsync<T>(kernelName, accelerator, allArgs);
    }

    /// <inheritdoc />
    public async Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName)
    {
        await Task.CompletedTask; // Make async
        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            return null;
        }

        var availableAccelerators = _runtime.GetAccelerators()
            .Where(a => registration.SupportedBackends.Contains(MapDeviceTypeToBackend(a.Info.DeviceType)))
            .ToList();

        if (availableAccelerators.Count == 0)
        {
            _logger.LogWarningMessage($"No suitable accelerators found for kernel {kernelName}");
            return null;
        }

        // Select best accelerator based on priority
        var optimalAccelerator = availableAccelerators
            .OrderBy(a => GetBackendPriority(a.Info.DeviceType))
            .FirstOrDefault();

        _logger.LogDebugMessage($"Selected {optimalAccelerator?.Info.DeviceType} for kernel {kernelName}");

        return optimalAccelerator;
    }

    /// <inheritdoc />
    public async Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        _logger.LogInfoMessage($"Pre-compilation requested for kernel {kernelName} (placeholder implementation)");
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
        await Task.CompletedTask; // Make async
        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            return Array.Empty<IAccelerator>();
        }

        var supportedAccelerators = _runtime.GetAccelerators()
            .Where(a => registration.SupportedBackends.Contains(MapDeviceTypeToBackend(a.Info.DeviceType)))
            .ToList();

        return supportedAccelerators.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<bool> ValidateKernelArgsAsync(string kernelName, params object[] args)
    {

        if (!_kernelRegistry.TryGetValue(kernelName, out _))
        {
            return false;
        }

        // Basic validation - actual implementation would validate argument types, counts, etc.
        if (args == null || args.Length == 0)
        {
            return true;
        }

        await Task.CompletedTask; // Make properly async
        return true;
    }

    private static string MapDeviceTypeToBackend(string deviceType)
    {
        return deviceType.ToUpperInvariant() switch
        {
            "CUDA" => "CUDA",
            "CPU" => "CPU",

            "METAL" => "Metal",
            "OPENCL" => "OpenCL",
            _ => deviceType
        };
    }

    private static int GetBackendPriority(string deviceType)
    {
        return deviceType.ToUpperInvariant() switch
        {
            "CUDA" => 1,     // Highest priority for compute-intensive tasks
            "METAL" => 2,    // macOS GPU acceleration
            "OPENCL" => 3,   // Cross-platform GPU
            "CPU" => 4,      // Fallback option
            _ => 999
        };
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteKernelAsync(string kernelName, IKernelExecutionParameters executionParameters)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var accelerator = (!string.IsNullOrEmpty(executionParameters.PreferredBackend)
            ? _runtime.GetAccelerators()
                .FirstOrDefault(a => a.Info.DeviceType.Equals(executionParameters.PreferredBackend, StringComparison.OrdinalIgnoreCase))
            : await GetOptimalAcceleratorAsync(kernelName)) ?? throw new InvalidOperationException($"No suitable accelerator found for kernel: {kernelName}");

        // Execute kernel and return as object
        var result = await ExecuteAsync<object>(kernelName, accelerator, executionParameters.Arguments);
        return result;
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteKernelAsync(string kernelName, object[] args, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var accelerator = await GetOptimalAcceleratorAsync(kernelName) ?? throw new InvalidOperationException($"No suitable accelerator found for kernel: {kernelName}");

        // Execute kernel and return as object
        var result = await ExecuteAsync<object>(kernelName, accelerator, args);
        return result;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
/// Information about a registered kernel from the generated registry.
/// </summary>
public class KernelRegistrationInfo
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the fully qualified kernel name.
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Gets the type containing the kernel method.
    /// </summary>
    public required Type ContainingType { get; init; }

    /// <summary>
    /// Gets the supported backend types.
    /// </summary>
    public required string[] SupportedBackends { get; init; }

    /// <summary>
    /// Gets the preferred vector size for SIMD operations.
    /// </summary>
    public int VectorSize { get; init; } = 8;

    /// <summary>
    /// Gets whether the kernel supports parallel execution.
    /// </summary>
    public bool IsParallel { get; init; } = true;

    /// <summary>
    /// Gets additional kernel metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}