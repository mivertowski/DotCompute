// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Memory;
using DotCompute.Runtime.Services.Interfaces;
using Microsoft.Extensions.Logging;
using KernelValidationResult = DotCompute.Runtime.Services.Types.KernelValidationResult;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Production-grade kernel execution service that bridges generated kernel code with runtime infrastructure.
/// Provides automatic backend selection, caching, and optimization.
/// </summary>
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

    #region LoggerMessage Delegates
    
    private static readonly Action<ILogger, string, string, Exception?> LogKernelRegistered =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(1001, nameof(LogKernelRegistered)),
            "Registered kernel: {KernelName} with backends: {Backends}");
    
    private static readonly Action<ILogger, int, Exception?> LogKernelsRegistered =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1002, nameof(LogKernelsRegistered)),
            "Registered {Count} kernels from generated registry");
    
    private static readonly Action<ILogger, string, Exception?> LogKernelExecutionError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1003, nameof(LogKernelExecutionError)),
            "Failed to execute kernel {KernelName}");
    
    private static readonly Action<ILogger, string, string, Exception?> LogPreferredBackendFallback =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1004, nameof(LogPreferredBackendFallback)),
            "Preferred backend {Backend} not available for kernel {KernelName}, falling back to optimal selection");
    
    private static readonly Action<ILogger, string, string, Exception?> LogCompilingKernel =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(1005, nameof(LogCompilingKernel)),
            "Compiling kernel {KernelName} for accelerator {AcceleratorType}");
    
    private static readonly Action<ILogger, string, string, Exception?> LogKernelExecutionFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1006, nameof(LogKernelExecutionFailed)),
            "Kernel execution failed for {KernelName} on {AcceleratorType}");
    
    private static readonly Action<ILogger, string, Exception?> LogNoSuitableAccelerators =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1007, nameof(LogNoSuitableAccelerators)),
            "No suitable accelerators found for kernel {KernelName}");
    
    private static readonly Action<ILogger, string?, string, Exception?> LogSelectedAccelerator =
        LoggerMessage.Define<string?, string>(
            LogLevel.Debug,
            new EventId(1008, nameof(LogSelectedAccelerator)),
            "Selected {AcceleratorType} for kernel {KernelName}");
    
    private static readonly Action<ILogger, string, string, Exception?> LogPrecompilingKernel =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(1009, nameof(LogPrecompilingKernel)),
            "Pre-compiling kernel {KernelName} for {AcceleratorType}");
    
    private static readonly Action<ILogger, string, int, Exception?> LogPrecompiledKernel =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(1010, nameof(LogPrecompiledKernel)),
            "Pre-compiled kernel {KernelName} for {Count} accelerators");
    
    private static readonly Action<ILogger, Exception?> LogNoArgumentsWarning =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1011, nameof(LogNoArgumentsWarning)),
            "No arguments provided - verify this is expected for the kernel");
    
    #endregion

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
            LogKernelRegistered(_logger, registration.FullName, string.Join(", ", registration.SupportedBackends), null);
        }

        LogKernelsRegistered(_logger, _kernelRegistry.Count, null);
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
            LogKernelExecutionError(_logger, kernelName, ex);
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
            LogPreferredBackendFallback(_logger, preferredBackend, kernelName, null);
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
        var isValid = await ValidateKernelArgsAsync(kernelName, args);
        if (!isValid)
        {
            throw new ArgumentException($"Kernel argument validation failed for kernel: {kernelName}");
        }

        // Get or compile kernel
        var cacheKey = _cache.GenerateCacheKey(CreateKernelDefinition(registration), accelerator, null);
        var compiledKernel = await _cache.GetAsync(cacheKey);

        if (compiledKernel == null)
        {
            LogCompilingKernel(_logger, kernelName, accelerator.Info.DeviceType, null);

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
            LogKernelExecutionFailed(_logger, kernelName, accelerator.Info.DeviceType, ex);
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
            LogNoSuitableAccelerators(_logger, kernelName, null);
            return null;
        }

        // Select best accelerator based on priority and current load
        var optimalAccelerator = availableAccelerators
            .OrderBy(a => GetBackendPriority(a.Info.DeviceType))
            .ThenBy(GetAcceleratorLoad)
            .FirstOrDefault();

        LogSelectedAccelerator(_logger, optimalAccelerator?.Info.DeviceType, kernelName, null);

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
                LogPrecompilingKernel(_logger, kernelName, acc.Info.DeviceType, null);

                var kernelDefinition = CreateKernelDefinition(registration);
                var compiled = await _compiler.CompileAsync(kernelDefinition, acc);
                await _cache.StoreAsync(cacheKey, compiled);
            }
        });

        await Task.WhenAll(precompileTasks);
        LogPrecompiledKernel(_logger, kernelName, acceleratorsToPrecompile.Count(), null);
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
    public async Task<bool> ValidateKernelArgsAsync(string kernelName, params object[] args)
    {
        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            return false;
        }

        // Validate argument count (implementation-specific validation would go here)
        if (args == null || args.Length == 0)
        {
            // Might be valid for some kernels, so just return true with a warning logged
            LogNoArgumentsWarning(_logger, null);
        }

        // Additional validation logic would be implemented based on kernel metadata TODO

        return await Task.FromResult(true);
    }

    private KernelDefinition CreateKernelDefinition(KernelRegistrationInfo registration)
    {
        return new KernelDefinition
        {
            Name = registration.Name,
            FullName = registration.FullName,
            // Source and other properties would be populated from the registration
            // This would integrate with the generated kernel source code TODO
            Source = GetKernelSource(registration),
            EntryPoint = registration.Name,
            Language = KernelLanguage.CSharp // Default, would be determined by target backend
        };
    }

    private static string GetKernelSource(KernelRegistrationInfo registration)
    {
        // This would retrieve the generated kernel source for the target backend
        // Implementation would depend on how the generator stores the source code TODO
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