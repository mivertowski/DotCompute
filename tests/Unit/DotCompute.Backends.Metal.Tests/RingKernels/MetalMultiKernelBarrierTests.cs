// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.RingKernels;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalMultiKernelBarrier structure.
/// </summary>
public sealed class MetalMultiKernelBarrierTests
{
    [Fact]
    public void Create_Should_Initialize_With_Correct_Participant_Count()
    {
        // Act
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Assert
        Assert.Equal(10, barrier.ParticipantCount);
        Assert.Equal(0, barrier.ArrivedCount);
        Assert.Equal(0, barrier.Generation);
        Assert.Equal(MetalMultiKernelBarrier.BARRIER_FLAG_ACTIVE, barrier.Flags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void Create_Should_Throw_When_Participant_Count_Invalid(int participantCount)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetalMultiKernelBarrier.Create(participantCount));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(256)]
    [InlineData(65535)]
    public void Create_Should_Accept_Valid_Participant_Counts(int participantCount)
    {
        // Act
        var barrier = MetalMultiKernelBarrier.Create(participantCount);

        // Assert
        Assert.Equal(participantCount, barrier.ParticipantCount);
    }

    [Fact]
    public void IsTimedOut_Should_Return_True_When_Timeout_Flag_Set()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);
        barrier.Flags |= MetalMultiKernelBarrier.BARRIER_FLAG_TIMEOUT;

        // Act & Assert
        Assert.True(barrier.IsTimedOut);
    }

    [Fact]
    public void IsTimedOut_Should_Return_False_When_No_Timeout_Flag()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Act & Assert
        Assert.False(barrier.IsTimedOut);
    }

    [Fact]
    public void IsFailed_Should_Return_True_When_Failed_Flag_Set()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);
        barrier.Flags |= MetalMultiKernelBarrier.BARRIER_FLAG_FAILED;

        // Act & Assert
        Assert.True(barrier.IsFailed);
    }

    [Fact]
    public void IsFailed_Should_Return_False_When_No_Failed_Flag()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Act & Assert
        Assert.False(barrier.IsFailed);
    }

    [Fact]
    public void IsHealthy_Should_Return_True_When_No_Error_Flags()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Act & Assert
        Assert.True(barrier.IsHealthy);
    }

    [Fact]
    public void IsHealthy_Should_Return_False_When_Timeout_Flag_Set()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);
        barrier.Flags |= MetalMultiKernelBarrier.BARRIER_FLAG_TIMEOUT;

        // Act & Assert
        Assert.False(barrier.IsHealthy);
    }

    [Fact]
    public void IsHealthy_Should_Return_False_When_Failed_Flag_Set()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);
        barrier.Flags |= MetalMultiKernelBarrier.BARRIER_FLAG_FAILED;

        // Act & Assert
        Assert.False(barrier.IsHealthy);
    }

    [Fact]
    public void Equals_Should_Return_True_For_Identical_Barriers()
    {
        // Arrange
        var barrier1 = new MetalMultiKernelBarrier
        {
            ParticipantCount = 10,
            ArrivedCount = 5,
            Generation = 3,
            Flags = MetalMultiKernelBarrier.BARRIER_FLAG_ACTIVE
        };

        var barrier2 = new MetalMultiKernelBarrier
        {
            ParticipantCount = 10,
            ArrivedCount = 5,
            Generation = 3,
            Flags = MetalMultiKernelBarrier.BARRIER_FLAG_ACTIVE
        };

        // Act & Assert
        Assert.True(barrier1.Equals(barrier2));
        Assert.True(barrier1 == barrier2);
        Assert.False(barrier1 != barrier2);
    }

    [Fact]
    public void Equals_Should_Return_False_For_Different_Barriers()
    {
        // Arrange
        var barrier1 = MetalMultiKernelBarrier.Create(10);
        var barrier2 = MetalMultiKernelBarrier.Create(20);

        // Act & Assert
        Assert.False(barrier1.Equals(barrier2));
        Assert.False(barrier1 == barrier2);
        Assert.True(barrier1 != barrier2);
    }

    [Fact]
    public void GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Act
        int hash1 = barrier.GetHashCode();
        int hash2 = barrier.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_Should_Differ_For_Different_Barriers()
    {
        // Arrange
        var barrier1 = MetalMultiKernelBarrier.Create(10);
        var barrier2 = MetalMultiKernelBarrier.Create(20);

        // Act
        int hash1 = barrier1.GetHashCode();
        int hash2 = barrier2.GetHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void BarrierFlags_Should_Be_Combinable()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Act - set both timeout and failed flags
        barrier.Flags |= MetalMultiKernelBarrier.BARRIER_FLAG_TIMEOUT;
        barrier.Flags |= MetalMultiKernelBarrier.BARRIER_FLAG_FAILED;

        // Assert
        Assert.True(barrier.IsTimedOut);
        Assert.True(barrier.IsFailed);
        Assert.False(barrier.IsHealthy);
    }

    [Fact]
    public void ArrivedCount_Should_Track_Progress()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Act - simulate arrivals
        barrier.ArrivedCount = 3;

        // Assert
        Assert.Equal(3, barrier.ArrivedCount);
        Assert.Equal(10, barrier.ParticipantCount);
        Assert.True(barrier.IsHealthy);
    }

    [Fact]
    public void Generation_Should_Increment_After_Barrier_Completion()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);
        Assert.Equal(0, barrier.Generation);

        // Act - simulate barrier completion
        barrier.Generation++;

        // Assert
        Assert.Equal(1, barrier.Generation);
    }

    [Fact]
    public void Multiple_Barrier_Completions_Should_Increment_Generation()
    {
        // Arrange
        var barrier = MetalMultiKernelBarrier.Create(10);

        // Act - simulate 5 barrier completions
        for (int i = 0; i < 5; i++)
        {
            barrier.Generation++;
        }

        // Assert
        Assert.Equal(5, barrier.Generation);
    }
}
