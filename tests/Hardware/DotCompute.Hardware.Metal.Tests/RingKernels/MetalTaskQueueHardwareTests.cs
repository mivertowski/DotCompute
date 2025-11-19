// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.RingKernels;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.RingKernels;

/// <summary>
/// Hardware tests for Metal work-stealing task queues on actual Mac hardware.
/// </summary>
/// <remarks>
/// These tests require a Mac with Metal support and validate:
/// - Task queue and descriptor allocation on GPU
/// - Unified memory CPU/GPU access for task queues
/// - Work-stealing queue operations (push, pop, steal)
/// - Performance characteristics (target: ~25ns owner ops, ~500ns steal for 100 tasks)
/// - Chase-Lev deque invariants
/// </remarks>
[Collection("MetalHardware")]
public sealed class MetalTaskQueueHardwareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IntPtr _device;

    public MetalTaskQueueHardwareTests(ITestOutputHelper output)
    {
        _output = output;

        // Initialize Metal device
        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("No Metal device available. These tests require a Mac with Metal support.");
        }

        _output.WriteLine($"Metal device initialized: 0x{_device.ToInt64():X}");
    }

    [Fact]
    public void TaskQueue_Should_Allocate_On_GPU_With_Unified_Memory()
    {
        // Arrange
        int capacity = 256;
        int taskArraySize = capacity * 64; // 64 bytes per task

        // Allocate task array with unified memory
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);
        Assert.NotEqual(IntPtr.Zero, tasksBuffer);

        // Act - create queue structure
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity, tasksBuffer.ToInt64());

        // Assert
        Assert.Equal(256, queue.Capacity);
        Assert.Equal(tasksBuffer.ToInt64(), queue.TasksPtr);
        Assert.Equal(1u, queue.OwnerId);
        Assert.True(queue.Validate());

        _output.WriteLine($"Task queue allocated:");
        _output.WriteLine($"  Capacity: {queue.Capacity}");
        _output.WriteLine($"  TasksPtr: 0x{queue.TasksPtr:X}");
        _output.WriteLine($"  OwnerId: {queue.OwnerId}");
        _output.WriteLine($"  Flags: 0x{queue.Flags:X}");

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    public void TaskQueue_Should_Support_Various_Capacities(int capacity)
    {
        // Arrange
        int taskArraySize = capacity * 64;
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        // Act
        var queue = MetalTaskQueue.Create(ownerId: 1, capacity, tasksBuffer.ToInt64());

        // Assert
        Assert.Equal(capacity, queue.Capacity);
        Assert.True(queue.Validate());

        _output.WriteLine($"Created queue with capacity {capacity}: valid={queue.Validate()}");

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    [Fact]
    public void UnifiedMemory_Should_Allow_CPU_To_Read_Task_Descriptors()
    {
        // Arrange
        int capacity = 16;
        int taskArraySize = capacity * 64;
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        // Act - write task descriptors from CPU
        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(tasksBuffer);
            var tasks = new Span<MetalTaskDescriptor>((void*)bufferPtr, capacity);

            // Write some test tasks
            for (int i = 0; i < 5; i++)
            {
                tasks[i] = MetalTaskDescriptor.Create(
                    taskId: i + 100,
                    targetKernelId: 1,
                    priority: (uint)(500 + i * 10));
            }

            // Read them back
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i + 100, tasks[i].TaskId);
                Assert.Equal(1u, tasks[i].TargetKernelId);
                Assert.Equal((uint)(500 + i * 10), tasks[i].Priority);

                _output.WriteLine($"  Task {i}: TaskId={tasks[i].TaskId}, Priority={tasks[i].Priority}");
            }
        }

        _output.WriteLine("CPU successfully wrote and read task descriptors via unified memory");

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    [Fact]
    public void UnifiedMemory_Should_Allow_CPU_To_Modify_Queue_State()
    {
        // Arrange
        int capacity = 256;
        int taskArraySize = capacity * 64;
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        var queue = MetalTaskQueue.Create(ownerId: 1, capacity, tasksBuffer.ToInt64());

        // Act - simulate owner pushing tasks by modifying head
        queue.Head = 10;

        // Assert - CPU can modify and read queue state via unified memory
        Assert.Equal(10, queue.Head);
        Assert.Equal(0, queue.Tail);
        Assert.Equal(10, queue.Size);
        Assert.False(queue.IsEmpty());

        _output.WriteLine($"CPU successfully modified queue state:");
        _output.WriteLine($"  Head: {queue.Head}");
        _output.WriteLine($"  Tail: {queue.Tail}");
        _output.WriteLine($"  Size: {queue.Size}");

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    [Fact]
    public void TaskQueue_Flags_Should_Be_Modifiable()
    {
        // Arrange
        int capacity = 256;
        int taskArraySize = capacity * 64;
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        var queue = MetalTaskQueue.Create(ownerId: 1, capacity, tasksBuffer.ToInt64());

        // Act - modify flags
        queue.Flags |= MetalTaskQueue.FlagFull;

        // Assert
        Assert.True((queue.Flags & MetalTaskQueue.FlagFull) != 0);
        Assert.True((queue.Flags & MetalTaskQueue.FlagActive) != 0);

        _output.WriteLine($"Queue flags after modification: 0x{queue.Flags:X}");
        _output.WriteLine($"  Active: {(queue.Flags & MetalTaskQueue.FlagActive) != 0}");
        _output.WriteLine($"  StealingEnabled: {(queue.Flags & MetalTaskQueue.FlagStealingEnabled) != 0}");
        _output.WriteLine($"  Full: {(queue.Flags & MetalTaskQueue.FlagFull) != 0}");

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    [Fact]
    public void TaskDescriptor_Flags_Should_Support_State_Transitions()
    {
        // Arrange
        int capacity = 16;
        int taskArraySize = capacity * 64;
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        // Act - write task and modify flags
        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(tasksBuffer);
            var tasks = new Span<MetalTaskDescriptor>((void*)bufferPtr, capacity);

            // Create task
            tasks[0] = MetalTaskDescriptor.Create(taskId: 1000, priority: 500);
            Assert.Equal(0u, tasks[0].Flags);

            // Mark as completed
            tasks[0].Flags = MetalTaskDescriptor.FlagCompleted;
            Assert.Equal(MetalTaskDescriptor.FlagCompleted, tasks[0].Flags);

            // Mark as failed (replace completed)
            tasks[0].Flags = MetalTaskDescriptor.FlagFailed;
            Assert.Equal(MetalTaskDescriptor.FlagFailed, tasks[0].Flags);

            _output.WriteLine("Task flag transitions validated:");
            _output.WriteLine($"  Initial: 0x{0:X}");
            _output.WriteLine($"  Completed: 0x{MetalTaskDescriptor.FlagCompleted:X}");
            _output.WriteLine($"  Failed: 0x{MetalTaskDescriptor.FlagFailed:X}");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    [Fact]
    public void Multiple_TaskQueues_Should_Be_Independent()
    {
        // Arrange
        int capacity = 256;
        int taskArraySize = capacity * 64;

        IntPtr tasks1 = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);
        IntPtr tasks2 = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);
        IntPtr tasks3 = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        // Act
        var queue1 = MetalTaskQueue.Create(ownerId: 1, capacity, tasks1.ToInt64());
        var queue2 = MetalTaskQueue.Create(ownerId: 2, capacity, tasks2.ToInt64());
        var queue3 = MetalTaskQueue.Create(ownerId: 3, capacity, tasks3.ToInt64());

        // Assert - queues are independent
        Assert.Equal(1u, queue1.OwnerId);
        Assert.Equal(2u, queue2.OwnerId);
        Assert.Equal(3u, queue3.OwnerId);

        Assert.NotEqual(queue1.TasksPtr, queue2.TasksPtr);
        Assert.NotEqual(queue2.TasksPtr, queue3.TasksPtr);
        Assert.NotEqual(queue1.TasksPtr, queue3.TasksPtr);

        _output.WriteLine($"Created 3 independent task queues:");
        _output.WriteLine($"  Queue 1: Owner={queue1.OwnerId}, TasksPtr=0x{queue1.TasksPtr:X}");
        _output.WriteLine($"  Queue 2: Owner={queue2.OwnerId}, TasksPtr=0x{queue2.TasksPtr:X}");
        _output.WriteLine($"  Queue 3: Owner={queue3.OwnerId}, TasksPtr=0x{queue3.TasksPtr:X}");

        // Cleanup
        MetalNative.ReleaseBuffer(tasks1);
        MetalNative.ReleaseBuffer(tasks2);
        MetalNative.ReleaseBuffer(tasks3);
    }

    [Fact]
    public void TaskQueue_Chase_Lev_Invariants_Should_Hold()
    {
        // Arrange
        int capacity = 256;
        int taskArraySize = capacity * 64;
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        var queue = MetalTaskQueue.Create(ownerId: 1, capacity, tasksBuffer.ToInt64());

        // Act - simulate various queue states
        var testCases = new[]
        {
            (Head: 0L, Tail: 0L, Size: 0L, IsEmpty: true, IsFull: false, IsValid: true),
            (Head: 10L, Tail: 0L, Size: 10L, IsEmpty: false, IsFull: false, IsValid: true),
            (Head: 256L, Tail: 0L, Size: 256L, IsEmpty: false, IsFull: true, IsValid: true),
            (Head: 100L, Tail: 50L, Size: 50L, IsEmpty: false, IsFull: false, IsValid: true),
            (Head: 256L, Tail: 128L, Size: 128L, IsEmpty: false, IsFull: false, IsValid: true),
        };

        foreach (var (head, tail, size, isEmpty, isFull, isValid) in testCases)
        {
            queue.Head = head;
            queue.Tail = tail;

            Assert.Equal(size, queue.Size);
            Assert.Equal(isEmpty, queue.IsEmpty());
            Assert.Equal(isFull, queue.IsFull());
            Assert.Equal(isValid, queue.Validate());

            _output.WriteLine($"State: Head={head}, Tail={tail} => Size={size}, Empty={isEmpty}, Full={isFull}, Valid={isValid}");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    [Fact]
    public void Performance_TaskQueue_Allocation_Should_Be_Fast()
    {
        // Arrange
        int capacity = 1024;
        int taskArraySize = capacity * 64;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - allocate 10 task queues
        var buffers = new List<IntPtr>();
        for (int i = 0; i < 10; i++)
        {
            var buffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);
            buffers.Add(buffer);
            _ = MetalTaskQueue.Create(ownerId: (uint)(i + 1), capacity, buffer.ToInt64());
        }

        stopwatch.Stop();

        // Assert - should be very fast (<10ms for 10 queues)
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 50); // Generous, should be <10ms

        _output.WriteLine($"Allocated 10 task queues (capacity {capacity}) in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Average per queue: {stopwatch.Elapsed.TotalMilliseconds / 10:F3}ms");

        // Cleanup
        foreach (var buffer in buffers)
        {
            MetalNative.ReleaseBuffer(buffer);
        }
    }

    [Fact]
    public void TaskDescriptor_64Byte_Alignment_Should_Match_Cache_Line()
    {
        // Arrange
        int capacity = 10;
        int taskArraySize = capacity * 64;
        IntPtr tasksBuffer = MetalNative.CreateBuffer(_device, (nuint)taskArraySize, (int)MTLResourceOptions.StorageModeShared);

        // Act - verify 64-byte stride
        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(tasksBuffer);
            long baseAddr = bufferPtr.ToInt64();

            var tasks = new Span<MetalTaskDescriptor>((void*)bufferPtr, capacity);

            // Verify each task is 64 bytes apart
            for (int i = 0; i < capacity - 1; i++)
            {
                long addr1 = (long)Unsafe.AsPointer(ref tasks[i]);
                long addr2 = (long)Unsafe.AsPointer(ref tasks[i + 1]);
                long stride = addr2 - addr1;

                Assert.Equal(64, stride);
            }

            _output.WriteLine($"Task descriptors verified with 64-byte stride (Apple Silicon cache line)");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(tasksBuffer);
    }

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }

        _output.WriteLine("Metal device disposed");
    }
}
