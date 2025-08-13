using Xunit;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Tests.Unit;

public class AcceleratorManagerTests
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
        provider.Name.Returns("TestProvider");

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
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RegisterProvider_AfterInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        await manager.InitializeAsync();
        var provider = Substitute.For<IAcceleratorProvider>();
        provider.Name.Returns("TestProvider");

        // Act
        var act = () => manager.RegisterProvider(provider);

        // Assert
        act.Should().Throw<InvalidOperationException>()
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
        accelerator.Info.Returns(info);

        provider.Name.Returns("TestProvider");
        provider.DiscoverAsync(default).Returns(new[] { accelerator });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        var result = manager.GetAccelerator(0);

        // Assert
        result.Should().BeSameAs(accelerator);
    }

    [Fact]
    public async Task GetAccelerator_WithoutInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);

        // Act
        var act = () => manager.GetAccelerator(0);

        // Assert
        act.Should().Throw<InvalidOperationException>()
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
        accelerator.Info.Returns(info);

        provider.Name.Returns("TestProvider");
        provider.DiscoverAsync(default).Returns(new[] { accelerator });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        var result = manager.GetAcceleratorById("CPU_Test");

        // Assert
        result.Should().BeSameAs(accelerator);
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
        result.Should().BeNull();
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

        cpuAccel.Info.Returns(cpuInfo);
        gpuAccel.Info.Returns(gpuInfo);

        provider.Name.Returns("TestProvider");
        provider.DiscoverAsync(default).Returns(new[] { cpuAccel, gpuAccel });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        var cpus = manager.GetAcceleratorsByType(AcceleratorType.CPU).ToList();
        var gpus = manager.GetAcceleratorsByType(AcceleratorType.OpenCL).ToList();

        // Assert
        cpus.Should().HaveCount(1);
        cpus.Should().Contain(cpuAccel);
        gpus.Should().HaveCount(1);
        gpus.Should().Contain(gpuAccel);
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

        accel1.Info.Returns(info1);
        accel2.Info.Returns(info2);
        accel3.Info.Returns(info3);

        provider1.Name.Returns("Provider1");
        provider2.Name.Returns("Provider2");
        provider1.DiscoverAsync(default).Returns(new[] { accel1, accel2 });
        provider2.DiscoverAsync(default).Returns(new[] { accel3 });

        manager.RegisterProvider(provider1);
        manager.RegisterProvider(provider2);

        // Act
        await manager.InitializeAsync();

        // Assert
        manager.Count.Should().Be(3);
        manager.AvailableAccelerators.Should().HaveCount(3);
        // Since none have DeviceType == "GPU", it should fall back to first CPU
        manager.Default.Should().BeSameAs(accel1); // Falls back to CPU since no GPU found
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

        accel1.Info.Returns(info1);
        accel2.Info.Returns(info2);
        accel3.Info.Returns(info3);

        provider.Name.Returns("Provider");
        provider.DiscoverAsync(default).Returns(new[] { accel1, accel2, accel3 });

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
        result.Should().BeSameAs(accel3); // GPU2 with 4GB
    }

    [Fact]
    public async Task CreateContext_WithValidAccelerator_ShouldReturnContext()
    {
        // Arrange
        await using var manager = new DefaultAcceleratorManager(_logger);
        var provider = Substitute.For<IAcceleratorProvider>();
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo(AcceleratorType.CPU, "CPU", "1.0", 1024 * 1024 * 1024);
        accelerator.Info.Returns(info);

        provider.Name.Returns("Provider");
        provider.DiscoverAsync(default).Returns(new[] { accelerator });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act  
        var context = manager.CreateContext(accelerator);

        // Assert
        context.Should().NotBeNull();
        context.DeviceId.Should().Be(0);
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

        accel1.Info.Returns(info1);
        accel2.Info.Returns(info2);

        provider.Name.Returns("Provider");
        provider.DiscoverAsync(default).Returns(new[] { accel1, accel2 });

        manager.RegisterProvider(provider);
        await manager.InitializeAsync();

        // Act
        await manager.DisposeAsync();

        // Assert
        await accel1.Received(1).DisposeAsync();
        await accel2.Received(1).DisposeAsync();
    }
}
