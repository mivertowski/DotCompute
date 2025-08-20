using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Tests.Implementations.Memory;
using FluentAssertions;

using DotCompute.Abstractions.Kernels;
namespace DotCompute.Tests.Implementations.Accelerators;


/// <summary>
/// Test implementation of IAcceleratorManager for testing without GPU hardware.
/// </summary>
public sealed class TestAcceleratorManager : IAcceleratorManager
{
    private readonly List<IAccelerator> _accelerators;
    private readonly List<IAcceleratorProvider> _providers;
    private readonly Dictionary<IAccelerator, AcceleratorContext> _contexts;
    private IAccelerator? _defaultAccelerator;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAcceleratorManager"/> class.
    /// </summary>
    public TestAcceleratorManager()
    {
        _accelerators = [];
        _providers = [];
        _contexts = [];

        // Register default CPU provider
        RegisterProvider(new TestCpuAcceleratorProvider());
    }

    /// <summary>
    /// Gets the default accelerator instance.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Accelerator manager not initialized. Call InitializeAsync first.
    /// or
    /// No default accelerator available.
    /// </exception>
    public IAccelerator Default
    {
        get
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
            }

            if (_defaultAccelerator == null)
            {
                throw new InvalidOperationException("No default accelerator available.");
            }

            return _defaultAccelerator;
        }
    }

    /// <summary>
    /// Gets all available accelerators.
    /// </summary>
    public IReadOnlyList<IAccelerator> AvailableAccelerators => _accelerators.AsReadOnly();

    /// <summary>
    /// Gets the number of available accelerators.
    /// </summary>
    public int Count => _accelerators.Count;

    /// <summary>
    /// Discovers and initializes all available accelerators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task representing the async operation.
    /// </returns>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        // Discover accelerators from all providers
        foreach (var provider in _providers)
        {
            var accelerators = await provider.DiscoverAsync(cancellationToken);
            _accelerators.AddRange(accelerators);
        }

        // If no accelerators found, create a default CPU accelerator
        if (_accelerators.Count == 0)
        {
            var cpuAccelerator = new TestCpuAccelerator("Default CPU Accelerator");
            _accelerators.Add(cpuAccelerator);
        }

        // Set the first accelerator as default
        _defaultAccelerator = _accelerators.FirstOrDefault();
        _initialized = true;
    }

    /// <summary>
    /// Gets an accelerator by index.
    /// </summary>
    /// <param name="index">The index of the accelerator.</param>
    /// <returns>
    /// The accelerator at the specified index.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">index</exception>
    public IAccelerator GetAccelerator(int index)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        if (index < 0 || index >= _accelerators.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _accelerators[index];
    }

    /// <summary>
    /// Gets an accelerator by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the accelerator.</param>
    /// <returns>
    /// The accelerator with the specified ID, or null if not found.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized.</exception>
    public IAccelerator? GetAcceleratorById(string id)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        return _accelerators.FirstOrDefault(a => a.Info.Id == id);
    }

    /// <summary>
    /// Gets accelerators of a specific type.
    /// </summary>
    /// <param name="type">The type of accelerators to get.</param>
    /// <returns>
    /// A list of accelerators of the specified type.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized.</exception>
    public IEnumerable<IAccelerator> GetAcceleratorsByType(AcceleratorType type)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        return _accelerators.Where(a => a.Info.DeviceType == type.ToString());
    }

    /// <summary>
    /// Selects the best accelerator based on the given criteria.
    /// </summary>
    /// <param name="criteria">The selection criteria.</param>
    /// <returns>
    /// The best matching accelerator, or null if none match.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized.</exception>
    public IAccelerator? SelectBest(AcceleratorSelectionCriteria criteria)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        var candidates = _accelerators.AsEnumerable();

        // Filter by minimum memory
        if (criteria.MinimumMemory.HasValue)
        {
            candidates = candidates.Where(a => a.Info.TotalMemory >= criteria.MinimumMemory.Value);
        }

        // Filter by preferred type
        if (criteria.PreferredType.HasValue)
        {
            var preferredTypeStr = criteria.PreferredType.Value.ToString().ToUpperInvariant();
            candidates = candidates.Where(a => string.Equals(a.Info.DeviceType, preferredTypeStr, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by compute capability
        if (criteria.MinimumComputeCapability != null)
        {
            candidates = candidates.Where(a =>
                a.Info.ComputeCapability != null &&
                a.Info.ComputeCapability >= criteria.MinimumComputeCapability);
        }

        // Apply custom scorer if provided
        if (criteria.CustomScorer != null)
        {
            return candidates.OrderByDescending(criteria.CustomScorer).FirstOrDefault();
        }

        // Default scoring: prefer higher memory and compute units
        return candidates
            .OrderByDescending(a => a.Info.TotalMemory)
            .ThenByDescending(a => a.Info.ComputeUnits)
            .FirstOrDefault();
    }

    /// <summary>
    /// Creates a new accelerator context for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to create a context for.</param>
    /// <returns>
    /// A new accelerator context.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized.</exception>
    /// <exception cref="System.ArgumentException">Accelerator not managed by this manager. - accelerator</exception>
    public AcceleratorContext CreateContext(IAccelerator accelerator)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        if (!_accelerators.Contains(accelerator))
        {
            throw new ArgumentException("Accelerator not managed by this manager.", nameof(accelerator));
        }

        if (!_contexts.TryGetValue(accelerator, out var context))
        {
            // Create a context with a fake handle and device ID
            context = new AcceleratorContext(new IntPtr(accelerator.GetHashCode()), 0);
            _contexts[accelerator] = context;
        }

        return context;
    }

    /// <summary>
    /// Registers a custom accelerator provider.
    /// </summary>
    /// <param name="provider">The accelerator provider to register.</param>
    /// <exception cref="System.ArgumentNullException"></exception>
    /// <exception cref="System.InvalidOperationException">Cannot register providers after initialization.</exception>
    public void RegisterProvider(IAcceleratorProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (_initialized)
        {
            throw new InvalidOperationException("Cannot register providers after initialization.");
        }

        _providers.Add(provider);
    }

    /// <summary>
    /// Refreshes the list of available accelerators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task representing the async operation.
    /// </returns>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Clear existing accelerators
        foreach (var accelerator in _accelerators)
        {
            _ = _contexts.Remove(accelerator);
            await accelerator.DisposeAsync();
        }
        _accelerators.Clear();
        _defaultAccelerator = null;
        _initialized = false;

        // Re-initialize
        await InitializeAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all available accelerators asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task representing the async operation that returns all available accelerators.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized. Call InitializeAsync first.</exception>
    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
        }
        return Task.FromResult<IEnumerable<IAccelerator>>(_accelerators.AsEnumerable());
    }

    /// <summary>
    /// Gets accelerators of a specific type asynchronously.
    /// </summary>
    /// <param name="type">The type of accelerators to get.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task representing the async operation that returns accelerators of the specified type.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized. Call InitializeAsync first.</exception>
    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
        }
        var filtered = _accelerators.Where(a => a.Type == type);
        return Task.FromResult<IEnumerable<IAccelerator>>(filtered);
    }

    /// <summary>
    /// Gets the best accelerator for the specified type asynchronously.
    /// </summary>
    /// <param name="type">The preferred accelerator type, or null for any type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task representing the async operation that returns the best matching accelerator, or null if none match.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Accelerator manager not initialized. Call InitializeAsync first.</exception>
    public Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? type = null, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
        }

        IAccelerator? best = null;
        if (type.HasValue)
        {
            best = _accelerators.FirstOrDefault(a => a.Type == type.Value);
        }

        // Fallback to any accelerator if no preferred type match or no preferred type specified
        best ??= _accelerators.FirstOrDefault();

        return Task.FromResult(best);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Dispose all contexts
        _contexts.Clear();

        // Dispose all accelerators
        foreach (var accelerator in _accelerators)
        {
            await accelerator.DisposeAsync();
        }
        _accelerators.Clear();

        // Dispose _defaultAccelerator if it's not null and not in _accelerators
        if (_defaultAccelerator != null && !_accelerators.Contains(_defaultAccelerator))
        {
            await _defaultAccelerator.DisposeAsync();
        }
        _defaultAccelerator = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test CPU accelerator provider for testing.
/// </summary>
public sealed class TestCpuAcceleratorProvider : IAcceleratorProvider
{
    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string Name => "Test CPU Provider";

    /// <summary>
    /// Gets the types of accelerators this provider can create.
    /// </summary>
    public AcceleratorType[] SupportedTypes => [AcceleratorType.CPU];

    /// <summary>
    /// Discovers available accelerators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of discovered accelerators.
    /// </returns>
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var accelerators = new List<IAccelerator>();

        // Create CPU accelerators based on processor count
        var processorCount = Environment.ProcessorCount;

        // Create one accelerator per physical processor(simplified)
        var acceleratorCount = Math.Max(1, processorCount / 4);

        for (var i = 0; i < acceleratorCount; i++)
        {
            var accelerator = new TestCpuAccelerator($"CPU Accelerator {i}");
            accelerators.Add(accelerator);
        }

        return ValueTask.FromResult<IEnumerable<IAccelerator>>(accelerators);
    }

    /// <summary>
    /// Creates an accelerator instance.
    /// </summary>
    /// <param name="info">The accelerator information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The created accelerator instance.
    /// </returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
#pragma warning disable CA2000 // Dispose objects before losing scope - caller is responsible for disposal
        var accelerator = new TestCpuAccelerator(info.Name);
#pragma warning restore CA2000
        return ValueTask.FromResult<IAccelerator>(accelerator);
    }
}

/// <summary>
/// Test GPU accelerator provider for simulating GPU devices.
/// </summary>
public sealed class TestGpuAcceleratorProvider(int deviceCount = 1) : IAcceleratorProvider
{
    private readonly int _deviceCount = deviceCount;

    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string Name => "Test GPU Provider";

    /// <summary>
    /// Gets the types of accelerators this provider can create.
    /// </summary>
    public AcceleratorType[] SupportedTypes => [AcceleratorType.CUDA, AcceleratorType.OpenCL];

    /// <summary>
    /// Discovers available accelerators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of discovered accelerators.
    /// </returns>
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var accelerators = new List<IAccelerator>();

        for (var i = 0; i < _deviceCount; i++)
        {
            // Create simulated GPU accelerator
            var accelerator = new TestGpuAccelerator($"GPU {i}", i);
            accelerators.Add(accelerator);
        }

        return ValueTask.FromResult<IEnumerable<IAccelerator>>(accelerators);
    }

    /// <summary>
    /// Creates an accelerator instance.
    /// </summary>
    /// <param name="info">The accelerator information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The created accelerator instance.
    /// </returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
#pragma warning disable CA2000 // Dispose objects before losing scope - caller is responsible for disposal
        var accelerator = new TestGpuAccelerator(info.Name, 0);
#pragma warning restore CA2000
        return ValueTask.FromResult<IAccelerator>(accelerator);
    }
}

/// <summary>
/// Test GPU accelerator implementation for simulating GPU behavior.
/// </summary>
public sealed class TestGpuAccelerator : IAccelerator
{
    /// <summary>
    /// Gets the accelerator type.
    /// </summary>
    public AcceleratorType Type => AcceleratorType.CUDA;

    /// <summary>
    /// Gets the accelerator context.
    /// </summary>
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    /// <summary>
    /// The memory manager
    /// </summary>
    private readonly IMemoryManager _memoryManager;

    /// <summary>
    /// The compiled kernels
    /// </summary>
    private readonly ConcurrentDictionary<string, TestCompiledKernel> _compiledKernels;

    /// <summary>
    /// The disposed
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestGpuAccelerator"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="deviceId">The device identifier.</param>
    public TestGpuAccelerator(string name, int deviceId)
    {
        _memoryManager = new TestMemoryManager();
        _compiledKernels = new ConcurrentDictionary<string, TestCompiledKernel>();

        // Set GPU-specific details
        Info = new AcceleratorInfo
        {
            Id = $"test_gpu_{deviceId}_{Guid.NewGuid():N}",
            Name = name,
            DeviceType = "CUDA",
            Vendor = "Test GPU Vendor",
            DriverVersion = "525.125.06",
            TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
            AvailableMemory = 7L * 1024 * 1024 * 1024, // 7GB available
            MaxThreadsPerBlock = 1024,
            ComputeUnits = 68, // Simulated SMs
            MaxClockFrequency = 1770, // 1.77 GHz
            IsUnifiedMemory = false,
            ComputeCapability = new Version(8, 6), // Simulated compute capability
            MaxSharedMemoryPerBlock = 49152, // 48KB
            Capabilities = new Dictionary<string, object>
            {
                ["TensorCores"] = true,
                ["RayTracing"] = true,
                ["AsyncCopy"] = true
            }
        };
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public AcceleratorInfo Info { get; }

    /// <summary>
    /// Gets memory manager for this accelerator.
    /// </summary>
    public IMemoryManager Memory => _memoryManager;

    /// <summary>
    /// Compiles a kernel for execution.
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await Task.Delay(5, cancellationToken); // Simulate faster GPU compilation

        var kernel = new TestCompiledKernel(
            definition.Name,
            System.Text.Encoding.UTF8.GetBytes(definition.Code ?? string.Empty),
            options ?? new CompilationOptions());

        _compiledKernels[definition.Name] = kernel;
        return kernel;
    }

    /// <summary>
    /// Synchronizes all pending operations.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => await Task.Yield(); // Simulate GPU synchronization

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await SynchronizeAsync();
            if (_memoryManager is IDisposable disposable)
            {
                disposable.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
