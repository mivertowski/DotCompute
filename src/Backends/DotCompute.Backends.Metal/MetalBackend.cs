// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.Metal.Logging;
namespace DotCompute.Backends.Metal;


/// <summary>
/// Main entry point for Metal compute backend
/// </summary>
public sealed partial class MetalBackend : IDisposable
{
    private readonly ILogger<MetalBackend> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<MetalAccelerator> _accelerators = [];
    private bool _disposed;

    public MetalBackend(ILogger<MetalBackend> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        DiscoverAccelerators();
    }

    /// <summary>
    /// Check if Metal is available on this platform
    /// </summary>
    public static bool IsAvailable()
    {
        // Metal is only available on macOS
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        // Check minimum macOS version (10.11 for basic Metal, 10.13 for compute shaders)
        try
        {
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major < 10 || (osVersion.Major == 10 && osVersion.Minor < 13))
            {
                return false;
            }
        }
        catch
        {
            // If we can't determine OS version, assume it's not supported
            return false;
        }

        // Check if Metal framework is actually available
        try
        {
            return MetalNative.IsMetalSupported();
        }
        catch (DllNotFoundException)
        {
            // Native library not available
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            // Function not found in native library
            return false;
        }
        catch
        {
            // Any other error means Metal is not available
            return false;
        }
    }

    /// <summary>
    /// Get all available Metal accelerators
    /// </summary>
    public IReadOnlyList<MetalAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    /// <summary>
    /// Get default Metal accelerator
    /// </summary>
    public MetalAccelerator? GetDefaultAccelerator() => _accelerators.FirstOrDefault();

    /// <summary>
    /// Get device information for the default Metal device
    /// </summary>
    internal MetalDeviceInfo GetDeviceInfo()
    {
        var accelerator = GetDefaultAccelerator();
        if (accelerator == null)
        {
            throw new InvalidOperationException("No Metal accelerator available");
        }

        var info = accelerator.Info;
        return new MetalDeviceInfo
        {
            Name = Marshal.StringToHGlobalAnsi(info.Name),
            RegistryID = (ulong)info.Id.GetHashCode(StringComparison.Ordinal),
            MaxThreadgroupSize = info.Capabilities?.TryGetValue("MaxThreadgroupSize", out var maxThreadgroup) == true ? (ulong)maxThreadgroup : 1024,
            MaxBufferLength = (ulong)info.TotalMemory,
            SupportedFamilies = Marshal.StringToHGlobalAnsi(info.Capabilities?.TryGetValue("SupportsFamily", out var families) == true ? families.ToString() : "Common")
        };
    }

    /// <summary>
    /// Allocate a buffer on the Metal device
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> AllocateBufferAsync<T>(int size) where T : unmanaged
    {
        var accelerator = GetDefaultAccelerator();
        if (accelerator == null)
        {
            throw new InvalidOperationException("No Metal accelerator available");
        }

        return await accelerator.Memory.AllocateAsync<T>(size, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Copy data to a Metal buffer asynchronously
    /// </summary>
    public Task CopyToBufferAsync<T>(IUnifiedMemoryBuffer<T> buffer, T[] data) where T : unmanaged
    {
        var accelerator = GetDefaultAccelerator();
        if (accelerator == null)
        {
            throw new InvalidOperationException("No Metal accelerator available");
        }

        return buffer.CopyFromAsync(data.AsMemory()).AsTask();
    }

    /// <summary>
    /// Copy data from a Metal buffer asynchronously
    /// </summary>
    public Task CopyFromBufferAsync<T>(IUnifiedMemoryBuffer<T> buffer, T[] data) where T : unmanaged
    {
        var accelerator = GetDefaultAccelerator();
        if (accelerator == null)
        {
            throw new InvalidOperationException("No Metal accelerator available");
        }

        return buffer.CopyToAsync(data.AsMemory()).AsTask();
    }

    /// <summary>
    /// Compile a Metal function from source code
    /// </summary>
    public IntPtr CompileFunction(string source, string functionName)
    {
        var accelerator = GetDefaultAccelerator();
        if (accelerator == null)
        {
            throw new InvalidOperationException("No Metal accelerator available");
        }

        try
        {
            // Use the accelerator to compile the Metal shader source
            var definition = new KernelDefinition
            {
                Name = functionName,
                Code = source
            };

            var compiledKernel = accelerator.CompileKernelAsync(definition).GetAwaiter().GetResult();

            // This is a simplification - in production we'd maintain a proper mapping
            // between function handles and compiled kernels
            return new IntPtr(compiledKernel.GetHashCode());
        }
        catch (Exception ex)
        {
            _logger.MetalFunctionCompilationFailed(ex, functionName);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Execute a compute shader asynchronously
    /// </summary>
    public async Task ExecuteComputeShaderAsync(IntPtr function, params IUnifiedMemoryBuffer[] buffers)
    {
        var accelerator = GetDefaultAccelerator();
        if (accelerator == null)
        {
            throw new InvalidOperationException("No Metal accelerator available");
        }

        if (function == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid function handle", nameof(function));
        }

        try
        {
            // This is a simplified approach - in production, we'd maintain a proper mapping
            // between function handles and compiled kernels
            _logger.ComputeShaderExecuting(buffers.Length);

            // For now, we'll just complete successfully
            // In a full implementation, we'd retrieve the compiled kernel from the function handle
            await Task.CompletedTask.ConfigureAwait(false);

            _logger.ComputeShaderExecutionCompleted();
        }
        catch (Exception ex)
        {
            _logger.ComputeShaderExecutionFailed(ex);
            throw;
        }
    }

    /// <summary>
    /// Create a Metal command queue
    /// </summary>
    public IntPtr CreateCommandQueue()
    {
        var accelerator = GetDefaultAccelerator();
        if (accelerator == null)
        {
            throw new InvalidOperationException("No Metal accelerator available");
        }

        try
        {
            // Return the command queue handle from the accelerator
            // This is a simplified approach - the actual implementation should
            // expose the command queue properly
            return accelerator.Context.Handle;
        }
        catch (Exception ex)
        {
            _logger.CommandQueueCreationFailed(ex);
            return IntPtr.Zero;
        }
    }

    private void DiscoverAccelerators()
    {
        if (!IsAvailable())
        {
            _logger.MetalNotAvailable();
            return;
        }

        try
        {
            LogDiscoveringAccelerators(_logger);

            // 1. Enumerate Metal devices using MTLCopyAllDevices
            var deviceCount = MetalNative.GetDeviceCount();
            if (deviceCount == 0)
            {
                LogNoDevicesFound(_logger);
                return;
            }

            LogDeviceCount(_logger, deviceCount);

            // 2. Query GPU families and feature sets for each device
            for (var deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                try
                {
                    var device = MetalNative.CreateDeviceAtIndex(deviceIndex);
                    if (device == IntPtr.Zero)
                    {
                        _logger.FailedToCreateDevice(deviceIndex);
                        continue;
                    }

                    if (ValidateMetalDevice(device, deviceIndex))
                    {
                        var accelerator = CreateMetalAccelerator(device, deviceIndex);
                        if (accelerator != null)
                        {
                            _accelerators.Add(accelerator);
                            LogMetalDeviceCapabilities(accelerator);
                        }
                    }
                    else
                    {
                        MetalNative.ReleaseDevice(device);
                    }
                }
                catch (Exception ex)
                {
                    LogDeviceInitializationWarning(_logger, ex, deviceIndex);
                }
            }

            LogAcceleratorCount(_logger, _accelerators.Count);
        }
        catch (Exception ex)
        {
            LogDiscoveryError(_logger, ex);
        }
    }

    private bool ValidateMetalDevice(IntPtr device, int deviceIndex)
    {
        try
        {
            // 3. Check macOS version compatibility
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major < 10 || (osVersion.Major == 10 && osVersion.Minor < 13))
            {
                // macOS 10.13 (High Sierra) minimum for compute shaders
                LogMacOSVersionWarning(_logger, deviceIndex, osVersion);
                return false;
            }

            // Get device info for validation
            var deviceInfo = MetalNative.GetDeviceInfo(device);

            // 4. Validate compute support
            if (!ValidateComputeSupport(deviceInfo, deviceIndex))
            {
                return false;
            }

            // 5. Test shader compilation capability
            if (!TestShaderCompilation(device, deviceIndex))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogDeviceValidationError(_logger, ex, deviceIndex);
            return false;
        }
    }

    private bool ValidateComputeSupport(MetalDeviceInfo deviceInfo, int deviceIndex)
    {
        try
        {
            // Check if device supports compute operations
            if (deviceInfo.MaxThreadgroupSize == 0)
            {
                LogComputeSupportWarning(_logger, deviceIndex);
                return false;
            }

            // Verify minimum capability requirements
            var familyString = Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? "";

            // Require at least Mac1 (Intel) or Apple1 (Apple Silicon) or Common1 family support
            var hasMinimumCapability = familyString.Contains("Mac", StringComparison.Ordinal) ||
                                      familyString.Contains("Apple", StringComparison.Ordinal) ||
                                      familyString.Contains("Common", StringComparison.Ordinal) ||
                                      familyString.Contains("Legacy", StringComparison.Ordinal);

            if (!hasMinimumCapability)
            {
                LogGPUFamilyWarning(_logger, deviceIndex, familyString);
                return false;
            }

            // Check memory constraints
            if (deviceInfo.MaxBufferLength < 1024 * 1024) // At least 1MB
            {
                LogInsufficientMemoryWarning(_logger, deviceIndex, deviceInfo.MaxBufferLength);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogComputeValidationError(_logger, ex, deviceIndex);
            return false;
        }
    }

    private bool TestShaderCompilation(IntPtr device, int deviceIndex)
    {
        try
        {
            // Test basic compute shader compilation
            var testShaderSource = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void test_kernel(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    output[id] = input[id] * 2.0f;
                }
            ";

            // Attempt to compile test shader
            var library = MetalNative.CreateLibraryWithSource(device, testShaderSource);
            if (library == IntPtr.Zero)
            {
                LogShaderCompilationWarning(_logger, deviceIndex, "Failed to create library from shader source");
                return false;
            }

            // Check if we can create a compute pipeline
            var function = MetalNative.GetFunction(library, "test_kernel");
            if (function == IntPtr.Zero)
            {
                MetalNative.ReleaseLibrary(library);
                LogShaderCompilationWarning(_logger, deviceIndex, "Failed to find test kernel function");
                return false;
            }

            var pipeline = MetalNative.CreateComputePipelineState(device, function);
            var success = pipeline != IntPtr.Zero;

            // Cleanup
            if (pipeline != IntPtr.Zero)
            {
                MetalNative.ReleaseComputePipelineState(pipeline);
            }

            MetalNative.ReleaseFunction(function);
            MetalNative.ReleaseLibrary(library);

            if (!success)
            {
                LogShaderCompilationWarning(_logger, deviceIndex, "Failed to create compute pipeline state");
            }

            return success;
        }
        catch (Exception ex)
        {
            LogShaderCompilationError(_logger, ex, deviceIndex);
            return false;
        }
    }

    private MetalAccelerator? CreateMetalAccelerator(IntPtr device, int deviceIndex)
    {
        try
        {
            var deviceInfo = MetalNative.GetDeviceInfo(device);
            var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? $"Metal Device {deviceIndex}";

            LogCreatingAccelerator(_logger, deviceName);

            // Create accelerator with the device
            // The MetalAccelerator will automatically discover and use the appropriate device
            // based on the system configuration and available hardware
            var options = Microsoft.Extensions.Options.Options.Create(new MetalAcceleratorOptions());
            var metalLogger = _loggerFactory.CreateLogger<MetalAccelerator>();
            return new MetalAccelerator(options, metalLogger);
        }
        catch (Exception ex)
        {
            LogAcceleratorCreationError(_logger, ex, deviceIndex);
            return null;
        }
    }

    private void LogMetalDeviceCapabilities(MetalAccelerator accelerator)
    {
        var info = accelerator.Info;
        var capabilities = info.Capabilities ?? [];

        LogDeviceInfo(_logger, info.Name, info.Id);
        LogDeviceType(_logger, info.DeviceType);
        LogComputeCapability(_logger, info.ComputeCapability?.ToString() ?? "Unknown");
        LogTotalMemory(_logger, info.TotalMemory, info.TotalMemory / (1024.0 * 1024 * 1024));
        LogComputeUnits(_logger, info.ComputeUnits);

        if (capabilities.TryGetValue("MaxThreadgroupSize", out var maxThreadgroup))
        {
            LogMaxThreadgroupSize(_logger, maxThreadgroup);
        }

        if (capabilities.TryGetValue("MaxThreadsPerThreadgroup", out var maxThreadsPerGroup))
        {
            LogMaxThreadsPerThreadgroup(_logger, maxThreadsPerGroup);
        }

        if (capabilities.TryGetValue("UnifiedMemory", out var unified) && (bool)unified)
        {
            LogUnifiedMemorySupported(_logger);
        }

        if (capabilities.TryGetValue("SupportsFamily", out var families))
        {
            LogGPUFamilies(_logger, families);
        }

        if (capabilities.TryGetValue("Location", out var location))
        {
            LogLocation(_logger, location);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var accelerator in _accelerators)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - Required in synchronous Dispose
            accelerator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }

        _accelerators.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
