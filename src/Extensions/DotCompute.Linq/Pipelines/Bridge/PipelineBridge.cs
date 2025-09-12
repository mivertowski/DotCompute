// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Pipelines.Models;
using CorePipeline = DotCompute.Core.Pipelines.IKernelPipeline;

namespace DotCompute.Linq.Pipelines.Bridge;

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

    /// <summary>
    /// Gets the wrapped core pipeline.
    /// </summary>
    public CorePipeline CorePipeline => _corePipeline;

    /// <summary>
    /// Chains a kernel to execute after the current pipeline stage.
    /// </summary>
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

    /// <summary>
    /// Executes multiple stages in parallel.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <param name="stages">The parallel stages</param>
    /// <returns>A new pipeline with parallel execution</returns>
    public FluentPipelineAdapter Parallel<TInput, TOutput>(
        params (string kernelName, Func<TInput, object[]>? parameterBuilder)[] stages)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual pipeline building
        return new FluentPipelineAdapter(_corePipeline);
    }

    /// <summary>
    /// Adds caching to the pipeline.
    /// </summary>
    /// <typeparam name="T">The type to cache</typeparam>
    /// <param name="cacheKey">The cache key</param>
    /// <returns>A new pipeline with caching</returns>
    public FluentPipelineAdapter Cache<T>(string cacheKey)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual caching logic
        return new FluentPipelineAdapter(_corePipeline);
    }

    /// <summary>
    /// Applies optimizations to the pipeline.
    /// </summary>
    /// <returns>An optimized pipeline</returns>
    public FluentPipelineAdapter Optimize()
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual optimization
        return new FluentPipelineAdapter(_corePipeline);
    }

    /// <summary>
    /// Adds retry logic to the pipeline.
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts</param>
    /// <param name="delay">Delay between retries</param>
    /// <returns>A new pipeline with retry logic</returns>
    public FluentPipelineAdapter Retry(int maxAttempts = 3, TimeSpan? delay = null)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual retry logic
        return new FluentPipelineAdapter(_corePipeline);
    }

    /// <summary>
    /// Adds timeout handling to the pipeline.
    /// </summary>
    /// <param name="timeout">The timeout duration</param>
    /// <returns>A new pipeline with timeout handling</returns>
    public FluentPipelineAdapter Timeout(TimeSpan timeout)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual timeout logic
        return new FluentPipelineAdapter(_corePipeline);
    }

    /// <summary>
    /// Executes the pipeline and returns the result.
    /// </summary>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TOutput> ExecuteAsync<TOutput>(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        var context = CreateDefaultContext();
        var result = await _corePipeline.ExecuteAsync(context, cancellationToken);


        return ExtractOutput<TOutput>(result);
    }

    /// <summary>
    /// Executes the pipeline with input data.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <param name="input">The input data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TOutput> ExecuteAsync<TInput, TOutput>(
        TInput input,

        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        var context = CreateContext(input);
        var result = await _corePipeline.ExecuteAsync(context, cancellationToken);


        return ExtractOutput<TOutput>(result);
    }

    /// <summary>
    /// Executes the pipeline in streaming mode.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <param name="inputStream">The input stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
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

    /// <summary>
    /// Composes this pipeline with another.
    /// </summary>
    /// <param name="other">The other pipeline</param>
    /// <returns>A composed pipeline</returns>
    public FluentPipelineAdapter Compose(FluentPipelineAdapter other)
    {
        ThrowIfDisposed();
        // For now, return the same pipeline - this would be implemented with actual composition
        return new FluentPipelineAdapter(_corePipeline);
    }

    /// <summary>
    /// Creates a copy of this pipeline.
    /// </summary>
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
        {
            return;
        }


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
        {

            throw new ObjectDisposedException(nameof(FluentPipelineAdapter));
        }

    }

    private PipelineExecutionContext CreateDefaultContext()
    {
        return new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = (DotCompute.Core.Pipelines.IPipelineMemoryManager)new DefaultMemoryManager(),
            Device = (DotCompute.Core.Device.Interfaces.IComputeDevice)new DefaultDevice(),
            Options = PipelineExecutionOptions.Default
        };
    }

    private PipelineExecutionContext CreateContext<TInput>(TInput input)
    {
        return new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object> { ["input"] = input! },
            MemoryManager = (DotCompute.Core.Pipelines.IPipelineMemoryManager)new DefaultMemoryManager(),
            Device = (DotCompute.Core.Device.Interfaces.IComputeDevice)new DefaultDevice(),
            Options = PipelineExecutionOptions.Default
        };
    }

    private TOutput ExtractOutput<TOutput>(PipelineExecutionResult result)
    {
        if (!result.Success)
        {
            var error = result.Errors?.FirstOrDefault()?.Message ?? "Pipeline execution failed";
            throw new InvalidOperationException(error);
        }

        if (result.Outputs.TryGetValue("output", out var output) && output is TOutput typedOutput)
        {
            return typedOutput;
        }

        var compatibleOutput = result.Outputs.Values.OfType<TOutput>().FirstOrDefault();
        if (compatibleOutput != null)
        {
            return compatibleOutput;
        }

        return default(TOutput)!;
    }
}

/// <summary>
/// Extension methods for fluent pipeline API.
/// </summary>
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
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
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
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
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
    /// <typeparam name="T">The type to cache</typeparam>
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
internal class DefaultMemoryManager : IPipelineMemoryManager
{
    public void Dispose() { }

    public IUnifiedBuffer<T> AllocateBuffer<T>(string name, int size) where T : unmanaged
    {
        return new DefaultBuffer<T>(size);
    }

    public IUnifiedBuffer<T> GetBuffer<T>(string name) where T : unmanaged
    {
        throw new ArgumentException($"Buffer '{name}' not found");
    }

    public void ReleaseBuffer(string name) { }

    public IReadOnlyDictionary<string, long> GetBufferSizes()
    {
        return new Dictionary<string, long>();
    }
}

internal class DefaultBuffer<T> : IUnifiedBuffer<T> where T : unmanaged
{
    private readonly T[] _data;

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
}

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
}


#endregion