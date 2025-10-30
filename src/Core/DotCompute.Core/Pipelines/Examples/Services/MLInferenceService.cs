// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Examples.Models;

namespace DotCompute.Core.Pipelines.Examples.Services;

/// <summary>
/// Service for machine learning inference using kernel chains.
/// Demonstrates AI/ML workloads with GPU acceleration.
/// </summary>
public class MLInferenceService
{

    /// <summary>
    /// Performs ML inference using optimized kernel chains.
    /// </summary>
    /// <param name="input">Input data for prediction</param>
    /// <returns>ML prediction result</returns>
#pragma warning disable CA1822 // Mark members as static - Intentionally instance method for DI registration
    public async Task<MLPrediction> PredictAsync(MLInputData input) => await KernelChainExamples.MachineLearningInferenceExampleAsync(input);
#pragma warning restore CA1822 // Mark members as static
}
