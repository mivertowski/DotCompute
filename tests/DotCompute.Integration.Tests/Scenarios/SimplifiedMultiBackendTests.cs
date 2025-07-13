// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core;
using DotCompute.Core.Memory;
using DotCompute.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DotCompute.Integration.Tests.Scenarios;

[Collection(nameof(IntegrationTestCollection))]
public class SimplifiedMultiBackendTests
{
    private readonly SimpleIntegrationTestFixture _fixture;

    public SimplifiedMultiBackendTests(SimpleIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CPUBuffer_Creation_Should_Work()
    {
        // Arrange
        var inputData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
        
        // Act
        using var buffer = await _fixture.CreateBufferAsync(inputData, MemoryLocation.Host);
        var result = await buffer.ReadAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(inputData.Length);
        result.Should().BeEquivalentTo(inputData);
    }

    [Fact]
    public async Task MemoryCopy_Should_Work()
    {
        // Arrange
        var inputData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
        using var sourceBuffer = await _fixture.CreateBufferAsync(inputData, MemoryLocation.Host);
        using var destBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(inputData.Length, MemoryLocation.Host);

        // Act
        await _fixture.MemoryManager.CopyAsync(sourceBuffer, destBuffer);
        var result = await destBuffer.ReadAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(inputData.Length);
        result.Should().BeEquivalentTo(inputData);
    }

    [Fact]
    public async Task KernelCompilation_Should_Work()
    {
        // Arrange
        const string simpleKernel = @"
            kernel void simple_add(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = input[i] + 1.0f;
                }
            }";

        // Act
        using var kernel = await _fixture.ComputeEngine.CompileKernelAsync(simpleKernel, "simple_add");

        // Assert
        kernel.Should().NotBeNull();
        kernel.Id.Should().NotBeNullOrEmpty();
        kernel.Source.Should().Be(simpleKernel);
        kernel.EntryPoint.Should().Be("simple_add");
        kernel.Backend.Should().Be(ComputeBackendType.CPU);
    }

    [Fact]
    public async Task MemoryStatistics_Should_Be_Available()
    {
        // Arrange & Act
        var stats = _fixture.MemoryManager.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalAllocatedBytes.Should().BeGreaterThan(0);
        stats.AvailableBytes.Should().BeGreaterThan(0);
        stats.AllocationCount.Should().BeGreaterOrEqualTo(0);
        stats.FragmentationPercentage.Should().BeInRange(0, 100);
        stats.UsageByLocation.Should().NotBeNull();
    }

    [Fact]
    public async Task BufferFill_Operation_Should_Work()
    {
        // Arrange
        const int elementCount = 10;
        const float fillValue = 42.0f;
        using var buffer = await _fixture.MemoryManager.CreateBufferAsync<float>(elementCount, MemoryLocation.Host);

        // Act
        await buffer.FillAsync(fillValue);
        var result = await buffer.ReadAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(elementCount);
        result.Should().AllSatisfy(x => x.Should().Be(fillValue));
    }

    [Fact]
    public async Task AvailableBackends_Should_Include_CPU()
    {
        // Act
        var availableBackends = _fixture.ComputeEngine.AvailableBackends;
        var defaultBackend = _fixture.ComputeEngine.DefaultBackend;

        // Assert
        availableBackends.Should().NotBeNull();
        availableBackends.Should().Contain(ComputeBackendType.CPU);
        defaultBackend.Should().Be(ComputeBackendType.CPU);
    }

    [Fact]
    public async Task MemoryLocations_Should_Be_Available()
    {
        // Act
        var locations = _fixture.MemoryManager.AvailableLocations;

        // Assert
        locations.Should().NotBeNull();
        locations.Should().Contain(MemoryLocation.Host);
        locations.Length.Should().BeGreaterThan(0);
    }
}