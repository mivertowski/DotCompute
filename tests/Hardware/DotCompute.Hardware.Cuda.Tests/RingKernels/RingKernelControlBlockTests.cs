// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Unit tests for RingKernelControlBlock structure.
/// Tests memory layout, size, alignment, and value semantics.
/// </summary>
public class RingKernelControlBlockTests
{
    [Fact(DisplayName = "Control block should be exactly 64 bytes (cache-line aligned)")]
    public void ControlBlock_ShouldBe64Bytes()
    {
        // Arrange & Act
        var size = Marshal.SizeOf<RingKernelControlBlock>();

        // Assert
        size.Should().Be(64, "control block must be cache-line aligned for optimal GPU performance");
    }

    [Fact(DisplayName = "Control block should have Pack=4 alignment")]
    public void ControlBlock_ShouldHavePack4Alignment()
    {
        // Act
        var layoutAttribute = typeof(RingKernelControlBlock).StructLayoutAttribute;

        // Assert
        layoutAttribute.Should().NotBeNull("StructLayoutAttribute should be applied");
        layoutAttribute!.Value.Should().Be(LayoutKind.Sequential, "should use sequential layout");
        layoutAttribute.Pack.Should().Be(4, "4-byte packing ensures correct alignment for atomics");
    }

    [Fact(DisplayName = "CreateInactive should initialize all fields to zero except timestamp")]
    public void CreateInactive_ShouldInitializeCorrectly()
    {
        // Act
        var controlBlock = RingKernelControlBlock.CreateInactive();

        // Assert
        controlBlock.IsActive.Should().Be(0);
        controlBlock.ShouldTerminate.Should().Be(0);
        controlBlock.HasTerminated.Should().Be(0);
        controlBlock.ErrorsEncountered.Should().Be(0);
        controlBlock.MessagesProcessed.Should().Be(0);
        controlBlock.LastActivityTicks.Should().BeGreaterThan(0, "timestamp should be set to current time");
        controlBlock.InputQueueHeadPtr.Should().Be(0);
        controlBlock.InputQueueTailPtr.Should().Be(0);
        controlBlock.OutputQueueHeadPtr.Should().Be(0);
        controlBlock.OutputQueueTailPtr.Should().Be(0);
    }

    [Fact(DisplayName = "Equals should compare all fields correctly")]
    public void Equals_ShouldCompareAllFields()
    {
        // Arrange
        var block1 = new RingKernelControlBlock
        {
            IsActive = 1,
            ShouldTerminate = 0,
            HasTerminated = 0,
            ErrorsEncountered = 2,
            MessagesProcessed = 100,
            LastActivityTicks = 12345678,
            InputQueueHeadPtr = 0x1000,
            InputQueueTailPtr = 0x2000,
            OutputQueueHeadPtr = 0x3000,
            OutputQueueTailPtr = 0x4000
        };

        var block2 = new RingKernelControlBlock
        {
            IsActive = 1,
            ShouldTerminate = 0,
            HasTerminated = 0,
            ErrorsEncountered = 2,
            MessagesProcessed = 100,
            LastActivityTicks = 12345678,
            InputQueueHeadPtr = 0x1000,
            InputQueueTailPtr = 0x2000,
            OutputQueueHeadPtr = 0x3000,
            OutputQueueTailPtr = 0x4000
        };

        // Act & Assert
        block1.Equals(block2).Should().BeTrue("identical blocks should be equal");
        (block1 == block2).Should().BeTrue("== operator should work");
    }

    [Fact(DisplayName = "Equals should detect differences in any field")]
    public void Equals_ShouldDetectDifferences()
    {
        // Arrange
        var baseBlock = RingKernelControlBlock.CreateInactive();

        var differentActive = baseBlock;
        differentActive.IsActive = 1;

        var differentMessages = baseBlock;
        differentMessages.MessagesProcessed = 42;

        var differentPointers = baseBlock;
        differentPointers.InputQueueHeadPtr = 0x5000;

        // Act & Assert
        baseBlock.Equals(differentActive).Should().BeFalse("IsActive differs");
        baseBlock.Equals(differentMessages).Should().BeFalse("MessagesProcessed differs");
        baseBlock.Equals(differentPointers).Should().BeFalse("InputQueueHeadPtr differs");
    }

    [Fact(DisplayName = "GetHashCode should be consistent for equal objects")]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var block1 = new RingKernelControlBlock
        {
            IsActive = 1,
            MessagesProcessed = 42,
            InputQueueHeadPtr = 0x1000
        };

        var block2 = new RingKernelControlBlock
        {
            IsActive = 1,
            MessagesProcessed = 42,
            InputQueueHeadPtr = 0x1000
        };

        // Act
        var hash1 = block1.GetHashCode();
        var hash2 = block2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2, "equal objects must have equal hash codes");
    }

    [Fact(DisplayName = "Inequality operator should work correctly")]
    public void InequalityOperator_ShouldWork()
    {
        // Arrange
        var block1 = RingKernelControlBlock.CreateInactive();
        var block2 = RingKernelControlBlock.CreateInactive();
        block2.IsActive = 1;

        // Act & Assert
        (block1 != block2).Should().BeTrue("different blocks should be not equal");
    }

    [Fact(DisplayName = "Control block fields should be at correct offsets")]
    public void ControlBlock_FieldsShouldHaveCorrectOffsets()
    {
        // Arrange
        var block = new RingKernelControlBlock();

        // Act - Get field offsets via Marshal
        var isActiveOffset = Marshal.OffsetOf<RingKernelControlBlock>(nameof(RingKernelControlBlock.IsActive));
        var shouldTerminateOffset = Marshal.OffsetOf<RingKernelControlBlock>(nameof(RingKernelControlBlock.ShouldTerminate));
        var hasTerminatedOffset = Marshal.OffsetOf<RingKernelControlBlock>(nameof(RingKernelControlBlock.HasTerminated));

        // Assert - Critical offsets for GPU kernel access
        isActiveOffset.ToInt32().Should().Be(0, "IsActive must be at offset 0");
        shouldTerminateOffset.ToInt32().Should().Be(4, "ShouldTerminate must be at offset 4");
        hasTerminatedOffset.ToInt32().Should().Be(8, "HasTerminated must be at offset 8");
    }

    [Fact(DisplayName = "Control block should support value semantics")]
    public void ControlBlock_ShouldSupportValueSemantics()
    {
        // Arrange
        var original = new RingKernelControlBlock
        {
            IsActive = 1,
            MessagesProcessed = 100
        };

        // Act - Copy by value
        var copy = original;
        copy.IsActive = 0;

        // Assert - Original should be unchanged (value type behavior)
        original.IsActive.Should().Be(1, "modifying copy should not affect original");
    }

    [Theory(DisplayName = "Control block should handle extreme values correctly")]
    [InlineData(int.MaxValue, long.MaxValue)]
    [InlineData(int.MinValue, long.MinValue)]
    [InlineData(0, 0)]
    public void ControlBlock_ShouldHandleExtremeValues(int intValue, long longValue)
    {
        // Act
        var block = new RingKernelControlBlock
        {
            IsActive = intValue,
            MessagesProcessed = longValue,
            InputQueueHeadPtr = longValue
        };

        // Assert
        block.IsActive.Should().Be(intValue);
        block.MessagesProcessed.Should().Be(longValue);
        block.InputQueueHeadPtr.Should().Be(longValue);
    }
}
