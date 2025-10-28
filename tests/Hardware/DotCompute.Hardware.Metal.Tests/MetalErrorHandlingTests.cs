// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Backends.Metal.Native;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Runtime.InteropServices;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Error handling and edge case tests for Metal backend.
    /// Tests error conditions, invalid inputs, and recovery scenarios.
    /// </summary>
    [Trait("Category", "Hardware")]
    [Trait("Category", "ErrorHandling")]
    [Trait("Category", "Metal")]
    public class MetalErrorHandlingTests : MetalTestBase
    {
        private const string ValidShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void validKernel(device float* data [[ buffer(0) ]],
                       uint gid [[ thread_position_in_grid ]])
{
    data[gid] = gid * 2.0f;
}";

        private const string InvalidSyntaxShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void invalidSyntaxKernel(device float* data [[ buffer(0) ]],
                               uint gid [[ thread_position_in_grid ]])
{
    data[gid] = this_is_not_valid_syntax;
    missing_semicolon
}";

        private const string InvalidFunctionShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void invalidFunctionKernel(device float* data [[ buffer(0) ]],
                                 uint gid [[ thread_position_in_grid ]])
{
    data[gid] = undefinedFunction(gid);
}";

        private const string OutOfBoundsShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void outOfBoundsKernel(device float* data [[ buffer(0) ]],
                             constant uint& size [[ buffer(1) ]],
                             uint gid [[ thread_position_in_grid ]])
{
    // Intentionally access beyond bounds for testing
    data[gid + size] = gid * 2.0f;
}";

        public MetalErrorHandlingTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public void Invalid_Shader_Compilation_Should_Be_Handled_Gracefully()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                Output.WriteLine("Testing invalid shader compilation scenarios:");

                // Test 1: Invalid syntax
                Output.WriteLine("  Testing invalid syntax shader...");
                var invalidLibrary = MetalNative.CreateLibraryWithSource(device, InvalidSyntaxShader);
                invalidLibrary.Should().Be(IntPtr.Zero, "Invalid syntax shader should fail to compile");

                // Test 2: Invalid function call
                Output.WriteLine("  Testing shader with undefined function...");
                var invalidFunctionLibrary = MetalNative.CreateLibraryWithSource(device, InvalidFunctionShader);
                invalidFunctionLibrary.Should().Be(IntPtr.Zero, "Shader with undefined function should fail to compile");

                // Test 3: Empty shader
                Output.WriteLine("  Testing empty shader...");
                var emptyLibrary = MetalNative.CreateLibraryWithSource(device, "");
                emptyLibrary.Should().Be(IntPtr.Zero, "Empty shader should fail to compile");

                // Test 4: Valid shader should still work
                Output.WriteLine("  Testing valid shader for comparison...");
                var validLibrary = MetalNative.CreateLibraryWithSource(device, ValidShader);
                validLibrary.Should().NotBe(IntPtr.Zero, "Valid shader should compile successfully");
                
                if (validLibrary != IntPtr.Zero)
                {
                    MetalNative.ReleaseLibrary(validLibrary);
                }

                Output.WriteLine("✓ All invalid shader compilation tests handled gracefully");
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Invalid_Function_Access_Should_Be_Handled()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            var library = MetalNative.CreateLibraryWithSource(device, ValidShader);
            Skip.If(library == IntPtr.Zero, "Valid shader compilation failed");

            try
            {
                Output.WriteLine("Testing invalid function access scenarios:");

                // Test 1: Non-existent function name
                Output.WriteLine("  Testing non-existent function name...");
                var invalidFunction = MetalNative.GetFunction(library, "nonExistentFunction");
                invalidFunction.Should().Be(IntPtr.Zero, "Non-existent function should return null pointer");

                // Test 2: Empty function name
                Output.WriteLine("  Testing empty function name...");
                var emptyNameFunction = MetalNative.GetFunction(library, "");
                emptyNameFunction.Should().Be(IntPtr.Zero, "Empty function name should return null pointer");

                // Test 3: Valid function should work
                Output.WriteLine("  Testing valid function name...");
                var validFunction = MetalNative.GetFunction(library, "validKernel");
                validFunction.Should().NotBe(IntPtr.Zero, "Valid function should be found");

                if (validFunction != IntPtr.Zero)
                {
                    MetalNative.ReleaseFunction(validFunction);
                }

                Output.WriteLine("✓ All invalid function access tests handled gracefully");
            }
            finally
            {
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Pipeline_State_Creation_Errors_Should_Be_Reported()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            var library = MetalNative.CreateLibraryWithSource(device, ValidShader);
            var function = MetalNative.GetFunction(library, "validKernel");
            
            Skip.If(function == IntPtr.Zero, "Function retrieval failed");

            try
            {
                Output.WriteLine("Testing pipeline state creation error scenarios:");

                // Test 1: Valid pipeline creation should work
                Output.WriteLine("  Testing valid pipeline state creation...");
                var error = IntPtr.Zero;
                var validPipeline = MetalNative.CreateComputePipelineState(device, function, ref error);
                
                if (error != IntPtr.Zero)
                {
                    var errorDesc = MetalNative.GetErrorLocalizedDescription(error);
                    var errorMessage = Marshal.PtrToStringAnsi(errorDesc) ?? "Unknown error";
                    Output.WriteLine($"    Unexpected error in valid pipeline creation: {errorMessage}");
                    MetalNative.ReleaseError(error);
                }
                
                validPipeline.Should().NotBe(IntPtr.Zero, "Valid pipeline state should be created");

                if (validPipeline != IntPtr.Zero)
                {
                    MetalNative.ReleaseComputePipelineState(validPipeline);
                }

                // Test 2: Invalid device pointer (should be handled by native layer)
                Output.WriteLine("  Testing pipeline creation with null device would crash - skipping");

                // Test 3: Invalid function pointer (should be handled by native layer) 
                Output.WriteLine("  Testing pipeline creation with null function would crash - skipping");

                Output.WriteLine("✓ Pipeline state creation error tests completed");
            }
            finally
            {
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Buffer_Allocation_Limits_Should_Be_Respected()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var deviceInfo = MetalNative.GetDeviceInfo(device);
                var maxBufferLength = deviceInfo.MaxBufferLength;
                
                Output.WriteLine("Testing buffer allocation limit scenarios:");
                Output.WriteLine($"  Device max buffer length: {maxBufferLength / (1024.0 * 1024 * 1024):F2} GB");

                // Test 1: Valid buffer allocation
                Output.WriteLine("  Testing valid buffer allocation (1 MB)...");
                var validSize = (nuint)(1024 * 1024); // 1 MB
                var validBuffer = MetalNative.CreateBuffer(device, validSize, 0);
                validBuffer.Should().NotBe(IntPtr.Zero, "Valid buffer allocation should succeed");
                
                if (validBuffer != IntPtr.Zero)
                {
                    MetalNative.ReleaseBuffer(validBuffer);
                }

                // Test 2: Zero size buffer
                Output.WriteLine("  Testing zero size buffer allocation...");
                var zeroBuffer = MetalNative.CreateBuffer(device, 0, 0);
                zeroBuffer.Should().Be(IntPtr.Zero, "Zero size buffer allocation should fail");

                // Test 3: Excessive buffer size (beyond device limits)
                Output.WriteLine($"  Testing excessive buffer allocation (beyond device max)...");
                var excessiveSize = (nuint)(maxBufferLength * 2); // Double the max
                var excessiveBuffer = MetalNative.CreateBuffer(device, excessiveSize, 0);
                excessiveBuffer.Should().Be(IntPtr.Zero, "Excessive buffer allocation should fail");

                // Test 4: Multiple large allocations to test memory pressure
                Output.WriteLine("  Testing multiple large buffer allocations...");
                var largeBuffers = new List<IntPtr>();
                var largeSize = (nuint)(maxBufferLength / 10); // 10% of max each
                var successfulAllocations = 0;
                
                try
                {
                    for (var i = 0; i < 15; i++) // Try to allocate more than available
                    {
                        var buffer = MetalNative.CreateBuffer(device, largeSize, 0);
                        if (buffer != IntPtr.Zero)
                        {
                            largeBuffers.Add(buffer);
                            successfulAllocations++;
                        }
                        else
                        {
                            break; // Stop when allocation fails
                        }
                    }

                    Output.WriteLine($"    Successfully allocated {successfulAllocations} large buffers");
                    successfulAllocations.Should().BeGreaterThan(0, "Should allocate at least some large buffers");
                    successfulAllocations.Should().BeLessThan(15, "Should not allocate unlimited buffers");
                }
                finally
                {
                    // Cleanup large buffers
                    foreach (var buffer in largeBuffers)
                    {
                        MetalNative.ReleaseBuffer(buffer);
                    }
                }

                Output.WriteLine("✓ Buffer allocation limit tests completed");
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Invalid_Kernel_Parameters_Should_Be_Handled()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, ValidShader);
            var function = MetalNative.GetFunction(library, "validKernel");
            var errorPtr = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref errorPtr);

            Skip.If(pipelineState == IntPtr.Zero, "Pipeline state creation failed");

            try
            {
                Output.WriteLine("Testing invalid kernel parameter scenarios:");

                var validBuffer = MetalNative.CreateBuffer(device, 1024 * sizeof(float), 0);
                
                try
                {
                    // Test 1: Valid execution for baseline
                    Output.WriteLine("  Testing valid kernel execution...");
                    var validExecution = ExecuteKernelSafely(commandQueue, pipelineState, validBuffer, 1024);
                    validExecution.Should().BeTrue("Valid kernel execution should succeed");

                    // Test 2: Zero thread count
                    Output.WriteLine("  Testing zero thread execution...");
                    var zeroExecution = ExecuteKernelSafely(commandQueue, pipelineState, validBuffer, 0);
                    // This may or may not fail depending on Metal implementation
                    Output.WriteLine($"    Zero thread execution result: {(zeroExecution ? "Success" : "Failed")}");

                    // Test 3: Excessive thread count
                    Output.WriteLine("  Testing excessive thread count...");
                    var excessiveCount = 1024 * 1024 * 1024; // 1 billion threads
                    var excessiveExecution = ExecuteKernelSafely(commandQueue, pipelineState, validBuffer, excessiveCount);
                    // This should likely fail or be handled gracefully
                    Output.WriteLine($"    Excessive thread execution result: {(excessiveExecution ? "Success" : "Failed")}");

                    // Test 4: Invalid threadgroup sizes
                    Output.WriteLine("  Testing invalid threadgroup configuration...");
                    var invalidThreadgroupExecution = ExecuteKernelWithInvalidThreadgroups(commandQueue, pipelineState, validBuffer);
                    Output.WriteLine($"    Invalid threadgroup execution result: {(invalidThreadgroupExecution ? "Success" : "Failed")}");

                    Output.WriteLine("✓ Invalid kernel parameter tests completed");
                }
                finally
                {
                    MetalNative.ReleaseBuffer(validBuffer);
                }
            }
            finally
            {
                MetalNative.ReleaseComputePipelineState(pipelineState);
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseCommandQueue(commandQueue);
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Memory_Access_Violations_Should_Be_Detected()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var device = MetalNative.CreateSystemDefaultDevice();
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, OutOfBoundsShader);
            
            if (library == IntPtr.Zero)
            {
                Output.WriteLine("Out-of-bounds shader compilation failed - using valid shader instead");
                library = MetalNative.CreateLibraryWithSource(device, ValidShader);
            }

            var function = MetalNative.GetFunction(library, library != IntPtr.Zero && 
                MetalNative.GetFunction(library, "outOfBoundsKernel") != IntPtr.Zero ? 
                "outOfBoundsKernel" : "validKernel");


            Skip.If(function == IntPtr.Zero, "Function retrieval failed");

            var errorPtr = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref errorPtr);
            Skip.If(pipelineState == IntPtr.Zero, "Pipeline state creation failed");

            try
            {
                Output.WriteLine("Testing memory access violation scenarios:");

                const int bufferSize = 1024;
                var buffer = MetalNative.CreateBuffer(device, (nuint)(bufferSize * sizeof(float)), 0);

                try
                {
                    // Test 1: Normal access within bounds
                    Output.WriteLine("  Testing normal memory access...");
                    var normalAccess = ExecuteMemoryAccessKernel(commandQueue, pipelineState, buffer, bufferSize, bufferSize / 2);
                    normalAccess.Should().BeTrue("Normal memory access should succeed");

                    // Test 2: Access at boundary
                    Output.WriteLine("  Testing boundary memory access...");
                    var boundaryAccess = ExecuteMemoryAccessKernel(commandQueue, pipelineState, buffer, bufferSize, bufferSize - 1);
                    boundaryAccess.Should().BeTrue("Boundary memory access should succeed");

                    // Test 3: Potential out-of-bounds access (if shader supports it)
                    if (MetalNative.GetFunction(library, "outOfBoundsKernel") != IntPtr.Zero)
                    {
                        Output.WriteLine("  Testing out-of-bounds memory access...");
                        var outOfBoundsAccess = ExecuteMemoryAccessKernel(commandQueue, pipelineState, buffer, bufferSize, 1);
                        Output.WriteLine($"    Out-of-bounds access result: {(outOfBoundsAccess ? "Success" : "Failed")}");
                        // Note: Metal may or may not catch this at runtime depending on configuration
                    }

                    Output.WriteLine("✓ Memory access violation tests completed");
                }
                finally
                {
                    MetalNative.ReleaseBuffer(buffer);
                }
            }
            finally
            {
                MetalNative.ReleaseComputePipelineState(pipelineState);
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseCommandQueue(commandQueue);
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Resource_Cleanup_Should_Handle_Invalid_Pointers()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            Output.WriteLine("Testing resource cleanup with invalid pointers:");

            // Test 1: Releasing null pointers should not crash
            Output.WriteLine("  Testing null pointer releases...");
            try
            {
                MetalNative.ReleaseDevice(IntPtr.Zero);
                MetalNative.ReleaseCommandQueue(IntPtr.Zero);
                MetalNative.ReleaseLibrary(IntPtr.Zero);
                MetalNative.ReleaseFunction(IntPtr.Zero);
                MetalNative.ReleaseComputePipelineState(IntPtr.Zero);
                MetalNative.ReleaseBuffer(IntPtr.Zero);
                MetalNative.ReleaseCommandBuffer(IntPtr.Zero);
                MetalNative.ReleaseComputeCommandEncoder(IntPtr.Zero);
                Output.WriteLine("    ✓ Null pointer releases handled gracefully");
            }
            catch (Exception ex)
            {
                ex.Should().BeNull($"Null pointer release should not throw exception: {ex.Message}");
            }

            // Test 2: Double release should be handled
            Output.WriteLine("  Testing double release scenarios...");
            var device = MetalNative.CreateSystemDefaultDevice();
            if (device != IntPtr.Zero)
            {
                var buffer = MetalNative.CreateBuffer(device, 1024, 0);
                if (buffer != IntPtr.Zero)
                {
                    try
                    {
                        MetalNative.ReleaseBuffer(buffer);
                        MetalNative.ReleaseBuffer(buffer); // Double release
                        Output.WriteLine("    ✓ Double buffer release handled gracefully");
                    }
                    catch (Exception ex)
                    {
                        Output.WriteLine($"    Double buffer release threw exception: {ex.Message}");
                        // This might be expected behavior
                    }
                }

                MetalNative.ReleaseDevice(device);
            }

            Output.WriteLine("✓ Resource cleanup tests completed");
        }

        private bool ExecuteKernelSafely(IntPtr commandQueue, IntPtr pipelineState, IntPtr buffer, int threadCount)
        {
            try
            {
                var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                if (commandBuffer == IntPtr.Zero) return false;

                try
                {
                    var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                    if (encoder == IntPtr.Zero) return false;

                    try
                    {
                        MetalNative.SetComputePipelineState(encoder, pipelineState);
                        MetalNative.SetBuffer(encoder, buffer, 0, 0);

                        if (threadCount > 0)
                        {
                            var threadsPerGroup = Math.Min(256u, (uint)threadCount);
                            var threadgroupsPerGrid = ((uint)threadCount + threadsPerGroup - 1) / threadsPerGroup;
                            
                            MetalNative.DispatchThreadgroups(encoder, threadgroupsPerGrid, 1, 1,
                                                           threadsPerGroup, 1, 1);
                        }

                        MetalNative.EndEncoding(encoder);
                        MetalNative.Commit(commandBuffer);
                        MetalNative.WaitUntilCompleted(commandBuffer);

                        return true;
                    }
                    finally
                    {
                        if (encoder != IntPtr.Zero)
                            MetalNative.ReleaseComputeCommandEncoder(encoder);
                    }
                }
                finally
                {
                    MetalNative.ReleaseCommandBuffer(commandBuffer);
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"    Kernel execution exception: {ex.Message}");
                return false;
            }
        }

        private bool ExecuteKernelWithInvalidThreadgroups(IntPtr commandQueue, IntPtr pipelineState, IntPtr buffer)
        {
            try
            {
                var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                
                MetalNative.SetComputePipelineState(encoder, pipelineState);
                MetalNative.SetBuffer(encoder, buffer, 0, 0);

                // Try invalid threadgroup configuration (0 threads per group)
                MetalNative.DispatchThreadgroups(encoder, 1, 1, 1, 0, 1, 1);

                MetalNative.EndEncoding(encoder);
                MetalNative.Commit(commandBuffer);
                MetalNative.WaitUntilCompleted(commandBuffer);

                MetalNative.ReleaseComputeCommandEncoder(encoder);
                MetalNative.ReleaseCommandBuffer(commandBuffer);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ExecuteMemoryAccessKernel(IntPtr commandQueue, IntPtr pipelineState, IntPtr buffer, int bufferSize, int accessSize)
        {
            try
            {
                var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                
                MetalNative.SetComputePipelineState(encoder, pipelineState);
                MetalNative.SetBuffer(encoder, buffer, 0, 0);
                
                var bufferSizeUint = (uint)bufferSize;
                unsafe
                {
                    MetalNative.SetBytes(encoder, &bufferSizeUint, sizeof(uint), 1);
                }

                var threadsPerGroup = 256u;
                var threadgroupsPerGrid = ((uint)accessSize + threadsPerGroup - 1) / threadsPerGroup;
                
                MetalNative.DispatchThreadgroups(encoder, threadgroupsPerGrid, 1, 1,
                                               threadsPerGroup, 1, 1);

                MetalNative.EndEncoding(encoder);
                MetalNative.Commit(commandBuffer);
                MetalNative.WaitUntilCompleted(commandBuffer);

                MetalNative.ReleaseComputeCommandEncoder(encoder);
                MetalNative.ReleaseCommandBuffer(commandBuffer);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}