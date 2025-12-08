// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Atomics;

namespace DotCompute.Abstractions.Tests.Atomics;

/// <summary>
/// Unit tests for <see cref="AtomicOps"/> static class.
/// Tests atomic operations using .NET Interlocked (CPU backend behavior).
/// </summary>
public class AtomicOpsTests
{
    #region Add Tests

    [Fact]
    public void Add_Int32_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicAdd(ref target, 5);

        // Assert
        original.Should().Be(10);
        target.Should().Be(15);
    }

    [Fact]
    public void Add_Int32_NegativeValue()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicAdd(ref target, -3);

        // Assert
        original.Should().Be(10);
        target.Should().Be(7);
    }

    [Fact]
    public void Add_UInt32_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10u;

        // Act
        var original = AtomicOps.AtomicAdd(ref target, 5u);

        // Assert
        original.Should().Be(10u);
        target.Should().Be(15u);
    }

    [Fact]
    public void Add_Int64_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10L;

        // Act
        var original = AtomicOps.AtomicAdd(ref target, 5L);

        // Assert
        original.Should().Be(10L);
        target.Should().Be(15L);
    }

    [Fact]
    public void Add_UInt64_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10UL;

        // Act
        var original = AtomicOps.AtomicAdd(ref target, 5UL);

        // Assert
        original.Should().Be(10UL);
        target.Should().Be(15UL);
    }

    [Fact]
    public void Add_Float_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10.5f;

        // Act
        var original = AtomicOps.AtomicAdd(ref target, 2.5f);

        // Assert
        original.Should().Be(10.5f);
        target.Should().Be(13.0f);
    }

    [Fact]
    public void Add_Double_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10.5;

        // Act
        var original = AtomicOps.AtomicAdd(ref target, 2.5);

        // Assert
        original.Should().Be(10.5);
        target.Should().Be(13.0);
    }

    #endregion

    #region Sub Tests

    [Fact]
    public void Sub_Int32_ReturnsOriginalValue()
    {
        // Arrange
        var target = 15;

        // Act
        var original = AtomicOps.AtomicSub(ref target, 5);

        // Assert
        original.Should().Be(15);
        target.Should().Be(10);
    }

    [Fact]
    public void Sub_UInt32_ReturnsOriginalValue()
    {
        // Arrange
        var target = 15u;

        // Act
        var original = AtomicOps.AtomicSub(ref target, 5u);

        // Assert
        original.Should().Be(15u);
        target.Should().Be(10u);
    }

    [Fact]
    public void Sub_Int64_ReturnsOriginalValue()
    {
        // Arrange
        var target = 15L;

        // Act
        var original = AtomicOps.AtomicSub(ref target, 5L);

        // Assert
        original.Should().Be(15L);
        target.Should().Be(10L);
    }

    [Fact]
    public void Sub_UInt64_ReturnsOriginalValue()
    {
        // Arrange
        var target = 15UL;

        // Act
        var original = AtomicOps.AtomicSub(ref target, 5UL);

        // Assert
        original.Should().Be(15UL);
        target.Should().Be(10UL);
    }

    #endregion

    #region Exchange Tests

    [Fact]
    public void Exchange_Int32_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicExchange(ref target, 20);

        // Assert
        original.Should().Be(10);
        target.Should().Be(20);
    }

    [Fact]
    public void Exchange_Float_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10.5f;

        // Act
        var original = AtomicOps.AtomicExchange(ref target, 20.5f);

        // Assert
        original.Should().Be(10.5f);
        target.Should().Be(20.5f);
    }

    [Fact]
    public void Exchange_Double_ReturnsOriginalValue()
    {
        // Arrange
        var target = 10.5;

        // Act
        var original = AtomicOps.AtomicExchange(ref target, 20.5);

        // Assert
        original.Should().Be(10.5);
        target.Should().Be(20.5);
    }

    #endregion

    #region CompareExchange Tests

    [Fact]
    public void CompareExchange_Int32_SuccessfulSwap()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicCompareExchange(ref target, 10, 20);

        // Assert
        original.Should().Be(10);
        target.Should().Be(20);
    }

    [Fact]
    public void CompareExchange_Int32_FailedSwap()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicCompareExchange(ref target, 5, 20);

        // Assert
        original.Should().Be(10);
        target.Should().Be(10); // Unchanged
    }

    [Fact]
    public void CompareExchange_Int64_SuccessfulSwap()
    {
        // Arrange
        var target = 10L;

        // Act
        var original = AtomicOps.AtomicCompareExchange(ref target, 10L, 20L);

        // Assert
        original.Should().Be(10L);
        target.Should().Be(20L);
    }

    [Fact]
    public void CompareExchange_Float_SuccessfulSwap()
    {
        // Arrange
        var target = 10.5f;

        // Act
        var original = AtomicOps.AtomicCompareExchange(ref target, 10.5f, 20.5f);

        // Assert
        original.Should().Be(10.5f);
        target.Should().Be(20.5f);
    }

    [Fact]
    public void CompareExchange_Double_SuccessfulSwap()
    {
        // Arrange
        var target = 10.5;

        // Act
        var original = AtomicOps.AtomicCompareExchange(ref target, 10.5, 20.5);

        // Assert
        original.Should().Be(10.5);
        target.Should().Be(20.5);
    }

    #endregion

    #region Min/Max Tests

    [Fact]
    public void Min_Int32_UpdatesWhenNewValueIsSmaller()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicMin(ref target, 5);

        // Assert
        original.Should().Be(10);
        target.Should().Be(5);
    }

    [Fact]
    public void Min_Int32_NoUpdateWhenNewValueIsLarger()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicMin(ref target, 15);

        // Assert
        original.Should().Be(10);
        target.Should().Be(10);
    }

    [Fact]
    public void Max_Int32_UpdatesWhenNewValueIsLarger()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicMax(ref target, 15);

        // Assert
        original.Should().Be(10);
        target.Should().Be(15);
    }

    [Fact]
    public void Max_Int32_NoUpdateWhenNewValueIsSmaller()
    {
        // Arrange
        var target = 10;

        // Act
        var original = AtomicOps.AtomicMax(ref target, 5);

        // Assert
        original.Should().Be(10);
        target.Should().Be(10);
    }

    [Fact]
    public void Min_Int64_UpdatesWhenNewValueIsSmaller()
    {
        // Arrange
        var target = 100L;

        // Act
        var original = AtomicOps.AtomicMin(ref target, 50L);

        // Assert
        original.Should().Be(100L);
        target.Should().Be(50L);
    }

    [Fact]
    public void Max_UInt64_UpdatesWhenNewValueIsLarger()
    {
        // Arrange
        var target = 100UL;

        // Act
        var original = AtomicOps.AtomicMax(ref target, 150UL);

        // Assert
        original.Should().Be(100UL);
        target.Should().Be(150UL);
    }

    #endregion

    #region Bitwise Operation Tests

    [Fact]
    public void And_Int32_PerformsBitwiseAnd()
    {
        // Arrange
        var target = 0b1111; // 15

        // Act
        var original = AtomicOps.AtomicAnd(ref target, 0b0101); // 5

        // Assert
        original.Should().Be(15);
        target.Should().Be(0b0101); // 5
    }

    [Fact]
    public void Or_Int32_PerformsBitwiseOr()
    {
        // Arrange
        var target = 0b0101; // 5

        // Act
        var original = AtomicOps.AtomicOr(ref target, 0b1010); // 10

        // Assert
        original.Should().Be(5);
        target.Should().Be(0b1111); // 15
    }

    [Fact]
    public void Xor_Int32_PerformsBitwiseXor()
    {
        // Arrange
        var target = 0b1111; // 15

        // Act
        var original = AtomicOps.AtomicXor(ref target, 0b0101); // 5

        // Assert
        original.Should().Be(15);
        target.Should().Be(0b1010); // 10
    }

    [Fact]
    public void And_Int64_PerformsBitwiseAnd()
    {
        // Arrange
        var target = 0b1111L; // 15

        // Act
        var original = AtomicOps.AtomicAnd(ref target, 0b0101L); // 5

        // Assert
        original.Should().Be(15L);
        target.Should().Be(0b0101L); // 5
    }

    [Fact]
    public void Or_UInt32_PerformsBitwiseOr()
    {
        // Arrange
        var target = 0b0101u; // 5

        // Act
        var original = AtomicOps.AtomicOr(ref target, 0b1010u); // 10

        // Assert
        original.Should().Be(5u);
        target.Should().Be(0b1111u); // 15
    }

    [Fact]
    public void Xor_UInt64_PerformsBitwiseXor()
    {
        // Arrange
        var target = 0b1111UL; // 15

        // Act
        var original = AtomicOps.AtomicXor(ref target, 0b0101UL); // 5

        // Assert
        original.Should().Be(15UL);
        target.Should().Be(0b1010UL); // 10
    }

    #endregion

    #region Load/Store Tests

    [Fact]
    public void Load_Int32_ReturnsCurrentValue()
    {
        // Arrange
        var target = 42;

        // Act
        var value = AtomicOps.AtomicLoad(ref target, MemoryOrder.Acquire);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void Load_Int64_ReturnsCurrentValue()
    {
        // Arrange
        var target = 42L;

        // Act
        var value = AtomicOps.AtomicLoad(ref target, MemoryOrder.SequentiallyConsistent);

        // Assert
        value.Should().Be(42L);
    }

    [Fact]
    public void Store_Int32_SetsValue()
    {
        // Arrange
        var target = 0;

        // Act
        AtomicOps.AtomicStore(ref target, 42, MemoryOrder.Release);

        // Assert
        target.Should().Be(42);
    }

    [Fact]
    public void Store_Int64_SetsValue()
    {
        // Arrange
        var target = 0L;

        // Act
        AtomicOps.AtomicStore(ref target, 42L, MemoryOrder.SequentiallyConsistent);

        // Assert
        target.Should().Be(42L);
    }

    #endregion

    #region Fence Tests

    [Fact]
    public void ThreadFence_Workgroup_DoesNotThrow()
    {
        // Act & Assert
        var act = () => AtomicOps.ThreadFence(MemoryScope.Workgroup);
        act.Should().NotThrow();
    }

    [Fact]
    public void ThreadFence_Device_DoesNotThrow()
    {
        // Act & Assert
        var act = () => AtomicOps.ThreadFence(MemoryScope.Device);
        act.Should().NotThrow();
    }

    [Fact]
    public void ThreadFence_System_DoesNotThrow()
    {
        // Act & Assert
        var act = () => AtomicOps.ThreadFence(MemoryScope.System);
        act.Should().NotThrow();
    }

    [Fact]
    public void MemoryBarrier_DoesNotThrow()
    {
        // Act & Assert
        var act = () => AtomicOps.MemoryBarrier();
        act.Should().NotThrow();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task Add_Int32_ThreadSafe()
    {
        // Arrange
        var target = 0;
        var iterations = 10000;
        var tasks = new List<Task>();

        // Act
        for (var i = 0; i < 4; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < iterations; j++)
                {
                    AtomicOps.AtomicAdd(ref target, 1);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        target.Should().Be(4 * iterations);
    }

    [Fact]
    public async Task CompareExchange_Int32_SpinLockPattern()
    {
        // Arrange
        var counter = 0;
        var iterations = 1000;
        var tasks = new List<Task>();

        // Act - Simulate increment using CAS loop
        for (var i = 0; i < 4; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < iterations; j++)
                {
                    int oldValue, newValue;
                    do
                    {
                        oldValue = counter;
                        newValue = oldValue + 1;
                    } while (AtomicOps.AtomicCompareExchange(ref counter, oldValue, newValue) != oldValue);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        counter.Should().Be(4 * iterations);
    }

    #endregion
}
