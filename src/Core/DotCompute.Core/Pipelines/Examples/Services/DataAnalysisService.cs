// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Examples.Services;

/// <summary>
/// Service for data analysis using kernel chains.
/// Demonstrates high-performance data processing workflows.
/// </summary>
public static class DataAnalysisService
{
    /// <summary>
    /// Analyzes data using parallel processing kernel chains.
    /// </summary>
    /// <param name="data">Raw data to analyze</param>
    /// <returns>Analyzed data results</returns>
    public static async Task<float[]> AnalyzeDataAsync(float[] data) => await KernelChainExamples.ParallelDataProcessingExampleAsync(data);
}
