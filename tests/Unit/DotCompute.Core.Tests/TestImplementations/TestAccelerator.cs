// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Tests.TestImplementations;

/// <summary>
/// Test implementation of BaseAccelerator for unit testing.
/// Provides visibility into protected methods and tracks method calls.
/// </summary>
public class TestAccelerator(
    AcceleratorInfo info,
    IUnifiedMemoryManager memoryManager,
    ILogger logger) : BaseAccelerator(info, AcceleratorType.CPU, memoryManager, new AcceleratorContext(IntPtr.Zero, 0), logger)
{
    private readonly ConcurrentDictionary<string, ICompiledKernel> _kernelCache = new();
    private int _compilationCount;
    private int _cacheHits;
    /// <summary>
    /// Gets or sets the initialize core called.
    /// </summary>
    /// <value>The initialize core called.</value>

    // Tracking properties for test verification
    public bool InitializeCoreCalled { get; private set; }
    /// <summary>
    /// Gets or sets the compile kernel async called.
    /// </summary>
    /// <value>The compile kernel async called.</value>
    public bool CompileKernelAsyncCalled { get; private set; }
    /// <summary>
    /// Gets or sets the synchronize called.
    /// </summary>
    /// <value>The synchronize called.</value>
    public bool SynchronizeCalled { get; private set; }
    /// <summary>
    /// Gets or sets the disposed count.
    /// </summary>
    /// <value>The disposed count.</value>
    public int DisposedCount { get; private set; }
    /// <summary>
    /// Gets or sets the last compilation options.
    /// </summary>
    /// <value>The last compilation options.</value>
    public CompilationOptions? LastCompilationOptions { get; private set; }
    /// <summary>
    /// Gets or sets the last logged kernel name.
    /// </summary>
    /// <value>The last logged kernel name.</value>
    public string? LastLoggedKernelName { get; private set; }
    /// <summary>
    /// Gets or sets the last logged compilation time.
    /// </summary>
    /// <value>The last logged compilation time.</value>
    public TimeSpan LastLoggedCompilationTime { get; private set; }
    /// <summary>
    /// Gets or sets the last logged byte code size.
    /// </summary>
    /// <value>The last logged byte code size.</value>
    public long? LastLoggedByteCodeSize { get; private set; }
    /// <summary>
    /// Gets or sets the simulate compilation error.
    /// </summary>
    /// <value>The simulate compilation error.</value>

    // Error simulation flags
    public bool SimulateCompilationError { get; set; }
    /// <summary>
    /// Gets or sets the simulate synchronize error.
    /// </summary>
    /// <value>The simulate synchronize error.</value>
    public bool SimulateSynchronizeError { get; set; }
    /// <summary>
    /// Gets or sets the simulate initialization error.
    /// </summary>
    /// <value>The simulate initialization error.</value>
    public bool SimulateInitializationError { get; set; }
    /// <summary>
    /// Gets or sets the compilation count.
    /// </summary>
    /// <value>The compilation count.</value>

    // Performance tracking
    public int CompilationCount => _compilationCount;
    /// <summary>
    /// Gets or sets the cache hits.
    /// </summary>
    /// <value>The cache hits.</value>
    public int CacheHits => _cacheHits;

    protected override object? InitializeCore()
    {
        if (SimulateInitializationError)
            throw new InvalidOperationException("Simulated initialization error");

        InitializeCoreCalled = true;
        return null;
    }

    protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
        KernelDefinition definition,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SimulateCompilationError)
            throw new InvalidOperationException("Simulated compilation error");

        CompileKernelAsyncCalled = true;
        LastCompilationOptions = options;

        // Check cache
        var cacheKey = $"{definition.Name}_{options.OptimizationLevel}_{options.GenerateDebugInfo}";
        if (_kernelCache.TryGetValue(cacheKey, out var cachedKernel))
        {
            _ = Interlocked.Increment(ref _cacheHits);
            return cachedKernel;
        }

        // Simulate compilation
        await Task.Delay(10, cancellationToken); // Simulate compilation time
        _ = Interlocked.Increment(ref _compilationCount);

        var compiledKernel = new TestCompiledKernel(
            definition.Name,
            definition.EntryFunction,
            [0x01, 0x02, 0x03]);

        // Log metrics
        TestLogCompilationMetrics(definition.Name, TimeSpan.FromMilliseconds(10), compiledKernel.ByteCode.Length);

        // Cache the result
        _ = _kernelCache.TryAdd(cacheKey, compiledKernel);

        return compiledKernel;
    }
    /// <summary>
    /// Performs synchronize.
    /// </summary>

    public void Synchronize()
    {
        if (SimulateSynchronizeError)
            throw new InvalidOperationException("Simulated synchronize error");

        SynchronizeCalled = true;
    }

    protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken = default)
    {
        if (SimulateSynchronizeError)
            throw new InvalidOperationException("Simulated synchronize error");

        SynchronizeCalled = true;
        return ValueTask.CompletedTask;
    }

    protected override ValueTask DisposeCoreAsync()
    {
        DisposedCount++;
        _kernelCache.Clear();
        return base.DisposeCoreAsync();
    }
    /// <summary>
    /// Performs test throw if disposed.
    /// </summary>

    // Test helper methods to access protected members
    public void TestThrowIfDisposed() => ThrowIfDisposed();
    /// <summary>
    /// Performs test log compilation metrics.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="compilationTime">The compilation time.</param>
    /// <param name="byteCodeSize">The byte code size.</param>

    public void TestLogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize)
    {
        LastLoggedKernelName = kernelName;
        LastLoggedCompilationTime = compilationTime;
        LastLoggedByteCodeSize = byteCodeSize;
        LogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
    }
    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    /// <value>The context.</value>

    public new AcceleratorContext Context => base.Context;
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() => DisposeAsync().AsTask().Wait();
}

/// <summary>
/// Test implementation of ICompiledKernel for unit testing.
/// </summary>
public class TestCompiledKernel(string kernelName, string entryFunction, byte[] byteCode) : ICompiledKernel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; } = kernelName ?? throw new ArgumentNullException(nameof(kernelName));
    /// <summary>
    /// Gets or sets the entry function.
    /// </summary>
    /// <value>The entry function.</value>
    public string EntryFunction { get; } = entryFunction ?? throw new ArgumentNullException(nameof(entryFunction));
    /// <summary>
    /// Gets or sets the byte code.
    /// </summary>
    /// <value>The byte code.</value>
    public byte[] ByteCode { get; } = byteCode ?? throw new ArgumentNullException(nameof(byteCode));
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid => ByteCode.Length > 0;
    /// <summary>
    /// Gets or sets the max threads per block.
    /// </summary>
    /// <value>The max threads per block.</value>
    public int MaxThreadsPerBlock => 1024;
    /// <summary>
    /// Gets or sets the required shared memory.
    /// </summary>
    /// <value>The required shared memory.</value>
    public int RequiredSharedMemory => 0;
    /// <summary>
    /// Gets or sets the registers per thread.
    /// </summary>
    /// <value>The registers per thread.</value>
    public int RegistersPerThread => 32;
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
        // Test implementation - return completed task

        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
        // Test implementation - no resources to dispose

        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        // Test implementation - no resources to dispose
    }
}
