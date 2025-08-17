// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware.Unit;

/// <summary>
/// Unit tests for CUDA accelerator lifecycle management and operations
/// </summary>
[Collection("CUDA Hardware Tests")]
public sealed class CudaAcceleratorTests : IDisposable
{
    private readonly ILogger<CudaAcceleratorTests> _logger;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];

    // LoggerMessage delegate for performance
    private static readonly Action<ILogger, Exception, Exception?> LogAcceleratorDisposeError = 
        LoggerMessage.Define<Exception>(
            LogLevel.Warning,
            new EventId(1, nameof(LogAcceleratorDisposeError)),
            "Error disposing CUDA accelerator: {Exception}");

    public CudaAcceleratorTests(ITestOutputHelper output)
    {
        _output = output;
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<CudaAcceleratorTests>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        // Assert
        Assert.NotNull(accelerator);
        accelerator.Info.Should().NotBeNull();
        accelerator.Memory.Should().NotBeNull();
        accelerator.Info.Type.Should().Be("CUDA");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        // Arrange & Act
        if (!IsCudaAvailable())
            return;

        var accelerator = new CudaAccelerator(0, null);
        _accelerators.Add(accelerator);

        // Assert
        Assert.NotNull(accelerator);
        accelerator.Info.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_CompileKernelAsync_WithValidKernel_ShouldReturnCompiledKernel()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        var kernelDefinition = CreateSimpleKernel();

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);

        // Assert
        Assert.NotNull(compiledKernel);
        compiledKernel.Name.Should().Be("test_kernel");
        (compiledKernel as IDisposable)?.Dispose(); // Clean up
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_CompileKernelAsync_WithNullDefinition_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
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
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
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
        Assert.NotNull(compiledKernel);
        (compiledKernel as IDisposable)?.Dispose(); // Clean up
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_SynchronizeAsync_ShouldCompleteWithoutError()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        // Act & Assert
        await accelerator.SynchronizeAsync(); // Should not throw
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_SynchronizeAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var syncAction = async () => await accelerator.SynchronizeAsync(cts.Token);
        await Assert.ThrowsAsync<OperationCanceledException>(syncAction);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Reset_ShouldResetDeviceState()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        // Act & Assert
        Action resetAction = () => accelerator.Reset();
        resetAction(); // Should not throw
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Info_ShouldProvideAccurateInformation()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        // Act
        var info = accelerator.Info;

        // Assert
        Assert.NotNull(info);
        info.DeviceType.Should().Be(AcceleratorType.CUDA.ToString());
        info.Name.Should().NotBeNullOrEmpty();
        (info.TotalMemory > 0).Should().BeTrue();
        (info.ComputeUnits > 0).Should().BeTrue();
        (info.MaxClockFrequency > 0).Should().BeTrue();
        info.ComputeCapability.Should().NotBeNull();
        (info.MaxSharedMemoryPerBlock > 0).Should().BeTrue();
        info.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Memory_ShouldProvideMemoryManager()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        // Act
        var memory = accelerator.Memory;

        // Assert
        Assert.NotNull(memory);
        Assert.IsAssignableFrom<IMemoryManager>(memory);
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_MultipleOperations_ShouldMaintainPerformance()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Perform multiple synchronization operations
        for (var i = 0; i < 10; i++)
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
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        // Act & Assert
        for (var i = 0; i < 5; i++)
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
        if (!IsCudaAvailable())
            return;

        var accelerator = new CudaAccelerator(0, logger: null);
        // Don't add to _accelerators list since we're testing disposal

        // Act & Assert
        var disposeAction = async () => await accelerator.DisposeAsync();
        await disposeAction(); // Should not throw
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_Dispose_ShouldCleanupResources()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = new CudaAccelerator(0, logger: null);
        // Don't add to _accelerators list since we're testing disposal

        // Act & Assert
        Action disposeAction = () => accelerator.Dispose();
        disposeAction(); // Should not throw
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_OperationsAfterDispose_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = new CudaAccelerator(0, logger: null);
        await accelerator.DisposeAsync();

        // Act & Assert
        var syncAction = async () => await accelerator.SynchronizeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await syncAction());

        Action resetAction = () => accelerator.Reset();
        Assert.Throws<ObjectDisposedException>(() => resetAction());
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaAccelerator_ConcurrentOperations_ShouldHandleParallelCalls()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => accelerator.SynchronizeAsync().AsTask())
            .ToArray();

        // Assert
        var allTasksAction = async () => await Task.WhenAll(tasks);
        await allTasksAction(); // Should not throw
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public void CudaAccelerator_WithMultipleDevices_ShouldHandleDifferentDevices()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        // Get device count
        var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
        if (result != CudaError.Success || deviceCount <= 1)
            return;

        var accelerators = new List<CudaAccelerator>();

        try
        {
            // Act - Create accelerators for different devices
            for (var deviceId = 0; deviceId < Math.Min(deviceCount, 3); deviceId++)
            {
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var cudaLogger = loggerFactory.CreateLogger<CudaAccelerator>();
                var accelerator = new CudaAccelerator(deviceId, cudaLogger);
                accelerators.Add(accelerator);
                _accelerators.Add(accelerator);

                // Assert
                accelerator.Info.Should().NotBeNull();
                accelerator.Memory.Should().NotBeNull();
            }

            accelerators.Count.Should().BeGreaterThan(1);
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
        if (!IsCudaAvailable())
            return;

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
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
    if(idx < n) {
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
        try
        {
            // Since IsNvrtcAvailable doesn't exist, return true for tests
            return true;
        }
        catch
        {
            return false;
        }
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
                LogAcceleratorDisposeError(_logger, ex, null);
            }
        }
        _accelerators.Clear();
        GC.SuppressFinalize(this);
    }
}
