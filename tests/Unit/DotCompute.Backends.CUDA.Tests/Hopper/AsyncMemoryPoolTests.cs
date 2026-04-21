// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA.Hopper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Hopper;

/// <summary>
/// Unit tests for <see cref="AsyncMemoryPool"/>. These tests validate the C# argument/state
/// logic without invoking the CUDA driver. Hardware-backed tests live under the Hardware test
/// projects and exercise the actual allocator.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HopperAsyncMem")]
public sealed class AsyncMemoryPoolTests
{
    [Fact]
    public void Ctor_DefaultStream_IsZero()
    {
        using var pool = new AsyncMemoryPool(logger: NullLogger.Instance);
        pool.PreferredStream.Should().Be(IntPtr.Zero);
        pool.TotalAllocatedBytes.Should().Be(0);
        pool.OutstandingAllocationCount.Should().Be(0);
    }

    [Fact]
    public void Ctor_CustomStream_IsStored()
    {
        var stream = (IntPtr)0x1234;
        using var pool = new AsyncMemoryPool(stream, NullLogger.Instance);
        pool.PreferredStream.Should().Be(stream);
    }

    [Fact]
    public async Task AllocAsync_ZeroBytes_Throws()
    {
        using var pool = new AsyncMemoryPool();
        var act = async () => await pool.AllocAsync(0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AllocAsync_AfterDispose_Throws()
    {
        var pool = new AsyncMemoryPool();
        pool.Dispose();

        var act = async () => await pool.AllocAsync(1024);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task AllocAsync_CancelledToken_Throws()
    {
        using var pool = new AsyncMemoryPool();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await pool.AllocAsync(1024, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FreeAsync_NullPointer_Throws()
    {
        using var pool = new AsyncMemoryPool();
        var act = async () => await pool.FreeAsync(IntPtr.Zero);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FreeAsync_AfterDispose_Throws()
    {
        var pool = new AsyncMemoryPool();
        pool.Dispose();

        var act = async () => await pool.FreeAsync((IntPtr)0x1);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var pool = new AsyncMemoryPool();
        pool.Dispose();

        // Second dispose must not throw.
        var act = () => pool.Dispose();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(6, 0, true)]
    [InlineData(9, 0, true)]
    [InlineData(5, 2, false)]
    public void IsSupported_DelegatesToHopperFeatures(int major, int minor, bool expected)
    {
        AsyncMemoryPool.IsSupported(major, minor).Should().Be(expected);
    }
}
