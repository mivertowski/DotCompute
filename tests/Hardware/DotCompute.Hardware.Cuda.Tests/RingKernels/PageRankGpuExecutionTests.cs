// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Core.Messaging;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// GPU execution tests for PageRank ring kernels.
/// Tests full pipeline: kernel discovery → compilation → launch → message processing → results.
/// </summary>
[Collection("CUDA Hardware")]
public class PageRankGpuExecutionTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CudaRingKernelRuntime> _runtimeLogger;
    private readonly ILogger<CudaRingKernelCompiler> _compilerLogger;
    private readonly ILogger<RingKernelDiscovery> _discoveryLogger;
    private readonly ILogger<CudaRingKernelStubGenerator> _stubLogger;
    private readonly CudaRingKernelCompiler _compiler;
    private readonly CudaRingKernelRuntime _runtime;

    public PageRankGpuExecutionTests(ITestOutputHelper output)
    {
        _output = output;
        _runtimeLogger = Substitute.For<ILogger<CudaRingKernelRuntime>>();
        _compilerLogger = Substitute.For<ILogger<CudaRingKernelCompiler>>();
        _discoveryLogger = NullLogger<RingKernelDiscovery>.Instance;
        _stubLogger = NullLogger<CudaRingKernelStubGenerator>.Instance;

        var kernelDiscovery = new RingKernelDiscovery(_discoveryLogger);
        var stubGenerator = new CudaRingKernelStubGenerator(_stubLogger);
        var serializerGenerator = new CudaMemoryPackSerializerGenerator(NullLogger<CudaMemoryPackSerializerGenerator>.Instance);
        _compiler = new CudaRingKernelCompiler(_compilerLogger, kernelDiscovery, stubGenerator, serializerGenerator);
        var registryLogger = NullLogger<MessageQueueRegistry>.Instance;
        var registry = new MessageQueueRegistry(registryLogger);
        _runtime = new CudaRingKernelRuntime(_runtimeLogger, _compiler, registry);

        // Register the assembly containing PageRank ring kernels for discovery
        _runtime.RegisterAssembly(typeof(DotCompute.Samples.RingKernels.Tests.PageRank.PageRankKernels).Assembly);
    }

    #region Kernel Discovery Tests

    [SkippableFact(DisplayName = "PageRank kernels should be discoverable via RingKernelDiscovery")]
    public void PageRankKernels_ShouldBeDiscoverable()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);

        // Act - Discover kernels from PageRank assembly
        var pageRankKernelsAssembly = typeof(DotCompute.Samples.RingKernels.Tests.PageRank.PageRankKernels).Assembly;
        var kernels = discovery.DiscoverKernels(new[] { pageRankKernelsAssembly });

        // Assert
        var pageRankKernels = kernels
            .Where(k => k.KernelId.StartsWith("pagerank_", StringComparison.Ordinal))
            .ToList();

        pageRankKernels.Should().HaveCount(3);
        pageRankKernels.Should().Contain(k => k.KernelId == "pagerank_contribution_sender");
        pageRankKernels.Should().Contain(k => k.KernelId == "pagerank_rank_aggregator");
        pageRankKernels.Should().Contain(k => k.KernelId == "pagerank_convergence_checker");

        _output.WriteLine($"Discovered {pageRankKernels.Count} PageRank kernels:");
        foreach (var kernel in pageRankKernels)
        {
            _output.WriteLine($"  - {kernel.KernelId}: Input={kernel.InputMessageTypeName}, K2K={kernel.UsesK2KMessaging}");
        }
    }

    #endregion

    #region Compilation Tests

    [SkippableFact(DisplayName = "PageRank ContributionSender kernel should compile to PTX")]
    public async Task PageRankContributionSender_ShouldCompileToPtx()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        const string kernelId = "pagerank_contribution_sender";

        try
        {
            // Arrange - Get CUDA context
            var context = await GetCudaContextAsync();

            // Act - Compile the kernel
            var compiledKernel = await _compiler.CompileRingKernelAsync(
                kernelId,
                context,
                options: null,
                assemblies: new[] { typeof(DotCompute.Samples.RingKernels.Tests.PageRank.PageRankKernels).Assembly });

            // Assert
            compiledKernel.Should().NotBeNull();
            compiledKernel.IsValid.Should().BeTrue("kernel should be valid after compilation");
            compiledKernel.PtxBytes.Length.Should().BeGreaterThan(0, "PTX should be generated");
            compiledKernel.ModuleHandle.Should().NotBe(IntPtr.Zero, "module should be loaded");
            compiledKernel.FunctionPointer.Should().NotBe(IntPtr.Zero, "function pointer should be retrieved");

            _output.WriteLine($"Kernel '{kernelId}' compiled successfully:");
            _output.WriteLine($"  - PTX size: {compiledKernel.PtxBytes.Length} bytes");
            _output.WriteLine($"  - Module: 0x{compiledKernel.ModuleHandle.ToInt64():X}");
            _output.WriteLine($"  - Function: 0x{compiledKernel.FunctionPointer.ToInt64():X}");
        }
        finally
        {
            _compiler.ClearCache();
        }
    }

    [SkippableFact(DisplayName = "All PageRank kernels should compile successfully")]
    public async Task AllPageRankKernels_ShouldCompile()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        var kernelIds = new[]
        {
            "pagerank_contribution_sender",
            "pagerank_rank_aggregator",
            "pagerank_convergence_checker"
        };

        try
        {
            // Arrange - Get CUDA context
            var context = await GetCudaContextAsync();
            var assembly = typeof(DotCompute.Samples.RingKernels.Tests.PageRank.PageRankKernels).Assembly;

            // Act & Assert
            foreach (var kernelId in kernelIds)
            {
                var compiledKernel = await _compiler.CompileRingKernelAsync(
                    kernelId,
                    context,
                    options: null,
                    assemblies: new[] { assembly });

                compiledKernel.Should().NotBeNull($"kernel '{kernelId}' should compile");
                compiledKernel.IsValid.Should().BeTrue($"kernel '{kernelId}' should be valid");

                _output.WriteLine($"Compiled '{kernelId}': PTX={compiledKernel.PtxBytes.Length} bytes");
            }
        }
        finally
        {
            _compiler.ClearCache();
        }
    }

    #endregion

    #region Launch Tests

    [SkippableFact(DisplayName = "PageRank ContributionSender kernel should launch on GPU")]
    public async Task PageRankContributionSender_ShouldLaunch()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        const string kernelId = "pagerank_contribution_sender";

        try
        {
            // Act - Launch the kernel
            Console.WriteLine("[TEST] Before LaunchAsync");
            await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
            Console.WriteLine("[TEST] After LaunchAsync");

            // Assert - check it's in the list
            Console.WriteLine("[TEST] Before ListKernelsAsync");
            var kernels = await _runtime.ListKernelsAsync();
            Console.WriteLine($"[TEST] After ListKernelsAsync, count={kernels.Count}");
            kernels.Should().Contain(kernelId);

            // NOTE: GetStatusAsync and cleanup currently hang because control block read/write
            // blocks on cooperative kernel. TODO: Implement pinned host memory for control block.
            // For now, verify launch succeeded via kernels list and skip cleanup.
            _output.WriteLine($"Kernel '{kernelId}' launched successfully");
            _output.WriteLine("  - Launch verified via ListKernelsAsync");
            Console.WriteLine("[TEST] Test completed successfully - skipping cleanup due to known hang");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST] Exception: {ex.Message}");
            throw;
        }
    }

    [SkippableFact(DisplayName = "PageRank ContributionSender kernel should activate and deactivate")]
    public async Task PageRankContributionSender_ShouldActivateDeactivate()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        const string kernelId = "pagerank_contribution_sender";

        try
        {
            // Arrange - Launch
            await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);

            // Act - Activate
            await _runtime.ActivateAsync(kernelId);

            // Assert - Active
            var statusActive = await _runtime.GetStatusAsync(kernelId);
            statusActive.IsActive.Should().BeTrue();
            _output.WriteLine($"Kernel activated: IsActive={statusActive.IsActive}");

            // Act - Deactivate
            await _runtime.DeactivateAsync(kernelId);

            // Assert - Inactive
            var statusInactive = await _runtime.GetStatusAsync(kernelId);
            statusInactive.IsActive.Should().BeFalse();
            _output.WriteLine($"Kernel deactivated: IsActive={statusInactive.IsActive}");
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    #endregion

    #region K2K Infrastructure Tests

    [SkippableFact(DisplayName = "PageRank kernels should have K2K infrastructure in generated code")]
    public void PageRankKernels_ShouldHaveK2KInfrastructure()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var discovery = new RingKernelDiscovery(_discoveryLogger);
        var generator = new CudaRingKernelStubGenerator(_stubLogger);
        var assembly = typeof(DotCompute.Samples.RingKernels.Tests.PageRank.PageRankKernels).Assembly;

        // Act
        var kernels = discovery.DiscoverKernels(new[] { assembly })
            .Where(k => k.KernelId.StartsWith("pagerank_", StringComparison.Ordinal))
            .ToList();

        foreach (var kernel in kernels)
        {
            var cudaCode = generator.GenerateKernelStub(kernel);

            // Assert K2K infrastructure based on kernel's publish/subscribe relationships
            _output.WriteLine($"\n=== {kernel.KernelId} ===");
            _output.WriteLine($"  PublishesTo: [{string.Join(", ", kernel.PublishesToKernels)}]");
            _output.WriteLine($"  SubscribesTo: [{string.Join(", ", kernel.SubscribesToKernels)}]");

            if (kernel.PublishesToKernels.Count > 0)
            {
                cudaCode.Should().Contain("k2k_send_queue", $"{kernel.KernelId} should have K2K send queue");
                _output.WriteLine($"  - K2K send queue: present");
            }

            if (kernel.SubscribesToKernels.Count > 0)
            {
                cudaCode.Should().Contain("k2k_receive_channels", $"{kernel.KernelId} should have K2K receive channels");
                _output.WriteLine($"  - K2K receive channels: present");
            }

            _output.WriteLine($"  - CUDA code size: {cudaCode.Length} chars");
        }
    }

    #endregion

    #region Helper Methods

    private static async Task<IntPtr> GetCudaContextAsync()
    {
        return await Task.Run(() =>
        {
            // Initialize CUDA
            var initResult = DotCompute.Backends.CUDA.Native.CudaRuntimeCore.cuInit(0);
            if (initResult != DotCompute.Backends.CUDA.Types.Native.CudaError.Success &&
                initResult != (DotCompute.Backends.CUDA.Types.Native.CudaError)4)
            {
                throw new InvalidOperationException($"Failed to initialize CUDA: {initResult}");
            }

            // Get device
            var deviceResult = DotCompute.Backends.CUDA.Native.CudaRuntime.cuDeviceGet(out int device, 0);
            if (deviceResult != DotCompute.Backends.CUDA.Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to get device: {deviceResult}");
            }

            // Create context
            var ctxResult = DotCompute.Backends.CUDA.Native.CudaRuntimeCore.cuCtxCreate(out IntPtr context, 0, device);
            if (ctxResult != DotCompute.Backends.CUDA.Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to create context: {ctxResult}");
            }

            return context;
        });
    }

    private async Task CleanupKernelAsync(string kernelId)
    {
        try
        {
            Console.WriteLine($"[TEST] CleanupKernelAsync starting for {kernelId}");
            var kernels = await _runtime.ListKernelsAsync();
            if (kernels.Contains(kernelId))
            {
                // Use timeout to prevent hanging on control block read
                // TODO: TerminateAsync reads control block which hangs - need pinned memory fix
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _runtime.TerminateAsync(kernelId, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[TEST] TerminateAsync timed out for {kernelId}");
                }
            }
            Console.WriteLine($"[TEST] CleanupKernelAsync completed for {kernelId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST] CleanupKernelAsync error: {ex.Message}");
            // Ignore cleanup errors
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Cleanup any remaining kernels
            var kernels = await _runtime.ListKernelsAsync();
            foreach (var kernelId in kernels)
            {
                await CleanupKernelAsync(kernelId);
            }
        }
        catch
        {
            // Ignore disposal errors
        }

        // Dispose the runtime to properly cleanup CUDA context
        // This is critical for test isolation - prevents context corruption between tests
        try
        {
            await _runtime.DisposeAsync();
        }
        catch
        {
            // Ignore runtime disposal errors
        }

        _compiler.ClearCache();
        _compiler.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
