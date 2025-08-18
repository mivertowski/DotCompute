// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Services;
using DotCompute.Tests;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Runtime.Tests;

/// <summary>
/// Comprehensive unit tests for RuntimeAcceleratorManager to achieve 90%+ coverage.
/// Tests all public methods, properties, error conditions, and edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Mock)]
[Trait("Category", TestCategories.CI)]
public sealed class RuntimeAcceleratorManagerTests : IDisposable
{
    private readonly Mock<ILogger<RuntimeAcceleratorManager>> _mockLogger;
    private readonly RuntimeAcceleratorManager _manager;
    private readonly Mock<IAcceleratorProvider> _mockProvider;

    public RuntimeAcceleratorManagerTests()
    {
        _mockLogger = new Mock<ILogger<RuntimeAcceleratorManager>>();
        _manager = new RuntimeAcceleratorManager(_mockLogger.Object);
        _mockProvider = new Mock<IAcceleratorProvider>();
    }

    #region Constructor Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Constructor_WithValidLogger_ShouldSucceed()
    {
        // Arrange & Act
        var manager = new RuntimeAcceleratorManager(_mockLogger.Object);

        // Assert
        manager.Should().NotBeNull();
        manager.IsDisposed.Should().BeFalse();
        manager.Count.Should().Be(0);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new RuntimeAcceleratorManager(null!);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("logger");
    }

    #endregion

    #region Property Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void IsDisposed_InitiallyFalse_ShouldReturnFalse()
    {
        // Arrange & Act
        var isDisposed = _manager.IsDisposed;

        // Assert
        isDisposed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Default_BeforeInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange, Act & Assert
        var act = () => _manager.Default;
        act.Should().Throw<InvalidOperationException>()
           .Which.Message.Should().Contain("No accelerators available");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task Default_AfterInitialization_ShouldReturnFirstAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var defaultAccelerator = _manager.Default;

        // Assert
        defaultAccelerator.Should().NotBeNull();
        defaultAccelerator.Info.Id.Should().Be("default_cpu");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void AvailableAccelerators_InitiallyEmpty_ShouldReturnEmptyList()
    {
        // Arrange & Act
        var accelerators = _manager.AvailableAccelerators;

        // Assert
        accelerators.Should().NotBeNull();
        accelerators.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Count_InitiallyZero_ShouldReturnZero()
    {
        // Arrange & Act
        var count = _manager.Count;

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region InitializeAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task InitializeAsync_FirstCall_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        await _manager.InitializeAsync();

        // Assert
        _manager.Count.Should().BeGreaterThan(0);
        _manager.AvailableAccelerators.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task InitializeAsync_SecondCall_ShouldNotReinitialize()
    {
        // Arrange
        await _manager.InitializeAsync();
        var firstCount = _manager.Count;

        // Act
        await _manager.InitializeAsync();

        // Assert
        _manager.Count.Should().Be(firstCount);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task InitializeAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = async () => await _manager.InitializeAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task InitializeAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = async () => await _manager.InitializeAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task InitializeAsync_WithProvider_ShouldDiscoverFromProvider()
    {
        // Arrange
        var mockAccelerator = CreateMockAccelerator("provider_gpu", AcceleratorType.GPU);
        _mockProvider.Setup(p => p.Name).Returns("TestProvider");
        _mockProvider.Setup(p => p.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mockAccelerator });

        _manager.RegisterProvider(_mockProvider.Object);

        // Act
        await _manager.InitializeAsync();

        // Assert
        _manager.Count.Should().BeGreaterThan(1); // Default CPU + discovered accelerator
        _manager.AvailableAccelerators.Should().Contain(a => a.Info.Id == "provider_gpu");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task InitializeAsync_ProviderThrows_ShouldContinueWithOtherProviders()
    {
        // Arrange
        _mockProvider.Setup(p => p.Name).Returns("FailingProvider");
        _mockProvider.Setup(p => p.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider failed"));

        _manager.RegisterProvider(_mockProvider.Object);

        // Act & Assert
        var act = async () => await _manager.InitializeAsync();
        await act.Should().NotThrowAsync();
        _manager.Count.Should().BeGreaterThan(0); // Should still have default CPU accelerator
    }

    #endregion

    #region GetAccelerator Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAccelerator_WithValidIndex_ShouldReturnAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerator = _manager.GetAccelerator(0);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Info.Id.Should().Be("default_cpu");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task GetAccelerator_WithInvalidIndex_ShouldThrowArgumentOutOfRangeException(int invalidIndex)
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act & Assert
        var act = () => _manager.GetAccelerator(invalidIndex);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("index");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAccelerator_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = () => _manager.GetAccelerator(0);
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region GetAcceleratorById Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorById_WithValidId_ShouldReturnAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerator = _manager.GetAcceleratorById("default_cpu");

        // Assert
        accelerator.Should().NotBeNull();
        accelerator!.Info.Id.Should().Be("default_cpu");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorById_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerator = _manager.GetAcceleratorById("nonexistent");

        // Assert
        accelerator.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public void GetAcceleratorById_WithInvalidId_ShouldThrowArgumentException(string? invalidId)
    {
        // Arrange, Act & Assert
        var act = () => _manager.GetAcceleratorById(invalidId!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorById_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = () => _manager.GetAcceleratorById("test");
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region GetAcceleratorsByType Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorsByType_WithCPU_ShouldReturnCPUAccelerators()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerators = _manager.GetAcceleratorsByType(AcceleratorType.CPU);

        // Assert
        accelerators.Should().NotBeEmpty();
        accelerators.Should().OnlyContain(a => a.Info.DeviceType == AcceleratorType.CPU.ToString());
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorsByType_WithGPU_ShouldReturnEmpty()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerators = _manager.GetAcceleratorsByType(AcceleratorType.GPU);

        // Assert
        accelerators.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorsByType_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = () => _manager.GetAcceleratorsByType(AcceleratorType.CPU);
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region SelectBest Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task SelectBest_WithBasicCriteria_ShouldReturnAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();
        var criteria = new AcceleratorSelectionCriteria
        {
            PreferredType = AcceleratorType.CPU
        };

        // Act
        var accelerator = _manager.SelectBest(criteria);

        // Assert
        accelerator.Should().NotBeNull();
        _ = accelerator!.Info.DeviceType.Should().Be(AcceleratorType.CPU.ToString());
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task SelectBest_WithMemoryRequirement_ShouldReturnSuitableAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();
        var criteria = new AcceleratorSelectionCriteria
        {
            MinimumMemory = 1024 * 1024 // 1MB
        };

        // Act
        var accelerator = _manager.SelectBest(criteria);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator!.Info.TotalMemory.Should().BeGreaterOrEqualTo(1024 * 1024);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task SelectBest_WithUnrealisticMemoryRequirement_ShouldReturnNull()
    {
        // Arrange
        await _manager.InitializeAsync();
        var criteria = new AcceleratorSelectionCriteria
        {
            MinimumMemory = long.MaxValue
        };

        // Act
        var accelerator = _manager.SelectBest(criteria);

        // Assert
        accelerator.Should().BeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task SelectBest_WithComputeCapabilityRequirement_ShouldReturnSuitableAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();
        var criteria = new AcceleratorSelectionCriteria
        {
            MinimumComputeCapability = new Version(1, 0)
        };

        // Act
        var accelerator = _manager.SelectBest(criteria);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator!.Info.ComputeCapability.Should().BeGreaterOrEqualTo(new Version(1, 0));
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task SelectBest_WithCustomScorer_ShouldUseCustomScoring()
    {
        // Arrange
        await _manager.InitializeAsync();
        var criteria = new AcceleratorSelectionCriteria
        {
            CustomScorer = acc => acc.Info.ComputeUnits * 2
        };

        // Act
        var accelerator = _manager.SelectBest(criteria);

        // Assert
        accelerator.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void SelectBest_WithNullCriteria_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => _manager.SelectBest(null!);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("criteria");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task SelectBest_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var criteria = new AcceleratorSelectionCriteria();
        await _manager.DisposeAsync();

        // Act & Assert
        var act = () => _manager.SelectBest(criteria);
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region CreateContext Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CreateContext_WithValidAccelerator_ShouldReturnContext()
    {
        // Arrange
        await _manager.InitializeAsync();
        var accelerator = _manager.Default;

        // Act
        var context = _manager.CreateContext(accelerator);

        // Assert
        context.Should().NotBeNull();
        context.Should().Be(accelerator.Context);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void CreateContext_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => _manager.CreateContext(null!);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("accelerator");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CreateContext_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockAccelerator = CreateMockAccelerator();
        await _manager.DisposeAsync();

        // Act & Assert
        var act = () => _manager.CreateContext(mockAccelerator);
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region RegisterProvider Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void RegisterProvider_WithValidProvider_ShouldSucceed()
    {
        // Arrange
        _mockProvider.Setup(p => p.Name).Returns("TestProvider");

        // Act & Assert
        var act = () => _manager.RegisterProvider(_mockProvider.Object);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void RegisterProvider_WithNullProvider_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => _manager.RegisterProvider(null!);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("provider");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task RegisterProvider_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = () => _manager.RegisterProvider(_mockProvider.Object);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void RegisterProvider_SameProviderTwice_ShouldNotThrow()
    {
        // Arrange
        _mockProvider.Setup(p => p.Name).Returns("TestProvider");

        // Act & Assert
        var act = () =>
        {
            _manager.RegisterProvider(_mockProvider.Object);
            _manager.RegisterProvider(_mockProvider.Object);
        };
        act.Should().NotThrow();
    }

    #endregion

    #region RefreshAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task RefreshAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act & Assert
        var act = async () => await _manager.RefreshAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task RefreshAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = async () => await _manager.RefreshAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task RefreshAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        await _manager.InitializeAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = async () => await _manager.RefreshAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region GetAcceleratorsAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorsAsync_ShouldReturnAllAccelerators()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerators = await _manager.GetAcceleratorsAsync();

        // Assert
        accelerators.Should().NotBeEmpty();
        accelerators.Should().HaveCount(_manager.Count);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorsAsync_WithType_ShouldReturnFilteredAccelerators()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerators = await _manager.GetAcceleratorsAsync(AcceleratorType.CPU);

        // Assert
        accelerators.Should().NotBeEmpty();
        accelerators.Should().OnlyContain(a => a.Info.DeviceType == AcceleratorType.CPU.ToString());
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetAcceleratorsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = async () => await _manager.GetAcceleratorsAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region GetBestAcceleratorAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetBestAcceleratorAsync_WithoutType_ShouldReturnFirstAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerator = await _manager.GetBestAcceleratorAsync();

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Should().Be(_manager.Default);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetBestAcceleratorAsync_WithType_ShouldReturnBestOfType()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerator = await _manager.GetBestAcceleratorAsync(AcceleratorType.CPU);

        // Assert
        accelerator.Should().NotBeNull();
        _ = accelerator!.Info.DeviceType.Should().Be(AcceleratorType.CPU.ToString());
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetBestAcceleratorAsync_WithUnavailableType_ShouldReturnNull()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerator = await _manager.GetBestAcceleratorAsync(AcceleratorType.GPU);

        // Assert
        accelerator.Should().BeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GetBestAcceleratorAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        await _manager.DisposeAsync();

        // Act & Assert
        var act = async () => await _manager.GetBestAcceleratorAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task DisposeAsync_ShouldDisposeAllAccelerators()
    {
        // Arrange
        await _manager.InitializeAsync();
        _ = _manager.Default; // Store reference for test

        // Act
        await _manager.DisposeAsync();

        // Assert
        _manager.IsDisposed.Should().BeTrue();
        _manager.Count.Should().Be(0);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act & Assert
        var act = async () =>
        {
            await _manager.DisposeAsync();
            await _manager.DisposeAsync();
            await _manager.DisposeAsync();
        };
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Performance)]
    public async Task InitializeAsync_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        const int taskCount = 10;

        // Act
        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => _manager.InitializeAsync().AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        _manager.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Performance)]
    public async Task GetAccelerator_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        await _manager.InitializeAsync();
        const int taskCount = 10;

        // Act
        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => Task.Run(() => _manager.GetAccelerator(0)))
            .ToArray();

        var accelerators = await Task.WhenAll(tasks);

        // Assert
        accelerators.Should().HaveCount(taskCount);
        accelerators.Should().OnlyContain(a => a.Info.Id == "default_cpu");
    }

    #endregion

    #region Performance Tests

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.Unit)]
    public async Task InitializeAsync_Performance_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _manager.InitializeAsync();

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
        _manager.Count.Should().BeGreaterThan(0);
    }

    #endregion

    #region Helper Methods

    private static IAccelerator CreateMockAccelerator(string id = "test_accelerator", AcceleratorType type = AcceleratorType.CPU)
    {
        var mockAccelerator = new Mock<IAccelerator>();
        var info = new AcceleratorInfo
        {
            Id = id,
            Name = $"Test {type}",
            DeviceType = type.ToString(),
            Vendor = "Test Vendor",
            DriverVersion = "1.0.0",
            TotalMemory = 1024 * 1024 * 1024, // 1GB
            AvailableMemory = 512 * 1024 * 1024, // 512MB
            ComputeUnits = 4,
            MaxClockFrequency = 2000,
            ComputeCapability = new Version(1, 0),
            IsUnifiedMemory = type == AcceleratorType.CPU
        };

        mockAccelerator.Setup(a => a.Info).Returns(info);
        mockAccelerator.Setup(a => a.Type).Returns(type);
        mockAccelerator.Setup(a => a.Context).Returns(new AcceleratorContext(IntPtr.Zero, 0));

        return mockAccelerator.Object;
    }

    #endregion

    public void Dispose()
    {
        _manager?.DisposeAsync().AsTask().Wait();
    }
}