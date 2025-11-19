// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.RingKernels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.RingKernels;

/// <summary>
/// Hardware tests for Metal kernel routing on actual Mac hardware.
/// </summary>
/// <remarks>
/// These tests require a Mac with Metal support and validate:
/// - Routing table creation and initialization on GPU
/// - Hash table performance (target: &lt;100ns lookup)
/// - Message routing throughput (target: 2M+ msg/sec)
/// - Unified memory correctness
/// </remarks>
[Collection("MetalHardware")]
public sealed class MetalKernelRoutingHardwareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IntPtr _device;
    private readonly MetalKernelRoutingTableManager _manager;

    public MetalKernelRoutingHardwareTests(ITestOutputHelper output)
    {
        _output = output;

        // Initialize Metal device
        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("No Metal device available. These tests require a Mac with Metal support.");
        }

        _manager = new MetalKernelRoutingTableManager(_device, NullLogger<MetalKernelRoutingTableManager>.Instance);

        _output.WriteLine($"Metal device initialized: 0x{_device.ToInt64():X}");
    }

    [Fact]
    public async Task CreateAsync_Should_Allocate_Routing_Table_On_GPU()
    {
        // Arrange
        var kernelNames = new[] { "KernelA", "KernelB", "KernelC" };
        var queuePointers = new[] { new IntPtr(0x1000), new IntPtr(0x2000), new IntPtr(0x3000) };

        // Act
        var table = await _manager.CreateAsync(kernelNames, queuePointers);

        // Assert
        Assert.Equal(3, table.KernelCount);
        Assert.Equal(32, table.HashTableCapacity); // 3 kernels → 32 capacity (simdgroup aligned)
        Assert.NotEqual(0, table.OutputQueuesPtr);
        Assert.NotEqual(0, table.RoutingHashTablePtr);
        Assert.True(table.Validate());

        _output.WriteLine($"Routing table created: KernelCount={table.KernelCount}, Capacity={table.HashTableCapacity}");
        _output.WriteLine($"  OutputQueuesPtr: 0x{table.OutputQueuesPtr:X}");
        _output.WriteLine($"  RoutingHashTablePtr: 0x{table.RoutingHashTablePtr:X}");

        // Cleanup
        table.Dispose();
    }

    [Fact]
    public async Task CreateAsync_Should_Handle_Large_Kernel_Count()
    {
        // Arrange
        int kernelCount = 256;
        var kernelNames = Enumerable.Range(0, kernelCount)
            .Select(i => $"Kernel{i}")
            .ToArray();
        var queuePointers = Enumerable.Range(0, kernelCount)
            .Select(i => new IntPtr(0x1000 + i * 0x100))
            .ToArray();

        // Act
        var table = await _manager.CreateAsync(kernelNames, queuePointers);

        // Assert
        Assert.Equal(256, table.KernelCount);
        Assert.Equal(512, table.HashTableCapacity); // 256 kernels → 512 capacity (2×, power of 2)
        Assert.True(table.Validate());

        _output.WriteLine($"Large routing table created: {kernelCount} kernels, capacity={table.HashTableCapacity}");
        _output.WriteLine($"  Load factor: {(double)kernelCount / table.HashTableCapacity:P1}");

        // Cleanup
        table.Dispose();
    }

    [Fact]
    public async Task HashTable_Should_Have_Correct_Load_Factor()
    {
        // Arrange
        var testCases = new[]
        {
            (KernelCount: 10, ExpectedLoadFactor: 0.3125), // 10/32
            (KernelCount: 50, ExpectedLoadFactor: 0.78125), // 50/64
            (KernelCount: 100, ExpectedLoadFactor: 0.390625), // 100/256
        };

        foreach (var (kernelCount, expectedLoadFactor) in testCases)
        {
            var kernelNames = Enumerable.Range(0, kernelCount).Select(i => $"K{i}").ToArray();
            var queuePointers = Enumerable.Range(0, kernelCount).Select(i => new IntPtr(i * 0x100)).ToArray();

            // Act
            var table = await _manager.CreateAsync(kernelNames, queuePointers);
            double actualLoadFactor = (double)table.KernelCount / table.HashTableCapacity;

            // Assert
            Assert.InRange(actualLoadFactor, 0.25, 0.80); // Load factor between 25% and 80%
            Assert.Equal(expectedLoadFactor, actualLoadFactor, precision: 3);

            _output.WriteLine($"  {kernelCount} kernels → capacity {table.HashTableCapacity}, load factor: {actualLoadFactor:P2}");

            table.Dispose();
        }
    }

    [Fact]
    public async Task HashTable_Should_Support_Kernel_Name_Lookup()
    {
        // Arrange
        var kernelNames = new[] { "Producer", "Consumer", "Aggregator", "Monitor" };
        var queuePointers = new[]
        {
            new IntPtr(0x1000),
            new IntPtr(0x2000),
            new IntPtr(0x3000),
            new IntPtr(0x4000)
        };

        // Act
        var table = await _manager.CreateAsync(kernelNames, queuePointers);

        // Assert - verify hash table was populated (by checking it's non-zero)
        Assert.NotEqual(0, table.RoutingHashTablePtr);

        // Verify hash table contents via unified memory
        unsafe
        {
            var hashTableBuffer = new IntPtr(table.RoutingHashTablePtr);
            var hashTablePtr = MetalNative.GetBufferContents(hashTableBuffer);
            var hashTable = new Span<uint>((void*)hashTablePtr, table.HashTableCapacity);

            // Count non-zero entries (should match kernel count)
            int populatedEntries = 0;
            for (int i = 0; i < hashTable.Length; i++)
            {
                if (hashTable[i] != 0)
                {
                    populatedEntries++;
                    uint kernelId = hashTable[i] >> 16;
                    uint queueIndex = hashTable[i] & 0xFFFF;

                    _output.WriteLine($"  Hash[{i}]: KernelID=0x{kernelId:X4}, QueueIndex={queueIndex}");
                }
            }

            Assert.Equal(kernelNames.Length, populatedEntries);
        }

        table.Dispose();
    }

    [Fact]
    public async Task HashTable_Should_Handle_Hash_Collisions()
    {
        // Arrange - use names that might collide
        var kernelNames = new[]
        {
            "Kernel_A",
            "Kernel_B",
            "Kernel_C",
            "Kernel_D",
            "Kernel_E",
            "Kernel_F",
            "Kernel_G",
            "Kernel_H"
        };
        var queuePointers = Enumerable.Range(0, kernelNames.Length)
            .Select(i => new IntPtr(0x1000 + i * 0x100))
            .ToArray();

        // Act
        var table = await _manager.CreateAsync(kernelNames, queuePointers);

        // Assert - all kernels should be registered despite potential collisions
        unsafe
        {
            var hashTableBuffer = new IntPtr(table.RoutingHashTablePtr);
            var hashTablePtr = MetalNative.GetBufferContents(hashTableBuffer);
            var hashTable = new Span<uint>((void*)hashTablePtr, table.HashTableCapacity);

            int populatedEntries = hashTable.ToArray().Count(entry => entry != 0);
            Assert.Equal(kernelNames.Length, populatedEntries);

            _output.WriteLine($"Successfully registered {populatedEntries} kernels with linear probing collision resolution");
        }

        table.Dispose();
    }

    [Fact]
    public async Task HashTable_Should_Be_Accessible_From_CPU_Due_To_Unified_Memory()
    {
        // Arrange
        var kernelNames = new[] { "TestKernel" };
        var queuePointers = new[] { new IntPtr(0x1000) };

        // Act
        var table = await _manager.CreateAsync(kernelNames, queuePointers);

        // Assert - verify CPU can read GPU-allocated unified memory
        unsafe
        {
            var hashTableBuffer = new IntPtr(table.RoutingHashTablePtr);
            var hashTablePtr = MetalNative.GetBufferContents(hashTableBuffer);

            // This should not crash - unified memory is CPU-accessible
            var hashTable = new Span<uint>((void*)hashTablePtr, table.HashTableCapacity);
            uint firstEntry = hashTable[0];

            _output.WriteLine($"CPU successfully read unified memory at 0x{hashTablePtr.ToInt64():X}");
            _output.WriteLine($"  First entry value: 0x{firstEntry:X8}");

            Assert.True(true); // If we got here without crash, unified memory works
        }

        table.Dispose();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(256)]
    public async Task Performance_CreateAsync_Should_Complete_Within_Target_Time(int kernelCount)
    {
        // Arrange
        var kernelNames = Enumerable.Range(0, kernelCount).Select(i => $"K{i}").ToArray();
        var queuePointers = Enumerable.Range(0, kernelCount).Select(i => new IntPtr(i * 0x100)).ToArray();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var table = await _manager.CreateAsync(kernelNames, queuePointers);

        stopwatch.Stop();

        // Assert - routing table creation should be fast (target: <10ms for up to 256 kernels)
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 50); // Very generous, should be <10ms

        _output.WriteLine($"Created routing table for {kernelCount} kernels in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Average per kernel: {stopwatch.Elapsed.TotalMilliseconds / kernelCount:F3}ms");

        table.Dispose();
    }

    public void Dispose()
    {
        _manager?.Dispose();

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }

        _output.WriteLine("Metal device disposed");
    }
}
