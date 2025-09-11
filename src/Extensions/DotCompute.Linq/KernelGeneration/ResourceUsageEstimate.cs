// <copyright file="ResourceUsageEstimate.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace DotCompute.Linq.KernelGeneration;

/// <summary>
/// Estimates resource usage for kernel execution to help with optimization and scheduling.
/// Provides detailed analysis of memory, compute, and bandwidth requirements.
/// </summary>
public sealed class ResourceUsageEstimate : IEquatable<ResourceUsageEstimate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceUsageEstimate"/> class.
    /// </summary>
    public ResourceUsageEstimate()
    {
        MemoryDetails = new Dictionary<string, long>();
        ComputeDetails = new Dictionary<string, double>();
        BandwidthDetails = new Dictionary<string, double>();
        CustomMetrics = new Dictionary<string, object>();
        EstimatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets or sets the total memory usage in bytes.
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the global memory usage in bytes.
    /// </summary>
    public long GlobalMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the shared memory usage in bytes.
    /// </summary>
    public long SharedMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the local memory usage in bytes.
    /// </summary>
    public long LocalMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the constant memory usage in bytes.
    /// </summary>
    public long ConstantMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the register usage per thread.
    /// </summary>
    public int RegistersPerThread { get; set; }

    /// <summary>
    /// Gets or sets the estimated number of floating-point operations.
    /// </summary>
    public double EstimatedFlops { get; set; }

    /// <summary>
    /// Gets or sets the estimated number of integer operations.
    /// </summary>
    public double EstimatedIntOps { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory bandwidth usage in GB/s.
    /// </summary>
    public double MemoryBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets the estimated compute intensity (FLOPS per byte).
    /// </summary>
    public double ComputeIntensity { get; set; }

    /// <summary>
    /// Gets or sets the estimated execution time in milliseconds.
    /// </summary>
    public double EstimatedExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the expected occupancy percentage (0-100).
    /// </summary>
    public double ExpectedOccupancy { get; set; }

    /// <summary>
    /// Gets or sets the estimated power consumption in watts.
    /// </summary>
    public double EstimatedPowerWatts { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of the estimate (0-1).
    /// </summary>
    public double ConfidenceLevel { get; set; } = 0.8;

    /// <summary>
    /// Gets detailed memory usage breakdown by category.
    /// </summary>
    public Dictionary<string, long> MemoryDetails { get; }

    /// <summary>
    /// Gets detailed compute operation breakdown.
    /// </summary>
    public Dictionary<string, double> ComputeDetails { get; }

    /// <summary>
    /// Gets detailed bandwidth usage breakdown.
    /// </summary>
    public Dictionary<string, double> BandwidthDetails { get; }

    /// <summary>
    /// Gets custom metrics for specific backends or use cases.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; }

    /// <summary>
    /// Gets the timestamp when this estimate was calculated.
    /// </summary>
    public DateTimeOffset EstimatedAt { get; }

    /// <summary>
    /// Gets or sets the hardware configuration used for the estimate.
    /// </summary>
    public string? HardwareProfile { get; set; }

    /// <summary>
    /// Gets or sets the data size that this estimate is based on.
    /// </summary>
    public long InputDataSize { get; set; }

    /// <summary>
    /// Gets or sets whether this is a worst-case estimate.
    /// </summary>
    public bool IsWorstCase { get; set; }

    /// <summary>
    /// Adds a memory usage detail.
    /// </summary>
    /// <param name="category">The memory category.</param>
    /// <param name="bytes">The number of bytes.</param>
    public void AddMemoryDetail(string category, long bytes)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be null or whitespace.", nameof(category));

        MemoryDetails[category] = bytes;
    }

    /// <summary>
    /// Adds a compute operation detail.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="count">The operation count.</param>
    public void AddComputeDetail(string operation, double count)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation cannot be null or whitespace.", nameof(operation));

        ComputeDetails[operation] = count;
    }

    /// <summary>
    /// Adds a bandwidth usage detail.
    /// </summary>
    /// <param name="type">The bandwidth type.</param>
    /// <param name="gbps">The bandwidth in GB/s.</param>
    public void AddBandwidthDetail(string type, double gbps)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be null or whitespace.", nameof(type));

        BandwidthDetails[type] = gbps;
    }

    /// <summary>
    /// Adds a custom metric.
    /// </summary>
    /// <param name="name">The metric name.</param>
    /// <param name="value">The metric value.</param>
    public void AddCustomMetric(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));

        CustomMetrics[name] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets a custom metric by name.
    /// </summary>
    /// <typeparam name="T">The type of the metric value.</typeparam>
    /// <param name="name">The metric name.</param>
    /// <returns>The metric value if found; otherwise, default(T).</returns>
    public T? GetCustomMetric<T>(string name)
    {
        if (CustomMetrics.TryGetValue(name, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    /// Calculates the total operations per second based on execution time.
    /// </summary>
    /// <returns>The operations per second.</returns>
    public double GetOperationsPerSecond()
    {
        if (EstimatedExecutionTimeMs <= 0) return 0.0;
        
        var totalOps = EstimatedFlops + EstimatedIntOps;
        var timeInSeconds = EstimatedExecutionTimeMs / 1000.0;
        return totalOps / timeInSeconds;
    }

    /// <summary>
    /// Calculates the memory efficiency ratio (useful work vs. total memory access).
    /// </summary>
    /// <returns>The memory efficiency ratio (0-1).</returns>
    public double GetMemoryEfficiency()
    {
        if (TotalMemoryBytes == 0) return 1.0;
        
        var usefulWork = Math.Min(GlobalMemoryBytes + ConstantMemoryBytes, TotalMemoryBytes);
        return usefulWork / (double)TotalMemoryBytes;
    }

    /// <summary>
    /// Gets a human-readable summary of the resource usage.
    /// </summary>
    /// <returns>A formatted summary string.</returns>
    public string GetSummary()
    {
        return $"Memory: {TotalMemoryBytes / 1024.0 / 1024.0:F2} MB, " +
               $"Registers: {RegistersPerThread}, " +
               $"FLOPS: {EstimatedFlops:E2}, " +
               $"Bandwidth: {MemoryBandwidthGBps:F2} GB/s, " +
               $"Execution: {EstimatedExecutionTimeMs:F2} ms, " +
               $"Occupancy: {ExpectedOccupancy:F1}%";
    }

    /// <summary>
    /// Creates a scaled estimate for a different data size.
    /// </summary>
    /// <param name="newDataSize">The new data size.</param>
    /// <returns>A scaled resource usage estimate.</returns>
    public ResourceUsageEstimate ScaleToDataSize(long newDataSize)
    {
        if (InputDataSize <= 0)
            throw new InvalidOperationException("Cannot scale estimate with unknown input data size.");

        var scaleFactor = (double)newDataSize / InputDataSize;
        
        var scaled = new ResourceUsageEstimate
        {
            TotalMemoryBytes = (long)(TotalMemoryBytes * scaleFactor),
            GlobalMemoryBytes = (long)(GlobalMemoryBytes * scaleFactor),
            SharedMemoryBytes = SharedMemoryBytes, // Usually doesn't scale with data size
            LocalMemoryBytes = (long)(LocalMemoryBytes * scaleFactor),
            ConstantMemoryBytes = ConstantMemoryBytes, // Usually constant
            RegistersPerThread = RegistersPerThread, // Doesn't scale with data size
            EstimatedFlops = EstimatedFlops * scaleFactor,
            EstimatedIntOps = EstimatedIntOps * scaleFactor,
            MemoryBandwidthGBps = MemoryBandwidthGBps, // Hardware limit
            ComputeIntensity = ComputeIntensity, // Usually constant
            EstimatedExecutionTimeMs = EstimatedExecutionTimeMs * scaleFactor,
            ExpectedOccupancy = ExpectedOccupancy, // Usually doesn't change
            EstimatedPowerWatts = EstimatedPowerWatts, // Usually hardware dependent
            ConfidenceLevel = ConfidenceLevel * 0.9, // Lower confidence for extrapolated data
            HardwareProfile = HardwareProfile,
            InputDataSize = newDataSize,
            IsWorstCase = IsWorstCase
        };

        // Scale memory details
        foreach (var detail in MemoryDetails)
        {
            scaled.MemoryDetails[detail.Key] = (long)(detail.Value * scaleFactor);
        }

        // Scale compute details
        foreach (var detail in ComputeDetails)
        {
            scaled.ComputeDetails[detail.Key] = detail.Value * scaleFactor;
        }

        // Copy bandwidth details (usually don't scale)
        foreach (var detail in BandwidthDetails)
        {
            scaled.BandwidthDetails[detail.Key] = detail.Value;
        }

        // Copy custom metrics
        foreach (var metric in CustomMetrics)
        {
            scaled.CustomMetrics[metric.Key] = metric.Value;
        }

        return scaled;
    }

    /// <inheritdoc/>
    public bool Equals(ResourceUsageEstimate? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return TotalMemoryBytes == other.TotalMemoryBytes &&
               RegistersPerThread == other.RegistersPerThread &&
               Math.Abs(EstimatedFlops - other.EstimatedFlops) < 1e-6 &&
               Math.Abs(EstimatedExecutionTimeMs - other.EstimatedExecutionTimeMs) < 1e-6;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ResourceUsageEstimate other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(TotalMemoryBytes, RegistersPerThread, EstimatedFlops, EstimatedExecutionTimeMs);
    }

    /// <inheritdoc/>
    public override string ToString() => GetSummary();
}