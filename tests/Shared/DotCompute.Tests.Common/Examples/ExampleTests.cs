using DotCompute.Tests.Common.Fixtures;
using DotCompute.Tests.Common.Assertions;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common.Examples;

/// <summary>
/// Example tests demonstrating the usage of DotCompute test infrastructure.
/// These examples show best practices for testing accelerated computing scenarios.
/// </summary>
public class ExampleTests(ITestOutputHelper output) : ConsolidatedTestBase(output)
{
    [Fact]
    [Trait("Category", TestCategories.HardwareIndependent)]
    public void Example_BasicArrayComparison()
    {
        // Arrange - Using test fixtures for consistent data
        var input1 = TestDataFixture.Small.SequentialFloats;
        var input2 = TestDataFixture.Small.SequentialFloats;

        // Act - Simulate some computation

        var result = new float[input1.Length];
        for (var i = 0; i < input1.Length; i++)
        {
            result[i] = input1[i] + input2[i];
        }

        // Assert - Using custom assertions with appropriate tolerance

        var expected = TestDataFixture.ExpectedResults.VectorAdd(input1, input2);
        result.ShouldBeApproximatelyEqualTo(expected, tolerance: 1e-6f);
        result.ShouldContainOnlyFiniteValues();


        Output.WriteLine($"Successfully processed {result.Length} elements");
    }

    [Fact]
    [Trait("Category", TestCategories.HardwareIndependent)]
    [Trait("Category", TestCategories.Performance)]
    public void Example_PerformanceMeasurement()
    {
        // Arrange
        var data = TestDataFixture.Medium.RandomFloats;
        const int iterations = 10;

        // Act - Measure execution time

        var executionTime = MeasureExecutionTime(() =>
        {
            // Simulate some CPU-intensive operation
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = MathF.Sin(data[i]) * MathF.Cos(data[i]);
            }
        }, iterations);

        // Assert - Validate performance expectations

        executionTime.TotalMilliseconds.ShouldBeWithinTimeLimit(1000.0,
            because: "Medium dataset should process within 1 second");

        // Calculate and log throughput

        var elementsPerSecond = (data.Length * iterations) / (executionTime.TotalMilliseconds / 1000.0);
        Output.WriteLine($"Throughput: {elementsPerSecond:N0} elements/second");
    }

    [Fact]
    [Trait("Category", TestCategories.HardwareIndependent)]
    [Trait("Category", TestCategories.MemoryIntensive)]
    public void Example_MemoryTracking()
    {
        // Capture initial memory state
        _ = GetMemoryUsageDelta();

        // Act - Allocate and process data

        var largeDataSet = TestDataFixture.Large.RandomFloats;

        // Process the data to ensure it's actually used

        double sum = 0;
        for (var i = 0; i < largeDataSet.Length; i++)
        {
            sum += largeDataSet[i];
        }

        // Log memory usage information

        LogMemoryUsage();

        // Verify we got a reasonable result

        Assert.True(Math.Abs(sum) < largeDataSet.Length * 2,

            "Sum should be reasonable for random data in [-1,1] range");


        Output.WriteLine($"Processed {largeDataSet.Length:N0} elements, sum = {sum:F2}");
    }

    [Fact]
    [Trait("Category", TestCategories.HardwareIndependent)]
    public void Example_MatrixOperations()
    {
        // Arrange - Using matrix fixtures
        var matrixA = TestDataFixture.Matrices.Small32x32.Sequential;
        var matrixB = TestDataFixture.Matrices.Small32x32.Identity;
        const int size = 32;

        // Act - Matrix multiplication (A * I should equal A)

        var result = TestDataFixture.ExpectedResults.MatrixMultiply(
            matrixA, matrixB, size, size, size);

        // Assert - Matrix multiplication by identity should return original matrix

        result.ShouldBeApproximatelyEqualTo(matrixA, 1e-6f,
            because: "Matrix multiplied by identity should equal original matrix");


        Output.WriteLine($"Matrix multiplication verification completed for {size}x{size} matrices");
    }

    [Fact]
    [Trait("Category", TestCategories.HardwareIndependent)]
    public void Example_SpecializedDataPatterns()
    {
        // Test different data patterns
        var sineData = TestDataFixture.SpecializedData.PatternedData(1000, TestDataFixture.DataPattern.Sine);
        var sparseData = TestDataFixture.SpecializedData.SparseData(1000, sparsityRatio: 0.1f);
        var precisionData = TestDataFixture.SpecializedData.PrecisionTestData(1000);

        // Validate patterns

        sineData.ShouldContainOnlyFiniteValues();
        // Sine values should be in [-1,1] range
        _ = sineData.All(v => v >= -1.1f && v <= 1.1f).Should().BeTrue("Sine values should be in [-1,1] range");

        // Count non-zero elements in sparse data

        var nonZeroCount = sparseData.Count(x => x != 0.0f);
        var sparsityRatio = nonZeroCount / (float)sparseData.Length;


        Assert.InRange(sparsityRatio, 0.08f, 0.12f); // Approximately 10% with some tolerance

        // Precision data should contain mix of scales

        precisionData.ShouldContainOnlyFiniteValues();


        Output.WriteLine($"Sine data range: [{sineData.Min():F3}, {sineData.Max():F3}]");
        Output.WriteLine($"Sparse data has {nonZeroCount} non-zero elements ({sparsityRatio:P1})");
        Output.WriteLine($"Precision data range: [{precisionData.Min():E2}, {precisionData.Max():E2}]");
    }

    [Fact]
    [Trait("Category", TestCategories.HardwareIndependent)]
    public void Example_ErrorHandling()
    {
        // Test error conditions and edge cases
        var emptyArray = Array.Empty<float>();
        var singleElement = new[] { 42.0f };

        // Verify empty arrays don't crash

        emptyArray.ShouldContainOnlyFiniteValues("empty arrays should be valid");

        // Test single element operations

        var singleResult = TestDataFixture.ExpectedResults.VectorScalarMultiply(singleElement, 2.0f);
        _ = Assert.Single(singleResult);
        Assert.Equal(84.0f, singleResult[0]);

        // Test NaN handling in custom data

        var dataWithSpecialValues = new[] { 1.0f, float.NaN, float.PositiveInfinity, 2.0f };

        // This should detect the invalid values

        var exception = Assert.Throws<InvalidOperationException>(() =>
            dataWithSpecialValues.ShouldContainOnlyFiniteValues());


        Assert.Contains("finite values", exception.Message, StringComparison.OrdinalIgnoreCase);


        Output.WriteLine("Error handling validation completed");
    }
}

/// <summary>
/// Example GPU-specific tests demonstrating GPU test infrastructure.
/// These tests will be skipped if appropriate hardware is not available.
/// </summary>
public class ExampleGpuTests(ITestOutputHelper output) : ConsolidatedTestBase(output)
{
    [Fact]
    [Trait("Category", TestCategories.RequiresCUDA)]
    public void Example_CudaHardwareDetection()
    {
        // Skip if CUDA not available
        SkipIfNoCuda();

        // Get GPU capabilities
        var capabilities = GetNvidiaCapabilities();

        Assert.True(capabilities.HasNvidiaGpu, "NVIDIA GPU should be detected");
        Assert.True(capabilities.TotalMemoryMB > 0, "GPU should have memory");

        Output.WriteLine($"GPU Compute Capability: {capabilities.ComputeCapability}");
        Output.WriteLine($"GPU Memory: {capabilities.TotalMemoryMB} MB");
        Output.WriteLine($"Unified Memory: {capabilities.SupportsUnifiedMemory}");
        Output.WriteLine($"Dynamic Parallelism: {capabilities.SupportsDynamicParallelism}");
    }

    [Fact]
    [Trait("Category", TestCategories.RequiresGPU)]
    public void Example_GpuMemoryTracking()
    {
        // Skip if no GPU available
        SkipIfNoGpu();

        // Take memory snapshots

        TakeGpuMemorySnapshot("initial");

        // Simulate GPU memory allocation
        // In real tests, this would be actual GPU operations

        var data = TestDataFixture.Large.RandomFloats;


        TakeGpuMemorySnapshot("allocated");

        // Simulate processing

        var result = TestDataFixture.ExpectedResults.VectorScalarMultiply(data, 2.0f);


        TakeGpuMemorySnapshot("processed");

        // Compare memory usage

        CompareGpuMemorySnapshots("initial", "processed");

        // Validate results

        result.ShouldContainOnlyFiniteValues();
        Assert.Equal(data.Length, result.Length);


        Output.WriteLine($"Processed {data.Length:N0} elements on GPU simulation");
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.RequiresCUDA)]
    public void Example_GpuPerformanceMeasurement()
    {
        // Skip if insufficient resources
        SkipIfNoCuda();
        SkipIfInsufficientGpuMemory(requiredMemoryMB: 100);

        // Arrange

        var data = TestDataFixture.Medium.RandomFloats;

        // Act - Measure GPU kernel execution time

        var kernelTime = MeasureGpuKernelTime(() =>
            // Simulate GPU kernel execution
            // In real tests, this would launch actual GPU kernels
            Thread.Sleep(10), iterations: 5);

        // Assert - Validate performance

        kernelTime.ShouldBeWithinTimeLimit(50.0,
            because: "GPU kernel should complete quickly");

        // Calculate theoretical bandwidth

        long bytesProcessed = data.Length * sizeof(float) * 2; // Read + Write
        var bandwidth = CalculateBandwidth(bytesProcessed, kernelTime);


        Output.WriteLine($"Simulated kernel processed {data.Length:N0} elements in {kernelTime:F2}ms");
        Output.WriteLine($"Theoretical bandwidth: {bandwidth:F2} GB/s");
    }

    [Fact]
    [Trait("Category", TestCategories.RequiresCUDA)]
    [Trait("Category", TestCategories.AdvancedFeatures)]
    public void Example_AdvancedGpuFeatures()
    {
        // Skip if insufficient capabilities
        SkipIfNoCuda();
        SkipIfInsufficientComputeCapability(minimumComputeCapability: "3.5");

        // Test advanced GPU features
        var capabilities = GetNvidiaCapabilities();

        if (capabilities.SupportsUnifiedMemory)
        {
            Output.WriteLine("Unified Memory is supported - can test UVM scenarios");
        }


        if (capabilities.SupportsDynamicParallelism)
        {
            Output.WriteLine("Dynamic Parallelism is supported - can test nested kernels");
        }

        // Validate GPU memory constraints

        ValidateGpuMemoryUsage(maxAllowedMemoryMB: 512);


        Output.WriteLine("Advanced GPU feature validation completed");
    }
}