// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.OpenCL.DeviceManagement;
using DotCompute.Backends.OpenCL.Kernels;
using DotCompute.Backends.OpenCL.Memory;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Backends.OpenCL;

/// <summary>
/// OpenCL implementation of the compute accelerator interface.
/// Provides cross-platform GPU acceleration using OpenCL runtime.
/// </summary>
public sealed class OpenCLAccelerator : IAccelerator
{
    private readonly ILogger<OpenCLAccelerator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OpenCLDeviceManager _deviceManager;
    private readonly object _lock = new();

    private OpenCLContext? _context;
    private OpenCLDeviceInfo? _selectedDevice;
    private OpenCLMemoryManager? _memoryManager;
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
    /// Gets the accelerator information.
    /// </summary>
    public AcceleratorInfo Info => new AcceleratorInfo
    {
        Id = Id.ToString(),
        Name = Name,
        DeviceType = Type.ToString(),
        Vendor = _selectedDevice?.Vendor ?? "Unknown",
        DriverVersion = _selectedDevice?.DriverVersion ?? "Unknown",
        TotalMemory = (long)(_selectedDevice?.GlobalMemorySize ?? 0),
        AvailableMemory = (long)(_selectedDevice?.GlobalMemorySize ?? 0),
        MaxMemoryAllocationSize = (long)(_selectedDevice?.MaxMemoryAllocationSize ?? 0),
        LocalMemorySize = (long)(_selectedDevice?.LocalMemorySize ?? 0),
        MaxThreadsPerBlock = (int)(_selectedDevice?.MaxWorkGroupSize ?? 0),
        IsUnifiedMemory = false
    };

    /// <summary>
    /// Gets the memory manager for this accelerator.
    /// </summary>
    public IUnifiedMemoryManager Memory
    {
        get
        {
            ThrowIfDisposed();
            if (_memoryManager == null)
                throw new InvalidOperationException("Accelerator not initialized. Call InitializeAsync first.");
            return _memoryManager;
        }
    }

    /// <summary>
    /// Gets the accelerator context.
    /// </summary>
    public AcceleratorContext Context => new AcceleratorContext();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLAccelerator"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    public OpenCLAccelerator(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<OpenCLAccelerator>();
        _deviceManager = new OpenCLDeviceManager(_loggerFactory.CreateLogger<OpenCLDeviceManager>());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLAccelerator"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLAccelerator(ILogger<OpenCLAccelerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create a simple logger factory from the provided logger
        _loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(logger)));
        
        _deviceManager = new OpenCLDeviceManager(_loggerFactory.CreateLogger<OpenCLDeviceManager>());
    }

    /// <summary>
    /// Initializes a new instance with a specific device.
    /// </summary>
    /// <param name="device">The OpenCL device to use.</param>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    public OpenCLAccelerator(OpenCLDeviceInfo device, ILoggerFactory loggerFactory)
        : this(loggerFactory)
    {
        _selectedDevice = device ?? throw new ArgumentNullException(nameof(device));
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
            _logger.LogDebugMessage("OpenCL accelerator already initialized");
            return;
        }

        _logger.LogInfoMessage("Initializing OpenCL accelerator");

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
                    _context = new OpenCLContext(_selectedDevice, _loggerFactory.CreateLogger<OpenCLContext>());
                    
                    // Create memory manager
                    _memoryManager = new OpenCLMemoryManager(this, _context, _loggerFactory.CreateLogger<OpenCLMemoryManager>());
                    
                    _logger.LogInfoMessage($"OpenCL accelerator initialized successfully with device: {_selectedDevice.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, $"Failed to initialize OpenCL context for device: {_selectedDevice.Name}");
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
    public async Task<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        nuint elementCount,
        MemoryOptions? options = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);

        _logger.LogDebugMessage($"Allocating OpenCL buffer: type={typeof(T).Name}, elements={elementCount}");

        // Convert nuint to int for the memory manager
        var count = (int)elementCount;
        if (count <= 0)
            throw new ArgumentException("Element count must be positive", nameof(elementCount));

        return await Memory.AllocateAsync<T>(count, options ?? MemoryOptions.None, cancellationToken);
    }

    /// <summary>
    /// Compiles a kernel from source code.
    /// </summary>
    /// <param name="definition">Kernel definition containing source code and entry point.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A compiled kernel ready for execution.</returns>
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(definition?.Source))
            throw new ArgumentException("Kernel source cannot be null or empty", nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.EntryPoint))
            throw new ArgumentException("Entry point cannot be null or empty", nameof(definition));

        _logger.LogDebugMessage($"Compiling OpenCL kernel: {definition.EntryPoint} ({definition.Source.Length} chars)");

        return await Task.Run(() =>
        {
            var buildOptions = DetermineBuildOptions(options);
            
            // Create program from source
            var program = _context!.CreateProgramFromSource(definition.Source);

            try
            {
                // Build program
                _context.BuildProgram(program, buildOptions);

                // Create kernel
                var kernel = _context.CreateKernel(program, definition.EntryPoint);

                return new OpenCLCompiledKernel(
                    _context,
                    program,
                    kernel,
                    definition.EntryPoint,
                    _loggerFactory.CreateLogger<OpenCLCompiledKernel>());
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
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_context == null)
        {
            _logger.LogDebugMessage("Synchronize called on uninitialized accelerator");
            return ValueTask.CompletedTask;
        }

        return new ValueTask(Task.Run(() =>
        {
            _context.Finish(); // Synchronous wait for all operations
            _logger.LogTrace("OpenCL accelerator synchronized");
        }, cancellationToken));
    }


    /// <summary>
    /// Determines memory flags based on allocation options.
    /// </summary>
    private static MemoryFlags DetermineMemoryFlags(MemoryOptions? options)
    {
        if (options == null)
            return MemoryFlags.ReadWrite;

        var flags = MemoryFlags.ReadWrite;

        // Map common options to OpenCL flags - for now just use ReadWrite
        // In a full implementation, this would map specific flags
        
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
        if (options.EnableDebugInfo)
        {
            buildOptions.Add("-g");
        }

        // Note: CustomOptions not available in base CompilationOptions
        // In a full implementation, this could be extended through inheritance

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

            _logger.LogInfoMessage("Disposing OpenCL accelerator: {Name}");

            try
            {
                _memoryManager?.Dispose();
                _context?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred while disposing OpenCL resources");
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