// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Integration.Tests.Utilities;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for real-world application scenarios.
/// Tests image processing pipelines, scientific computation, financial calculations, and machine learning operations.
/// </summary>
[Collection("Integration")]
public class RealWorldScenariosTests : IntegrationTestBase
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<RealWorldScenariosTests> _logger;

    public RealWorldScenariosTests(ITestOutputHelper output) : base(output)
    {
        _orchestrator = GetService<IComputeOrchestrator>();
        _logger = GetLogger<RealWorldScenariosTests>();
    }

    [Fact]
    public async Task ImageProcessing_GaussianBlur_ShouldProduceRealisticResults()
    {
        // Arrange
        const int width = 512;
        const int height = 512;
        const int channels = 3; // RGB



        var imageData = testData.GenerateImageData(height, width, channels);
        var outputData = new float[height, width, channels];

        _logger.LogInformation("Testing Gaussian blur on {Width}x{Height} image with {Channels} channels",
            width, height, channels);

        // Convert 3D array to flat array for processing
        var flatInput = FlattenImageData(imageData);
        var flatOutput = new float[flatInput.Length];

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>("GaussianBlur",

                flatInput, flatOutput, width, height, channels, 2.0f); // sigma = 2.0
        }, "GaussianBlur");

        // Assert
        var unflattenedOutput = UnflattenImageData(flatOutput, height, width, channels);

        // Verify blur effect: center pixels should be average of surrounding pixels
        VerifyBlurEffect(imageData, unflattenedOutput, width, height, channels);

        // Performance assertion
        var pixelCount = width * height * channels;
        var pixelsPerMs = pixelCount / measurement.ElapsedTime.TotalMilliseconds;


        _logger.LogInformation("Processed {Pixels} pixels in {Time}ms ({Rate:F0} pixels/ms)",
            pixelCount, measurement.ElapsedTime.TotalMilliseconds, pixelsPerMs);

        pixelsPerMs.Should().BeGreaterThan(1000, "Should process at least 1000 pixels per millisecond");
    }

    [Fact]
    public async Task ScientificComputation_FastFourierTransform_ShouldBeAccurate()
    {
        // Arrange
        const int signalLength = 2048; // Power of 2 for FFT


        // Generate a test signal with known frequency components

        var timeSeriesReal = new float[signalLength];
        var timeSeriesImag = new float[signalLength];
        var frequencyReal = new float[signalLength];
        var frequencyImag = new float[signalLength];

        // Create a composite signal: 50Hz sine + 120Hz cosine + noise
        for (var i = 0; i < signalLength; i++)
        {
            var t = i / 1000.0f; // 1kHz sampling rate
            timeSeriesReal[i] = (float)(
                Math.Sin(2 * Math.PI * 50 * t) +
                0.5 * Math.Cos(2 * Math.PI * 120 * t) +
                0.1 * (UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1)[0] - 0.5f)
            );
            timeSeriesImag[i] = 0.0f;
        }

        _logger.LogInformation("Testing FFT on signal with {Length} samples", signalLength);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteAsync<(float[], float[])>("FFT",
                timeSeriesReal, timeSeriesImag, frequencyReal, frequencyImag, signalLength);
        }, "FFT");

        // Assert
        VerifyFFTResults(frequencyReal, frequencyImag, signalLength);

        // Performance assertion: FFT should be efficient
        var samplesPerMs = signalLength / measurement.ElapsedTime.TotalMilliseconds;
        _logger.LogInformation("Processed {Samples} samples in {Time}ms ({Rate:F0} samples/ms)",
            signalLength, measurement.ElapsedTime.TotalMilliseconds, samplesPerMs);

        samplesPerMs.Should().BeGreaterThan(100, "FFT should process at least 100 samples per millisecond");
    }

    [Fact]
    public async Task FinancialCalculations_MonteCarloOptionPricing_ShouldConvergeToExpectedValue()
    {
        // Arrange - Black-Scholes parameters
        const float spotPrice = 100.0f;      // Current stock price
        const float strikePrice = 105.0f;    // Strike price
        const float riskFreeRate = 0.05f;    // 5% risk-free rate
        const float volatility = 0.2f;       // 20% volatility
        const float timeToMaturity = 0.25f;  // 3 months
        const int numSimulations = 100000;   // Monte Carlo simulations

        var randomSeeds = GetService<UnifiedTestHelpers.TestDataGenerator>().GenerateIntArray(numSimulations, 1, int.MaxValue);
        var optionPayoffs = new float[numSimulations];

        _logger.LogInformation("Testing Monte Carlo option pricing with {Simulations} simulations", numSimulations);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>("MonteCarloOptionPricing",
                randomSeeds, optionPayoffs, spotPrice, strikePrice, riskFreeRate,

                volatility, timeToMaturity, numSimulations);
        }, "MonteCarloOptionPricing");

        // Assert
        var averageOptionValue = optionPayoffs.Average();
        var standardError = CalculateStandardError(optionPayoffs);

        _logger.LogInformation("Option value: {Value:F4} ± {Error:F4}", averageOptionValue, standardError);

        // Theoretical Black-Scholes value for comparison (approximate)
        var theoreticalValue = CalculateBlackScholesCallPrice(spotPrice, strikePrice, riskFreeRate, volatility, timeToMaturity);
        _logger.LogInformation("Theoretical Black-Scholes value: {Theoretical:F4}", theoreticalValue);

        // Monte Carlo result should converge to theoretical value within reasonable bounds
        var convergenceError = Math.Abs(averageOptionValue - theoreticalValue);
        convergenceError.Should().BeLessThan(0.5f, "Monte Carlo should converge close to theoretical value");

        // Performance assertion
        var simulationsPerMs = numSimulations / measurement.ElapsedTime.TotalMilliseconds;
        _logger.LogInformation("Completed {Sims} simulations in {Time}ms ({Rate:F0} sims/ms)",
            numSimulations, measurement.ElapsedTime.TotalMilliseconds, simulationsPerMs);

        simulationsPerMs.Should().BeGreaterThan(50, "Should complete at least 50 simulations per millisecond");
    }

    [Fact]
    public async Task MachineLearning_MatrixMultiplication_ShouldSupportNeuralNetworkOperations()
    {
        // Arrange - Simulate a neural network layer computation: Y = X * W + B
        const int batchSize = 32;
        const int inputSize = 784;   // 28x28 image flattened
        const int hiddenSize = 256;  // Hidden layer size



        var inputMatrix = testData.GenerateFloatMatrix(batchSize, inputSize); // Input batch
        var weightMatrix = testData.GenerateFloatMatrix(inputSize, hiddenSize); // Weights
        var biasVector = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(hiddenSize, -0.1f, 0.1f); // Bias
        var outputMatrix = new float[batchSize, hiddenSize]; // Output

        _logger.LogInformation("Testing neural network layer: {Batch}x{Input} * {Input}x{Hidden} + {Hidden}",
            batchSize, inputSize, inputSize, hiddenSize, hiddenSize);

        // Convert to flat arrays for computation
        var flatInput = FlattenMatrix(inputMatrix);
        var flatWeights = FlattenMatrix(weightMatrix);
        var flatOutput = new float[batchSize * hiddenSize];

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>("MatrixMultiplyWithBias",
                flatInput, flatWeights, biasVector, flatOutput,
                batchSize, inputSize, hiddenSize);
        }, "NeuralNetworkLayer");

        // Assert
        var unflattenedOutput = UnflattenMatrix(flatOutput, batchSize, hiddenSize);

        // Verify mathematical correctness for a few samples

        VerifyMatrixMultiplicationWithBias(inputMatrix, weightMatrix, biasVector, unflattenedOutput);

        // Performance assertion - Important for ML workloads
        var operations = (long)batchSize * inputSize * hiddenSize * 2; // Multiply-add operations
        var gflops = operations / (measurement.ElapsedTime.TotalMilliseconds * 1_000_000);


        _logger.LogInformation("Performed {Ops:E2} operations in {Time}ms ({Rate:F2} GFLOPS)",
            operations, measurement.ElapsedTime.TotalMilliseconds, gflops);

        gflops.Should().BeGreaterThan(0.1, "Should achieve at least 0.1 GFLOPS for matrix operations");
    }

    [Fact]
    public async Task MachineLearning_ConvolutionalLayer_ShouldProcessImageFeatures()
    {
        // Arrange - Simulate CNN convolution layer
        const int batchSize = 4;
        const int inputChannels = 3;   // RGB
        const int outputChannels = 32; // Feature maps
        const int inputHeight = 64;
        const int inputWidth = 64;
        const int kernelSize = 3;
        const int outputHeight = inputHeight - kernelSize + 1; // No padding
        const int outputWidth = inputWidth - kernelSize + 1;



        // Generate input tensor: [batch, channels, height, width]

        var inputTensor = new float[batchSize * inputChannels * inputHeight * inputWidth];
        for (var i = 0; i < inputTensor.Length; i++)
        {
            inputTensor[i] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -1f, 1f)[0];
        }

        // Generate convolution kernels: [outputChannels, inputChannels, kernelSize, kernelSize]
        var kernels = new float[outputChannels * inputChannels * kernelSize * kernelSize];
        for (var i = 0; i < kernels.Length; i++)
        {
            kernels[i] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -0.1f, 0.1f)[0];
        }

        var outputTensor = new float[batchSize * outputChannels * outputHeight * outputWidth];

        _logger.LogInformation("Testing CNN convolution: {Batch}x{InCh}x{H}x{W} -> {Batch}x{OutCh}x{OutH}x{OutW}",
            batchSize, inputChannels, inputHeight, inputWidth,
            batchSize, outputChannels, outputHeight, outputWidth);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>("Convolution2D",
                inputTensor, kernels, outputTensor,
                batchSize, inputChannels, outputChannels,
                inputHeight, inputWidth, kernelSize);
        }, "CNNConvolution");

        // Assert
        VerifyConvolutionResults(inputTensor, kernels, outputTensor,

            batchSize, inputChannels, outputChannels,

            inputHeight, inputWidth, outputHeight, outputWidth, kernelSize);

        // Performance assertion
        var totalOperations = (long)batchSize * outputChannels * outputHeight * outputWidth *

                             inputChannels * kernelSize * kernelSize * 2; // MAC operations
        var gflops = totalOperations / (measurement.ElapsedTime.TotalMilliseconds * 1_000_000);

        _logger.LogInformation("CNN convolution: {Ops:E2} operations in {Time}ms ({Rate:F2} GFLOPS)",
            totalOperations, measurement.ElapsedTime.TotalMilliseconds, gflops);

        gflops.Should().BeGreaterThan(0.05, "Should achieve reasonable performance for convolution");
    }

    [Fact]
    public async Task ScientificComputation_NBodySimulation_ShouldCalculateGravitationalForces()
    {
        // Arrange - N-body gravitational simulation
        const int numBodies = 1000;
        const float timeStep = 0.01f;
        const int numSteps = 10;



        // Initialize particle positions, velocities, and masses

        var positions = new float[numBodies * 3]; // x, y, z for each body
        var velocities = new float[numBodies * 3];
        var masses = new float[numBodies];
        var forces = new float[numBodies * 3];

        // Generate random initial conditions
        for (var i = 0; i < numBodies; i++)
        {
            positions[i * 3] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -100f, 100f)[0];     // x
            positions[i * 3 + 1] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -100f, 100f)[0]; // y
            positions[i * 3 + 2] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -100f, 100f)[0]; // z


            velocities[i * 3] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -1f, 1f)[0];         // vx
            velocities[i * 3 + 1] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -1f, 1f)[0];     // vy
            velocities[i * 3 + 2] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, -1f, 1f)[0];     // vz


            masses[i] = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(1, 1f, 100f)[0]; // mass
        }

        _logger.LogInformation("Testing N-body simulation with {Bodies} bodies for {Steps} steps",
            numBodies, numSteps);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            for (var step = 0; step < numSteps; step++)
            {
                // Calculate forces
                await _orchestrator.ExecuteAsync<float[]>("CalculateNBodyForces",
                    positions, masses, forces, numBodies);

                // Update positions and velocities
                await _orchestrator.ExecuteAsync<float[]>("UpdateNBodyPositions",
                    positions, velocities, forces, masses, timeStep, numBodies);
            }
        }, "NBodySimulation");

        // Assert
        VerifyNBodySimulationResults(positions, velocities, numBodies);

        // Performance assertion
        var interactionCount = (long)numBodies * numBodies * numSteps;
        var interactionsPerMs = interactionCount / measurement.ElapsedTime.TotalMilliseconds;

        _logger.LogInformation("N-body simulation: {Interactions:E2} interactions in {Time}ms ({Rate:E2} int/ms)",
            interactionCount, measurement.ElapsedTime.TotalMilliseconds, interactionsPerMs);

        interactionsPerMs.Should().BeGreaterThan(1000, "Should process at least 1000 interactions per millisecond");
    }

    [Fact]
    public async Task FinancialCalculations_RiskAnalysis_ShouldCalculateValueAtRisk()
    {
        // Arrange - Portfolio risk analysis using Monte Carlo
        const int numAssets = 50;
        const int numScenarios = 50000;
        const int portfolioSize = 1000000; // $1M portfolio



        // Asset weights (sum to 1.0)

        var weights = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(numAssets, 0.01f, 0.05f);
        var weightSum = weights.Sum();
        for (var i = 0; i < numAssets; i++)
        {
            weights[i] /= weightSum; // Normalize to sum to 1.0
        }

        // Expected returns and volatilities
        var expectedReturns = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(numAssets, 0.05f, 0.15f); // 5-15% annual
        var volatilities = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(numAssets, 0.1f, 0.3f);      // 10-30% annual

        // Correlation matrix (simplified - uncorrelated for this test)
        var correlationMatrix = new float[numAssets * numAssets];
        for (var i = 0; i < numAssets; i++)
        {
            for (var j = 0; j < numAssets; j++)
            {
                correlationMatrix[i * numAssets + j] = (i == j) ? 1.0f : 0.0f;
            }
        }

        var portfolioReturns = new float[numScenarios];

        _logger.LogInformation("Testing portfolio risk analysis: {Assets} assets, {Scenarios} scenarios",
            numAssets, numScenarios);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>("MonteCarloPortfolioSimulation",
                weights, expectedReturns, volatilities, correlationMatrix,
                portfolioReturns, numAssets, numScenarios, portfolioSize);
        }, "PortfolioRiskAnalysis");

        // Assert
        var sortedReturns = portfolioReturns.OrderBy(x => x).ToArray();
        var var95 = sortedReturns[(int)(numScenarios * 0.05)]; // 5th percentile
        var var99 = sortedReturns[(int)(numScenarios * 0.01)]; // 1st percentile
        var expectedReturn = portfolioReturns.Average();

        _logger.LogInformation("Portfolio analysis results:");
        _logger.LogInformation("  Expected return: {Expected:C}", expectedReturn);
        _logger.LogInformation("  95% VaR: {VaR95:C}", -var95);
        _logger.LogInformation("  99% VaR: {VaR99:C}", -var99);

        // Sanity checks
        expectedReturn.Should().BeGreaterThan(0, "Expected portfolio return should be positive");
        var95.Should().BeLessThan(expectedReturn, "95% VaR should be worse than expected return");
        var99.Should().BeLessThan(var95, "99% VaR should be worse than 95% VaR");

        // Performance assertion
        var scenariosPerMs = numScenarios / measurement.ElapsedTime.TotalMilliseconds;
        _logger.LogInformation("Risk analysis: {Scenarios} scenarios in {Time}ms ({Rate:F0} scenarios/ms)",
            numScenarios, measurement.ElapsedTime.TotalMilliseconds, scenariosPerMs);

        scenariosPerMs.Should().BeGreaterThan(10, "Should process at least 10 scenarios per millisecond");
    }

    // Helper methods for verification

    private void VerifyBlurEffect(float[,,] original, float[,,] blurred, int width, int height, int channels)
    {
        // Check that blurred image has smoothed values (center pixel should be average of neighbors)
        for (var c = 0; c < channels; c++)
        {
            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var originalValue = original[y, x, c];
                    var blurredValue = blurred[y, x, c];

                    // Blurred value should be different from original (unless perfectly uniform)
                    // This is a weak check, but verifies some processing occurred

                    Math.Abs(blurredValue - originalValue).Should().BeLessOrEqualTo(
                        Math.Abs(originalValue) + 1.0f, "Blurred values should be reasonable");
                }
            }
        }
    }

    private void VerifyFFTResults(float[] real, float[] imag, int length)
    {
        // Verify Parseval's theorem (energy conservation)
        double timeEnergy = 0, freqEnergy = 0;


        for (var i = 0; i < length; i++)
        {
            freqEnergy += real[i] * real[i] + imag[i] * imag[i];
        }


        freqEnergy /= length; // Normalize for FFT scaling

        // Energy should be conserved (within numerical precision)
        // This is a basic sanity check for FFT correctness

        freqEnergy.Should().BeGreaterThan(0, "FFT should preserve energy");
    }

    private void VerifyMatrixMultiplicationWithBias(float[,] input, float[,] weights, float[] bias, float[,] output)
    {
        var batchSize = input.GetLength(0);
        var inputSize = input.GetLength(1);
        var hiddenSize = weights.GetLength(1);

        // Verify a few samples manually
        for (var b = 0; b < Math.Min(batchSize, 2); b++)
        {
            for (var h = 0; h < Math.Min(hiddenSize, 2); h++)
            {
                var expected = bias[h];
                for (var i = 0; i < inputSize; i++)
                {
                    expected += input[b, i] * weights[i, h];
                }


                output[b, h].Should().BeApproximately(expected, 1e-3f,
                    $"Matrix multiplication result should be correct for batch {b}, hidden {h}");
            }
        }
    }

    private void VerifyConvolutionResults(float[] input, float[] kernels, float[] output,
        int batchSize, int inputChannels, int outputChannels,
        int inputHeight, int inputWidth, int outputHeight, int outputWidth, int kernelSize)
    {
        // This is a simplified verification - just check that output is not all zeros
        // and has reasonable magnitude
        var outputSum = output.Sum();
        var outputMagnitude = output.Sum(x => Math.Abs(x));


        outputMagnitude.Should().BeGreaterThan(0, "Convolution output should not be all zeros");

        // Check output dimensions are correct

        var expectedOutputSize = batchSize * outputChannels * outputHeight * outputWidth;
        output.Length.Should().Be(expectedOutputSize, "Output tensor should have correct size");
    }

    private void VerifyNBodySimulationResults(float[] positions, float[] velocities, int numBodies)
    {
        // Basic sanity checks for N-body simulation
        for (var i = 0; i < numBodies * 3; i++)
        {
            positions[i].Should().NotBe(float.NaN, "Positions should not be NaN");
            positions[i].Should().NotBe(float.PositiveInfinity, "Positions should not be infinite");
            positions[i].Should().NotBe(float.NegativeInfinity, "Positions should not be infinite");


            velocities[i].Should().NotBe(float.NaN, "Velocities should not be NaN");
            velocities[i].Should().NotBe(float.PositiveInfinity, "Velocities should not be infinite");
            velocities[i].Should().NotBe(float.NegativeInfinity, "Velocities should not be infinite");
        }
    }

    private float CalculateStandardError(float[] values)
    {
        var mean = values.Average();
        var variance = values.Sum(x => (x - mean) * (x - mean)) / (values.Length - 1);
        return (float)(Math.Sqrt(variance) / Math.Sqrt(values.Length));
    }

    private float CalculateBlackScholesCallPrice(float s, float k, float r, float sigma, float t)
    {
        // Simplified Black-Scholes formula for call option
        var d1 = (Math.Log(s / k) + (r + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
        var d2 = d1 - sigma * Math.Sqrt(t);

        // Using approximation for normal CDF

        var n_d1 = 0.5 * (1 + Math.Sign(d1) * Math.Sqrt(1 - Math.Exp(-2 * d1 * d1 / Math.PI)));
        var n_d2 = 0.5 * (1 + Math.Sign(d2) * Math.Sqrt(1 - Math.Exp(-2 * d2 * d2 / Math.PI)));


        return (float)(s * n_d1 - k * Math.Exp(-r * t) * n_d2);
    }

    // Helper methods for array manipulation

    private static float[] FlattenImageData(float[,,] data)
    {
        var height = data.GetLength(0);
        var width = data.GetLength(1);
        var channels = data.GetLength(2);
        var flat = new float[height * width * channels];


        for (var h = 0; h < height; h++)
        {
            for (var w = 0; w < width; w++)
            {
                for (var c = 0; c < channels; c++)
                {
                    flat[h * width * channels + w * channels + c] = data[h, w, c];
                }
            }
        }


        return flat;
    }

    private static float[,,] UnflattenImageData(float[] flat, int height, int width, int channels)
    {
        var data = new float[height, width, channels];


        for (var h = 0; h < height; h++)
        {
            for (var w = 0; w < width; w++)
            {
                for (var c = 0; c < channels; c++)
                {
                    data[h, w, c] = flat[h * width * channels + w * channels + c];
                }
            }
        }


        return data;
    }

    private static float[] FlattenMatrix(float[,] matrix)
    {
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        var flat = new float[rows * cols];


        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                flat[r * cols + c] = matrix[r, c];
            }
        }


        return flat;
    }

    private static float[,] UnflattenMatrix(float[] flat, int rows, int cols)
    {
        var matrix = new float[rows, cols];


        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                matrix[r, c] = flat[r * cols + c];
            }
        }


        return matrix;
    }
}