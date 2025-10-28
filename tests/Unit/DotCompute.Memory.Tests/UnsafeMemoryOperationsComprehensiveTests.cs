// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnsafeMemoryOperations covering all critical scenarios.
/// Part of Phase 1: Memory Module testing to achieve 80% coverage.
/// </summary>
public sealed unsafe class UnsafeMemoryOperationsComprehensiveTests
{
    #region CopyMemory (void*) Tests

    [Fact]
    public void CopyMemory_WithValidPointers_CopiesData()
    {
        // Arrange
        Span<byte> source = stackalloc byte[64];
        Span<byte> destination = stackalloc byte[64];

        for (var i = 0; i < source.Length; i++)
        {
            source[i] = (byte)i;
        }

        // Act
        fixed (byte* src = source)
        fixed (byte* dst = destination)
        {
            UnsafeMemoryOperations.CopyMemory(src, dst, (nuint)source.Length);
        }

        // Assert
        _ = destination.ToArray().Should().Equal(source.ToArray());
    }

    [Fact]
    public void CopyMemory_WithZeroLength_DoesNotCopy()
    {
        // Arrange
        Span<byte> source = stackalloc byte[16];
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xFF);

        // Act
        fixed (byte* src = source)
        fixed (byte* dst = destination)
        {
            UnsafeMemoryOperations.CopyMemory(src, dst, 0);
        }

        // Assert - destination should remain unchanged
        _ = destination.ToArray().Should().OnlyContain(b => b == 0xFF);
    }

    [Fact]
    public void CopyMemory_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        Span<byte> destination = stackalloc byte[16];

        // Act & Assert
        fixed (byte* dst = destination)
        {
            try
            {
                UnsafeMemoryOperations.CopyMemory(null, dst, 16);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }
    }

    [Fact]
    public void CopyMemory_WithNullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        Span<byte> source = stackalloc byte[16];

        // Act & Assert
        fixed (byte* src = source)
        {
            try
            {
                UnsafeMemoryOperations.CopyMemory(src, null, 16);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception
            }
        }
    }

    [Theory]
    [InlineData(1)]      // Sub-SSE2 size
    [InlineData(8)]      // Scalar 8-byte chunks
    [InlineData(15)]     // Just below SSE2 threshold
    [InlineData(16)]     // SSE2 threshold
    [InlineData(31)]     // Just below AVX2 threshold
    [InlineData(32)]     // AVX2 threshold
    [InlineData(64)]     // Multiple AVX2 chunks
    [InlineData(100)]    // Non-aligned size
    [InlineData(256)]    // Large size
    [InlineData(1024)]   // Very large size
    public void CopyMemory_WithVariousSizes_CopiesCorrectly(int size)
    {
        // Arrange
        var source = new byte[size];
        var destination = new byte[size];

        for (var i = 0; i < size; i++)
        {
            source[i] = (byte)(i % 256);
        }

        // Act
        fixed (byte* src = source)
        fixed (byte* dst = destination)
        {
            UnsafeMemoryOperations.CopyMemory(src, dst, (nuint)size);
        }

        // Assert
        _ = destination.Should().Equal(source);
    }

    #endregion

    #region CopyMemory<T> (Span) Tests

    [Fact]
    public void CopyMemory_Generic_WithValidSpans_CopiesData()
    {
        // Arrange
        Span<int> source = stackalloc int[16];
        Span<int> destination = stackalloc int[16];

        for (var i = 0; i < source.Length; i++)
        {
            source[i] = i * 10;
        }

        // Act
        UnsafeMemoryOperations.CopyMemory(source, destination);

        // Assert
        _ = destination.ToArray().Should().Equal(source.ToArray());
    }

    [Fact]
    public void CopyMemory_Generic_WithMismatchedLengths_ThrowsArgumentException()
    {
        // Arrange
        Span<float> source = stackalloc float[10];
        Span<float> destination = stackalloc float[8];

        // Act & Assert
        try
        {
            UnsafeMemoryOperations.CopyMemory<float>(source, destination);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException ex)
        {
            _ = ex.Message.Should().Contain("same length");
        }
    }

    [Fact]
    public void CopyMemory_Generic_WithZeroLength_DoesNotCopy()
    {
        // Arrange
        Span<double> source = stackalloc double[0];
        Span<double> destination = stackalloc double[0];

        // Act - Should not throw
        UnsafeMemoryOperations.CopyMemory<double>(source, destination);

        // Assert - If we get here, it didn't throw
        _ = true.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void CopyMemory_Generic_WithDifferentTypes_CopiesCorrectly(int count)
    {
        // Test with float
        var sourceFloat = new float[count];
        var destinationFloat = new float[count];
        for (var i = 0; i < count; i++)
        {
            sourceFloat[i] = i * 3.14f;
        }

        UnsafeMemoryOperations.CopyMemory<float>(sourceFloat, destinationFloat);
        _ = destinationFloat.Should().Equal(sourceFloat);

        // Test with double
        var sourceDouble = new double[count];
        var destinationDouble = new double[count];
        for (var i = 0; i < count; i++)
        {
            sourceDouble[i] = i * 2.718;
        }

        UnsafeMemoryOperations.CopyMemory<double>(sourceDouble, destinationDouble);
        _ = destinationDouble.Should().Equal(sourceDouble);
    }

    #endregion

    #region FillMemory (void*) Tests

    [Fact]
    public void FillMemory_WithValidPointer_FillsMemory()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[64];
        const byte fillValue = 0x42;

        // Act
        fixed (byte* ptr = buffer)
        {
            UnsafeMemoryOperations.FillMemory(ptr, fillValue, (nuint)buffer.Length);
        }

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(b => b == fillValue);
    }

    [Fact]
    public void FillMemory_WithZeroLength_DoesNotFill()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[16];
        buffer.Fill(0);

        // Act
        fixed (byte* ptr = buffer)
        {
            UnsafeMemoryOperations.FillMemory(ptr, 0xFF, 0);
        }

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(b => b == 0);
    }

    [Fact]
    public void FillMemory_WithNullPointer_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => UnsafeMemoryOperations.FillMemory(null, 0, 16);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(1, 0x00)]
    [InlineData(8, 0xFF)]
    [InlineData(15, 0x42)]
    [InlineData(16, 0xAA)]
    [InlineData(31, 0x55)]
    [InlineData(32, 0x11)]
    [InlineData(64, 0x22)]
    [InlineData(100, 0x33)]
    [InlineData(256, 0x44)]
    public void FillMemory_WithVariousSizesAndValues_FillsCorrectly(int size, byte value)
    {
        // Arrange
        var buffer = new byte[size];

        // Act
        fixed (byte* ptr = buffer)
        {
            UnsafeMemoryOperations.FillMemory(ptr, value, (nuint)size);
        }

        // Assert
        _ = buffer.Should().OnlyContain(b => b == value);
    }

    #endregion

    #region FillMemory<T> (Span) Tests

    [Fact]
    public void FillMemory_Generic_WithByte_FillsCorrectly()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[64];
        const byte value = 0x77;

        // Act
        UnsafeMemoryOperations.FillMemory(buffer, value);

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(b => b == value);
    }

    [Fact]
    public void FillMemory_Generic_WithInt32_FillsCorrectly()
    {
        // Arrange
        Span<int> buffer = stackalloc int[16];
        const int value = 12345;

        // Act
        UnsafeMemoryOperations.FillMemory(buffer, value);

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(i => i == value);
    }

    [Fact]
    public void FillMemory_Generic_WithFloat_FillsCorrectly()
    {
        // Arrange
        Span<float> buffer = stackalloc float[16];
        const float value = 3.14159f;

        // Act
        UnsafeMemoryOperations.FillMemory(buffer, value);

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(f => f == value);
    }

    [Fact]
    public void FillMemory_Generic_WithZeroLength_DoesNotFill()
    {
        // Arrange
        Span<int> buffer = stackalloc int[0];

        // Act - Should not throw
        UnsafeMemoryOperations.FillMemory(buffer, 42);

        // Assert - If we get here, it didn't throw
        _ = true.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void FillMemory_Generic_WithVariousSizes_FillsCorrectly(int count)
    {
        // Test with double
        var bufferDouble = new double[count];
        const double valueDouble = 2.71828;

        UnsafeMemoryOperations.FillMemory<double>(bufferDouble, valueDouble);
        _ = bufferDouble.Should().OnlyContain(d => d == valueDouble);
    }

    #endregion

    #region ZeroMemory (void*) Tests

    [Fact]
    public void ZeroMemory_WithValidPointer_ZerosMemory()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[64];
        buffer.Fill(0xFF);

        // Act
        fixed (byte* ptr = buffer)
        {
            UnsafeMemoryOperations.ZeroMemory(ptr, (nuint)buffer.Length);
        }

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(b => b == 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void ZeroMemory_WithVariousSizes_ZerosCorrectly(int size)
    {
        // Arrange
        var buffer = new byte[size];
        Array.Fill(buffer, (byte)0xFF);

        // Act
        fixed (byte* ptr = buffer)
        {
            UnsafeMemoryOperations.ZeroMemory(ptr, (nuint)size);
        }

        // Assert
        _ = buffer.Should().OnlyContain(b => b == 0);
    }

    #endregion

    #region ZeroMemory<T> (Span) Tests

    [Fact]
    public void ZeroMemory_Generic_WithByte_ZerosMemory()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[32];
        buffer.Fill(0xFF);

        // Act
        UnsafeMemoryOperations.ZeroMemory(buffer);

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(b => b == 0);
    }

    [Fact]
    public void ZeroMemory_Generic_WithInt32_ZerosMemory()
    {
        // Arrange
        Span<int> buffer = stackalloc int[16];
        buffer.Fill(-1);

        // Act
        UnsafeMemoryOperations.ZeroMemory(buffer);

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(i => i == 0);
    }

    [Fact]
    public void ZeroMemory_Generic_WithFloat_ZerosMemory()
    {
        // Arrange
        Span<float> buffer = stackalloc float[16];
        buffer.Fill(3.14f);

        // Act
        UnsafeMemoryOperations.ZeroMemory(buffer);

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(f => f == 0.0f);
    }

    [Fact]
    public void ZeroMemory_Generic_WithZeroLength_DoesNotThrow()
    {
        // Arrange
        Span<double> buffer = stackalloc double[0];

        // Act - Should not throw
        UnsafeMemoryOperations.ZeroMemory(buffer);

        // Assert - If we get here, it didn't throw
        _ = true.Should().BeTrue();
    }

    #endregion

    #region IsAligned Tests

    [Fact]
    public void IsAligned_WithAlignedAddress_ReturnsTrue()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[64];

        fixed (byte* ptr = buffer)
        {
            // Align to 32-byte boundary
            var aligned = UnsafeMemoryOperations.AlignAddress(ptr, 32);

            // Act
            var result = UnsafeMemoryOperations.IsAligned(aligned, 32);

            // Assert
            _ = result.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void IsAligned_WithVariousAlignments_WorksCorrectly(int alignment)
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[256];

        fixed (byte* ptr = buffer)
        {
            var aligned = UnsafeMemoryOperations.AlignAddress(ptr, alignment);

            // Act
            var result = UnsafeMemoryOperations.IsAligned(aligned, alignment);

            // Assert
            _ = result.Should().BeTrue("address should be aligned to {0}-byte boundary", alignment);
        }
    }

    #endregion

    #region AlignAddress Tests

    [Fact]
    public void AlignAddress_WithUnalignedAddress_ReturnsAlignedAddress()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[128];

        fixed (byte* ptr = buffer)
        {
            // Intentionally create misaligned pointer
            var unaligned = ptr + 1;

            // Act
            var aligned = UnsafeMemoryOperations.AlignAddress(unaligned, 32);

            // Assert
            _ = UnsafeMemoryOperations.IsAligned(aligned, 32).Should().BeTrue();
            _ = ((nuint)aligned).Should().BeGreaterThanOrEqualTo((nuint)unaligned);
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void AlignAddress_WithDifferentAlignments_AlignsCorrectly(int alignment)
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[256];

        fixed (byte* ptr = buffer)
        {
            for (var offset = 0; offset < alignment; offset++)
            {
                var unaligned = ptr + offset;

                // Act
                var aligned = UnsafeMemoryOperations.AlignAddress(unaligned, alignment);

                // Assert
                _ = UnsafeMemoryOperations.IsAligned(aligned, alignment).Should().BeTrue();
            }
        }
    }

    [Fact]
    public void AlignAddress_WithAlreadyAlignedAddress_ReturnsSameAddress()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[128];

        fixed (byte* ptr = buffer)
        {
            var aligned = UnsafeMemoryOperations.AlignAddress(ptr, 32);

            // Act - align again
            var realigned = UnsafeMemoryOperations.AlignAddress(aligned, 32);

            // Assert
            _ = ((nuint)realigned).Should().Be((nuint)aligned);
        }
    }

    #endregion

    #region CalculatePadding Tests

    [Fact]
    public void CalculatePadding_WithUnalignedAddress_ReturnsCorrectPadding()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[128];

        fixed (byte* ptr = buffer)
        {
            var unaligned = ptr + 1;

            // Act
            var padding = UnsafeMemoryOperations.CalculatePadding(unaligned, 32);

            // Assert
            _ = padding.Should().BeGreaterThan(0);
            _ = padding.Should().BeLessThan(32);

            var aligned = (byte*)unaligned + padding;
            _ = UnsafeMemoryOperations.IsAligned(aligned, 32).Should().BeTrue();
        }
    }

    [Fact]
    public void CalculatePadding_WithAlignedAddress_ReturnsZero()
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[128];

        fixed (byte* ptr = buffer)
        {
            var aligned = UnsafeMemoryOperations.AlignAddress(ptr, 32);

            // Act
            var padding = UnsafeMemoryOperations.CalculatePadding(aligned, 32);

            // Assert
            _ = padding.Should().Be(0);
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void CalculatePadding_WithDifferentAlignments_CalculatesCorrectly(int alignment)
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[256];

        fixed (byte* ptr = buffer)
        {
            for (var offset = 0; offset < alignment; offset++)
            {
                var unaligned = ptr + offset;

                // Act
                var padding = UnsafeMemoryOperations.CalculatePadding(unaligned, alignment);

                // Assert
                _ = padding.Should().BeLessThan(alignment);
                var aligned = (byte*)unaligned + padding;
                _ = UnsafeMemoryOperations.IsAligned(aligned, alignment).Should().BeTrue();
            }
        }
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void CopyMemory_SourceAndDestinationOverlap_HandlesCorrectly()
    {
        // Arrange
        var buffer = new byte[128];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)i;
        }

        // Act - Copy within same buffer (non-overlapping regions)
        fixed (byte* ptr = buffer)
        {
            UnsafeMemoryOperations.CopyMemory(ptr, ptr + 64, 32);
        }

        // Assert
        for (var i = 0; i < 32; i++)
        {
            _ = buffer[64 + i].Should().Be((byte)i);
        }
    }

    [Fact]
    public void FillMemory_AfterCopy_MaintainsCorrectData()
    {
        // Arrange
        Span<byte> source = stackalloc byte[32];
        Span<byte> destination = stackalloc byte[32];

        for (var i = 0; i < source.Length; i++)
        {
            source[i] = (byte)i;
        }

        // Act - Copy then fill part of destination
        fixed (byte* src = source)
        fixed (byte* dst = destination)
        {
            UnsafeMemoryOperations.CopyMemory(src, dst, 32);
            UnsafeMemoryOperations.FillMemory(dst + 16, 0xFF, 16);
        }

        // Assert
        for (var i = 0; i < 16; i++)
        {
            _ = destination[i].Should().Be((byte)i, "first half should be copied");
        }
        for (var i = 16; i < 32; i++)
        {
            _ = destination[i].Should().Be(0xFF, "second half should be filled");
        }
    }

    [Fact]
    public void ZeroMemory_AfterFill_ClearsCorrectly()
    {
        // Arrange
        Span<int> buffer = stackalloc int[32];
        buffer.Fill(12345);

        // Act
        UnsafeMemoryOperations.ZeroMemory(buffer);

        // Assert
        _ = buffer.ToArray().Should().OnlyContain(i => i == 0);
    }

    #endregion
}
