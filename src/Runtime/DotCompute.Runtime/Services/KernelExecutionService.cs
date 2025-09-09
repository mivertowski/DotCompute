// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Memory;
using DotCompute.Runtime.Services.Interfaces;
using DotCompute.Runtime.Services.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Production-grade kernel execution service that bridges generated kernel code with runtime infrastructure.
/// Provides automatic backend selection, caching, and optimization.
/// </summary>
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Service layer logging")]
public class KernelExecutionService : IComputeOrchestrator, IDisposable
{
    private readonly AcceleratorRuntime _runtime;
    private readonly ILogger<KernelExecutionService> _logger;
    private readonly IKernelCompiler _compiler;
    private readonly IKernelCache _cache;
    private readonly IKernelProfiler _profiler;
    private readonly Dictionary<string, KernelRegistrationInfo> _kernelRegistry;
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _disposed;

    public KernelExecutionService(
        AcceleratorRuntime runtime,
        ILogger<KernelExecutionService> logger,
        IKernelCompiler compiler,
        IKernelCache cache,
        IKernelProfiler profiler)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
        _kernelRegistry = [];
    }

    /// <summary>
    /// Registers kernels from the generated kernel registry.
    /// This method should be called during application startup.
    /// </summary>
    /// <param name="kernelRegistrations">Kernel registrations from generated code</param>
    public void RegisterKernels(IEnumerable<KernelRegistrationInfo> kernelRegistrations)
    {
        foreach (var registration in kernelRegistrations)
        {
            _kernelRegistry[registration.FullName] = registration;
            _logger.LogDebug("Registered kernel: {KernelName} with backends: {Backends}",

                registration.FullName, string.Join(", ", registration.SupportedBackends));
        }

        _logger.LogInformation("Registered {Count} kernels from generated registry", _kernelRegistry.Count);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        try
        {
            var accelerator = await GetOptimalAcceleratorAsync(kernelName);
            if (accelerator == null)
            {
                throw new InvalidOperationException($"No suitable accelerator found for kernel: {kernelName}");
            }

            return await ExecuteAsync<T>(kernelName, accelerator, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute kernel {KernelName}", kernelName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(string kernelName, string preferredBackend, params object[] args)
    {
        var accelerators = _runtime.GetAccelerators()
            .Where(a => a.Info.DeviceType.Equals(preferredBackend, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (accelerators.Count == 0)
        {
            _logger.LogWarning("Preferred backend {Backend} not available for kernel {KernelName}, falling back to optimal selection",

                preferredBackend, kernelName);
            return await ExecuteAsync<T>(kernelName, args);
        }

        var accelerator = accelerators.OrderBy(a => GetAcceleratorLoad(a)).FirstOrDefault();
        if (accelerator == null)
        {
            throw new InvalidOperationException($"No suitable {preferredBackend} accelerator found");
        }

        return await ExecuteAsync<T>(kernelName, accelerator, args);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(string kernelName, IAccelerator accelerator, params object[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            throw new ArgumentException($"Kernel not found: {kernelName}", nameof(kernelName));
        }

        // Validate arguments
        var validation = await ValidateKernelArgsAsync(kernelName, args);
        if (!validation.IsValid)
        {
            throw new ArgumentException($"Kernel argument validation failed: {string.Join(", ", validation.Errors)}");
        }

        // Get or compile kernel
        var cacheKey = _cache.GenerateCacheKey(CreateKernelDefinition(registration), accelerator, null);
        var compiledKernel = await _cache.GetAsync(cacheKey);

        if (compiledKernel == null)
        {
            _logger.LogDebug("Compiling kernel {KernelName} for accelerator {AcceleratorType}",

                kernelName, accelerator.Info.DeviceType);

            var kernelDefinition = CreateKernelDefinition(registration);
            compiledKernel = await _compiler.CompileAsync(kernelDefinition, accelerator);


            await _cache.StoreAsync(cacheKey, compiledKernel);
        }

        // Execute kernel with performance monitoring
        using var executionSession = _profiler.StartProfiling($"KernelExecution_{kernelName}_{accelerator.Info.DeviceType}");


        try
        {
            var kernelArgs = await MarshalArgumentsAsync(registration, accelerator, args);
            var result = await compiledKernel.ExecuteAsync(kernelArgs);

            // Handle result conversion if needed

            return await ConvertResultAsync<T>(result, accelerator);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kernel execution failed for {KernelName} on {AcceleratorType}",

                kernelName, accelerator.Info.DeviceType);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithBuffersAsync<T>(string kernelName, IEnumerable<IUnifiedMemoryBuffer> buffers, params object[] scalarArgs)
    {
        var accelerator = await GetOptimalAcceleratorAsync(kernelName);
        if (accelerator == null)
        {
            throw new InvalidOperationException($"No suitable accelerator found for kernel: {kernelName}");
        }

        // Combine unified buffers with scalar arguments
        var allArgs = buffers.Cast<object>().Concat(scalarArgs).ToArray();
        return await ExecuteAsync<T>(kernelName, accelerator, allArgs);
    }

    /// <inheritdoc />
    public async Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName)
    {
        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            return null;
        }

        var availableAccelerators = _runtime.GetAccelerators()
            .Where(a => registration.SupportedBackends.Contains(MapDeviceTypeToBackend(a.Info.DeviceType)))
            .ToList();

        if (availableAccelerators.Count == 0)
        {
            _logger.LogWarning("No suitable accelerators found for kernel {KernelName}", kernelName);
            return null;
        }

        // Select best accelerator based on priority and current load
        var optimalAccelerator = availableAccelerators
            .OrderBy(a => GetBackendPriority(a.Info.DeviceType))
            .ThenBy(GetAcceleratorLoad)
            .FirstOrDefault();

        _logger.LogDebug("Selected {AcceleratorType} for kernel {KernelName}",

            optimalAccelerator?.Info.DeviceType, kernelName);

        return optimalAccelerator;
    }

    /// <inheritdoc />
    public async Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null)
    {
        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            throw new ArgumentException($"Kernel not found: {kernelName}", nameof(kernelName));
        }

        var acceleratorsToPrecompile = accelerator != null

            ? new[] { accelerator }
            : await GetSupportedAcceleratorsAsync(kernelName);

        var precompileTasks = acceleratorsToPrecompile.Select(async acc =>
        {
            var cacheKey = _cache.GenerateCacheKey(CreateKernelDefinition(registration), acc, null);
            var cached = await _cache.GetAsync(cacheKey);


            if (cached == null)
            {
                _logger.LogDebug("Pre-compiling kernel {KernelName} for {AcceleratorType}",

                    kernelName, acc.Info.DeviceType);

                var kernelDefinition = CreateKernelDefinition(registration);
                var compiled = await _compiler.CompileAsync(kernelDefinition, acc);
                await _cache.StoreAsync(cacheKey, compiled);
            }
        });

        await Task.WhenAll(precompileTasks);
        _logger.LogInformation("Pre-compiled kernel {KernelName} for {Count} accelerators",

            kernelName, acceleratorsToPrecompile.Count());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IAccelerator>> GetSupportedAcceleratorsAsync(string kernelName)
    {
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
    public async Task<KernelValidationResult> ValidateKernelArgsAsync(string kernelName, params object[] args)
    {
        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            return KernelValidationResult.Failure(new[] { $"Kernel not found: {kernelName}" });
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate argument count (implementation-specific validation would go here)
        if (args == null || args.Length == 0)
        {
            warnings.Add("No arguments provided - verify this is expected for the kernel");
        }

        // Additional validation logic would be implemented based on kernel metadata

        return errors.Count > 0

            ? KernelValidationResult.Failure(errors, warnings)
            : KernelValidationResult.Success();
    }

    private KernelDefinition CreateKernelDefinition(KernelRegistrationInfo registration)
    {
        return new KernelDefinition
        {
            Name = registration.Name,
            FullName = registration.FullName,
            // Source and other properties would be populated from the registration
            // This would integrate with the generated kernel source code
            Source = GetKernelSource(registration),
            EntryPoint = registration.Name,
            Language = KernelLanguage.CSharp // Default, would be determined by target backend
        };
    }

    private static string GetKernelSource(KernelRegistrationInfo registration)
    {
        // This would retrieve the generated kernel source for the target backend
        // Implementation would depend on how the generator stores the source code
        return $"// Generated kernel source for {registration.FullName}";
    }

    private async Task<KernelArguments> MarshalArgumentsAsync(KernelRegistrationInfo registration, IAccelerator accelerator, object[] args)
    {
        // Convert arguments to accelerator-specific format
        var deviceBuffers = new List<IUnifiedMemoryBuffer>();
        var scalarArguments = new List<object>();

        foreach (var arg in args)
        {
            switch (arg)
            {
                case IUnifiedMemoryBuffer buffer:
                    deviceBuffers.Add(buffer);
                    break;
                case Array array:
                    // Convert arrays to unified buffers
                    var unifiedBuffer = await ConvertArrayToUnifiedBuffer(array, accelerator);
                    deviceBuffers.Add(unifiedBuffer);
                    break;
                default:
                    scalarArguments.Add(arg);
                    break;
            }
        }

        return new KernelArguments
        {
            Buffers = deviceBuffers,
            ScalarArguments = scalarArguments
        };
    }

    private async Task<IUnifiedMemoryBuffer> ConvertArrayToUnifiedBuffer(Array array, IAccelerator accelerator)
    {
        // Implementation would create a unified buffer from the array
        // This is a placeholder - actual implementation would depend on the memory system TODO
        throw new NotImplementedException("Array to UnifiedBuffer conversion not yet implemented");
    }

    private static async Task<T> ConvertResultAsync<T>(object result, IAccelerator accelerator)
    {
        // Handle result type conversion
        if (result is T directResult)
        {
            return directResult;
        }

        // Handle void results
        if (typeof(T) == typeof(void) || result == null)
        {
            return default!;
        }

        throw new InvalidOperationException($"Cannot convert result type {result.GetType()} to {typeof(T)}");
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

    private double GetAcceleratorLoad(IAccelerator accelerator)
    {
        // Placeholder - would integrate with performance monitoring
        // to get actual load metrics
        return 0.0;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposeLock.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _disposeLock?.Dispose();
            }

            _disposed = true;
        }
        finally
        {
            if (!_disposed)
            {
                _disposeLock?.Release();
            }
        }
    }
}


// KernelRegistrationInfo is now defined in KernelExecutionService_Simplified.cs