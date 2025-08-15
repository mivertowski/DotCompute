// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Stress tests and edge cases for accelerator abstractions.
/// Tests boundary conditions, null handling, and resource limits.
/// </summary>
public sealed class AcceleratorStressTests
{
    #region AcceleratorInfo Edge Cases

    [Fact]
    public void AcceleratorInfo_WithExtremeValues_ShouldHandleCorrectly()
    {
        // Act & Assert - Test with extreme but valid values
        var info = new AcceleratorInfo(
            AcceleratorType.GPU,
            "Test GPU with Very Long Name That Exceeds Normal Expectations",
            "999.999.999.999",
            long.MaxValue,
            int.MaxValue,
            int.MaxValue, // maxClockFrequency - use int.MaxValue instead
            new Version(999, 999, 999, 999),
            long.MaxValue, // maxSharedMemoryPerBlock - should be long 
            true
        );

        info.Type.Should().Be("GPU");
        info.Name.Should().Contain("Long Name");
        info.TotalMemory.Should().Be(long.MaxValue);
        info.ComputeUnits.Should().Be(int.MaxValue);
    }

    [Fact]
    public void AcceleratorInfo_WithMinimumValues_ShouldWork()
    {
        // Act & Assert - Test with minimum valid values
        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "A", // Single character name
            "0.0",
            1, // Minimum memory
            1, // Single processor
            1, // Minimum clock speed
            new Version(0, 0),
            1, // Minimum cache
            false
        );

        info.Name.Should().Be("A");
        info.TotalMemory.Should().Be(1);
        info.ComputeUnits.Should().Be(1);
    }

    [Theory]
    [InlineData(AcceleratorType.CPU)]
    [InlineData(AcceleratorType.GPU)]
    [InlineData(AcceleratorType.FPGA)]
    [InlineData(AcceleratorType.TPU)]
    [InlineData(AcceleratorType.Custom)]
    public void AcceleratorInfo_WithAllAcceleratorTypes_ShouldWork(AcceleratorType type)
    {
        // Act
        var info = new AcceleratorInfo(
            type,
            $"Test {type}",
            "1.0",
            1024 * 1024,
            4,
            2000,
            new Version(1, 0),
            64,
            true
        );

        // Assert
        info.Type.Should().Be(type.ToString());
        info.Name.Should().Contain(type.ToString());
    }

    #endregion

    #region Memory Options Edge Cases

    [Theory]
    [InlineData(MemoryOptions.None)]
    [InlineData(MemoryOptions.ReadOnly)]
    [InlineData(MemoryOptions.WriteOnly)]
    [InlineData(MemoryOptions.HostVisible)]
    public void MemoryOptions_WithAllFlags_ShouldWork(MemoryOptions memoryOptions)
    {
        // Act & Assert - Test that enum values are defined and work correctly
        Enum.IsDefined(typeof(MemoryOptions), memoryOptions).Should().BeTrue();

        // Test flag combinations
        var combined = MemoryOptions.ReadOnly | MemoryOptions.HostVisible;
        combined.Should().HaveFlag(MemoryOptions.ReadOnly);
        combined.Should().HaveFlag(MemoryOptions.HostVisible);
    }

    [Theory]
    [InlineData(MemoryOptions.None)]
    [InlineData(MemoryOptions.ReadOnly)]
    [InlineData(MemoryOptions.WriteOnly)]
    [InlineData(MemoryOptions.HostVisible)]
    public void MemoryOptions_FlagValues_ShouldBePowersOfTwo(MemoryOptions option)
    {
        // Act & Assert - Verify flags are proper powers of 2(except None)
        if (option == MemoryOptions.None)
        {
            ((int)option).Should().Be(0);
        }
        else
        {
            var value = (int)option;
            ((value > 0) && ((value & (value - 1)) == 0)).Should().BeTrue();
        }
    }

    [Fact]
    public void MemoryOptions_CombinedFlags_ShouldWorkCorrectly()
    {
        // Act
        var combined = MemoryOptions.ReadOnly | MemoryOptions.HostVisible;

        // Assert
        combined.Should().HaveFlag(MemoryOptions.ReadOnly);
        combined.Should().HaveFlag(MemoryOptions.HostVisible);
        combined.Should().NotHaveFlag(MemoryOptions.WriteOnly);
    }

    #endregion

    #region Compilation Options Edge Cases

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Debug)]
    [InlineData(OptimizationLevel.Release)]
    [InlineData(OptimizationLevel.Aggressive)]
    public void CompilationOptions_WithAllOptimizationLevels_ShouldWork(OptimizationLevel level)
    {
        // Act
        var options = new CompilationOptions
        {
            OptimizationLevel = level,
            EnableDebugInfo = level == OptimizationLevel.Debug,
            FastMath = level == OptimizationLevel.Aggressive,
            UnrollLoops = level != OptimizationLevel.None
        };

        // Assert
        options.OptimizationLevel.Should().Be(level);
        options.EnableDebugInfo.Should().Be(level == OptimizationLevel.Debug);
        options.FastMath.Should().Be(level == OptimizationLevel.Aggressive);
        options.UnrollLoops.Should().Be(level != OptimizationLevel.None);
    }

    [Fact]
    public void CompilationOptions_WithExtremeValues_ShouldWork()
    {
        // Act
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = true,
            FastMath = true,
            UnrollLoops = true
        };

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        options.EnableDebugInfo.Should().BeTrue();
        options.FastMath.Should().BeTrue();
        options.UnrollLoops.Should().BeTrue();
    }

    [Fact]
    public void CompilationOptions_WithMinimumValues_ShouldWork()
    {
        // Act
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.None,
            EnableDebugInfo = false,
            FastMath = false,
            UnrollLoops = false
        };

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.None);
        options.EnableDebugInfo.Should().BeFalse();
        options.FastMath.Should().BeFalse();
        options.UnrollLoops.Should().BeFalse();
    }

    #endregion

    #region Kernel Arguments Edge Cases

    [Fact]
    public void KernelArguments_WithManyArguments_ShouldHandleCorrectly()
    {
        // Arrange
        const int maxArgs = 1000;
        var argArray = new object[maxArgs];

        // Act - Create arguments with many values
        for (var i = 0; i < maxArgs; i++)
        {
            argArray[i] = i;
        }
        var args = new KernelArguments(argArray);

        // Assert
        args.Length.Should().Be(maxArgs);

        for (var i = 0; i < maxArgs; i++)
        {
            args.Get(i).Should().Be(i);
        }
    }

    [Fact]
    public void KernelArguments_WithLargeArgumentValues_ShouldWork()
    {
        // Arrange
        var largeArray = new byte[1024 * 1024]; // 1MB array
        var largeString = new string('X', 10000); // 10K character string

        // Act
        var args = new KernelArguments(largeArray, largeString, int.MaxValue, long.MaxValue);

        // Assert
        args.Get(0).Should().BeSameAs(largeArray);
        args.Get(1).Should().Be(largeString);
        args.Get(2).Should().Be(int.MaxValue);
        args.Get(3).Should().Be(long.MaxValue);
        args.Length.Should().Be(4);
    }

    [Fact]
    public void KernelArguments_WithOverwrittenValues_ShouldOverwrite()
    {
        // Arrange
        var args = KernelArguments.Create(2);

        // Act
        args.Set(0, "original");
        args.Set(0, "updated");
        args.Set(1, "second");

        // Assert
        args.Get(0).Should().Be("updated");
        args.Get(1).Should().Be("second");
        args.Length.Should().Be(2);
    }

    [Fact]
    public void KernelArguments_WithNullValues_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var args = new KernelArguments(null!, "");

        // Assert
        args.Get(0).Should().BeNull();
        args.Get(1).Should().Be("");
        args.Length.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void KernelArguments_WithWhitespaceValues_ShouldWork(string value)
    {
        // Arrange & Act
        var args = new KernelArguments(value);

        // Assert
        args.Get(0).Should().Be(value);
        args.Length.Should().Be(1);
    }

    [Fact]
    public void KernelArguments_WithSpecialCharacterValues_ShouldWork()
    {
        // Arrange
        var specialValues = new[]
        {
            "value.with.dots",
            "value-with-dashes",
            "value_with_underscores",
            "value with spaces",
            "value@with#special$characters%",
            "πvalue", // Unicode
            "🔑value" // Emoji
        };

        // Act
        var args = new KernelArguments(specialValues);

        // Assert
        for (var i = 0; i < specialValues.Length; i++)
        {
            args.Get(i).Should().Be(specialValues[i]);
        }

        args.Length.Should().Be(specialValues.Length);
    }

    [Fact]
    public void KernelArguments_WithExtremelyLongValue_ShouldWork()
    {
        // Arrange
        var longValue = new string('A', 10000); // 10K character value

        // Act
        var args = new KernelArguments(longValue);

        // Assert
        args.Get(0).Should().Be(longValue);
        args.Length.Should().Be(1);
    }

    #endregion

    #region DeviceMemory Edge Cases

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void DeviceMemory_WithVariousSizes_ShouldWork(int size)
    {
        // Act
        var handle = size > 0 ? new IntPtr(0x1000) : IntPtr.Zero; // Valid handle for non-zero sizes
        var memory = new DeviceMemory(handle, size);

        // Assert
        memory.Size.Should().Be(size);
        if (size > 0)
        {
            memory.IsValid.Should().BeTrue();
        }
        else
        {
            memory.IsValid.Should().BeFalse();
        }
    }

    [Fact]
    public void DeviceMemory_WithMaxIntSize_ShouldThrowOrWorkCorrectly()
    {
        // This test depends on available system memory
        // Act & Assert
        try
        {
            var memory = new DeviceMemory(IntPtr.Zero, int.MaxValue);
            memory.Size.Should().Be(int.MaxValue);
        }
        catch (OutOfMemoryException)
        {
            // Expected on systems without sufficient memory
        }
        catch (OverflowException)
        {
            // Expected when calculation overflows
        }
    }

    [Fact]
    public void DeviceMemory_WithNegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => new DeviceMemory(IntPtr.Zero, -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Fact]
    public void DeviceMemory_Creation_WithVariousBoundaries_ShouldWork()
    {
        // Arrange & Act - Test various memory sizes
        var memories = new[]
        {
            new DeviceMemory(IntPtr.Zero, 0),     // Empty memory
            new DeviceMemory(new IntPtr(1000), 1),     // Single byte
            new DeviceMemory(new IntPtr(1000), 999),   // Almost 1KB
            new DeviceMemory(new IntPtr(1000), 998),   // Middle size
            new DeviceMemory(new IntPtr(1000), 1),     // Single byte again
            new DeviceMemory(new IntPtr(1000), 500)    // Half KB
        };

        // Assert
        memories[0].Size.Should().Be(0);
        memories[1].Size.Should().Be(1);
        memories[2].Size.Should().Be(999);
        memories[3].Size.Should().Be(998);
        memories[4].Size.Should().Be(1);
        memories[5].Size.Should().Be(500);
    }

    [Fact]
    public void DeviceMemory_WithInvalidParameters_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert - Test invalid memory creation
        var act1 = () => new DeviceMemory(new IntPtr(-1), 10);  // Invalid handle is allowed, negative size is not
        var act2 = () => new DeviceMemory(IntPtr.Zero, -1);     // Negative size

        // Only negative size should throw
        Assert.Throws<ArgumentOutOfRangeException>(() => act2());

        // Invalid handles are allowed(they just result in invalid memory)
        var invalidMemory = new DeviceMemory(IntPtr.Zero, 0);
        invalidMemory.IsValid.Should().BeFalse();
    }

    #endregion

    #region Exception Edge Cases

    [Fact]
    public void AcceleratorException_WithLongMessage_ShouldWork()
    {
        // Arrange
        var longMessage = new string('X', 10000);

        // Act
        var exception = new AcceleratorException(longMessage);

        // Assert
        exception.Message.Should().Be(longMessage);
        exception.Message.Length.Should().Be(10000);
    }

    [Fact]
    public void AcceleratorException_WithInnerException_ShouldPreserveStackTrace()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new AcceleratorException("Outer error", innerException);

        // Assert
        exception.InnerException.Should().BeSameAs(innerException);
        exception.Message.Should().Be("Outer error");
    }

    [Fact]
    public void MemoryException_WithSpecialCharacters_ShouldWork()
    {
        // Arrange
        var message = "Memory error with special chars: πΩ∑∆ and emojis: 🚫💾🔥";

        // Act
        var exception = new MemoryException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    #endregion

    #region Concurrency Stress Tests

    [Fact]
    public async Task MultipleKernelArguments_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 50;
        const int operationsPerThread = 100;
        var exceptions = new ConcurrentBag<Exception>();
        var argumentsList = new ConcurrentBag<KernelArguments>();

        // Act - Multiple threads creating arguments concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                try
                {
                    for (var i = 0; i < operationsPerThread; i++)
                    {
                        var value = $"thread{threadId}_value{i}";
                        var args = new KernelArguments(value, threadId, i);

                        argumentsList.Add(args);

                        // Verify the values were set correctly
                        if (!args.Get(0).Equals(value) ||
                            !args.Get(1).Equals(threadId) ||
                            !args.Get(2).Equals(i))
                        {
                            exceptions.Add(new InvalidOperationException(
                                $"Value mismatch in arguments for thread {threadId}, operation {i}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Concurrent access should be thread-safe");
        argumentsList.Count.Should().Be(threadCount * operationsPerThread);
    }

    [Fact]
    public async Task DeviceMemory_ConcurrentCreation_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 20;
        const int memoriesPerThread = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var memories = new ConcurrentBag<DeviceMemory>();

        // Act - Multiple threads creating memories concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                try
                {
                    for (var i = 0; i < memoriesPerThread; i++)
                    {
                        var handle = new IntPtr(threadId * 1000 + i);
                        var size = threadId * 10 + i * 2;

                        var memory = new DeviceMemory(handle, size);
                        memories.Add(memory);

                        // Verify the memory was created correctly
                        if (memory.Handle != handle || memory.Size != size)
                        {
                            exceptions.Add(new InvalidOperationException(
                                $"Memory creation failed for thread {threadId}, iteration {i}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Concurrent memory creation should be thread-safe");
        memories.Should().NotBeEmpty("Should have created some memories");
        memories.Count.Should().Be(threadCount * memoriesPerThread);
    }

    #endregion
}
