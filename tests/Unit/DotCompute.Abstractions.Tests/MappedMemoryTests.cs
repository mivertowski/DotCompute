// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests;

/// <summary>
/// Comprehensive unit tests for the MappedMemory&lt;T&gt; struct.
/// </summary>
public class MappedMemoryTests
{
    private readonly Mock<IBuffer<float>> _mockBuffer;

    public MappedMemoryTests()
    {
        _mockBuffer = new Mock<IBuffer<float>>();
    }

    private static MappedMemory<T> CreateMappedMemory<T>(IBuffer<T> buffer, Memory<T> memory, MapMode mode) where T : unmanaged
    {
        // Use reflection to create MappedMemory since constructor is internal
        var constructor = typeof(MappedMemory<T>).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        return(MappedMemory<T>)constructor.Invoke(new object[] { buffer, memory, mode });
    }

    #region Property Tests

    [Fact]
    public void Memory_ShouldReturnCorrectMemory()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var memory = new Memory<float>(sourceData);
        var mode = MapMode.ReadWrite;
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, mode);

        // Act
        var actualMemory = mappedMemory.Memory;

        // Assert
        actualMemory.Length.Should().Be(sourceData.Length);
        actualMemory.Span.ToArray().Should().BeEquivalentTo(sourceData);
    }

    [Fact]
    public void Mode_ShouldReturnCorrectMapMode()
    {
        // Arrange
        var memory = new Memory<float>(new float[10]);
        var mode = MapMode.Read;
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, mode);

        // Act
        var actualMode = mappedMemory.Mode;

        // Assert
        Assert.Equal(mode, actualMode);
    }

    [Theory]
    [InlineData(MapMode.Read)]
    [InlineData(MapMode.Write)]
    [InlineData(MapMode.ReadWrite)]
    [InlineData(MapMode.Read | MapMode.NoWait)]
    [InlineData(MapMode.Write | MapMode.Discard)]
    [InlineData(MapMode.ReadWrite | MapMode.NoWait | MapMode.Discard)]
    public void Mode_ShouldSupportAllMapModes(MapMode mode)
    {
        // Arrange
        var memory = new Memory<float>(new float[5]);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, mode);

        // Act
        var actualMode = mappedMemory.Mode;

        // Assert
        Assert.Equal(mode, actualMode);
    }

    [Fact]
    public void Span_ShouldReturnMemorySpan()
    {
        // Arrange
        var sourceData = new float[] { 10.0f, 20.0f, 30.0f };
        var memory = new Memory<float>(sourceData);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act
        var span = mappedMemory.Span;

        // Assert
        span.Length.Should().Be(sourceData.Length);
        span.ToArray().Should().BeEquivalentTo(sourceData);
    }

    [Fact]
    public void Span_ShouldAllowDataModification()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f };
        var memory = new Memory<float>(sourceData);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act
        var span = mappedMemory.Span;
        span[0] = 99.0f;
        span[1] = 88.0f;
        span[2] = 77.0f;

        // Assert
        mappedMemory.Memory.Span[0].Should().Be(99.0f);
        mappedMemory.Memory.Span[1].Should().Be(88.0f);
        mappedMemory.Memory.Span[2].Should().Be(77.0f);
        sourceData[0].Should().Be(99.0f); // Original array should be modified too
        sourceData[1].Should().Be(88.0f);
        sourceData[2].Should().Be(77.0f);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f };
        var memory = new Memory<float>(sourceData);
        var mode = MapMode.Read;

        // Act
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, mode);

        // Assert
        mappedMemory.Memory.Length.Should().Be(2);
        mappedMemory.Mode.Should().Be(mode);
        mappedMemory.Span.Length.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithEmptyMemory_ShouldInitializeCorrectly()
    {
        // Arrange
        var emptyMemory = Memory<float>.Empty;
        var mode = MapMode.ReadWrite;

        // Act
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, emptyMemory, mode);

        // Assert
        mappedMemory.Memory.Length.Should().Be(0);
        mappedMemory.Mode.Should().Be(mode);
        mappedMemory.Span.Length.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithLargeMemory_ShouldHandleCorrectly()
    {
        // Arrange
        var largeData = new float[1000000]; // 1 million elements
        var memory = new Memory<float>(largeData);
        var mode = MapMode.ReadWrite;

        // Act
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, mode);

        // Assert
        mappedMemory.Memory.Length.Should().Be(1000000);
        mappedMemory.Mode.Should().Be(mode);
        mappedMemory.Span.Length.Should().Be(1000000);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldCompleteWithoutError()
    {
        // Arrange
        var memory = new Memory<float>(new float[10]);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act & Assert(should not throw)
        mappedMemory.Dispose();
    }

    [Fact]
    public void Dispose_MultipleCallsShouldBeIdempotent()
    {
        // Arrange
        var memory = new Memory<float>(new float[5]);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act & Assert(should not throw)
        mappedMemory.Dispose();
        mappedMemory.Dispose();
        mappedMemory.Dispose();
    }

    [Fact]
    public void Dispose_ShouldNotAffectMemoryAccess()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f };
        var memory = new Memory<float>(sourceData);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act
        mappedMemory.Dispose();

        // Assert - Memory should still be accessible
        mappedMemory.Memory.Length.Should().Be(3);
        mappedMemory.Span.Length.Should().Be(3);
        mappedMemory.Mode.Should().Be(MapMode.ReadWrite);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameInstance_ShouldReturnTrue()
    {
        // Arrange
        var memory = new Memory<float>(new float[10]);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act & Assert
        mappedMemory.Equals(mappedMemory).Should().BeTrue();
        mappedMemory.Equals(mappedMemory).Should().BeTrue();
       (!mappedMemory.Equals(null)).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithIdenticalProperties_ShouldReturnTrue()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f };
        var memory1 = new Memory<float>(sourceData);
        var memory2 = new Memory<float>(sourceData); // Same underlying data
        var mode = MapMode.Read;

        var mappedMemory1 = CreateMappedMemory(_mockBuffer.Object, memory1, mode);
        var mappedMemory2 = CreateMappedMemory(_mockBuffer.Object, memory2, mode);

        // Act & Assert
        mappedMemory1.Equals(mappedMemory2).Should().BeTrue();
        (mappedMemory1 == mappedMemory2).Should().BeTrue();
        (mappedMemory1 != mappedMemory2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentBuffers_ShouldReturnFalse()
    {
        // Arrange
        var mockBuffer2 = new Mock<IBuffer<float>>();
        var memory = new Memory<float>(new float[5]);
        var mode = MapMode.ReadWrite;

        var mappedMemory1 = CreateMappedMemory(_mockBuffer.Object, memory, mode);
        var mappedMemory2 = CreateMappedMemory(mockBuffer2.Object, memory, mode);

        // Act & Assert
        mappedMemory1.Equals(mappedMemory2).Should().BeFalse();
       (mappedMemory1 == mappedMemory2).Should().BeFalse();
       (mappedMemory1 != mappedMemory2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentModes_ShouldReturnFalse()
    {
        // Arrange
        var memory = new Memory<float>(new float[5]);
        var mappedMemory1 = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.Read);
        var mappedMemory2 = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.Write);

        // Act & Assert
        mappedMemory1.Equals(mappedMemory2).Should().BeFalse();
       (mappedMemory1 == mappedMemory2).Should().BeFalse();
       (mappedMemory1 != mappedMemory2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentMemory_ShouldReturnFalse()
    {
        // Arrange
        var memory1 = new Memory<float>(new float[5]);
        var memory2 = new Memory<float>(new float[5]);
        var mode = MapMode.ReadWrite;

        var mappedMemory1 = CreateMappedMemory(_mockBuffer.Object, memory1, mode);
        var mappedMemory2 = CreateMappedMemory(_mockBuffer.Object, memory2, mode);

        // Act & Assert
        mappedMemory1.Equals(mappedMemory2).Should().BeFalse();
       (mappedMemory1 == mappedMemory2).Should().BeFalse();
       (mappedMemory1 != mappedMemory2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNullObject_ShouldReturnFalse()
    {
        // Arrange
        var memory = new Memory<float>(new float[5]);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act & Assert
        mappedMemory.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var memory = new Memory<float>(new float[5]);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);
        var otherObject = "not a mapped memory";

        // Act & Assert
        mappedMemory.Equals(otherObject).Should().BeFalse();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_WithSameInstance_ShouldReturnSameHashCode()
    {
        // Arrange
        var memory = new Memory<float>(new float[10]);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act
        var hashCode1 = mappedMemory.GetHashCode();
        var hashCode2 = mappedMemory.GetHashCode();

        // Assert
        Assert.Equal(hashCode2, hashCode1);
    }

    [Fact]
    public void GetHashCode_WithEqualInstances_ShouldReturnSameHashCode()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f };
        var memory1 = new Memory<float>(sourceData);
        var memory2 = new Memory<float>(sourceData);
        var mode = MapMode.Read;

        var mappedMemory1 = CreateMappedMemory(_mockBuffer.Object, memory1, mode);
        var mappedMemory2 = CreateMappedMemory(_mockBuffer.Object, memory2, mode);

        // Act
        var hashCode1 = mappedMemory1.GetHashCode();
        var hashCode2 = mappedMemory2.GetHashCode();

        // Assert
        Assert.Equal(hashCode2, hashCode1);
    }

    [Fact]
    public void GetHashCode_WithDifferentBuffers_ShouldReturnDifferentHashCodes()
    {
        // Arrange
        var mockBuffer2 = new Mock<IBuffer<float>>();
        var memory = new Memory<float>(new float[5]);
        var mode = MapMode.ReadWrite;

        var mappedMemory1 = CreateMappedMemory(_mockBuffer.Object, memory, mode);
        var mappedMemory2 = CreateMappedMemory(mockBuffer2.Object, memory, mode);

        // Act
        var hashCode1 = mappedMemory1.GetHashCode();
        var hashCode2 = mappedMemory2.GetHashCode();

        // Assert
        hashCode1.Should().NotBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_WithDifferentModes_ShouldReturnDifferentHashCodes()
    {
        // Arrange
        var memory = new Memory<float>(new float[5]);
        var mappedMemory1 = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.Read);
        var mappedMemory2 = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.Write);

        // Act
        var hashCode1 = mappedMemory1.GetHashCode();
        var hashCode2 = mappedMemory2.GetHashCode();

        // Assert
        hashCode1.Should().NotBe(hashCode2);
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void MappedMemory_ShouldWorkWithDifferentUnmanagedTypes()
    {
        // Arrange
        var intBuffer = new Mock<IBuffer<int>>();
        var doubleBuffer = new Mock<IBuffer<double>>();
        var byteBuffer = new Mock<IBuffer<byte>>();

        var intMemory = new Memory<int>(new int[] { 1, 2, 3 });
        var doubleMemory = new Memory<double>(new double[] { 1.0, 2.0, 3.0 });
        var byteMemory = new Memory<byte>(new byte[] { 0x01, 0x02, 0x03 });

        // Act
        var intMappedMemory = CreateMappedMemory(intBuffer.Object, intMemory, MapMode.ReadWrite);
        var doubleMappedMemory = CreateMappedMemory(doubleBuffer.Object, doubleMemory, MapMode.Read);
        var byteMappedMemory = CreateMappedMemory(byteBuffer.Object, byteMemory, MapMode.Write);

        // Assert
        intMappedMemory.Memory.Length.Should().Be(3);
        intMappedMemory.Mode.Should().Be(MapMode.ReadWrite);
        
        doubleMappedMemory.Memory.Length.Should().Be(3);
        doubleMappedMemory.Mode.Should().Be(MapMode.Read);
        
        byteMappedMemory.Memory.Length.Should().Be(3);
        byteMappedMemory.Mode.Should().Be(MapMode.Write);
    }

    #endregion

    #region IDisposable Implementation Tests

    [Fact]
    public void MappedMemory_ShouldImplementIDisposable()
    {
        // Arrange & Act
        var type = typeof(MappedMemory<float>);

        // Assert
        Assert.IsAssignableFrom<IDisposable>(type);
    }

    [Fact]
    public void MappedMemory_ShouldImplementIEquatable()
    {
        // Arrange & Act
        var type = typeof(MappedMemory<float>);

        // Assert
        type.Should().BeAssignableTo<IEquatable<MappedMemory<float>>>();
    }

    #endregion

    #region Using Statement Tests

    [Fact]
    public void UsingStatement_ShouldDisposeCorrectly()
    {
        // Arrange
        var memory = new Memory<float>(new float[10]);

        // Act & Assert(should not throw)
        using(var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite))
        {
            mappedMemory.Memory.Length.Should().Be(10);
            mappedMemory.Mode.Should().Be(MapMode.ReadWrite);
        } // Dispose called here
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void MappedMemory_WithZeroLengthMemory_ShouldHandleCorrectly()
    {
        // Arrange
        var emptyMemory = Memory<float>.Empty;
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, emptyMemory, MapMode.ReadWrite);

        // Act & Assert
        mappedMemory.Memory.Length.Should().Be(0);
        mappedMemory.Span.Length.Should().Be(0);
        mappedMemory.Mode.Should().Be(MapMode.ReadWrite);
        
        // Should not throw when disposed
        mappedMemory.Dispose();
    }

    [Fact]
    public void MappedMemory_WithSlicedMemory_ShouldHandleCorrectly()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
        var slicedMemory = new Memory<float>(sourceData, 1, 3); // Elements 2, 3, 4
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, slicedMemory, MapMode.ReadWrite);

        // Act & Assert
        mappedMemory.Memory.Length.Should().Be(3);
        mappedMemory.Span.Length.Should().Be(3);
        mappedMemory.Span[0].Should().Be(2.0f);
        mappedMemory.Span[1].Should().Be(3.0f);
        mappedMemory.Span[2].Should().Be(4.0f);
    }

    [Fact]
    public void MappedMemory_MemoryModification_ShouldReflectInOriginalData()
    {
        // Arrange
        var sourceData = new float[] { 10.0f, 20.0f, 30.0f };
        var memory = new Memory<float>(sourceData);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        // Act
        mappedMemory.Span[1] = 999.0f;

        // Assert
        sourceData[1].Should().Be(999.0f);
        mappedMemory.Memory.Span[1].Should().Be(999.0f);
    }

    [Fact]
    public void MappedMemory_WithNullBuffer_ShouldStillWork()
    {
        // Note: In real implementation, null buffer might be handled differently
        // but the struct should still function for memory operations
        
        // Arrange
        var memory = new Memory<float>(new float[] { 1.0f, 2.0f });
        var mappedMemory = CreateMappedMemory(null!, memory, MapMode.ReadWrite);

        // Act & Assert
        mappedMemory.Memory.Length.Should().Be(2);
        mappedMemory.Mode.Should().Be(MapMode.ReadWrite);
        mappedMemory.Span.Length.Should().Be(2);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void MappedMemory_PropertiesAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
        var memory = new Memory<float>(sourceData);
        var mappedMemory = CreateMappedMemory(_mockBuffer.Object, memory, MapMode.ReadWrite);

        var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();

        // Act - Multiple threads accessing properties
        for(int i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                var mode = mappedMemory.Mode;
                var memoryLength = mappedMemory.Memory.Length;
                var spanLength = mappedMemory.Span.Length;
                
                Assert.Equal(MapMode.ReadWrite, mode);
                Assert.Equal(5, memoryLength);
                Assert.Equal(5, spanLength);
            }));
        }

        System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

        // Assert - No exceptions should be thrown
        tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }

    #endregion
}
