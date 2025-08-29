// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.OpenCL.DeviceManagement;
using DotCompute.Backends.OpenCL.Kernels;
using DotCompute.Backends.OpenCL.Memory;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL;

/// <summary>
/// OpenCL implementation of the compute accelerator interface.
/// Provides cross-platform GPU acceleration using OpenCL runtime.
/// </summary>
public sealed class OpenCLAccelerator : IAccelerator
{
    private readonly ILogger<OpenCLAccelerator> _logger;
    private readonly OpenCLDeviceManager _deviceManager;
    private readonly object _lock = new();

    private OpenCLContext? _context;
    private OpenCLDeviceInfo? _selectedDevice;
    private bool _disposed;

    /// <summary>
    /// Gets the unique identifier for this accelerator instance.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the accelerator name.
    /// </summary>
    public string Name => $"OpenCL Accelerator ({_selectedDevice?.Name ?? "Not Initialized"})";

    /// <summary>
    /// Gets the accelerator type.
    /// </summary>
    public AcceleratorType Type => AcceleratorType.OpenCL;

    /// <summary>
    /// Gets whether the accelerator is available for use.
    /// </summary>
    public bool IsAvailable => _context != null && !_context.IsDisposed && !_disposed;

    /// <summary>
    /// Gets the device information for the selected device.
    /// </summary>
    public OpenCLDeviceInfo? DeviceInfo => _selectedDevice;

    /// <summary>
    /// Gets whether the accelerator has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLAccelerator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLAccelerator(ILogger<OpenCLAccelerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deviceManager = new OpenCLDeviceManager(logger.CreateLogger<OpenCLDeviceManager>());
    }

    /// <summary>
    /// Initializes a new instance with a specific device.
    /// </summary>
    /// <param name="device">The OpenCL device to use.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLAccelerator(OpenCLDeviceInfo device, ILogger<OpenCLAccelerator> logger)
        : this(logger)
    {
        _selectedDevice = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>
    /// Initializes the accelerator and selects the best available device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OpenCLAccelerator));

        if (_context != null)
        {
            _logger.LogDebug("OpenCL accelerator already initialized");
            return;
        }

        _logger.LogInformation("Initializing OpenCL accelerator");

        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_context != null) return; // Double-check locking

                // Select device if not already selected
                if (_selectedDevice == null)
                {
                    _selectedDevice = _deviceManager.GetBestDevice();
                    if (_selectedDevice == null)
                    {
                        throw new InvalidOperationException("No suitable OpenCL devices found");
                    }
                }

                // Create OpenCL context
                try
                {
                    _context = new OpenCLContext(_selectedDevice, _logger.CreateLogger<OpenCLContext>());
                    _logger.LogInformation("OpenCL accelerator initialized successfully with device: {DeviceName}",
                        _selectedDevice.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize OpenCL context for device: {DeviceName}",
                        _selectedDevice.Name);
                    throw;
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Creates a memory buffer of the specified type and size.
    /// </summary>
    /// <typeparam name="T">The element type for the buffer.</typeparam>
    /// <param name="elementCount">Number of elements in the buffer.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A new memory buffer.</returns>
    public async Task<IMemoryBuffer<T>> AllocateAsync<T>(
        nuint elementCount,
        MemoryAllocationOptions? options = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);

        _logger.LogDebug("Allocating OpenCL buffer: type={Type}, elements={Count}",
            typeof(T).Name, elementCount);

        return await Task.Run(() =>
        {
            var flags = DetermineMemoryFlags(options);
            return new OpenCLMemoryBuffer<T>(
                _context!,
                elementCount,
                flags,
                _logger.CreateLogger<OpenCLMemoryBuffer<T>>());
        }, cancellationToken);
    }

    /// <summary>
    /// Compiles a kernel from source code.
    /// </summary>
    /// <param name="source">OpenCL kernel source code.</param>
    /// <param name="entryPoint">Name of the kernel function.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A compiled kernel ready for execution.</returns>
    public async Task<IAccelerator.ICompiledKernel> CompileKernelAsync(
        string source,
        string entryPoint,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Kernel source cannot be null or empty", nameof(source));

        if (string.IsNullOrWhiteSpace(entryPoint))
            throw new ArgumentException("Entry point cannot be null or empty", nameof(entryPoint));

        _logger.LogDebug("Compiling OpenCL kernel: {EntryPoint} ({SourceLength} chars)",
            entryPoint, source.Length);

        return await Task.Run(() =>
        {
            var buildOptions = DetermineBuildOptions(options);
            
            // Create program from source
            var program = _context!.CreateProgramFromSource(source);

            try
            {
                // Build program
                _context.BuildProgram(program, buildOptions);

                // Create kernel
                var kernel = _context.CreateKernel(program, entryPoint);

                return new OpenCLCompiledKernel(
                    _context,
                    program,
                    kernel,
                    entryPoint,
                    _logger.CreateLogger<OpenCLCompiledKernel>());
            }
            catch
            {
                // Clean up program on failure
                OpenCLContext.ReleaseObject(program.Handle, OpenCLRuntime.clReleaseProgram, "program");
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Synchronizes all operations on the accelerator.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_context == null)
        {
            _logger.LogDebug("Synchronize called on uninitialized accelerator");
            return;
        }

        await Task.Run(() =>
        {
            _context.Finish(); // Synchronous wait for all operations
            _logger.LogTrace("OpenCL accelerator synchronized");
        }, cancellationToken);
    }

    /// <summary>
    /// Gets accelerator capabilities and properties.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Accelerator capabilities information.</returns>
    public async Task<AcceleratorCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);

        return await Task.FromResult(new AcceleratorCapabilities
        {
            Name = Name,
            Type = Type,
            MaxMemoryAllocation = _selectedDevice!.MaxMemoryAllocationSize,
            GlobalMemorySize = _selectedDevice.GlobalMemorySize,
            LocalMemorySize = _selectedDevice.LocalMemorySize,
            MaxWorkGroupSize = (uint)_selectedDevice.MaxWorkGroupSize,
            MaxComputeUnits = _selectedDevice.MaxComputeUnits,
            SupportsDoublePrecision = _selectedDevice.SupportsDoublePrecision,
            SupportsImages = _selectedDevice.ImageSupport,
            Extensions = _selectedDevice.Extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            DriverVersion = _selectedDevice.DriverVersion,
            OpenCLVersion = _selectedDevice.OpenCLVersion
        });
    }

    /// <summary>
    /// Determines memory flags based on allocation options.
    /// </summary>
    private static MemoryFlags DetermineMemoryFlags(MemoryAllocationOptions? options)
    {
        if (options == null)
            return MemoryFlags.ReadWrite;

        var flags = MemoryFlags.ReadWrite;

        // Map common options to OpenCL flags
        switch (options.AccessPattern)
        {
            case MemoryAccessPattern.ReadOnly:
                flags = MemoryFlags.ReadOnly;
                break;
            case MemoryAccessPattern.WriteOnly:
                flags = MemoryFlags.WriteOnly;
                break;
            case MemoryAccessPattern.ReadWrite:
                flags = MemoryFlags.ReadWrite;
                break;
        }

        // Add additional flags based on options
        if (options.UseHostMemory)
        {
            flags |= MemoryFlags.UseHostPtr;
        }

        return flags;
    }

    /// <summary>
    /// Determines build options for kernel compilation.
    /// </summary>
    private static string? DetermineBuildOptions(CompilationOptions? options)
    {
        if (options == null)
            return null;

        var buildOptions = new List<string>();

        // Map optimization level to OpenCL build options
        switch (options.OptimizationLevel)
        {
            case OptimizationLevel.None:
                buildOptions.Add("-cl-opt-disable");
                break;
            case OptimizationLevel.Minimal:
            case OptimizationLevel.Default:
                // Default optimization
                break;
            case OptimizationLevel.Aggressive:
            case OptimizationLevel.Maximum:
                buildOptions.Add("-cl-fast-relaxed-math");
                break;
        }

        // Add debug information if requested
        if (options.IncludeDebugInformation)
        {
            buildOptions.Add("-g");
        }

        // Add custom options
        if (!string.IsNullOrEmpty(options.CustomOptions))
        {
            buildOptions.Add(options.CustomOptions);
        }

        return buildOptions.Count > 0 ? string.Join(" ", buildOptions) : null;
    }

    /// <summary>
    /// Ensures the accelerator is initialized.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_context == null)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Throws if the accelerator has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OpenCLAccelerator));
    }

    /// <summary>
    /// Disposes the OpenCL accelerator and associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            _logger.LogInformation("Disposing OpenCL accelerator: {AcceleratorName}", Name);

            try
            {
                _context?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred while disposing OpenCL context");
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the OpenCL accelerator.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Task.Run(Dispose);
    }
}