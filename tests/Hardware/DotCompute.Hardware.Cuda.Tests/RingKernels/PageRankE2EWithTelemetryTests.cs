// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Core.Messaging;
using DotCompute.Samples.RingKernels.Tests.PageRank;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// End-to-end tests for PageRank ring kernels with telemetry verification.
/// Validates complete pipeline: launch → activate → message processing → telemetry collection.
/// </summary>
[Collection("CUDA Hardware")]
public class PageRankE2EWithTelemetryTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CudaRingKernelRuntime> _runtimeLogger;
    private readonly ILogger<CudaRingKernelCompiler> _compilerLogger;
    private readonly ILogger<RingKernelDiscovery> _discoveryLogger;
    private readonly ILogger<CudaRingKernelStubGenerator> _stubLogger;
    private readonly CudaRingKernelCompiler _compiler;
    private readonly CudaRingKernelRuntime _runtime;
    private readonly List<string> _launchedKernels = new();

    // Force load the PageRank kernels assembly by referencing a type from it
    private static readonly System.Reflection.Assembly PageRankAssembly =
        typeof(PageRankKernels).Assembly;

    public PageRankE2EWithTelemetryTests(ITestOutputHelper output)
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
        _runtime.RegisterAssembly(PageRankAssembly);
    }

    #region Launch and Lifecycle Tests

    [SkippableFact(DisplayName = "E2E: Kernel launch, activate, and deactivate cycle should work")]
    public async Task E2E_Kernel_LaunchActivateDeactivate_ShouldWork()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        const string kernelId = "pagerank_contribution_sender";

        try
        {
            _output.WriteLine("=== E2E Kernel Lifecycle Test ===");
            var stopwatch = Stopwatch.StartNew();

            // Step 1: Launch kernel
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Launching kernel '{kernelId}'...");
            await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
            _launchedKernels.Add(kernelId);
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Kernel launched successfully");

            // Step 2: Verify launch state
            var status = await _runtime.GetStatusAsync(kernelId);
            status.IsLaunched.Should().BeTrue("kernel should be launched");
            status.IsActive.Should().BeFalse("kernel should start inactive");
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Status: Launched={status.IsLaunched}, Active={status.IsActive}");

            // Step 3: Read control block
            var controlBlock = await _runtime.ReadControlBlockAsync(kernelId);
            controlBlock.IsActive.Should().Be(0, "control block IsActive should be 0");
            controlBlock.ShouldTerminate.Should().Be(0, "control block ShouldTerminate should be 0");
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Control block: IsActive={controlBlock.IsActive}, ShouldTerminate={controlBlock.ShouldTerminate}");

            // Step 4: Activate kernel
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Activating kernel...");
            await _runtime.ActivateAsync(kernelId);

            // Step 5: Verify activation
            status = await _runtime.GetStatusAsync(kernelId);
            status.IsActive.Should().BeTrue("kernel should be active after activation");
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Activation verified: IsActive={status.IsActive}");

            controlBlock = await _runtime.ReadControlBlockAsync(kernelId);
            controlBlock.IsActive.Should().Be(1, "control block IsActive should be 1 after activation");
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Control block after activation: IsActive={controlBlock.IsActive}");

            // Step 6: Deactivate kernel
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Deactivating kernel...");
            await _runtime.DeactivateAsync(kernelId);

            // Step 7: Verify deactivation
            status = await _runtime.GetStatusAsync(kernelId);
            status.IsActive.Should().BeFalse("kernel should be inactive after deactivation");
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Deactivation verified: IsActive={status.IsActive}");

            controlBlock = await _runtime.ReadControlBlockAsync(kernelId);
            controlBlock.IsActive.Should().Be(0, "control block IsActive should be 0 after deactivation");

            stopwatch.Stop();
            _output.WriteLine($"\n=== Test completed in {stopwatch.ElapsedMilliseconds}ms ===");
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    #endregion

    #region Telemetry Tests

    [SkippableFact(DisplayName = "E2E: Enable telemetry and verify buffer allocation")]
    public async Task E2E_Telemetry_EnableAndVerifyAllocation()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        const string kernelId = "pagerank_contribution_sender";

        try
        {
            _output.WriteLine("=== E2E Telemetry Allocation Test ===");
            var stopwatch = Stopwatch.StartNew();

            // Step 1: Launch kernel
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Launching kernel...");
            await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
            _launchedKernels.Add(kernelId);

            // Step 2: Enable telemetry
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Enabling telemetry...");
            await _runtime.SetTelemetryEnabledAsync(kernelId, true);
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Telemetry enabled");

            // Step 3: Poll telemetry
            var telemetry = await _runtime.GetTelemetryAsync(kernelId);
            telemetry.Should().NotBeNull("telemetry should be available after enabling");
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Initial telemetry poll:");
            _output.WriteLine($"  - MessagesProcessed: {telemetry.MessagesProcessed}");
            _output.WriteLine($"  - MessagesDropped: {telemetry.MessagesDropped}");
            _output.WriteLine($"  - QueueDepth: {telemetry.QueueDepth}");
            _output.WriteLine($"  - TotalLatencyNanos: {telemetry.TotalLatencyNanos}");
            _output.WriteLine($"  - MinLatencyNanos: {telemetry.MinLatencyNanos}");
            _output.WriteLine($"  - MaxLatencyNanos: {telemetry.MaxLatencyNanos}");

            // Initial values should be zeros
            telemetry.MessagesProcessed.Should().Be(0, "initial messages processed should be 0");
            telemetry.MessagesDropped.Should().Be(0, "initial messages dropped should be 0");

            // Step 4: Reset telemetry
            await _runtime.ResetTelemetryAsync(kernelId);
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Telemetry reset");

            // Step 5: Verify reset
            telemetry = await _runtime.GetTelemetryAsync(kernelId);
            telemetry.MessagesProcessed.Should().Be(0, "messages processed should be 0 after reset");

            stopwatch.Stop();
            _output.WriteLine($"\n=== Test completed in {stopwatch.ElapsedMilliseconds}ms ===");
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    #endregion

    #region Metrics Tests

    [SkippableFact(DisplayName = "E2E: Get metrics for launched kernel")]
    public async Task E2E_Metrics_GetForLaunchedKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        const string kernelId = "pagerank_contribution_sender";

        try
        {
            _output.WriteLine("=== E2E Metrics Test ===");
            var stopwatch = Stopwatch.StartNew();

            // Launch kernel
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Launching kernel...");
            await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
            _launchedKernels.Add(kernelId);

            // Get metrics
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Getting metrics...");
            var metrics = await _runtime.GetMetricsAsync(kernelId);

            metrics.Should().NotBeNull("metrics should be available");
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Kernel Metrics:");
            _output.WriteLine($"  - LaunchCount: {metrics.LaunchCount}");
            _output.WriteLine($"  - MessagesSent: {metrics.MessagesSent}");
            _output.WriteLine($"  - MessagesReceived: {metrics.MessagesReceived}");
            _output.WriteLine($"  - AvgProcessingTimeMs: {metrics.AvgProcessingTimeMs:F3}");
            _output.WriteLine($"  - ThroughputMsgsPerSec: {metrics.ThroughputMsgsPerSec:F2}");
            _output.WriteLine($"  - InputQueueUtilization: {metrics.InputQueueUtilization:P2}");
            _output.WriteLine($"  - OutputQueueUtilization: {metrics.OutputQueueUtilization:P2}");
            _output.WriteLine($"  - GpuUtilizationPercent: {metrics.GpuUtilizationPercent:F2}%");

            metrics.LaunchCount.Should().Be(1, "launch count should be 1");

            stopwatch.Stop();
            _output.WriteLine($"\n=== Test completed in {stopwatch.ElapsedMilliseconds}ms ===");
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    #endregion

    #region Control Block Verification Tests

    [SkippableFact(DisplayName = "E2E: Control block should track kernel state correctly")]
    public async Task E2E_ControlBlock_TrackState()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        const string kernelId = "pagerank_contribution_sender";

        try
        {
            _output.WriteLine("=== E2E Control Block State Test ===");
            var stopwatch = Stopwatch.StartNew();

            // Launch
            await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
            _launchedKernels.Add(kernelId);

            // Initial state
            var cb = await _runtime.ReadControlBlockAsync(kernelId);
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Initial state:");
            _output.WriteLine($"  - IsActive: {cb.IsActive}");
            _output.WriteLine($"  - ShouldTerminate: {cb.ShouldTerminate}");
            _output.WriteLine($"  - HasTerminated: {cb.HasTerminated}");
            _output.WriteLine($"  - MessagesProcessed: {cb.MessagesProcessed}");
            _output.WriteLine($"  - InputQueueHeadPtr: 0x{cb.InputQueueHeadPtr:X}");
            _output.WriteLine($"  - OutputQueueHeadPtr: 0x{cb.OutputQueueHeadPtr:X}");

            cb.IsActive.Should().Be(0);
            cb.ShouldTerminate.Should().Be(0);
            cb.HasTerminated.Should().Be(0);

            // Activate
            await _runtime.ActivateAsync(kernelId);
            cb = await _runtime.ReadControlBlockAsync(kernelId);
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] After activation:");
            _output.WriteLine($"  - IsActive: {cb.IsActive}");
            cb.IsActive.Should().Be(1);

            // Deactivate
            await _runtime.DeactivateAsync(kernelId);
            cb = await _runtime.ReadControlBlockAsync(kernelId);
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] After deactivation:");
            _output.WriteLine($"  - IsActive: {cb.IsActive}");
            cb.IsActive.Should().Be(0);

            stopwatch.Stop();
            _output.WriteLine($"\n=== Test completed in {stopwatch.ElapsedMilliseconds}ms ===");
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    #endregion

    #region Multi-Kernel Tests

    [SkippableFact(DisplayName = "E2E: Launch multiple PageRank kernels")]
    public async Task E2E_MultiKernel_Launch()
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
            _output.WriteLine("=== E2E Multi-Kernel Launch Test ===");
            var stopwatch = Stopwatch.StartNew();

            // Launch all kernels
            foreach (var kernelId in kernelIds)
            {
                _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Launching '{kernelId}'...");
                await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
                _launchedKernels.Add(kernelId);
                _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Launched");
            }

            // Verify all launched
            var launchedList = await _runtime.ListKernelsAsync();
            launchedList.Count.Should().Be(3, "all 3 PageRank kernels should be launched");

            foreach (var kernelId in kernelIds)
            {
                launchedList.Should().Contain(kernelId);
            }

            _output.WriteLine($"\n[{stopwatch.ElapsedMilliseconds}ms] Launched kernels: {string.Join(", ", launchedList)}");

            // Get status for each
            foreach (var kernelId in kernelIds)
            {
                var status = await _runtime.GetStatusAsync(kernelId);
                _output.WriteLine($"\n{kernelId}:");
                _output.WriteLine($"  - IsLaunched: {status.IsLaunched}");
                _output.WriteLine($"  - IsActive: {status.IsActive}");
                _output.WriteLine($"  - Grid: {status.GridSize}");
                _output.WriteLine($"  - Block: {status.BlockSize}");
            }

            stopwatch.Stop();
            _output.WriteLine($"\n=== Test completed in {stopwatch.ElapsedMilliseconds}ms ===");
        }
        finally
        {
            foreach (var kernelId in kernelIds)
            {
                await CleanupKernelAsync(kernelId);
            }
        }
    }

    [SkippableFact(DisplayName = "E2E: Activate multiple kernels and verify state")]
    public async Task E2E_MultiKernel_ActivateAndVerify()
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
            _output.WriteLine("=== E2E Multi-Kernel Activation Test ===");
            var stopwatch = Stopwatch.StartNew();

            // Launch all
            foreach (var kernelId in kernelIds)
            {
                await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
                _launchedKernels.Add(kernelId);
            }
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] All kernels launched");

            // Activate all
            foreach (var kernelId in kernelIds)
            {
                await _runtime.ActivateAsync(kernelId);
            }
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] All kernels activated");

            // Verify all active
            foreach (var kernelId in kernelIds)
            {
                var status = await _runtime.GetStatusAsync(kernelId);
                status.IsActive.Should().BeTrue($"kernel '{kernelId}' should be active");
                _output.WriteLine($"  - {kernelId}: Active={status.IsActive}");
            }

            // Deactivate all
            foreach (var kernelId in kernelIds)
            {
                await _runtime.DeactivateAsync(kernelId);
            }
            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] All kernels deactivated");

            // Verify all inactive
            foreach (var kernelId in kernelIds)
            {
                var status = await _runtime.GetStatusAsync(kernelId);
                status.IsActive.Should().BeFalse($"kernel '{kernelId}' should be inactive");
            }

            stopwatch.Stop();
            _output.WriteLine($"\n=== Test completed in {stopwatch.ElapsedMilliseconds}ms ===");
        }
        finally
        {
            foreach (var kernelId in kernelIds)
            {
                await CleanupKernelAsync(kernelId);
            }
        }
    }

    #endregion

    #region K2K Infrastructure Verification

    [SkippableFact(DisplayName = "E2E: Verify K2K message routing infrastructure")]
    public async Task E2E_K2K_VerifyMessageRoutingInfrastructure()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        try
        {
            _output.WriteLine("=== E2E K2K Infrastructure Test ===");
            var stopwatch = Stopwatch.StartNew();

            var discovery = new RingKernelDiscovery(_discoveryLogger);
            var assembly = typeof(PageRankKernels).Assembly;
            var kernels = discovery.DiscoverKernels(new[] { assembly })
                .Where(k => k.KernelId.StartsWith("pagerank_", StringComparison.Ordinal))
                .ToDictionary(k => k.KernelId);

            _output.WriteLine($"[{stopwatch.ElapsedMilliseconds}ms] Discovered {kernels.Count} PageRank kernels\n");

            // Verify K2K routing graph
            _output.WriteLine("K2K Message Routing Graph:");
            _output.WriteLine("=".PadRight(50, '='));

            // ContributionSender → RankAggregator
            var sender = kernels["pagerank_contribution_sender"];
            sender.PublishesToKernels.Should().Contain("pagerank_rank_aggregator");
            _output.WriteLine($"  {sender.KernelId}");
            _output.WriteLine($"    └─→ publishes to: [{string.Join(", ", sender.PublishesToKernels)}]");

            // RankAggregator ← ContributionSender, → ConvergenceChecker
            var aggregator = kernels["pagerank_rank_aggregator"];
            aggregator.SubscribesToKernels.Should().Contain("pagerank_contribution_sender");
            aggregator.PublishesToKernels.Should().Contain("pagerank_convergence_checker");
            _output.WriteLine($"  {aggregator.KernelId}");
            _output.WriteLine($"    ├─← subscribes to: [{string.Join(", ", aggregator.SubscribesToKernels)}]");
            _output.WriteLine($"    └─→ publishes to: [{string.Join(", ", aggregator.PublishesToKernels)}]");

            // ConvergenceChecker ← RankAggregator
            var checker = kernels["pagerank_convergence_checker"];
            checker.SubscribesToKernels.Should().Contain("pagerank_rank_aggregator");
            _output.WriteLine($"  {checker.KernelId}");
            _output.WriteLine($"    └─← subscribes to: [{string.Join(", ", checker.SubscribesToKernels)}]");

            _output.WriteLine("=".PadRight(50, '='));

            // Verify message types
            _output.WriteLine("\nMessage Types:");
            _output.WriteLine($"  - ContributionSender input: {sender.InputMessageTypeName}");
            _output.WriteLine($"  - RankAggregator input: {aggregator.InputMessageTypeName}");
            _output.WriteLine($"  - ConvergenceChecker input: {checker.InputMessageTypeName}");

            stopwatch.Stop();
            _output.WriteLine($"\n=== Test completed in {stopwatch.ElapsedMilliseconds}ms ===");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Comprehensive E2E System Test

    [SkippableFact(DisplayName = "E2E SYSTEM TEST: Full PageRank pipeline with telemetry")]
    public async Task E2E_SystemTest_FullPipelineWithTelemetry()
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
            _output.WriteLine("╔" + "═".PadRight(60, '═') + "╗");
            _output.WriteLine("║ E2E SYSTEM TEST: Full PageRank Pipeline with Telemetry    ║");
            _output.WriteLine("╚" + "═".PadRight(60, '═') + "╝\n");

            var stopwatch = Stopwatch.StartNew();
            var timings = new Dictionary<string, long>();

            // Phase 1: Launch all kernels
            _output.WriteLine("═══ PHASE 1: KERNEL LAUNCH ═══");
            var launchStart = stopwatch.ElapsedMilliseconds;
            foreach (var kernelId in kernelIds)
            {
                var kernelStart = stopwatch.ElapsedMilliseconds;
                await _runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
                _launchedKernels.Add(kernelId);
                var kernelTime = stopwatch.ElapsedMilliseconds - kernelStart;
                _output.WriteLine($"  ✓ Launched '{kernelId}' ({kernelTime}ms)");
            }
            timings["Phase1_Launch"] = stopwatch.ElapsedMilliseconds - launchStart;
            _output.WriteLine($"  Total launch time: {timings["Phase1_Launch"]}ms\n");

            // Phase 2: Enable telemetry on all kernels
            _output.WriteLine("═══ PHASE 2: ENABLE TELEMETRY ═══");
            var telemetryStart = stopwatch.ElapsedMilliseconds;
            foreach (var kernelId in kernelIds)
            {
                await _runtime.SetTelemetryEnabledAsync(kernelId, true);
                _output.WriteLine($"  ✓ Telemetry enabled for '{kernelId}'");
            }
            timings["Phase2_Telemetry"] = stopwatch.ElapsedMilliseconds - telemetryStart;
            _output.WriteLine($"  Total telemetry setup: {timings["Phase2_Telemetry"]}ms\n");

            // Phase 3: Activate all kernels
            _output.WriteLine("═══ PHASE 3: KERNEL ACTIVATION ═══");
            var activateStart = stopwatch.ElapsedMilliseconds;
            foreach (var kernelId in kernelIds)
            {
                await _runtime.ActivateAsync(kernelId);
                _output.WriteLine($"  ✓ Activated '{kernelId}'");
            }
            timings["Phase3_Activate"] = stopwatch.ElapsedMilliseconds - activateStart;
            _output.WriteLine($"  Total activation time: {timings["Phase3_Activate"]}ms\n");

            // Phase 4: Verify all kernels are running
            _output.WriteLine("═══ PHASE 4: VERIFICATION ═══");
            var verifyStart = stopwatch.ElapsedMilliseconds;
            foreach (var kernelId in kernelIds)
            {
                var status = await _runtime.GetStatusAsync(kernelId);
                var controlBlock = await _runtime.ReadControlBlockAsync(kernelId);

                status.IsLaunched.Should().BeTrue($"'{kernelId}' should be launched");
                status.IsActive.Should().BeTrue($"'{kernelId}' should be active");
                controlBlock.IsActive.Should().Be(1, $"'{kernelId}' control block should show active");

                _output.WriteLine($"  {kernelId}:");
                _output.WriteLine($"    Status: Launched={status.IsLaunched}, Active={status.IsActive}");
                _output.WriteLine($"    Grid: {status.GridSize}, Block: {status.BlockSize}");
                _output.WriteLine($"    Control Block: IsActive={controlBlock.IsActive}, Terminated={controlBlock.HasTerminated}");
            }
            timings["Phase4_Verify"] = stopwatch.ElapsedMilliseconds - verifyStart;
            _output.WriteLine($"  Verification time: {timings["Phase4_Verify"]}ms\n");

            // Phase 5: Collect telemetry from all kernels
            _output.WriteLine("═══ PHASE 5: TELEMETRY COLLECTION ═══");
            var collectStart = stopwatch.ElapsedMilliseconds;
            foreach (var kernelId in kernelIds)
            {
                var telemetry = await _runtime.GetTelemetryAsync(kernelId);
                var metrics = await _runtime.GetMetricsAsync(kernelId);

                _output.WriteLine($"  {kernelId}:");
                _output.WriteLine($"    Telemetry:");
                _output.WriteLine($"      - MessagesProcessed: {telemetry.MessagesProcessed}");
                _output.WriteLine($"      - MessagesDropped: {telemetry.MessagesDropped}");
                _output.WriteLine($"      - QueueDepth: {telemetry.QueueDepth}");
                _output.WriteLine($"      - TotalLatencyNs: {telemetry.TotalLatencyNanos}");
                if (telemetry.MessagesProcessed > 0)
                {
                    var avgLatencyNs = telemetry.TotalLatencyNanos / telemetry.MessagesProcessed;
                    _output.WriteLine($"      - AvgLatencyNs: {avgLatencyNs}");
                }
                _output.WriteLine($"    Metrics:");
                _output.WriteLine($"      - ThroughputMsgsPerSec: {metrics.ThroughputMsgsPerSec:F2}");
                _output.WriteLine($"      - InputQueueUtilization: {metrics.InputQueueUtilization:P2}");
            }
            timings["Phase5_Collect"] = stopwatch.ElapsedMilliseconds - collectStart;
            _output.WriteLine($"  Collection time: {timings["Phase5_Collect"]}ms\n");

            // Phase 6: Deactivate all kernels
            _output.WriteLine("═══ PHASE 6: DEACTIVATION ═══");
            var deactivateStart = stopwatch.ElapsedMilliseconds;
            foreach (var kernelId in kernelIds)
            {
                await _runtime.DeactivateAsync(kernelId);
                _output.WriteLine($"  ✓ Deactivated '{kernelId}'");
            }
            timings["Phase6_Deactivate"] = stopwatch.ElapsedMilliseconds - deactivateStart;
            _output.WriteLine($"  Deactivation time: {timings["Phase6_Deactivate"]}ms\n");

            // Final verification
            _output.WriteLine("═══ FINAL VERIFICATION ═══");
            foreach (var kernelId in kernelIds)
            {
                var status = await _runtime.GetStatusAsync(kernelId);
                status.IsActive.Should().BeFalse($"'{kernelId}' should be inactive after deactivation");
                _output.WriteLine($"  ✓ '{kernelId}' inactive: {!status.IsActive}");
            }

            stopwatch.Stop();

            // Summary
            _output.WriteLine("\n" + "═".PadRight(62, '═'));
            _output.WriteLine("                    SYSTEM TEST SUMMARY");
            _output.WriteLine("═".PadRight(62, '═'));
            _output.WriteLine($"  Total kernels: {kernelIds.Length}");
            _output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Phase timings:");
            foreach (var timing in timings)
            {
                _output.WriteLine($"    - {timing.Key}: {timing.Value}ms");
            }
            _output.WriteLine("═".PadRight(62, '═'));
            _output.WriteLine("                    ✓ ALL SYSTEMS OPERATIONAL");
            _output.WriteLine("═".PadRight(62, '═'));
        }
        finally
        {
            foreach (var kernelId in kernelIds)
            {
                await CleanupKernelAsync(kernelId);
            }
        }
    }

    #endregion

    #region Helper Methods

    private async Task CleanupKernelAsync(string kernelId)
    {
        try
        {
            var kernels = await _runtime.ListKernelsAsync();
            if (kernels.Contains(kernelId))
            {
                await _runtime.TerminateAsync(kernelId);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            _launchedKernels.Remove(kernelId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Cleanup any remaining kernels
            foreach (var kernelId in _launchedKernels.ToList())
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
        GC.SuppressFinalize(this);
    }

    #endregion
}
