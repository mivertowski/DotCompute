// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Tests.TestImplementations;

/// <summary>
/// Test implementation of BaseAccelerator for unit testing.
/// Provides visibility into protected methods and tracks method calls.
/// </summary>
public class TestAccelerator : BaseAccelerator
{
    private readonly ConcurrentDictionary<string, ICompiledKernel> _kernelCache = new();
    private int _compilationCount;
    private int _cacheHits;

    // Tracking properties for test verification
    public bool InitializeCoreCalled { get; private set; }
    public bool CompileKernelAsyncCalled { get; private set; }
    public bool SynchronizeCalled { get; private set; }
    public int DisposedCount { get; private set; }
    public CompilationOptions? LastCompilationOptions { get; private set; }
    public string? LastLoggedKernelName { get; private set; }
    public TimeSpan LastLoggedCompilationTime { get; private set; }
    public long? LastLoggedByteCodeSize { get; private set; }

    // Error simulation flags
    public bool SimulateCompilationError { get; set; }
    public bool SimulateSynchronizeError { get; set; }
    public bool SimulateInitializationError { get; set; }

    // Performance tracking
    public int CompilationCount => _compilationCount;
    public int CacheHits => _cacheHits;

    public TestAccelerator(
        AcceleratorInfo info,
        IUnifiedMemoryManager memoryManager,
        ILogger logger)
        : base(info, memoryManager, logger)
    {
    }

    protected override object? InitializeCore()
    {
        if (SimulateInitializationError)
            throw new InvalidOperationException("Simulated initialization error");

        InitializeCoreCalled = true;
        return null;
    }

    protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
        KernelDefinition kernelDefinition,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SimulateCompilationError)
            throw new InvalidOperationException("Simulated compilation error");

        CompileKernelAsyncCalled = true;
        LastCompilationOptions = options;

        // Check cache
        var cacheKey = $"{kernelDefinition.Name}_{options.OptimizationLevel}_{options.GenerateDebugInfo}";
        if (_kernelCache.TryGetValue(cacheKey, out var cachedKernel))
        {
            Interlocked.Increment(ref _cacheHits);
            return new ValueTask<ICompiledKernel>(cachedKernel);
        }

        // Simulate compilation
        Thread.Sleep(10); // Simulate compilation time
        Interlocked.Increment(ref _compilationCount);

        var compiledKernel = new TestCompiledKernel(
            kernelDefinition.Name,
            kernelDefinition.EntryFunction,
            new byte[] { 0x01, 0x02, 0x03 });

        // Log metrics
        TestLogCompilationMetrics(kernelDefinition.Name, TimeSpan.FromMilliseconds(10), compiledKernel.ByteCode.Length);

        // Cache the result
        _kernelCache.TryAdd(cacheKey, compiledKernel);

        return new ValueTask<ICompiledKernel>(compiledKernel);
    }

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

    // Test helper methods to access protected members
    public void TestThrowIfDisposed() => ThrowIfDisposed();

    public void TestLogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize)
    {
        LastLoggedKernelName = kernelName;
        LastLoggedCompilationTime = compilationTime;
        LastLoggedByteCodeSize = byteCodeSize;
        LogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
    }

    public new AcceleratorContext Context => base.Context;
}

/// <summary>
/// Test implementation of ICompiledKernel for unit testing.
/// </summary>
public class TestCompiledKernel : ICompiledKernel
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name => KernelName;
    public string KernelName { get; }
    public string EntryFunction { get; }
    public byte[] ByteCode { get; }
    public bool IsValid => ByteCode.Length > 0;
    public int MaxThreadsPerBlock => 1024;
    public int RequiredSharedMemory => 0;
    public int RegistersPerThread => 32;

    public TestCompiledKernel(string kernelName, string entryFunction, byte[] byteCode)
    {
        KernelName = kernelName ?? throw new ArgumentNullException(nameof(kernelName));
        EntryFunction = entryFunction ?? throw new ArgumentNullException(nameof(entryFunction));
        ByteCode = byteCode ?? throw new ArgumentNullException(nameof(byteCode));
    }

    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        // Test implementation - return completed task
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // Test implementation - no resources to dispose
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // Test implementation - no resources to dispose
    }
}