// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using FluentAssertions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Integration tests for Metal backend with real-world computation workflows.
/// Tests end-to-end scenarios, multi-kernel pipelines, and complex memory patterns.
/// </summary>
[Trait("Category", "RequiresMetal")]
[Trait("Category", "Integration")]
[Trait("Category", "LongRunning")]
public class MetalIntegrationTests : MetalTestBase
{
    public MetalIntegrationTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Image_Processing_Pipeline_Should_Work_End_To_End()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        // Simulate a 1024x1024 RGBA image processing pipeline
        const int width = 1024;
        const int height = 1024;
        const int channels = 4; // RGBA
        const int imageSize = width * height * channels;

        // Generate test image data
        var inputImage = MetalTestUtilities.TestDataGenerator.CreateRandomData(imageSize, seed: 42, min: 0.0f, max: 1.0f);
        var outputImage = new float[imageSize];

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(imageSize);
        await using var deviceTemp1 = await accelerator.Memory.AllocateAsync<float>(imageSize);
        await using var deviceTemp2 = await accelerator.Memory.AllocateAsync<float>(imageSize);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(imageSize);

        // Upload input image
        await deviceInput.CopyFromAsync(inputImage.AsMemory());

        // Step 1: Gaussian blur kernel
        var blurKernel = new KernelDefinition
        {
            Name = "gaussian_blur",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void gaussian_blur(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    constant uint& width [[buffer(2)]],
                    constant uint& height [[buffer(3)]],
                    uint2 gid [[thread_position_in_grid]]
                ) {
                    const uint x = gid.x;
                    const uint y = gid.y;
                    
                    if (x >= width || y >= height) return;
                    
                    const uint channels = 4;
                    const uint idx = (y * width + x) * channels;
                    
                    // Simple 3x3 Gaussian blur
                    const float kernel[9] = {
                        0.0625f, 0.125f, 0.0625f,
                        0.125f,  0.25f,  0.125f,
                        0.0625f, 0.125f, 0.0625f
                    };
                    
                    for (uint c = 0; c < channels; c++) {
                        float sum = 0.0f;
                        for (int dy = -1; dy <= 1; dy++) {
                            for (int dx = -1; dx <= 1; dx++) {
                                int nx = int(x) + dx;
                                int ny = int(y) + dy;
                                
                                if (nx >= 0 && nx < int(width) && ny >= 0 && ny < int(height)) {
                                    uint sidx = (uint(ny) * width + uint(nx)) * channels + c;
                                    uint kidx = uint((dy + 1) * 3 + (dx + 1));
                                    sum += input[sidx] * kernel[kidx];
                                }
                            }
                        }
                        output[idx + c] = sum;
                    }
                }",
            EntryPoint = "gaussian_blur"
        };

        // Step 2: Edge detection kernel
        var edgeKernel = new KernelDefinition
        {
            Name = "edge_detection",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void edge_detection(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    constant uint& width [[buffer(2)]],
                    constant uint& height [[buffer(3)]],
                    uint2 gid [[thread_position_in_grid]]
                ) {
                    const uint x = gid.x;
                    const uint y = gid.y;
                    
                    if (x >= width || y >= height) return;
                    
                    const uint channels = 4;
                    const uint idx = (y * width + x) * channels;
                    
                    // Sobel operator
                    const float sobelX[9] = {
                        -1.0f, 0.0f, 1.0f,
                        -2.0f, 0.0f, 2.0f,
                        -1.0f, 0.0f, 1.0f
                    };
                    
                    const float sobelY[9] = {
                        -1.0f, -2.0f, -1.0f,
                         0.0f,  0.0f,  0.0f,
                         1.0f,  2.0f,  1.0f
                    };
                    
                    for (uint c = 0; c < 3; c++) { // Skip alpha channel
                        float gx = 0.0f, gy = 0.0f;
                        
                        for (int dy = -1; dy <= 1; dy++) {
                            for (int dx = -1; dx <= 1; dx++) {
                                int nx = int(x) + dx;
                                int ny = int(y) + dy;
                                
                                if (nx >= 0 && nx < int(width) && ny >= 0 && ny < int(height)) {
                                    uint sidx = (uint(ny) * width + uint(nx)) * channels + c;
                                    uint kidx = uint((dy + 1) * 3 + (dx + 1));
                                    float pixel = input[sidx];
                                    gx += pixel * sobelX[kidx];
                                    gy += pixel * sobelY[kidx];
                                }
                            }
                        }
                        
                        float magnitude = sqrt(gx * gx + gy * gy);
                        output[idx + c] = min(magnitude, 1.0f);
                    }
                    output[idx + 3] = input[idx + 3]; // Preserve alpha
                }",
            EntryPoint = "edge_detection"
        };

        // Step 3: Color enhancement kernel
        var enhanceKernel = new KernelDefinition
        {
            Name = "color_enhance",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void color_enhance(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    constant float& contrast [[buffer(2)]],
                    constant float& brightness [[buffer(3)]],
                    constant uint& size [[buffer(4)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= size) return;
                    
                    float pixel = input[id];
                    // Apply contrast and brightness
                    pixel = (pixel - 0.5f) * contrast + 0.5f + brightness;
                    output[id] = clamp(pixel, 0.0f, 1.0f);
                }",
            EntryPoint = "color_enhance"
        };

        var compiledBlur = await accelerator.CompileKernelAsync(blurKernel);
        var compiledEdge = await accelerator.CompileKernelAsync(edgeKernel);
        var compiledEnhance = await accelerator.CompileKernelAsync(enhanceKernel);

        var threadgroupSize = 16;
        var threadgroupsX = (width + threadgroupSize - 1) / threadgroupSize;
        var threadgroupsY = (height + threadgroupSize - 1) / threadgroupSize;
        var gridSize2D = (threadgroupsX, threadgroupsY, 1);
        var threadSize2D = (threadgroupSize, threadgroupSize, 1);

        var threadgroups1D = (imageSize + 256 - 1) / 256;
        var gridSize1D = (threadgroups1D, 1, 1);
        var threadSize1D = (256, 1, 1);

        var stopwatch = Stopwatch.StartNew();

        // Execute pipeline: Input -> Blur -> Edge Detection -> Enhancement -> Output

        // Step 1: Gaussian blur
        var blurArgs = new KernelArguments { deviceInput, deviceTemp1, (uint)width, (uint)height };
        await compiledBlur.LaunchAsync(gridSize2D, threadSize2D, blurArgs);

        // Step 2: Edge detection
        var edgeArgs = new KernelArguments { deviceTemp1, deviceTemp2, (uint)width, (uint)height };
        await compiledEdge.LaunchAsync(gridSize2D, threadSize2D, edgeArgs);

        // Step 3: Color enhancement
        var enhanceArgs = new KernelArguments { deviceTemp2, deviceOutput, 1.2f, 0.1f, (uint)imageSize };
        await compiledEnhance.LaunchAsync(gridSize1D, threadSize1D, enhanceArgs);

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Download results
        await deviceOutput.CopyToAsync(outputImage.AsMemory());

        // Validate results
        var processedPixels = 0;
        var validPixels = 0;

        for (var i = 0; i < imageSize; i++)
        {
            if (i % channels != 3) // Skip alpha channel
            {
                processedPixels++;
                if (outputImage[i] >= 0.0f && outputImage[i] <= 1.0f)
                {
                    validPixels++;
                }
            }
        }

        var validityRatio = (double)validPixels / processedPixels;

        Output.WriteLine($"Image Processing Pipeline Results:");
        Output.WriteLine($"  Image size: {width}x{height} ({imageSize:N0} elements)");
        Output.WriteLine($"  Processing time: {stopwatch.ElapsedMilliseconds:F2} ms");
        Output.WriteLine($"  Throughput: {imageSize / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024):F2} Mpixels/s");
        Output.WriteLine($"  Valid pixels: {validPixels}/{processedPixels} ({validityRatio:P1})");

        // Performance and correctness assertions
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Pipeline should complete within reasonable time");
        validityRatio.Should().BeGreaterThan(0.95, "Most pixels should have valid values");

        // Check that processing actually changed the image
        var significantChanges = 0;
        for (var i = 0; i < imageSize; i += channels)
        {
            var inputPixel = (inputImage[i] + inputImage[i + 1] + inputImage[i + 2]) / 3.0f;
            var outputPixel = (outputImage[i] + outputImage[i + 1] + outputImage[i + 2]) / 3.0f;

            if (Math.Abs(inputPixel - outputPixel) > 0.1f)
            {
                significantChanges++;
            }
        }

        var changeRatio = (double)significantChanges / (imageSize / channels);
        Output.WriteLine($"  Pixels significantly changed: {significantChanges}/{imageSize / channels} ({changeRatio:P1})");

        changeRatio.Should().BeGreaterThan(0.5, "Processing should significantly change the image");
    }

    [SkippableFact]
    public async Task Matrix_Operations_Chain_Should_Work_Correctly()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        // Test a chain of matrix operations: A * B + C * D = Result
        const int matrixSize = 512;
        const int elementCount = matrixSize * matrixSize;

        // Generate test matrices
        var matrixA = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 1);
        var matrixB = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 2);
        var matrixC = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 3);
        var matrixD = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 4);

        // Allocate device memory
        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceD = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceTemp1 = await accelerator.Memory.AllocateAsync<float>(elementCount); // A * B
        await using var deviceTemp2 = await accelerator.Memory.AllocateAsync<float>(elementCount); // C * D
        await using var deviceResult = await accelerator.Memory.AllocateAsync<float>(elementCount); // Final result

        // Upload matrices
        await Task.WhenAll(
            deviceA.CopyFromAsync(matrixA.AsMemory()).AsTask(),
            deviceB.CopyFromAsync(matrixB.AsMemory()).AsTask(),
            deviceC.CopyFromAsync(matrixC.AsMemory()).AsTask(),
            deviceD.CopyFromAsync(matrixD.AsMemory()).AsTask()
        );

        // Matrix multiply kernel
        var matmulKernel = new KernelDefinition
        {
            Name = "matrix_multiply",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void matrix_multiply(
                    device float* A [[buffer(0)]],
                    device float* B [[buffer(1)]],
                    device float* C [[buffer(2)]],
                    constant uint& N [[buffer(3)]],
                    uint2 gid [[thread_position_in_grid]]
                ) {
                    const uint row = gid.y;
                    const uint col = gid.x;
                    
                    if (row >= N || col >= N) return;
                    
                    float sum = 0.0f;
                    for (uint k = 0; k < N; k++) {
                        sum += A[row * N + k] * B[k * N + col];
                    }
                    C[row * N + col] = sum;
                }",
            EntryPoint = "matrix_multiply"
        };

        // Matrix addition kernel
        var addKernel = new KernelDefinition
        {
            Name = "matrix_add",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void matrix_add(
                    device float* A [[buffer(0)]],
                    device float* B [[buffer(1)]],
                    device float* C [[buffer(2)]],
                    constant uint& size [[buffer(3)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= size) return;
                    C[id] = A[id] + B[id];
                }",
            EntryPoint = "matrix_add"
        };

        var compiledMatmul = await accelerator.CompileKernelAsync(matmulKernel);
        var compiledAdd = await accelerator.CompileKernelAsync(addKernel);

        var threadgroupSize = 16;
        var threadgroups2D = ((matrixSize + threadgroupSize - 1) / threadgroupSize,
                             (matrixSize + threadgroupSize - 1) / threadgroupSize, 1);
        var threadSize2D = (threadgroupSize, threadgroupSize, 1);

        var threadgroups1D = (elementCount + 256 - 1) / 256;
        var gridSize1D = (threadgroups1D, 1, 1);
        var threadSize1D = (256, 1, 1);

        var stopwatch = Stopwatch.StartNew();

        // Execute: A * B -> temp1
        var args1 = new KernelArguments { deviceA, deviceB, deviceTemp1, (uint)matrixSize };
        await compiledMatmul.LaunchAsync(threadgroups2D, threadSize2D, args1);

        // Execute: C * D -> temp2
        var args2 = new KernelArguments { deviceC, deviceD, deviceTemp2, (uint)matrixSize };
        await compiledMatmul.LaunchAsync(threadgroups2D, threadSize2D, args2);

        // Execute: temp1 + temp2 -> result
        var args3 = new KernelArguments { deviceTemp1, deviceTemp2, deviceResult, (uint)elementCount };
        await compiledAdd.LaunchAsync(gridSize1D, threadSize1D, args3);

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Download and verify results
        var deviceResultData = new float[elementCount];
        await deviceResult.CopyToAsync(deviceResultData.AsMemory());

        // Compute expected result on CPU (for a small subset)
        var cpuResult = new float[elementCount];

        // CPU matrix multiply A * B -> temp1
        var temp1 = new float[elementCount];
        for (var row = 0; row < Math.Min(32, matrixSize); row++)
        {
            for (var col = 0; col < Math.Min(32, matrixSize); col++)
            {
                var sum = 0.0f;
                for (var k = 0; k < matrixSize; k++)
                {
                    sum += matrixA[row * matrixSize + k] * matrixB[k * matrixSize + col];
                }
                temp1[row * matrixSize + col] = sum;
            }
        }

        // CPU matrix multiply C * D -> temp2
        var temp2 = new float[elementCount];
        for (var row = 0; row < Math.Min(32, matrixSize); row++)
        {
            for (var col = 0; col < Math.Min(32, matrixSize); col++)
            {
                var sum = 0.0f;
                for (var k = 0; k < matrixSize; k++)
                {
                    sum += matrixC[row * matrixSize + k] * matrixD[k * matrixSize + col];
                }
                temp2[row * matrixSize + col] = sum;
            }
        }

        // CPU addition: temp1 + temp2
        for (var i = 0; i < Math.Min(32 * 32, elementCount); i++)
        {
            cpuResult[i] = temp1[i] + temp2[i];
        }

        // Verify results (check first 32x32 block)
        var errors = 0;
        var tolerance = 0.1f; // Relaxed tolerance for accumulated floating point errors

        for (var i = 0; i < Math.Min(32 * 32, elementCount); i++)
        {
            var diff = Math.Abs(deviceResultData[i] - cpuResult[i]);
            if (diff > tolerance)
            {
                errors++;
                if (errors <= 5) // Report first 5 errors
                {
                    var row = i / matrixSize;
                    var col = i % matrixSize;
                    Output.WriteLine($"Error at ({row},{col}): GPU={deviceResultData[i]:F6}, CPU={cpuResult[i]:F6}, diff={diff:F6}");
                }
            }
        }

        var errorRate = (double)errors / Math.Min(32 * 32, elementCount);

        var totalOps = 2.0 * matrixSize * matrixSize * matrixSize * 2; // Two matrix multiplies
        var gflops = totalOps / (stopwatch.Elapsed.TotalSeconds * 1e9);

        Output.WriteLine($"Matrix Operations Chain Results:");
        Output.WriteLine($"  Matrix size: {matrixSize}x{matrixSize}");
        Output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds:F2} ms");
        Output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
        Output.WriteLine($"  Verification errors: {errors}/{Math.Min(32 * 32, elementCount)} ({errorRate:P2})");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Matrix chain should complete in reasonable time");
        errorRate.Should().BeLessThan(0.01, "Error rate should be less than 1%");
        gflops.Should().BeGreaterThan(10.0, "Should achieve reasonable performance");
    }

    [SkippableFact]
    public async Task Scientific_Simulation_Workflow_Should_Execute_Correctly()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        // Simulate a 2D heat equation: ∂u/∂t = α(∂²u/∂x² + ∂²u/∂y²)
        const int gridSize = 512;
        const int totalElements = gridSize * gridSize;
        const int timeSteps = 100;
        const float alpha = 0.01f;
        const float dx = 1.0f;
        const float dt = 0.1f;

        // Initialize temperature field with a hot spot in the center
        var initialTemp = new float[totalElements];
        for (var y = 0; y < gridSize; y++)
        {
            for (var x = 0; x < gridSize; x++)
            {
                var centerX = gridSize / 2.0f;
                var centerY = gridSize / 2.0f;
                var distance = MathF.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));

                // Hot spot with Gaussian distribution
                initialTemp[y * gridSize + x] = (float)(100.0 * Math.Exp(-distance * distance / (2.0 * 50.0 * 50.0)));
            }
        }

        await using var deviceTemp1 = await accelerator.Memory.AllocateAsync<float>(totalElements);
        await using var deviceTemp2 = await accelerator.Memory.AllocateAsync<float>(totalElements);

        // Upload initial conditions
        await deviceTemp1.CopyFromAsync(initialTemp.AsMemory());

        // Heat equation kernel (finite difference)
        var heatKernel = new KernelDefinition
        {
            Name = "heat_equation",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void heat_equation(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    constant uint& width [[buffer(2)]],
                    constant uint& height [[buffer(3)]],
                    constant float& alpha [[buffer(4)]],
                    constant float& dt [[buffer(5)]],
                    constant float& dx [[buffer(6)]],
                    uint2 gid [[thread_position_in_grid]]
                ) {
                    const uint x = gid.x;
                    const uint y = gid.y;
                    
                    if (x >= width || y >= height) return;
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1) {
                        // Boundary conditions (fixed temperature)
                        output[y * width + x] = 0.0f;
                        return;
                    }
                    
                    const uint idx = y * width + x;
                    const float current = input[idx];
                    const float left = input[idx - 1];
                    const float right = input[idx + 1];
                    const float up = input[(y - 1) * width + x];
                    const float down = input[(y + 1) * width + x];
                    
                    // Finite difference approximation
                    const float laplacian = (left + right + up + down - 4.0f * current) / (dx * dx);
                    output[idx] = current + alpha * dt * laplacian;
                }",
            EntryPoint = "heat_equation"
        };

        var compiledHeat = await accelerator.CompileKernelAsync(heatKernel);

        var threadgroupSize = 16;
        var threadgroupsX = (gridSize + threadgroupSize - 1) / threadgroupSize;
        var threadgroupsY = (gridSize + threadgroupSize - 1) / threadgroupSize;
        var gridDim = (threadgroupsX, threadgroupsY, 1);
        var threadDim = (threadgroupSize, threadgroupSize, 1);

        var stopwatch = Stopwatch.StartNew();

        // Time evolution loop
        for (var step = 0; step < timeSteps; step++)
        {
            var input = step % 2 == 0 ? deviceTemp1 : deviceTemp2;
            var output = step % 2 == 0 ? deviceTemp2 : deviceTemp1;

            var args = new KernelArguments { input, output, (uint)gridSize, (uint)gridSize, alpha, dt, dx };
            await compiledHeat.LaunchAsync(gridDim, threadDim, args);

            // Synchronize every 10 steps to prevent too much queue buildup
            if (step % 10 == 9)
            {
                await accelerator.SynchronizeAsync();
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Download final results
        var finalTemp = new float[totalElements];
        var finalBuffer = timeSteps % 2 == 0 ? deviceTemp1 : deviceTemp2;
        await finalBuffer.CopyToAsync(finalTemp.AsMemory());

        // Analyze results
        var maxTemp = finalTemp.Max();
        var minTemp = finalTemp.Min();
        var avgTemp = finalTemp.Average();
        var centerTemp = finalTemp[(gridSize / 2) * gridSize + (gridSize / 2)];

        // Verify heat diffusion (temperature should have spread out from center)
        var initialMaxTemp = initialTemp.Max();
        var temperatureReduction = (initialMaxTemp - maxTemp) / initialMaxTemp;

        // Check for temperature conservation (approximately)
        var initialTotalEnergy = initialTemp.Sum();
        var finalTotalEnergy = finalTemp.Sum();
        var energyConservation = Math.Abs(finalTotalEnergy - initialTotalEnergy) / initialTotalEnergy;

        // Verify numerical stability
        var validValues = finalTemp.Count(t => !float.IsNaN(t) && !float.IsInfinity(t));
        var validityRatio = (double)validValues / totalElements;

        Output.WriteLine($"Heat Equation Simulation Results:");
        Output.WriteLine($"  Grid size: {gridSize}x{gridSize}");
        Output.WriteLine($"  Time steps: {timeSteps}");
        Output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds:F2} ms");
        Output.WriteLine($"  Time per step: {stopwatch.ElapsedMilliseconds / timeSteps:F2} ms");
        Output.WriteLine($"  Initial max temp: {initialMaxTemp:F2}°C");
        Output.WriteLine($"  Final max temp: {maxTemp:F2}°C");
        Output.WriteLine($"  Final min temp: {minTemp:F2}°C");
        Output.WriteLine($"  Final avg temp: {avgTemp:F2}°C");
        Output.WriteLine($"  Center temp: {centerTemp:F2}°C");
        Output.WriteLine($"  Temperature reduction: {temperatureReduction:P1}");
        Output.WriteLine($"  Energy conservation error: {energyConservation:P2}");
        Output.WriteLine($"  Valid values: {validValues}/{totalElements} ({validityRatio:P1})");

        // Assertions for physical correctness and numerical stability
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "Simulation should complete in reasonable time");
        temperatureReduction.Should().BeInRange((float)0.1, (float)0.9, "Temperature should diffuse but not completely disappear");
        energyConservation.Should().BeLessThan((float)0.1, "Energy should be approximately conserved");
        validityRatio.Should().BeGreaterThan(0.99, "All values should remain valid (no NaN/Inf)");
        maxTemp.Should().BeLessThan(initialMaxTemp, "Maximum temperature should decrease due to diffusion");
        minTemp.Should().BeGreaterThanOrEqualTo(0, "Temperature should not go negative");
    }

    [SkippableFact]
    public async Task Multi_Buffer_Dependency_Chain_Should_Handle_Complex_Patterns()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        // Create a dependency chain: A -> B -> C -> D -> E with feedback from C to A
        const int dataSize = 1024 * 1024; // 1M elements

        await using var bufferA = await accelerator.Memory.AllocateAsync<float>(dataSize);
        await using var bufferB = await accelerator.Memory.AllocateAsync<float>(dataSize);
        await using var bufferC = await accelerator.Memory.AllocateAsync<float>(dataSize);
        await using var bufferD = await accelerator.Memory.AllocateAsync<float>(dataSize);
        await using var bufferE = await accelerator.Memory.AllocateAsync<float>(dataSize);
        await using var bufferFeedback = await accelerator.Memory.AllocateAsync<float>(dataSize);

        // Initialize buffer A
        var initialData = MetalTestUtilities.TestDataGenerator.CreateLinearSequence(dataSize, 1.0f, 0.001f);
        await bufferA.CopyFromAsync(initialData.AsMemory());

        // Define transformation kernels
        var transformKernel = new KernelDefinition
        {
            Name = "transform",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void transform(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    constant float& multiplier [[buffer(2)]],
                    constant float& offset [[buffer(3)]],
                    constant uint& size [[buffer(4)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= size) return;
                    output[id] = input[id] * multiplier + offset;
                }",
            EntryPoint = "transform"
        };

        var feedbackKernel = new KernelDefinition
        {
            Name = "feedback",
            Code = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void feedback(
                    device float* original [[buffer(0)]],
                    device float* feedback [[buffer(1)]],
                    device float* output [[buffer(2)]],
                    constant float& weight [[buffer(3)]],
                    constant uint& size [[buffer(4)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= size) return;
                    output[id] = original[id] * (1.0f - weight) + feedback[id] * weight;
                }",
            EntryPoint = "feedback"
        };

        var compiledTransform = await accelerator.CompileKernelAsync(transformKernel);
        var compiledFeedback = await accelerator.CompileKernelAsync(feedbackKernel);

        var threadgroups = (dataSize + 256 - 1) / 256;
        var gridSize = (threadgroups, 1, 1);
        var threadSize = (256, 1, 1);

        const int iterations = 10;
        var stopwatch = Stopwatch.StartNew();

        for (var iter = 0; iter < iterations; iter++)
        {
            // Forward pass: A -> B -> C -> D -> E
            var argsAB = new KernelArguments { bufferA, bufferB, 1.1f, 0.01f, (uint)dataSize };
            await compiledTransform.LaunchAsync(gridSize, threadSize, argsAB);

            var argsBC = new KernelArguments { bufferB, bufferC, 0.9f, 0.02f, (uint)dataSize };
            await compiledTransform.LaunchAsync(gridSize, threadSize, argsBC);

            var argsCD = new KernelArguments { bufferC, bufferD, 1.05f, -0.01f, (uint)dataSize };
            await compiledTransform.LaunchAsync(gridSize, threadSize, argsCD);

            var argsDE = new KernelArguments { bufferD, bufferE, 0.95f, 0.005f, (uint)dataSize };
            await compiledTransform.LaunchAsync(gridSize, threadSize, argsDE);

            // Feedback: C -> feedback buffer with processing
            var argsFeedback = new KernelArguments { bufferC, bufferFeedback, 0.8f, 0.001f, (uint)dataSize };
            await compiledTransform.LaunchAsync(gridSize, threadSize, argsFeedback);

            // Apply feedback to A
            var argsFeedbackApply = new KernelArguments { bufferA, bufferFeedback, bufferA, 0.1f, (uint)dataSize };
            await compiledFeedback.LaunchAsync(gridSize, threadSize, argsFeedbackApply);

            // Synchronize every few iterations
            if (iter % 3 == 2)
            {
                await accelerator.SynchronizeAsync();
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Validate results from each buffer
        var resultA = new float[dataSize];
        var resultB = new float[dataSize];
        var resultC = new float[dataSize];
        var resultD = new float[dataSize];
        var resultE = new float[dataSize];

        await Task.WhenAll(
            bufferA.CopyToAsync(resultA.AsMemory()).AsTask(),
            bufferB.CopyToAsync(resultB.AsMemory()).AsTask(),
            bufferC.CopyToAsync(resultC.AsMemory()).AsTask(),
            bufferD.CopyToAsync(resultD.AsMemory()).AsTask(),
            bufferE.CopyToAsync(resultE.AsMemory()).AsTask()
        );

        // Statistical analysis
        var results = new[] { resultA, resultB, resultC, resultD, resultE };
        var bufferNames = new[] { "A", "B", "C", "D", "E" };

        Output.WriteLine($"Multi-Buffer Dependency Chain Results:");
        Output.WriteLine($"  Data size: {dataSize:N0} elements");
        Output.WriteLine($"  Iterations: {iterations}");
        Output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds:F2} ms");
        Output.WriteLine($"  Time per iteration: {stopwatch.ElapsedMilliseconds / iterations:F2} ms");

        for (var i = 0; i < results.Length; i++)
        {
            var data = results[i];
            var mean = data.Average();
            var min = data.Min();
            var max = data.Max();
            var validValues = data.Count(v => !float.IsNaN(v) && !float.IsInfinity(v));
            var validityRatio = (double)validValues / dataSize;

            Output.WriteLine($"  Buffer {bufferNames[i]}: mean={mean:F6}, min={min:F6}, max={max:F6}, valid={validityRatio:P1}");

            // Each buffer should have valid values
            validityRatio.Should().BeGreaterThan(0.99, $"Buffer {bufferNames[i]} should contain valid values");
        }

        // Verify that data has flowed through the dependency chain
        var finalA = resultA.Take(100).Average();
        var finalE = resultE.Take(100).Average();
        var initialMean = initialData.Take(100).Average();

        Output.WriteLine($"  Initial A mean: {initialMean:F6}");
        Output.WriteLine($"  Final A mean: {finalA:F6}");
        Output.WriteLine($"  Final E mean: {finalE:F6}");

        MathF.Abs((float)(finalA - initialMean)).Should().BeGreaterThan(0.01f, "Buffer A should have changed due to feedback");
        MathF.Abs((float)(finalE - initialMean)).Should().BeGreaterThan(0.1f, "Buffer E should be significantly different after processing chain");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Complex dependency chain should complete in reasonable time");
    }
}
