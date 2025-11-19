// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.RingKernels;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalTopicSubscription and MetalTopicRegistry structures.
/// </summary>
public sealed class MetalTopicRegistryTests
{
    [Fact]
    public void TopicSubscription_Create_Should_Initialize_With_Correct_Values()
    {
        // Arrange
        uint topicId = 0x7A3B9C12;
        uint kernelId = 0x1234;
        ushort queueIndex = 5;
        ushort flags = MetalTopicSubscription.FlagHighPriority;

        // Act
        var subscription = MetalTopicSubscription.Create(topicId, kernelId, queueIndex, flags);

        // Assert
        Assert.Equal(topicId, subscription.TopicId);
        Assert.Equal(kernelId, subscription.KernelId);
        Assert.Equal(queueIndex, subscription.QueueIndex);
        Assert.Equal(flags, subscription.Flags);
    }

    [Fact]
    public void TopicSubscription_Create_Should_Default_Flags_To_Zero()
    {
        // Act
        var subscription = MetalTopicSubscription.Create(0x12345678, 100, 10);

        // Assert
        Assert.Equal(0, subscription.Flags);
    }

    [Fact]
    public void TopicSubscription_FlagConstants_Should_Have_Correct_Values()
    {
        // Assert
        Assert.Equal(0x0001, MetalTopicSubscription.FlagWildcard);
        Assert.Equal(0x0002, MetalTopicSubscription.FlagHighPriority);
    }

    [Fact]
    public void TopicSubscription_Equals_Should_Return_True_For_Identical_Subscriptions()
    {
        // Arrange
        var sub1 = MetalTopicSubscription.Create(0x12345678, 100, 10, MetalTopicSubscription.FlagHighPriority);
        var sub2 = MetalTopicSubscription.Create(0x12345678, 100, 10, MetalTopicSubscription.FlagHighPriority);

        // Act & Assert
        Assert.True(sub1.Equals(sub2));
        Assert.True(sub1 == sub2);
        Assert.False(sub1 != sub2);
    }

    [Fact]
    public void TopicSubscription_Equals_Should_Return_False_For_Different_Subscriptions()
    {
        // Arrange
        var sub1 = MetalTopicSubscription.Create(0x12345678, 100, 10);
        var sub2 = MetalTopicSubscription.Create(0x87654321, 200, 20);

        // Act & Assert
        Assert.False(sub1.Equals(sub2));
        Assert.False(sub1 == sub2);
        Assert.True(sub1 != sub2);
    }

    [Fact]
    public void TopicSubscription_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var subscription = MetalTopicSubscription.Create(0x12345678, 100, 10);

        // Act
        int hash1 = subscription.GetHashCode();
        int hash2 = subscription.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void TopicSubscription_GetHashCode_Should_Differ_For_Different_Subscriptions()
    {
        // Arrange
        var sub1 = MetalTopicSubscription.Create(0x12345678, 100, 10);
        var sub2 = MetalTopicSubscription.Create(0x87654321, 200, 20);

        // Act
        int hash1 = sub1.GetHashCode();
        int hash2 = sub2.GetHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void TopicRegistry_CreateEmpty_Should_Initialize_All_Fields_To_Zero()
    {
        // Act
        var registry = MetalTopicRegistry.CreateEmpty();

        // Assert
        Assert.Equal(0, registry.SubscriptionCount);
        Assert.Equal(0, registry.SubscriptionsPtr);
        Assert.Equal(0, registry.TopicHashTablePtr);
        Assert.Equal(0, registry.HashTableCapacity);
    }

    [Fact]
    public void TopicRegistry_Validate_Should_Return_True_For_Empty_Registry()
    {
        // Arrange
        var registry = MetalTopicRegistry.CreateEmpty();

        // Act & Assert
        Assert.True(registry.Validate());
    }

    [Fact]
    public void TopicRegistry_Validate_Should_Return_False_For_Negative_SubscriptionCount()
    {
        // Arrange
        var registry = new MetalTopicRegistry
        {
            SubscriptionCount = -1,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act & Assert
        Assert.False(registry.Validate());
    }

    [Fact]
    public void TopicRegistry_Validate_Should_Return_False_For_SubscriptionCount_Exceeding_Max()
    {
        // Arrange
        var registry = new MetalTopicRegistry
        {
            SubscriptionCount = 65536, // Exceeds max (65535)
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act & Assert
        Assert.False(registry.Validate());
    }

    [Fact]
    public void TopicRegistry_Validate_Should_Return_False_For_Non_Power_Of_Two_Capacity()
    {
        // Arrange
        var registry = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 15 // Not power of 2
        };

        // Act & Assert
        Assert.False(registry.Validate());
    }

    [Fact]
    public void TopicRegistry_Validate_Should_Return_False_For_Zero_Pointer_With_Non_Zero_Count()
    {
        // Arrange
        var registry = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0, // Should be non-zero
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act & Assert
        Assert.False(registry.Validate());
    }

    [Fact]
    public void TopicRegistry_Validate_Should_Return_True_For_Valid_Registry()
    {
        // Arrange
        var registry = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act & Assert
        Assert.True(registry.Validate());
    }

    [Theory]
    [InlineData(0, 16)]    // 0 topics → 16 (minimum)
    [InlineData(1, 16)]    // 1 topic → 16 (minimum)
    [InlineData(8, 16)]    // 8 topics → 16 (minimum)
    [InlineData(9, 32)]    // 9 topics → 32 (2×9=18, round up to 32)
    [InlineData(16, 32)]   // 16 topics → 32
    [InlineData(17, 64)]   // 17 topics → 64 (2×17=34, round up to 64)
    [InlineData(32, 64)]   // 32 topics → 64
    [InlineData(100, 256)] // 100 topics → 256
    [InlineData(256, 512)] // 256 topics → 512
    [InlineData(32768, 65536)] // 32768 topics → 65536 (max)
    [InlineData(65535, 65536)] // 65535 topics → 65536 (max, capped)
    public void TopicRegistry_CalculateCapacity_Should_Return_Correct_Capacity(int uniqueTopics, int expectedCapacity)
    {
        // Act
        int actualCapacity = MetalTopicRegistry.CalculateCapacity(uniqueTopics);

        // Assert
        Assert.Equal(expectedCapacity, actualCapacity);

        // Verify power of 2
        Assert.True((actualCapacity & (actualCapacity - 1)) == 0);

        // Verify at least 2× unique topics (unless capped at 65536)
        if (expectedCapacity < 65536)
        {
            Assert.True(actualCapacity >= uniqueTopics * 2);
        }
    }

    [Fact]
    public void TopicRegistry_CalculateCapacity_Should_Return_Minimum_For_Negative_Count()
    {
        // Act
        int capacity = MetalTopicRegistry.CalculateCapacity(-10);

        // Assert
        Assert.Equal(16, capacity);
    }

    [Fact]
    public void TopicRegistry_CalculateCapacity_Should_Cap_At_Maximum()
    {
        // Act
        int capacity = MetalTopicRegistry.CalculateCapacity(100000);

        // Assert
        Assert.Equal(65536, capacity);
    }

    [Fact]
    public void TopicRegistry_Equals_Should_Return_True_For_Identical_Registries()
    {
        // Arrange
        var registry1 = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        var registry2 = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act & Assert
        Assert.True(registry1.Equals(registry2));
        Assert.True(registry1 == registry2);
        Assert.False(registry1 != registry2);
    }

    [Fact]
    public void TopicRegistry_Equals_Should_Return_False_For_Different_Registries()
    {
        // Arrange
        var registry1 = MetalTopicRegistry.CreateEmpty();
        var registry2 = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act & Assert
        Assert.False(registry1.Equals(registry2));
        Assert.False(registry1 == registry2);
        Assert.True(registry1 != registry2);
    }

    [Fact]
    public void TopicRegistry_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var registry = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act
        int hash1 = registry.GetHashCode();
        int hash2 = registry.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void TopicRegistry_GetHashCode_Should_Differ_For_Different_Registries()
    {
        // Arrange
        var registry1 = MetalTopicRegistry.CreateEmpty();
        var registry2 = new MetalTopicRegistry
        {
            SubscriptionCount = 10,
            SubscriptionsPtr = 0x1000,
            TopicHashTablePtr = 0x2000,
            HashTableCapacity = 16
        };

        // Act
        int hash1 = registry1.GetHashCode();
        int hash2 = registry2.GetHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void TopicSubscription_Flags_Should_Be_Combinable()
    {
        // Arrange
        ushort combinedFlags = (ushort)(MetalTopicSubscription.FlagWildcard | MetalTopicSubscription.FlagHighPriority);

        // Act
        var subscription = MetalTopicSubscription.Create(0x12345678, 100, 10, combinedFlags);

        // Assert
        Assert.Equal(0x0003, subscription.Flags);
        Assert.True((subscription.Flags & MetalTopicSubscription.FlagWildcard) != 0);
        Assert.True((subscription.Flags & MetalTopicSubscription.FlagHighPriority) != 0);
    }

    [Fact]
    public void TopicRegistry_Size_Should_Be_32_Bytes()
    {
        // Arrange
        var registry = MetalTopicRegistry.CreateEmpty();

        // Act
        int size = System.Runtime.InteropServices.Marshal.SizeOf<MetalTopicRegistry>();

        // Assert
        Assert.Equal(32, size);
    }

    [Fact]
    public void TopicSubscription_Size_Should_Be_12_Bytes()
    {
        // Arrange
        var subscription = MetalTopicSubscription.Create(0, 0, 0);

        // Act
        int size = System.Runtime.InteropServices.Marshal.SizeOf<MetalTopicSubscription>();

        // Assert
        Assert.Equal(12, size);
    }
}
