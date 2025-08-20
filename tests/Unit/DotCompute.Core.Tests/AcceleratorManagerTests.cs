using Xunit;
using FluentAssertions;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests;


public sealed class AcceleratorManagerTests
{
    private readonly ILogger<DefaultAcceleratorManager> _logger;

    public AcceleratorManagerTests()
    {
        _logger = Substitute.For<ILogger<DefaultAcceleratorManager>>();
    }

    [Fact]
    public async Task RegisterProvider_WithValidProvider_ShouldSucceed()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();
        _ = provider.Name.Returns("TestProvider");

        // Act
        manager.RegisterProvider(provider);

        // Assert
        // Provider should be registered successfully
    }

    [Fact]
    public async Task RegisterProvider_WithNullProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);

        // Act & Assert
        var act = () => manager.RegisterProvider(null!);
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RegisterProvider_AfterInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        await manager.InitializeAsync();
        var provider = Substitute.For<IAcceleratorProvider>();
        _ = provider.Name.Returns("TestProvider");

        // Act
        var act = () => manager.RegisterProvider(provider);

        // Assert
        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot register providers after initialization*");
    }

    [Fact]
    public async Task GetAccelerator_ByIndex_ShouldReturnCorrectAccelerator()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024 * 1024 * 1024);
        _ = accelerator.Info.Returns(info);

        _ = provider.Name.Returns("TestProvider");
        _ = provider.DiscoverAsync(default).Returns(new[] { accelerator });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        var result = manager.GetAccelerator(0);

        // Assert
        _ = result.Should().BeSameAs(accelerator);
    }

    [Fact]
    public async Task GetAccelerator_WithoutInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);

        // Act
        var act = () => manager.GetAccelerator(0);

        // Assert
        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be initialized*");
    }

    [Fact]
    public async Task GetAcceleratorById_WithExistingId_ShouldReturnAccelerator()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024 * 1024 * 1024);
        _ = accelerator.Info.Returns(info);

        _ = provider.Name.Returns("TestProvider");
        _ = provider.DiscoverAsync(default).Returns(new[] { accelerator });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        var result = manager.GetAcceleratorById("CPU_Test");

        // Assert
        _ = result.Should().BeSameAs(accelerator);
    }

    [Fact]
    public async Task GetAcceleratorById_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        await manager.InitializeAsync();

        // Act
        var result = manager.GetAcceleratorById("NonExisting");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAcceleratorsByType_ShouldReturnCorrectAccelerators()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();
        var cpuAccel = Substitute.For<IAccelerator>();
        var gpuAccel = Substitute.For<IAccelerator>();

        var cpuInfo = new AcceleratorInfo(AcceleratorType.CPU, "CPU", "1.0", 1024 * 1024 * 1024);
        var gpuInfo = new AcceleratorInfo(AcceleratorType.OpenCL, "GPU", "1.0", 1024 * 1024 * 1024);

        _ = cpuAccel.Info.Returns(cpuInfo);
        _ = gpuAccel.Info.Returns(gpuInfo);

        _ = provider.Name.Returns("TestProvider");
        _ = provider.DiscoverAsync(default).Returns(new[] { cpuAccel, gpuAccel });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        var cpus = manager.GetAcceleratorsByType(AcceleratorType.CPU).ToList();
        var gpus = manager.GetAcceleratorsByType(AcceleratorType.OpenCL).ToList();

        // Assert
        _ = Assert.Single(cpus);
        Assert.Contains(cpuAccel, cpus);
        _ = Assert.Single(gpus);
        Assert.Contains(gpuAccel, gpus);
    }

    [Fact]
    public async Task InitializeAsync_ShouldDiscoverFromAllProviders()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider1 = Substitute.For<IAcceleratorProvider>();
        var provider2 = Substitute.For<IAcceleratorProvider>();

        var accel1 = Substitute.For<IAccelerator>();
        var accel2 = Substitute.For<IAccelerator>();
        var accel3 = Substitute.For<IAccelerator>();

        var info1 = new AcceleratorInfo(AcceleratorType.CPU, "CPU1", "1.0", 1024 * 1024 * 1024);
        var info2 = new AcceleratorInfo(AcceleratorType.CUDA, "GPU1", "1.0", 2 * 1024 * 1024 * 1024L);
        var info3 = new AcceleratorInfo(AcceleratorType.CUDA, "GPU2", "1.0", 2 * 1024 * 1024 * 1024L);

        _ = accel1.Info.Returns(info1);
        _ = accel2.Info.Returns(info2);
        _ = accel3.Info.Returns(info3);

        _ = provider1.Name.Returns("Provider1");
        _ = provider2.Name.Returns("Provider2");
        _ = provider1.DiscoverAsync(default).Returns(new[] { accel1, accel2 });
        _ = provider2.DiscoverAsync(default).Returns(new[] { accel3 });

        manager.RegisterProvider(provider1);
        manager.RegisterProvider(provider2);

        // Act
        await manager.InitializeAsync();

        // Assert
        _ = manager.Count.Should().Be(3);
        _ = manager.AvailableAccelerators.Count.Should().Be(3);
        // Since none have DeviceType == "GPU", it should fall back to first CPU
        _ = manager.Default.Should().BeSameAs(accel1); // Falls back to CPU since no GPU found
    }

    [Fact]
    public async Task SelectBest_WithCriteria_ShouldReturnBestMatch()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();

        var accel1 = Substitute.For<IAccelerator>();
        var accel2 = Substitute.For<IAccelerator>();
        var accel3 = Substitute.For<IAccelerator>();

        var info1 = new AcceleratorInfo(AcceleratorType.CPU, "CPU", "1.0", 1024L * 1024 * 1024);
        var info2 = new AcceleratorInfo(AcceleratorType.OpenCL, "GPU1", "1.0", 2048L * 1024 * 1024);
        var info3 = new AcceleratorInfo(AcceleratorType.OpenCL, "GPU2", "1.0", 4096L * 1024 * 1024);

        _ = accel1.Info.Returns(info1);
        _ = accel2.Info.Returns(info2);
        _ = accel3.Info.Returns(info3);

        _ = provider.Name.Returns("Provider");
        _ = provider.DiscoverAsync(default).Returns(new[] { accel1, accel2, accel3 });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        var criteria = new AcceleratorSelectionCriteria
        {
            PreferredType = AcceleratorType.OpenCL,
            MinimumMemory = 3L * 1024 * 1024 * 1024
        };

        // Act
        var result = manager.SelectBest(criteria);

        // Assert
        _ = result.Should().BeSameAs(accel3); // GPU2 with 4GB
    }

    [Fact]
    public async Task CreateContext_WithValidAccelerator_ShouldReturnContext()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo(AcceleratorType.CPU, "CPU", "1.0", 1024 * 1024 * 1024);
        _ = accelerator.Info.Returns(info);

        _ = provider.Name.Returns("Provider");
        _ = provider.DiscoverAsync(default).Returns(new[] { accelerator });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act  
        var context = manager.CreateContext(accelerator);

        // Assert
        // Context is a value type, so no null check needed
        _ = context.DeviceId.Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAllAccelerators()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();
        var accel1 = Substitute.For<IAccelerator>();
        var accel2 = Substitute.For<IAccelerator>();

        var info1 = new AcceleratorInfo(AcceleratorType.CPU, "CPU", "1.0", 1024 * 1024 * 1024);
        var info2 = new AcceleratorInfo(AcceleratorType.OpenCL, "GPU", "1.0", 1024 * 1024 * 1024);

        _ = accel1.Info.Returns(info1);
        _ = accel2.Info.Returns(info2);

        _ = provider.Name.Returns("Provider");
        _ = provider.DiscoverAsync(default).Returns(new[] { accel1, accel2 });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        await manager.DisposeAsync();

        // Assert
        await accel1.Received(1).DisposeAsync();
        await accel2.Received(1).DisposeAsync();
    }
}
