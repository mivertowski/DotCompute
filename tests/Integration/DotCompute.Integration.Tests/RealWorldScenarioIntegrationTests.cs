// Copyright(c) 2025 Michael Ivertowski

#pragma warning disable CA1848 // Use LoggerMessage delegates - will be migrated in future iteration
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using DotCompute.Integration.Tests.Infrastructure;

namespace DotCompute.Integration.Tests;


/// <summary>
/// Integration tests simulating real-world scenarios including machine learning workloads,
/// scientific computing, image processing, financial modeling, and data analytics.
/// </summary>
[Collection("Integration")]
public sealed class RealWorldScenarioIntegrationTests(ITestOutputHelper output) : ComputeWorkflowTestBase(output)
{
    [Fact]
    public async Task MachineLearningTraining_NeuralNetworkLayer_ShouldProcessBatchesEfficiently()
    {
        // Arrange - Simulate CNN training scenario
        const int batchSize = 32;
        const int inputChannels = 3;
        const int outputChannels = 64;
        const int inputHeight = 224;
        const int inputWidth = 224;
        const int kernelSize = 3;

        var trainingBatch = TestDataGenerators.GenerateGaussianArray(
            batchSize * inputChannels * inputHeight * inputWidth, 0f, 0.1f);
        var weights = TestDataGenerators.GenerateGaussianArray(
            outputChannels * inputChannels * kernelSize * kernelSize, 0f, 0.1f);
        var biases = TestDataGenerators.GenerateGaussianArray(outputChannels, 0f, 0.1f);

        var mlWorkflow = new ComputeWorkflowDefinition
        {
            Name = "MLTrainingWorkflow",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "conv_forward",
                SourceCode = RealWorldKernels.ConvolutionalForward,
                CompilationOptions = new CompilationOptions
                {
                    OptimizationLevel = OptimizationLevel.Aggressive,
                    FastMath = true,
                    EnableOperatorFusion = true
                }
            },
            new WorkflowKernel
            {
                Name = "batch_norm",
                SourceCode = RealWorldKernels.BatchNormalization
            },
            new WorkflowKernel
            {
                Name = "activation",
                SourceCode = RealWorldKernels.ReLUActivation
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input_batch", Data = trainingBatch },
            new WorkflowInput { Name = "conv_weights", Data = weights },
            new WorkflowInput { Name = "conv_biases", Data = biases }
            ],
            Outputs =
            [
                new WorkflowOutput
            {
                Name = "feature_maps",
                Size = batchSize * outputChannels *(inputHeight - kernelSize + 1) *(inputWidth - kernelSize + 1)
            }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer
            {
                Name = "conv_output",
                SizeInBytes = batchSize * outputChannels *(inputHeight - kernelSize + 1) *(inputWidth - kernelSize + 1) * sizeof(float)
            },
            new WorkflowIntermediateBuffer
            {
                Name = "normalized_output",
                SizeInBytes = batchSize * outputChannels *(inputHeight - kernelSize + 1) *(inputWidth - kernelSize + 1) * sizeof(float)
            }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "convolution",
                Order = 1,
                KernelName = "conv_forward",
                ArgumentNames = ["input_batch", "conv_weights", "conv_biases", "conv_output"],
                Parameters = new Dictionary<string, object>
                {
                    ["batch_size"] = batchSize,
                    ["input_channels"] = inputChannels,
                    ["output_channels"] = outputChannels,
                    ["input_height"] = inputHeight,
                    ["input_width"] = inputWidth,
                    ["kernel_size"] = kernelSize
                }
            },
            new WorkflowExecutionStage
            {
                Name = "batch_normalization",
                Order = 2,
                KernelName = "batch_norm",
                ArgumentNames = ["conv_output", "normalized_output"]
            },
            new WorkflowExecutionStage
            {
                Name = "relu_activation",
                Order = 3,
                KernelName = "activation",
                ArgumentNames = ["normalized_output", "feature_maps"]
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("MLTrainingWorkflow", mlWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();
        _ = result.ExecutionResults.Count.Should().Be(3);

        var featureMaps = (float[])result.Results["feature_maps"];
        _ = featureMaps.Should().NotContain(float.NaN);
        _ = featureMaps.Should().NotContain(float.PositiveInfinity);
        _ = featureMaps.Should().NotContain(float.NegativeInfinity);

        // Verify activation outputs are reasonable(ReLU should produce non-negative values)
        _ = featureMaps.Should().AllSatisfy(value => value.Should().BeGreaterThanOrEqualTo(0f));

        // Performance validation
        var totalDataMB = (trainingBatch.Length + weights.Length) * sizeof(float) / 1024.0 / 1024.0;
        var throughputMBps = totalDataMB / result.Duration.TotalSeconds;

        Logger.LogInformation("ML Training performance: {Throughput:F2} MB/s, {Duration:F1}ms for batch size {BatchSize}",
            throughputMBps, result.Duration.TotalMilliseconds, batchSize);

        _ = throughputMBps.Should().BeGreaterThan(10, "ML workload should maintain good throughput");
    }

    [Fact]
    public async Task ScientificComputing_MolecularDynamics_ShouldSimulateParticleInteractions()
    {
        // Arrange - Simulate N-body particle system
        const int particleCount = 4096;
        const float timeStep = 0.001f;
        const int simulationSteps = 100; // Commenting out to prevent unused variable warning
        _ = simulationSteps;

        var positions = TestDataGenerators.GenerateFloatArray(particleCount * 3, -10f, 10f); // x,y,z coordinates
        var velocities = TestDataGenerators.GenerateFloatArray(particleCount * 3, -1f, 1f);
        var masses = TestDataGenerators.GenerateFloatArray(particleCount, 1f, 10f);

        var mdWorkflow = new ComputeWorkflowDefinition
        {
            Name = "MolecularDynamicsSimulation",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "compute_forces",
                SourceCode = RealWorldKernels.ComputeForces,
                CompilationOptions = new CompilationOptions
                {
                    OptimizationLevel = OptimizationLevel.Maximum,
                    FastMath = true
                }
            },
            new WorkflowKernel
            {
                Name = "integrate_motion",
                SourceCode = RealWorldKernels.IntegrateMotion
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "positions", Data = positions },
            new WorkflowInput { Name = "velocities", Data = velocities },
            new WorkflowInput { Name = "masses", Data = masses }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "final_positions", Size = particleCount * 3 },
            new WorkflowOutput { Name = "final_velocities", Size = particleCount * 3 }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "forces", SizeInBytes = particleCount * 3 * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "temp_positions", SizeInBytes = particleCount * 3 * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "temp_velocities", SizeInBytes = particleCount * 3 * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "force_computation",
                Order = 1,
                KernelName = "compute_forces",
                ArgumentNames = ["positions", "masses", "forces"],
                Parameters = new Dictionary<string, object> { ["particle_count"] = particleCount }
            },
            new WorkflowExecutionStage
            {
                Name = "motion_integration",
                Order = 2,
                KernelName = "integrate_motion",
                ArgumentNames = ["positions", "velocities", "forces", "masses", "final_positions", "final_velocities"],
                Parameters = new Dictionary<string, object>
                {
                    ["time_step"] = timeStep,
                    ["particle_count"] = particleCount
                }
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("MolecularDynamicsSimulation", mdWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        var finalPositions = (float[])result.Results["final_positions"];
        var finalVelocities = (float[])result.Results["final_velocities"];

        // Validate simulation results
        _ = finalPositions.Should().NotContain(float.NaN);
        _ = finalVelocities.Should().NotContain(float.NaN);

        // Check conservation principles(energy should be roughly conserved)
        ValidatePhysicsConservation(positions, velocities, masses, finalPositions, finalVelocities);

        // Performance validation for scientific computing
        var computationalIntensity = particleCount * particleCount; // O(N^2) force calculation
        var performanceScore = computationalIntensity / result.Duration.TotalMilliseconds;

        Logger.LogInformation("Scientific computing performance: {Score:E2} operations/ms for {Particles} particles",
            performanceScore, particleCount);

        _ = (performanceScore > 1e6).Should().BeTrue();
    }

    [Fact]
    public async Task ImageProcessingPipeline_PhotoEnhancement_ShouldProcessHighResolutionImages()
    {
        // Arrange - Simulate high-resolution image processing
        const int imageWidth = 1920;
        const int imageHeight = 1080;
        const int channels = 3; // RGB

        var rawImage = TestDataGenerators.GenerateFloatArray(
            imageWidth * imageHeight * channels, 0f, 255f);

        var imageWorkflow = new ComputeWorkflowDefinition
        {
            Name = "ImageProcessingPipeline",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "noise_reduction",
                SourceCode = RealWorldKernels.NoiseReduction,
                CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
            },
            new WorkflowKernel
            {
                Name = "edge_enhancement",
                SourceCode = RealWorldKernels.EdgeEnhancement
            },
            new WorkflowKernel
            {
                Name = "color_correction",
                SourceCode = RealWorldKernels.ColorCorrection
            },
            new WorkflowKernel
            {
                Name = "tone_mapping",
                SourceCode = RealWorldKernels.ToneMapping
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "raw_image", Data = rawImage }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "enhanced_image", Size = imageWidth * imageHeight * channels }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "denoised", SizeInBytes = imageWidth * imageHeight * channels * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "sharpened", SizeInBytes = imageWidth * imageHeight * channels * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "color_corrected", SizeInBytes = imageWidth * imageHeight * channels * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "denoise",
                Order = 1,
                KernelName = "noise_reduction",
                ArgumentNames = ["raw_image", "denoised"],
                Parameters = new Dictionary<string, object> { ["width"] = imageWidth, ["height"] = imageHeight, ["channels"] = channels }
            },
            new WorkflowExecutionStage
            {
                Name = "enhance_edges",
                Order = 2,
                KernelName = "edge_enhancement",
                ArgumentNames = ["denoised", "sharpened"],
                Parameters = new Dictionary<string, object> { ["width"] = imageWidth, ["height"] = imageHeight }
            },
            new WorkflowExecutionStage
            {
                Name = "correct_colors",
                Order = 3,
                KernelName = "color_correction",
                ArgumentNames = ["sharpened", "color_corrected"]
            },
            new WorkflowExecutionStage
            {
                Name = "tone_map",
                Order = 4,
                KernelName = "tone_mapping",
                ArgumentNames = ["color_corrected", "enhanced_image"]
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("ImageProcessingPipeline", imageWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();
        _ = result.ExecutionResults.Count.Should().Be(4);

        var enhancedImage = (float[])result.Results["enhanced_image"];

        // Validate image processing results
        _ = enhancedImage.Should().NotContain(float.NaN);
        _ = enhancedImage.Should().AllSatisfy(pixel => pixel.Should().BeInRange(0f, 255f));

        // Performance validation for image processing
        var pixelCount = imageWidth * imageHeight;
        var megapixels = pixelCount / 1_000_000.0;
        var processingRate = megapixels / result.Duration.TotalSeconds;

        Logger.LogInformation("Image processing performance: {Rate:F2} MP/s{Width}x{Height})",
            processingRate, imageWidth, imageHeight);

        _ = processingRate.Should().BeGreaterThan(10, "Image processing should achieve reasonable megapixel/second rate");

        // Verify all processing stages completed in reasonable time
        _ = result.ExecutionResults.Values.Should().AllSatisfy(stage =>
            stage.Duration.TotalSeconds.Should().BeLessThan(5));
    }

    [Fact]
    public async Task FinancialModeling_MonteCarloSimulation_ShouldCalculateOptionPrices()
    {
        // Arrange - Monte Carlo option pricing
        const int simulationPaths = 100000;
        const int timeSteps = 252; // Trading days in a year
        const float spotPrice = 100.0f;
        const float strikePrice = 105.0f;
        const float riskFreeRate = 0.05f;
        const float volatility = 0.2f;
        const float timeToExpiry = 1.0f;

        var randomSeeds = TestDataGenerators.GenerateFloatArray(simulationPaths * timeSteps, 0f, 1f);

        var financeWorkflow = new ComputeWorkflowDefinition
        {
            Name = "MonteCarloOptionPricing",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "generate_paths",
                SourceCode = RealWorldKernels.GenerateStockPaths,
                CompilationOptions = new CompilationOptions { FastMath = true }
            },
            new WorkflowKernel
            {
                Name = "calculate_payoffs",
                SourceCode = RealWorldKernels.CalculateOptionPayoffs
            },
            new WorkflowKernel
            {
                Name = "discount_and_average",
                SourceCode = RealWorldKernels.DiscountAndAverage
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "random_numbers", Data = randomSeeds }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "option_prices", Size = 1 }, // Single option price
            new WorkflowOutput { Name = "confidence_interval", Size = 2 } // Lower and upper bounds
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "stock_paths", SizeInBytes = simulationPaths * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "payoffs", SizeInBytes = simulationPaths * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "path_generation",
                Order = 1,
                KernelName = "generate_paths",
                ArgumentNames = ["random_numbers", "stock_paths"],
                Parameters = new Dictionary<string, object>
                {
                    ["spot_price"] = spotPrice,
                    ["risk_free_rate"] = riskFreeRate,
                    ["volatility"] = volatility,
                    ["time_to_expiry"] = timeToExpiry,
                    ["time_steps"] = timeSteps,
                    ["num_paths"] = simulationPaths
                }
            },
            new WorkflowExecutionStage
            {
                Name = "payoff_calculation",
                Order = 2,
                KernelName = "calculate_payoffs",
                ArgumentNames = ["stock_paths", "payoffs"],
                Parameters = new Dictionary<string, object> { ["strike_price"] = strikePrice }
            },
            new WorkflowExecutionStage
            {
                Name = "pricing",
                Order = 3,
                KernelName = "discount_and_average",
                ArgumentNames = ["payoffs", "option_prices", "confidence_interval"],
                Parameters = new Dictionary<string, object>
                {
                    ["risk_free_rate"] = riskFreeRate,
                    ["time_to_expiry"] = timeToExpiry,
                    ["num_paths"] = simulationPaths
                }
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("MonteCarloOptionPricing", financeWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        var optionPrice = ((float[])result.Results["option_prices"])[0];
        var confidenceInterval = (float[])result.Results["confidence_interval"];

        // Validate financial model results
        Assert.True(optionPrice > 0f);
        Assert.True(optionPrice < spotPrice); // Option price should be reasonable
        _ = confidenceInterval[0].Should().BeLessThan(optionPrice);
        _ = confidenceInterval[1].Should().BeGreaterThan(optionPrice);

        // Calculate theoretical Black-Scholes price for comparison
        var theoreticalPrice = CalculateBlackScholesPrice(spotPrice, strikePrice, riskFreeRate, volatility, timeToExpiry);
        var priceDifference = Math.Abs(optionPrice - theoreticalPrice);

        Logger.LogInformation("Monte Carlo option pricing: MC={MC:F4}, BS={BS:F4}, Diff={Diff:F4}, " +
                             "CI=[{Lower:F4}, {Upper:F4}], {Paths} paths",
            optionPrice, theoreticalPrice, priceDifference,
            confidenceInterval[0], confidenceInterval[1], simulationPaths);

        // Monte Carlo should be reasonably close to theoretical price
        _ = (priceDifference < theoreticalPrice * 0.05f).Should().BeTrue(
            "Monte Carlo price should be within 5% of theoretical price");

        // Performance validation for financial computing
        var pathsPerSecond = simulationPaths / result.Duration.TotalSeconds;
        _ = pathsPerSecond.Should().BeGreaterThan(10000, "Monte Carlo should process paths efficiently");
    }

    [Fact]
    public async Task DataAnalytics_LargeDatasetAggregation_ShouldProcessBigData()
    {
        // Arrange - Simulate big data analytics scenario
        const int recordCount = 1_000_000; // 1 million records
        const int dimensionCount = 10;

        var dataset = TestDataGenerators.GenerateFloatArray(recordCount * dimensionCount, -100f, 100f);
        var groupKeys = TestDataGenerators.GenerateFloatArray(recordCount).Select(x => (int)(x * 100) % 100).ToArray();

        var analyticsWorkflow = new ComputeWorkflowDefinition
        {
            Name = "BigDataAnalytics",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "filter_data",
                SourceCode = RealWorldKernels.FilterData,
                CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
            },
            new WorkflowKernel
            {
                Name = "compute_statistics",
                SourceCode = RealWorldKernels.ComputeStatistics
            },
            new WorkflowKernel
            {
                Name = "aggregate_groups",
                SourceCode = RealWorldKernels.AggregateGroups
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "raw_data", Data = dataset },
            new WorkflowInput { Name = "group_keys", Data = [.. groupKeys.Select(k => (float)k)] }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "aggregated_results", Size = 100 * dimensionCount }, // 100 groups
            new WorkflowOutput { Name = "summary_stats", Size = dimensionCount * 4 } // Mean, std, min, max per dimension
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "filtered_data", SizeInBytes = recordCount * dimensionCount * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "statistics", SizeInBytes = dimensionCount * 4 * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "data_filtering",
                Order = 1,
                KernelName = "filter_data",
                ArgumentNames = ["raw_data", "filtered_data"],
                Parameters = new Dictionary<string, object>
                {
                    ["record_count"] = recordCount,
                    ["dimension_count"] = dimensionCount
                }
            },
            new WorkflowExecutionStage
            {
                Name = "statistics_computation",
                Order = 2,
                KernelName = "compute_statistics",
                ArgumentNames = ["filtered_data", "statistics"],
                Parameters = new Dictionary<string, object>
                {
                    ["record_count"] = recordCount,
                    ["dimension_count"] = dimensionCount
                }
            },
            new WorkflowExecutionStage
            {
                Name = "group_aggregation",
                Order = 3,
                KernelName = "aggregate_groups",
                ArgumentNames = ["filtered_data", "group_keys", "aggregated_results"],
                Parameters = new Dictionary<string, object>
                {
                    ["record_count"] = recordCount,
                    ["dimension_count"] = dimensionCount,
                    ["group_count"] = 100
                }
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("BigDataAnalytics", analyticsWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        var aggregatedResults = (float[])result.Results["aggregated_results"];
        var summaryStats = (float[])result.Results["summary_stats"];

        // Validate data analytics results
        _ = aggregatedResults.Should().NotContain(float.NaN);
        _ = summaryStats.Should().NotContain(float.NaN);

        // Performance validation for big data processing
        var recordsPerSecond = recordCount / result.Duration.TotalSeconds;
        var throughputMBps = recordCount * dimensionCount * sizeof(float) / 1024.0 / 1024.0 / result.Duration.TotalSeconds;

        Logger.LogInformation("Big data analytics performance: {RecordsPerSec:E2} records/s, {Throughput:F2} MB/s " +
                             "for {Records:E0} records",
            recordsPerSecond, throughputMBps, (double)recordCount);

        _ = recordsPerSecond.Should().BeGreaterThan(50000, "Big data processing should handle large record volumes efficiently");
        _ = throughputMBps.Should().BeGreaterThan(50, "Data analytics should maintain good memory throughput");
    }

    [Theory]
    [InlineData("VideoProcessing", 1920, 1080, 30)] // Full HD at 30 FPS
    [InlineData("AudioProcessing", 44100, 2, 10)]   // CD quality stereo, 10 seconds
    [InlineData("CryptographicHashing", 1024, 1000, 1)] // 1MB data, 1000 iterations
    public async Task StreamingWorkload_RealTimeProcessing_ShouldMaintainThroughput(
        string workloadType, int param1, int param2, int param3)
    {
        // Arrange
        var streamingWorkflow = CreateStreamingWorkflow(workloadType, param1, param2, param3);

        // Act
        var result = await ExecuteComputeWorkflowAsync($"StreamingWorkload_{workloadType}", streamingWorkflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        // Validate streaming performance
        var expectedThroughput = CalculateExpectedStreamingThroughput(workloadType, param1, param2, param3);
        var actualThroughput = result.Metrics?.ThroughputMBps ?? 0;

        Logger.LogInformation("Streaming workload {Type}: {Actual:F2} MB/sexpected: {Expected:F2} MB/s)",
            workloadType, actualThroughput, expectedThroughput);

        _ = (actualThroughput > expectedThroughput * 0.7).Should().BeTrue(
            $"{workloadType} streaming should meet 70% of expected throughput");

        // Real-time workloads should complete within reasonable time
        _ = result.Duration.TotalSeconds.Should().BeLessThan(10,
            "Streaming workloads should complete quickly for real-time requirements");
    }

    // Helper methods

    private static void ValidatePhysicsConservation(float[] initialPositions, float[] initialVelocities, float[] masses,
        float[] finalPositions, float[] finalVelocities)
    {
        const int particleCount = 4096;

        // Calculate initial and final kinetic energy(simplified)
        double initialKE = 0, finalKE = 0;

        for (var i = 0; i < particleCount; i++)
        {
            var mass = masses[i];

            // Initial kinetic energy
            var vx0 = initialVelocities[i * 3];
            var vy0 = initialVelocities[i * 3 + 1];
            var vz0 = initialVelocities[i * 3 + 2];
            initialKE += 0.5 * mass * (vx0 * vx0 + vy0 * vy0 + vz0 * vz0);

            // Final kinetic energy
            var vxf = finalVelocities[i * 3];
            var vyf = finalVelocities[i * 3 + 1];
            var vzf = finalVelocities[i * 3 + 2];
            finalKE += 0.5 * mass * (vxf * vxf + vyf * vyf + vzf * vzf);
        }

        // Energy should be roughly conserved(within numerical precision)
        var energyChange = Math.Abs(finalKE - initialKE) / initialKE;
        _ = energyChange.Should().BeLessThan(0.1, "Energy should be approximately conserved in MD simulation");
    }

    private static float CalculateBlackScholesPrice(float S, float K, float r, float sigma, float T)
    {
        var d1 = (Math.Log(S / K) + (r + 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));
        var d2 = d1 - sigma * Math.Sqrt(T);

        var N_d1 = 0.5 * (1 + Erf(d1 / Math.Sqrt(2)));
        var N_d2 = 0.5 * (1 + Erf(d2 / Math.Sqrt(2)));

        return (float)(S * N_d1 - K * Math.Exp(-r * T) * N_d2);
    }

    private static double Erf(double x)
    {
        // Approximation of error function for Black-Scholes
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - ((a5 * t + a4) * t + a3) * t + a2 * t + a1 * t * Math.Exp(-x * x);

        return sign * y;
    }

    private static ComputeWorkflowDefinition CreateStreamingWorkflow(string workloadType, int param1, int param2, int param3)
    {
        return workloadType switch
        {
            "VideoProcessing" => CreateVideoProcessingWorkflow(param1, param2, param3),
            "AudioProcessing" => CreateAudioProcessingWorkflow(param1, param2, param3),
            "CryptographicHashing" => CreateCryptographicWorkflow(param1, param2, param3),
            _ => throw new ArgumentException($"Unknown workload type: {workloadType}")
        };
    }

    private static ComputeWorkflowDefinition CreateVideoProcessingWorkflow(int width, int height, int fps)
    {
        var frameData = TestDataGenerators.GenerateFloatArray(width * height * 3, 0f, 255f);

        return new ComputeWorkflowDefinition
        {
            Name = "VideoProcessing",
            Kernels =
            [
                new WorkflowKernel { Name = "video_filter", SourceCode = RealWorldKernels.VideoFilter }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "frame", Data = frameData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "processed_frame", Size = width * height * 3 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "filter_stage",
                Order = 1,
                KernelName = "video_filter",
                ArgumentNames = ["frame", "processed_frame"],
                Parameters = new Dictionary<string, object> { ["width"] = width, ["height"] = height }
            }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateAudioProcessingWorkflow(int sampleRate, int channels, int duration)
    {
        var audioData = TestDataGenerators.GenerateFloatArray(sampleRate * channels * duration, -1f, 1f);

        return new ComputeWorkflowDefinition
        {
            Name = "AudioProcessing",
            Kernels =
            [
                new WorkflowKernel { Name = "audio_effect", SourceCode = RealWorldKernels.AudioEffect }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "audio", Data = audioData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "processed_audio", Size = audioData.Length }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "effect_stage",
                Order = 1,
                KernelName = "audio_effect",
                ArgumentNames = ["audio", "processed_audio"]
            }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateCryptographicWorkflow(int dataSize, int iterations, int unused)
    {
        var data = TestDataGenerators.GenerateFloatArray(dataSize, 0f, 255f);

        return new ComputeWorkflowDefinition
        {
            Name = "CryptographicHashing",
            Kernels =
            [
                new WorkflowKernel { Name = "hash_function", SourceCode = RealWorldKernels.HashFunction }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "data", Data = data }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "hashes", Size = data.Length / 16 } // 16:1 compression ratio
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "hashing_stage",
                Order = 1,
                KernelName = "hash_function",
                ArgumentNames = ["data", "hashes"],
                Parameters = new Dictionary<string, object> { ["iterations"] = iterations }
            }
            ]
        };
    }

    private static double CalculateExpectedStreamingThroughput(string workloadType, int param1, int param2, int param3)
    {
        return workloadType switch
        {
            "VideoProcessing" => param1 * param2 * 3 * param3 * sizeof(float) / 1024.0 / 1024.0, // MB/s
            "AudioProcessing" => param1 * param2 * param3 * sizeof(float) / 1024.0 / 1024.0,
            "CryptographicHashing" => param1 * param2 * sizeof(float) / 1024.0 / 1024.0,
            _ => 10.0 // Default expected throughput
        };
    }
}

/// <summary>
/// Real-world scenario kernel sources.
/// </summary>
internal static class RealWorldKernels
{
    public const string ConvolutionalForward = @"
__kernel void conv_forward(__global const float* input, __global const float* weights, 
                          __global const float* biases, __global float* output,
                          int batch_size, int input_channels, int output_channels,
                          int input_height, int input_width, int kernel_size) {
    int batch = get_global_id(0);
    int out_c = get_global_id(1);
    int out_h = get_global_id(2);
    int out_w = get_global_id(3);
    
    if(batch >= batch_size || out_c >= output_channels) return;
    
    int output_height = input_height - kernel_size + 1;
    int output_width = input_width - kernel_size + 1;
    
    if(out_h >= output_height || out_w >= output_width) return;
    
    float sum = biases[out_c];
    
    for(int in_c = 0; in_c < input_channels; in_c++) {
        for(int k_h = 0; k_h < kernel_size; k_h++) {
            for(int k_w = 0; k_w < kernel_size; k_w++) {
                int in_h = out_h + k_h;
                int in_w = out_w + k_w;
                
                int input_idx =((batch * input_channels + in_c) * input_height + in_h) * input_width + in_w;
                int weight_idx =((out_c * input_channels + in_c) * kernel_size + k_h) * kernel_size + k_w;
                
                sum += input[input_idx] * weights[weight_idx];
            }
        }
    }
    
    int output_idx =((batch * output_channels + out_c) * output_height + out_h) * output_width + out_w;
    output[output_idx] = sum;
}";

    public const string BatchNormalization = @"
__kernel void batch_norm(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Simplified batch normalization - just normalize to [-1, 1]
    float value = input[gid];
    output[gid] = tanh(value * 0.01f);
}";

    public const string ReLUActivation = @"
__kernel void activation(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = fmax(0.0f, input[gid]);
}";

    public const string ComputeForces = @"
__kernel void compute_forces(__global const float* positions, __global const float* masses,
                            __global float* forces, int particle_count) {
    int i = get_global_id(0);
    if(i >= particle_count) return;
    
    float3 force =(float3)(0.0f, 0.0f, 0.0f);
    float3 pos_i =(float3)(positions[i*3], positions[i*3+1], positions[i*3+2]);
    float mass_i = masses[i];
    
    for(int j = 0; j < particle_count; j++) {
        if(i != j) {
            float3 pos_j =(float3)(positions[j*3], positions[j*3+1], positions[j*3+2]);
            float3 r = pos_j - pos_i;
            float dist = length(r) + 0.01f; // Softening factor
            float3 force_ij =(mass_i * masses[j]) /(dist * dist * dist) * r;
            force += force_ij;
        }
    }
    
    forces[i*3] = force.x;
    forces[i*3+1] = force.y;
    forces[i*3+2] = force.z;
}";

    public const string IntegrateMotion = @"
__kernel void integrate_motion(__global const float* positions, __global const float* velocities,
                              __global const float* forces, __global const float* masses,
                              __global float* new_positions, __global float* new_velocities,
                              float time_step, int particle_count) {
    int i = get_global_id(0);
    if(i >= particle_count) return;
    
    float mass = masses[i];
    float3 pos =(float3)(positions[i*3], positions[i*3+1], positions[i*3+2]);
    float3 vel =(float3)(velocities[i*3], velocities[i*3+1], velocities[i*3+2]);
    float3 force =(float3)(forces[i*3], forces[i*3+1], forces[i*3+2]);
    
    // Velocity Verlet integration
    float3 acc = force / mass;
    float3 new_vel = vel + acc * time_step;
    float3 new_pos = pos + new_vel * time_step;
    
    new_positions[i*3] = new_pos.x;
    new_positions[i*3+1] = new_pos.y;
    new_positions[i*3+2] = new_pos.z;
    
    new_velocities[i*3] = new_vel.x;
    new_velocities[i*3+1] = new_vel.y;
    new_velocities[i*3+2] = new_vel.z;
}";

    public const string NoiseReduction = @"
__kernel void noise_reduction(__global const float* input, __global float* output,
                             int width, int height, int channels) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    int c = get_global_id(2);
    
    if(x >= width || y >= height || c >= channels) return;
    
    // Gaussian blur for noise reduction
    float kernel[9] = {0.0625f, 0.125f, 0.0625f,
                       0.125f,  0.25f,  0.125f,
                       0.0625f, 0.125f, 0.0625f};
    
    float sum = 0.0f;
    for(int dy = -1; dy <= 1; dy++) {
        for(int dx = -1; dx <= 1; dx++) {
            int nx = clamp(x + dx, 0, width - 1);
            int ny = clamp(y + dy, 0, height - 1);
            int idx =(ny * width + nx) * channels + c;
            sum += input[idx] * kernel[(dy + 1) * 3 +(dx + 1)];
        }
    }
    
    int output_idx =(y * width + x) * channels + c;
    output[output_idx] = sum;
}";

    public const string EdgeEnhancement = @"
__kernel void edge_enhancement(__global const float* input, __global float* output,
                              int width, int height) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    
    if(x >= width || y >= height) return;
    
    // Unsharp mask for edge enhancement
    float original = input[y * width + x];
    
    // Gaussian blur(simplified)
    float blurred = 0.0f;
    int count = 0;
    for(int dy = -2; dy <= 2; dy++) {
        for(int dx = -2; dx <= 2; dx++) {
            int nx = clamp(x + dx, 0, width - 1);
            int ny = clamp(y + dy, 0, height - 1);
            blurred += input[ny * width + nx];
            count++;
        }
    }
    blurred /= count;
    
    // Enhance edges
    float enhanced = original + 0.5f *(original - blurred);
    output[y * width + x] = clamp(enhanced, 0.0f, 255.0f);
}";

    public const string ColorCorrection = @"
__kernel void color_correction(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    
    // Simple gamma correction and contrast enhancement
    value = clamp(value / 255.0f, 0.0f, 1.0f);
    value = pow(value, 0.8f); // Gamma correction
    value =(value - 0.5f) * 1.2f + 0.5f; // Contrast enhancement
    
    output[gid] = clamp(value * 255.0f, 0.0f, 255.0f);
}";

    public const string ToneMapping = @"
__kernel void tone_mapping(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float hdr_value = input[gid] / 255.0f;
    
    // Reinhard tone mapping
    float ldr_value = hdr_value /(1.0f + hdr_value);
    
    output[gid] = clamp(ldr_value * 255.0f, 0.0f, 255.0f);
}";

    public const string GenerateStockPaths = @"
__kernel void generate_paths(__global const float* randoms, __global float* stock_paths,
                           float spot_price, float risk_free_rate, float volatility,
                           float time_to_expiry, int time_steps, int num_paths) {
    int path_idx = get_global_id(0);
    if(path_idx >= num_paths) return;
    
    float dt = time_to_expiry / time_steps;
    float drift =(risk_free_rate - 0.5f * volatility * volatility) * dt;
    float diffusion = volatility * sqrt(dt);
    
    float stock_price = spot_price;
    
    for(int t = 0; t < time_steps; t++) {
        float random = randoms[path_idx * time_steps + t];
        // Box-Muller transform(simplified)
        float normal = sqrt(-2.0f * log(random + 1e-8f)) * cos(2.0f * M_PI * random);
        
        stock_price *= exp(drift + diffusion * normal);
    }
    
    stock_paths[path_idx] = stock_price;
}";

    public const string CalculateOptionPayoffs = @"
__kernel void calculate_payoffs(__global const float* stock_paths, __global float* payoffs,
                               float strike_price) {
    int path_idx = get_global_id(0);
    float stock_price = stock_paths[path_idx];
    
    // European call option payoff
    payoffs[path_idx] = fmax(0.0f, stock_price - strike_price);
}";

    public const string DiscountAndAverage = @"
__kernel void discount_and_average(__global const float* payoffs, __global float* option_price,
                                  __global float* confidence_interval,
                                  float risk_free_rate, float time_to_expiry, int num_paths) {
    int lid = get_local_id(0);
    int lsize = get_local_size(0);
    
    __local float shared_sum[256];
    __local float shared_sum_sq[256];
    
    // Each thread processes multiple payoffs
    float sum = 0.0f;
    float sum_sq = 0.0f;
    int paths_per_thread =(num_paths + get_global_size(0) - 1) / get_global_size(0);
    
    for(int i = 0; i < paths_per_thread; i++) {
        int idx = get_global_id(0) * paths_per_thread + i;
        if(idx < num_paths) {
            float payoff = payoffs[idx];
            sum += payoff;
            sum_sq += payoff * payoff;
        }
    }
    
    shared_sum[lid] = sum;
    shared_sum_sq[lid] = sum_sq;
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Reduction
    for(int offset = lsize / 2; offset > 0; offset >>= 1) {
        if(lid < offset) {
            shared_sum[lid] += shared_sum[lid + offset];
            shared_sum_sq[lid] += shared_sum_sq[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    if(lid == 0 && get_group_id(0) == 0) {
        float total_sum = shared_sum[0];
        float total_sum_sq = shared_sum_sq[0];
        
        float mean = total_sum / num_paths;
        float variance = total_sum_sq / num_paths - mean * mean;
        float std_error = sqrt(variance / num_paths);
        
        float discount_factor = exp(-risk_free_rate * time_to_expiry);
        float price = mean * discount_factor;
        
        option_price[0] = price;
        confidence_interval[0] = price - 1.96f * std_error * discount_factor;
        confidence_interval[1] = price + 1.96f * std_error * discount_factor;
    }
}";

    public const string FilterData = @"
__kernel void filter_data(__global const float* input, __global float* output,
                         int record_count, int dimension_count) {
    int gid = get_global_id(0);
    if(gid >= record_count * dimension_count) return;
    
    float value = input[gid];
    
    // Simple data filtering: remove outliers and normalize
    if(fabs(value) > 50.0f) {
        value = copysign(50.0f, value); // Clamp outliers
    }
    
    output[gid] = value / 50.0f; // Normalize to [-1, 1]
}";

    public const string ComputeStatistics = @"
__kernel void compute_statistics(__global const float* data, __global float* stats,
                                int record_count, int dimension_count) {
    int dim = get_global_id(0);
    if(dim >= dimension_count) return;
    
    float sum = 0.0f;
    float sum_sq = 0.0f;
    float min_val = data[dim];
    float max_val = data[dim];
    
    for(int i = 0; i < record_count; i++) {
        float value = data[i * dimension_count + dim];
        sum += value;
        sum_sq += value * value;
        min_val = fmin(min_val, value);
        max_val = fmax(max_val, value);
    }
    
    float mean = sum / record_count;
    float variance = sum_sq / record_count - mean * mean;
    
    stats[dim * 4 + 0] = mean;
    stats[dim * 4 + 1] = sqrt(variance);
    stats[dim * 4 + 2] = min_val;
    stats[dim * 4 + 3] = max_val;
}";

    public const string AggregateGroups = @"
__kernel void aggregate_groups(__global const float* data, __global const float* group_keys,
                              __global float* results, int record_count, int dimension_count, int group_count) {
    int group_id = get_global_id(0);
    int dim = get_global_id(1);
    
    if(group_id >= group_count || dim >= dimension_count) return;
    
    float sum = 0.0f;
    int count = 0;
    
    for(int i = 0; i < record_count; i++) {
        if((int)group_keys[i] == group_id) {
            sum += data[i * dimension_count + dim];
            count++;
        }
    }
    
    float avg = count > 0 ? sum / count : 0.0f;
    results[group_id * dimension_count + dim] = avg;
}";

    public const string VideoFilter = @"
__kernel void video_filter(__global const float* frame, __global float* output, int width, int height) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    
    if(x >= width || y >= height) return;
    
    int idx = y * width * 3 + x * 3; // RGB
    
    // Simple video filter: enhance contrast and saturation
    for(int c = 0; c < 3; c++) {
        float pixel = frame[idx + c] / 255.0f;
        pixel =(pixel - 0.5f) * 1.2f + 0.5f; // Contrast
        pixel = clamp(pixel, 0.0f, 1.0f);
        output[idx + c] = pixel * 255.0f;
    }
}";

    public const string AudioEffect = @"
__kernel void audio_effect(__global const float* audio, __global float* output) {
    int gid = get_global_id(0);
    
    float sample = audio[gid];
    
    // Simple reverb effect
    float delay_sample = gid > 1000 ? audio[gid - 1000] : 0.0f;
    float processed = sample + 0.3f * delay_sample;
    
    output[gid] = clamp(processed, -1.0f, 1.0f);
}";

    public const string HashFunction = @"
__kernel void hash_function(__global const float* data, __global float* hashes, int iterations) {
    int gid = get_global_id(0);
    
    uint hash =(uint)(data[gid * 16] * 4294967295.0f); // Convert to uint
    
    // Simple hash function with iterations
    for(int i = 0; i < iterations; i++) {
        hash = hash * 1103515245u + 12345u; // Linear congruential generator
        hash ^= hash >> 16;
        hash *= 0x85ebca6bu;
        hash ^= hash >> 13;
        hash *= 0xc2b2ae35u;
        hash ^= hash >> 16;
    }
    
    hashes[gid] =(float)hash / 4294967295.0f; // Convert back to float [0,1]
}";
}
