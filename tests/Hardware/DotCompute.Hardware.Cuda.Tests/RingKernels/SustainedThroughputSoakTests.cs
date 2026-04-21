// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Long-running stability smoke test for the CUDA message queue.
/// </summary>
/// <remarks>
/// <para>
/// Patterned after the sustained-throughput soak in the RustCompute paper: four independent
/// 60-second windows, each with a dedicated producer/consumer thread pair hammering a
/// <see cref="CudaMessageQueue{TestMessage}"/>. For each window we record throughput,
/// p50/p95/p99 dequeue latency, and the total message count; at the end we assert loose
/// stability bounds across the four windows.
/// </para>
/// <para>
/// This is a <i>stability</i> check, not a benchmark. The assertions are deliberately slack
/// so a healthy run passes easily — they exist to catch catastrophic regressions
/// (e.g. a stall, lock contention explosion, or leaking latency) rather than micro-changes.
/// </para>
/// <para>
/// The test is gated on CUDA availability via <see cref="CudaTestGate"/>. It carries the
/// <c>Soak=true</c> trait so it can be excluded from quick CI runs
/// (<c>--filter "Soak!=true"</c>) and included on demand (<c>--filter "Soak=true"</c>).
/// </para>
/// </remarks>
[Trait("Category", "Hardware")]
[Trait("Backend", "CUDA")]
[Trait("Soak", "true")]
public class SustainedThroughputSoakTests
{
    private readonly ITestOutputHelper _output;

    public SustainedThroughputSoakTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Simple unmanaged payload kept deliberately small to isolate queue overhead from copy cost.
    /// Layout: 1× <see cref="int"/> sequence + 7× <see cref="long"/> padding (stable via
    /// <see cref="LayoutKind.Sequential"/> with pack=4). No MemoryPack — this is a stability
    /// probe, not an end-to-end serialization test.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TestMessage
    {
        public int Seq;
#pragma warning disable IDE0044, IDE0051, CS0169 // padding fields are intentional
        private readonly long _pad0;
        private readonly long _pad1;
        private readonly long _pad2;
        private readonly long _pad3;
        private readonly long _pad4;
        private readonly long _pad5;
        private readonly long _pad6;
#pragma warning restore IDE0044, IDE0051, CS0169
    }

    private sealed record WindowStats(
        int Index,
        long MessagesProduced,
        long MessagesConsumed,
        double Throughput,
        double P50LatencyMs,
        double P95LatencyMs,
        double P99LatencyMs);

    [SkippableFact(DisplayName = "Sustained-throughput soak: 4x60s windows remain stable")]
    public async Task SustainedThroughput_FourWindows_RemainStable()
    {
        // Gate on a real CUDA device; compute capability is irrelevant for this stability test.
        if (!CudaTestGate.TryGetCurrentDeviceCapability(out _, out _))
        {
            Skip.If(true, "CUDA device not available — skipping sustained-throughput soak.");
        }

        const int WindowCount = 4;
        var windowDuration = TimeSpan.FromSeconds(60);
        const int QueueCapacity = 16384; // Power of two, generous headroom for producer bursts

        // Loose stability bounds. Calibrated to pass on a healthy CC 8.9 run with room to spare
        // and to catch order-of-magnitude regressions (not micro-changes).
        const double MaxP99LatencyMs = 50.0;
        const double MaxCoefficientOfVariation = 0.25;

        var queue = new CudaMessageQueue<TestMessage>(QueueCapacity, NullLogger<CudaMessageQueue<TestMessage>>.Instance);
        await queue.InitializeAsync().ConfigureAwait(false);

        try
        {
            var windowStats = new List<WindowStats>(WindowCount);

            for (var windowIndex = 0; windowIndex < WindowCount; windowIndex++)
            {
                var stats = await RunWindowAsync(queue, windowIndex, windowDuration).ConfigureAwait(false);
                windowStats.Add(stats);

                _output.WriteLine(
                    $"Window {stats.Index}: produced={stats.MessagesProduced:N0} consumed={stats.MessagesConsumed:N0} " +
                    $"throughput={stats.Throughput:N0} msg/s p50={stats.P50LatencyMs:F2}ms " +
                    $"p95={stats.P95LatencyMs:F2}ms p99={stats.P99LatencyMs:F2}ms");
            }

            // Per-window sanity: every window produced and consumed work.
            foreach (var s in windowStats)
            {
                s.MessagesProduced.Should().BeGreaterThan(0, $"window {s.Index} should have enqueued messages");
                s.MessagesConsumed.Should().BeGreaterThan(0, $"window {s.Index} should have dequeued messages");
            }

            // p99 latency bound — catastrophic-stall detector, deliberately loose.
            var worstP99 = windowStats.Max(s => s.P99LatencyMs);
            worstP99.Should().BeLessThan(
                MaxP99LatencyMs,
                $"the worst p99 dequeue latency across all windows must stay under {MaxP99LatencyMs}ms");

            // Throughput coefficient of variation — detects increasing degradation over time.
            var throughputs = windowStats.Select(s => s.Throughput).ToArray();
            var mean = throughputs.Average();
            var variance = throughputs.Select(t => (t - mean) * (t - mean)).Average();
            var stdDev = Math.Sqrt(variance);
            var cv = mean > 0 ? stdDev / mean : double.PositiveInfinity;

            _output.WriteLine($"Throughput stability: mean={mean:N0} msg/s stddev={stdDev:N0} CV={cv:P1}");

            cv.Should().BeLessThan(
                MaxCoefficientOfVariation,
                $"throughput coefficient of variation across {WindowCount} windows should stay under " +
                $"{MaxCoefficientOfVariation:P0} to indicate stable sustained performance");
        }
        finally
        {
            // CudaMessageQueue<T> implements IAsyncDisposable only.
            await queue.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<WindowStats> RunWindowAsync(
        CudaMessageQueue<TestMessage> queue,
        int windowIndex,
        TimeSpan duration)
    {
        using var cts = new CancellationTokenSource(duration);
        var token = cts.Token;

        long produced = 0;
        long consumed = 0;

        // Latency samples in milliseconds, captured from dequeue timestamps. We cap the sample
        // count to keep memory bounded over a 60s burst even at high rates.
        const int MaxLatencySamples = 200_000;
        var latencies = new List<double>(MaxLatencySamples);
        var latencyLock = new Lock();

        var stopwatch = Stopwatch.StartNew();

        var producer = Task.Run(async () =>
        {
            var seq = 0;
            while (!token.IsCancellationRequested)
            {
                var message = new KernelMessage<TestMessage>
                {
                    SenderId = 0,
                    ReceiverId = 1,
                    Type = MessageType.Data,
                    Payload = new TestMessage { Seq = seq },
                    // Record send time in 100ns ticks so the dequeue path can compute latency.
                    Timestamp = Stopwatch.GetTimestamp(),
                    SequenceNumber = (ulong)seq,
                };

                try
                {
                    var enqueued = await queue.TryEnqueueAsync(message, token).ConfigureAwait(false);
                    if (enqueued)
                    {
                        _ = Interlocked.Increment(ref produced);
                        seq++;
                    }
                    else
                    {
                        // Back off briefly if the queue is full; avoids pegging a core.
                        await Task.Yield();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);

        var consumer = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var message = await queue.TryDequeueAsync(token).ConfigureAwait(false);
                    if (message is not null)
                    {
                        var nowTicks = Stopwatch.GetTimestamp();
                        var elapsedTicks = nowTicks - message.Value.Timestamp;
                        var latencyMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;

                        _ = Interlocked.Increment(ref consumed);

                        lock (latencyLock)
                        {
                            if (latencies.Count < MaxLatencySamples)
                            {
                                latencies.Add(latencyMs);
                            }
                        }
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);

        // Wait for either task to complete or the window to elapse; suppress the cancellation
        // exception — it's the intended stop signal.
        try
        {
            await Task.WhenAll(producer, consumer).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the window timer fires.
        }

        stopwatch.Stop();

        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        var throughput = elapsedSeconds > 0 ? consumed / elapsedSeconds : 0.0;

        double p50, p95, p99;
        lock (latencyLock)
        {
            var sorted = latencies.OrderBy(x => x).ToArray();
            p50 = Percentile(sorted, 0.50);
            p95 = Percentile(sorted, 0.95);
            p99 = Percentile(sorted, 0.99);
        }

        return new WindowStats(
            Index: windowIndex,
            MessagesProduced: produced,
            MessagesConsumed: consumed,
            Throughput: throughput,
            P50LatencyMs: p50,
            P95LatencyMs: p95,
            P99LatencyMs: p99);
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0.0;
        }

        var rank = percentile * (sortedValues.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = rank - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }
}
