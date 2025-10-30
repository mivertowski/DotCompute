// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Debugging;

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
        _ = options.EnableDebugHooks.Should().BeTrue();
        _ = options.ValidateBeforeExecution.Should().BeTrue();
        _ = options.ValidateAfterExecution.Should().BeFalse();
        _ = options.FailOnValidationErrors.Should().BeFalse();
        _ = options.EnableCrossBackendValidation.Should().BeFalse();
        _ = options.CrossValidationProbability.Should().Be(0.1);
        _ = options.ValidationTolerance.Should().Be(1e-6f);
        _ = options.EnablePerformanceMonitoring.Should().BeTrue();
        _ = options.StorePerformanceHistory.Should().BeFalse();
        _ = options.TestDeterminism.Should().BeFalse();
        _ = options.AnalyzeErrorsOnFailure.Should().BeTrue();
    }

    #endregion

    #region Property Tests

    [Fact]
    public void EnableDebugHooks_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            EnableDebugHooks = false
        };

        // Assert
        _ = options.EnableDebugHooks.Should().BeFalse();
    }

    [Fact]
    public void ValidateBeforeExecution_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            ValidateBeforeExecution = false
        };

        // Assert
        _ = options.ValidateBeforeExecution.Should().BeFalse();
    }

    [Fact]
    public void ValidateAfterExecution_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            ValidateAfterExecution = true
        };

        // Assert
        _ = options.ValidateAfterExecution.Should().BeTrue();
    }

    [Fact]
    public void FailOnValidationErrors_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            FailOnValidationErrors = true
        };

        // Assert
        _ = options.FailOnValidationErrors.Should().BeTrue();
    }

    [Fact]
    public void EnableCrossBackendValidation_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            EnableCrossBackendValidation = true
        };

        // Assert
        _ = options.EnableCrossBackendValidation.Should().BeTrue();
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
        _ = options.CrossValidationProbability.Should().Be(customProbability);
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
        _ = options.ValidationTolerance.Should().Be(customTolerance);
    }

    [Fact]
    public void EnablePerformanceMonitoring_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            EnablePerformanceMonitoring = false
        };

        // Assert
        _ = options.EnablePerformanceMonitoring.Should().BeFalse();
    }

    [Fact]
    public void StorePerformanceHistory_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            StorePerformanceHistory = true
        };

        // Assert
        _ = options.StorePerformanceHistory.Should().BeTrue();
    }

    [Fact]
    public void TestDeterminism_SetToTrue_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            TestDeterminism = true
        };

        // Assert
        _ = options.TestDeterminism.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeErrorsOnFailure_SetToFalse_ShouldUpdateValue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            AnalyzeErrorsOnFailure = false
        };

        // Assert
        _ = options.AnalyzeErrorsOnFailure.Should().BeFalse();
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
        _ = options.EnableDebugHooks.Should().BeTrue();
        _ = options.ValidateBeforeExecution.Should().BeTrue();
        _ = options.ValidateAfterExecution.Should().BeTrue();
        _ = options.EnableCrossBackendValidation.Should().BeTrue();
        _ = options.EnablePerformanceMonitoring.Should().BeTrue();
        _ = options.TestDeterminism.Should().BeTrue();
        _ = options.AnalyzeErrorsOnFailure.Should().BeTrue();
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
        _ = options.EnableDebugHooks.Should().BeTrue();
        _ = options.ValidateBeforeExecution.Should().BeTrue();
        _ = options.ValidateAfterExecution.Should().BeTrue();
        _ = options.EnableCrossBackendValidation.Should().BeTrue();
        _ = options.TestDeterminism.Should().BeTrue();
        _ = options.FailOnValidationErrors.Should().BeTrue();
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
        _ = options.EnableDebugHooks.Should().BeTrue();
        _ = options.ValidateBeforeExecution.Should().BeFalse();
        _ = options.ValidateAfterExecution.Should().BeFalse();
        _ = options.EnableCrossBackendValidation.Should().BeFalse();
        _ = options.EnablePerformanceMonitoring.Should().BeTrue();
        _ = options.TestDeterminism.Should().BeFalse();
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
        _ = options.EnableDebugHooks.Should().BeFalse();
        _ = options.ValidateBeforeExecution.Should().BeFalse();
        _ = options.ValidateAfterExecution.Should().BeFalse();
        _ = options.EnableCrossBackendValidation.Should().BeFalse();
        _ = options.EnablePerformanceMonitoring.Should().BeFalse();
        _ = options.TestDeterminism.Should().BeFalse();
        _ = options.AnalyzeErrorsOnFailure.Should().BeFalse();
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public void CrossValidationProbability_SetToZero_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            CrossValidationProbability = 0.0
        };

        // Assert
        _ = options.CrossValidationProbability.Should().Be(0.0);
    }

    [Fact]
    public void CrossValidationProbability_SetToOne_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            CrossValidationProbability = 1.0
        };

        // Assert
        _ = options.CrossValidationProbability.Should().Be(1.0);
    }

    [Fact]
    public void ValidationTolerance_SetToMinimumValue_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            ValidationTolerance = float.Epsilon
        };

        // Assert
        _ = options.ValidationTolerance.Should().Be(float.Epsilon);
    }

    [Fact]
    public void ValidationTolerance_SetToLargeValue_ShouldBeValid()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            // Act
            ValidationTolerance = 1.0f
        };

        // Assert
        _ = options.ValidationTolerance.Should().Be(1.0f);
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
        _ = options.EnableDebugHooks.Should().BeFalse();
        _ = options.ValidateBeforeExecution.Should().BeFalse();
        _ = options.ValidateAfterExecution.Should().BeTrue();
        _ = options.FailOnValidationErrors.Should().BeTrue();
        _ = options.EnableCrossBackendValidation.Should().BeTrue();
        _ = options.CrossValidationProbability.Should().Be(0.75);
        _ = options.ValidationTolerance.Should().Be(1e-5f);
        _ = options.EnablePerformanceMonitoring.Should().BeFalse();
        _ = options.StorePerformanceHistory.Should().BeTrue();
        _ = options.TestDeterminism.Should().BeTrue();
        _ = options.AnalyzeErrorsOnFailure.Should().BeFalse();
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
        _ = options.ValidateBeforeExecution.Should().BeTrue();
        _ = options.ValidateAfterExecution.Should().BeTrue();
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
        _ = options.EnablePerformanceMonitoring.Should().BeTrue();
        _ = options.StorePerformanceHistory.Should().BeTrue();
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
        _ = options.ValidateBeforeExecution.Should().BeTrue();
        _ = options.ValidateAfterExecution.Should().BeTrue();
        _ = options.EnableCrossBackendValidation.Should().BeTrue();
        _ = options.TestDeterminism.Should().BeTrue();
        _ = options.FailOnValidationErrors.Should().BeTrue();
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
        _ = options.FailOnValidationErrors.Should().BeTrue();
        _ = options.TestDeterminism.Should().BeTrue();
        _ = options.CrossValidationProbability.Should().Be(1.0);
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
        _ = options.EnableDebugHooks.Should().BeTrue();
        _ = options.ValidateBeforeExecution.Should().BeTrue();
        _ = options.EnablePerformanceMonitoring.Should().BeTrue();
        _ = options.FailOnValidationErrors.Should().BeFalse();
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
        _ = options.ValidateBeforeExecution.Should().BeFalse();
        _ = options.ValidateAfterExecution.Should().BeFalse();
        _ = options.EnablePerformanceMonitoring.Should().BeTrue();
        _ = options.CrossValidationProbability.Should().Be(0.01);
    }

    #endregion
}
