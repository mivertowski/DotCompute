// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Examples.Services;

/// <summary>
/// Service for data analysis using kernel chains.
/// Demonstrates high-performance data processing workflows.
/// </summary>
public class DataAnalysisService
{
    /// <summary>
    /// Analyzes data using parallel processing kernel chains.
    /// </summary>
    /// <param name="data">Raw data to analyze</param>
    /// <returns>Analyzed data results</returns>
#pragma warning disable CA1822 // Mark members as static - Intentionally instance method for DI registration
    public async Task<float[]> AnalyzeDataAsync(float[] data) => await KernelChainExamples.ParallelDataProcessingExampleAsync(data);
#pragma warning restore CA1822 // Mark members as static
}
