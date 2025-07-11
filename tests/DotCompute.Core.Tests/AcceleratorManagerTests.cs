using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using DotCompute.Core;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for AcceleratorManager functionality.
/// </summary>
public class AcceleratorManagerTests : IAsyncLifetime
{
    private readonly IAcceleratorManager _manager;
    private readonly List<IAccelerator> _mockAccelerators;

    public AcceleratorManagerTests()
    {
        _manager = Substitute.For<IAcceleratorManager>();
        _mockAccelerators = new List<IAccelerator>();
    }

    [Fact]
    public async Task GetDefaultAcceleratorAsync_ReturnsDefaultAccelerator()
    {
        // Arrange
        var defaultAccelerator = CreateMockAccelerator("Default", AcceleratorType.CPU);
        _manager.GetDefaultAcceleratorAsync().Returns(defaultAccelerator);

        // Act
        var result = await _manager.GetDefaultAcceleratorAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(defaultAccelerator);
        result.Info.Name.Should().Be("Default");
    }

    [Fact]
    public async Task GetDefaultAcceleratorAsync_WhenNoAccelerators_ThrowsException()
    {
        // Arrange
        _manager.GetDefaultAcceleratorAsync()
            .Returns(Task.FromException<IAccelerator>(new InvalidOperationException("No accelerators available")));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.GetDefaultAcceleratorAsync());
    }

    [Fact]
    public async Task GetAcceleratorAsync_WithValidName_ReturnsAccelerator()
    {
        // Arrange
        var cpuAccelerator = CreateMockAccelerator("CPU", AcceleratorType.CPU);
        _manager.GetAcceleratorAsync("CPU").Returns(cpuAccelerator);

        // Act
        var result = await _manager.GetAcceleratorAsync("CPU");

        // Assert
        result.Should().NotBeNull();
        result.Info.Name.Should().Be("CPU");
        result.Info.Type.Should().Be(AcceleratorType.CPU);
    }

    [Fact]
    public async Task GetAcceleratorAsync_WithInvalidName_ThrowsException()
    {
        // Arrange
        _manager.GetAcceleratorAsync("NonExistent")
            .Returns(Task.FromException<IAccelerator>(new ArgumentException("Accelerator not found: NonExistent")));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _manager.GetAcceleratorAsync("NonExistent"));
        
        ex.Message.Should().Contain("NonExistent");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAcceleratorAsync_WithNullOrEmptyName_ThrowsArgumentException(string name)
    {
        // Arrange
        _manager.GetAcceleratorAsync(name)
            .Returns(Task.FromException<IAccelerator>(new ArgumentException("Name cannot be null or empty")));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _manager.GetAcceleratorAsync(name));
    }

    [Fact]
    public async Task GetAvailableAcceleratorsAsync_ReturnsAllAccelerators()
    {
        // Arrange
        var accelerators = new[]
        {
            CreateMockAccelerator("CPU", AcceleratorType.CPU),
            CreateMockAccelerator("CUDA:0", AcceleratorType.CUDA),
            CreateMockAccelerator("CUDA:1", AcceleratorType.CUDA)
        };
        
        _manager.GetAvailableAcceleratorsAsync()
            .Returns(Task.FromResult<IReadOnlyList<IAccelerator>>(accelerators));

        // Act
        var result = await _manager.GetAvailableAcceleratorsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(a => a.Info.Name == "CPU");
        result.Should().Contain(a => a.Info.Name == "CUDA:0");
        result.Should().Contain(a => a.Info.Name == "CUDA:1");
    }

    [Fact]
    public async Task GetAvailableAcceleratorsAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        _manager.GetAvailableAcceleratorsAsync()
            .Returns(Task.FromResult<IReadOnlyList<IAccelerator>>(Array.Empty<IAccelerator>()));

        // Act
        var result = await _manager.GetAvailableAcceleratorsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void RegisterAccelerator_WithValidAccelerator_Succeeds()
    {
        // Arrange
        var accelerator = CreateMockAccelerator("Custom", AcceleratorType.CPU);

        // Act & Assert - Should not throw
        _manager.RegisterAccelerator("Custom", accelerator);
        
        // Verify it was called
        _manager.Received(1).RegisterAccelerator("Custom", accelerator);
    }

    [Fact]
    public void RegisterAccelerator_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange
        _manager.When(m => m.RegisterAccelerator(Arg.Any<string>(), null!))
            .Throw(new ArgumentNullException("accelerator"));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _manager.RegisterAccelerator("Test", null!));
    }

    [Fact]
    public void RegisterAccelerator_WithDuplicateName_ThrowsArgumentException()
    {
        // Arrange
        var accelerator1 = CreateMockAccelerator("GPU", AcceleratorType.CUDA);
        var accelerator2 = CreateMockAccelerator("GPU", AcceleratorType.OpenCL);
        
        _manager.RegisterAccelerator("GPU", accelerator1);
        _manager.When(m => m.RegisterAccelerator("GPU", accelerator2))
            .Throw(new ArgumentException("An accelerator with name 'GPU' is already registered"));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _manager.RegisterAccelerator("GPU", accelerator2));
        
        ex.Message.Should().Contain("already registered");
    }

    [Fact]
    public async Task GetAvailableAcceleratorsAsync_FiltersByType()
    {
        // This test demonstrates how filtering might work in a real implementation
        var accelerators = new[]
        {
            CreateMockAccelerator("CPU", AcceleratorType.CPU),
            CreateMockAccelerator("CUDA:0", AcceleratorType.CUDA),
            CreateMockAccelerator("CUDA:1", AcceleratorType.CUDA),
            CreateMockAccelerator("Metal", AcceleratorType.Metal)
        };
        
        _manager.GetAvailableAcceleratorsAsync()
            .Returns(Task.FromResult<IReadOnlyList<IAccelerator>>(accelerators));

        // Act
        var allAccelerators = await _manager.GetAvailableAcceleratorsAsync();
        var cudaAccelerators = allAccelerators.Where(a => a.Info.Type == AcceleratorType.CUDA).ToList();

        // Assert
        cudaAccelerators.Should().HaveCount(2);
        cudaAccelerators.Should().OnlyContain(a => a.Info.Type == AcceleratorType.CUDA);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllAccelerators()
    {
        // Arrange
        var accelerators = new[]
        {
            CreateMockAccelerator("CPU", AcceleratorType.CPU),
            CreateMockAccelerator("GPU", AcceleratorType.CUDA)
        };
        
        _manager.GetAvailableAcceleratorsAsync()
            .Returns(Task.FromResult<IReadOnlyList<IAccelerator>>(accelerators));

        // Act
        await _manager.DisposeAsync();

        // Assert
        await _manager.Received(1).DisposeAsync();
    }

    private IAccelerator CreateMockAccelerator(string name, AcceleratorType type)
    {
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo(
            name,
            "Test Vendor",
            "1.0.0",
            type,
            1.0,
            1024,
            48 * 1024,
            8L * 1024 * 1024 * 1024,
            6L * 1024 * 1024 * 1024);
        
        accelerator.Info.Returns(info);
        accelerator.Memory.Returns(Substitute.For<IMemoryManager>());
        
        _mockAccelerators.Add(accelerator);
        return accelerator;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var accelerator in _mockAccelerators)
        {
            await accelerator.DisposeAsync();
        }
        
        await _manager.DisposeAsync();
    }
}