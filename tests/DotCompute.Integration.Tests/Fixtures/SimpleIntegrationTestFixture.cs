// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core;
using DotCompute.Core.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DotCompute.Integration.Tests.Fixtures;

/// <summary>
/// Simplified integration test fixture for Phase 3 testing.
/// </summary>
public class SimpleIntegrationTestFixture : IDisposable
{
    public IHost Host { get; }
    public IServiceProvider Services => Host.Services;
    public IMemoryManager MemoryManager { get; }
    public IComputeEngine ComputeEngine { get; }
    public ILogger<SimpleIntegrationTestFixture> Logger { get; }

    public SimpleIntegrationTestFixture()
    {
        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register core services
                services.AddSingleton<IMemoryManager, TestMemoryManager>();
                services.AddSingleton<IComputeEngine, TestComputeEngine>();

                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
            });

        Host = hostBuilder.Build();
        Host.Start();

        MemoryManager = Services.GetRequiredService<IMemoryManager>();
        ComputeEngine = Services.GetRequiredService<IComputeEngine>();
        Logger = Services.GetRequiredService<ILogger<SimpleIntegrationTestFixture>>();
    }

    public bool IsCudaAvailable => false; // For now, focus on CPU testing

    public async Task<IBuffer<T>> CreateBufferAsync<T>(T[] data, MemoryLocation location = MemoryLocation.Host)
        where T : unmanaged
    {
        return await MemoryManager.CreateBufferAsync<T>(data, location);
    }

    public void Dispose()
    {
        Host?.Dispose();
    }
}

/// <summary>
/// Test implementation of memory manager.
/// </summary>
internal class TestMemoryManager : IMemoryManager
{
    public MemoryLocation[] AvailableLocations => new[] { MemoryLocation.Host, MemoryLocation.Device };

    public async ValueTask<IBuffer<T>> CreateBufferAsync<T>(int elementCount, MemoryLocation location, MemoryAccess access = MemoryAccess.ReadWrite, CancellationToken cancellationToken = default) where T : unmanaged
    {
        await Task.Delay(1, cancellationToken); // Simulate async operation
        return new TestBuffer<T>(elementCount, location, access);
    }

    public async ValueTask<IBuffer<T>> CreateBufferAsync<T>(ReadOnlyMemory<T> data, MemoryLocation location, MemoryAccess access = MemoryAccess.ReadWrite, CancellationToken cancellationToken = default) where T : unmanaged
    {
        await Task.Delay(1, cancellationToken); // Simulate async operation
        var buffer = new TestBuffer<T>(data.Length, location, access);
        await buffer.WriteAsync(data, 0, cancellationToken);
        return buffer;
    }

    public async ValueTask CopyAsync<T>(IBuffer<T> source, IBuffer<T> destination, long sourceOffset = 0, long destinationOffset = 0, long? elementCount = null, CancellationToken cancellationToken = default) where T : unmanaged
    {
        await Task.Delay(1, cancellationToken); // Simulate async operation
        var count = (int)(elementCount ?? Math.Min(source.ElementCount - sourceOffset, destination.ElementCount - destinationOffset));
        var data = await source.ReadAsync((int)sourceOffset, count, cancellationToken);
        await destination.WriteAsync(data, (int)destinationOffset, cancellationToken);
    }

    public IMemoryStatistics GetStatistics()
    {
        return new TestMemoryStatistics();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test implementation of compute engine.
/// </summary>
internal class TestComputeEngine : IComputeEngine
{
    public ComputeBackendType[] AvailableBackends => new[] { ComputeBackendType.CPU };
    public ComputeBackendType DefaultBackend => ComputeBackendType.CPU;

    public async ValueTask<ICompiledKernel> CompileKernelAsync(string kernelSource, string? entryPoint = null, DotCompute.Core.Compute.CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken); // Simulate compilation
        return new TestCompiledKernel(kernelSource, entryPoint ?? "main");
    }

    public async ValueTask ExecuteAsync(ICompiledKernel kernel, object[] arguments, ComputeBackendType backendType, DotCompute.Core.Compute.ExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(5, cancellationToken); // Simulate execution
        // Simple test execution - just validate that we can run
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test implementation of buffer.
/// </summary>
internal class TestBuffer<T> : IBuffer<T> where T : unmanaged
{
    private T[] _data;

    public TestBuffer(int elementCount, MemoryLocation location, MemoryAccess access)
    {
        ElementCount = elementCount;
        Location = location;
        Access = access;
        SizeInBytes = elementCount * sizeof(T);
        _data = new T[elementCount];
    }

    public int ElementCount { get; }
    public long SizeInBytes { get; }
    public MemoryLocation Location { get; }
    public MemoryAccess Access { get; }

    public async ValueTask<T[]> ReadAsync(int offset = 0, int? count = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        var actualCount = count ?? (ElementCount - offset);
        var result = new T[actualCount];
        Array.Copy(_data, offset, result, 0, actualCount);
        return result;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<T> data, int offset = 0, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        data.Span.CopyTo(_data.AsSpan(offset));
    }

    public async ValueTask FillAsync(T value, int offset = 0, int? count = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        var actualCount = count ?? (ElementCount - offset);
        Array.Fill(_data, value, offset, actualCount);
    }

    public async ValueTask<IMemoryMapping<T>> MapAsync(MemoryMapMode mode = MemoryMapMode.ReadWrite, int offset = 0, int? count = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return new TestMemoryMapping<T>(_data, offset, count ?? (ElementCount - offset), mode);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test implementation of compiled kernel.
/// </summary>
internal class TestCompiledKernel : ICompiledKernel
{
    public TestCompiledKernel(string source, string entryPoint)
    {
        Id = Guid.NewGuid().ToString();
        Source = source;
        EntryPoint = entryPoint;
        Backend = ComputeBackendType.CPU;
        Metadata = new TestCompilationMetadata();
    }

    public string Id { get; }
    public string Source { get; }
    public string EntryPoint { get; }
    public ComputeBackendType Backend { get; }
    public DotCompute.Core.Compute.ICompilationMetadata Metadata { get; }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test implementation of compilation metadata.
/// </summary>
internal class TestCompilationMetadata : DotCompute.Core.Compute.ICompilationMetadata
{
    public DateTimeOffset CompilationTime { get; } = DateTimeOffset.UtcNow;
    public DotCompute.Core.Compute.CompilationOptions Options { get; } = DotCompute.Core.Compute.CompilationOptions.Default;
    public string[] Warnings { get; } = Array.Empty<string>();
    public DotCompute.Core.Compute.OptimizationLevel OptimizationLevel { get; } = DotCompute.Core.Compute.OptimizationLevel.Release;
}

/// <summary>
/// Test implementation of memory statistics.
/// </summary>
internal class TestMemoryStatistics : IMemoryStatistics
{
    public long TotalAllocatedBytes => 1024 * 1024; // 1MB
    public long AvailableBytes => 1024 * 1024 * 1024; // 1GB
    public long PeakUsageBytes => 2 * 1024 * 1024; // 2MB
    public int AllocationCount => 10;
    public double FragmentationPercentage => 5.0;
    public IReadOnlyDictionary<MemoryLocation, long> UsageByLocation => new Dictionary<MemoryLocation, long>
    {
        { MemoryLocation.Host, 512 * 1024 },
        { MemoryLocation.Device, 512 * 1024 }
    };
}

/// <summary>
/// Test implementation of memory mapping.
/// </summary>
internal class TestMemoryMapping<T> : IMemoryMapping<T> where T : unmanaged
{
    private readonly T[] _data;
    private readonly int _offset;
    private readonly int _count;

    public TestMemoryMapping(T[] data, int offset, int count, MemoryMapMode mode)
    {
        _data = data;
        _offset = offset;
        _count = count;
        Mode = mode;
        IsValid = true;
    }

    public Span<T> Span => _data.AsSpan(_offset, _count);
    public MemoryMapMode Mode { get; }
    public bool IsValid { get; private set; }

    public void Flush()
    {
        // No-op for test implementation
    }

    public void Dispose()
    {
        IsValid = false;
    }
}

/// <summary>
/// Test collection for integration tests.
/// </summary>
[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<SimpleIntegrationTestFixture>
{
    // This class is intentionally empty. It's used by xUnit to share the fixture across multiple test classes.
}