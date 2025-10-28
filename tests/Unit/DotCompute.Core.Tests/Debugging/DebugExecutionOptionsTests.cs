// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Debugging;
using FluentAssertions;
using Xunit;

namespace DotCompute.Core.Tests.Debugging;

/// <summary>
/// Comprehensive unit tests for DebugExecutionOptions.
/// Tests configuration and profile functionality.
/// </summary>
public class DebugExecutionOptionsTests
{
    #region Constructor and Default Values Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var options = new DebugExecutionOptions();

        // Assert
        options.EnableDebugHooks.Should().BeTrue();
        options.ValidateBeforeExecution.Should().BeTrue();
        options.ValidateAfterExecution.Should().BeFalse();
        options.FailOnValidationErrors.Should().BeFalse();
        options.EnableCrossBackendValidation.Should().BeFalse();
        options.CrossValidationProbability.Should().Be(0.1);
        options.ValidationTolerance.Should().Be(1e-6f);
        options.EnablePerformanceMonitoring.Should().BeTrue();
        options.StorePerformanceHistory.Should().BeFalse();
        options.TestDeterminism.Should().BeFalse();
        options.AnalyzeErrorsOnFailure.Should().BeTrue();
    }

    #endregion

    #region Property Tests

    [Fact]
    public void EnableDebugHooks_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.EnableDebugHooks = false;

        // Assert
        options.EnableDebugHooks.Should().BeFalse();
    }

    [Fact]
    public void ValidateBeforeExecution_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.ValidateBeforeExecution = false;

        // Assert
        options.ValidateBeforeExecution.Should().BeFalse();
    }

    [Fact]
    public void ValidateAfterExecution_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.ValidateAfterExecution = true;

        // Assert
        options.ValidateAfterExecution.Should().BeTrue();
    }

    [Fact]
    public void FailOnValidationErrors_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.FailOnValidationErrors = true;

        // Assert
        options.FailOnValidationErrors.Should().BeTrue();
    }

    [Fact]
    public void EnableCrossBackendValidation_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.EnableCrossBackendValidation = true;

        // Assert
        options.EnableCrossBackendValidation.Should().BeTrue();
    }

    [Fact]
    public void CrossValidationProbability_SetToCustomValue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();
        var customProbability = 0.5;

        // Act
        options.CrossValidationProbability = customProbability;

        // Assert
        options.CrossValidationProbability.Should().Be(customProbability);
    }

    [Fact]
    public void ValidationTolerance_SetToCustomValue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();
        var customTolerance = 1e-4f;

        // Act
        options.ValidationTolerance = customTolerance;

        // Assert
        options.ValidationTolerance.Should().Be(customTolerance);
    }

    [Fact]
    public void EnablePerformanceMonitoring_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.EnablePerformanceMonitoring = false;

        // Assert
        options.EnablePerformanceMonitoring.Should().BeFalse();
    }

    [Fact]
    public void StorePerformanceHistory_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.StorePerformanceHistory = true;

        // Assert
        options.StorePerformanceHistory.Should().BeTrue();
    }

    [Fact]
    public void TestDeterminism_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.TestDeterminism = true;

        // Assert
        options.TestDeterminism.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeErrorsOnFailure_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.AnalyzeErrorsOnFailure = false;

        // Assert
        options.AnalyzeErrorsOnFailure.Should().BeFalse();
    }

    #endregion

    #region Profile Configuration Tests

    [Fact]
    public void DevelopmentProfile_ShouldEnableAllDebugFeatures()
    {
        // Arrange & Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true,
            ValidateAfterExecution = true,
            EnableCrossBackendValidation = true,
            CrossValidationProbability = 1.0,
            EnablePerformanceMonitoring = true,
            TestDeterminism = true,
            AnalyzeErrorsOnFailure = true
        };

        // Assert
        options.EnableDebugHooks.Should().BeTrue();
        options.ValidateBeforeExecution.Should().BeTrue();
        options.ValidateAfterExecution.Should().BeTrue();
        options.EnableCrossBackendValidation.Should().BeTrue();
        options.EnablePerformanceMonitoring.Should().BeTrue();
        options.TestDeterminism.Should().BeTrue();
        options.AnalyzeErrorsOnFailure.Should().BeTrue();
    }

    [Fact]
    public void TestingProfile_ShouldEnableValidationFeatures()
    {
        // Arrange & Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true,
            ValidateAfterExecution = true,
            EnableCrossBackendValidation = true,
            CrossValidationProbability = 0.5,
            TestDeterminism = true,
            FailOnValidationErrors = true
        };

        // Assert
        options.EnableDebugHooks.Should().BeTrue();
        options.ValidateBeforeExecution.Should().BeTrue();
        options.ValidateAfterExecution.Should().BeTrue();
        options.EnableCrossBackendValidation.Should().BeTrue();
        options.TestDeterminism.Should().BeTrue();
        options.FailOnValidationErrors.Should().BeTrue();
    }

    [Fact]
    public void ProductionProfile_ShouldMinimizeOverhead()
    {
        // Arrange & Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = false,
            ValidateAfterExecution = false,
            EnableCrossBackendValidation = false,
            EnablePerformanceMonitoring = true,
            StorePerformanceHistory = false,
            TestDeterminism = false,
            AnalyzeErrorsOnFailure = true
        };

        // Assert
        options.EnableDebugHooks.Should().BeTrue();
        options.ValidateBeforeExecution.Should().BeFalse();
        options.ValidateAfterExecution.Should().BeFalse();
        options.EnableCrossBackendValidation.Should().BeFalse();
        options.EnablePerformanceMonitoring.Should().BeTrue();
        options.TestDeterminism.Should().BeFalse();
    }

    [Fact]
    public void DisabledProfile_ShouldDisableAllDebugFeatures()
    {
        // Arrange & Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = false,
            ValidateBeforeExecution = false,
            ValidateAfterExecution = false,
            EnableCrossBackendValidation = false,
            EnablePerformanceMonitoring = false,
            TestDeterminism = false,
            AnalyzeErrorsOnFailure = false
        };

        // Assert
        options.EnableDebugHooks.Should().BeFalse();
        options.ValidateBeforeExecution.Should().BeFalse();
        options.ValidateAfterExecution.Should().BeFalse();
        options.EnableCrossBackendValidation.Should().BeFalse();
        options.EnablePerformanceMonitoring.Should().BeFalse();
        options.TestDeterminism.Should().BeFalse();
        options.AnalyzeErrorsOnFailure.Should().BeFalse();
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public void CrossValidationProbability_SetToZero_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.CrossValidationProbability = 0.0;

        // Assert
        options.CrossValidationProbability.Should().Be(0.0);
    }

    [Fact]
    public void CrossValidationProbability_SetToOne_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.CrossValidationProbability = 1.0;

        // Assert
        options.CrossValidationProbability.Should().Be(1.0);
    }

    [Fact]
    public void ValidationTolerance_SetToMinimumValue_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.ValidationTolerance = float.Epsilon;

        // Assert
        options.ValidationTolerance.Should().Be(float.Epsilon);
    }

    [Fact]
    public void ValidationTolerance_SetToLargeValue_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions();

        // Act
        options.ValidationTolerance = 1.0f;

        // Assert
        options.ValidationTolerance.Should().Be(1.0f);
    }

    #endregion

    #region Object Initialization Tests

    [Fact]
    public void ObjectInitializer_WithAllProperties_ShouldSetCorrectly()
    {
        // Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = false,
            ValidateBeforeExecution = false,
            ValidateAfterExecution = true,
            FailOnValidationErrors = true,
            EnableCrossBackendValidation = true,
            CrossValidationProbability = 0.75,
            ValidationTolerance = 1e-5f,
            EnablePerformanceMonitoring = false,
            StorePerformanceHistory = true,
            TestDeterminism = true,
            AnalyzeErrorsOnFailure = false
        };

        // Assert
        options.EnableDebugHooks.Should().BeFalse();
        options.ValidateBeforeExecution.Should().BeFalse();
        options.ValidateAfterExecution.Should().BeTrue();
        options.FailOnValidationErrors.Should().BeTrue();
        options.EnableCrossBackendValidation.Should().BeTrue();
        options.CrossValidationProbability.Should().Be(0.75);
        options.ValidationTolerance.Should().Be(1e-5f);
        options.EnablePerformanceMonitoring.Should().BeFalse();
        options.StorePerformanceHistory.Should().BeTrue();
        options.TestDeterminism.Should().BeTrue();
        options.AnalyzeErrorsOnFailure.Should().BeFalse();
    }

    #endregion

    #region Combination Tests

    [Fact]
    public void ValidateBeforeAndAfter_BothEnabled_ShouldBeValid()
    {
        // Act
        var options = new DebugExecutionOptions
        {
            ValidateBeforeExecution = true,
            ValidateAfterExecution = true
        };

        // Assert
        options.ValidateBeforeExecution.Should().BeTrue();
        options.ValidateAfterExecution.Should().BeTrue();
    }

    [Fact]
    public void PerformanceFeatures_AllEnabled_ShouldBeValid()
    {
        // Act
        var options = new DebugExecutionOptions
        {
            EnablePerformanceMonitoring = true,
            StorePerformanceHistory = true
        };

        // Assert
        options.EnablePerformanceMonitoring.Should().BeTrue();
        options.StorePerformanceHistory.Should().BeTrue();
    }

    [Fact]
    public void ValidationFeatures_AllEnabled_ShouldBeValid()
    {
        // Act
        var options = new DebugExecutionOptions
        {
            ValidateBeforeExecution = true,
            ValidateAfterExecution = true,
            EnableCrossBackendValidation = true,
            TestDeterminism = true,
            FailOnValidationErrors = true
        };

        // Assert
        options.ValidateBeforeExecution.Should().BeTrue();
        options.ValidateAfterExecution.Should().BeTrue();
        options.EnableCrossBackendValidation.Should().BeTrue();
        options.TestDeterminism.Should().BeTrue();
        options.FailOnValidationErrors.Should().BeTrue();
    }

    #endregion

    #region Realistic Scenario Tests

    [Fact]
    public void CIEnvironment_ShouldEnableStrictValidation()
    {
        // Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true,
            ValidateAfterExecution = true,
            FailOnValidationErrors = true,
            EnableCrossBackendValidation = true,
            CrossValidationProbability = 1.0,
            TestDeterminism = true
        };

        // Assert
        options.FailOnValidationErrors.Should().BeTrue();
        options.TestDeterminism.Should().BeTrue();
        options.CrossValidationProbability.Should().Be(1.0);
    }

    [Fact]
    public void LocalDevelopment_ShouldEnableHelpfulDebugging()
    {
        // Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true,
            ValidateAfterExecution = true,
            FailOnValidationErrors = false,
            EnablePerformanceMonitoring = true,
            AnalyzeErrorsOnFailure = true
        };

        // Assert
        options.EnableDebugHooks.Should().BeTrue();
        options.ValidateBeforeExecution.Should().BeTrue();
        options.EnablePerformanceMonitoring.Should().BeTrue();
        options.FailOnValidationErrors.Should().BeFalse();
    }

    [Fact]
    public void ProductionDeployment_ShouldMinimizeOverheadButKeepMonitoring()
    {
        // Act
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = false,
            ValidateAfterExecution = false,
            EnableCrossBackendValidation = false,
            CrossValidationProbability = 0.01,
            EnablePerformanceMonitoring = true,
            StorePerformanceHistory = true,
            TestDeterminism = false
        };

        // Assert
        options.ValidateBeforeExecution.Should().BeFalse();
        options.ValidateAfterExecution.Should().BeFalse();
        options.EnablePerformanceMonitoring.Should().BeTrue();
        options.CrossValidationProbability.Should().Be(0.01);
    }

    #endregion
}
