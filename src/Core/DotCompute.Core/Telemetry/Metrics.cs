// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Stub metrics class for Prometheus integration.
/// TODO: Replace with actual Prometheus.NET library integration.
/// </summary>
internal static class PrometheusMetricsStub
{
    public static readonly object DefaultRegistry = new();

    public static ICounter CreateCounter(string name, string help, params string[] labelNames) => new StubCounter();

    public static IHistogram CreateHistogram(string name, string help, HistogramConfiguration? config = null, params string[] labelNames) => new StubHistogram();


    public static IGauge CreateGauge(string name, string help, params string[] labelNames) => new StubGauge();
}

internal interface ICounter
{
    public ICounter WithLabels(params string[] labels);
    public void Inc(double increment = 1);
}

internal interface IHistogram
{
    public IHistogram WithLabels(params string[] labels);
    public void Observe(double value);
}

internal interface IGauge
{
    public IGauge WithLabels(params string[] labels);
    public void Set(double value);
    public void Inc(double increment = 1);
    public void Dec(double decrement = 1);
}

internal class StubCounter : ICounter
{
    public ICounter WithLabels(params string[] labels) => this;
    public void Inc(double increment = 1) { }
}

internal class StubHistogram : IHistogram
{
    public IHistogram WithLabels(params string[] labels) => this;
    public void Observe(double value) { }
}

internal class StubGauge : IGauge
{
    public IGauge WithLabels(params string[] labels) => this;
    public void Set(double value) { }
    public void Inc(double increment = 1) { }
    public void Dec(double decrement = 1) { }
}

internal class HistogramConfiguration
{
    public double[]? Buckets { get; set; }
}