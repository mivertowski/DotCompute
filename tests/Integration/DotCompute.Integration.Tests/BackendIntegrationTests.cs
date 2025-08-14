using DotCompute.Abstractions;
using DotCompute.Tests.Shared.TestInfrastructure;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for backend implementations
/// </summary>
[Collection("Hardware Integration Tests")]
public class BackendIntegrationTests : CoverageTestBase
{
    private readonly HardwareSimulator _hardwareSimulator;

    public BackendIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _hardwareSimulator = RegisterDisposable(new HardwareSimulator());
    }

    [Fact]
    public async Task CpuBackend_BasicOperation_ExecutesSuccessfully()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var cpuAccelerator = _hardwareSimulator.GetAccelerators(AcceleratorType.CPU).First();

        // Act
        var executionTime = await cpuAccelerator.ExecuteKernelAsync(
            "kernel void vectorAdd(global const float* a, global const float* b, global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
            new object[] { new float[] { 1, 2, 3 }, new float[] { 4, 5, 6 }, new float[3] },
            CancellationToken);

        // Assert
        Assert.True(executionTime > TimeSpan.Zero);
        Assert.True(executionTime < TimeSpan.FromSeconds(1));
    }

    [SkippableTheory]
    [InlineData(AcceleratorType.CUDA)]
    [InlineData(AcceleratorType.OpenCL)]
    [InlineData(AcceleratorType.DirectML)]
    public async Task GpuBackend_BasicOperation_ExecutesSuccessfully(AcceleratorType type)
    {
        // Arrange
        Skip.IfNot(IsHardwareAvailable(type.ToString()), $"{type} hardware not available");
        
        _hardwareSimulator.AddAccelerator(type, $"Test {type} Device");
        var accelerator = _hardwareSimulator.GetAccelerators(type).First();

        // Act
        var executionTime = await accelerator.ExecuteKernelAsync(
            GenerateKernelSource(type),
            CreateTestParameters(),
            CancellationToken);

        // Assert
        Assert.True(executionTime > TimeSpan.Zero);
        Assert.True(executionTime < TimeSpan.FromSeconds(1));
        Logger.LogInformation("{Type} backend execution completed in {Time}ms", type, executionTime.TotalMilliseconds);
    }

    [Fact]
    public async Task MultipleBackends_ParallelExecution_AllComplete()
    {
        // Arrange
        _hardwareSimulator.CreateStandardGpuSetup();
        var accelerators = _hardwareSimulator.GetAllAccelerators().Take(3).ToList();

        // Act
        var tasks = accelerators.Select(async accelerator =>
        {
            var kernelSource = GenerateKernelSource(accelerator.Type);
            var parameters = CreateTestParameters();
            return await accelerator.ExecuteKernelAsync(kernelSource, parameters, CancellationToken);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(accelerators.Count, results.Length);
        Assert.All(results, time => Assert.True(time > TimeSpan.Zero));
    }

    [Fact]
    public async Task Backend_MemoryAllocation_HandlesLargeBuffers()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var accelerator = _hardwareSimulator.GetAccelerators(AcceleratorType.CPU).First();
        const int bufferSize = 1024 * 1024; // 1MB

        // Act
        var handle = accelerator.AllocateMemory(bufferSize);
        var testData = CreateTestData(bufferSize);
        await accelerator.CopyMemoryAsync(testData, handle, CancellationToken);

        // Assert
        Assert.NotNull(handle);
        accelerator.FreeMemory(handle);
    }

    [Fact]
    public async Task Backend_ErrorHandling_ThrowsExpectedExceptions()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var accelerator = _hardwareSimulator.GetAccelerators(AcceleratorType.CPU).First();

        // Act & Assert - Invalid kernel
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _hardwareSimulator.SimulateFailure(AcceleratorType.CPU, "Test failure");
            await accelerator.ExecuteKernelAsync("invalid kernel", new object[0], CancellationToken);
        });

        // Reset for next test
        _hardwareSimulator.ResetFailures();

        // Act & Assert - Out of memory
        Assert.Throws<OutOfMemoryException>(() => 
            accelerator.AllocateMemory((int)accelerator.TotalMemory + 1));
    }

    [Fact]
    public async Task Backend_ContextManagement_CreatesAndDisposesCorrectly()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var accelerator = _hardwareSimulator.GetAccelerators(AcceleratorType.CPU).First();

        // Act
        using var context = accelerator.Context;
        await context.SynchronizeAsync(CancellationToken);

        // Assert
        Assert.NotNull(context);
        
        // Context should be disposed automatically
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(10 * 1024 * 1024)]
    public async Task Backend_DataTransfer_HandlesVariousSizes(int dataSize)
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var accelerator = _hardwareSimulator.GetAccelerators(AcceleratorType.CPU).First();
        
        // Skip if not enough memory
        Skip.If(dataSize > accelerator.AvailableMemory, "Not enough memory for test");

        var testData = CreateTestData(dataSize);

        // Act
        var handle = accelerator.AllocateMemory(dataSize);
        await accelerator.CopyMemoryAsync(testData, handle, CancellationToken);

        // Assert
        Assert.NotNull(handle);
        accelerator.FreeMemory(handle);
    }

    [Fact]
    public async Task Backend_ConcurrentOperations_HandlesCorrectly()
    {
        // Arrange
        _hardwareSimulator.CreateStandardGpuSetup();
        var accelerator = _hardwareSimulator.GetAllAccelerators().First();
        const int concurrentOps = 5;

        // Act
        var tasks = Enumerable.Range(0, concurrentOps).Select(async i =>
        {
            var kernelSource = $"kernel void test{i}() {{ }}";
            return await accelerator.ExecuteKernelAsync(kernelSource, new object[0], CancellationToken);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentOps, results.Length);
        Assert.All(results, time => Assert.True(time >= TimeSpan.Zero));
    }

    [Fact] 
    public Task Backend_ResourceCleanup_ReleasesMemoryProperly()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var accelerator = _hardwareSimulator.GetAccelerators(AcceleratorType.CPU).First();
        var initialMemory = accelerator.AvailableMemory;
        const int allocationSize = 1024 * 1024; // 1MB

        // Act
        var handle = accelerator.AllocateMemory(allocationSize);
        var memoryAfterAlloc = accelerator.AvailableMemory;
        
        accelerator.FreeMemory(handle);
        var memoryAfterFree = accelerator.AvailableMemory;

        // Assert
        Assert.True(memoryAfterAlloc < initialMemory);
        Assert.Equal(initialMemory, memoryAfterFree);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Backend_LongRunningOperation_CanBeCancelled()
    {
        // Arrange
        _hardwareSimulator.CreateCpuOnlySetup();
        var accelerator = _hardwareSimulator.GetAccelerators(AcceleratorType.CPU).First();
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await accelerator.ExecuteKernelAsync("long_running_kernel", new object[0], cts.Token));
    }

    private static string GenerateKernelSource(AcceleratorType type)
    {
        return type switch
        {
            AcceleratorType.CPU => "kernel void cpuTest() { }",
            AcceleratorType.CUDA => "__global__ void cudaTest() { }",
            AcceleratorType.OpenCL => "kernel void openclTest() { }",
            AcceleratorType.DirectML => "[numthreads(1,1,1)] void dcTest() { }",
            AcceleratorType.Metal => "kernel void metalTest(uint id [[thread_position_in_grid]]) { }",
            _ => "kernel void genericTest() { }"
        };
    }

    private static object[] CreateTestParameters()
    {
        return new object[] 
        { 
            new int[] { 1, 2, 3, 4, 5 },
            new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f }
        };
    }
}

/// <summary>
/// Collection definition for hardware integration tests
/// </summary>
[CollectionDefinition("Hardware Integration Tests")]
public class HardwareIntegrationTestsCollection : ICollectionFixture<HardwareIntegrationTestsFixture>
{
}

/// <summary>
/// Fixture for hardware integration tests
/// </summary>
public class HardwareIntegrationTestsFixture : IDisposable
{
    public HardwareIntegrationTestsFixture()
    {
        // Setup any shared resources
    }

    public void Dispose()
    {
        // Cleanup shared resources
    }
}
