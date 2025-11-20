// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.RingKernels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.RingKernels;

/// <summary>
/// Hardware tests for Metal topic registry (pub/sub) on actual Mac hardware.
/// </summary>
/// <remarks>
/// These tests require a Mac with Metal support and validate:
/// - Topic registry creation and initialization on GPU
/// - Subscription hash table performance
/// - Unified memory correctness for pub/sub
/// - Message broadcasting throughput (target: <10μs for 100 subscribers)
/// </remarks>
[Collection("MetalHardware")]
public sealed class MetalTopicRegistryHardwareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IntPtr _device;

    public MetalTopicRegistryHardwareTests(ITestOutputHelper output)
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
    public async Task BuildAsync_Should_Allocate_Topic_Registry_On_GPU()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device, NullLogger<MetalTopicRegistryBuilder>.Instance);
        _ = builder.Subscribe("physics.particles", kernelId: 1, queueIndex: 0);
        _ = builder.Subscribe("physics.forces", kernelId: 2, queueIndex: 1);
        _ = builder.Subscribe("physics.particles", kernelId: 3, queueIndex: 2); // Same topic, different kernel

        // Act
        var registry = await builder.BuildAsync();

        // Assert
        Assert.Equal(3, registry.SubscriptionCount);
        Assert.NotEqual(0, registry.SubscriptionsPtr);
        Assert.NotEqual(0, registry.TopicHashTablePtr);
        Assert.Equal(16, registry.HashTableCapacity); // 2 topics → 16 capacity (minimum)
        Assert.True(registry.Validate());

        _output.WriteLine($"Topic registry allocated:");
        _output.WriteLine($"  Subscriptions: {registry.SubscriptionCount}");
        _output.WriteLine($"  SubscriptionsPtr: 0x{registry.SubscriptionsPtr:X}");
        _output.WriteLine($"  TopicHashTablePtr: 0x{registry.TopicHashTablePtr:X}");
        _output.WriteLine($"  HashTableCapacity: {registry.HashTableCapacity}");

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Theory]
    [InlineData(10, 16)]   // 10 subscriptions, 2 topics → 2*2=4 → 16 (minimum)
    [InlineData(50, 32)]   // 50 subscriptions, 10 topics → 10*2=20 → 32
    [InlineData(100, 64)]  // 100 subscriptions, 20 topics → 20*2=40 → 64
    public async Task BuildAsync_Should_Handle_Various_Subscription_Counts(int subscriptionCount, int expectedCapacity)
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        int topicCount = subscriptionCount / 5; // ~5 subscriptions per topic

        for (int i = 0; i < subscriptionCount; i++)
        {
            int topicIndex = i % topicCount;
            _ = builder.Subscribe($"topic.{topicIndex}", (ushort)(i + 1), (ushort)i);
        }

        // Act
        var registry = await builder.BuildAsync();

        // Assert
        Assert.Equal(subscriptionCount, registry.SubscriptionCount);
        Assert.Equal(expectedCapacity, registry.HashTableCapacity);
        Assert.True(registry.Validate());

        _output.WriteLine($"Built registry for {subscriptionCount} subscriptions:");
        _output.WriteLine($"  Topics: {topicCount}");
        _output.WriteLine($"  Capacity: {registry.HashTableCapacity}");
        _output.WriteLine($"  Load factor: {(double)topicCount / registry.HashTableCapacity:P1}");

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task BuildAsync_Should_Reject_Duplicate_Subscriptions()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        _ = builder.Subscribe("physics.particles", kernelId: 1, queueIndex: 0);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            builder.Subscribe("physics.particles", kernelId: 1, queueIndex: 0));
    }

    [Fact]
    public async Task BuildAsync_Should_Reject_Empty_Topic_Name()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            builder.Subscribe("", kernelId: 1, queueIndex: 0));
        Assert.Throws<ArgumentException>(() =>
            builder.Subscribe("   ", kernelId: 1, queueIndex: 0));
    }

    [Fact]
    public async Task BuildAsync_Should_Throw_For_No_Subscriptions()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync());
    }

    [Fact]
    public async Task SubscribeMultiple_Should_Add_Multiple_Subscriptions()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        ushort[] kernelIds = [1, 2, 3, 4, 5];
        ushort[] queueIndices = [0, 1, 2, 3, 4];

        // Act
        int count = builder.SubscribeMultiple("network.messages", kernelIds, queueIndices);

        // Assert
        Assert.Equal(5, count);

        var registry = await builder.BuildAsync();
        Assert.Equal(5, registry.SubscriptionCount);

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task SubscribeMultiple_Should_Skip_Duplicate_Subscriptions()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        _ = builder.Subscribe("network.messages", kernelId: 1, queueIndex: 0);

        ushort[] kernelIds = [1, 2, 3]; // First one is duplicate
        ushort[] queueIndices = [0, 1, 2];

        // Act
        int count = builder.SubscribeMultiple("network.messages", kernelIds, queueIndices);

        // Assert
        Assert.Equal(2, count); // Only 2 added (1 was duplicate)

        var registry = await builder.BuildAsync();
        Assert.Equal(3, registry.SubscriptionCount); // Total 3 (1 + 2 new)

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task UnifiedMemory_Should_Allow_CPU_To_Read_Subscriptions()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        _ = builder.Subscribe("physics.particles", kernelId: 100, queueIndex: 5);
        _ = builder.Subscribe("physics.forces", kernelId: 200, queueIndex: 10);

        var registry = await builder.BuildAsync();

        // Act - read subscriptions from unified memory
        unsafe
        {
            var subscriptionsBuffer = new IntPtr(registry.SubscriptionsPtr);
            var subscriptionsPtr = MetalNative.GetBufferContents(subscriptionsBuffer);
            var subscriptions = new Span<MetalTopicSubscription>((void*)subscriptionsPtr, registry.SubscriptionCount);

            // Assert - CPU can read GPU-allocated unified memory
            Assert.Equal(2, subscriptions.Length);

            _output.WriteLine("CPU successfully read subscriptions from unified memory:");
            for (int i = 0; i < subscriptions.Length; i++)
            {
                _output.WriteLine($"  Subscription {i}:");
                _output.WriteLine($"    TopicId: 0x{subscriptions[i].TopicId:X8}");
                _output.WriteLine($"    KernelId: {subscriptions[i].KernelId}");
                _output.WriteLine($"    QueueIndex: {subscriptions[i].QueueIndex}");
            }
        }

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task UnifiedMemory_Should_Allow_CPU_To_Read_HashTable()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        _ = builder.Subscribe("physics.particles", kernelId: 1, queueIndex: 0);
        _ = builder.Subscribe("physics.forces", kernelId: 2, queueIndex: 1);

        var registry = await builder.BuildAsync();

        // Act - read hash table from unified memory
        unsafe
        {
            var hashTableBuffer = new IntPtr(registry.TopicHashTablePtr);
            var hashTablePtr = MetalNative.GetBufferContents(hashTableBuffer);
            var hashTable = new Span<ulong>((void*)hashTablePtr, registry.HashTableCapacity);

            // Assert - count non-zero entries (should be 2 topics)
            int populatedEntries = 0;
            for (int i = 0; i < hashTable.Length; i++)
            {
                if (hashTable[i] != 0)
                {
                    populatedEntries++;

                    uint topicId = (uint)(hashTable[i] >> 32);
                    uint firstIndex = (uint)(hashTable[i] & 0xFFFFFFFF);

                    _output.WriteLine($"  Hash[{i}]: TopicId=0x{topicId:X8}, FirstIndex={firstIndex}");
                }
            }

            Assert.Equal(2, populatedEntries); // 2 unique topics
        }

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task HashTable_Should_Correctly_Map_Topics_To_Subscription_Indices()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        _ = builder.Subscribe("topic.A", kernelId: 1, queueIndex: 0);
        _ = builder.Subscribe("topic.A", kernelId: 2, queueIndex: 1); // Same topic
        _ = builder.Subscribe("topic.B", kernelId: 3, queueIndex: 2);

        var registry = await builder.BuildAsync();

        // Act - verify hash table points to first subscription for each topic
        unsafe
        {
            var subscriptionsBuffer = new IntPtr(registry.SubscriptionsPtr);
            var subscriptionsPtr = MetalNative.GetBufferContents(subscriptionsBuffer);
            var subscriptions = new Span<MetalTopicSubscription>((void*)subscriptionsPtr, registry.SubscriptionCount);

            var hashTableBuffer = new IntPtr(registry.TopicHashTablePtr);
            var hashTablePtr = MetalNative.GetBufferContents(hashTableBuffer);
            var hashTable = new Span<ulong>((void*)hashTablePtr, registry.HashTableCapacity);

            // Find first occurrence of each topic in the sorted subscription array
            // (subscriptions are sorted by TopicId, so we can't assume insertion order)
            var topicFirstIndices = new Dictionary<uint, uint>();
            for (int i = 0; i < subscriptions.Length; i++)
            {
                uint topicId = subscriptions[i].TopicId;
                if (!topicFirstIndices.ContainsKey(topicId))
                {
                    topicFirstIndices[topicId] = (uint)i;
                }
            }

            // Verify hash table entries match the first indices
            int verifiedTopics = 0;
            for (int i = 0; i < hashTable.Length; i++)
            {
                if (hashTable[i] != 0)
                {
                    uint topicId = (uint)(hashTable[i] >> 32);
                    uint firstIndex = (uint)(hashTable[i] & 0xFFFFFFFF);

                    // Hash table should point to first occurrence in sorted array
                    Assert.True(topicFirstIndices.ContainsKey(topicId),
                        $"Topic 0x{topicId:X8} found in hash table but not in subscription array");
                    Assert.Equal(topicFirstIndices[topicId], firstIndex);
                    verifiedTopics++;
                }
            }

            // Should have verified both topics (topic.A and topic.B)
            Assert.Equal(2, verifiedTopics);
        }

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task HighPriority_Subscriptions_Should_Be_Marked_With_Flag()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);
        _ = builder.Subscribe("important.events", kernelId: 1, queueIndex: 0, MetalTopicSubscription.FlagHighPriority);
        _ = builder.Subscribe("normal.events", kernelId: 2, queueIndex: 1); // No flags

        var registry = await builder.BuildAsync();

        // Act - read subscriptions and verify flags
        unsafe
        {
            var subscriptionsBuffer = new IntPtr(registry.SubscriptionsPtr);
            var subscriptionsPtr = MetalNative.GetBufferContents(subscriptionsBuffer);
            var subscriptions = new Span<MetalTopicSubscription>((void*)subscriptionsPtr, registry.SubscriptionCount);

            // Assert
            Assert.Equal(MetalTopicSubscription.FlagHighPriority, subscriptions[0].Flags);
            Assert.Equal(0, subscriptions[1].Flags);

            _output.WriteLine($"Subscription 0: Flags=0x{subscriptions[0].Flags:X4} (high priority)");
            _output.WriteLine($"Subscription 1: Flags=0x{subscriptions[1].Flags:X4} (normal)");
        }

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task Performance_BuildAsync_Should_Complete_Within_Target_Time()
    {
        // Arrange
        int subscriptionCount = 100;
        int topicCount = 20;

        using var builder = new MetalTopicRegistryBuilder(_device);

        for (int i = 0; i < subscriptionCount; i++)
        {
            int topicIndex = i % topicCount;
            _ = builder.Subscribe($"topic.{topicIndex}", (ushort)(i + 1), (ushort)i);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var registry = await builder.BuildAsync();

        stopwatch.Stop();

        // Assert - should be very fast (<10ms for 100 subscriptions)
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 50); // Generous, should be <10ms

        _output.WriteLine($"Built registry with {subscriptionCount} subscriptions in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Average per subscription: {stopwatch.Elapsed.TotalMilliseconds / subscriptionCount:F3}ms");

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
    }

    [Fact]
    public async Task Subscriptions_Should_Be_Sorted_By_TopicId()
    {
        // Arrange
        using var builder = new MetalTopicRegistryBuilder(_device);

        // Add subscriptions in non-sorted order
        _ = builder.Subscribe("topic.Z", kernelId: 3, queueIndex: 2);
        _ = builder.Subscribe("topic.A", kernelId: 1, queueIndex: 0);
        _ = builder.Subscribe("topic.M", kernelId: 2, queueIndex: 1);

        var registry = await builder.BuildAsync();

        // Act - read subscriptions
        unsafe
        {
            var subscriptionsBuffer = new IntPtr(registry.SubscriptionsPtr);
            var subscriptionsPtr = MetalNative.GetBufferContents(subscriptionsBuffer);
            var subscriptions = new Span<MetalTopicSubscription>((void*)subscriptionsPtr, registry.SubscriptionCount);

            // Assert - subscriptions should be sorted by topic ID
            for (int i = 0; i < subscriptions.Length - 1; i++)
            {
                Assert.True(subscriptions[i].TopicId <= subscriptions[i + 1].TopicId,
                    $"Subscriptions not sorted: [{i}] TopicId=0x{subscriptions[i].TopicId:X8} > [{i + 1}] TopicId=0x{subscriptions[i + 1].TopicId:X8}");
            }

            _output.WriteLine("Subscriptions are correctly sorted by TopicId:");
            for (int i = 0; i < subscriptions.Length; i++)
            {
                _output.WriteLine($"  [{i}] TopicId=0x{subscriptions[i].TopicId:X8}, KernelId={subscriptions[i].KernelId}");
            }
        }

        // Cleanup
        if (registry.SubscriptionsPtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.SubscriptionsPtr));
        }
        if (registry.TopicHashTablePtr != 0)
        {
            MetalNative.ReleaseBuffer(new IntPtr(registry.TopicHashTablePtr));
        }
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
