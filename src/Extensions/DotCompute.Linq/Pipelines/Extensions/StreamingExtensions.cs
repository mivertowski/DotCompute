// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Pipelines.Streaming;

namespace DotCompute.Linq.Pipelines.Extensions;

/// <summary>
/// Extension methods for streaming data processing and anomaly detection.
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// Detects anomalies in a streaming data sequence.
    /// </summary>
    /// <param name="source">The source data stream</param>
    /// <param name="windowSize">Size of the analysis window</param>
    /// <param name="threshold">Anomaly detection threshold</param>
    /// <returns>Stream of anomaly detection results</returns>
    public static async IAsyncEnumerable<AnomalyDetectionResult> DetectAnomalies(
        this IAsyncEnumerable<double> source,
        int windowSize = 50,
        double threshold = 2.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        
        var window = new Queue<double>();
        
        await foreach (var value in source)
        {
            window.Enqueue(value);
            
            if (window.Count > windowSize)
            {
                window.Dequeue();
            }
            
            if (window.Count >= windowSize)
            {
                var mean = window.Average();
                var variance = window.Select(v => Math.Pow(v - mean, 2)).Average();
                var stdDev = Math.Sqrt(variance);
                var zScore = Math.Abs(value - mean) / stdDev;
                
                yield return new AnomalyDetectionResult
                {
                    Value = value,
                    IsAnomaly = zScore > threshold,
                    Score = zScore
                };
            }
            else
            {
                yield return new AnomalyDetectionResult
                {
                    Value = value,
                    IsAnomaly = false,
                    Score = 0.0
                };
            }
        }
    }

    /// <summary>
    /// Adds backpressure handling to a stream.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="source">The source stream</param>
    /// <param name="bufferSize">Buffer size for backpressure</param>
    /// <param name="strategy">Backpressure handling strategy</param>
    /// <returns>Stream with backpressure handling</returns>
    public static IAsyncEnumerable<T> WithBackpressure<T>(
        this IAsyncEnumerable<T> source,
        int bufferSize,
        BackpressureStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(source);
        
        // For demonstration, just return the source
        // In a real implementation, this would implement backpressure handling
        return source;
    }

    /// <summary>
    /// Processes items in real-time with maximum latency constraints.
    /// </summary>
    /// <typeparam name="TIn">Input element type</typeparam>
    /// <typeparam name="TOut">Output element type</typeparam>
    /// <param name="source">The source stream</param>
    /// <param name="processor">The processing function</param>
    /// <param name="maxLatency">Maximum processing latency</param>
    /// <returns>Stream of processed items</returns>
    public static async IAsyncEnumerable<TOut> ProcessRealTime<TIn, TOut>(
        this IAsyncEnumerable<TIn> source,
        Func<TIn, Task<TOut>> processor,
        TimeSpan maxLatency)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(processor);
        
        await foreach (var item in source)
        {
            using var cts = new CancellationTokenSource(maxLatency);
            
            var result = await ProcessWithTimeoutAsync(processor, item, cts.Token);
            if (result != null)
            {
                yield return result;
            }
        }
    }
    
    /// <summary>
    /// Helper method to process items with timeout handling.
    /// </summary>
    private static async Task<TOut?> ProcessWithTimeoutAsync<TIn, TOut>(
        Func<TIn, Task<TOut>> processor,
        TIn item,
        CancellationToken cancellationToken)
    {
        try
        {
            return await processor(item).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Skip items that take too long to process
            return default(TOut);
        }
    }
}

/// <summary>
/// Anomaly detection result.
/// </summary>
public struct AnomalyDetectionResult
{
    public double Value { get; set; }
    public bool IsAnomaly { get; set; }
    public double Score { get; set; }
}

/// <summary>
/// Backpressure handling strategies.
/// </summary>
public enum BackpressureStrategy
{
    DropOldest,
    DropNewest,
    Block
}