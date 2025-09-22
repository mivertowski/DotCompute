// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Interfaces.Device;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Memory.Interfaces;
using CorePipeline = DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline;
namespace DotCompute.Linq.Pipelines.Bridge;
{
/// <summary>
/// Minimal bridge adapter that provides fluent API extensions to Core IKernelPipeline.
/// This critical bridge resolves compilation errors between incompatible interfaces.
/// </summary>
public sealed class FluentPipelineAdapter : IAsyncDisposable
{
    private readonly CorePipeline _corePipeline;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the FluentPipelineAdapter.
    /// </summary>
    /// <param name="corePipeline">The core pipeline to wrap</param>
    public FluentPipelineAdapter(CorePipeline corePipeline)
    {
        _corePipeline = corePipeline ?? throw new ArgumentNullException(nameof(corePipeline));
    }
    /// Gets the wrapped core pipeline.
    public CorePipeline CorePipeline => _corePipeline;
    /// Chains a kernel to execute after the current pipeline stage.
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <param name="kernelName">The kernel name</param>
    /// <param name="parameterBuilder">Parameter builder function</param>
    /// <returns>A new pipeline with the additional stage</returns>
    public FluentPipelineAdapter Then<TInput, TOutput>(
        string kernelName,
        Func<TInput, object[]>? parameterBuilder = null)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual pipeline building
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// Executes multiple stages in parallel.
    /// <param name="stages">The parallel stages</param>
    /// <returns>A new pipeline with parallel execution</returns>
    public FluentPipelineAdapter Parallel<TInput, TOutput>(
        params (string kernelName, Func<TInput, object[]>? parameterBuilder)[] stages)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual parallel logic
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// Adds caching to the pipeline.
    /// <typeparam name="T">The type to cache</typeparam>
    /// <param name="cacheKey">The cache key</param>
    /// <returns>A new pipeline with caching</returns>
    public FluentPipelineAdapter Cache<T>(string cacheKey)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual caching logic
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// Applies optimizations to the pipeline.
    /// <returns>An optimized pipeline</returns>
    public FluentPipelineAdapter Optimize()
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual optimization
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// Adds retry logic to the pipeline.
    /// <param name="maxAttempts">Maximum retry attempts</param>
    /// <param name="delay">Delay between retries</param>
    /// <returns>A new pipeline with retry logic</returns>
    public FluentPipelineAdapter Retry(int maxAttempts = 3, TimeSpan? delay = null)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual retry logic
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// Adds timeout handling to the pipeline.
    /// <param name="timeout">The timeout duration</param>
    /// <returns>A new pipeline with timeout handling</returns>
    public FluentPipelineAdapter Timeout(TimeSpan timeout)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual timeout logic
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// Executes the pipeline and returns the result.
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TOutput> ExecuteAsync<TOutput>(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var context = CreateDefaultContext();
        var result = await _corePipeline.ExecuteAsync(context, cancellationToken);
        return ExtractOutput<TOutput>(result);
    }
    /// Executes the pipeline with input data.
    /// <param name="input">The input data</param>
    public async Task<TOutput> ExecuteAsync<TInput, TOutput>(
        TInput input,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var context = CreateContext(input);
        var result = await _corePipeline.ExecuteAsync(context, cancellationToken);
        return ExtractOutput<TOutput>(result);
    }
    /// Executes the pipeline in streaming mode.
    /// <param name="inputStream">The input stream</param>
    /// <returns>The output stream</returns>
    public async IAsyncEnumerable<TOutput> ExecuteStreamAsync<TInput, TOutput>(
        IAsyncEnumerable<TInput> inputStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await foreach (var input in inputStream.WithCancellation(cancellationToken))
        {
            var result = await ExecuteAsync<TInput, TOutput>(input, cancellationToken);
            yield return result;
        }
    }
    /// Composes this pipeline with another.
    /// <param name="other">The other pipeline</param>
    /// <returns>A composed pipeline</returns>
    public FluentPipelineAdapter Compose(FluentPipelineAdapter other)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(other);
        // For now, return the same pipeline - this would be implemented with actual composition
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// Creates a copy of this pipeline.
    /// <returns>A cloned pipeline</returns>
    public FluentPipelineAdapter Clone()
    {
        ThrowIfDisposed();
        // For now, return a new adapter with the same core pipeline
        return new FluentPipelineAdapter(_corePipeline);
    }
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        try
        {
            if (_corePipeline != null)
            {
                await _corePipeline.DisposeAsync();
            }
        }
        finally
        {
            _disposed = true;
        }
    }
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FluentPipelineAdapter));
    }
    private PipelineExecutionContext CreateDefaultContext()
    {
        var context = new PipelineExecutionContext();
        context.Inputs.Clear();
        context.SetMemoryManager(new DefaultMemoryManager());
        context.SetDevice(new DefaultDevice());
        context.Options = PipelineExecutionOptions.Default;
        return context;
    }
    private PipelineExecutionContext CreateContext<TInput>(TInput input)
    {
        var context = CreateDefaultContext();
        context.Inputs["input"] = input!;
        return context;
    }
    private TOutput ExtractOutput<TOutput>(PipelineExecutionResult result)
    {
        if (!result.Success)
        {
            var error = result.Errors?.FirstOrDefault()?.Message ?? "Pipeline execution failed";
            throw new InvalidOperationException(error);
        }
        if (result.Outputs.TryGetValue("output", out var output) && output is TOutput typedOutput)
            return typedOutput;
        var compatibleOutput = result.Outputs.Values.OfType<TOutput>().FirstOrDefault();
        if (compatibleOutput != null)
            return compatibleOutput;
        return default(TOutput)!;
    }
}
/// Extension methods for fluent pipeline API.
public static class CorePipelineFluentExtensions
{
    /// <summary>
    /// Converts a core pipeline to a fluent adapter.
    /// </summary>
    /// <param name="corePipeline">The core pipeline</param>
    /// <returns>A fluent pipeline adapter</returns>
    public static FluentPipelineAdapter AsFluentPipeline(this CorePipeline corePipeline)
    {
        return new FluentPipelineAdapter(corePipeline);
    }
    /// <summary>
    /// Adds a sequential stage using fluent syntax.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <param name="kernelName">The kernel name</param>
    /// <param name="parameterBuilder">Parameter builder</param>
    /// <returns>A new pipeline with the stage</returns>
    public static FluentPipelineAdapter Then<TInput, TOutput>(
        this CorePipeline pipeline,
        string kernelName,
        Func<TInput, object[]>? parameterBuilder = null)
    {
        return pipeline.AsFluentPipeline().Then<TInput, TOutput>(kernelName, parameterBuilder);
    }
    /// <summary>
    /// Adds parallel stages using fluent syntax.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <param name="stages">The parallel stages</param>
    /// <returns>A new pipeline with parallel stages</returns>
    public static FluentPipelineAdapter Parallel<TInput, TOutput>(
        this CorePipeline pipeline,
        params (string kernelName, Func<TInput, object[]>? parameterBuilder)[] stages)
    {
        return pipeline.AsFluentPipeline().Parallel<TInput, TOutput>(stages);
    }
    /// <summary>
    /// Adds caching using fluent syntax.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <param name="cacheKey">The cache key</param>
    /// <returns>A new pipeline with caching</returns>
    public static FluentPipelineAdapter Cache<T>(
        this CorePipeline pipeline,
        string cacheKey)
    {
        return pipeline.AsFluentPipeline().Cache<T>(cacheKey);
    }
    /// <summary>
    /// Applies optimizations using fluent syntax.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <returns>An optimized pipeline</returns>
    public static FluentPipelineAdapter Optimize(this CorePipeline pipeline)
    {
        return pipeline.AsFluentPipeline().Optimize();
    }
    /// <summary>
    /// Adds retry logic using fluent syntax.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <param name="maxAttempts">Maximum retry attempts</param>
    /// <param name="delay">Delay between retries</param>
    /// <returns>A new pipeline with retry logic</returns>
    public static FluentPipelineAdapter Retry(
        {
        this CorePipeline pipeline,
        int maxAttempts = 3,
        TimeSpan? delay = null)
    {
        return pipeline.AsFluentPipeline().Retry(maxAttempts, delay);
    }
    /// <summary>
    /// Adds timeout handling using fluent syntax.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <param name="timeout">The timeout duration</param>
    /// <returns>A new pipeline with timeout handling</returns>
    public static FluentPipelineAdapter Timeout(
        this CorePipeline pipeline,
        TimeSpan timeout)
    {
        return pipeline.AsFluentPipeline().Timeout(timeout);
    }
}
#region Minimal Supporting Types
// Minimal implementations for memory manager and device
internal class DefaultMemoryManager : DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemoryManager
{
    public async ValueTask DisposeAsync() { }
        {
    public async ValueTask<DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T>> AllocateAsync<T>(
        long elementCount,
        DotCompute.Abstractions.Pipelines.Enums.MemoryHint hint = DotCompute.Abstractions.Pipelines.Enums.MemoryHint.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return new DefaultPipelineMemory<T>(elementCount);
    }
    public async ValueTask<DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T>> AllocateSharedAsync<T>(
        string key,
        long elementCount,
        DotCompute.Abstractions.Pipelines.Enums.MemoryHint hint = DotCompute.Abstractions.Pipelines.Enums.MemoryHint.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return new DefaultPipelineMemory<T>(elementCount);
    }
    public DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T>? GetShared<T>(string key) where T : unmanaged
    {
        return null;
    }
    public async ValueTask TransferAsync<T>(
        DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T> memory,
        string fromStage,
        string toStage,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        // No-op for default implementation
    }
    public DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemoryView<T> CreateView<T>(
        DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T> memory,
        long offset = 0,
        long? length = null) where T : unmanaged
    {
        return new DefaultPipelineMemoryView<T>(memory, offset, length ?? memory.ElementCount);
    }
    public async ValueTask<DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T>> OptimizeLayoutAsync<T>(
        DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T> memory,
        DotCompute.Abstractions.Interfaces.Pipelines.MemoryLayoutHint layoutHint,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return memory; // No optimization in default implementation
    }
    public DotCompute.Abstractions.Interfaces.Pipelines.MemoryManagerStats GetStats()
    {
        return new DotCompute.Abstractions.Interfaces.Pipelines.MemoryManagerStats
        {
            TotalAllocatedBytes = 0,
            CurrentUsedBytes = 0,
            PeakUsedBytes = 0,
            ActiveAllocationCount = 0,
            TotalAllocationCount = 0,
            CacheHitRate = 0.0,
            PoolEfficiency = 0.0,
            FragmentationPercentage = 0.0
        };
    }
    public async ValueTask CollectAsync(CancellationToken cancellationToken = default)
    {
        GC.Collect();
    }
    public void RegisterPool<T>(DotCompute.Abstractions.Interfaces.Pipelines.MemoryPoolOptions options) where T : unmanaged
    {
        // No-op for default implementation
    }
}
internal class DefaultPipelineMemory<T> : DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T> where T : unmanaged
{
    private readonly T[] _data;
    private bool _isDisposed;

    }
    public DefaultPipelineMemory(long elementCount)
    {
        ElementCount = elementCount;
        _data = new T[elementCount];
        Id = Guid.NewGuid().ToString();
        Device = new DefaultDevice();
    }
    public string Id { get; }
    public long ElementCount { get; }
    public long SizeInBytes => ElementCount * Marshal.SizeOf<T>();
    public bool IsLocked { get; private set; }
    public DotCompute.Abstractions.Pipelines.Enums.MemoryAccess AccessMode => DotCompute.Abstractions.Pipelines.Enums.MemoryAccess.ReadWrite;
    public DotCompute.Abstractions.Interfaces.Device.IComputeDevice Device { get; }
    public async ValueTask<DotCompute.Abstractions.Interfaces.Pipelines.MemoryLock<T>> LockAsync(
        DotCompute.Abstractions.Interfaces.Pipelines.MemoryLockMode mode,
        CancellationToken cancellationToken = default)
    {
        IsLocked = true;
        return new DotCompute.Abstractions.Interfaces.Pipelines.MemoryLock<T>(this, mode, () => IsLocked = false);
    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        source.Span.CopyTo(_data.AsSpan((int)offset));
    }
    public async ValueTask CopyToAsync(Memory<T> destination, long offset = 0, int? count = null, CancellationToken cancellationToken = default)
    {
        var length = count ?? (int)(ElementCount - offset);
        _data.AsSpan((int)offset, length).CopyTo(destination.Span);
    }
    public DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemoryView<T> Slice(long offset, long length)
    {
        return new DefaultPipelineMemoryView<T>(this, offset, length);
    }
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // No-op for CPU memory
    }

    public void Dispose()
    {
        _isDisposed = true;
    }
}
internal class DefaultPipelineMemoryView<T> : DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemoryView<T> where T : unmanaged
{
    }
    public DefaultPipelineMemoryView(DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T> parent, long offset, long length)
    {
        Parent = parent;
        Offset = offset;
        Length = length;
    }
    public long Offset { get; }
    public long Length { get; }
    public DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemory<T> Parent { get; }

    public DotCompute.Abstractions.Interfaces.Pipelines.IPipelineMemoryView<T> Slice(long offset, long length)
    {
        return new DefaultPipelineMemoryView<T>(Parent, Offset + offset, length);
    }
}
internal class DefaultBuffer<T> : IUnifiedBuffer<T> where T : unmanaged
{
    private readonly T[] _data;

    }
    public DefaultBuffer(int length)
    {
        Length = length;
        _data = new T[length];
    }
    public int Length { get; }
    public long SizeInBytes => Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
    public bool IsDisposed { get; private set; }
    public Span<T> GetSpan() => _data.AsSpan();
    public Memory<T> GetMemory() => _data.AsMemory();
    public void CopyFrom(ReadOnlySpan<T> source) => source.CopyTo(_data);
    public void CopyTo(Span<T> destination) => _data.AsSpan().CopyTo(destination);
    public Task CopyFromAsync(IUnifiedBuffer<T> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_data);
        return Task.CompletedTask;
    }
    public Task CopyToAsync(IUnifiedBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        _data.AsSpan().CopyTo(destination.GetSpan());
        return Task.CompletedTask;
    }
    public void Dispose() => IsDisposed = true;
internal class DefaultDevice : IComputeDevice
{
    public string Id => "default-cpu";
    public string Name => "Default CPU Device";
    public DeviceType Type => DeviceType.CPU;
    public bool IsAvailable => true;
    public long TotalMemory => GC.GetTotalMemory(false);
    public long AvailableMemory => GC.GetTotalMemory(false);
    public int ComputeUnits => Environment.ProcessorCount;
    public IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object>();
    public DeviceCapabilities Capabilities => new DeviceCapabilities
    {
        SupportsDoubles = true,
        SupportsAtomics = true,
        SupportsImages = false,
        MaxWorkGroupSize = 1024,
        LocalMemorySize = 64 * 1024
    };
    public void Dispose() { }
        {
}
#endregion
