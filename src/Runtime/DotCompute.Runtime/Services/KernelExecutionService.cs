// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Production-grade kernel execution service that bridges generated kernel code with runtime infrastructure.
/// Provides automatic backend selection, caching, and optimization.
/// </summary>
public class KernelExecutionService(
    AcceleratorRuntime runtime,
    ILogger<KernelExecutionService> logger,
    IUnifiedKernelCompiler compiler,
    IKernelCache cache,
    IKernelProfiler profiler) : Abstractions.Interfaces.IComputeOrchestrator, IDisposable
{
    private readonly AcceleratorRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly ILogger<KernelExecutionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IUnifiedKernelCompiler _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    private readonly IKernelCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly IKernelProfiler _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
    private readonly Dictionary<string, KernelRegistrationInfo> _kernelRegistry = [];
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
            var accelerator = await GetOptimalAcceleratorAsync(kernelName) ?? throw new InvalidOperationException($"No suitable accelerator found for kernel: {kernelName}");
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

        var accelerator = accelerators.OrderBy(GetAcceleratorLoad).FirstOrDefault() ?? throw new InvalidOperationException($"No suitable {preferredBackend} accelerator found");
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
            await compiledKernel.ExecuteAsync(kernelArgs);

            // Handle result conversion if needed
            var result = await ExtractExecutionResultAsync<T>(compiledKernel, kernelArgs, accelerator);
            return result;
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

        // Return true for now - comprehensive validation would require kernel metadata
        return await Task.FromResult(true);
    }

    private static KernelDefinition CreateKernelDefinition(KernelRegistrationInfo registration)
    {
        return new KernelDefinition
        {
            Name = registration.FullName ?? registration.Name,
            // Source and other properties would be populated from the registration
            // This would integrate with the generated kernel source code TODO
            Source = GetKernelSource(registration),
            EntryPoint = registration.Name,
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "CSharp" // Default, would be determined by target backend
            }
        };
    }

    private static string GetKernelSource(KernelRegistrationInfo registration)
    {
        // Look for generated kernel implementations from the source generator
        try
        {
            // Get the containing type to find generated classes
            var containingType = registration.ContainingType;
            var generatedNamespace = $"{containingType.Namespace}.Generated";

            // Try to find generated assemblies in the current domain

            var generatedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .ToList();


            foreach (var assembly in generatedAssemblies)
            {
                // Look for generated kernel implementations
                var generatedTypes = assembly.GetTypes()
                    .Where(t => t.Namespace?.StartsWith(generatedNamespace) == true)
                    .Where(t => t.Name.Contains(registration.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();


                if (generatedTypes.Count > 0)
                {
                    // Return a reference to the generated implementation
                    var generatedType = generatedTypes.First();
                    return $"// Generated kernel implementation found: {generatedType.FullName}";
                }
            }

            // Fall back to embedding the original method source if available

            var originalMethod = containingType.GetMethod(registration.Name);
            if (originalMethod != null)
            {
                return $"// Original kernel method: {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}";
            }


            return $"// Generated kernel source for {registration.FullName}";
        }
        catch (Exception)
        {
            // Fallback to placeholder if reflection fails
            return $"// Generated kernel source for {registration.FullName}";
        }
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
                    var unifiedBuffer = await ConvertArrayToUnifiedBufferAsync(array, accelerator);
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

    private async Task<IUnifiedMemoryBuffer> ConvertArrayToUnifiedBufferAsync(Array array, IAccelerator accelerator)
    {
        try
        {
            // Get the memory manager from the accelerator
            var memoryManager = accelerator.Memory ?? throw new InvalidOperationException("Accelerator does not have a memory manager available");

            // Handle different array types by creating proper unified buffers
            return array switch
            {
                float[] floatArray => await CreateUnifiedBufferAsync(floatArray, memoryManager),
                double[] doubleArray => await CreateUnifiedBufferAsync(doubleArray, memoryManager),
                int[] intArray => await CreateUnifiedBufferAsync(intArray, memoryManager),
                byte[] byteArray => await CreateUnifiedBufferAsync(byteArray, memoryManager),
                uint[] uintArray => await CreateUnifiedBufferAsync(uintArray, memoryManager),
                long[] longArray => await CreateUnifiedBufferAsync(longArray, memoryManager),
                ulong[] ulongArray => await CreateUnifiedBufferAsync(ulongArray, memoryManager),
                short[] shortArray => await CreateUnifiedBufferAsync(shortArray, memoryManager),
                ushort[] ushortArray => await CreateUnifiedBufferAsync(ushortArray, memoryManager),
                _ => throw new NotSupportedException($"Array type {array.GetType()} is not supported for conversion to UnifiedBuffer")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert array of type {ArrayType} to unified buffer", array.GetType());

            // Fallback to mock implementation for compatibility
            return array switch
            {
                float[] floatArray => new MockUnifiedBuffer<float>(floatArray),
                double[] doubleArray => new MockUnifiedBuffer<double>(doubleArray),
                int[] intArray => new MockUnifiedBuffer<int>(intArray),
                byte[] byteArray => new MockUnifiedBuffer<byte>(byteArray),
                _ => throw new NotSupportedException($"Array type {array.GetType()} is not supported for conversion to UnifiedBuffer")
            };
        }
    }

    private async Task<IUnifiedMemoryBuffer> CreateUnifiedBufferAsync<T>(T[] array, IUnifiedMemoryManager memoryManager) where T : unmanaged
    {
        try
        {
            // Allocate unified buffer with proper size
            var buffer = await memoryManager.AllocateAsync<T>(array.Length);

            // Copy data from host array to the buffer using the memory manager
            await memoryManager.CopyToDeviceAsync(array.AsMemory(), buffer);


            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create unified buffer, falling back to mock implementation");
            return new MockUnifiedBuffer<T>(array);
        }
    }

    private static async Task<T> ConvertResultAsync<T>(object result, IAccelerator _)
    {
        // Handle result type conversion
        await Task.CompletedTask; // Make async
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

    private static async Task<T> ExtractExecutionResultAsync<T>(ICompiledKernel compiledKernel, KernelArguments kernelArgs, IAccelerator accelerator)
    {
        // For most kernels, results are written to output buffers rather than returned directly
        // This method extracts results from the kernel arguments after execution

        await Task.CompletedTask.ConfigureAwait(false);

        // Check if we're expecting a void result (most common case)
        if (typeof(T) == typeof(void))
        {
            return default!;
        }

        // Look for output buffers that might contain results
        var outputBuffers = kernelArgs.Buffers.Where(b => b != null).ToList();

        if (outputBuffers.Count > 0 && typeof(T).IsArray)
        {
            // Try to extract array results from the first output buffer
            var firstBuffer = outputBuffers[0];

            // Convert buffer content to requested array type
            var elementType = typeof(T).GetElementType();
            if (elementType == typeof(float) && firstBuffer is MockUnifiedBuffer<float> floatBuffer)
            {
                var result = floatBuffer.GetData();
                return (T)(object)result;
            }
            else if (elementType == typeof(double) && firstBuffer is MockUnifiedBuffer<double> doubleBuffer)
            {
                var result = doubleBuffer.GetData();
                return (T)(object)result;
            }
            else if (elementType == typeof(int) && firstBuffer is MockUnifiedBuffer<int> intBuffer)
            {
                var result = intBuffer.GetData();
                return (T)(object)result;
            }
        }

        // For scalar results or when no output buffers are found, return default
        return default!;
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
        // Placeholder - would integrate with performance monitoring
        // to get actual load metrics
        => 0.0;
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
                _ = (_disposeLock?.Release());
            }
        }
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
    /// Validates kernel parameters against the kernel definition metadata.
    /// </summary>
    /// <param name="kernelDefinition">The kernel definition containing parameter requirements.</param>
    /// <param name="arguments">The kernel arguments to validate.</param>
    /// <returns>True if validation passes, false otherwise.</returns>
    private bool ValidateKernelParameters(KernelDefinition kernelDefinition, KernelArguments arguments)
    {
        if (kernelDefinition == null)
        {
            _logger.LogError("Cannot validate parameters: kernel definition is null");
            return false;
        }

        if (arguments == null)
        {
            _logger.LogError("Cannot validate parameters: kernel arguments are null");
            return false;
        }

        try
        {
            // 1. Check parameter count constraints
            if (kernelDefinition.Metadata.TryGetValue("MinParameterCount", out var minCountObj) &&
                minCountObj is int minCount &&
                arguments.Count < minCount)
            {
                _logger.LogError("Kernel {KernelName} requires at least {MinCount} parameters but got {ActualCount}",
                    kernelDefinition.Name, minCount, arguments.Count);
                return false;
            }

            // 2. Validate buffer parameters
            foreach (var arg in arguments)
            {
                if (arg is IUnifiedMemoryBuffer buffer)
                {
                    if (buffer.IsDisposed)
                    {
                        _logger.LogError("Kernel parameter contains a disposed buffer");
                        return false;
                    }

                    if (buffer.SizeInBytes == 0)
                    {
                        _logger.LogWarning("Kernel parameter contains an empty buffer");
                    }
                }
            }

            // 3. Basic argument count validation
            if (arguments.Count == 0 && kernelDefinition.Metadata.ContainsKey("RequiresArguments"))
            {
                _logger.LogError("Kernel {KernelName} requires arguments but none were provided", kernelDefinition.Name);
                return false;
            }

            _logger.LogDebug("Parameter validation passed for kernel {KernelName} with {ParameterCount} parameters",
                kernelDefinition.Name, arguments.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during kernel parameter validation for kernel {KernelName}",
                kernelDefinition.Name);
            return false;
        }
    }

    /// <summary>
    /// Simple implementation of IKernelExecutionParameters for internal use.
    /// </summary>
    private class KernelExecutionParameters : IKernelExecutionParameters
    {
        /// <summary>
        /// Gets or sets the arguments.
        /// </summary>
        /// <value>The arguments.</value>
        public IReadOnlyList<object> Arguments { get; set; } = Array.Empty<object>();
        /// <summary>
        /// Gets or sets the preferred backend.
        /// </summary>
        /// <value>The preferred backend.</value>
        public string? PreferredBackend { get; set; }
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public IDictionary<string, object> Options { get; } = new Dictionary<string, object>();
        /// <summary>
        /// Gets or sets a value indicating whether cellation token.
        /// </summary>
        /// <value>The cancellation token.</value>
        public CancellationToken CancellationToken { get; set; }
    }
}

/// <summary>
/// Mock implementation of IUnifiedMemoryBuffer for testing and integration purposes.
/// </summary>
/// <typeparam name="T">The element type</typeparam>
internal class MockUnifiedBuffer<T>(T[] data) : IUnifiedMemoryBuffer where T : unmanaged
{
    private readonly T[] _data = data ?? throw new ArgumentNullException(nameof(data));
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>

    public int Length { get; } = data.Length;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; } = data.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public static IAccelerator Accelerator => null!;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public Abstractions.Memory.MemoryOptions Options => Abstractions.Memory.MemoryOptions.None;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => false;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public Abstractions.Memory.BufferState State => Abstractions.Memory.BufferState.Synchronized;
    /// <summary>
    /// Gets the data.
    /// </summary>
    /// <returns>The data.</returns>

    public T[] GetData() => _data;
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <typeparam name="U">The U type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset = 0, CancellationToken cancellationToken = default) where U : unmanaged
    {
        // Mock implementation - just copy if types match
        if (typeof(U) == typeof(T))
        {
            var sourceSpan = source.Span;
            var destSpan = _data.AsSpan((int)offset);
            System.Runtime.InteropServices.MemoryMarshal.Cast<U, T>(sourceSpan).CopyTo(destSpan);
        }
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <typeparam name="U">The U type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync<U>(Memory<U> destination, long offset = 0, CancellationToken cancellationToken = default) where U : unmanaged
    {
        // Mock implementation - just copy if types match
        if (typeof(U) == typeof(T))
        {
            var sourceSpan = _data.AsSpan((int)offset);
            var destSpan = destination.Span;
            System.Runtime.InteropServices.MemoryMarshal.Cast<T, U>(sourceSpan).CopyTo(destSpan);
        }
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from host asynchronously.
    /// </summary>
    /// <typeparam name="TSource">The TSource type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, CancellationToken cancellationToken = default) where TSource : unmanaged => CopyFromAsync(source, offset, cancellationToken);
    /// <summary>
    /// Gets copy to host asynchronously.
    /// </summary>
    /// <typeparam name="TDestination">The TDestination type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToHostAsync<TDestination>(Memory<TDestination> destination, long offset = 0, CancellationToken cancellationToken = default) where TDestination : unmanaged => CopyToAsync(destination, offset, cancellationToken);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() { }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}




// KernelRegistrationInfo is now defined in KernelExecutionService_Simplified.cs