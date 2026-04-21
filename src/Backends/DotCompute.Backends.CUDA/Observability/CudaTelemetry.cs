// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Backends.CUDA.Observability;

/// <summary>
/// Central OpenTelemetry <see cref="ActivitySource"/> for the DotCompute CUDA backend.
/// </summary>
/// <remarks>
/// <para>
/// Downstream consumers (e.g. Orleans.GpuBridge) subscribe to the source name
/// <c>DotCompute.Backends.CUDA</c> and receive spans for high-value entry points:
/// context activation, P2P enablement, and kernel launch.
/// </para>
/// <para>
/// Tag conventions follow the RustCompute OpenTelemetry layout to keep dashboards
/// portable between the Rust and .NET backends:
/// <list type="bullet">
/// <item><description><c>kernel_id</c> — logical identifier for the kernel being launched.</description></item>
/// <item><description><c>device_id</c> — CUDA device index on which the operation runs.</description></item>
/// <item><description><c>phase</c> — optional sub-phase label (e.g. "compile", "launch", "sync").</description></item>
/// <item><description><c>peer_device_id</c> — for P2P operations, the remote device index.</description></item>
/// </list>
/// </para>
/// <para>
/// No external listener registration is required — <see cref="ActivitySource"/> is part of
/// the BCL. Spans are no-ops when no <see cref="ActivityListener"/> is subscribed, so the
/// overhead on an un-observed run is a single sampling check per call.
/// </para>
/// </remarks>
public static class CudaTelemetry
{
    /// <summary>
    /// Shared <see cref="ActivitySource"/> for the CUDA backend. Version is kept in step with
    /// the package version so downstream observability tooling can correlate spans to a build.
    /// </summary>
    public static readonly ActivitySource Source = new("DotCompute.Backends.CUDA", "0.6.2");

    /// <summary>
    /// Starts a CUDA backend <see cref="Activity"/> with the standard tag set.
    /// </summary>
    /// <param name="name">The span name (e.g. <c>cuda.kernel.launch</c>).</param>
    /// <param name="kernelId">Optional logical kernel identifier.</param>
    /// <param name="deviceId">Optional CUDA device index.</param>
    /// <param name="phase">Optional phase label (e.g. "compile", "launch").</param>
    /// <returns>
    /// The newly started <see cref="Activity"/>, or <c>null</c> when no listener has subscribed
    /// to <see cref="Source"/> (in which case the call is a cheap no-op).
    /// </returns>
    /// <remarks>
    /// Callers should dispose the returned activity (e.g. via <c>using</c>) to stop the span.
    /// All tag arguments are optional; null values are not attached. The activity kind is
    /// <see cref="ActivityKind.Internal"/>, matching the RustCompute convention for in-process
    /// GPU work.
    /// </remarks>
    public static Activity? StartActivity(
        string name,
        string? kernelId = null,
        int? deviceId = null,
        string? phase = null)
    {
        var activity = Source.StartActivity(name, ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        if (kernelId is not null)
        {
            _ = activity.SetTag("kernel_id", kernelId);
        }

        if (deviceId is not null)
        {
            _ = activity.SetTag("device_id", deviceId.Value);
        }

        if (phase is not null)
        {
            _ = activity.SetTag("phase", phase);
        }

        return activity;
    }
}
