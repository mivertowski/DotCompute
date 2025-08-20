using DotCompute.Abstractions;

namespace DotCompute.Tests.Mocks;


/// <summary>
/// Mock execution strategy for testing parallel execution without real hardware.
/// </summary>
public sealed class MockParallelExecutionStrategy : IDisposable
{
    private readonly List<IAccelerator> _accelerators = [];
    private readonly Dictionary<string, object> _metrics = [];
    private bool _isDisposed;

    public MockParallelExecutionStrategy()
    {
        MaxConcurrency = Environment.ProcessorCount;
        Type = ExecutionStrategyType.Parallel;
    }

    public int MaxConcurrency { get; set; }
    public ExecutionStrategyType Type { get; set; }
    public bool IsDisposed => _isDisposed;

    public void AddAccelerator(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _accelerators.Add(accelerator);
    }

    public IReadOnlyList<IAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    public async Task<T> ExecuteAsync<T>(ExecutionTask<T> task, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(task);

        cancellationToken.ThrowIfCancellationRequested();

        // Simulate async execution
        await Task.Delay(10, cancellationToken);

        // Execute the work item
        if (task.WorkItem != null)
        {
            return await task.WorkItem(cancellationToken);
        }

        return default!;
    }

    public async Task<IEnumerable<T>> ExecuteParallelAsync<T>(
        IEnumerable<ExecutionTask<T>> tasks,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(tasks);

        var taskList = tasks.ToList();
        if (taskList.Count == 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        // Use SemaphoreSlim to limit concurrency
        using SemaphoreSlim semaphore = new(MaxConcurrency, MaxConcurrency);

        var executionTasks = taskList.Select(async task =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteAsync(task, cancellationToken);
            }
            finally
            {
                _ = semaphore.Release();
            }
        });

        return await Task.WhenAll(executionTasks);
    }

    public void SetMetric(string key, object value) => _metrics[key] = value;

    public object? GetMetric(string key) => _metrics.TryGetValue(key, out var value) ? value : null;

    public void Dispose()
    {
        if (_isDisposed)
            return;

        foreach (var accelerator in _accelerators)
        {
            if (accelerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _accelerators.Clear();
        _metrics.Clear();
        _isDisposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);
}

/// <summary>
/// Represents an execution task for the mock strategy.
/// </summary>
public sealed class ExecutionTask<T>
{
    public ExecutionTask(string name, Func<CancellationToken, Task<T>>? workItem = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        WorkItem = workItem;
    }

    public string Name { get; }
    public Func<CancellationToken, Task<T>>? WorkItem { get; }
    public AcceleratorType? PreferredAccelerator { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Execution strategy types.
/// </summary>
public enum ExecutionStrategyType
{
    Sequential,
    Parallel,
    Adaptive,
    Custom
}

/// <summary>
/// Mock load balancer for testing.
/// </summary>
public sealed class MockLoadBalancer
{
    private readonly List<IAccelerator> _accelerators = [];
    private int _nextIndex;

    public void AddAccelerator(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _accelerators.Add(accelerator);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Method has side effects")]
    public IAccelerator GetNextAccelerator()
    {
        if (_accelerators.Count == 0)
            throw new InvalidOperationException("No accelerators available");

        var accelerator = _accelerators[_nextIndex % _accelerators.Count];
        _nextIndex++;
        return accelerator;
    }

    public IAccelerator GetLeastLoadedAccelerator()
    {
        if (_accelerators.Count == 0)
            throw new InvalidOperationException("No accelerators available");

        // For mock, just return the one with most available memory
        return _accelerators
            .Where(a => a is MockAccelerator)
            .OrderByDescending(a => ((MockAccelerator)a).AvailableMemory)
            .FirstOrDefault() ?? _accelerators[0];
    }

    public void Reset() => _nextIndex = 0;
}

/// <summary>
/// Mock accelerator manager for testing.
/// </summary>
public sealed class MockAcceleratorManager : IAcceleratorManager
{
    private readonly List<IAccelerator> _accelerators = [];
    private readonly List<IAcceleratorProvider> _providers = [];
    private bool _isDisposed;
    private bool _isInitialized;

    public MockAcceleratorManager()
    {
        // Add a default mock accelerator
        _accelerators.Add(new MockAccelerator(name: "DefaultMockGPU"));
    }

    // IAcceleratorManager implementation
    public IAccelerator Default => _accelerators.FirstOrDefault() ?? throw new InvalidOperationException("No accelerators available");

    public IReadOnlyList<IAccelerator> AvailableAccelerators => _accelerators.AsReadOnly();

    public int Count => _accelerators.Count;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isInitialized)
            return;

        // Simulate async initialization
        await Task.Delay(10, cancellationToken);

        // Discover accelerators from providers
        foreach (var provider in _providers)
        {
            var discovered = await provider.DiscoverAsync(cancellationToken);
            foreach (var accelerator in discovered)
            {
                if (!_accelerators.Any(a => a.Info.Id == accelerator.Info.Id))
                {
                    _accelerators.Add(accelerator);
                }
            }
        }

        _isInitialized = true;
    }

    public IAccelerator GetAccelerator(int index)
    {
        if (index < 0 || index >= _accelerators.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _accelerators[index];
    }

    public IAccelerator? GetAcceleratorById(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _accelerators.FirstOrDefault(a => a.Info.Id == id);
    }

    public IEnumerable<IAccelerator> GetAcceleratorsByType(AcceleratorType type) => _accelerators.Where(a => Enum.Parse<AcceleratorType>(a.Info.DeviceType) == type);

    public IAccelerator? SelectBest(DotCompute.Abstractions.AcceleratorSelectionCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var query = _accelerators.AsEnumerable();

        if (criteria.PreferredType.HasValue)
        {
            query = query.Where(a => Enum.Parse<AcceleratorType>(a.Info.DeviceType) == criteria.PreferredType.Value);
        }

        if (criteria.MinimumMemory.HasValue)
        {
            query = query.Where(a => a.Info.TotalMemory >= criteria.MinimumMemory.Value);
        }

        if (criteria.RequiredFeatures.HasValue)
        {
            query = query.Where(a =>
            {
                if (a is MockAccelerator mock)
                {
                    return (mock.Features & criteria.RequiredFeatures.Value) == criteria.RequiredFeatures.Value;
                }
                return false;
            });
        }

        if (criteria.MinimumComputeCapability != null)
        {
            query = query.Where(a => a.Info.ComputeCapability != null &&
                                    a.Info.ComputeCapability >= criteria.MinimumComputeCapability);
        }

        // Apply custom scorer if provided
        if (criteria.CustomScorer != null)
        {
            return query.OrderByDescending(criteria.CustomScorer).FirstOrDefault();
        }

        // Default scoring: prefer dedicated, then by memory
        if (criteria.PreferDedicated)
        {
            query = query.OrderByDescending(a => a.Info.DeviceType != "CPU");
        }

        return query.OrderByDescending(a => a.Info.TotalMemory).FirstOrDefault();
    }

    public AcceleratorContext CreateContext(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);

        // For mock, create a simple context
        var deviceId = _accelerators.IndexOf(accelerator);
        if (deviceId < 0)
            throw new ArgumentException("Accelerator not managed by this manager", nameof(accelerator));

        return new AcceleratorContext(IntPtr.Zero, deviceId);
    }

    public void RegisterProvider(IAcceleratorProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!_providers.Contains(provider))
        {
            _providers.Add(provider);
        }
    }

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // Re-initialize to refresh
        _isInitialized = false;
        await InitializeAsync(cancellationToken);
    }

    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return Task.FromResult<IEnumerable<IAccelerator>>(_accelerators.AsReadOnly());
    }

    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var result = GetAcceleratorsByType(type);
        return Task.FromResult(result);
    }

    public Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? type = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var result = type.HasValue
            ? SelectBest(new DotCompute.Abstractions.AcceleratorSelectionCriteria { PreferredType = type.Value })
            : _accelerators.FirstOrDefault();

        return Task.FromResult(result);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        foreach (var accelerator in _accelerators)
        {
            if (accelerator is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (accelerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _accelerators.Clear();
        _providers.Clear();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    // Additional helper methods for testing
    public void AddAccelerator(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _accelerators.Add(accelerator);
    }

    public void RemoveAccelerator(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _ = _accelerators.Remove(accelerator);
    }

    public void ClearAccelerators() => _accelerators.Clear();
}

/// <summary>
/// Mock kernel reference for testing.
/// Since IKernel has static abstract members, we can't use it directly in collections.
/// This wrapper allows us to reference kernel instances.
/// </summary>
public sealed class MockKernelReference
{
    public MockKernelReference(string name, object kernelInstance)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(kernelInstance);
        Name = name;
        KernelInstance = kernelInstance;
    }

    public string Name { get; }
    public object KernelInstance { get; }

    public T GetKernel<T>() where T : class => (T)KernelInstance;
}

/// <summary>
/// Mock kernel manager for testing.
/// Uses MockKernelReference to work around IKernel's static abstract members.
/// </summary>
public sealed class MockKernelManager
{
    private readonly Dictionary<string, MockKernelReference> _kernels = [];

    public void RegisterKernel(string name, object kernelInstance)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(kernelInstance);

        _kernels[name] = new MockKernelReference(name, kernelInstance);
    }

    public MockKernelReference? GetKernel(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        return _kernels.TryGetValue(name, out var kernel) ? kernel : null;
    }

    public bool HasKernel(string name) => !string.IsNullOrEmpty(name) && _kernels.ContainsKey(name);

    public void RemoveKernel(string name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            _ = _kernels.Remove(name);
        }
    }

    public void Clear() => _kernels.Clear();

    public IEnumerable<string> KernelNames => _kernels.Keys;
}
