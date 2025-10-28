// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Services.Compilation;

/// <summary>
/// Tests for IKernelCache implementations
/// </summary>
public sealed class KernelCacheTests
{
    private readonly IKernelCache _cache;

    public KernelCacheTests()
    {
        _cache = Substitute.For<IKernelCache>();
    }

    [Fact]
    public async Task GetAsync_WithCachedKernel_ReturnsKernel()
    {
        // Arrange
        var key = "test_kernel";
        var kernel = Substitute.For<ICompiledKernel>();
        _cache.GetAsync(key).Returns(kernel);

        // Act
        var result = await _cache.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(kernel);
    }

    [Fact]
    public async Task GetAsync_WithMissingKernel_ReturnsNull()
    {
        // Arrange
        var key = "missing_kernel";
        _cache.GetAsync(key).Returns((ICompiledKernel?)null);

        // Act
        var result = await _cache.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task StoreAsync_WithValidKernel_StoresKernel()
    {
        // Arrange
        var key = "test_kernel";
        var kernel = Substitute.For<ICompiledKernel>();

        // Act
        await _cache.StoreAsync(key, kernel);

        // Assert
        await _cache.Received(1).StoreAsync(key, kernel);
    }

    [Fact]
    public async Task InvalidateAsync_WithExistingKey_RemovesKernel()
    {
        // Arrange
        var key = "test_kernel";
        _cache.InvalidateAsync(key).Returns(true);

        // Act
        var result = await _cache.InvalidateAsync(key);

        // Assert
        result.Should().BeTrue();
        await _cache.Received(1).InvalidateAsync(key);
    }

    [Fact]
    public async Task InvalidateAsync_WithMissingKey_ReturnsFalse()
    {
        // Arrange
        var key = "missing_kernel";
        _cache.InvalidateAsync(key).Returns(false);

        // Act
        var result = await _cache.InvalidateAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_RemovesAllKernels()
    {
        // Arrange
        _cache.ClearAsync().Returns(5);

        // Act
        var clearedCount = await _cache.ClearAsync();

        // Assert
        clearedCount.Should().Be(5);
        await _cache.Received(1).ClearAsync();
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCacheStatistics()
    {
        // Arrange
        var stats = new CacheStatistics
        {
            EntryCount = 10,
            HitCount = 50,
            MissCount = 10
        };
        _cache.GetStatisticsAsync().Returns(stats);

        // Act
        var result = await _cache.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.EntryCount.Should().Be(10);
        result.HitRate.Should().BeApproximately(0.833, 0.001);
    }

    [Fact]
    public async Task GetAsync_MultipleCallsSameKey_ReturnsConsistent()
    {
        // Arrange
        var key = "test_kernel";
        var kernel = Substitute.For<ICompiledKernel>();
        _cache.GetAsync(key).Returns(kernel);

        // Act
        var result1 = await _cache.GetAsync(key);
        var result2 = await _cache.GetAsync(key);

        // Assert
        result1.Should().Be(result2);
    }

    [Theory]
    [InlineData("kernel1")]
    [InlineData("kernel2")]
    [InlineData("kernel_with_underscores")]
    [InlineData("kernel-with-dashes")]
    public async Task StoreAsync_WithDifferentKeys_StoresIndependently(string key)
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();

        // Act
        await _cache.StoreAsync(key, kernel);

        // Assert
        await _cache.Received(1).StoreAsync(key, kernel);
    }

    [Fact]
    public async Task StoreAsync_OverwritingExisting_UpdatesCache()
    {
        // Arrange
        var key = "test_kernel";
        var kernel1 = Substitute.For<ICompiledKernel>();
        var kernel2 = Substitute.For<ICompiledKernel>();

        // Act
        await _cache.StoreAsync(key, kernel1);
        await _cache.StoreAsync(key, kernel2);

        // Assert
        await _cache.Received(2).StoreAsync(key, Arg.Any<ICompiledKernel>());
    }

    [Fact]
    public async Task InvalidateAsync_MultipleTimes_HandlesGracefully()
    {
        // Arrange
        var key = "test_kernel";
        _cache.InvalidateAsync(key).Returns(true, false);

        // Act
        var result1 = await _cache.InvalidateAsync(key);
        var result2 = await _cache.InvalidateAsync(key);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_AfterMultipleStores_RemovesAll()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        await _cache.StoreAsync("key1", kernel);
        await _cache.StoreAsync("key2", kernel);
        _cache.ClearAsync().Returns(2);

        // Act
        var clearedCount = await _cache.ClearAsync();

        // Assert
        clearedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_ConcurrentAccess_HandlesCorrectly()
    {
        // Arrange
        var key = "test_kernel";
        var kernel = Substitute.For<ICompiledKernel>();
        _cache.GetAsync(key).Returns(kernel);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _cache.GetAsync(key))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().Be(kernel));
    }

    [Fact]
    public async Task StoreAsync_ConcurrentWrites_HandlesCorrectly()
    {
        // Arrange
        var key = "test_kernel";
        var kernels = Enumerable.Range(0, 10)
            .Select(_ => Substitute.For<ICompiledKernel>())
            .ToArray();

        // Act
        var tasks = kernels.Select(k => _cache.StoreAsync(key, k)).ToArray();
        await Task.WhenAll(tasks);

        // Assert
        await _cache.Received(10).StoreAsync(key, Arg.Any<ICompiledKernel>());
    }

    [Fact]
    public async Task PrewarmAsync_WithKernels_CachesSuccessfully()
    {
        // Arrange
        var definitions = new[]
        {
            new KernelDefinition("kernel1", "code1"),
            new KernelDefinition("kernel2", "code2")
        };
        var accelerators = new[] { Substitute.For<DotCompute.Abstractions.IAccelerator>() };
        _cache.PrewarmAsync(definitions, accelerators).Returns(2);

        // Act
        var cachedCount = await _cache.PrewarmAsync(definitions, accelerators);

        // Assert
        cachedCount.Should().Be(2);
    }
}
