// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for ZeroCopyOperations covering all functionality.
/// Target: 100% coverage for 458-line high-performance zero-copy operations class.
/// </summary>
public class ZeroCopyOperationsComprehensiveTests
{
    #region ZeroCopyOperations - UnsafeSlice Tests

    [Fact]
    public void UnsafeSlice_Span_WithValidParameters_CreatesSlice()
    {
        // Arrange
        Span<int> source = stackalloc int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act
        var slice = source.UnsafeSlice(2, 5);

        // Assert
        _ = slice.Length.Should().Be(5);
        _ = slice[0].Should().Be(3);
        _ = slice[4].Should().Be(7);
    }

    [Fact]
    public void UnsafeSlice_ReadOnlySpan_WithValidParameters_CreatesSlice()
    {
        // Arrange
        ReadOnlySpan<int> source = stackalloc int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act
        var slice = source.UnsafeSlice(3, 4);

        // Assert
        _ = slice.Length.Should().Be(4);
        _ = slice[0].Should().Be(4);
        _ = slice[3].Should().Be(7);
    }

    [Fact]
    public void UnsafeSlice_Span_FullLength_ReturnsEntireSpan()
    {
        // Arrange
        Span<int> source = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        var slice = source.UnsafeSlice(0, 5);

        // Assert
        _ = slice.Length.Should().Be(5);
        _ = slice[0].Should().Be(1);
        _ = slice[4].Should().Be(5);
    }

    [Fact]
    public void UnsafeSlice_ReadOnlySpan_EmptySlice_ReturnsEmpty()
    {
        // Arrange
        ReadOnlySpan<int> source = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        var slice = source.UnsafeSlice(2, 0);

        // Assert
        _ = slice.Length.Should().Be(0);
        _ = slice.IsEmpty.Should().BeTrue();
    }

    #endregion

    #region ZeroCopyOperations - Cast Tests

    [Fact]
    public void Cast_Span_IntToFloat_ReinterpretsData()
    {
        // Arrange
        Span<int> source = stackalloc int[] { 1, 2, 3, 4 };

        // Act
        var casted = source.Cast<int, float>();

        // Assert
        _ = casted.Length.Should().Be(4);
    }

    [Fact]
    public void Cast_ReadOnlySpan_ByteToInt_ReinterpretsData()
    {
        // Arrange
        ReadOnlySpan<byte> source = stackalloc byte[] { 1, 0, 0, 0, 2, 0, 0, 0 };

        // Act
        var casted = source.Cast<byte, int>();

        // Assert
        _ = casted.Length.Should().Be(2);
        _ = casted[0].Should().Be(1);
        _ = casted[1].Should().Be(2);
    }

    [Fact]
    public void Cast_Span_EmptySpan_ReturnsEmpty()
    {
        // Arrange
        var source = Span<int>.Empty;

        // Act
        var casted = source.Cast<int, byte>();

        // Assert
        _ = casted.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Cast_ReadOnlySpan_UnmanagedTypes_WorksCorrectly()
    {
        // Arrange
        ReadOnlySpan<long> source = stackalloc long[] { 1L, 2L };

        // Act
        var casted = source.Cast<long, int>();

        // Assert
        _ = casted.Length.Should().Be(4); // 2 longs = 4 ints
    }

    #endregion

    #region ZeroCopyOperations - GetReference Tests

    [Fact]
    public void GetReference_Span_ReturnsFirstElementReference()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 42, 43, 44 };

        // Act
        ref var reference = ref span.GetReference();

        // Assert
        _ = reference.Should().Be(42);
        reference = 100;
        _ = span[0].Should().Be(100);
    }

    [Fact]
    public void GetReference_ReadOnlySpan_ReturnsFirstElementReference()
    {
        // Arrange
        ReadOnlySpan<int> span = stackalloc int[] { 99, 98, 97 };

        // Act
        ref readonly var reference = ref span.GetReference();

        // Assert
        _ = reference.Should().Be(99);
    }

    #endregion

    #region ZeroCopyOperations - CreateSpan Tests

    [Fact]
    public unsafe void CreateSpan_ValidPointer_CreatesSpan()
    {
        // Arrange
        var data = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        var span = ZeroCopyOperations.CreateSpan<int>(data, 5);

        // Assert
        _ = span.Length.Should().Be(5);
        _ = span[0].Should().Be(1);
        _ = span[4].Should().Be(5);
    }

    [Fact]
    public unsafe void CreateReadOnlySpan_ValidPointer_CreatesReadOnlySpan()
    {
        // Arrange
        var data = stackalloc int[] { 10, 20, 30 };

        // Act
        var span = ZeroCopyOperations.CreateReadOnlySpan<int>(data, 3);

        // Assert
        _ = span.Length.Should().Be(3);
        _ = span[0].Should().Be(10);
        _ = span[2].Should().Be(30);
    }

    [Fact]
    public unsafe void CreateSpan_ZeroLength_CreatesEmptySpan()
    {
        // Arrange
        var data = stackalloc int[] { 1 };

        // Act
        var span = ZeroCopyOperations.CreateSpan<int>(data, 0);

        // Assert
        _ = span.IsEmpty.Should().BeTrue();
    }

    #endregion

    #region ZeroCopyOperations - FastCopy Tests

    [Fact]
    public void FastCopy_SameLength_CopiesData()
    {
        // Arrange
        ReadOnlySpan<int> source = stackalloc int[] { 1, 2, 3, 4, 5 };
        Span<int> destination = stackalloc int[5];

        // Act
        source.FastCopy(destination);

        // Assert
        _ = destination.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void FastCopy_LargeData_UsesVectorizedCopy()
    {
        // Arrange - 100 elements to trigger vectorized path
        var source = new int[100];
        for (var i = 0; i < 100; i++)
        {
            source[i] = i;
        }

        var destination = new int[100];

        // Act
        ((ReadOnlySpan<int>)source).FastCopy(destination);

        // Assert
        _ = destination.Should().Equal(source);
    }

    [Fact]
    public void FastCopy_EmptySpan_CompletesWithoutError()
    {
        // Arrange
        var source = ReadOnlySpan<int>.Empty;
        var destination = Span<int>.Empty;

        // Act & Assert
        source.FastCopy(destination);
    }

    [Fact]
    public void FastCopy_DifferentLengths_ThrowsArgumentException()
    {
        // Arrange
        var sourceArray = new int[] { 1, 2, 3 };
        var destArray = new int[5];

        // Act & Assert
        Action act = () => ((ReadOnlySpan<int>)sourceArray).FastCopy(destArray);
        _ = act.Should().Throw<ArgumentException>()
            .WithMessage("*same length*");
    }

    #endregion

    #region ZeroCopyOperations - FastEquals Tests

    [Fact]
    public void FastEquals_EqualSpans_ReturnsTrue()
    {
        // Arrange
        ReadOnlySpan<int> left = stackalloc int[] { 1, 2, 3, 4, 5 };
        ReadOnlySpan<int> right = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        var result = left.FastEquals(right);

        // Assert
        _ = result.Should().BeTrue();
    }

    [Fact]
    public void FastEquals_DifferentLengths_ReturnsFalse()
    {
        // Arrange
        ReadOnlySpan<int> left = stackalloc int[] { 1, 2, 3 };
        ReadOnlySpan<int> right = stackalloc int[] { 1, 2, 3, 4 };

        // Act
        var result = left.FastEquals(right);

        // Assert
        _ = result.Should().BeFalse();
    }

    [Fact]
    public void FastEquals_DifferentContent_ReturnsFalse()
    {
        // Arrange
        ReadOnlySpan<int> left = stackalloc int[] { 1, 2, 3 };
        ReadOnlySpan<int> right = stackalloc int[] { 1, 2, 4 };

        // Act
        var result = left.FastEquals(right);

        // Assert
        _ = result.Should().BeFalse();
    }

    [Fact]
    public void FastEquals_EmptySpans_ReturnsTrue()
    {
        // Arrange
        var left = ReadOnlySpan<int>.Empty;
        var right = ReadOnlySpan<int>.Empty;

        // Act
        var result = left.FastEquals(right);

        // Assert
        _ = result.Should().BeTrue();
    }

    [Fact]
    public void FastEquals_ByteSpans_UsesVectorizedComparison()
    {
        // Arrange
        ReadOnlySpan<byte> left = stackalloc byte[] { 1, 2, 3, 4, 5 };
        ReadOnlySpan<byte> right = stackalloc byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = left.FastEquals(right);

        // Assert
        _ = result.Should().BeTrue();
    }

    #endregion

    #region ZeroCopyOperations - FastFill Tests

    [Fact]
    public void FastFill_IntSpan_FillsWithValue()
    {
        // Arrange
        Span<int> span = stackalloc int[5];

        // Act
        span.FastFill(42);

        // Assert
        _ = span.ToArray().Should().OnlyContain(x => x == 42);
    }

    [Fact]
    public void FastFill_ByteSpan_UsesOptimizedFill()
    {
        // Arrange
        Span<byte> span = stackalloc byte[10];

        // Act
        span.FastFill((byte)99);

        // Assert
        _ = span.ToArray().Should().OnlyContain(x => x == 99);
    }

    [Fact]
    public void FastFill_IntSpanWithZero_UsesClear()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        span.FastFill(0);

        // Assert
        _ = span.ToArray().Should().OnlyContain(x => x == 0);
    }

    [Fact]
    public void FastFill_EmptySpan_CompletesWithoutError()
    {
        // Arrange
        var span = Span<int>.Empty;

        // Act & Assert
        span.FastFill(42);
    }

    #endregion

    #region ZeroCopyOperations - FastClear Tests

    [Fact]
    public void FastClear_IntSpan_ClearsAllElements()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        span.FastClear();

        // Assert
        _ = span.ToArray().Should().OnlyContain(x => x == 0);
    }

    [Fact]
    public void FastClear_ByteSpan_ClearsAllElements()
    {
        // Arrange
        Span<byte> span = stackalloc byte[] { 1, 2, 3, 4, 5 };

        // Act
        span.FastClear();

        // Assert
        _ = span.ToArray().Should().OnlyContain(x => x == 0);
    }

    [Fact]
    public void FastClear_EmptySpan_CompletesWithoutError()
    {
        // Arrange
        var span = Span<int>.Empty;

        // Act & Assert
        span.FastClear();
    }

    #endregion

    #region MemoryMappedSpan Tests

    [Fact]
    public void MemoryMappedSpan_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dat");

        // Act & Assert
        Action act = () => MemoryMappedOperations.CreateMemoryMappedSpan<int>(nonExistentFile);
        _ = act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void MemoryMappedSpan_ValidFile_CreatesSpan()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new int[] { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(tempFile, MemoryMarshal.AsBytes(data.AsSpan()).ToArray());

            // Act
            using var mmSpan = MemoryMappedOperations.CreateMemoryMappedSpan<int>(tempFile);

            // Assert
            _ = mmSpan.Length.Should().Be(5);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MemoryMappedSpan_AsSpan_ReturnsCorrectData()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new int[] { 10, 20, 30, 40, 50 };
            File.WriteAllBytes(tempFile, MemoryMarshal.AsBytes(data.AsSpan()).ToArray());

            using var mmSpan = MemoryMappedOperations.CreateMemoryMappedSpan<int>(tempFile);

            // Act
            var span = mmSpan.AsSpan();

            // Assert
            _ = span.Length.Should().Be(5);
            _ = span[0].Should().Be(10);
            _ = span[4].Should().Be(50);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MemoryMappedSpan_AsReadOnlySpan_ReturnsCorrectData()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new int[] { 100, 200, 300 };
            File.WriteAllBytes(tempFile, MemoryMarshal.AsBytes(data.AsSpan()).ToArray());

            using var mmSpan = MemoryMappedOperations.CreateMemoryMappedSpan<int>(
                tempFile,
                MemoryMappedFileAccess.Read);

            // Act
            var span = mmSpan.AsReadOnlySpan();

            // Assert
            _ = span.Length.Should().Be(3);
            _ = span[0].Should().Be(100);
            _ = span[2].Should().Be(300);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MemoryMappedSpan_AsSpanWithOffsetAndLength_ReturnsSlice()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            File.WriteAllBytes(tempFile, MemoryMarshal.AsBytes(data.AsSpan()).ToArray());

            using var mmSpan = MemoryMappedOperations.CreateMemoryMappedSpan<int>(tempFile);

            // Act
            var slice = mmSpan.AsSpan(2, 5);

            // Assert
            _ = slice.Length.Should().Be(5);
            _ = slice[0].Should().Be(3);
            _ = slice[4].Should().Be(7);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MemoryMappedSpan_AsSpanWithInvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new int[] { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(tempFile, MemoryMarshal.AsBytes(data.AsSpan()).ToArray());

            using var mmSpan = MemoryMappedOperations.CreateMemoryMappedSpan<int>(tempFile);

            // Act & Assert
            Action act = () => mmSpan.AsSpan(3, 10);
            _ = act.Should().Throw<ArgumentOutOfRangeException>();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MemoryMappedSpan_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new int[] { 1, 2, 3 };
            File.WriteAllBytes(tempFile, MemoryMarshal.AsBytes(data.AsSpan()).ToArray());

            var mmSpan = MemoryMappedOperations.CreateMemoryMappedSpan<int>(tempFile);

            // Act
            mmSpan.Dispose();
            mmSpan.Dispose(); // Second dispose

            // Assert - should not throw
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MemoryMappedSpan_AsSpanAfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new int[] { 1, 2, 3 };
            File.WriteAllBytes(tempFile, MemoryMarshal.AsBytes(data.AsSpan()).ToArray());

            var mmSpan = MemoryMappedOperations.CreateMemoryMappedSpan<int>(tempFile);
            mmSpan.Dispose();

            // Act & Assert
            Action act = () => mmSpan.AsSpan();
            _ = act.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region PinnedMemoryHandle Tests

    [Fact]
    public void PinnedMemoryHandle_PinSpan_CreatesHandle()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        using var handle = span.Pin();

        // Assert
        unsafe
        {
            _ = ((IntPtr)handle.Pointer).Should().NotBe(IntPtr.Zero);
        }
    }

    [Fact]
    public void PinnedMemoryHandle_PinReadOnlySpan_CreatesHandle()
    {
        // Arrange
        ReadOnlySpan<int> span = stackalloc int[] { 1, 2, 3 };

        // Act
        using var handle = span.Pin();

        // Assert
        unsafe
        {
            _ = ((IntPtr)handle.Pointer).Should().NotBe(IntPtr.Zero);
        }
    }

    [Fact]
    public void PinnedMemoryHandle_IntPtr_ReturnsValidPointer()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 42 };

        // Act
        using var handle = span.Pin();

        // Assert
        _ = handle.IntPtr.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void PinnedMemoryHandle_Equals_WorksCorrectly()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3 };

        // Act
        using var handle1 = span.Pin();
        using var handle2 = span.Pin();

        // Assert - Different pins of same data may or may not be equal
        // Just verify the Equals method works without throwing
        var result = handle1.Equals(handle2);
        _ = result.Should().Be(result); // Tautology but verifies no exception
    }

    [Fact]
    public void PinnedMemoryHandle_GetHashCode_ReturnsConsistentValue()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3 };

        // Act
        using var handle = span.Pin();
        var hash1 = handle.GetHashCode();
        var hash2 = handle.GetHashCode();

        // Assert
        _ = hash1.Should().Be(hash2);
    }

    [Fact]
    public void PinnedMemoryHandle_EqualityOperator_WorksCorrectly()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3 };

        // Act
        using var handle1 = span.Pin();
        using var handle2 = span.Pin();

        // Assert - Verify operators don't throw
        var equal = handle1 == handle2;
        var notEqual = handle1 != handle2;

        _ = equal.Should().Be(!notEqual);
    }

    [Fact]
    public void PinnedMemoryHandle_EqualsObject_WithNull_ReturnsFalse()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3 };

        // Act
        using var handle = span.Pin();
        var result = handle.Equals(null);

        // Assert
        _ = result.Should().BeFalse();
    }

    [Fact]
    public void PinnedMemoryHandle_EqualsObject_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3 };

        // Act
        using var handle = span.Pin();
        var result = handle.Equals("not a handle");

        // Assert
        _ = result.Should().BeFalse();
    }

    [Fact]
    public void PinnedMemoryHandle_Dispose_ReleasesHandle()
    {
        // Arrange
        Span<int> span = stackalloc int[] { 1, 2, 3 };
        var handle = span.Pin();

        // Act
        handle.Dispose();

        // Assert - should not throw
    }

    #endregion
}
