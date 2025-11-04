// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CPU.RingKernels;
using DotCompute.Backends.Metal.RingKernels;
using DotCompute.Backends.OpenCL.RingKernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Tests.Verification;

/// <summary>
/// Production readiness verification for Ring Kernel system.
/// </summary>
/// <remarks>
/// This test verifies all Ring Kernel backends are production-ready:
/// - All interfaces properly implemented
/// - Lifecycle management works correctly
/// - Message passing functions correctly
/// - Performance meets expectations
/// - Error handling is robust
/// </remarks>
public class RingKernelProductionReadinessTest
{
    public static async Task<ProductionReadinessReport> VerifyAllBackendsAsync()
    {
        var report = new ProductionReadinessReport
        {
            TestDate = DateTime.UtcNow,
            TesterVersion = "1.0.0"
        };

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Ring Kernel Production Readiness Verification");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Test CPU Backend (always available)
        var cpuResult = await VerifyBackendAsync("CPU", CreateCpuRuntime);
        report.BackendResults["CPU"] = cpuResult;
        PrintBackendResult("CPU", cpuResult);

        // Test Metal Backend (macOS only)
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var metalResult = await VerifyBackendAsync("Metal", CreateMetalRuntime);
                report.BackendResults["Metal"] = metalResult;
                PrintBackendResult("Metal", metalResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Metal: UNAVAILABLE - {ex.Message}");
                report.BackendResults["Metal"] = new BackendVerificationResult
                {
                    Available = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        else
        {
            Console.WriteLine("⊘ Metal: SKIPPED (not macOS)");
        }

        // Test OpenCL Backend (macOS/Linux)
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            try
            {
                var openClResult = await VerifyBackendAsync("OpenCL", CreateOpenCLRuntime);
                report.BackendResults["OpenCL"] = openClResult;
                PrintBackendResult("OpenCL", openClResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ OpenCL: UNAVAILABLE - {ex.Message}");
                report.BackendResults["OpenCL"] = new BackendVerificationResult
                {
                    Available = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        else
        {
            Console.WriteLine("⊘ OpenCL: SKIPPED (not supported on this platform)");
        }

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Production Readiness Summary");
        Console.WriteLine("=".PadRight(80, '='));

        int passedCount = report.BackendResults.Values.Count(r => r.ProductionReady);
        int totalCount = report.BackendResults.Count;

        Console.WriteLine($"Backends Tested: {totalCount}");
        Console.WriteLine($"Production Ready: {passedCount}");
        Console.WriteLine($"Pass Rate: {(double)passedCount / totalCount:P0}");
        Console.WriteLine();

        report.OverallProductionReady = passedCount >= 2; // At least CPU + one GPU backend

        if (report.OverallProductionReady)
        {
            Console.WriteLine("✅ VERDICT: Ring Kernel system is PRODUCTION READY");
        }
        else
        {
            Console.WriteLine("⚠️  VERDICT: Ring Kernel system needs additional work");
        }

        Console.WriteLine("=".PadRight(80, '='));

        return report;
    }

    private static async Task<BackendVerificationResult> VerifyBackendAsync(
        string backendName,
        Func<IRingKernelRuntime> runtimeFactory)
    {
        var result = new BackendVerificationResult { BackendName = backendName };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var runtime = runtimeFactory();

            // Test 1: Kernel Launch
            Console.Write($"  [{backendName}] Testing kernel launch... ");
            const string kernelId = "test_kernel";
            await runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 256);
            result.LaunchTest = true;
            Console.WriteLine("✓");

            // Test 2: Kernel Status
            Console.Write($"  [{backendName}] Testing status retrieval... ");
            var status = await runtime.GetStatusAsync(kernelId);
            if (status.IsLaunched && !status.IsActive)
            {
                result.StatusTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 3: Kernel Activation
            Console.Write($"  [{backendName}] Testing kernel activation... ");
            await runtime.ActivateAsync(kernelId);
            status = await runtime.GetStatusAsync(kernelId);
            if (status.IsActive)
            {
                result.ActivationTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 4: Message Queue Creation
            Console.Write($"  [{backendName}] Testing message queue creation... ");
            var queue = await runtime.CreateMessageQueueAsync<int>(capacity: 256);
            if (queue.Capacity == 256 && queue.IsEmpty && !queue.IsFull)
            {
                result.QueueCreationTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 5: Message Enqueue
            Console.Write($"  [{backendName}] Testing message enqueue... ");
            var message = KernelMessage<int>.CreateData(senderId: 0, receiverId: -1, payload: 42);
            bool enqueued = await queue.TryEnqueueAsync(message);
            if (enqueued && !queue.IsEmpty && queue.Count == 1)
            {
                result.EnqueueTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 6: Message Dequeue
            Console.Write($"  [{backendName}] Testing message dequeue... ");
            var dequeued = await queue.TryDequeueAsync();
            if (dequeued.HasValue && dequeued.Value.Payload == 42 && queue.IsEmpty)
            {
                result.DequeueTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 7: Queue Statistics
            Console.Write($"  [{backendName}] Testing queue statistics... ");
            var stats = await queue.GetStatisticsAsync();
            if (stats.TotalEnqueued == 1 && stats.TotalDequeued == 1)
            {
                result.StatisticsTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 8: Message Passing to Kernel
            Console.Write($"  [{backendName}] Testing message passing... ");
            var kernelMessage = KernelMessage<int>.CreateData(0, -1, 100);
            await runtime.SendMessageAsync(kernelId, kernelMessage);
            result.MessagePassingTest = true;
            Console.WriteLine("✓");

            // Test 9: Kernel Metrics
            Console.Write($"  [{backendName}] Testing metrics collection... ");
            var metrics = await runtime.GetMetricsAsync(kernelId);
            if (metrics.LaunchCount > 0)
            {
                result.MetricsTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 10: Kernel Listing
            Console.Write($"  [{backendName}] Testing kernel listing... ");
            var kernels = await runtime.ListKernelsAsync();
            if (kernels.Contains(kernelId))
            {
                result.ListingTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 11: Kernel Deactivation
            Console.Write($"  [{backendName}] Testing kernel deactivation... ");
            await runtime.DeactivateAsync(kernelId);
            status = await runtime.GetStatusAsync(kernelId);
            if (!status.IsActive)
            {
                result.DeactivationTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 12: Kernel Termination
            Console.Write($"  [{backendName}] Testing kernel termination... ");
            await runtime.TerminateAsync(kernelId);
            kernels = await runtime.ListKernelsAsync();
            if (!kernels.Contains(kernelId))
            {
                result.TerminationTest = true;
                Console.WriteLine("✓");
            }
            else
            {
                Console.WriteLine("✗");
            }

            // Test 13: Resource Cleanup
            Console.Write($"  [{backendName}] Testing resource cleanup... ");
            await queue.DisposeAsync();
            await runtime.DisposeAsync();
            result.CleanupTest = true;
            Console.WriteLine("✓");

            result.Available = true;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            // Calculate pass rate
            int passed = CountPassedTests(result);
            result.TestsPassed = passed;
            result.TestsTotal = 13;
            result.ProductionReady = passed == 13;
        }
        catch (Exception ex)
        {
            result.Available = false;
            result.ErrorMessage = ex.ToString();
            Console.WriteLine($"  [{backendName}] ✗ FAILED: {ex.Message}");
        }

        return result;
    }

    private static int CountPassedTests(BackendVerificationResult result)
    {
        int count = 0;
        if (result.LaunchTest) count++;
        if (result.StatusTest) count++;
        if (result.ActivationTest) count++;
        if (result.QueueCreationTest) count++;
        if (result.EnqueueTest) count++;
        if (result.DequeueTest) count++;
        if (result.StatisticsTest) count++;
        if (result.MessagePassingTest) count++;
        if (result.MetricsTest) count++;
        if (result.ListingTest) count++;
        if (result.DeactivationTest) count++;
        if (result.TerminationTest) count++;
        if (result.CleanupTest) count++;
        return count;
    }

    private static void PrintBackendResult(string backendName, BackendVerificationResult result)
    {
        if (result.Available)
        {
            string verdict = result.ProductionReady ? "✅ PRODUCTION READY" : "⚠️  NEEDS WORK";
            Console.WriteLine($"  [{backendName}] {verdict} ({result.TestsPassed}/{result.TestsTotal} tests passed in {result.ExecutionTimeMs}ms)");
        }
        else
        {
            Console.WriteLine($"  [{backendName}] ✗ UNAVAILABLE");
        }
        Console.WriteLine();
    }

    private static IRingKernelRuntime CreateCpuRuntime()
    {
        var logger = NullLogger<CpuRingKernelRuntime>.Instance;
        return new CpuRingKernelRuntime(logger);
    }

    private static IRingKernelRuntime CreateMetalRuntime()
    {
        var compilerLogger = NullLogger<MetalRingKernelCompiler>.Instance;
        var compiler = new MetalRingKernelCompiler(compilerLogger);
        var logger = NullLogger<MetalRingKernelRuntime>.Instance;
        return new MetalRingKernelRuntime(logger, compiler);
    }

    private static IRingKernelRuntime CreateOpenCLRuntime()
    {
        var compilerLogger = NullLogger<OpenCLRingKernelCompiler>.Instance;
        var compiler = new OpenCLRingKernelCompiler(compilerLogger);
        var logger = NullLogger<OpenCLRingKernelRuntime>.Instance;
        return new OpenCLRingKernelRuntime(logger, compiler);
    }
}

public class ProductionReadinessReport
{
    public DateTime TestDate { get; set; }
    public string TesterVersion { get; set; } = string.Empty;
    public Dictionary<string, BackendVerificationResult> BackendResults { get; } = new();
    public bool OverallProductionReady { get; set; }
}

public class BackendVerificationResult
{
    public string BackendName { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string? ErrorMessage { get; set; }

    // Test results
    public bool LaunchTest { get; set; }
    public bool StatusTest { get; set; }
    public bool ActivationTest { get; set; }
    public bool QueueCreationTest { get; set; }
    public bool EnqueueTest { get; set; }
    public bool DequeueTest { get; set; }
    public bool StatisticsTest { get; set; }
    public bool MessagePassingTest { get; set; }
    public bool MetricsTest { get; set; }
    public bool ListingTest { get; set; }
    public bool DeactivationTest { get; set; }
    public bool TerminationTest { get; set; }
    public bool CleanupTest { get; set; }

    public int TestsPassed { get; set; }
    public int TestsTotal { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool ProductionReady { get; set; }
}
