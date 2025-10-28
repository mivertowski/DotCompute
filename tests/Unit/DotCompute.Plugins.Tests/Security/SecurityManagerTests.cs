// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Tests.Security;

/// <summary>
/// Tests for SecurityManager covering assembly validation and analysis.
/// </summary>
public sealed class SecurityManagerTests : IDisposable
{
    private readonly ILogger _mockLogger;
    private SecurityManager? _securityManager;

    public SecurityManagerTests()
    {
        _mockLogger = Substitute.For<ILogger>();
    }

    [Fact]
    public void Constructor_WithLogger_ShouldInitialize()
    {
        // Arrange & Act
        var manager = new SecurityManager(_mockLogger);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new SecurityManager(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAssemblyIntegrityAsync_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);
        var assemblyPath = "/non/existent/path.dll";

        // Act
        var result = await _securityManager.ValidateAssemblyIntegrityAsync(assemblyPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAssemblyIntegrityAsync_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);

        // Act
        var act = async () => await _securityManager.ValidateAssemblyIntegrityAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateAssemblyIntegrityAsync_WithWhitespacePath_ShouldThrowArgumentException()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);

        // Act
        var act = async () => await _securityManager.ValidateAssemblyIntegrityAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateAssemblyIntegrityAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);
        _securityManager.Dispose();

        // Act
        var act = async () => await _securityManager.ValidateAssemblyIntegrityAsync("/test/path.dll");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ValidateAssemblyIntegrityAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _securityManager.ValidateAssemblyIntegrityAsync(
            "/test/path.dll", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadataAsync_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);

        // Act
        var act = async () => await _securityManager.AnalyzeAssemblyMetadataAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadataAsync_WithNonExistentFile_ShouldReturnErrorResult()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);
        var assemblyPath = "/non/existent/path.dll";

        // Act
        var result = await _securityManager.AnalyzeAssemblyMetadataAsync(assemblyPath);

        // Assert
        result.Should().NotBeNull();
        result.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadataAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _securityManager = new SecurityManager(_mockLogger);
        _securityManager.Dispose();

        // Act
        var act = async () => await _securityManager.AnalyzeAssemblyMetadataAsync("/test/path.dll");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var manager = new SecurityManager(_mockLogger);

        // Act
        manager.Dispose();

        // Assert - should not throw
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var manager = new SecurityManager(_mockLogger);

        // Act
        manager.Dispose();
        manager.Dispose();

        // Assert - should not throw
    }

    public void Dispose()
    {
        _securityManager?.Dispose();
    }
}

/// <summary>
/// Tests for AssemblyMetadataAnalysis class.
/// </summary>
public sealed class AssemblyMetadataAnalysisTests
{
    [Fact]
    public void Constructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var analysis = new AssemblyMetadataAnalysis();

        // Assert
        analysis.SuspiciousPatterns.Should().NotBeNull();
        analysis.SuspiciousPatterns.Should().BeEmpty();
        analysis.RiskLevel.Should().Be(RiskLevel.Low);
        analysis.HasError.Should().BeFalse();
    }

    [Fact]
    public void HasSuspiciousPatterns_WithNoPatterns_ShouldReturnFalse()
    {
        // Arrange
        var analysis = new AssemblyMetadataAnalysis();

        // Act & Assert
        analysis.HasSuspiciousPatterns.Should().BeFalse();
    }

    [Fact]
    public void HasSuspiciousPatterns_WithPatterns_ShouldReturnTrue()
    {
        // Arrange
        var analysis = new AssemblyMetadataAnalysis();
        analysis.SuspiciousPatterns.Add("Suspicious pattern found");

        // Act & Assert
        analysis.HasSuspiciousPatterns.Should().BeTrue();
    }

    [Theory]
    [InlineData(RiskLevel.Low)]
    [InlineData(RiskLevel.Medium)]
    [InlineData(RiskLevel.High)]
    [InlineData(RiskLevel.Critical)]
    public void RiskLevel_ShouldBeSettable(RiskLevel level)
    {
        // Arrange
        var analysis = new AssemblyMetadataAnalysis();

        // Act
        analysis.RiskLevel = level;

        // Assert
        analysis.RiskLevel.Should().Be(level);
    }

    [Fact]
    public void ErrorMessage_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var analysis = new AssemblyMetadataAnalysis();

        // Assert
        analysis.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ErrorMessage_ShouldBeSettable()
    {
        // Arrange
        var analysis = new AssemblyMetadataAnalysis();
        var errorMessage = "Test error message";

        // Act
        analysis.ErrorMessage = errorMessage;

        // Assert
        analysis.ErrorMessage.Should().Be(errorMessage);
        analysis.HasError.Should().BeFalse(); // HasError is separate property
    }
}

/// <summary>
/// Tests for RiskLevel enum.
/// </summary>
public sealed class RiskLevelTests
{
    [Fact]
    public void RiskLevel_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<RiskLevel>().Should().Contain(new[]
        {
            RiskLevel.Low,
            RiskLevel.Medium,
            RiskLevel.High,
            RiskLevel.Critical
        });
    }

    [Fact]
    public void RiskLevel_ShouldBeOrdered()
    {
        // Assert
        ((int)RiskLevel.Low).Should().BeLessThan((int)RiskLevel.Medium);
        ((int)RiskLevel.Medium).Should().BeLessThan((int)RiskLevel.High);
        ((int)RiskLevel.High).Should().BeLessThan((int)RiskLevel.Critical);
    }
}
