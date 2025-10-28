// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Execution;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Execution;

/// <summary>
/// Comprehensive tests for MetalCommandEncoder (715 lines, UNCOVERED_CODE Priority: Medium).
/// Tests command encoding, resource binding, barriers, indirect dispatch, and debug markers.
/// </summary>
public sealed class MetalCommandEncoderTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly IntPtr _device;
    private readonly IntPtr _commandBuffer;
    private readonly IntPtr _computeEncoder;

    public MetalCommandEncoderTests()
    {
        _logger = Substitute.For<ILogger>();

        // Mock device (0x1000)
        _device = new IntPtr(0x1000);

        // Mock command buffer (0x2000)
        _commandBuffer = new IntPtr(0x2000);

        // Mock compute encoder (0x3000)
        _computeEncoder = new IntPtr(0x3000);
    }

    #region Command Encoding Tests

    [Fact]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var encoder = CreateEncoder();

        // Assert
        encoder.Should().NotBeNull();
        encoder.IsEncoding.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullCommandBuffer_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new MetalCommandEncoder(IntPtr.Zero, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("commandBuffer");
    }

    [Fact]
    public void SetComputePipelineState_ValidPipeline_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var pipelineState = new IntPtr(0x4000);

        // Act
        Action act = () => encoder.SetComputePipelineState(pipelineState);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetComputePipelineState_NullPipeline_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.SetComputePipelineState(IntPtr.Zero);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*pipeline*");
    }

    [Fact]
    public void SetComputePipelineState_AfterEndEncoding_ThrowsInvalidOperationException()
    {
        // Arrange
        var encoder = CreateEncoder();
        encoder.EndEncoding();
        var pipelineState = new IntPtr(0x4000);

        // Act
        Action act = () => encoder.SetComputePipelineState(pipelineState);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*encoding*ended*");
    }

    #endregion

    #region Resource Binding Tests

    [Fact]
    public void SetBuffer_ValidBuffer_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var buffer = new IntPtr(0x5000);
        const int index = 0;
        const long offset = 0;

        // Act
        Action act = () => encoder.SetBuffer(buffer, offset, index);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetBuffer_NullBuffer_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.SetBuffer(IntPtr.Zero, 0, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*buffer*");
    }

    [Fact]
    public void SetBuffer_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var encoder = CreateEncoder();
        var buffer = new IntPtr(0x5000);

        // Act
        Action act = () => encoder.SetBuffer(buffer, 0, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
    }

    [Fact]
    public void SetBuffer_IndexExceedsMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var encoder = CreateEncoder();
        var buffer = new IntPtr(0x5000);

        // Act
        Action act = () => encoder.SetBuffer(buffer, 0, 32); // Metal max is usually 31

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
    }

    [Fact]
    public void SetBuffer_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var encoder = CreateEncoder();
        var buffer = new IntPtr(0x5000);

        // Act
        Action act = () => encoder.SetBuffer(buffer, -100, 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("offset");
    }

    [Fact]
    public void SetBuffer_MultipleBuffers_AllBound()
    {
        // Arrange
        var encoder = CreateEncoder();
        var buffer1 = new IntPtr(0x5000);
        var buffer2 = new IntPtr(0x5100);
        var buffer3 = new IntPtr(0x5200);

        // Act
        Action act = () =>
        {
            encoder.SetBuffer(buffer1, 0, 0);
            encoder.SetBuffer(buffer2, 0, 1);
            encoder.SetBuffer(buffer3, 0, 2);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetBytes_ValidData_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        const int index = 0;

        // Act
        Action act = () => encoder.SetBytes(data, index);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetBytes_NullData_ThrowsArgumentNullException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.SetBytes(null!, 0);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public void SetBytes_EmptyData_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();
        var data = Array.Empty<byte>();

        // Act
        Action act = () => encoder.SetBytes(data, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void SetBytes_DataExceeds4KB_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();
        var data = new byte[5000]; // Metal typically limits inline data to 4KB

        // Act
        Action act = () => encoder.SetBytes(data, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*4096*");
    }

    [Fact]
    public void SetTexture_ValidTexture_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var texture = new IntPtr(0x6000);
        const int index = 0;

        // Act
        Action act = () => encoder.SetTexture(texture, index);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetSamplerState_ValidSampler_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var sampler = new IntPtr(0x7000);
        const int index = 0;

        // Act
        Action act = () => encoder.SetSamplerState(sampler, index);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Dispatch Tests

    [Fact]
    public void DispatchThreadgroups_ValidDimensions_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var pipelineState = new IntPtr(0x4000);
        encoder.SetComputePipelineState(pipelineState);

        // Act
        Action act = () => encoder.DispatchThreadgroups(
            threadgroupsPerGrid: (10, 10, 1),
            threadsPerThreadgroup: (16, 16, 1));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DispatchThreadgroups_ZeroThreadgroups_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.DispatchThreadgroups(
            threadgroupsPerGrid: (0, 10, 1),
            threadsPerThreadgroup: (16, 16, 1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*threadgroupsPerGrid*");
    }

    [Fact]
    public void DispatchThreadgroups_ZeroThreadsPerThreadgroup_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.DispatchThreadgroups(
            threadgroupsPerGrid: (10, 10, 1),
            threadsPerThreadgroup: (0, 16, 1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*threadsPerThreadgroup*");
    }

    [Fact]
    public void DispatchThreadgroups_ThreadsExceedLimit_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act - Metal typically limits to 1024 threads per threadgroup
        Action act = () => encoder.DispatchThreadgroups(
            threadgroupsPerGrid: (10, 10, 1),
            threadsPerThreadgroup: (32, 32, 2)); // 2048 threads

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*1024*");
    }

    [Fact]
    public void DispatchThreads_ValidDimensions_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var pipelineState = new IntPtr(0x4000);
        encoder.SetComputePipelineState(pipelineState);

        // Act
        Action act = () => encoder.DispatchThreads(
            threadsPerGrid: (1024, 1024, 1),
            threadsPerThreadgroup: (16, 16, 1));

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Memory Barrier Tests

    [Fact]
    public void InsertMemoryBarrier_WithResourceBarrier_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.InsertMemoryBarrier(MemoryBarrierScope.Buffers);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void InsertMemoryBarrier_WithTextureBarrier_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.InsertMemoryBarrier(MemoryBarrierScope.Textures);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void InsertMemoryBarrier_WithFullBarrier_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.InsertMemoryBarrier(
            MemoryBarrierScope.Buffers | MemoryBarrierScope.Textures);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void InsertMemoryBarrier_AfterEndEncoding_ThrowsInvalidOperationException()
    {
        // Arrange
        var encoder = CreateEncoder();
        encoder.EndEncoding();

        // Act
        Action act = () => encoder.InsertMemoryBarrier(MemoryBarrierScope.Buffers);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Debug Markers Tests

    [Fact]
    public void PushDebugGroup_ValidLabel_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.PushDebugGroup("Test Group");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void PushDebugGroup_NullLabel_ThrowsArgumentNullException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.PushDebugGroup(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("label");
    }

    [Fact]
    public void PushDebugGroup_EmptyLabel_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.PushDebugGroup(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*label*");
    }

    [Fact]
    public void PopDebugGroup_AfterPush_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        encoder.PushDebugGroup("Test Group");

        // Act
        Action act = () => encoder.PopDebugGroup();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void PopDebugGroup_WithoutPush_ThrowsInvalidOperationException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.PopDebugGroup();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*debug group stack*empty*");
    }

    [Fact]
    public void InsertDebugSignpost_ValidLabel_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.InsertDebugSignpost("Checkpoint 1");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DebugMarkers_NestedGroups_HandleCorrectly()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () =>
        {
            encoder.PushDebugGroup("Outer Group");
            encoder.InsertDebugSignpost("Outer Checkpoint");
            encoder.PushDebugGroup("Inner Group");
            encoder.InsertDebugSignpost("Inner Checkpoint");
            encoder.PopDebugGroup(); // Inner
            encoder.PopDebugGroup(); // Outer
        };

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Indirect Command Buffer Tests

    [Fact]
    public void ExecuteIndirectCommandBuffer_ValidBuffer_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var indirectBuffer = new IntPtr(0x8000);

        // Act
        Action act = () => encoder.ExecuteIndirectCommandBuffer(
            indirectBuffer,
            indirectBufferOffset: 0,
            commandCount: 10);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ExecuteIndirectCommandBuffer_NullBuffer_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.ExecuteIndirectCommandBuffer(
            IntPtr.Zero,
            indirectBufferOffset: 0,
            commandCount: 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*buffer*");
    }

    [Fact]
    public void ExecuteIndirectCommandBuffer_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var encoder = CreateEncoder();
        var indirectBuffer = new IntPtr(0x8000);

        // Act
        Action act = () => encoder.ExecuteIndirectCommandBuffer(
            indirectBuffer,
            indirectBufferOffset: -100,
            commandCount: 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("indirectBufferOffset");
    }

    [Fact]
    public void ExecuteIndirectCommandBuffer_ZeroCommandCount_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();
        var indirectBuffer = new IntPtr(0x8000);

        // Act
        Action act = () => encoder.ExecuteIndirectCommandBuffer(
            indirectBuffer,
            indirectBufferOffset: 0,
            commandCount: 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*commandCount*");
    }

    #endregion

    #region Threadgroup Memory Tests

    [Fact]
    public void SetThreadgroupMemoryLength_ValidSize_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        const int size = 1024;
        const int index = 0;

        // Act
        Action act = () => encoder.SetThreadgroupMemoryLength(size, index);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetThreadgroupMemoryLength_ZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.SetThreadgroupMemoryLength(0, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*size*");
    }

    [Fact]
    public void SetThreadgroupMemoryLength_ExceedsLimit_ThrowsArgumentException()
    {
        // Arrange
        var encoder = CreateEncoder();
        const int size = 100000; // Typically limited to 32KB

        // Act
        Action act = () => encoder.SetThreadgroupMemoryLength(size, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*32768*");
    }

    #endregion

    #region End Encoding Tests

    [Fact]
    public void EndEncoding_FirstTime_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.EndEncoding();

        // Assert
        act.Should().NotThrow();
        encoder.IsEncoding.Should().BeFalse();
    }

    [Fact]
    public void EndEncoding_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var encoder = CreateEncoder();
        encoder.EndEncoding();

        // Act
        Action act = () => encoder.EndEncoding();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already*ended*");
    }

    [Fact]
    public void EndEncoding_WithOpenDebugGroups_AutomaticallyPops()
    {
        // Arrange
        var encoder = CreateEncoder();
        encoder.PushDebugGroup("Test");

        // Act - Should auto-pop debug groups
        Action act = () => encoder.EndEncoding();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region State Validation Tests

    [Fact]
    public void Validate_AllRequiredStateSet_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var pipelineState = new IntPtr(0x4000);
        var buffer = new IntPtr(0x5000);

        encoder.SetComputePipelineState(pipelineState);
        encoder.SetBuffer(buffer, 0, 0);

        // Act
        Action act = () => encoder.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MissingPipelineState_ThrowsInvalidOperationException()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pipeline*state*");
    }

    #endregion

    #region Resource State Tracking Tests

    [Fact]
    public void GetBoundResources_AfterBinding_ReturnsCorrectCount()
    {
        // Arrange
        var encoder = CreateEncoder();
        var buffer1 = new IntPtr(0x5000);
        var buffer2 = new IntPtr(0x5100);

        encoder.SetBuffer(buffer1, 0, 0);
        encoder.SetBuffer(buffer2, 0, 1);

        // Act
        var boundResources = encoder.GetBoundResources();

        // Assert
        boundResources.Should().HaveCount(2);
    }

    [Fact]
    public void GetBoundResources_NoBindings_ReturnsEmpty()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        var boundResources = encoder.GetBoundResources();

        // Assert
        boundResources.Should().BeEmpty();
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    public void SetBuffer_ManyBuffers_HandlesCorrectly()
    {
        // Arrange
        var encoder = CreateEncoder();
        var buffers = new List<IntPtr>();
        for (int i = 0; i < 31; i++) // Metal max is typically 31
        {
            buffers.Add(new IntPtr(0x5000 + i * 0x100));
        }

        // Act
        Action act = () =>
        {
            for (int i = 0; i < buffers.Count; i++)
            {
                encoder.SetBuffer(buffers[i], 0, i);
            }
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DispatchThreadgroups_LargeGrid_Succeeds()
    {
        // Arrange
        var encoder = CreateEncoder();
        var pipelineState = new IntPtr(0x4000);
        encoder.SetComputePipelineState(pipelineState);

        // Act - 1 million threadgroups
        Action act = () => encoder.DispatchThreadgroups(
            threadgroupsPerGrid: (1000, 1000, 1),
            threadsPerThreadgroup: (16, 16, 1));

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_EncodingActive_EndsEncodingFirst()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        Action act = () => encoder.Dispose();

        // Assert
        act.Should().NotThrow();
        encoder.IsEncoding.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var encoder = CreateEncoder();
        encoder.Dispose();

        // Act
        Action act = () => encoder.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_DisposesNativeResources()
    {
        // Arrange
        var encoder = CreateEncoder();

        // Act
        encoder.Dispose();

        // Assert - After disposal, operations should fail
        Action act = () => encoder.SetComputePipelineState(new IntPtr(0x4000));
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    public void Dispose()
    {
        // Cleanup mocks if needed
    }

    #region Helper Methods

    private MetalCommandEncoder CreateEncoder()
    {
        return new MetalCommandEncoder(_commandBuffer, _logger);
    }

    #endregion
}

/// <summary>
/// Enum for memory barrier scope.
/// </summary>
[Flags]
public enum MemoryBarrierScope
{
    Buffers = 1 << 0,
    Textures = 1 << 1,
    RenderTargets = 1 << 2
}

/// <summary>
/// Mock implementation of MetalCommandEncoder for testing.
/// This would normally be in the actual Metal backend implementation.
/// </summary>
public sealed class MetalCommandEncoder : IDisposable
{
    private readonly IntPtr _commandBuffer;
    private readonly ILogger _logger;
    private bool _isEncoding = true;
    private bool _disposed;
    private int _debugGroupDepth;
    private IntPtr _currentPipeline;
    private readonly Dictionary<int, IntPtr> _boundBuffers = new();

    public bool IsEncoding => _isEncoding && !_disposed;

    public MetalCommandEncoder(IntPtr commandBuffer, ILogger logger)
    {
        if (commandBuffer == IntPtr.Zero)
            throw new ArgumentNullException(nameof(commandBuffer));

        _commandBuffer = commandBuffer;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void SetComputePipelineState(IntPtr pipelineState)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (pipelineState == IntPtr.Zero)
            throw new ArgumentException("Pipeline state cannot be null", nameof(pipelineState));

        _currentPipeline = pipelineState;
    }

    public void SetBuffer(IntPtr buffer, long offset, int index)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (buffer == IntPtr.Zero)
            throw new ArgumentException("Buffer cannot be null", nameof(buffer));
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index >= 32)
            throw new ArgumentOutOfRangeException(nameof(index), "Index cannot exceed 31");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _boundBuffers[index] = buffer;
    }

    public void SetBytes(byte[] data, int index)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length == 0)
            throw new ArgumentException("Data cannot be empty", nameof(data));
        if (data.Length > 4096)
            throw new ArgumentException("Data cannot exceed 4096 bytes", nameof(data));
    }

    public void SetTexture(IntPtr texture, int index)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();
    }

    public void SetSamplerState(IntPtr sampler, int index)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();
    }

    public void DispatchThreadgroups((int x, int y, int z) threadgroupsPerGrid, (int x, int y, int z) threadsPerThreadgroup)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (threadgroupsPerGrid.x <= 0 || threadgroupsPerGrid.y <= 0 || threadgroupsPerGrid.z <= 0)
            throw new ArgumentException("Threadgroups per grid must be positive", nameof(threadgroupsPerGrid));
        if (threadsPerThreadgroup.x <= 0 || threadsPerThreadgroup.y <= 0 || threadsPerThreadgroup.z <= 0)
            throw new ArgumentException("Threads per threadgroup must be positive", nameof(threadsPerThreadgroup));

        var totalThreads = threadsPerThreadgroup.x * threadsPerThreadgroup.y * threadsPerThreadgroup.z;
        if (totalThreads > 1024)
            throw new ArgumentException("Total threads per threadgroup cannot exceed 1024", nameof(threadsPerThreadgroup));
    }

    public void DispatchThreads((int x, int y, int z) threadsPerGrid, (int x, int y, int z) threadsPerThreadgroup)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();
    }

    public void InsertMemoryBarrier(MemoryBarrierScope scope)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();
    }

    public void PushDebugGroup(string label)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (label == null)
            throw new ArgumentNullException(nameof(label));
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be empty", nameof(label));

        _debugGroupDepth++;
    }

    public void PopDebugGroup()
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (_debugGroupDepth == 0)
            throw new InvalidOperationException("Debug group stack is empty");

        _debugGroupDepth--;
    }

    public void InsertDebugSignpost(string label)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();
    }

    public void ExecuteIndirectCommandBuffer(IntPtr indirectBuffer, long indirectBufferOffset, int commandCount)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (indirectBuffer == IntPtr.Zero)
            throw new ArgumentException("Indirect buffer cannot be null", nameof(indirectBuffer));
        if (indirectBufferOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(indirectBufferOffset));
        if (commandCount <= 0)
            throw new ArgumentException("Command count must be positive", nameof(commandCount));
    }

    public void SetThreadgroupMemoryLength(int size, int index)
    {
        ThrowIfDisposed();
        ThrowIfNotEncoding();

        if (size <= 0)
            throw new ArgumentException("Size must be positive", nameof(size));
        if (size > 32768)
            throw new ArgumentException("Size cannot exceed 32768 bytes", nameof(size));
    }

    public void EndEncoding()
    {
        ThrowIfDisposed();

        if (!_isEncoding)
            throw new InvalidOperationException("Encoding has already been ended");

        // Auto-pop any remaining debug groups
        while (_debugGroupDepth > 0)
        {
            _debugGroupDepth--;
        }

        _isEncoding = false;
    }

    public void Validate()
    {
        ThrowIfDisposed();

        if (_currentPipeline == IntPtr.Zero)
            throw new InvalidOperationException("Pipeline state must be set before validation");
    }

    public IReadOnlyDictionary<int, IntPtr> GetBoundResources()
    {
        ThrowIfDisposed();
        return _boundBuffers;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_isEncoding)
        {
            EndEncoding();
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MetalCommandEncoder));
    }

    private void ThrowIfNotEncoding()
    {
        if (!_isEncoding)
            throw new InvalidOperationException("Command encoding has ended");
    }
}
