// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests.Memory.P2P
{
    /// <summary>
    /// Mock accelerator implementation for P2P testing.
    /// </summary>
    internal sealed class MockAccelerator : IAccelerator
    {
        public MockAccelerator(string id, string name, AcceleratorType type)
        {
            Info = new AcceleratorInfo
            {
                Id = id,
                Name = name,
                Vendor = "Mock",
                Type = type,
                ComputeCapability = "1.0",
                MemorySize = 8L * 1024 * 1024 * 1024, // 8GB
                MaxWorkGroupSize = 1024,
                MaxThreadsPerWorkGroup = 1024
            };
        }

        public AcceleratorInfo Info { get; }
        public AcceleratorType Type => Info.Type;
        public bool IsDisposed => false;

        public Task<IBuffer<T>> AllocateBufferAsync<T>(int length, MemoryOptions? options = null, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return Task.FromResult<IBuffer<T>>(new MockBuffer<T>(this, length));
        }

        public Task<IExecutionContext> CreateExecutionContextAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Mock accelerator does not support execution contexts");
        }

        public void Dispose()
        {
            // No cleanup needed for mock
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Mock buffer implementation for P2P testing.
    /// </summary>
    internal sealed class MockBuffer<T> : IBuffer<T> where T : unmanaged
    {
        public MockBuffer(IAccelerator accelerator, int length)
        {
            Accelerator = accelerator;
            Length = length;
            SizeInBytes = length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        }

        public int Length { get; }
        public long SizeInBytes { get; }
        public IAccelerator Accelerator { get; }
        public MemoryOptions Options => MemoryOptions.Default;
        public bool IsDisposed => false;

        public ValueTask CopyFromHostAsync<TData>(TData[] source, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            // Simulate async copy with small delay
            return new ValueTask(Task.Delay(1, cancellationToken));
        }

        public ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            return new ValueTask(Task.Delay(1, cancellationToken));
        }

        public Task CopyToHostAsync<TData>(TData[] destination, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            return Task.Delay(1, cancellationToken);
        }

        public ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            return new ValueTask(Task.Delay(1, cancellationToken));
        }

        public ValueTask CopyToAsync(IBuffer<T> destination, CancellationToken cancellationToken = default)
        {
            // Simulate P2P transfer timing based on devices
            var isP2P = destination.Accelerator != Accelerator;
            var delay = isP2P ? 5 : 1; // P2P takes longer
            return new ValueTask(Task.Delay(delay, cancellationToken));
        }

        public ValueTask CopyToAsync(int sourceOffset, IBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        {
            var isP2P = destination.Accelerator != Accelerator;
            var delay = isP2P ? 3 : 1;
            return new ValueTask(Task.Delay(delay, cancellationToken));
        }

        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        {
            return new ValueTask(Task.Delay(1, cancellationToken));
        }

        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        {
            return new ValueTask(Task.Delay(1, cancellationToken));
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return Task.Delay(1, cancellationToken);
        }

        public IBuffer<T> Slice(int offset, int count)
        {
            if (offset < 0 || count <= 0 || offset + count > Length)
                throw new ArgumentOutOfRangeException();

            return new MockBuffer<T>(Accelerator, count);
        }

        public IBuffer<TNew> AsType<TNew>() where TNew : unmanaged
        {
            var oldElementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var newElementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
            var newElementCount = (int)(SizeInBytes / newElementSize);
            
            return new MockBuffer<TNew>(Accelerator, newElementCount);
        }

        public MappedMemory<T> Map(MapMode mode) => default;
        public MappedMemory<T> MapRange(int offset, int count, MapMode mode) => default;
        public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => default;

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}