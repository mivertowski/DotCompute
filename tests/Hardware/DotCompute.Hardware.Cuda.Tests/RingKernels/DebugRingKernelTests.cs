// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Minimal debug tests for ring kernel execution issues.
/// </summary>
[Collection("CUDA Hardware")]
public class DebugRingKernelTests
{
    private readonly ITestOutputHelper _output;

    public DebugRingKernelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact(DisplayName = "Debug: Compile and inspect TestSimpleKernel PTX")]
    public async Task Debug_CompileAndInspectPtx()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Setup
        var kernelDiscovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var serializerGenerator = new CudaMemoryPackSerializerGenerator(NullLogger<CudaMemoryPackSerializerGenerator>.Instance);
        var compiler = new CudaRingKernelCompiler(NullLogger<CudaRingKernelCompiler>.Instance, kernelDiscovery, stubGenerator, serializerGenerator);

        // Initialize CUDA
        var initResult = CudaRuntimeCore.cuInit(0);
        _output.WriteLine($"cuInit: {initResult}");

        var deviceResult = CudaRuntime.cuDeviceGet(out var device, 0);
        _output.WriteLine($"cuDeviceGet: {deviceResult}, device={device}");

        // Use primary context instead of creating a new one
        // This matches how the production runtime works and is more stable
        var context = IntPtr.Zero;
        var ctxResult = CudaRuntime.cuDevicePrimaryCtxRetain(ref context, device);
        _output.WriteLine($"cuDevicePrimaryCtxRetain: {ctxResult}, context=0x{context:X}");

        var setCtxResult = CudaRuntime.cuCtxSetCurrent(context);
        _output.WriteLine($"cuCtxSetCurrent: {setCtxResult}");

        try
        {
            // Compile with test assembly
            var assemblies = new[] { Assembly.GetExecutingAssembly() };
            const string kernelId = "TestSimpleKernel";

            _output.WriteLine($"\nCompiling kernel '{kernelId}'...");
            var compiledKernel = await compiler.CompileRingKernelAsync(
                kernelId,
                context,
                assemblies: assemblies);

            compiledKernel.Should().NotBeNull();
            compiledKernel.IsValid.Should().BeTrue();

            _output.WriteLine($"PTX size: {compiledKernel.PtxBytes.Length} bytes");
            _output.WriteLine($"Module: 0x{compiledKernel.ModuleHandle:X}");
            _output.WriteLine($"Function: 0x{compiledKernel.FunctionPointer:X}");

            // Show first 2000 chars of PTX
            var ptxString = System.Text.Encoding.UTF8.GetString(compiledKernel.PtxBytes.ToArray());
            _output.WriteLine($"\n=== PTX (first 2000 chars) ===\n{ptxString[..Math.Min(2000, ptxString.Length)]}");
        }
        finally
        {
            // Release primary context reference (don't destroy it)
            _ = CudaRuntime.cuDevicePrimaryCtxRelease(device);
            compiler.ClearCache();
        }
    }

    [SkippableFact(DisplayName = "Debug: Generate and inspect CUDA code for TestSimpleKernel")]
    public void Debug_InspectGeneratedCudaCode()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Setup
        var kernelDiscovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);

        // Discover the kernel
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var kernels = kernelDiscovery.DiscoverKernels(assemblies);
        var testKernel = kernels.FirstOrDefault(k => k.KernelId == "TestSimpleKernel");

        testKernel.Should().NotBeNull("TestSimpleKernel should be discoverable");

        _output.WriteLine($"Kernel: {testKernel!.KernelId}");
        _output.WriteLine($"Method: {testKernel.Method.DeclaringType?.Name}.{testKernel.Method.Name}");
        _output.WriteLine($"Input type: {testKernel.InputMessageTypeName}");
        _output.WriteLine($"Uses K2K: {testKernel.UsesK2KMessaging}");

        // Generate CUDA code
        var cudaCode = stubGenerator.GenerateKernelStub(testKernel);

        _output.WriteLine($"\n=== Generated CUDA Code ===\n{cudaCode}");
    }

    [SkippableFact(DisplayName = "Debug: Minimal kernel launch test")]
    public async Task Debug_MinimalKernelLaunch()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        Console.WriteLine("[DEBUG] Starting minimal kernel launch test");

        // Setup
        var kernelDiscovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var serializerGenerator = new CudaMemoryPackSerializerGenerator(NullLogger<CudaMemoryPackSerializerGenerator>.Instance);
        var compiler = new CudaRingKernelCompiler(NullLogger<CudaRingKernelCompiler>.Instance, kernelDiscovery, stubGenerator, serializerGenerator);

        // Initialize CUDA using DRIVER API consistently
        Console.WriteLine("[DEBUG] Initializing CUDA (driver API)...");
        var initResult = CudaRuntimeCore.cuInit(0);
        Console.WriteLine($"[DEBUG] cuInit: {initResult}");

        var deviceResult = CudaRuntime.cuDeviceGet(out var device, 0);
        Console.WriteLine($"[DEBUG] cuDeviceGet: {deviceResult}");

        var ctxResult = CudaRuntimeCore.cuCtxCreate(out var context, 0, device);
        Console.WriteLine($"[DEBUG] cuCtxCreate: {ctxResult}, context=0x{context:X}");

        try
        {
            // Compile kernel
            var assemblies = new[] { Assembly.GetExecutingAssembly() };
            const string kernelId = "TestSimpleKernel";

            Console.WriteLine($"[DEBUG] Compiling kernel '{kernelId}'...");
            var compiledKernel = await compiler.CompileRingKernelAsync(kernelId, context, assemblies: assemblies);

            Console.WriteLine($"[DEBUG] Compilation result: IsValid={compiledKernel?.IsValid}, PTX={compiledKernel?.PtxBytes.Length} bytes");

            if (compiledKernel == null || !compiledKernel.IsValid)
            {
                _output.WriteLine("Compilation failed!");
                return;
            }

            // Restore context after compilation (compilation may have changed current context)
            Console.WriteLine("[DEBUG] Restoring context after compilation...");
            var restoreCtxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
            Console.WriteLine($"[DEBUG] cuCtxSetCurrent: {restoreCtxResult}");

            // Allocate control block using DRIVER API
            Console.WriteLine("[DEBUG] Allocating control block (cuMemAlloc)...");
            var controlBlockSize = 128; // RingKernelControlBlock is 128 bytes
            var deviceControlBlock = IntPtr.Zero;
            var allocResult = CudaApi.cuMemAlloc(ref deviceControlBlock, (nuint)controlBlockSize);
            Console.WriteLine($"[DEBUG] cuMemAlloc: {allocResult}, ptr=0x{deviceControlBlock:X}");

            if (allocResult != CudaError.Success)
            {
                _output.WriteLine($"Failed to allocate control block: {allocResult}");
                return;
            }

            // Initialize control block to zeros using DRIVER API
            var memsetResult = CudaApi.cuMemsetD8(deviceControlBlock, 0, (nuint)controlBlockSize);
            Console.WriteLine($"[DEBUG] cuMemsetD8: {memsetResult}");

            // Create stream using DRIVER API
            var stream = IntPtr.Zero;
            var streamResult = CudaApi.cuStreamCreate(ref stream, 0);
            Console.WriteLine($"[DEBUG] cuStreamCreate: {streamResult}, stream=0x{stream:X}");

            try
            {
                // Launch kernel with minimal parameters
                Console.WriteLine("[DEBUG] Launching kernel...");

                unsafe
                {
                    var ptrStorage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(IntPtr));
                    *(IntPtr*)ptrStorage = deviceControlBlock;

                    var kernelParams = new IntPtr[] { ptrStorage };
                    var argPtrsHandle = System.Runtime.InteropServices.GCHandle.Alloc(kernelParams, System.Runtime.InteropServices.GCHandleType.Pinned);

                    Console.WriteLine($"[DEBUG] Kernel params: controlBlock=0x{deviceControlBlock:X}");

                    var launchResult = CudaRuntime.cuLaunchKernel(
                        compiledKernel.FunctionPointer,
                        1, 1, 1,    // Grid: 1x1x1
                        32, 1, 1,   // Block: 32x1x1 (minimal)
                        0,          // Shared memory
                        stream,
                        argPtrsHandle.AddrOfPinnedObject(),
                        IntPtr.Zero);

                    Console.WriteLine($"[DEBUG] cuLaunchKernel: {launchResult}");

                    argPtrsHandle.Free();
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptrStorage);

                    if (launchResult != CudaError.Success)
                    {
                        _output.WriteLine($"Kernel launch failed: {launchResult}");
                        return;
                    }
                }

                // Sync using DRIVER API
                Console.WriteLine("[DEBUG] Synchronizing stream (cuStreamSynchronize)...");
                var syncResult = CudaApi.cuStreamSynchronize(stream);
                Console.WriteLine($"[DEBUG] cuStreamSynchronize: {syncResult}");

                if (syncResult != CudaError.Success)
                {
                    _output.WriteLine($"Stream sync failed: {syncResult}");
                }
                else
                {
                    _output.WriteLine("Kernel executed successfully!");

                    // Read back control block using DRIVER API
                    var hostControlBlock = new byte[controlBlockSize];
                    unsafe
                    {
                        fixed (byte* ptr = hostControlBlock)
                        {
                            var copyResult = CudaApi.cuMemcpyDtoH(
                                (IntPtr)ptr,
                                deviceControlBlock,
                                (nuint)controlBlockSize);
                            Console.WriteLine($"[DEBUG] cuMemcpyDtoH: {copyResult}");
                        }
                    }

                    // Parse control block
                    var hasTerminated = BitConverter.ToInt32(hostControlBlock, 8);
                    var messagesProcessed = BitConverter.ToInt64(hostControlBlock, 16);
                    Console.WriteLine($"[DEBUG] Control block: HasTerminated={hasTerminated}, MessagesProcessed={messagesProcessed}");
                    _output.WriteLine($"Control block: HasTerminated={hasTerminated}, MessagesProcessed={messagesProcessed}");
                }
            }
            finally
            {
                CudaApi.cuStreamDestroy(stream);
                CudaApi.cuMemFree(deviceControlBlock);
            }
        }
        finally
        {
            CudaRuntimeCore.cuCtxDestroy(context);
            compiler.ClearCache();
        }
    }
}
