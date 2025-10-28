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
    /// <summary>
    /// Creates a new counter.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="help">The help.</param>
    /// <param name="labelNames">The label names.</param>
    /// <returns>The created counter.</returns>

    public static ICounter CreateCounter(string name, string help, params string[] labelNames) => new StubCounter();
    /// <summary>
    /// Creates a new histogram.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="help">The help.</param>
    /// <param name="config">The config.</param>
    /// <param name="labelNames">The label names.</param>
    /// <returns>The created histogram.</returns>

    public static IHistogram CreateHistogram(string name, string help, HistogramConfiguration? config = null, params string[] labelNames) => new StubHistogram();
    /// <summary>
    /// Creates a new gauge.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="help">The help.</param>
    /// <param name="labelNames">The label names.</param>
    /// <returns>The created gauge.</returns>


    public static IGauge CreateGauge(string name, string help, params string[] labelNames) => new StubGauge();
}

internal interface ICounter
{
    /// <summary>
    /// Gets with labels.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The result of the operation.</returns>
    public ICounter WithLabels(params string[] labels);
    /// <summary>
    /// Performs inc.
    /// </summary>
    /// <param name="increment">The increment.</param>
    public void Inc(double increment = 1);
}

internal interface IHistogram
{
    /// <summary>
    /// Gets with labels.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The result of the operation.</returns>
    public IHistogram WithLabels(params string[] labels);
    /// <summary>
    /// Performs observe.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Observe(double value);
}

internal interface IGauge
{
    /// <summary>
    /// Gets with labels.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The result of the operation.</returns>
    public IGauge WithLabels(params string[] labels);
    /// <summary>
    /// Sets the .
    /// </summary>
    /// <param name="value">The value.</param>
    public void Set(double value);
    /// <summary>
    /// Performs inc.
    /// </summary>
    /// <param name="increment">The increment.</param>
    public void Inc(double increment = 1);
    /// <summary>
    /// Performs dec.
    /// </summary>
    /// <param name="decrement">The decrement.</param>
    public void Dec(double decrement = 1);
}

internal class StubCounter : ICounter
{
    /// <summary>
    /// Gets with labels.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The result of the operation.</returns>
    public ICounter WithLabels(params string[] labels) => this;
    /// <summary>
    /// Performs inc.
    /// </summary>
    /// <param name="increment">The increment.</param>
    public void Inc(double increment = 1) { }
}

internal class StubHistogram : IHistogram
{
    /// <summary>
    /// Gets with labels.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The result of the operation.</returns>
    public IHistogram WithLabels(params string[] labels) => this;
    /// <summary>
    /// Performs observe.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Observe(double value) { }
}

internal class StubGauge : IGauge
{
    /// <summary>
    /// Gets with labels.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The result of the operation.</returns>
    public IGauge WithLabels(params string[] labels) => this;
    /// <summary>
    /// Sets the .
    /// </summary>
    /// <param name="value">The value.</param>
    public void Set(double value) { }
    /// <summary>
    /// Performs inc.
    /// </summary>
    /// <param name="increment">The increment.</param>
    public void Inc(double increment = 1) { }
    /// <summary>
    /// Performs dec.
    /// </summary>
    /// <param name="decrement">The decrement.</param>
    public void Dec(double decrement = 1) { }
}

internal class HistogramConfiguration
{
    /// <summary>
    /// Gets or sets the buckets.
    /// </summary>
    /// <value>The buckets.</value>
    public double[]? Buckets { get; set; }
}