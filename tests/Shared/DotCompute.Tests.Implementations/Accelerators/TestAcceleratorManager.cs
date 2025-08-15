using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Tests.Shared.Memory;
using FluentAssertions;

namespace DotCompute.Tests.Shared.Accelerators;

/// <summary>
/// Test implementation of IAcceleratorManager for testing without GPU hardware.
/// </summary>
public class TestAcceleratorManager : IAcceleratorManager
{
    private readonly List<IAccelerator> _accelerators;
    private readonly List<IAcceleratorProvider> _providers;
    private readonly Dictionary<IAccelerator, AcceleratorContext> _contexts;
    private IAccelerator? _defaultAccelerator;
    private bool _initialized;
    private bool _disposed;

    public TestAcceleratorManager()
    {
        _accelerators = new List<IAccelerator>();
        _providers = new List<IAcceleratorProvider>();
        _contexts = new Dictionary<IAccelerator, AcceleratorContext>();
        
        // Register default CPU provider
        RegisterProvider(new TestCpuAcceleratorProvider());
    }

    public IAccelerator Default 
    { 
        get 
        {
            if(!_initialized)
            {
                throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
            }
            
            if(_defaultAccelerator == null)
            {
                throw new InvalidOperationException("No default accelerator available.");
            }
            
            return _defaultAccelerator;
        }
    }

    public IReadOnlyList<IAccelerator> AvailableAccelerators => _accelerators.AsReadOnly();

    public int Count => _accelerators.Count;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if(_initialized)
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
        if(_accelerators.Count == 0)
        {
            var cpuAccelerator = new TestCpuAccelerator("Default CPU Accelerator");
            _accelerators.Add(cpuAccelerator);
        }

        // Set the first accelerator as default
        _defaultAccelerator = _accelerators.FirstOrDefault();
        _initialized = true;
    }

    public IAccelerator GetAccelerator(int index)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        if(index < 0 || index >= _accelerators.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _accelerators[index];
    }

    public IAccelerator? GetAcceleratorById(string id)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        return _accelerators.FirstOrDefault(a => a.Info.Id == id);
    }

    public IEnumerable<IAccelerator> GetAcceleratorsByType(AcceleratorType type)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        return _accelerators.Where(a => a.Info.DeviceType == type.ToString());
    }

    public IAccelerator? SelectBest(AcceleratorSelectionCriteria criteria)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        var candidates = _accelerators.AsEnumerable();

        // Filter by minimum memory
        if(criteria.MinimumMemory.HasValue)
        {
            candidates = candidates.Where(a => a.Info.TotalMemory >= criteria.MinimumMemory.Value);
        }

        // Filter by preferred type
        if(criteria.PreferredType.HasValue)
        {
            var preferredTypeStr = criteria.PreferredType.Value.ToString();
            candidates = candidates.Where(a => a.Info.DeviceType == preferredTypeStr);
        }

        // Filter by compute capability
        if(criteria.MinimumComputeCapability != null)
        {
            candidates = candidates.Where(a => 
                a.Info.ComputeCapability != null && 
                a.Info.ComputeCapability >= criteria.MinimumComputeCapability);
        }

        // Apply custom scorer if provided
        if(criteria.CustomScorer != null)
        {
            return candidates.OrderByDescending(criteria.CustomScorer).FirstOrDefault();
        }

        // Default scoring: prefer higher memory and compute units
        return candidates
            .OrderByDescending(a => a.Info.TotalMemory)
            .ThenByDescending(a => a.Info.ComputeUnits)
            .FirstOrDefault();
    }

    public AcceleratorContext CreateContext(IAccelerator accelerator)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized.");
        }

        if(!_accelerators.Contains(accelerator))
        {
            throw new ArgumentException("Accelerator not managed by this manager.", nameof(accelerator));
        }

        if(!_contexts.TryGetValue(accelerator, out var context))
        {
            // Create a context with a fake handle and device ID
            context = new AcceleratorContext(new IntPtr(accelerator.GetHashCode()), 0);
            _contexts[accelerator] = context;
        }

        return context;
    }

    public void RegisterProvider(IAcceleratorProvider provider)
    {
        if(provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if(_initialized)
        {
            throw new InvalidOperationException("Cannot register providers after initialization.");
        }

        _providers.Add(provider);
    }

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Clear existing accelerators
        foreach (var accelerator in _accelerators)
        {
            if(_contexts.ContainsKey(accelerator))
            {
                // AcceleratorContext is a struct, no disposal needed
                _contexts.Remove(accelerator);
            }
            await accelerator.DisposeAsync();
        }
        _accelerators.Clear();
        _defaultAccelerator = null;
        _initialized = false;

        // Re-initialize
        await InitializeAsync(cancellationToken);
    }

    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(CancellationToken cancellationToken = default)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
        }
        return Task.FromResult<IEnumerable<IAccelerator>>(_accelerators.AsEnumerable());
    }

    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
        }
        var filtered = _accelerators.Where(a => a.Type == type);
        return Task.FromResult<IEnumerable<IAccelerator>>(filtered);
    }

    public Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? preferredType = null, CancellationToken cancellationToken = default)
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("Accelerator manager not initialized. Call InitializeAsync first.");
        }
        
        IAccelerator? best = null;
        if(preferredType.HasValue)
        {
            best = _accelerators.FirstOrDefault(a => a.Type == preferredType.Value);
        }
        
        // Fallback to any accelerator if no preferred type match or no preferred type specified
        best ??= _accelerators.FirstOrDefault();
        
        return Task.FromResult(best);
    }

    public async ValueTask DisposeAsync()
    {
        if(_disposed)
        {
            return;
        }

        // Dispose all contexts
        foreach (var context in _contexts.Values)
        {
            // AcceleratorContext is a struct, no disposal needed
        }
        _contexts.Clear();

        // Dispose all accelerators
        foreach (var accelerator in _accelerators)
        {
            await accelerator.DisposeAsync();
        }
        _accelerators.Clear();

        _disposed = true;
    }
}

/// <summary>
/// Test CPU accelerator provider for testing.
/// </summary>
public class TestCpuAcceleratorProvider : IAcceleratorProvider
{
    public string Name => "Test CPU Provider";

    public AcceleratorType[] SupportedTypes => new[] { AcceleratorType.CPU };

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var accelerators = new List<IAccelerator>();
        
        // Create CPU accelerators based on processor count
        var processorCount = Environment.ProcessorCount;
        
        // Create one accelerator per physical processor(simplified)
        var acceleratorCount = Math.Max(1, processorCount / 4);
        
        for(int i = 0; i < acceleratorCount; i++)
        {
            var accelerator = new TestCpuAccelerator($"CPU Accelerator {i}");
            accelerators.Add(accelerator);
        }

        return ValueTask.FromResult<IEnumerable<IAccelerator>>(accelerators);
    }

    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        var accelerator = new TestCpuAccelerator(info.Name);
        return ValueTask.FromResult<IAccelerator>(accelerator);
    }
}

/// <summary>
/// Test GPU accelerator provider for simulating GPU devices.
/// </summary>
public class TestGpuAcceleratorProvider : IAcceleratorProvider
{
    private readonly int _deviceCount;

    public TestGpuAcceleratorProvider(int deviceCount = 1)
    {
        _deviceCount = deviceCount;
    }

    public string Name => "Test GPU Provider";

    public AcceleratorType[] SupportedTypes => new[] { AcceleratorType.CUDA, AcceleratorType.OpenCL };

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var accelerators = new List<IAccelerator>();
        
        for(int i = 0; i < _deviceCount; i++)
        {
            // Create simulated GPU accelerator
            var accelerator = new TestGpuAccelerator($"GPU {i}", i);
            accelerators.Add(accelerator);
        }

        return ValueTask.FromResult<IEnumerable<IAccelerator>>(accelerators);
    }

    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        var accelerator = new TestGpuAccelerator(info.Name, 0);
        return ValueTask.FromResult<IAccelerator>(accelerator);
    }
}

/// <summary>
/// Test GPU accelerator implementation for simulating GPU behavior.
/// </summary>
public class TestGpuAccelerator : IAccelerator
{
    public AcceleratorType Type => AcceleratorType.CUDA;
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);
    private readonly IMemoryManager _memoryManager;
    private readonly ConcurrentDictionary<string, TestCompiledKernel> _compiledKernels;
    private bool _disposed;

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

    public AcceleratorInfo Info { get; }
    public IMemoryManager Memory => _memoryManager;

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if(definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        await Task.Delay(5, cancellationToken); // Simulate faster GPU compilation
        
        var kernel = new TestCompiledKernel(
            definition.Name,
            definition.Code,
            options ?? new CompilationOptions());
        
        _compiledKernels[definition.Name] = kernel;
        return kernel;
    }

    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Simulate GPU synchronization
    }

    public async ValueTask DisposeAsync()
    {
        if(!_disposed)
        {
            _disposed = true;
            await SynchronizeAsync();
            if(_memoryManager is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
