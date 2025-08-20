// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Tests for Metal backend validation functionality.
/// These tests validate the validation utility itself, not the actual Metal backend.
/// </summary>
public sealed class MetalValidationTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_InitializesCorrectly()
    {
        // Arrange & Act
        var result = new ValidationResult();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Info);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_AddError_UpdatesState()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddError("Test error");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Single(result.Errors);
        Assert.Contains("Test error", result.Errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_AddWarning_DoesNotAffectValidity()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddWarning("Test warning");

        // Assert
        Assert.True(result.IsValid);  // Still valid with warnings
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Single(result.Warnings);
        Assert.Contains("Test warning", result.Warnings);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_AddInfo_DoesNotAffectValidity()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddInfo("Test info");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Single(result.Info);
        Assert.Contains("Test info", result.Info);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_Merge_CombinesResults()
    {
        // Arrange
        var result1 = new ValidationResult();
        result1.AddError("Error 1");
        result1.AddWarning("Warning 1");
        result1.AddInfo("Info 1");

        var result2 = new ValidationResult();
        result2.AddError("Error 2");
        result2.AddWarning("Warning 2");
        result2.AddInfo("Info 2");

        // Act
        result1.Merge(result2);

        // Assert
        Assert.False(result1.IsValid);
        Assert.Equal(2, result1.ErrorCount);
        Assert.Equal(2, result1.WarningCount);
        Assert.Equal(2, result1.Info.Count);
        Assert.Contains("Error 1", result1.Errors);
        Assert.Contains("Error 2", result1.Errors);
        Assert.Contains("Warning 1", result1.Warnings);
        Assert.Contains("Warning 2", result1.Warnings);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_GenerateReport_ProducesFormattedOutput()
    {
        // Arrange
        var result = new ValidationResult();
        result.AddError("Test error");
        result.AddWarning("Test warning");
        result.AddInfo("Test info");

        // Act
        var report = result.GenerateReport();

        // Assert
        Assert.NotNull(report);
        Assert.Contains("Metal Backend Validation Report", report);
        Assert.Contains("Status: FAILED", report);
        Assert.Contains("Errors: 1", report);
        Assert.Contains("Warnings: 1", report);
        Assert.Contains("❌ Test error", report);
        Assert.Contains("⚠️  Test warning", report);
        Assert.Contains("ℹ️  Test info", report);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_ToString_ReturnsCorrectFormat()
    {
        // Arrange
        var validResult = new ValidationResult();
        validResult.AddInfo("Test info");

        var invalidResult = new ValidationResult();
        invalidResult.AddError("Test error");
        invalidResult.AddWarning("Test warning");

        // Act & Assert
        Assert.Contains("Valid", validResult.ToString());
        Assert.Contains("Errors: 0", validResult.ToString());
        Assert.Contains("Warnings: 0", validResult.ToString());

        Assert.Contains("Invalid", invalidResult.ToString());
        Assert.Contains("Errors: 1", invalidResult.ToString());
        Assert.Contains("Warnings: 1", invalidResult.ToString());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "NonMacOS")]
    public void ValidateConfiguration_OnNonMacOS_ReturnsErrorResult()
    {
        // This test should only run on non-macOS platforms
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Skip on macOS
            return;
        }

        // Act
        var result = MetalValidation.ValidateConfiguration(_logger);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.ErrorCount > 0);
        Assert.Contains(result.Errors, error => error.Contains("Metal backend is only supported on macOS"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_HandlesNullInputs()
    {
        // Arrange
        var result = new ValidationResult();

        // Act & Assert (should not throw)
        result.AddError(null!);
        result.AddWarning(null!);
        result.AddInfo(null!);
        result.Merge(null!);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Info);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Platform", "macOS")]
    public void ValidationResult_HandlesEmptyStrings()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddError("");
        result.AddWarning(string.Empty);
        result.AddInfo("   ");

        // Assert
        Assert.True(result.IsValid);  // Empty strings should not count as errors
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Info);
    }
}