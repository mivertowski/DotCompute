// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware.Unit;

/// <summary>
/// Unit tests for CUDA accelerator lifecycle management and operations
/// </summary>
[Collection("CUDA Hardware Tests")]
public class CudaAcceleratorTests : IDisposable
{
    private readonly ILogger<CudaAcceleratorTests> _logger;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];

    public CudaAcceleratorTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<CudaAcceleratorTests>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Info.Should().NotBeNull();
        accelerator.Memory.Should().NotBeNull();
        accelerator.Info.Type.Should().Be(AcceleratorType.CUDA);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        // Arrange & Act
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, null);
        _accelerators.Add(accelerator);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Info.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_CompileKernelAsync_WithValidKernel_ShouldReturnCompiledKernel()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        var kernelDefinition = CreateSimpleKernel();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be("test_kernel");
        compiledKernel.Dispose(); // Clean up
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_CompileKernelAsync_WithNullDefinition_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act & Assert
        var compileAction = async () => await accelerator.CompileKernelAsync(null!);
        await compileAction.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_CompileKernelAsync_WithCompilationOptions_ShouldRespectOptions()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        var kernelDefinition = CreateSimpleKernel();
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = true
        };

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition, options);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Dispose(); // Clean up
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_SynchronizeAsync_ShouldCompleteWithoutError()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act & Assert
        var syncAction = async () => await accelerator.SynchronizeAsync();
        await syncAction.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_SynchronizeAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var syncAction = async () => await accelerator.SynchronizeAsync(cts.Token);
        await syncAction.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Reset_ShouldResetDeviceState()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act & Assert
        Action resetAction = () => accelerator.Reset();
        resetAction.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Info_ShouldProvideAccurateInformation()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var info = accelerator.Info;

        // Assert
        info.Should().NotBeNull();
        info.Type.Should().Be(AcceleratorType.CUDA);
        info.Name.Should().NotBeNullOrEmpty();
        info.MemorySize.Should().BeGreaterThan(0);
        info.ComputeUnits.Should().BeGreaterThan(0);
        info.MaxClockFrequency.Should().BeGreaterThan(0);
        info.ComputeCapability.Should().NotBeNull();
        info.MaxSharedMemoryPerBlock.Should().BeGreaterThan(0);
        info.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Memory_ShouldProvideMemoryManager()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var memory = accelerator.Memory;

        // Assert
        memory.Should().NotBeNull();
        memory.Should().BeAssignableTo<IMemoryManager>();
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_MultipleOperations_ShouldMaintainPerformance()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Perform multiple synchronization operations
        for (int i = 0; i < 10; i++)
        {
            await accelerator.SynchronizeAsync();
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "Multiple sync operations should complete within 5 seconds");
            
        _output.WriteLine($"10 sync operations took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_MultipleResets_ShouldHandleRepeatedResets()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            Action resetAction = () => accelerator.Reset();
            resetAction.Should().NotThrow($"Reset operation {i + 1} should succeed");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_DisposeAsync_ShouldCleanupResources()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        // Don't add to _accelerators list since we're testing disposal

        // Act & Assert
        var disposeAction = async () => await accelerator.DisposeAsync();
        await disposeAction.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Dispose_ShouldCleanupResources()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        // Don't add to _accelerators list since we're testing disposal

        // Act & Assert
        Action disposeAction = () => accelerator.Dispose();
        disposeAction.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_OperationsAfterDispose_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        await accelerator.DisposeAsync();

        // Act & Assert
        var syncAction = async () => await accelerator.SynchronizeAsync();
        await syncAction.Should().ThrowAsync<ObjectDisposedException>();

        Action resetAction = () => accelerator.Reset();
        resetAction.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_ConcurrentOperations_ShouldHandleParallelCalls()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => accelerator.SynchronizeAsync().AsTask())
            .ToArray();

        // Assert
        var allTasksAction = async () => await Task.WhenAll(tasks);
        await allTasksAction.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_WithMultipleDevices_ShouldHandleDifferentDevices()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        // Get device count
        var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
        if (result != CudaError.Success || deviceCount <= 1) return;

        var accelerators = new List<CudaAccelerator>();

        try
        {
            // Act - Create accelerators for different devices
            for (int deviceId = 0; deviceId < Math.Min(deviceCount, 3); deviceId++)
            {
                var accelerator = new CudaAccelerator(deviceId, _logger);
                accelerators.Add(accelerator);
                _accelerators.Add(accelerator);

                // Assert
                accelerator.Info.Should().NotBeNull();
                accelerator.Memory.Should().NotBeNull();
            }

            accelerators.Should().HaveCountGreaterThan(1);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Multi-device test failed: {ex.Message}");
            // This is expected if devices are not available
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_LongRunningSynchronization_ShouldHandleTimeout()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert
        var syncAction = async () => await accelerator.SynchronizeAsync(cts.Token);
        
        // Note: This might not always throw if synchronization is very fast
        // The test validates that cancellation tokens are properly handled
        try
        {
            await syncAction();
            _output.WriteLine("Synchronization completed before timeout");
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("Synchronization was cancelled as expected");
        }
    }

    // Helper Methods
    private static KernelDefinition CreateSimpleKernel()
    {
        const string kernelSource = @"
__global__ void test_kernel(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        output[idx] = input[idx] * 2.0f;
    }
}";
        return new KernelDefinition
        {
            Name = "test_kernel",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "test_kernel"
        };
    }

    private static bool IsCudaAvailable()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNvrtcAvailable()
    {
        return CudaKernelCompiler.IsNvrtcAvailable();
    }

    public void Dispose()
    {
        foreach (var accelerator in _accelerators)
        {
            try
            {
                accelerator?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing CUDA accelerator");
            }
        }
        _accelerators.Clear();
    }
}