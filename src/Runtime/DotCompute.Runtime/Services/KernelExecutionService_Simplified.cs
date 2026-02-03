// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Simplified kernel execution service that bridges generated kernel code with the runtime infrastructure.
/// Provides basic kernel compilation, caching, and execution without the full DI infrastructure.
/// </summary>
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Service layer logging")]
public class KernelExecutionServiceSimplified(
    AcceleratorRuntime runtime,
    ILogger<KernelExecutionServiceSimplified> logger) : Abstractions.Interfaces.IComputeOrchestrator, IDisposable
{
    private readonly AcceleratorRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly ILogger<KernelExecutionServiceSimplified> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<string, KernelRegistrationInfo> _kernelRegistry = [];
    private readonly ConcurrentDictionary<string, ICompiledKernel> _kernelCache = new();
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

        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            throw new ArgumentException($"Kernel not found: {kernelName}", nameof(kernelName));
        }

        _logger.LogDebugMessage($"Executing kernel {kernelName} on {accelerator.Info.DeviceType}");

        // 1. Get or compile the kernel for the target accelerator
        var cacheKey = $"{kernelName}_{accelerator.Info.Id}";
        var compiledKernel = await GetOrCompileKernelAsync(cacheKey, registration, accelerator);

        // 2. Marshal arguments to device-appropriate format
        var kernelArgs = await MarshalArgumentsAsync(accelerator, args);

        try
        {
            // 3. Execute the kernel on the accelerator
            await compiledKernel.ExecuteAsync(kernelArgs);

            // 4. Extract and return results
            var result = ExtractResult<T>(kernelArgs);

            _logger.LogDebugMessage($"Kernel {kernelName} execution completed on {accelerator.Info.DeviceType}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarningMessage($"Kernel {kernelName} execution failed on {accelerator.Info.DeviceType}: {ex.Message}");
            throw;
        }
    }

    private async Task<ICompiledKernel> GetOrCompileKernelAsync(
        string cacheKey,
        KernelRegistrationInfo registration,
        IAccelerator accelerator)
    {
        if (_kernelCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        _logger.LogDebugMessage($"Compiling kernel {registration.FullName} for {accelerator.Info.DeviceType}");

        var kernelDefinition = CreateKernelDefinition(registration);
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);

        // Cache the compiled kernel for future use
        _kernelCache.TryAdd(cacheKey, compiledKernel);

        return compiledKernel;
    }

    private static KernelDefinition CreateKernelDefinition(KernelRegistrationInfo registration)
    {
        return new KernelDefinition
        {
            Name = registration.FullName ?? registration.Name,
            Source = GetKernelSource(registration),
            EntryPoint = registration.Name,
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "CSharp",
                ["VectorSize"] = registration.VectorSize,
                ["IsParallel"] = registration.IsParallel
            }
        };
    }

    private static string GetKernelSource(KernelRegistrationInfo registration)
    {
        // Look for generated kernel source in the containing type's assembly
        try
        {
            var containingType = registration.ContainingType;
            var originalMethod = containingType?.GetMethod(registration.Name);

            if (originalMethod != null)
            {
                return $"// Generated kernel: {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}";
            }

            return $"// Generated kernel source for {registration.FullName}";
        }
        catch
        {
            return $"// Generated kernel source for {registration.FullName}";
        }
    }

    private async Task<KernelArguments> MarshalArgumentsAsync(IAccelerator accelerator, object[] args)
    {
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
                    var unifiedBuffer = await ConvertArrayToBufferAsync(array, accelerator);
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

    private async Task<IUnifiedMemoryBuffer> ConvertArrayToBufferAsync(Array array, IAccelerator accelerator)
    {
        var memoryManager = accelerator.Memory;
        if (memoryManager == null)
        {
            // Fallback to mock buffer if no memory manager
            return CreateMockBuffer(array);
        }

        try
        {
            return array switch
            {
                float[] floatArray => await CreateTypedBufferAsync(floatArray, memoryManager),
                double[] doubleArray => await CreateTypedBufferAsync(doubleArray, memoryManager),
                int[] intArray => await CreateTypedBufferAsync(intArray, memoryManager),
                byte[] byteArray => await CreateTypedBufferAsync(byteArray, memoryManager),
                uint[] uintArray => await CreateTypedBufferAsync(uintArray, memoryManager),
                long[] longArray => await CreateTypedBufferAsync(longArray, memoryManager),
                ulong[] ulongArray => await CreateTypedBufferAsync(ulongArray, memoryManager),
                short[] shortArray => await CreateTypedBufferAsync(shortArray, memoryManager),
                ushort[] ushortArray => await CreateTypedBufferAsync(ushortArray, memoryManager),
                _ => throw new NotSupportedException($"Array type {array.GetType()} is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarningMessage($"Failed to create unified buffer: {ex.Message}, using mock buffer");
            return CreateMockBuffer(array);
        }
    }

    private static async Task<IUnifiedMemoryBuffer> CreateTypedBufferAsync<T>(T[] array, IUnifiedMemoryManager memoryManager) where T : unmanaged
    {
        var buffer = await memoryManager.AllocateAsync<T>(array.Length);
        await memoryManager.CopyToDeviceAsync(array.AsMemory(), buffer);
        return buffer;
    }

    private static IUnifiedMemoryBuffer CreateMockBuffer(Array array)
    {
        return array switch
        {
            float[] floatArray => new SimplifiedMockBuffer<float>(floatArray),
            double[] doubleArray => new SimplifiedMockBuffer<double>(doubleArray),
            int[] intArray => new SimplifiedMockBuffer<int>(intArray),
            byte[] byteArray => new SimplifiedMockBuffer<byte>(byteArray),
            _ => new SimplifiedMockBuffer<byte>(new byte[array.Length])
        };
    }

    private static T ExtractResult<T>(KernelArguments kernelArgs)
    {
        // For void results (most common case)
        if (typeof(T) == typeof(void))
        {
            return default!;
        }

        // For array results, extract from the first output buffer
        var buffers = kernelArgs.Buffers?.ToList() ?? [];
        if (typeof(T).IsArray && buffers.Count > 0)
        {
            var firstBuffer = buffers[0];
            var elementType = typeof(T).GetElementType();

            if (elementType == typeof(float) && firstBuffer is SimplifiedMockBuffer<float> floatBuffer)
            {
                return (T)(object)floatBuffer.GetData();
            }
            else if (elementType == typeof(double) && firstBuffer is SimplifiedMockBuffer<double> doubleBuffer)
            {
                return (T)(object)doubleBuffer.GetData();
            }
            else if (elementType == typeof(int) && firstBuffer is SimplifiedMockBuffer<int> intBuffer)
            {
                return (T)(object)intBuffer.GetData();
            }
        }

        return default!;
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
        if (!_kernelRegistry.TryGetValue(kernelName, out var registration))
        {
            throw new ArgumentException($"Kernel not found: {kernelName}", nameof(kernelName));
        }

        var acceleratorsToPrecompile = accelerator != null
            ? new[] { accelerator }
            : (await GetSupportedAcceleratorsAsync(kernelName)).ToArray();

        foreach (var acc in acceleratorsToPrecompile)
        {
            var cacheKey = $"{kernelName}_{acc.Info.Id}";
            if (!_kernelCache.ContainsKey(cacheKey))
            {
                _logger.LogDebugMessage($"Pre-compiling kernel {kernelName} for {acc.Info.DeviceType}");
                await GetOrCompileKernelAsync(cacheKey, registration, acc);
            }
        }

        _logger.LogInfoMessage($"Pre-compiled kernel {kernelName} for {acceleratorsToPrecompile.Length} accelerators");
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
            // Dispose all cached compiled kernels
            foreach (var kernel in _kernelCache.Values)
            {
                kernel.Dispose();
            }
            _kernelCache.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
/// Simplified mock buffer implementation for the simplified kernel execution service.
/// </summary>
/// <typeparam name="T">The element type</typeparam>
internal sealed class SimplifiedMockBuffer<T>(T[] data) : IUnifiedMemoryBuffer where T : unmanaged
{
    private readonly T[] _data = data ?? throw new ArgumentNullException(nameof(data));
    private bool _disposed;

    /// <inheritdoc />
    public int Length => _data.Length;

    /// <inheritdoc />
    public long SizeInBytes => _data.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

    /// <inheritdoc />
    public Abstractions.Memory.MemoryOptions Options => Abstractions.Memory.MemoryOptions.None;

    /// <inheritdoc />
    public bool IsDisposed => _disposed;

    /// <inheritdoc />
    public Abstractions.Memory.BufferState State => Abstractions.Memory.BufferState.Synchronized;

    /// <summary>
    /// Gets the underlying data array.
    /// </summary>
    public T[] GetData() => _data;

    /// <inheritdoc />
    public ValueTask CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        if (typeof(TSource) == typeof(T))
        {
            var sourceSpan = source.Span;
            var destSpan = _data.AsSpan((int)offset);
            System.Runtime.InteropServices.MemoryMarshal.Cast<TSource, T>(sourceSpan).CopyTo(destSpan);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CopyToAsync<TDest>(Memory<TDest> destination, long offset = 0, CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        if (typeof(TDest) == typeof(T))
        {
            var sourceSpan = _data.AsSpan((int)offset);
            var destSpan = destination.Span;
            System.Runtime.InteropServices.MemoryMarshal.Cast<T, TDest>(sourceSpan).CopyTo(destSpan);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, CancellationToken cancellationToken = default) where TSource : unmanaged
        => CopyFromAsync(source, offset, cancellationToken);

    /// <inheritdoc />
    public ValueTask CopyToHostAsync<TDest>(Memory<TDest> destination, long offset = 0, CancellationToken cancellationToken = default) where TDest : unmanaged
        => CopyToAsync(destination, offset, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
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
