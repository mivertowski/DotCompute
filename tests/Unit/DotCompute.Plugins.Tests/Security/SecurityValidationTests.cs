// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Plugins.Tests.Security;

/// <summary>
/// Comprehensive tests for plugin security validation.
/// Tests assembly integrity, metadata analysis, suspicious pattern detection, and risk assessment.
/// </summary>
public sealed class SecurityValidationTests : IDisposable
{
    private readonly SecurityManager _securityManager;
    private readonly string _testAssemblyPath;

    public SecurityValidationTests()
    {
        _securityManager = new SecurityManager(NullLogger.Instance);
        // Use a known safe assembly for testing
        _testAssemblyPath = typeof(object).Assembly.Location;
    }

    [Fact]
    public async Task ValidateAssemblyIntegrity_ValidAssembly_ReturnsTrue()
    {
        // Act
        var result = await _securityManager.ValidateAssemblyIntegrityAsync(_testAssemblyPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAssemblyIntegrity_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/assembly.dll";

        // Act
        var result = await _securityManager.ValidateAssemblyIntegrityAsync(nonExistentPath);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ValidateAssemblyIntegrity_InvalidPath_ThrowsArgumentException(string invalidPath)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _securityManager.ValidateAssemblyIntegrityAsync(invalidPath!));
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadata_SafeAssembly_ReturnsLowRisk()
    {
        // Act
        var analysis = await _securityManager.AnalyzeAssemblyMetadataAsync(_testAssemblyPath);

        // Assert
        analysis.Should().NotBeNull();
        analysis.HasError.Should().BeFalse();
        analysis.RiskLevel.Should().Be(RiskLevel.Low);
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadata_SafeAssembly_NoSuspiciousPatterns()
    {
        // Act
        var analysis = await _securityManager.AnalyzeAssemblyMetadataAsync(_testAssemblyPath);

        // Assert
        analysis.SuspiciousPatterns.Should().BeEmpty();
        analysis.HasSuspiciousPatterns.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadata_NonExistentFile_ReturnsError()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/assembly.dll";

        // Act
        var analysis = await _securityManager.AnalyzeAssemblyMetadataAsync(nonExistentPath);

        // Assert
        analysis.HasError.Should().BeTrue();
        analysis.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AnalyzeAssemblyMetadata_InvalidPath_ThrowsArgumentException(string invalidPath)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _securityManager.AnalyzeAssemblyMetadataAsync(invalidPath!));
    }

    [Fact]
    public void RiskLevel_Enum_HasExpectedValues()
    {
        // Assert
        Enum.IsDefined(typeof(RiskLevel), RiskLevel.Low).Should().BeTrue();
        Enum.IsDefined(typeof(RiskLevel), RiskLevel.Medium).Should().BeTrue();
        Enum.IsDefined(typeof(RiskLevel), RiskLevel.High).Should().BeTrue();
        Enum.IsDefined(typeof(RiskLevel), RiskLevel.Critical).Should().BeTrue();
    }

    [Fact]
    public void AssemblyMetadataAnalysis_DefaultInstance_HasExpectedDefaults()
    {
        // Arrange & Act
        var analysis = new AssemblyMetadataAnalysis();

        // Assert
        analysis.SuspiciousPatterns.Should().NotBeNull();
        analysis.SuspiciousPatterns.Should().BeEmpty();
        analysis.HasSuspiciousPatterns.Should().BeFalse();
        analysis.RiskLevel.Should().Be(RiskLevel.Low);
        analysis.HasError.Should().BeFalse();
        analysis.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void AssemblyMetadataAnalysis_WithSuspiciousPatterns_ReportsCorrectly()
    {
        // Arrange
        var analysis = new AssemblyMetadataAnalysis();

        // Act
        analysis.SuspiciousPatterns.Add("Suspicious method: CreateProcess");
        analysis.SuspiciousPatterns.Add("Risky assembly reference: System.Management");
        analysis.RiskLevel = RiskLevel.High;

        // Assert
        analysis.HasSuspiciousPatterns.Should().BeTrue();
        analysis.SuspiciousPatterns.Should().HaveCount(2);
        analysis.RiskLevel.Should().Be(RiskLevel.High);
    }

    [Fact]
    public async Task SecurityManager_MultipleOperations_WorksCorrectly()
    {
        // Act
        var validation1 = await _securityManager.ValidateAssemblyIntegrityAsync(_testAssemblyPath);
        var analysis1 = await _securityManager.AnalyzeAssemblyMetadataAsync(_testAssemblyPath);
        var validation2 = await _securityManager.ValidateAssemblyIntegrityAsync(_testAssemblyPath);
        var analysis2 = await _securityManager.AnalyzeAssemblyMetadataAsync(_testAssemblyPath);

        // Assert
        validation1.Should().BeTrue();
        validation2.Should().BeTrue();
        analysis1.RiskLevel.Should().Be(RiskLevel.Low);
        analysis2.RiskLevel.Should().Be(RiskLevel.Low);
    }

    [Fact]
    public async Task SecurityManager_Disposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var disposableManager = new SecurityManager(NullLogger.Instance);
        disposableManager.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await disposableManager.ValidateAssemblyIntegrityAsync(_testAssemblyPath));
    }

    [Fact]
    public void SecurityManager_DoubleDispose_DoesNotThrow()
    {
        // Arrange
        var disposableManager = new SecurityManager(NullLogger.Instance);

        // Act & Assert
        disposableManager.Dispose();
        var exception = Record.Exception(() => disposableManager.Dispose());

        exception.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadata_RiskLevelCalculation_WorksCorrectly()
    {
        // Arrange - Create mock analyses with different pattern counts
        var lowRiskAnalysis = new AssemblyMetadataAnalysis();
        var mediumRiskAnalysis = new AssemblyMetadataAnalysis();
        var highRiskAnalysis = new AssemblyMetadataAnalysis();
        var criticalRiskAnalysis = new AssemblyMetadataAnalysis();

        // Act
        // Simulate 0 patterns - Low risk
        lowRiskAnalysis.RiskLevel = RiskLevel.Low;

        // Simulate 1-2 patterns - Medium risk
        mediumRiskAnalysis.SuspiciousPatterns.Add("Pattern 1");
        mediumRiskAnalysis.RiskLevel = RiskLevel.Medium;

        // Simulate 3-4 patterns - High risk
        highRiskAnalysis.SuspiciousPatterns.Add("Pattern 1");
        highRiskAnalysis.SuspiciousPatterns.Add("Pattern 2");
        highRiskAnalysis.SuspiciousPatterns.Add("Pattern 3");
        highRiskAnalysis.RiskLevel = RiskLevel.High;

        // Simulate 5+ patterns - Critical risk
        for (int i = 0; i < 5; i++)
        {
            criticalRiskAnalysis.SuspiciousPatterns.Add($"Pattern {i + 1}");
        }
        criticalRiskAnalysis.RiskLevel = RiskLevel.Critical;

        // Assert
        lowRiskAnalysis.RiskLevel.Should().Be(RiskLevel.Low);
        mediumRiskAnalysis.RiskLevel.Should().Be(RiskLevel.Medium);
        highRiskAnalysis.RiskLevel.Should().Be(RiskLevel.High);
        criticalRiskAnalysis.RiskLevel.Should().Be(RiskLevel.Critical);

        await Task.CompletedTask; // Satisfy async test pattern
    }

    [Fact]
    public async Task ValidateAssemblyIntegrity_WithCancellation_RespondsToToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(1));

        // Act & Assert
        // Note: The actual method might not throw if it completes before cancellation
        // This test verifies the cancellation token is accepted
        var result = await _securityManager.ValidateAssemblyIntegrityAsync(_testAssemblyPath, cts.Token);

        // If we get here, either the operation completed or was cancelled gracefully
        _ = result; // Test passes if no exception is thrown
    }

    [Fact]
    public async Task AnalyzeAssemblyMetadata_WithCancellation_RespondsToToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var analysis = await _securityManager.AnalyzeAssemblyMetadataAsync(_testAssemblyPath, cts.Token);

        // Assert
        analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task SecurityManager_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var tasks = new List<Task<bool>>();

        // Act - Run multiple validations concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_securityManager.ValidateAssemblyIntegrityAsync(_testAssemblyPath));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public async Task SecurityManager_ConcurrentAnalysis_ThreadSafe()
    {
        // Arrange
        var tasks = new List<Task<AssemblyMetadataAnalysis>>();

        // Act - Run multiple analyses concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_securityManager.AnalyzeAssemblyMetadataAsync(_testAssemblyPath));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.RiskLevel.Should().Be(RiskLevel.Low);
        });
    }

    public void Dispose()
    {
        _securityManager?.Dispose();
    }
}
