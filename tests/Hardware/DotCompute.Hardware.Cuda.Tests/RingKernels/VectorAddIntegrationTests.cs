// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Unit tests for Ring Kernel Phase 3 Advanced Features.
/// Tests all 5 components: Message Router, Topic Pub/Sub, Barriers, Task Queues, and Health Monitoring.
/// </summary>
public sealed class Phase3ComponentTests
{
    // ==================== Component 1: Message Router Tests ====================

    [Fact(DisplayName = "KernelRoutingTable should be exactly 32 bytes")]
    public void MessageRouter_KernelRoutingTable_ShouldBe32Bytes()
    {
        // Act
        var size = Marshal.SizeOf<KernelRoutingTable>();

        // Assert
        size.Should().Be(32, "struct size must match CUDA expectations (4+8+8+8+4)");
    }

    [Fact(DisplayName = "KernelRoutingTable CreateEmpty should initialize correctly")]
    public void MessageRouter_CreateEmpty_InitializesCorrectly()
    {
        // Act
        var table = KernelRoutingTable.CreateEmpty();

        // Assert
        table.KernelCount.Should().Be(0);
        table.KernelControlBlocksPtr.Should().Be(0);
        table.OutputQueuesPtr.Should().Be(0);
        table.RoutingHashTablePtr.Should().Be(0);
        table.HashTableCapacity.Should().Be(0);
    }

    [Fact(DisplayName = "KernelRoutingTable Validate should check invariants")]
    public void MessageRouter_Validate_ChecksInvariants()
    {
        // Empty table is valid
        var emptyTable = KernelRoutingTable.CreateEmpty();
        emptyTable.Validate().Should().BeTrue("empty table is valid");

        // Invalid: negative kernel count
        var invalidTable1 = new KernelRoutingTable { KernelCount = -1 };
        invalidTable1.Validate().Should().BeFalse("kernel count cannot be negative");

        // Invalid: capacity not power of 2
        var invalidTable2 = new KernelRoutingTable
        {
            KernelCount = 4,
            HashTableCapacity = 3,
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000
        };
        invalidTable2.Validate().Should().BeFalse("capacity must be power of 2");

        // Valid table
        var validTable = new KernelRoutingTable
        {
            KernelCount = 4,
            HashTableCapacity = 8,
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000
        };
        validTable.Validate().Should().BeTrue("valid table passes validation");
    }

    // ==================== Component 2: Topic Pub/Sub Tests ====================

    [Fact(DisplayName = "TopicSubscription should be exactly 12 bytes")]
    public void TopicPubSub_TopicSubscription_ShouldBe12Bytes()
    {
        // Act
        var size = Marshal.SizeOf<TopicSubscription>();

        // Assert
        size.Should().Be(12, "struct size must match CUDA expectations (4+4+2+2)");
    }

    [Fact(DisplayName = "TopicRegistry should be exactly 24 bytes")]
    public void TopicPubSub_TopicRegistry_ShouldBe24Bytes()
    {
        // Act
        var size = Marshal.SizeOf<TopicRegistry>();

        // Assert
        size.Should().Be(24, "struct size must match CUDA expectations (4+8+8+4)");
    }

    [Fact(DisplayName = "TopicRegistry CalculateCapacity should use power of 2")]
    public void TopicPubSub_CalculateCapacity_UsesPowerOf2()
    {
        // Act & Assert
        TopicRegistry.CalculateCapacity(0).Should().Be(16, "minimum capacity is 16");
        TopicRegistry.CalculateCapacity(1).Should().Be(16, "1 topic rounds up to 16");
        TopicRegistry.CalculateCapacity(10).Should().Be(32, "10 topics rounds up to 32 (2x)");
        TopicRegistry.CalculateCapacity(200).Should().Be(512, "200 topics rounds up to 512 (2x)");
        TopicRegistry.CalculateCapacity(100000).Should().Be(65536, "caps at 65536 max");
    }

    [Fact(DisplayName = "TopicSubscription flags should be properly defined")]
    public void TopicPubSub_TopicSubscription_FlagConstants()
    {
        // Assert
        TopicSubscription.FlagWildcard.Should().Be(0x0001, "wildcard flag is bit 0");
        TopicSubscription.FlagHighPriority.Should().Be(0x0002, "high priority flag is bit 1");
    }

    // ==================== Component 3: Multi-Kernel Barriers Tests ====================

    [Fact(DisplayName = "MultiKernelBarrier should be exactly 16 bytes")]
    public void Barriers_MultiKernelBarrier_ShouldBe16Bytes()
    {
        // Act
        var size = Marshal.SizeOf<MultiKernelBarrier>();

        // Assert
        size.Should().Be(16, "struct size must match CUDA expectations (4+4+4+4)");
    }

    [Fact(DisplayName = "MultiKernelBarrier Create should initialize correctly")]
    public void Barriers_Create_InitializesCorrectly()
    {
        // Act
        var barrier = MultiKernelBarrier.Create(participantCount: 4);

        // Assert
        barrier.ParticipantCount.Should().Be(4);
        barrier.ArrivedCount.Should().Be(0, "no arrivals yet");
        barrier.Generation.Should().Be(0, "starts at generation 0");
        barrier.Flags.Should().Be(MultiKernelBarrier.FlagActive, "should be active");
    }

    [Fact(DisplayName = "MultiKernelBarrier Validate should check invariants")]
    public void Barriers_Validate_ChecksInvariants()
    {
        // Arrange - Valid barrier
        var validBarrier = MultiKernelBarrier.Create(4);
        validBarrier.Validate().Should().BeTrue("valid barrier passes validation");

        // Arrange - Invalid: arrived > participants
        var invalidBarrier = new MultiKernelBarrier
        {
            ParticipantCount = 4,
            ArrivedCount = 5, // More than participants!
            Generation = 0,
            Flags = MultiKernelBarrier.FlagActive
        };

        // Assert
        invalidBarrier.Validate().Should().BeFalse("arrived count cannot exceed participant count");
    }

    [Fact(DisplayName = "MultiKernelBarrier flag constants should be properly defined")]
    public void Barriers_FlagConstants_AreCorrect()
    {
        // Assert
        MultiKernelBarrier.FlagActive.Should().Be(0x0001, "active flag is bit 0");
        MultiKernelBarrier.FlagTimeout.Should().Be(0x0002, "timeout flag is bit 1");
        MultiKernelBarrier.FlagFailed.Should().Be(0x0004, "failed flag is bit 2");
    }

    // ==================== Component 4: Work-Stealing Task Queues Tests ====================

    [Fact(DisplayName = "TaskDescriptor should be exactly 64 bytes (cache-line aligned)")]
    public void TaskQueues_TaskDescriptor_ShouldBe64Bytes()
    {
        // Act
        var size = Marshal.SizeOf<TaskDescriptor>();

        // Assert
        size.Should().Be(64, "task descriptors are cache-line aligned for performance");
    }

    [Fact(DisplayName = "TaskQueue should be exactly 40 bytes")]
    public void TaskQueues_TaskQueue_ShouldBe40Bytes()
    {
        // Act
        var size = Marshal.SizeOf<TaskQueue>();

        // Assert
        size.Should().Be(40, "struct size must match CUDA expectations (8+8+4+8+4+4+4 with padding)");
    }

    [Fact(DisplayName = "TaskQueue Create should require power of 2 capacity")]
    public void TaskQueues_Create_RequiresPowerOf2Capacity()
    {
        // Act & Assert - Invalid capacities
        FluentActions.Invoking(() =>
            TaskQueue.Create(ownerId: 1, capacity: 3, tasksPtr: 0x1000, enableStealing: true))
            .Should().Throw<ArgumentOutOfRangeException>("capacity 3 is not power of 2");

        FluentActions.Invoking(() =>
            TaskQueue.Create(ownerId: 1, capacity: 100000, tasksPtr: 0x1000, enableStealing: true))
            .Should().Throw<ArgumentOutOfRangeException>("capacity exceeds max of 65536");

        FluentActions.Invoking(() =>
            TaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0, enableStealing: true))
            .Should().Throw<ArgumentException>("tasks pointer cannot be zero");

        // Valid capacity should succeed
        var queue = TaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000, enableStealing: true);
        queue.Capacity.Should().Be(256);
        (queue.Flags & TaskQueue.FlagStealingEnabled).Should().NotBe(0u, "stealing should be enabled");
    }

    [Fact(DisplayName = "TaskQueue Size property should calculate correctly")]
    public void TaskQueues_Size_CalculatesCorrectly()
    {
        // Arrange
        var queue = TaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000, enableStealing: true);

        // Initially empty
        queue.Size.Should().Be(0, "queue starts empty");
        queue.IsEmpty().Should().BeTrue();
        queue.IsFull().Should().BeFalse();

        // Simulate adding tasks
        queue.Head = 10;
        queue.Tail = 0;
        queue.Size.Should().Be(10, "head - tail = queue size");
        queue.IsEmpty().Should().BeFalse();
        queue.IsFull().Should().BeFalse();

        // Simulate full queue
        queue.Head = 256;
        queue.Tail = 0;
        queue.Size.Should().Be(256, "queue is full");
        queue.IsFull().Should().BeTrue();
    }

    [Fact(DisplayName = "TaskDescriptor flag constants should be properly defined")]
    public void TaskQueues_TaskDescriptor_FlagConstants()
    {
        // Assert
        TaskDescriptor.FlagCompleted.Should().Be(0x0001, "completed flag is bit 0");
        TaskDescriptor.FlagFailed.Should().Be(0x0002, "failed flag is bit 1");
        TaskDescriptor.FlagCanceled.Should().Be(0x0004, "canceled flag is bit 2");
    }

    [Fact(DisplayName = "TaskQueue flag constants should be properly defined")]
    public void TaskQueues_TaskQueue_FlagConstants()
    {
        // Assert
        TaskQueue.FlagActive.Should().Be(0x0001, "active flag is bit 0");
        TaskQueue.FlagStealingEnabled.Should().Be(0x0002, "stealing enabled flag is bit 1");
        TaskQueue.FlagFull.Should().Be(0x0004, "full flag is bit 2");
    }

    // ==================== Component 5: Fault Tolerance Tests ====================

    [Fact(DisplayName = "KernelHealthStatus should be exactly 36 bytes")]
    public void FaultTolerance_KernelHealthStatus_ShouldBe36Bytes()
    {
        // Act
        var size = Marshal.SizeOf<KernelHealthStatus>();

        // Assert
        size.Should().Be(36, "struct size must match CUDA expectations (8+4+4+4+8+4+4 with padding)");
    }

    [Fact(DisplayName = "KernelHealthStatus CreateInitialized should set current timestamp")]
    public void FaultTolerance_CreateInitialized_SetsCurrentTimestamp()
    {
        // Arrange
        var beforeTicks = DateTime.UtcNow.Ticks;

        // Act
        var health = KernelHealthStatus.CreateInitialized();

        // Assert
        var afterTicks = DateTime.UtcNow.Ticks;
        health.LastHeartbeatTicks.Should().BeInRange(beforeTicks, afterTicks);
        health.ErrorCount.Should().Be(0);
        health.State.Should().Be((int)KernelState.Healthy);
    }

    [Fact(DisplayName = "KernelHealthStatus IsHeartbeatStale should detect timeout")]
    public void FaultTolerance_IsHeartbeatStale_DetectsTimeout()
    {
        // Arrange - 10 seconds old heartbeat
        var health = new KernelHealthStatus
        {
            LastHeartbeatTicks = DateTime.UtcNow.AddSeconds(-10).Ticks,
            ErrorCount = 0,
            State = (int)KernelState.Healthy
        };

        // Assert
        health.IsHeartbeatStale(TimeSpan.FromSeconds(5)).Should().BeTrue("10s > 5s timeout");
        health.IsHeartbeatStale(TimeSpan.FromSeconds(15)).Should().BeFalse("10s < 15s timeout");
    }

    [Fact(DisplayName = "KernelHealthStatus Validate should check all invariants")]
    public void FaultTolerance_Validate_ChecksAllInvariants()
    {
        // Valid health status
        var validHealth = KernelHealthStatus.CreateInitialized();
        validHealth.Validate().Should().BeTrue("initialized health status is valid");

        // Invalid: negative error count
        var invalidHealth1 = new KernelHealthStatus { ErrorCount = -1 };
        invalidHealth1.Validate().Should().BeFalse("error count cannot be negative");

        // Invalid: state out of range
        var invalidHealth2 = new KernelHealthStatus { State = 999 };
        invalidHealth2.Validate().Should().BeFalse("state must be valid KernelState enum value");

        // Invalid: negative checkpoint ID
        var invalidHealth3 = new KernelHealthStatus { LastCheckpointId = -1 };
        invalidHealth3.Validate().Should().BeFalse("checkpoint ID cannot be negative");
    }

    [Fact(DisplayName = "KernelState enum values should be correct")]
    public void FaultTolerance_KernelState_EnumValues()
    {
        // Assert
        ((int)KernelState.Healthy).Should().Be(0);
        ((int)KernelState.Degraded).Should().Be(1);
        ((int)KernelState.Failed).Should().Be(2);
        ((int)KernelState.Recovering).Should().Be(3);
        ((int)KernelState.Stopped).Should().Be(4);
    }

    [Fact(DisplayName = "KernelHealthStatus helper methods should work correctly")]
    public void FaultTolerance_HelperMethods_WorkCorrectly()
    {
        // Arrange
        var healthy = new KernelHealthStatus { State = (int)KernelState.Healthy };
        var degraded = new KernelHealthStatus { State = (int)KernelState.Degraded };
        var failed = new KernelHealthStatus { State = (int)KernelState.Failed };
        var recovering = new KernelHealthStatus { State = (int)KernelState.Recovering };

        // Assert
        healthy.IsHealthy().Should().BeTrue();
        healthy.IsDegraded().Should().BeFalse();
        healthy.IsFailed().Should().BeFalse();
        healthy.IsRecovering().Should().BeFalse();

        degraded.IsHealthy().Should().BeFalse();
        degraded.IsDegraded().Should().BeTrue();

        failed.IsFailed().Should().BeTrue();
        recovering.IsRecovering().Should().BeTrue();
    }
}
