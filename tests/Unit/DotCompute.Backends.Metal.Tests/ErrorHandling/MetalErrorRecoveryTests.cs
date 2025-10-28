// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.ErrorHandling;
using DotCompute.Backends.Metal.ErrorHandling.Exceptions;
using DotCompute.Backends.Metal.Telemetry;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.ErrorHandling;

/// <summary>
/// Comprehensive tests for MetalErrorRecovery and recovery strategies.
/// </summary>
public sealed class MetalErrorRecoveryTests
{
    private readonly Mock<ILogger> _mockLogger;

    public MetalErrorRecoveryTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    #region AnalyzeException Tests

    [Fact]
    public void AnalyzeException_CompilationException_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalCompilationException("Shader compilation failed");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Compilation, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        // Check that suggestions contain relevant keywords
        Assert.Contains(hints.SuggestedActions, action => action.Contains("Metal Shading Language", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hints.SuggestedActions, action => action.Contains("CPU fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeException_CompilationExceptionWithUnsupported_AddsSpecificHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        // Use constructor with compilerOutput parameter to trigger the unsupported check
        var exception = new MetalCompilationException("Shader compilation failed", "Feature is unsupported on this GPU family");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Contains(hints.SuggestedActions, action => action.Contains("not be supported", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hints.SuggestedActions, action => action.Contains("different algorithm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeException_DeviceException_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalDeviceException("Device initialization failed");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Device, hints.ErrorCategory);
        Assert.False(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        Assert.Contains(hints.SuggestedActions, action => action.Contains("Metal device", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hints.SuggestedActions, action => action.Contains("Restart application", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeException_OperationException_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Operation failed");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Operation, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.RetryOperation, hints.RecoveryStrategy);
        Assert.Contains(hints.SuggestedActions, action => action.Contains("Retry", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hints.SuggestedActions, action => action.Contains("memory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeException_OperationExceptionWithMemory_AddsPriorityHint()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Out of memory");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Contains(hints.SuggestedActions, action => action.Contains("Reduce memory allocation", StringComparison.OrdinalIgnoreCase));
        // Verify the memory hint is first in the list
        Assert.Contains("Reduce memory allocation", hints.SuggestedActions.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeException_CpuFallbackRequired_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new CpuFallbackRequiredException("Feature not supported");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.UnsupportedFeature, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        Assert.Contains(hints.SuggestedActions, action => action.Contains("does not support", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hints.SuggestedActions, action => action.Contains("falling back to CPU", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeException_GenericException_ReturnsUnknownCategory()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new InvalidOperationException("Unknown error");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Unknown, hints.ErrorCategory);
        Assert.False(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        Assert.Contains(hints.SuggestedActions, action => action.Contains("unexpected error", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hints.SuggestedActions, action => action.Contains("CPU fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeException_WithoutTelemetry_WorksCorrectly()
    {
        // Arrange - Test that error recovery works without telemetry (telemetry is optional)
        var recovery = new MetalErrorRecovery(_mockLogger.Object, telemetry: null);
        var exception = new MetalCompilationException("Test error");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert - Just verify hints are returned correctly
        Assert.Equal(ErrorCategory.Compilation, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
    }

    [Fact]
    public void AnalyzeException_LogsDebugInformation()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalCompilationException("Test error");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error analysis")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region TryRecover Tests

    [Fact]
    public void TryRecover_NonRecoverableError_ReturnsFalse()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalDeviceException("Device error");
        var hints = new RecoveryHints { IsRecoverable = false };
        var actionCalled = false;

        // Act
        var result = recovery.TryRecover(exception, hints, () =>
        {
            actionCalled = true;
            return true;
        });

        // Assert
        Assert.False(result);
        Assert.False(actionCalled);
    }

    [Fact]
    public void TryRecover_RecoverableError_SuccessfulRecovery_ReturnsTrue()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Transient error");
        var hints = new RecoveryHints
        {
            IsRecoverable = true,
            RecoveryStrategy = RecoveryStrategy.RetryOperation
        };

        // Act
        var result = recovery.TryRecover(exception, hints, () => true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryRecover_RecoverableError_FailedRecovery_ReturnsFalse()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Persistent error");
        var hints = new RecoveryHints
        {
            IsRecoverable = true,
            RecoveryStrategy = RecoveryStrategy.RetryOperation
        };

        // Act
        var result = recovery.TryRecover(exception, hints, () => false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryRecover_RecoveryActionThrows_ReturnsFalse()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Error");
        var hints = new RecoveryHints
        {
            IsRecoverable = true,
            RecoveryStrategy = RecoveryStrategy.RetryOperation
        };

        // Act
        var result = recovery.TryRecover(exception, hints, () => throw new InvalidOperationException("Recovery failed"));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryRecover_SuccessfulRecovery_LogsSuccess()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Error");
        var hints = new RecoveryHints
        {
            IsRecoverable = true,
            RecoveryStrategy = RecoveryStrategy.RetryOperation
        };

        // Act
        recovery.TryRecover(exception, hints, () => true);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("successful")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TryRecover_FailedRecovery_LogsWarning()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Error");
        var hints = new RecoveryHints
        {
            IsRecoverable = true,
            RecoveryStrategy = RecoveryStrategy.RetryOperation
        };

        // Act
        recovery.TryRecover(exception, hints, () => false);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void TryRecover_RecoveryThrows_LogsError()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Error");
        var hints = new RecoveryHints
        {
            IsRecoverable = true,
            RecoveryStrategy = RecoveryStrategy.RetryOperation
        };

        // Act
        recovery.TryRecover(exception, hints, () => throw new InvalidOperationException("Recovery exception"));

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("threw exception")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RecoveryHints Tests

    [Fact]
    public void RecoveryHints_DefaultState_HasEmptyLists()
    {
        // Arrange & Act
        var hints = new RecoveryHints();

        // Assert
        Assert.NotNull(hints.SuggestedActions);
        Assert.Empty(hints.SuggestedActions);
        Assert.Equal(ErrorCategory.Unknown, hints.ErrorCategory);
        Assert.False(hints.IsRecoverable);
        Assert.False(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.None, hints.RecoveryStrategy);
    }

    [Fact]
    public void RecoveryHints_SuggestedActions_CanBeModified()
    {
        // Arrange
        var hints = new RecoveryHints();

        // Act
        hints.SuggestedActions.Add("Action 1");
        hints.SuggestedActions.Add("Action 2");

        // Assert
        Assert.Equal(2, hints.SuggestedActions.Count);
        Assert.Contains("Action 1", hints.SuggestedActions);
        Assert.Contains("Action 2", hints.SuggestedActions);
    }

    #endregion

    #region ErrorCategory Tests

    [Fact]
    public void ErrorCategory_AllValues_AreDefined()
    {
        // Arrange & Act
        var categories = Enum.GetValues<ErrorCategory>();

        // Assert
        Assert.Contains(ErrorCategory.Unknown, categories);
        Assert.Contains(ErrorCategory.Compilation, categories);
        Assert.Contains(ErrorCategory.Device, categories);
        Assert.Contains(ErrorCategory.Operation, categories);
        Assert.Contains(ErrorCategory.UnsupportedFeature, categories);
    }

    #endregion

    #region RecoveryStrategy Tests

    [Fact]
    public void RecoveryStrategy_AllValues_AreDefined()
    {
        // Arrange & Act
        var strategies = Enum.GetValues<RecoveryStrategy>();

        // Assert
        Assert.Contains(RecoveryStrategy.None, strategies);
        Assert.Contains(RecoveryStrategy.RetryOperation, strategies);
        Assert.Contains(RecoveryStrategy.UseCpuFallback, strategies);
        Assert.Contains(RecoveryStrategy.ReinitializeDevice, strategies);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullRecoveryWorkflow_CompilationError_WithCpuFallback()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalCompilationException("Shader compilation failed");
        var fallbackExecuted = false;

        // Act
        var hints = recovery.AnalyzeException(exception);
        var recovered = recovery.TryRecover(exception, hints, () =>
        {
            fallbackExecuted = true;
            return true;
        });

        // Assert
        Assert.True(recovered);
        Assert.True(fallbackExecuted);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
    }

    [Fact]
    public void FullRecoveryWorkflow_OperationError_WithRetry()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalOperationException("Transient failure");
        var retryCount = 0;

        // Act
        var hints = recovery.AnalyzeException(exception);
        var recovered = recovery.TryRecover(exception, hints, () =>
        {
            retryCount++;
            return retryCount >= 1; // Succeed on first retry
        });

        // Assert
        Assert.True(recovered);
        Assert.Equal(1, retryCount);
        Assert.Equal(RecoveryStrategy.RetryOperation, hints.RecoveryStrategy);
    }

    [Fact]
    public void FullRecoveryWorkflow_DeviceError_NonRecoverable()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_mockLogger.Object);
        var exception = new MetalDeviceException("Hardware failure");
        var actionCalled = false;

        // Act
        var hints = recovery.AnalyzeException(exception);
        var recovered = recovery.TryRecover(exception, hints, () =>
        {
            actionCalled = true;
            return true;
        });

        // Assert
        Assert.False(recovered);
        Assert.False(actionCalled);
        Assert.False(hints.IsRecoverable);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MetalErrorRecovery(null!));
    }

    [Fact]
    public void Constructor_WithLogger_Succeeds()
    {
        // Arrange & Act
        var recovery = new MetalErrorRecovery(_mockLogger.Object);

        // Assert
        Assert.NotNull(recovery);
    }

    [Fact]
    public void Constructor_WithLoggerAndNullTelemetry_Succeeds()
    {
        // Arrange & Act
        var recovery = new MetalErrorRecovery(_mockLogger.Object, telemetry: null);

        // Assert
        Assert.NotNull(recovery);
    }

    #endregion
}
