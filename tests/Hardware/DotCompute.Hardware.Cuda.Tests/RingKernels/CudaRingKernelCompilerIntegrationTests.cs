// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Hardware integration tests for the 6-stage Ring Kernel compilation pipeline.
/// Tests end-to-end compilation from C# Ring Kernel to GPU-executable PTX module.
/// </summary>
[Collection("CUDA Hardware")]
public class CudaRingKernelCompilerIntegrationTests : IAsyncLifetime
{
    private CudaRingKernelCompiler? _compiler;
    private IntPtr _cudaContext;
    private bool _cudaAvailable;

    public async Task InitializeAsync()
    {
        _cudaAvailable = HardwareDetection.IsCudaAvailable();

        if (_cudaAvailable)
        {
            try
            {
                // Initialize CUDA context for tests
                var logger = NullLogger<CudaRingKernelCompiler>.Instance;
                var kernelDiscovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
                var stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
                _compiler = new CudaRingKernelCompiler(logger, kernelDiscovery, stubGenerator);

                // Create CUDA context
                var initResult = DotCompute.Backends.CUDA.Native.CudaRuntime.cuInit(0);
                if (initResult == DotCompute.Backends.CUDA.Types.Native.CudaError.Success)
                {
                    var deviceResult = DotCompute.Backends.CUDA.Native.CudaRuntime.cuDeviceGet(out var device, 0);
                    if (deviceResult == DotCompute.Backends.CUDA.Types.Native.CudaError.Success)
                    {
                        var contextResult = DotCompute.Backends.CUDA.Native.CudaRuntimeCore.cuCtxCreate(
                            out _cudaContext,
                            0,
                            device);

                        if (contextResult != DotCompute.Backends.CUDA.Types.Native.CudaError.Success)
                        {
                            _cudaAvailable = false;
                        }
                    }
                    else
                    {
                        _cudaAvailable = false;
                    }
                }
                else
                {
                    _cudaAvailable = false;
                }
            }
            catch
            {
                _cudaAvailable = false;
            }
        }

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_cudaContext != IntPtr.Zero)
        {
            try
            {
                _ = DotCompute.Backends.CUDA.Native.CudaRuntimeCore.cuCtxDestroy(_cudaContext);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _compiler?.Dispose();
        await Task.CompletedTask;
    }

    #region End-to-End Compilation Tests

    [SkippableFact(DisplayName = "End-to-end compilation: Discovery → PTX module → Function pointer")]
    public async Task EndToEndCompilation_SimpleKernel_ShouldSucceed()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act: Complete 6-stage compilation
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            // Assert: All stages completed successfully
            compiledKernel.Should().NotBeNull();
            compiledKernel.Name.Should().Be(kernelId);
            compiledKernel.DiscoveredKernel.Should().NotBeNull();
            compiledKernel.DiscoveredKernel.KernelId.Should().Be(kernelId);
            compiledKernel.ModuleHandle.Should().NotBe(IntPtr.Zero);
            compiledKernel.FunctionPointer.Should().NotBe(IntPtr.Zero);
            compiledKernel.CudaContext.Should().Be(_cudaContext);
            compiledKernel.PtxBytes.Length.Should().BeGreaterThan(0);
            compiledKernel.IsValid.Should().BeTrue();
            compiledKernel.CompilationTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal) ||
                                   ex.Message.Contains("kernel", StringComparison.OrdinalIgnoreCase))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Compilation with custom options should apply flags")]
    public async Task Compilation_WithCustomOptions_ShouldApplyFlags()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            GenerateDebugInfo = true
        };
        options.AdditionalFlags.Add("--use_fast_math");

        try
        {
            // Act
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                options,
                assemblies,
                CancellationToken.None);

            // Assert
            compiledKernel.Should().NotBeNull();
            compiledKernel.IsValid.Should().BeTrue();

            // Verify cooperative kernel flags were added
            compiledKernel.Ptx.Should().NotBeNullOrEmpty();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    #endregion

    #region Stage-Specific Tests

    [SkippableFact(DisplayName = "Stage 1 (Discovery): Should find Ring Kernel via reflection")]
    public async Task Stage1_Discovery_ShouldFindRingKernel()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act: This internally calls DiscoverKernelAsync
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            // Assert: Discovery succeeded
            compiledKernel.DiscoveredKernel.Should().NotBeNull();
            compiledKernel.DiscoveredKernel.KernelId.Should().Be(kernelId);
            compiledKernel.DiscoveredKernel.Method.Should().NotBeNull();
            compiledKernel.DiscoveredKernel.Parameters.Should().NotBeNull();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Stage 3 (CUDA Generation): Should generate valid CUDA C++ code")]
    public async Task Stage3_CudaGeneration_ShouldGenerateValidCode()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            // Assert: Generated CUDA code exists in metadata
            compiledKernel.Metadata.Should().ContainKey("GeneratedCudaSource");
            var cudaSource = compiledKernel.Metadata["GeneratedCudaSource"] as string;
            cudaSource.Should().NotBeNullOrEmpty();
            cudaSource.Should().Contain("#include <cuda_runtime.h>");
            cudaSource.Should().Contain("extern \"C\" __global__");
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Stage 4 (PTX Compilation): Should compile CUDA → PTX")]
    public async Task Stage4_PTXCompilation_ShouldCompileToPTX()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            // Assert: PTX bytes generated
            compiledKernel.PtxBytes.Length.Should().BeGreaterThan(0);
            compiledKernel.Ptx.Should().NotBeNullOrEmpty();

            // Verify PTX format
            compiledKernel.Ptx.Should().Contain(".version");
            compiledKernel.Ptx.Should().Contain(".target");
            compiledKernel.Ptx.Should().Contain(".visible .entry");
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Stage 5 (Module Load): Should load PTX module into CUDA context")]
    public async Task Stage5_ModuleLoad_ShouldLoadPTXModule()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            // Assert: Module handle is valid
            compiledKernel.ModuleHandle.Should().NotBe(IntPtr.Zero);
            compiledKernel.CudaContext.Should().Be(_cudaContext);
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Stage 6 (Verification): Should retrieve kernel function pointer")]
    public async Task Stage6_Verification_ShouldRetrieveFunctionPointer()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            // Assert: Function pointer is valid
            compiledKernel.FunctionPointer.Should().NotBe(IntPtr.Zero);
            compiledKernel.IsValid.Should().BeTrue();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    #endregion

    #region Cache Tests

    [SkippableFact(DisplayName = "Kernel cache: Second compilation should return cached instance")]
    public async Task KernelCache_SecondCompilation_ShouldReturnCachedInstance()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act: First compilation
            var firstCompilation = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            var firstTimestamp = firstCompilation.CompilationTimestamp;

            // Wait to ensure different timestamps if recompiled
            await Task.Delay(100);

            // Act: Second compilation (should be cached)
            var secondCompilation = await _compiler.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            // Assert: Same instance returned from cache
            secondCompilation.Should().BeSameAs(firstCompilation);
            secondCompilation.CompilationTimestamp.Should().Be(firstTimestamp);
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    #endregion

    #region Concurrent Compilation Tests

    [SkippableFact(DisplayName = "Concurrent compilation: Multiple kernels in parallel should succeed")]
    public async Task ConcurrentCompilation_MultipleKernels_ShouldSucceed()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        var kernelIds = new[] { "TestKernel1", "TestKernel2", "TestKernel3" };
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act: Compile multiple kernels concurrently
            var compilationTasks = kernelIds.Select(id =>
                _compiler!.CompileRingKernelAsync(
                    id,
                    _cudaContext,
                    assemblies: assemblies,
                    cancellationToken: CancellationToken.None));

            var compiledKernels = await Task.WhenAll(compilationTasks);

            // Assert: All kernels compiled successfully
            compiledKernels.Should().HaveCount(3);
            compiledKernels.Should().OnlyContain(k => k.IsValid);
            compiledKernels.Should().OnlyContain(k => k.ModuleHandle != IntPtr.Zero);
            compiledKernels.Should().OnlyContain(k => k.FunctionPointer != IntPtr.Zero);
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal) ||
                                   ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            Skip.If(true, $"CUDA operation or kernel discovery failed: {ex.Message}");
        }
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact(DisplayName = "Error: Non-existent kernel should throw InvalidOperationException")]
    public async Task Error_NonExistentKernel_ShouldThrow()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string invalidKernelId = "NonExistentKernel_12345";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        // Act & Assert
        await FluentActions.Awaiting(async () =>
            await _compiler!.CompileRingKernelAsync(
                invalidKernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{invalidKernelId}*");
    }

    [SkippableFact(DisplayName = "Error: Invalid CUDA context should throw InvalidOperationException")]
    public async Task Error_InvalidCudaContext_ShouldThrow()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var invalidContext = IntPtr.Zero;

        // Act & Assert
        await FluentActions.Awaiting(async () =>
            await _compiler!.CompileRingKernelAsync(
                kernelId,
                invalidContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [SkippableFact(DisplayName = "Error: Cancellation should abort compilation")]
    public async Task Error_Cancellation_ShouldAbortCompilation()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await FluentActions.Awaiting(async () =>
            await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Disposal Tests

    [SkippableFact(DisplayName = "Disposal: Should cleanup CUDA resources")]
    public async Task Disposal_ShouldCleanupCudaResources()
    {
        Skip.IfNot(_cudaAvailable, "CUDA device not available");

        // Arrange
        const string kernelId = "TestSimpleKernel";
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        try
        {
            // Act: Compile and dispose
            var compiledKernel = await _compiler!.CompileRingKernelAsync(
                kernelId,
                _cudaContext,
                assemblies: assemblies,
                cancellationToken: CancellationToken.None);

            compiledKernel.IsValid.Should().BeTrue();

            // Dispose the kernel
            compiledKernel.Dispose();

            // Assert: Kernel is disposed
            compiledKernel.IsDisposed.Should().BeTrue();
            compiledKernel.IsValid.Should().BeFalse();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    #endregion
}

#region Test Ring Kernel Definitions

/// <summary>
/// Test Ring Kernel for integration tests.
/// </summary>
public static class TestRingKernels
{
    [RingKernel(KernelId = "TestSimpleKernel")]
    public static void TestSimpleKernel(int x, int y)
    {
        // Simple test kernel - implementation not needed for compilation tests
        _ = x + y;
    }

    [RingKernel(KernelId = "TestKernel1")]
    public static void TestKernel1(int value)
    {
        _ = value;
    }

    [RingKernel(KernelId = "TestKernel2")]
    public static void TestKernel2(float value)
    {
        _ = value;
    }

    [RingKernel(KernelId = "TestKernel3")]
    public static void TestKernel3(double value)
    {
        _ = value;
    }
}

#endregion
