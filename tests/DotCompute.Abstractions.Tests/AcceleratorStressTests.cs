// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests;

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
            uint.MaxValue,
            new Version(999, 999, 999, 999),
            int.MaxValue,
            true
        );

        info.Type.Should().Be(AcceleratorType.GPU);
        info.Name.Should().Contain("Long Name");
        info.TotalMemoryBytes.Should().Be(long.MaxValue);
        info.ProcessorCount.Should().Be(int.MaxValue);
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
        info.TotalMemoryBytes.Should().Be(1);
        info.ProcessorCount.Should().Be(1);
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
        info.Type.Should().Be(type);
        info.Name.Should().Contain(type.ToString());
    }

    #endregion

    #region Memory Options Edge Cases

    [Theory]
    [InlineData(MemoryType.Default)]
    [InlineData(MemoryType.Pinned)]
    [InlineData(MemoryType.Unified)]
    [InlineData(MemoryType.ReadOnly)]
    [InlineData(MemoryType.WriteOnly)]
    public void MemoryOptions_WithAllMemoryTypes_ShouldWork(MemoryType memoryType)
    {
        // Act
        var options = new MemoryOptions
        {
            Type = memoryType,
            Alignment = 64,
            PreferredLocation = MemoryLocation.Device,
            AllowHostAccess = true
        };

        // Assert
        options.Type.Should().Be(memoryType);
        options.Alignment.Should().Be(64);
        options.PreferredLocation.Should().Be(MemoryLocation.Device);
        options.AllowHostAccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void MemoryOptions_WithVariousAlignments_ShouldWork(int alignment)
    {
        // Act
        var options = new MemoryOptions { Alignment = alignment };

        // Assert
        options.Alignment.Should().Be(alignment);
    }

    [Fact]
    public void MemoryOptions_WithExtremeAlignment_ShouldWork()
    {
        // Act
        var options = new MemoryOptions { Alignment = int.MaxValue };

        // Assert
        options.Alignment.Should().Be(int.MaxValue);
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
            EnableDebugging = level == OptimizationLevel.Debug,
            EnableProfiling = true,
            EnableBoundsChecking = level != OptimizationLevel.Aggressive,
            MaxRegisters = 128,
            PreferredWorkGroupSize = 256
        };

        // Assert
        options.OptimizationLevel.Should().Be(level);
        options.MaxRegisters.Should().Be(128);
        options.PreferredWorkGroupSize.Should().Be(256);
    }

    [Fact]
    public void CompilationOptions_WithExtremeValues_ShouldWork()
    {
        // Act
        var options = new CompilationOptions
        {
            MaxRegisters = int.MaxValue,
            PreferredWorkGroupSize = int.MaxValue,
            EnableDebugging = true,
            EnableProfiling = true,
            EnableBoundsChecking = true
        };

        // Assert
        options.MaxRegisters.Should().Be(int.MaxValue);
        options.PreferredWorkGroupSize.Should().Be(int.MaxValue);
        options.EnableDebugging.Should().BeTrue();
    }

    [Fact]
    public void CompilationOptions_WithMinimumValues_ShouldWork()
    {
        // Act
        var options = new CompilationOptions
        {
            MaxRegisters = 0,
            PreferredWorkGroupSize = 0,
            EnableDebugging = false,
            EnableProfiling = false,
            EnableBoundsChecking = false
        };

        // Assert
        options.MaxRegisters.Should().Be(0);
        options.PreferredWorkGroupSize.Should().Be(0);
        options.EnableDebugging.Should().BeFalse();
    }

    #endregion

    #region Kernel Arguments Edge Cases

    [Fact]
    public void KernelArguments_WithManyArguments_ShouldHandleCorrectly()
    {
        // Arrange
        var args = new KernelArguments();
        const int maxArgs = 1000;

        // Act - Add many arguments
        for (int i = 0; i < maxArgs; i++)
        {
            args.Add($"arg{i}", i);
        }

        // Assert
        args.Count.Should().Be(maxArgs);
        
        for (int i = 0; i < maxArgs; i++)
        {
            args[$"arg{i}"].Should().Be(i);
        }
    }

    [Fact]
    public void KernelArguments_WithLargeArgumentValues_ShouldWork()
    {
        // Arrange
        var args = new KernelArguments();
        var largeArray = new byte[1024 * 1024]; // 1MB array
        var largeString = new string('X', 10000); // 10K character string

        // Act
        args.Add("largeArray", largeArray);
        args.Add("largeString", largeString);
        args.Add("maxInt", int.MaxValue);
        args.Add("maxLong", long.MaxValue);

        // Assert
        args["largeArray"].Should().BeSameAs(largeArray);
        args["largeString"].Should().Be(largeString);
        args["maxInt"].Should().Be(int.MaxValue);
        args["maxLong"].Should().Be(long.MaxValue);
    }

    [Fact]
    public void KernelArguments_WithDuplicateKeys_ShouldOverwrite()
    {
        // Arrange
        var args = new KernelArguments();

        // Act
        args.Add("key", "original");
        args.Add("key", "updated");

        // Assert
        args["key"].Should().Be("updated");
        args.Count.Should().Be(1);
    }

    [Fact]
    public void KernelArguments_WithNullValues_ShouldHandleCorrectly()
    {
        // Arrange
        var args = new KernelArguments();

        // Act
        args.Add("nullValue", null);
        args.Add("emptyString", "");

        // Assert
        args["nullValue"].Should().BeNull();
        args["emptyString"].Should().Be("");
        args.Count.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void KernelArguments_WithWhitespaceKeys_ShouldWork(string key)
    {
        // Arrange
        var args = new KernelArguments();

        // Act
        args.Add(key, "value");

        // Assert
        args[key].Should().Be("value");
    }

    [Fact]
    public void KernelArguments_WithSpecialCharacterKeys_ShouldWork()
    {
        // Arrange
        var args = new KernelArguments();
        var specialKeys = new[]
        {
            "key.with.dots",
            "key-with-dashes",
            "key_with_underscores",
            "key with spaces",
            "key@with#special$characters%",
            "πkey", // Unicode
            "🔑key" // Emoji
        };

        // Act & Assert
        foreach (var key in specialKeys)
        {
            args.Add(key, $"value_for_{key}");
            args[key].Should().Be($"value_for_{key}");
        }

        args.Count.Should().Be(specialKeys.Length);
    }

    [Fact]
    public void KernelArguments_WithExtremelyLongKey_ShouldWork()
    {
        // Arrange
        var args = new KernelArguments();
        var longKey = new string('A', 10000); // 10K character key

        // Act
        args.Add(longKey, "value");

        // Assert
        args[longKey].Should().Be("value");
        args.Count.Should().Be(1);
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
        var memory = new DeviceMemory<byte>(size);

        // Assert
        memory.Length.Should().Be(size);
        memory.IsEmpty.Should().Be(size == 0);
    }

    [Fact]
    public void DeviceMemory_WithMaxIntSize_ShouldThrowOrWorkCorrectly()
    {
        // This test depends on available system memory
        // Act & Assert
        try
        {
            var memory = new DeviceMemory<byte>(int.MaxValue);
            memory.Length.Should().Be(int.MaxValue);
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
        var act = () => new DeviceMemory<byte>(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DeviceMemory_Slice_WithVariousBoundaries_ShouldWork()
    {
        // Arrange
        var memory = new DeviceMemory<byte>(1000);

        // Act & Assert - Test various slice boundaries
        var slices = new[]
        {
            memory.Slice(0, 0),     // Empty slice at start
            memory.Slice(0, 1),     // Single element at start
            memory.Slice(0, 999),   // Almost full slice
            memory.Slice(1, 998),   // Middle slice
            memory.Slice(999, 1),   // Single element at end
            memory.Slice(1000, 0),  // Empty slice at end
            memory.Slice(500)       // Slice from middle to end
        };

        slices[0].Length.Should().Be(0);
        slices[1].Length.Should().Be(1);
        slices[2].Length.Should().Be(999);
        slices[3].Length.Should().Be(998);
        slices[4].Length.Should().Be(1);
        slices[5].Length.Should().Be(0);
        slices[6].Length.Should().Be(500);
    }

    [Fact]
    public void DeviceMemory_Slice_WithInvalidParameters_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var memory = new DeviceMemory<byte>(100);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.Slice(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.Slice(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.Slice(50, 100)); // Start + length > total
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.Slice(101, 0));  // Start beyond end
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
        var args = new KernelArguments();
        const int threadCount = 50;
        const int operationsPerThread = 100;
        var exceptions = new ConcurrentBag<Exception>();

        // Act - Multiple threads modifying arguments concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var key = $"thread{threadId}_key{i}";
                        var value = $"thread{threadId}_value{i}";
                        
                        args.Add(key, value);
                        
                        // Verify the value was set correctly
                        if (args.TryGetValue(key, out var retrievedValue))
                        {
                            if (!retrievedValue.Equals(value))
                            {
                                exceptions.Add(new InvalidOperationException(
                                    $"Value mismatch for key {key}: expected {value}, got {retrievedValue}"));
                            }
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
        args.Count.Should().Be(threadCount * operationsPerThread);
    }

    [Fact]
    public async Task DeviceMemory_ConcurrentSlicing_ShouldBeThreadSafe()
    {
        // Arrange
        var memory = new DeviceMemory<int>(10000);
        const int threadCount = 20;
        const int slicesPerThread = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var slices = new ConcurrentBag<DeviceMemory<int>>();

        // Act - Multiple threads creating slices concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < slicesPerThread; i++)
                    {
                        var start = threadId * 10 + i * 2;
                        var length = Math.Min(10, memory.Length - start);
                        
                        if (start + length <= memory.Length)
                        {
                            var slice = memory.Slice(start, length);
                            slices.Add(slice);
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
        exceptions.Should().BeEmpty("Concurrent slicing should be thread-safe");
        slices.Should().NotBeEmpty("Should have created some slices");
    }

    #endregion
}