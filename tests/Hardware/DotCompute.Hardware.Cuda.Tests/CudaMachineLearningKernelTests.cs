// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Tests.Common.Helpers;
using DotCompute.Tests.Common.Specialized;
using Microsoft.Extensions.Logging;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for machine learning kernels on CUDA
    /// </summary>
    public class CudaMachineLearningKernelTests : CudaTestBase
    {
        private readonly CudaAccelerator? _accelerator;
        private readonly ILogger<CudaMachineLearningKernelTests>? _logger;
        private readonly ILoggerFactory? _loggerFactory;
        /// <summary>
        /// Initializes a new instance of the CudaMachineLearningKernelTests class.
        /// </summary>
        /// <param name="output">The output.</param>

        public CudaMachineLearningKernelTests(ITestOutputHelper output) : base(output)
        {
            if (IsCudaAvailable())
            {
                using var factory = new CudaAcceleratorFactory();
                // Create base CUDA accelerator for tests
                _accelerator = new CudaAccelerator(0, Microsoft.Extensions.Logging.Abstractions.NullLogger<CudaAccelerator>.Instance);


                _loggerFactory = LoggerFactory.Create(builder =>

                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
                _logger = _loggerFactory.CreateLogger<CudaMachineLearningKernelTests>();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _accelerator?.DisposeAsync().AsTask().Wait();
                _loggerFactory?.Dispose();
            }
            base.Dispose(disposing);
        }
        /// <summary>
        /// Gets convolution2 d_ should_ compute correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task Convolution2D_Should_ComputeCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - 2D convolution for image processing
            const int inputHeight = 128;
            const int inputWidth = 128;
            const int inputChannels = 3;
            const int outputChannels = 16;
            const int kernelSize = 3;
            const int stride = 1;
            const int padding = 1;


            var outputHeight = (inputHeight + 2 * padding - kernelSize) / stride + 1;
            var outputWidth = (inputWidth + 2 * padding - kernelSize) / stride + 1;

            // Create input image (batch size = 1)

            var input = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(inputHeight * inputWidth * inputChannels, 42);

            // Create convolution kernels

            var kernels = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(
                outputChannels * inputChannels * kernelSize * kernelSize, 43, -0.1f, 0.1f);

            // Create bias

            var bias = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(outputChannels, 44, -0.1f, 0.1f);


            var output = new float[outputHeight * outputWidth * outputChannels];

            // Act
            var perf = new PerformanceMeasurement("2D Convolution", true);
            perf.Start();
            await ExecuteConvolution2D(
                input, kernels, bias, output,
                inputHeight, inputWidth, inputChannels,
                outputChannels, kernelSize, stride, padding);
            _ = perf.Stop();


            var ops = (long)outputHeight * outputWidth * outputChannels *

                      inputChannels * kernelSize * kernelSize * 2; // multiply-add
            perf.LogResults();

            // Assert - verify output is reasonable
            _ = output.Should().NotContain(float.NaN);
            _ = output.Should().NotContain(float.PositiveInfinity);
            _ = output.Should().NotContain(float.NegativeInfinity);


            var outputMean = output.Average();
            var outputStd = MathF.Sqrt(output.Select(x => (x - outputMean) * (x - outputMean)).Average());


            Output.WriteLine($"Output statistics - Mean: {outputMean:F4}, Std: {outputStd:F4}");
            _ = outputStd.Should().BeGreaterThan(0.01f, "Convolution should produce varied output");
        }
        /// <summary>
        /// Gets max pooling2 d_ should_ compute correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task MaxPooling2D_Should_ComputeCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int inputHeight = 64;
            const int inputWidth = 64;
            const int channels = 16;
            const int poolSize = 2;
            const int stride = 2;


            var outputHeight = inputHeight / stride;
            var outputWidth = inputWidth / stride;


            var input = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(inputHeight * inputWidth * channels, 42);
            var output = new float[outputHeight * outputWidth * channels];
            var indices = new int[outputHeight * outputWidth * channels]; // For backprop

            // Act
            await ExecuteMaxPooling2D(
                input, output, indices,
                inputHeight, inputWidth, channels,
                poolSize, stride);

            // Assert - verify max pooling properties
            for (var c = 0; c < channels; c++)
            {
                for (var oh = 0; oh < outputHeight; oh++)
                {
                    for (var ow = 0; ow < outputWidth; ow++)
                    {
                        var outputIdx = (c * outputHeight + oh) * outputWidth + ow;

                        // Find max in corresponding input window

                        var expectedMax = float.MinValue;
                        for (var kh = 0; kh < poolSize; kh++)
                        {
                            for (var kw = 0; kw < poolSize; kw++)
                            {
                                var ih = oh * stride + kh;
                                var iw = ow * stride + kw;
                                if (ih < inputHeight && iw < inputWidth)
                                {
                                    var inputIdx = (c * inputHeight + ih) * inputWidth + iw;
                                    expectedMax = MathF.Max(expectedMax, input[inputIdx]);
                                }
                            }
                        }


                        _ = output[outputIdx].Should().BeApproximately(expectedMax, 0.0001f,
                            $"Max pooling at position ({oh}, {ow}, {c})");
                    }
                }
            }
        }
        /// <summary>
        /// Gets batch normalization_ should_ normalize correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task BatchNormalization_Should_NormalizeCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int batchSize = 32;
            const int features = 256;
            const float epsilon = 1e-5f;
            const float momentum = 0.9f;


            var input = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(batchSize * features, 42, -2.0f, 2.0f);
            var gamma = UnifiedTestHelpers.TestDataGenerator.CreateConstantData(features, 1.0f); // Scale
            var beta = UnifiedTestHelpers.TestDataGenerator.CreateConstantData(features, 0.0f);  // Shift
            var runningMean = new float[features];
            var runningVar = UnifiedTestHelpers.TestDataGenerator.CreateConstantData(features, 1.0f);
            var output = new float[batchSize * features];

            // Act
            await ExecuteBatchNormalization(
                input, gamma, beta, runningMean, runningVar, output,
                batchSize, features, epsilon, momentum, true); // training = true

            // Assert - verify normalization
            for (var f = 0; f < features; f++)
            {
                var featureValues = new float[batchSize];
                for (var b = 0; b < batchSize; b++)
                {
                    featureValues[b] = output[b * features + f];
                }


                var mean = featureValues.Average();
                var variance = featureValues.Select(x => (x - mean) * (x - mean)).Average();
                var std = MathF.Sqrt(variance);


                _ = mean.Should().BeApproximately(0.0f, 0.1f,
                    $"Normalized feature {f} should have mean ≈ 0");
                _ = std.Should().BeApproximately(1.0f, 0.1f,
                    $"Normalized feature {f} should have std ≈ 1");
            }
        }
        /// <summary>
        /// Gets re l u_ should_ apply activation correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task ReLU_Should_ApplyActivationCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int size = 10000;
            var input = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(size, 42, -2.0f, 2.0f);
            var output = new float[size];
            var expected = input.Select(x => MathF.Max(0.0f, x)).ToArray();

            // Act
            await ExecuteReLU(input, output);

            // Assert
            UnifiedTestHelpers.VerifyFloatArraysMatch(expected, output, 0.0f, 1000, "ReLU activation");
        }
        /// <summary>
        /// Gets softmax_ should_ compute probabilities correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task Softmax_Should_ComputeProbabilitiesCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int batchSize = 16;
            const int numClasses = 100;
            var logits = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(batchSize * numClasses, 42, -5.0f, 5.0f);
            var probabilities = new float[batchSize * numClasses];

            // Act
            await ExecuteSoftmax(logits, probabilities, batchSize, numClasses);

            // Assert
            for (var b = 0; b < batchSize; b++)
            {
                var sum = 0.0f;
                var maxProb = 0.0f;


                for (var c = 0; c < numClasses; c++)
                {
                    var idx = b * numClasses + c;
                    _ = probabilities[idx].Should().BeInRange(0.0f, 1.0f,
                        "Softmax output should be probabilities");
                    sum += probabilities[idx];
                    maxProb = MathF.Max(maxProb, probabilities[idx]);
                }


                _ = sum.Should().BeApproximately(1.0f, 0.001f,
                    $"Softmax probabilities for batch {b} should sum to 1");
                _ = maxProb.Should().BeGreaterThan(0.0f,
                    "At least one probability should be non-zero");
            }
        }
        /// <summary>
        /// Gets cross entropy loss_ should_ compute correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task CrossEntropyLoss_Should_ComputeCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int batchSize = 32;
            const int numClasses = 10;

            // Create predictions (after softmax)

            var predictions = new float[batchSize * numClasses];
            var labels = new int[batchSize];
            var random = new Random(42);

            // Generate random predictions and labels

            for (var b = 0; b < batchSize; b++)
            {
                labels[b] = random.Next(numClasses);

                // Create softmax-like predictions

                var sum = 0.0f;
                for (var c = 0; c < numClasses; c++)
                {
                    predictions[b * numClasses + c] = (float)random.NextDouble();
                    sum += predictions[b * numClasses + c];
                }

                // Normalize to sum to 1

                for (var c = 0; c < numClasses; c++)
                {
                    predictions[b * numClasses + c] /= sum;
                }
            }


            var losses = new float[batchSize];

            // Act
            await ExecuteCrossEntropyLoss(predictions, labels, losses, batchSize, numClasses);

            // Assert
            var avgLoss = losses.Average();
            _ = avgLoss.Should().BeGreaterThan(0.0f, "Loss should be positive");
            _ = avgLoss.Should().BeLessThan(10.0f, "Loss should be reasonable");

            // Verify individual losses

            for (var b = 0; b < batchSize; b++)
            {
                var label = labels[b];
                var pred = predictions[b * numClasses + label];
                var expectedLoss = -MathF.Log(MathF.Max(1e-7f, pred));


                _ = losses[b].Should().BeApproximately(expectedLoss, 0.001f,
                    $"Cross-entropy loss for batch {b}");
            }
        }
        /// <summary>
        /// Gets dropout_ should_ mask correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task Dropout_Should_MaskCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int size = 10000;
            const float dropoutRate = 0.5f;
            var input = UnifiedTestHelpers.TestDataGenerator.CreateConstantData(size, 1.0f);
            var output = new float[size];
            var mask = new float[size];

            // Act
            await ExecuteDropout(input, output, mask, dropoutRate, true); // training = true

            // Assert
            var droppedCount = mask.Count(m => m == 0.0f);
            var actualDropRate = (float)droppedCount / size;


            _ = actualDropRate.Should().BeApproximately(dropoutRate, 0.05f,
                "Dropout rate should match expected rate");

            // Check scaling

            var expectedScale = 1.0f / (1.0f - dropoutRate);
            var nonZeroOutputs = output.Where(o => o > 0.0f).ToArray();
            if (nonZeroOutputs.Length > 0)
            {
                _ = nonZeroOutputs.Average().Should().BeApproximately(expectedScale, 0.1f,
                    "Non-dropped outputs should be scaled correctly");
            }
        }
        /// <summary>
        /// Gets l s t m_ should_ process sequence correctly.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "MachineLearning")]
        public async Task LSTM_Should_ProcessSequenceCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - Simple LSTM cell
            const int batchSize = 8;
            const int inputSize = 64;
            const int hiddenSize = 128;
            const int seqLength = 10;

            // Input sequence

            var input = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(
                seqLength * batchSize * inputSize, 42, -1.0f, 1.0f);

            // LSTM weights (simplified - normally would have separate gates)

            var weightsIH = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(
                4 * hiddenSize * inputSize, 43, -0.1f, 0.1f);
            var weightsHH = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(
                4 * hiddenSize * hiddenSize, 44, -0.1f, 0.1f);
            var bias = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(4 * hiddenSize, 45, -0.1f, 0.1f);

            // Initial states

            var h0 = new float[batchSize * hiddenSize];
            var c0 = new float[batchSize * hiddenSize];

            // Output

            var output = new float[seqLength * batchSize * hiddenSize];
            var finalH = new float[batchSize * hiddenSize];
            var finalC = new float[batchSize * hiddenSize];

            // Act
            await ExecuteLSTM(
                input, weightsIH, weightsHH, bias,
                h0, c0, output, finalH, finalC,
                seqLength, batchSize, inputSize, hiddenSize);

            // Assert
            _ = output.Should().NotContain(float.NaN);
            _ = output.Should().NotContain(float.PositiveInfinity);

            // Check that output changes over time

            var firstTimeStep = output.Take(batchSize * hiddenSize).ToArray();
            var lastTimeStep = output.Skip((seqLength - 1) * batchSize * hiddenSize).ToArray();


            var difference = firstTimeStep.Zip(lastTimeStep, (a, b) => MathF.Abs(a - b)).Average();
            _ = difference.Should().BeGreaterThan(0.01f,
                "LSTM output should evolve over time steps");

            // Check hidden state magnitude is reasonable

            var hiddenMagnitude = finalH.Select(MathF.Abs).Average();
            _ = hiddenMagnitude.Should().BeInRange(0.0f, 10.0f,
                "Hidden state should have reasonable magnitude");
        }

        // Helper methods for kernel execution
        private async Task ExecuteConvolution2D(
            float[] input, float[] kernels, float[] bias, float[] output,
            int inputH, int inputW, int inputC,
            int outputC, int kernelSize, int stride, int padding)
        {
            _ = (inputH + 2 * padding - kernelSize) / stride + 1;
            _ = (inputW + 2 * padding - kernelSize) / stride + 1;


            await using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            await using var bufferKernels = await _accelerator.Memory.AllocateAsync<float>(kernels.Length);
            await using var bufferBias = await _accelerator.Memory.AllocateAsync<float>(bias.Length);
            await using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);

            await bufferInput.CopyFromAsync(input);
            await bufferKernels.CopyFromAsync(kernels);
            await bufferBias.CopyFromAsync(bias);

            var kernel = new KernelDefinition
            {
                Name = "conv2d",
                Source = GetConvolution2DKernel(),
                EntryPoint = "conv2d_kernel",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { bufferInput, bufferKernels, bufferBias, bufferOutput },
                // ScalarArguments = new object[] { 
                //     inputH, inputW, inputC, outputC, 
                //     kernelSize, stride, padding, outputH, outputW 
                // }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
        }

        private async Task ExecuteMaxPooling2D(
            float[] input, float[] output, int[] indices,
            int inputH, int inputW, int channels,
            int poolSize, int stride)
        {
            await using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            await using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);
            await using var bufferIndices = await _accelerator.Memory.AllocateAsync<int>(indices.Length);

            await bufferInput.CopyFromAsync(input);

            var kernel = new KernelDefinition
            {
                Name = "max_pooling_2d",
                Source = GetMaxPooling2DKernel(),
                EntryPoint = "max_pooling_2d",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { bufferInput, bufferOutput, bufferIndices },
                // ScalarArguments = new object[] { 
                //     inputH, inputW, channels, poolSize, stride 
                // }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
            await bufferIndices.CopyToAsync(indices);
        }

        private async Task ExecuteBatchNormalization(
            float[] input, float[] gamma, float[] beta,
            float[] runningMean, float[] runningVar, float[] output,
            int batchSize, int features, float epsilon, float momentum, bool training)
        {
            await using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            await using var bufferGamma = await _accelerator.Memory.AllocateAsync<float>(gamma.Length);
            await using var bufferBeta = await _accelerator.Memory.AllocateAsync<float>(beta.Length);
            await using var bufferRunningMean = await _accelerator.Memory.AllocateAsync<float>(runningMean.Length);
            await using var bufferRunningVar = await _accelerator.Memory.AllocateAsync<float>(runningVar.Length);
            await using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);

            await bufferInput.CopyFromAsync(input);
            await bufferGamma.CopyFromAsync(gamma);
            await bufferBeta.CopyFromAsync(beta);
            await bufferRunningMean.CopyFromAsync(runningMean);
            await bufferRunningVar.CopyFromAsync(runningVar);

            var kernel = new KernelDefinition
            {
                Name = "batch_norm",
                Source = GetBatchNormalizationKernel(),
                EntryPoint = "batch_normalization",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { 
                //     bufferInput, bufferGamma, bufferBeta, 
                //     bufferRunningMean, bufferRunningVar, bufferOutput 
                // },
                // ScalarArguments = new object[] { 
                //     batchSize, features, epsilon, momentum, training ? 1 : 0 
                // }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
            await bufferRunningMean.CopyToAsync(runningMean);
            await bufferRunningVar.CopyToAsync(runningVar);
        }

        private async Task ExecuteReLU(float[] input, float[] output)
        {
            await using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            await using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);

            await bufferInput.CopyFromAsync(input);

            var kernel = new KernelDefinition
            {
                Name = "relu",
                Source = GetActivationKernels(),
                EntryPoint = "relu_kernel",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { bufferInput, bufferOutput },
                // ScalarArguments = new object[] { input.Length }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
        }

        private async Task ExecuteSoftmax(float[] logits, float[] probabilities, int batchSize, int numClasses)
        {
            await using var bufferLogits = await _accelerator.Memory.AllocateAsync<float>(logits.Length);
            await using var bufferProbs = await _accelerator.Memory.AllocateAsync<float>(probabilities.Length);

            await bufferLogits.CopyFromAsync(logits);

            var kernel = new KernelDefinition
            {
                Name = "softmax",
                Source = GetActivationKernels(),
                EntryPoint = "softmax_kernel",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { bufferLogits, bufferProbs },
                // ScalarArguments = new object[] { batchSize, numClasses }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferProbs.CopyToAsync(probabilities);
        }

        private async Task ExecuteCrossEntropyLoss(
            float[] predictions, int[] labels, float[] losses,
            int batchSize, int numClasses)
        {
            await using var bufferPreds = await _accelerator.Memory.AllocateAsync<float>(predictions.Length);
            await using var bufferLabels = await _accelerator.Memory.AllocateAsync<int>(labels.Length);
            await using var bufferLosses = await _accelerator.Memory.AllocateAsync<float>(losses.Length);

            await bufferPreds.CopyFromAsync(predictions);
            await bufferLabels.CopyFromAsync(labels);

            var kernel = new KernelDefinition
            {
                Name = "cross_entropy",
                Source = GetLossKernels(),
                EntryPoint = "cross_entropy_loss",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { bufferPreds, bufferLabels, bufferLosses },
                // ScalarArguments = new object[] { batchSize, numClasses }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferLosses.CopyToAsync(losses);
        }

        private async Task ExecuteDropout(
            float[] input, float[] output, float[] mask,
            float dropoutRate, bool training)
        {
            await using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            await using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);
            await using var bufferMask = await _accelerator.Memory.AllocateAsync<float>(mask.Length);

            await bufferInput.CopyFromAsync(input);

            var kernel = new KernelDefinition
            {
                Name = "dropout",
                Source = GetDropoutKernel(),
                EntryPoint = "dropout_kernel",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { bufferInput, bufferOutput, bufferMask },
                // ScalarArguments = new object[] { 
                //     input.Length, dropoutRate, training ? 1 : 0, 42 // seed
                // }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
            await bufferMask.CopyToAsync(mask);
        }

        private async Task ExecuteLSTM(
            float[] input, float[] weightsIH, float[] weightsHH, float[] bias,
            float[] h0, float[] c0, float[] output, float[] hN, float[] cN,
            int seqLength, int batchSize, int inputSize, int hiddenSize)
        {
            await using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            await using var bufferWeightsIH = await _accelerator.Memory.AllocateAsync<float>(weightsIH.Length);
            await using var bufferWeightsHH = await _accelerator.Memory.AllocateAsync<float>(weightsHH.Length);
            await using var bufferBias = await _accelerator.Memory.AllocateAsync<float>(bias.Length);
            await using var bufferH = await _accelerator.Memory.AllocateAsync<float>(h0.Length);
            await using var bufferC = await _accelerator.Memory.AllocateAsync<float>(c0.Length);
            await using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);

            await bufferInput.CopyFromAsync(input);
            await bufferWeightsIH.CopyFromAsync(weightsIH);
            await bufferWeightsHH.CopyFromAsync(weightsHH);
            await bufferBias.CopyFromAsync(bias);
            await bufferH.CopyFromAsync(h0);
            await bufferC.CopyFromAsync(c0);

            var kernel = new KernelDefinition
            {
                Name = "lstm",
                Source = GetLSTMKernel(),
                EntryPoint = "lstm_forward",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                // Buffers = new[] { 
                //     bufferInput, bufferWeightsIH, bufferWeightsHH, bufferBias,
                //     bufferH, bufferC, bufferOutput 
                // },
                // ScalarArguments = new object[] { 
                //     seqLength, batchSize, inputSize, hiddenSize 
                // }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
            await bufferH.CopyToAsync(hN);
            await bufferC.CopyToAsync(cN);
        }

        // CUDA kernel source code
        private static string GetConvolution2DKernel() => @"
extern ""C"" __global__ void conv2d_kernel(
    const float* input, const float* kernels, const float* bias, float* output,
    int inputH, int inputW, int inputC, int outputC,
    int kernelSize, int stride, int padding,
    int outputH, int outputW)
{
    int outputIdx = blockIdx.x * blockDim.x + threadIdx.x;
    int totalOutput = outputH * outputW * outputC;
    
    if (outputIdx >= totalOutput) return;
    
    int ow = outputIdx % outputW;
    int oh = (outputIdx / outputW) % outputH;
    int oc = outputIdx / (outputW * outputH);
    
    float sum = bias[oc];
    
    for (int ic = 0; ic < inputC; ic++) {
        for (int kh = 0; kh < kernelSize; kh++) {
            for (int kw = 0; kw < kernelSize; kw++) {
                int ih = oh * stride - padding + kh;
                int iw = ow * stride - padding + kw;
                
                if (ih >= 0 && ih < inputH && iw >= 0 && iw < inputW) {
                    int inputIdx = (ic * inputH + ih) * inputW + iw;
                    int kernelIdx = ((oc * inputC + ic) * kernelSize + kh) * kernelSize + kw;
                    sum += input[inputIdx] * kernels[kernelIdx];
                }
            }
        }
    }
    
    output[outputIdx] = sum;
}";

        private static string GetMaxPooling2DKernel() => @"
extern ""C"" __global__ void max_pooling_2d(
    const float* input, float* output, int* indices,
    int inputH, int inputW, int channels,
    int poolSize, int stride)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int outputH = (inputH - poolSize) / stride + 1;
    int outputW = (inputW - poolSize) / stride + 1;
    int totalOutput = outputH * outputW * channels;
    
    if (idx >= totalOutput) return;
    
    int ow = idx % outputW;
    int oh = (idx / outputW) % outputH;
    int c = idx / (outputW * outputH);
    
    float maxVal = -1e38f;
    int maxIdx = -1;
    
    for (int ph = 0; ph < poolSize; ph++) {
        for (int pw = 0; pw < poolSize; pw++) {
            int ih = oh * stride + ph;
            int iw = ow * stride + pw;
            
            if (ih < inputH && iw < inputW) {
                int inputIdx = (c * inputH + ih) * inputW + iw;
                if (input[inputIdx] > maxVal) {
                    maxVal = input[inputIdx];
                    maxIdx = inputIdx;
                }
            }
        }
    }
    
    output[idx] = maxVal;
    indices[idx] = maxIdx;
}";

        private static string GetBatchNormalizationKernel() => @"
extern ""C"" __global__ void batch_normalization(
    const float* input, const float* gamma, const float* beta,
    float* running_mean, float* running_var, float* output,
    int batchSize, int features, float epsilon, float momentum, int training)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (idx >= batchSize * features) return;
    
    int f = idx % features;
    int b = idx / features;
    
    if (training) {
        // Compute batch statistics (simplified - normally done separately)
        float mean = 0.0f;
        float var = 0.0f;
        
        for (int i = 0; i < batchSize; i++) {
            mean += input[i * features + f];
        }
        mean /= batchSize;
        
        for (int i = 0; i < batchSize; i++) {
            float diff = input[i * features + f] - mean;
            var += diff * diff;
        }
        var /= batchSize;
        
        // Update running statistics
        if (b == 0) {
            atomicAdd(&running_mean[f], momentum * (mean - running_mean[f]));
            atomicAdd(&running_var[f], momentum * (var - running_var[f]));
        }
        
        // Normalize
        float normalized = (input[idx] - mean) / sqrtf(var + epsilon);
        output[idx] = gamma[f] * normalized + beta[f];
    } else {
        // Use running statistics
        float normalized = (input[idx] - running_mean[f]) / sqrtf(running_var[f] + epsilon);
        output[idx] = gamma[f] * normalized + beta[f];
    }
}";

        private static string GetActivationKernels() => @"
extern ""C"" __global__ void relu_kernel(
    const float* input, float* output, int size)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < size) {
        output[idx] = fmaxf(0.0f, input[idx]);
    }
}

extern ""C"" __global__ void softmax_kernel(
    const float* logits, float* probabilities,
    int batchSize, int numClasses)
{
    int batch = blockIdx.x;
    if (batch >= batchSize) return;
    
    extern __shared__ float shared[];
    
    int tid = threadIdx.x;
    int offset = batch * numClasses;
    
    // Find max for numerical stability
    float maxVal = -1e38f;
    for (int i = tid; i < numClasses; i += blockDim.x) {
        maxVal = fmaxf(maxVal, logits[offset + i]);
    }
    
    // Reduce max across threads
    shared[tid] = maxVal;
    __syncthreads();
    
    for (int s = blockDim.x / 2; s > 0; s >>= 1) {
        if (tid < s) {
            shared[tid] = fmaxf(shared[tid], shared[tid + s]);
        }
        __syncthreads();
    }
    maxVal = shared[0];
    
    // Compute exp and sum
    float sum = 0.0f;
    for (int i = tid; i < numClasses; i += blockDim.x) {
        float exp_val = expf(logits[offset + i] - maxVal);
        if (i < numClasses) {
            probabilities[offset + i] = exp_val;
        }
        sum += exp_val;
    }
    
    // Reduce sum
    shared[tid] = sum;
    __syncthreads();
    
    for (int s = blockDim.x / 2; s > 0; s >>= 1) {
        if (tid < s) {
            shared[tid] += shared[tid + s];
        }
        __syncthreads();
    }
    sum = shared[0];
    
    // Normalize
    for (int i = tid; i < numClasses; i += blockDim.x) {
        probabilities[offset + i] /= sum;
    }
}";

        private static string GetLossKernels() => @"
extern ""C"" __global__ void cross_entropy_loss(
    const float* predictions, const int* labels, float* losses,
    int batchSize, int numClasses)
{
    int batch = blockIdx.x * blockDim.x + threadIdx.x;
    if (batch >= batchSize) return;
    
    int label = labels[batch];
    float pred = predictions[batch * numClasses + label];
    losses[batch] = -logf(fmaxf(1e-7f, pred));
}";

        private static string GetDropoutKernel() => @"
#include <curand_kernel.h>

extern ""C"" __global__ void dropout_kernel(
    const float* input, float* output, float* mask,
    int size, float dropout_rate, int training, unsigned long long seed)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;
    
    if (training) {
        curandState state;
        curand_init(seed, idx, 0, &state);
        float rand = curand_uniform(&state);
        
        if (rand < dropout_rate) {
            mask[idx] = 0.0f;
            output[idx] = 0.0f;
        } else {
            mask[idx] = 1.0f;
            output[idx] = input[idx] / (1.0f - dropout_rate);
        }
    } else {
        mask[idx] = 1.0f;
        output[idx] = input[idx];
    }
}";

        private static string GetLSTMKernel() => @"
extern ""C"" __device__ float sigmoid(float x) {
    return 1.0f / (1.0f + expf(-x));
}

extern ""C"" __global__ void lstm_forward(
    const float* input, const float* weight_ih, const float* weight_hh, const float* bias,
    float* h_prev, float* c_prev, float* output,
    int seq_length, int batch_size, int input_size, int hidden_size)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int total_elements = batch_size * hidden_size;
    
    if (idx >= total_elements) return;
    
    int batch = idx / hidden_size;
    int hid = idx % hidden_size;
    
    for (int t = 0; t < seq_length; t++) {
        // Simplified LSTM - normally would compute all gates
        float i_gate = 0.0f, f_gate = 0.0f, g_gate = 0.0f, o_gate = 0.0f;
        
        // Input contribution
        for (int i = 0; i < input_size; i++) {
            float x = input[t * batch_size * input_size + batch * input_size + i];
            i_gate += x * weight_ih[hid * input_size + i];
            f_gate += x * weight_ih[(hidden_size + hid) * input_size + i];
            g_gate += x * weight_ih[(2 * hidden_size + hid) * input_size + i];
            o_gate += x * weight_ih[(3 * hidden_size + hid) * input_size + i];
        }
        
        // Hidden state contribution
        for (int h = 0; h < hidden_size; h++) {
            float h_val = h_prev[batch * hidden_size + h];
            i_gate += h_val * weight_hh[hid * hidden_size + h];
            f_gate += h_val * weight_hh[(hidden_size + hid) * hidden_size + h];
            g_gate += h_val * weight_hh[(2 * hidden_size + hid) * hidden_size + h];
            o_gate += h_val * weight_hh[(3 * hidden_size + hid) * hidden_size + h];
        }
        
        // Add bias
        i_gate = sigmoid(i_gate + bias[hid]);
        f_gate = sigmoid(f_gate + bias[hidden_size + hid]);
        g_gate = tanhf(g_gate + bias[2 * hidden_size + hid]);
        o_gate = sigmoid(o_gate + bias[3 * hidden_size + hid]);
        
        // Update cell state
        float c_new = f_gate * c_prev[idx] + i_gate * g_gate;
        float h_new = o_gate * tanhf(c_new);
        
        // Store outputs
        output[t * total_elements + idx] = h_new;
        h_prev[idx] = h_new;
        c_prev[idx] = c_new;
    }
}";
    }
}
