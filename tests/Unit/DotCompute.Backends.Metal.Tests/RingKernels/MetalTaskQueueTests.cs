// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.RingKernels;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalTaskDescriptor and MetalTaskQueue structures.
/// </summary>
public sealed class MetalTaskQueueTests
{
    [Fact]
    public void TaskDescriptor_Create_Should_Initialize_With_Correct_Values()
    {
        // Arrange
        long taskId = 12345;
        uint targetKernelId = 100;
        uint priority = 750;
        long dataPtr = 0x1000;
        uint dataSize = 256;
        uint flags = MetalTaskDescriptor.FlagCompleted;

        // Act
        var task = MetalTaskDescriptor.Create(taskId, targetKernelId, priority, dataPtr, dataSize, flags);

        // Assert
        Assert.Equal(taskId, task.TaskId);
        Assert.Equal(targetKernelId, task.TargetKernelId);
        Assert.Equal(priority, task.Priority);
        Assert.Equal(dataPtr, task.DataPtr);
        Assert.Equal(dataSize, task.DataSize);
        Assert.Equal(flags, task.Flags);
    }

    [Fact]
    public void TaskDescriptor_Create_Should_Use_Default_Values()
    {
        // Act
        var task = MetalTaskDescriptor.Create(taskId: 100);

        // Assert
        Assert.Equal(100, task.TaskId);
        Assert.Equal(0u, task.TargetKernelId);    // Default: no preference
        Assert.Equal(500u, task.Priority);          // Default: medium priority
        Assert.Equal(0, task.DataPtr);
        Assert.Equal(0u, task.DataSize);
        Assert.Equal(0u, task.Flags);
    }

    [Fact]
    public void TaskDescriptor_FlagConstants_Should_Have_Correct_Values()
    {
        // Assert
        Assert.Equal(0x0001u, MetalTaskDescriptor.FlagCompleted);
        Assert.Equal(0x0002u, MetalTaskDescriptor.FlagFailed);
        Assert.Equal(0x0004u, MetalTaskDescriptor.FlagCanceled);
    }

    [Fact]
    public void TaskDescriptor_Flags_Should_Be_Combinable()
    {
        // Arrange
        uint combinedFlags = MetalTaskDescriptor.FlagCompleted | MetalTaskDescriptor.FlagFailed;

        // Act
        var task = MetalTaskDescriptor.Create(taskId: 1, flags: combinedFlags);

        // Assert
        Assert.Equal(0x0003u, task.Flags);
        Assert.True((task.Flags & MetalTaskDescriptor.FlagCompleted) != 0);
        Assert.True((task.Flags & MetalTaskDescriptor.FlagFailed) != 0);
        Assert.False((task.Flags & MetalTaskDescriptor.FlagCanceled) != 0);
    }

    [Fact]
    public void TaskDescriptor_Equals_Should_Return_True_For_Identical_Tasks()
    {
        // Arrange
        var task1 = MetalTaskDescriptor.Create(100, 1, 500, 0x1000, 256);
        var task2 = MetalTaskDescriptor.Create(100, 1, 500, 0x1000, 256);

        // Act & Assert
        Assert.True(task1.Equals(task2));
        Assert.True(task1 == task2);
        Assert.False(task1 != task2);
    }

    [Fact]
    public void TaskDescriptor_Equals_Should_Return_False_For_Different_Tasks()
    {
        // Arrange
        var task1 = MetalTaskDescriptor.Create(100);
        var task2 = MetalTaskDescriptor.Create(200);

        // Act & Assert
        Assert.False(task1.Equals(task2));
        Assert.False(task1 == task2);
        Assert.True(task1 != task2);
    }

    [Fact]
    public void TaskDescriptor_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var task = MetalTaskDescriptor.Create(100, 1, 500);

        // Act
        int hash1 = task.GetHashCode();
        int hash2 = task.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void TaskDescriptor_Size_Should_Be_64_Bytes()
    {
        // Act
        int size = Marshal.SizeOf<MetalTaskDescriptor>();

        // Assert
        Assert.Equal(64, size);
    }

    [Fact]
    public void TaskQueue_CreateEmpty_Should_Initialize_All_Fields_To_Zero()
    {
        // Act
        var queue = MetalTaskQueue.CreateEmpty();

        // Assert
        Assert.Equal(0, queue.Head);
        Assert.Equal(0, queue.Tail);
        Assert.Equal(0, queue.Capacity);
        Assert.Equal(0, queue.TasksPtr);
        Assert.Equal(0u, queue.OwnerId);
        Assert.Equal(0u, queue.Flags);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(65536)]
    public void TaskQueue_Create_Should_Accept_Power_Of_Two_Capacities(int capacity)
    {
        // Act
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity, tasksPtr: 0x1000);

        // Assert
        Assert.Equal(capacity, queue.Capacity);
        Assert.True(queue.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(100)]
    [InlineData(65537)]
    [InlineData(100000)]
    public void TaskQueue_Create_Should_Reject_Non_Power_Of_Two_Capacities(int capacity)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetalTaskQueue.Create(ownerId: 1, capacity, tasksPtr: 0x1000));
    }

    [Fact]
    public void TaskQueue_Create_Should_Reject_Zero_TasksPtr()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0));
    }

    [Fact]
    public void TaskQueue_Create_Should_Set_Active_And_Stealing_Flags_By_Default()
    {
        // Act
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000);

        // Assert
        Assert.True((queue.Flags & MetalTaskQueue.FlagActive) != 0);
        Assert.True((queue.Flags & MetalTaskQueue.FlagStealingEnabled) != 0);
    }

    [Fact]
    public void TaskQueue_Create_Should_Disable_Stealing_When_Requested()
    {
        // Act
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000, enableStealing: false);

        // Assert
        Assert.True((queue.Flags & MetalTaskQueue.FlagActive) != 0);
        Assert.False((queue.Flags & MetalTaskQueue.FlagStealingEnabled) != 0);
    }

    [Fact]
    public void TaskQueue_FlagConstants_Should_Have_Correct_Values()
    {
        // Assert
        Assert.Equal(0x0001u, MetalTaskQueue.FlagActive);
        Assert.Equal(0x0002u, MetalTaskQueue.FlagStealingEnabled);
        Assert.Equal(0x0004u, MetalTaskQueue.FlagFull);
    }

    [Fact]
    public void TaskQueue_Validate_Should_Return_False_For_Invalid_Capacity()
    {
        // Arrange - manually create invalid queue
        var queue = new MetalTaskQueue
        {
            Head = 0,
            Tail = 0,
            Capacity = 100,  // Not power of 2
            TasksPtr = 0x1000,
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.False(queue.Validate());
    }

    [Fact]
    public void TaskQueue_Validate_Should_Return_False_For_Zero_TasksPtr_With_Nonzero_Capacity()
    {
        // Arrange
        var queue = new MetalTaskQueue
        {
            Head = 0,
            Tail = 0,
            Capacity = 256,
            TasksPtr = 0,  // Should be non-zero
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.False(queue.Validate());
    }

    [Fact]
    public void TaskQueue_Validate_Should_Return_False_For_Head_Less_Than_Tail()
    {
        // Arrange - manually create invalid queue
        var queue = new MetalTaskQueue
        {
            Head = 5,
            Tail = 10,  // Tail > Head violates invariant
            Capacity = 256,
            TasksPtr = 0x1000,
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.False(queue.Validate());
    }

    [Fact]
    public void TaskQueue_Validate_Should_Return_True_For_Valid_Queue()
    {
        // Arrange
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000);

        // Act & Assert
        Assert.True(queue.Validate());
    }

    [Fact]
    public void TaskQueue_Size_Should_Return_Difference_Between_Head_And_Tail()
    {
        // Arrange
        var queue = new MetalTaskQueue
        {
            Head = 10,
            Tail = 3,
            Capacity = 256,
            TasksPtr = 0x1000,
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.Equal(7, queue.Size);
    }

    [Fact]
    public void TaskQueue_IsEmpty_Should_Return_True_When_Head_Equals_Tail()
    {
        // Arrange
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000);

        // Act & Assert
        Assert.True(queue.IsEmpty());
    }

    [Fact]
    public void TaskQueue_IsEmpty_Should_Return_False_When_Queue_Has_Tasks()
    {
        // Arrange
        var queue = new MetalTaskQueue
        {
            Head = 5,
            Tail = 0,
            Capacity = 256,
            TasksPtr = 0x1000,
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.False(queue.IsEmpty());
    }

    [Fact]
    public void TaskQueue_IsFull_Should_Return_False_When_Size_Less_Than_Capacity()
    {
        // Arrange
        var queue = new MetalTaskQueue
        {
            Head = 100,
            Tail = 0,
            Capacity = 256,
            TasksPtr = 0x1000,
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.False(queue.IsFull());
    }

    [Fact]
    public void TaskQueue_IsFull_Should_Return_True_When_Size_Equals_Capacity()
    {
        // Arrange
        var queue = new MetalTaskQueue
        {
            Head = 256,
            Tail = 0,
            Capacity = 256,
            TasksPtr = 0x1000,
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.True(queue.IsFull());
    }

    [Fact]
    public void TaskQueue_IsFull_Should_Return_True_When_Size_Exceeds_Capacity()
    {
        // Arrange - can happen during concurrent operations
        var queue = new MetalTaskQueue
        {
            Head = 300,
            Tail = 0,
            Capacity = 256,
            TasksPtr = 0x1000,
            OwnerId = 1,
            Flags = MetalTaskQueue.FlagActive
        };

        // Act & Assert
        Assert.True(queue.IsFull());
    }

    [Fact]
    public void TaskQueue_Equals_Should_Return_True_For_Identical_Queues()
    {
        // Arrange
        var queue1 = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000);
        var queue2 = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000);

        // Act & Assert
        Assert.True(queue1.Equals(queue2));
        Assert.True(queue1 == queue2);
        Assert.False(queue1 != queue2);
    }

    [Fact]
    public void TaskQueue_Equals_Should_Return_False_For_Different_Queues()
    {
        // Arrange
        var queue1 = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000);
        var queue2 = MetalTaskQueue.Create(ownerId: 2, capacity: 256, tasksPtr: 0x1000);

        // Act & Assert
        Assert.False(queue1.Equals(queue2));
        Assert.False(queue1 == queue2);
        Assert.True(queue1 != queue2);
    }

    [Fact]
    public void TaskQueue_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity: 256, tasksPtr: 0x1000);

        // Act
        int hash1 = queue.GetHashCode();
        int hash2 = queue.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void TaskQueue_Size_Should_Be_32_Bytes()
    {
        // Act
        int size = Marshal.SizeOf<MetalTaskQueue>();

        // Assert
        Assert.Equal(32, size);
    }
}
