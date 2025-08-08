using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Execution;

namespace DotCompute.Tests.Mocks;

/// <summary>
/// Mock execution strategy for testing parallel execution without real hardware.
/// </summary>
public class MockParallelExecutionStrategy
{
    private readonly List<IAccelerator> _accelerators = new();
    private readonly Dictionary<string, object> _metrics = new();
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
        if (accelerator == null) throw new ArgumentNullException(nameof(accelerator));
        _accelerators.Add(accelerator);
    }

    public IReadOnlyList<IAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    public async Task<T> ExecuteAsync<T>(ExecutionTask<T> task, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (task == null) throw new ArgumentNullException(nameof(task));
        
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

    public async Task<IList<T>> ExecuteBatchAsync<T>(
        IEnumerable<ExecutionTask<T>> tasks, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (tasks == null) throw new ArgumentNullException(nameof(tasks));
        
        var taskList = tasks.ToList();
        if (taskList.Count == 0)
            return new List<T>();
        
        var results = new List<T>();
        var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var executionTasks = new List<Task<T>>();
        
        foreach (var task in taskList)
        {
            var executionTask = ExecuteWithSemaphoreAsync(task, semaphore, cancellationToken);
            executionTasks.Add(executionTask);
        }
        
        var completedResults = await Task.WhenAll(executionTasks);
        return completedResults.ToList();
    }

    private async Task<T> ExecuteWithSemaphoreAsync<T>(
        ExecutionTask<T> task,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteAsync(task, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public Task<IList<IWorkItem>> ScheduleAsync(IEnumerable<IWorkItem> workItems, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (workItems == null) throw new ArgumentNullException(nameof(workItems));
        
        // Simple priority-based scheduling
        var scheduled = workItems
            .OrderByDescending(w => w.Priority)
            .ThenBy(w => w.CreatedAt)
            .ToList();
        
        return Task.FromResult<IList<IWorkItem>>(scheduled);
    }

    public void SetMetric(string key, object value)
    {
        _metrics[key] = value;
    }

    public object? GetMetric(string key)
    {
        return _metrics.TryGetValue(key, out var value) ? value : null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        foreach (var accelerator in _accelerators)
        {
            accelerator.Dispose();
        }
        _accelerators.Clear();
        _metrics.Clear();
        
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockParallelExecutionStrategy));
    }
}

/// <summary>
/// Represents an execution task for parallel processing.
/// </summary>
public class ExecutionTask<T>
{
    public string Name { get; set; } = string.Empty;
    public Func<CancellationToken, Task<T>>? WorkItem { get; set; }
    public CancellationToken CancellationToken { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a unit of work to be executed.
/// </summary>
public interface IWorkItem
{
    string Id { get; }
    int Priority { get; }
    WorkItemStatus Status { get; set; }
    DateTime CreatedAt { get; }
    DateTime? StartedAt { get; set; }
    DateTime? CompletedAt { get; set; }
    TimeSpan EstimatedDuration { get; }
    object? Result { get; set; }
    Exception? Error { get; set; }
}

/// <summary>
/// Status of a work item.
/// </summary>
public enum WorkItemStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Simple work item implementation for testing.
/// </summary>
public class SimpleWorkItem : IWorkItem
{
    public SimpleWorkItem(string id, int priority = 0)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Priority = priority;
        CreatedAt = DateTime.UtcNow;
        EstimatedDuration = TimeSpan.FromSeconds(1);
    }

    public string Id { get; }
    public int Priority { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;
    public DateTime CreatedAt { get; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public object? Result { get; set; }
    public Exception? Error { get; set; }
}

/// <summary>
/// Mock accelerator manager for testing.
/// </summary>
public class MockAcceleratorManager : IAcceleratorManager
{
    private readonly List<IAccelerator> _accelerators = new();
    
    public MockAcceleratorManager()
    {
        // Add a default mock accelerator
        _accelerators.Add(new MockAccelerator("DefaultMockGPU"));
    }

    public async Task<IReadOnlyList<IAccelerator>> GetAvailableAcceleratorsAsync(
        AcceleratorType? type = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        
        if (type.HasValue)
        {
            return _accelerators.Where(a => a.Type == type.Value).ToList();
        }
        
        return _accelerators.AsReadOnly();
    }

    public async Task<IAccelerator> SelectAcceleratorAsync(
        AcceleratorSelectionCriteria? criteria = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        
        if (criteria != null)
        {
            var matching = _accelerators
                .Where(a => criteria.PreferredType == null || a.Type == criteria.PreferredType)
                .Where(a => a.TotalMemory >= criteria.MinimumMemory)
                .Where(a => criteria.RequiredFeatures == null || (a.Features & criteria.RequiredFeatures.Value) == criteria.RequiredFeatures.Value)
                .FirstOrDefault();
            
            if (matching != null)
                return matching;
        }
        
        return _accelerators.FirstOrDefault() ?? throw new InvalidOperationException("No accelerators available");
    }

    public void AddAccelerator(IAccelerator accelerator)
    {
        if (accelerator == null) throw new ArgumentNullException(nameof(accelerator));
        _accelerators.Add(accelerator);
    }

    public void RemoveAccelerator(IAccelerator accelerator)
    {
        _accelerators.Remove(accelerator);
    }

    public void ClearAccelerators()
    {
        _accelerators.Clear();
    }
}

/// <summary>
/// Mock kernel manager for testing.
/// </summary>
public class MockKernelManager
{
    private readonly Dictionary<string, IKernel> _kernels = new();
    
    public void RegisterKernel(string name, IKernel kernel)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name cannot be null or empty", nameof(name));
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        
        _kernels[name] = kernel;
    }
    
    public IKernel? GetKernel(string name)
    {
        return _kernels.TryGetValue(name, out var kernel) ? kernel : null;
    }
    
    public bool HasKernel(string name)
    {
        return _kernels.ContainsKey(name);
    }
    
    public void RemoveKernel(string name)
    {
        _kernels.Remove(name);
    }
    
    public void ClearKernels()
    {
        foreach (var kernel in _kernels.Values)
        {
            kernel.Dispose();
        }
        _kernels.Clear();
    }
}