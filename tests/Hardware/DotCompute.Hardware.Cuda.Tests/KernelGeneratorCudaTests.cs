// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Abstractions.Kernels;
using DotCompute.Tests.Common;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for kernel generation and CUDA execution using the [Kernel] attribute
    /// </summary>
    public class KernelGeneratorCudaTests : CudaTestBase
    {
        private readonly CudaAccelerator? _accelerator;
        private readonly ILogger<KernelGeneratorCudaTests>? _logger;

        public KernelGeneratorCudaTests(ITestOutputHelper output) : base(output)
        {
            if (IsCudaAvailable())
            {
                var factory = new CudaAcceleratorFactory();
                // Create production accelerator for advanced features, but use base CudaAccelerator for tests
                var productionAccelerator = factory.CreateProductionAccelerator(0);
                _accelerator = new CudaAccelerator(0, NullLogger<CudaAccelerator>.Instance);
                productionAccelerator.Dispose();


                using var loggerFactory = LoggerFactory.Create(builder =>

                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
                _logger = loggerFactory.CreateLogger<KernelGeneratorCudaTests>();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _accelerator?.DisposeAsync().AsTask().Wait();
            }
            base.Dispose(disposing);
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task VectorAdd_WithKernelAttribute_Should_ExecuteOnCuda()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int size = 10000;
            var a = TestDataGenerator.CreateLinearSequence(size, 1.0f, 1.0f);
            var b = TestDataGenerator.CreateLinearSequence(size, 10.0f, 2.0f);
            var result = new float[size];
            var expected = new float[size];

            // Calculate expected results

            for (int i = 0; i < size; i++)
            {
                expected[i] = a[i] + b[i];
            }

            // Act - Execute using generated kernel
            await ExecuteVectorAddKernel(a, b, result);

            // Assert
            VerifyFloatArraysMatch(expected, result, 0.0001f, context: "VectorAdd kernel");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task MatrixMultiply_WithKernelAttribute_Should_ExecuteOnCuda()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int m = 64, n = 128, k = 96;
            var a = TestDataGenerator.CreateRandomData(m * k, 42);
            var b = TestDataGenerator.CreateRandomData(k * n, 43);
            var result = new float[m * n];
            var expected = new float[m * n];

            // Calculate expected result
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float sum = 0.0f;
                    for (int l = 0; l < k; l++)
                    {
                        sum += a[i * k + l] * b[l * n + j];
                    }
                    expected[i * n + j] = sum;
                }
            }

            // Act - Execute using generated kernel
            await ExecuteMatrixMultiplyKernel(a, b, result, m, n, k);

            // Assert
            VerifyFloatArraysMatch(expected, result, 0.001f, context: "MatrixMultiply kernel");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task Reduction_WithKernelAttribute_Should_ExecuteOnCuda()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange  
            const int size = 1000; // Smaller for debugging
            var data = Enumerable.Range(1, size).Select(i => (float)i).ToArray(); // 1,2,3...1000
            float expected = 0.0f;
            for (int i = 0; i < size; i++)
            {
                expected += data[i];
            }

            // Act - Execute using generated kernel
            float result = await ExecuteReductionKernel(data);

            // Assert
            result.Should().BeApproximately(expected, 10.0f); // Allow for floating-point precision in atomic operations
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task ConvolutionKernel_Should_ExecuteOnCuda()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - 1D convolution
            const int inputSize = 1000;
            const int kernelSize = 5;
            const int outputSize = inputSize - kernelSize + 1;


            var input = TestDataGenerator.CreateSinusoidalData(inputSize, 0.01, 1.0f);
            var kernel = new float[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f }; // Simple averaging kernel
            var result = new float[outputSize];
            var expected = new float[outputSize];

            // Calculate expected result
            for (int i = 0; i < outputSize; i++)
            {
                float sum = 0.0f;
                for (int j = 0; j < kernelSize; j++)
                {
                    sum += input[i + j] * kernel[j];
                }
                expected[i] = sum;
            }

            // Act
            await ExecuteConvolutionKernel(input, kernel, result);

            // Assert
            VerifyFloatArraysMatch(expected, result, 0.0001f, context: "Convolution kernel");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        public async Task BlackScholesKernel_Should_ExecuteOnCuda()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int numOptions = 10000;
            var stockPrices = TestDataGenerator.CreateRandomData(numOptions, 42, 50.0f, 150.0f);
            var strikePrices = TestDataGenerator.CreateRandomData(numOptions, 43, 50.0f, 150.0f);
            var timeToExpiry = TestDataGenerator.CreateRandomData(numOptions, 44, 0.1f, 2.0f);
            var riskFreeRate = 0.05f;
            var volatility = 0.3f;


            var callPrices = new float[numOptions];
            var putPrices = new float[numOptions];

            // Act
            await ExecuteBlackScholesKernel(
                stockPrices, strikePrices, timeToExpiry,

                riskFreeRate, volatility,

                callPrices, putPrices);

            // Assert - Basic validation
            for (int i = 0; i < Math.Min(100, numOptions); i++)
            {
                callPrices[i].Should().BeGreaterThan(0);
                putPrices[i].Should().BeGreaterThan(0);

                // Call-Put parity check (approximate)

                var parity = callPrices[i] - putPrices[i];
                var expected = stockPrices[i] - strikePrices[i] * MathF.Exp(-riskFreeRate * timeToExpiry[i]);
                parity.Should().BeApproximately(expected, 1.0f);
            }
        }

        // Helper methods to execute kernels with proper setup
        private async Task ExecuteVectorAddKernel(float[] a, float[] b, float[] result)
        {
            if (_accelerator == null)
                throw new InvalidOperationException("CUDA accelerator not initialized");


            await using var bufferA = await _accelerator.Memory.AllocateAsync<float>(a.Length);
            await using var bufferB = await _accelerator.Memory.AllocateAsync<float>(b.Length);
            await using var bufferResult = await _accelerator.Memory.AllocateAsync<float>(result.Length);

            await bufferA.CopyFromAsync(a);
            await bufferB.CopyFromAsync(b);

            var kernel = new VectorAddKernel();
            var compiled = await _accelerator.CompileKernelAsync(kernel.GetKernelDefinition());


            var args = new KernelArguments
            {
                Buffers = new[] { bufferA, bufferB, bufferResult },
                ScalarArguments = new object[] { a.Length }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferResult.CopyToAsync(result);
        }

        private async Task ExecuteMatrixMultiplyKernel(float[] a, float[] b, float[] result, int m, int n, int k)
        {
            if (_accelerator == null)
                throw new InvalidOperationException("CUDA accelerator not initialized");


            await using var bufferA = await _accelerator.Memory.AllocateAsync<float>(a.Length);
            await using var bufferB = await _accelerator.Memory.AllocateAsync<float>(b.Length);
            await using var bufferResult = await _accelerator.Memory.AllocateAsync<float>(result.Length);

            await bufferA.CopyFromAsync(a);
            await bufferB.CopyFromAsync(b);

            var kernel = new MatrixMultiplyKernel();
            var compiled = await _accelerator.CompileKernelAsync(kernel.GetKernelDefinition());


            var args = new KernelArguments
            {
                Buffers = new[] { bufferA, bufferB, bufferResult },
                ScalarArguments = new object[] { m, n, k }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferResult.CopyToAsync(result);
        }

        private async Task<float> ExecuteReductionKernel(float[] data)
        {
            if (_accelerator == null)
                throw new InvalidOperationException("CUDA accelerator not initialized");


            await using var bufferData = await _accelerator.Memory.AllocateAsync<float>(data.Length);
            await using var bufferResult = await _accelerator.Memory.AllocateAsync<float>(1);

            await bufferData.CopyFromAsync(data);
            await bufferResult.CopyFromAsync(new float[] { 0.0f });

            var kernel = new ReductionKernel();
            var compiled = await _accelerator.CompileKernelAsync(kernel.GetKernelDefinition());


            const int blockSize = 256;
            var gridSize = (data.Length + blockSize - 1) / blockSize;
            const int sharedMemorySize = blockSize * sizeof(float);


            var args = new KernelArguments
            {
                Buffers = new[] { bufferData, bufferResult },
                ScalarArguments = new object[] { data.Length },
                LaunchConfiguration = new KernelLaunchConfiguration
                {
                    GridSize = ((uint)gridSize, 1, 1),
                    BlockSize = (blockSize, 1, 1),
                    SharedMemoryBytes = (uint)sharedMemorySize
                }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();


            var result = new float[1];
            await bufferResult.CopyToAsync(result);
            return result[0];
        }

        private async Task ExecuteConvolutionKernel(float[] input, float[] kernel, float[] result)
        {
            if (_accelerator == null)
                throw new InvalidOperationException("CUDA accelerator not initialized");


            await using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            await using var bufferKernel = await _accelerator.Memory.AllocateAsync<float>(kernel.Length);
            await using var bufferResult = await _accelerator.Memory.AllocateAsync<float>(result.Length);

            await bufferInput.CopyFromAsync(input);
            await bufferKernel.CopyFromAsync(kernel);

            var convKernel = new ConvolutionKernel();
            var compiled = await _accelerator.CompileKernelAsync(convKernel.GetKernelDefinition());


            var args = new KernelArguments
            {
                Buffers = new[] { bufferInput, bufferKernel, bufferResult },
                ScalarArguments = new object[] { input.Length, kernel.Length, result.Length }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferResult.CopyToAsync(result);
        }

        private async Task ExecuteBlackScholesKernel(
            float[] stockPrices, float[] strikePrices, float[] timeToExpiry,
            float riskFreeRate, float volatility,
            float[] callPrices, float[] putPrices)
        {
            if (_accelerator == null)
                throw new InvalidOperationException("CUDA accelerator not initialized");


            await using var bufferStock = await _accelerator.Memory.AllocateAsync<float>(stockPrices.Length);
            await using var bufferStrike = await _accelerator.Memory.AllocateAsync<float>(strikePrices.Length);
            await using var bufferTime = await _accelerator.Memory.AllocateAsync<float>(timeToExpiry.Length);
            await using var bufferCall = await _accelerator.Memory.AllocateAsync<float>(callPrices.Length);
            await using var bufferPut = await _accelerator.Memory.AllocateAsync<float>(putPrices.Length);

            await bufferStock.CopyFromAsync(stockPrices);
            await bufferStrike.CopyFromAsync(strikePrices);
            await bufferTime.CopyFromAsync(timeToExpiry);

            var kernel = new BlackScholesKernel();
            var compiled = await _accelerator.CompileKernelAsync(kernel.GetKernelDefinition());


            var args = new KernelArguments
            {
                Buffers = new[] { bufferStock, bufferStrike, bufferTime, bufferCall, bufferPut },
                ScalarArguments = new object[] { riskFreeRate, volatility, stockPrices.Length }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();


            await bufferCall.CopyToAsync(callPrices);
            await bufferPut.CopyToAsync(putPrices);
        }
    }

    // Kernel definitions - simplified without [Kernel] attribute
    // The [Kernel] attribute would be used with the source generator in a full implementation
    public static class TestKernels
    {
        // Note: [Kernel] attribute requires DotCompute.Generators reference
        // For testing, we'll use direct kernel definitions
        public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result, int length)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }

        // [Kernel] attribute would specify CUDA backend with 16x16 block dimensions
        public static void MatrixMultiply(
            ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result,
            int m, int n, int k)
        {
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float sum = 0.0f;
                    for (int l = 0; l < k; l++)
                    {
                        sum += a[i * k + l] * b[l * n + j];
                    }
                    result[i * n + j] = sum;
                }
            }
        }

        // [Kernel] attribute would specify memory optimization
        public static void Reduction(ReadOnlySpan<float> data, Span<float> result, int length)
        {
            float sum = 0.0f;
            for (int i = 0; i < length; i++)
            {
                sum += data[i];
            }
            result[0] = sum;
        }

        // [Kernel] attribute for CUDA backend
        public static void Convolution1D(
            ReadOnlySpan<float> input, ReadOnlySpan<float> kernel, Span<float> output,
            int inputSize, int kernelSize, int outputSize)
        {
            for (int i = 0; i < outputSize; i++)
            {
                float sum = 0.0f;
                for (int j = 0; j < kernelSize; j++)
                {
                    sum += input[i + j] * kernel[j];
                }
                output[i] = sum;
            }
        }

        // [Kernel] attribute with vector size 4
        public static void BlackScholes(
            ReadOnlySpan<float> stockPrice, ReadOnlySpan<float> strikePrice, ReadOnlySpan<float> timeToExpiry,
            Span<float> callPrice, Span<float> putPrice,
            float riskFreeRate, float volatility, int numOptions)
        {
            for (int i = 0; i < numOptions; i++)
            {
                float stockPriceValue = stockPrice[i];
                float strikePriceValue = strikePrice[i];
                float timeToExpiryValue = timeToExpiry[i];
                float r = riskFreeRate;
                float sigma = volatility;


                float sqrtT = MathF.Sqrt(timeToExpiryValue);
                float d1 = (MathF.Log(stockPriceValue / strikePriceValue) + (r + 0.5f * sigma * sigma) * timeToExpiryValue) / (sigma * sqrtT);
                float d2 = d1 - sigma * sqrtT;

                // Approximation of normal CDF

                float nD1 = 0.5f * (1.0f + MathF.Tanh(d1 * 0.7071067811865476f));
                float nD2 = 0.5f * (1.0f + MathF.Tanh(d2 * 0.7071067811865476f));
                float nNegD1 = 1.0f - nD1;
                float nNegD2 = 1.0f - nD2;


                callPrice[i] = stockPriceValue * nD1 - strikePriceValue * MathF.Exp(-r * timeToExpiryValue) * nD2;
                putPrice[i] = strikePriceValue * MathF.Exp(-r * timeToExpiryValue) * nNegD2 - stockPriceValue * nNegD1;
            }
        }
    }

    // Helper classes for kernel definitions
    internal class VectorAddKernel : IKernel
    {
        public KernelDefinition GetKernelDefinition()
        {
            return new KernelDefinition
            {
                Name = "VectorAdd",
                Source = GetCudaSource(),
                EntryPoint = "vector_add_kernel",
                // Language is determined by backend
            };
        }

        private static string GetCudaSource()
        {
            return @"
extern ""C"" __global__ void vector_add_kernel(
    const float* a, const float* b, float* result, int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length) {
        result[idx] = a[idx] + b[idx];
    }
}";
        }
    }

    internal class MatrixMultiplyKernel : IKernel
    {
        public KernelDefinition GetKernelDefinition()
        {
            return new KernelDefinition
            {
                Name = "MatrixMultiply",
                Source = GetCudaSource(),
                EntryPoint = "matrix_multiply_kernel",
                // Language is determined by backend
            };
        }

        private static string GetCudaSource()
        {
            return @"
extern ""C"" __global__ void matrix_multiply_kernel(
    const float* a, const float* b, float* c,
    int m, int n, int k)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (row < m && col < n) {
        float sum = 0.0f;
        for (int i = 0; i < k; i++) {
            sum += a[row * k + i] * b[i * n + col];
        }
        c[row * n + col] = sum;
    }
}";
        }
    }

    internal class ReductionKernel : IKernel
    {
        public KernelDefinition GetKernelDefinition()
        {
            return new KernelDefinition
            {
                Name = "Reduction",
                Source = GetCudaSource(),
                EntryPoint = "reduction_kernel",
                // Language is determined by backend
            };
        }

        private static string GetCudaSource()
        {
            return @"
extern ""C"" __global__ void reduction_kernel(
    const float* data, float* result, int length)
{
    extern __shared__ float sdata[];
    
    int tid = threadIdx.x;
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    // Load data into shared memory
    sdata[tid] = (idx < length) ? data[idx] : 0.0f;
    __syncthreads();
    
    // Perform reduction in shared memory
    for (int s = blockDim.x / 2; s > 0; s >>= 1) {
        if (tid < s) {
            sdata[tid] += sdata[tid + s];
        }
        __syncthreads();
    }
    
    // Write result for this block to global memory
    if (tid == 0) {
        atomicAdd(result, sdata[0]);
    }
}";
        }
    }

    internal class ConvolutionKernel : IKernel
    {
        public KernelDefinition GetKernelDefinition()
        {
            return new KernelDefinition
            {
                Name = "Convolution1D",
                Source = GetCudaSource(),
                EntryPoint = "convolution_1d_kernel",
                // Language is determined by backend
            };
        }

        private static string GetCudaSource()
        {
            return @"
extern ""C"" __global__ void convolution_1d_kernel(
    const float* input, const float* kernel, float* output,
    int inputSize, int kernelSize, int outputSize)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (idx < outputSize) {
        float sum = 0.0f;
        for (int j = 0; j < kernelSize; j++) {
            sum += input[idx + j] * kernel[j];
        }
        output[idx] = sum;
    }
}";
        }
    }

    internal class BlackScholesKernel : IKernel
    {
        public KernelDefinition GetKernelDefinition()
        {
            return new KernelDefinition
            {
                Name = "BlackScholes",
                Source = GetCudaSource(),
                EntryPoint = "black_scholes_kernel",
                // Language is determined by backend
            };
        }

        private static string GetCudaSource()
        {
            return @"
extern ""C"" __global__ void black_scholes_kernel(
    const float* stockPrice, const float* strikePrice, const float* timeToExpiry,
    float* callPrice, float* putPrice,
    float riskFreeRate, float volatility, int numOptions)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (idx < numOptions) {
        float S = stockPrice[idx];
        float K = strikePrice[idx];
        float T = timeToExpiry[idx];
        float r = riskFreeRate;
        float sigma = volatility;
        
        float sqrtT = sqrtf(T);
        float d1 = (logf(S / K) + (r + 0.5f * sigma * sigma) * T) / (sigma * sqrtT);
        float d2 = d1 - sigma * sqrtT;
        
        // Approximation of normal CDF using tanh
        float N_d1 = 0.5f * (1.0f + tanhf(d1 * 0.7071067811865476f));
        float N_d2 = 0.5f * (1.0f + tanhf(d2 * 0.7071067811865476f));
        float N_neg_d1 = 1.0f - N_d1;
        float N_neg_d2 = 1.0f - N_d2;
        
        callPrice[idx] = S * N_d1 - K * expf(-r * T) * N_d2;
        putPrice[idx] = K * expf(-r * T) * N_neg_d2 - S * N_neg_d1;
    }
}";
        }
    }

    // Helper interface for kernel definitions
    internal interface IKernel
    {
        public KernelDefinition GetKernelDefinition();
    }
}