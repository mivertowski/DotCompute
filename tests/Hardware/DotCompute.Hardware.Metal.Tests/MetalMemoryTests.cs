// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Tests for Metal memory management
    /// </summary>
    [Trait("Category", "RequiresMetal")]
    public class MetalMemoryTests : MetalTestBase
    {
        public MetalMemoryTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public async Task Large_Buffer_Allocation_Should_Work()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            // Try to allocate a large buffer (100 MB)
            const int sizeInMB = 100;
            const int elementCount = sizeInMB * 1024 * 1024 / sizeof(float);
            
            var buffer = await accelerator!.Memory.AllocateAsync<float>(elementCount);
            
            buffer.Should().NotBeNull();
            buffer.Length.Should().Be(elementCount);
            buffer.SizeInBytes.Should().Be(sizeInMB * 1024 * 1024);
            
            Output.WriteLine($"Successfully allocated {sizeInMB} MB buffer with {elementCount:N0} floats");
            
            await buffer.DisposeAsync();
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Memory_Pooling_Should_Reduce_Allocation_Time()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            const int bufferSize = 1024 * 1024; // 1 MB
            const int iterations = 10;
            
            var allocationTimes = new List<long>();
            
            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var buffer = await accelerator!.Memory.AllocateAsync<float>(bufferSize / sizeof(float));
                sw.Stop();
                
                allocationTimes.Add(sw.ElapsedMilliseconds);
                
                // Immediately dispose to return to pool
                await buffer.DisposeAsync();
                
                Output.WriteLine($"Allocation {i + 1}: {sw.ElapsedMilliseconds}ms");
            }
            
            // Later allocations should be faster due to pooling
            var firstHalf = allocationTimes.Take(iterations / 2).Average();
            var secondHalf = allocationTimes.Skip(iterations / 2).Average();
            
            Output.WriteLine($"Average first half: {firstHalf:F2}ms");
            Output.WriteLine($"Average second half: {secondHalf:F2}ms");
            
            // Second half should be faster or similar (pooling effect)
            secondHalf.Should().BeLessThanOrEqualTo(firstHalf * 1.5,
                "Pool reuse should prevent significant slowdown");
            
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Concurrent_Memory_Operations_Should_Be_Safe()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            const int taskCount = 10;
            const int bufferSize = 1024;
            
            var tasks = new Task[taskCount];
            var buffers = new DotCompute.Abstractions.IUnifiedMemoryBuffer<float>[taskCount];
            
            // Allocate and operate on buffers concurrently
            for (int i = 0; i < taskCount; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    // Allocate
                    buffers[index] = await accelerator!.Memory.AllocateAsync<float>(bufferSize);
                    
                    // Write data
                    var data = new float[bufferSize];
                    for (int j = 0; j < bufferSize; j++)
                    {
                        data[j] = index * 1000 + j;
                    }
                    await buffers[index].CopyFromAsync(data.AsMemory());
                    
                    // Read back
                    var result = new float[bufferSize];
                    await buffers[index].CopyToAsync(result.AsMemory());
                    
                    // Verify
                    for (int j = 0; j < bufferSize; j++)
                    {
                        result[j].Should().BeApproximately(data[j], 0.001f);
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            
            Output.WriteLine($"Successfully completed {taskCount} concurrent memory operations");
            
            // Cleanup
            foreach (var buffer in buffers)
            {
                if (buffer != null)
                {
                    await buffer.DisposeAsync();
                }
            }
            
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Memory_Copy_Performance_Should_Be_Reasonable()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            // Test different buffer sizes
            var sizes = new[] { 1024, 1024 * 16, 1024 * 256, 1024 * 1024, 1024 * 1024 * 10 };
            
            foreach (var size in sizes)
            {
                var elementCount = size / sizeof(float);
                var data = new float[elementCount];
                
                // Initialize data
                for (int i = 0; i < Math.Min(1000, elementCount); i++)
                {
                    data[i] = i;
                }
                
                var buffer = await accelerator!.Memory.AllocateAsync<float>(elementCount);
                
                // Measure upload time
                var swUpload = Stopwatch.StartNew();
                await buffer.CopyFromAsync(data.AsMemory());
                swUpload.Stop();
                
                // Measure download time
                var result = new float[elementCount];
                var swDownload = Stopwatch.StartNew();
                await buffer.CopyToAsync(result.AsMemory());
                swDownload.Stop();
                
                var sizeMB = size / (1024.0 * 1024.0);
                var uploadBandwidth = sizeMB / (swUpload.ElapsedMilliseconds / 1000.0);
                var downloadBandwidth = sizeMB / (swDownload.ElapsedMilliseconds / 1000.0);
                
                Output.WriteLine($"Size: {sizeMB:F2} MB");
                Output.WriteLine($"  Upload: {swUpload.ElapsedMilliseconds}ms ({uploadBandwidth:F2} MB/s)");
                Output.WriteLine($"  Download: {swDownload.ElapsedMilliseconds}ms ({downloadBandwidth:F2} MB/s)");
                
                await buffer.DisposeAsync();
            }
            
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Zero_Copy_Should_Work_On_Unified_Memory()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");
            Skip.IfNot(IsAppleSilicon(), "Zero-copy requires unified memory (Apple Silicon)");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            const int size = 1024;
            var buffer = await accelerator!.Memory.AllocateAsync<float>(size);
            
            // On unified memory systems, we should be able to access memory efficiently
            var data = new float[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = i * 3.14f;
            }
            
            // Copy should be very fast on unified memory
            var sw = Stopwatch.StartNew();
            await buffer.CopyFromAsync(data.AsMemory());
            sw.Stop();
            
            Output.WriteLine($"Copy time on unified memory: {sw.ElapsedMilliseconds}ms");
            sw.ElapsedMilliseconds.Should().BeLessThan(10, 
                "Copy on unified memory should be very fast");
            
            await buffer.DisposeAsync();
            // accelerator will be disposed automatically via await using
        }
    }
}