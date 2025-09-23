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
    public static async Task<MLPrediction> PredictAsync(MLInputData input)
    {
        return await KernelChainExamples.MachineLearningInferenceExample(input);
    }
}
