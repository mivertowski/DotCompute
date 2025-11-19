// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.RingKernels;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalKernelRoutingTable structure and utilities.
/// </summary>
public sealed class MetalKernelRoutingTableTests
{
    [Fact]
    public void CreateEmpty_Should_Initialize_All_Fields_To_Zero()
    {
        // Act
        var table = MetalKernelRoutingTable.CreateEmpty();

        // Assert
        Assert.Equal(0, table.KernelCount);
        Assert.Equal(0, table.KernelControlBlocksPtr);
        Assert.Equal(0, table.OutputQueuesPtr);
        Assert.Equal(0, table.RoutingHashTablePtr);
        Assert.Equal(0, table.HashTableCapacity);
    }

    [Fact]
    public void Validate_Should_Return_True_For_Empty_Table()
    {
        // Arrange
        var table = MetalKernelRoutingTable.CreateEmpty();

        // Act
        bool isValid = table.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_Should_Return_False_When_KernelCount_Is_Negative()
    {
        // Arrange
        var table = new MetalKernelRoutingTable
        {
            KernelCount = -1
        };

        // Act
        bool isValid = table.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_Should_Return_False_When_KernelCount_Exceeds_Max()
    {
        // Arrange
        var table = new MetalKernelRoutingTable
        {
            KernelCount = 65536 // Max is 65535
        };

        // Act
        bool isValid = table.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_Should_Return_False_When_HashTableCapacity_Not_Power_Of_Two()
    {
        // Arrange
        var table = new MetalKernelRoutingTable
        {
            KernelCount = 10,
            HashTableCapacity = 30, // Not a power of 2
            KernelControlBlocksPtr = 1,
            OutputQueuesPtr = 1,
            RoutingHashTablePtr = 1
        };

        // Act
        bool isValid = table.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_Should_Return_False_When_Capacity_Less_Than_KernelCount()
    {
        // Arrange
        var table = new MetalKernelRoutingTable
        {
            KernelCount = 64,
            HashTableCapacity = 32, // Less than kernel count
            KernelControlBlocksPtr = 1,
            OutputQueuesPtr = 1,
            RoutingHashTablePtr = 1
        };

        // Act
        bool isValid = table.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_Should_Return_False_When_Pointers_Are_Zero()
    {
        // Arrange
        var table = new MetalKernelRoutingTable
        {
            KernelCount = 10,
            HashTableCapacity = 32,
            KernelControlBlocksPtr = 0, // Zero pointer
            OutputQueuesPtr = 1,
            RoutingHashTablePtr = 1
        };

        // Act
        bool isValid = table.Validate();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_Should_Return_True_When_All_Invariants_Satisfied()
    {
        // Arrange
        var table = new MetalKernelRoutingTable
        {
            KernelCount = 10,
            HashTableCapacity = 32, // Power of 2, >= kernel count
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000
        };

        // Act
        bool isValid = table.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(0, 32)]   // Empty: minimum 32 (simdgroup size)
    [InlineData(1, 32)]   // 1 kernel: 32 (2× rounded to simdgroup)
    [InlineData(10, 32)]  // 10 kernels: 32 (2× = 20, rounded to 32)
    [InlineData(20, 64)]  // 20 kernels: 64 (2× = 40, rounded to 64)
    [InlineData(100, 256)] // 100 kernels: 256 (2× = 200, rounded to 256)
    [InlineData(1000, 2048)] // 1000 kernels: 2048 (2× = 2000, rounded to 2048)
    [InlineData(40000, 65536)] // 40000 kernels: 65536 (cap at max)
    public void CalculateCapacity_Should_Return_Correct_Power_Of_Two(int kernelCount, int expectedCapacity)
    {
        // Act
        int capacity = MetalKernelRoutingTable.CalculateCapacity(kernelCount);

        // Assert
        Assert.Equal(expectedCapacity, capacity);
    }

    [Fact]
    public void CalculateCapacity_Should_Always_Return_Power_Of_Two()
    {
        // Arrange
        var testCounts = new[] { 1, 5, 15, 31, 63, 127, 255, 511, 1023, 2047 };

        // Act & Assert
        foreach (var count in testCounts)
        {
            int capacity = MetalKernelRoutingTable.CalculateCapacity(count);

            // Verify it's a power of 2: (capacity & (capacity - 1)) == 0
            Assert.True((capacity & (capacity - 1)) == 0, $"Capacity {capacity} is not a power of 2");

            // Verify it's at least 2× the kernel count (50% load factor)
            Assert.True(capacity >= count * 2, $"Capacity {capacity} is less than 2× kernel count {count}");

            // Verify it's a multiple of 32 (simdgroup size)
            Assert.True(capacity % 32 == 0, $"Capacity {capacity} is not a multiple of 32");
        }
    }

    [Fact]
    public void CalculateCapacity_Should_Not_Exceed_Maximum()
    {
        // Act
        int capacity = MetalKernelRoutingTable.CalculateCapacity(100000);

        // Assert
        Assert.Equal(65536, capacity); // Maximum capacity
    }

    [Fact]
    public void Equals_Should_Return_True_For_Identical_Tables()
    {
        // Arrange
        var table1 = new MetalKernelRoutingTable
        {
            KernelCount = 10,
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000,
            HashTableCapacity = 32
        };

        var table2 = new MetalKernelRoutingTable
        {
            KernelCount = 10,
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000,
            HashTableCapacity = 32
        };

        // Act & Assert
        Assert.True(table1.Equals(table2));
        Assert.True(table1 == table2);
        Assert.False(table1 != table2);
    }

    [Fact]
    public void Equals_Should_Return_False_For_Different_Tables()
    {
        // Arrange
        var table1 = new MetalKernelRoutingTable
        {
            KernelCount = 10,
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000,
            HashTableCapacity = 32
        };

        var table2 = new MetalKernelRoutingTable
        {
            KernelCount = 20, // Different
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000,
            HashTableCapacity = 32
        };

        // Act & Assert
        Assert.False(table1.Equals(table2));
        Assert.False(table1 == table2);
        Assert.True(table1 != table2);
    }

    [Fact]
    public void GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var table = new MetalKernelRoutingTable
        {
            KernelCount = 10,
            KernelControlBlocksPtr = 0x1000,
            OutputQueuesPtr = 0x2000,
            RoutingHashTablePtr = 0x3000,
            HashTableCapacity = 32
        };

        // Act
        int hash1 = table.GetHashCode();
        int hash2 = table.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_Should_Differ_For_Different_Tables()
    {
        // Arrange
        var table1 = new MetalKernelRoutingTable { KernelCount = 10 };
        var table2 = new MetalKernelRoutingTable { KernelCount = 20 };

        // Act
        int hash1 = table1.GetHashCode();
        int hash2 = table2.GetHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }
}
