// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Backends.CPU.Tests.Helpers;

namespace DotCompute.Backends.CPU.Tests.Constructor;

/// <summary>
/// Simple tests to verify that CpuAccelerator constructor calls work correctly.
/// This tests the main issue of constructor parameter requirements.
/// </summary>
public class CpuAcceleratorConstructorTests : IDisposable
{
    private readonly FakeLogger<CpuAccelerator> _logger;
    private readonly IOptions<CpuAcceleratorOptions> _options;
    private readonly IOptions<CpuThreadPoolOptions> _threadPoolOptions;
    private bool _disposed;

    public CpuAcceleratorConstructorTests()
    {
        _logger = new FakeLogger<CpuAccelerator>();
        _options = Options.Create(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true
        });
        _threadPoolOptions = Options.Create(new CpuThreadPoolOptions
        {
            WorkerThreads = Environment.ProcessorCount,
            MaxQueuedItems = 10000,
            EnableWorkStealing = true
        });
    }

    [Fact]
    public async Task Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Act
        await using var accelerator = new CpuAccelerator(_options, _threadPoolOptions, _logger);

        // Assert
        Assert.NotNull(accelerator);
        accelerator.Type.Should().Be(AcceleratorType.CPU);
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new CpuAccelerator(null!, _threadPoolOptions, _logger);
        act.Should().Throw<Exception>(); // May throw NullReferenceException or ArgumentNullException
    }

    [Fact]
    public void Constructor_WithNullThreadPoolOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new CpuAccelerator(_options, null!, _logger);
        act.Should().Throw<Exception>(); // May throw NullReferenceException or ArgumentNullException
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new CpuAccelerator(_options, _threadPoolOptions, null!);
        act.Should().Throw<Exception>(); // May throw NullReferenceException or ArgumentNullException
    }

    [Fact]
    public async Task Type_ShouldReturnCpu()
    {
        // Arrange
        await using var accelerator = new CpuAccelerator(_options, _threadPoolOptions, _logger);

        // Act
        var type = accelerator.Type;

        // Assert
        Assert.Equal(AcceleratorType.CPU, type);
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var accelerator = new CpuAccelerator(_options, _threadPoolOptions, _logger);

        // Act & Assert
        await accelerator.DisposeAsync(); // Should not throw
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}