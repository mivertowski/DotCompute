// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using DotCompute.Backends.Metal.ErrorHandling;
using DotCompute.Backends.Metal.ErrorHandling.Exceptions;

namespace DotCompute.Backends.Metal.Tests.ErrorHandling;

/// <summary>
/// Tests for Metal exception hierarchy and error recovery.
/// </summary>
public sealed class ErrorHandlingTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;

    public ErrorHandlingTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<ErrorHandlingTests>();
    }

    #region Exception Tests

    [Fact]
    public void MetalOperationException_Construction_SetsProperties()
    {
        // Act
        var exception = new MetalOperationException("buffer_allocation", "Insufficient memory");

        // Assert
        Assert.Equal("buffer_allocation", exception.Operation);
        Assert.Contains("buffer_allocation", exception.Message);
        Assert.Contains("Insufficient memory", exception.Message);
        _output.WriteLine($"Exception message: {exception.Message}");
    }

    [Fact]
    public void MetalOperationException_WithInnerException_PreservesInner()
    {
        // Arrange
        var inner = new InvalidOperationException("Inner error");

        // Act
        var exception = new MetalOperationException("command_execution", "Failed", inner);

        // Assert
        Assert.Equal("command_execution", exception.Operation);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void MetalDeviceException_Construction_SetsProperties()
    {
        // Act
        var exception = new MetalDeviceException("Apple M2 Pro", "Device unavailable");

        // Assert
        Assert.Equal("Apple M2 Pro", exception.DeviceName);
        Assert.Contains("Apple M2 Pro", exception.Message);
        Assert.Contains("Device unavailable", exception.Message);
        _output.WriteLine($"Exception message: {exception.Message}");
    }

    [Fact]
    public void MetalDeviceException_WithInnerException_PreservesInner()
    {
        // Arrange
        var inner = new Exception("Hardware error");

        // Act
        var exception = new MetalDeviceException("Apple M1", "Failed", inner);

        // Assert
        Assert.Equal("Apple M1", exception.DeviceName);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void MetalCompilationException_Construction_SetsProperties()
    {
        // Act
        var exception = new MetalCompilationException(
            "Compilation failed",
            "error: syntax error at line 42",
            "kernel void test() { invalid_syntax }",
            -1);

        // Assert
        Assert.Contains("Compilation failed", exception.Message);
        Assert.Equal("error: syntax error at line 42", exception.CompilerOutput);
        Assert.Contains("invalid_syntax", exception.SourceCode);
        Assert.Equal(-1, exception.ErrorCode);
        _output.WriteLine($"Compiler output: {exception.CompilerOutput}");
    }

    [Fact]
    public void MetalCompilationException_NoOptionalParams_WorksCorrectly()
    {
        // Act
        var exception = new MetalCompilationException("Simple error");

        // Assert
        Assert.Contains("Simple error", exception.Message);
        Assert.Null(exception.CompilerOutput);
        Assert.Null(exception.SourceCode);
        Assert.Null(exception.ErrorCode);
    }

    [Fact]
    public void CpuFallbackRequiredException_Construction_SetsProperties()
    {
        // Act
        var exception = new CpuFallbackRequiredException("Unsupported atomic operation");

        // Assert
        Assert.Equal("Unsupported atomic operation", exception.Reason);
        Assert.Contains("CPU fallback required", exception.Message);
        Assert.Contains("Unsupported atomic operation", exception.Message);
        _output.WriteLine($"Exception message: {exception.Message}");
    }

    [Fact]
    public void CpuFallbackRequiredException_WithInnerException_PreservesInner()
    {
        // Arrange
        var inner = new NotSupportedException("Not implemented");

        // Act
        var exception = new CpuFallbackRequiredException("Feature not available", inner);

        // Assert
        Assert.Equal("Feature not available", exception.Reason);
        Assert.Same(inner, exception.InnerException);
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public void AnalyzeException_CompilationError_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new MetalCompilationException(
            "Compilation failed",
            "error: unsupported feature");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Compilation, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        Assert.NotEmpty(hints.SuggestedActions);
        Assert.Contains(hints.SuggestedActions, a => a.Contains("CPU fallback"));

        _output.WriteLine($"Recovery strategy: {hints.RecoveryStrategy}");
        foreach (var action in hints.SuggestedActions)
        {
            _output.WriteLine($"  - {action}");
        }
    }

    [Fact]
    public void AnalyzeException_DeviceError_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new MetalDeviceException("Apple M1", "Device lost");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Device, hints.ErrorCategory);
        Assert.False(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        Assert.NotEmpty(hints.SuggestedActions);

        _output.WriteLine($"Device error - Recoverable: {hints.IsRecoverable}");
    }

    [Fact]
    public void AnalyzeException_OperationError_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new MetalOperationException("buffer_allocation", "Out of memory");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Operation, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.RetryOperation, hints.RecoveryStrategy);
        Assert.Contains(hints.SuggestedActions, a => a.Contains("Retry"));

        _output.WriteLine($"Operation error - Strategy: {hints.RecoveryStrategy}");
    }

    [Fact]
    public void AnalyzeException_FallbackRequired_ReturnsCorrectHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new CpuFallbackRequiredException("Double precision not supported");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.UnsupportedFeature, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        Assert.Contains(hints.SuggestedActions, a => a.Contains("CPU backend"));

        _output.WriteLine($"Fallback required - Reason included: {hints.SuggestedActions[0]}");
    }

    [Fact]
    public void AnalyzeException_GenericException_ReturnsDefaultHints()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new InvalidOperationException("Unknown error");

        // Act
        var hints = recovery.AnalyzeException(exception);

        // Assert
        Assert.Equal(ErrorCategory.Unknown, hints.ErrorCategory);
        Assert.False(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);

        _output.WriteLine("Generic exception handled with fallback strategy");
    }

    [Fact]
    public void TryRecover_RecoverableError_CallsRecoveryAction()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new MetalOperationException("test", "recoverable");
        var hints = recovery.AnalyzeException(exception);
        var actionCalled = false;

        // Act
        var success = recovery.TryRecover(exception, hints, () =>
        {
            actionCalled = true;
            return true;
        });

        // Assert
        Assert.True(success);
        Assert.True(actionCalled);
        _output.WriteLine("Recovery action executed successfully");
    }

    [Fact]
    public void TryRecover_NonRecoverableError_ReturnsFalse()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new MetalDeviceException("device", "fatal");
        var hints = recovery.AnalyzeException(exception);
        var actionCalled = false;

        // Act
        var success = recovery.TryRecover(exception, hints, () =>
        {
            actionCalled = true;
            return true;
        });

        // Assert
        Assert.False(success);
        Assert.False(actionCalled);
        _output.WriteLine("Non-recoverable error correctly prevented recovery attempt");
    }

    [Fact]
    public void TryRecover_RecoveryActionFails_ReturnsFalse()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new MetalOperationException("test", "recoverable");
        var hints = recovery.AnalyzeException(exception);

        // Act
        var success = recovery.TryRecover(exception, hints, () => false);

        // Assert
        Assert.False(success);
        _output.WriteLine("Failed recovery action correctly returned false");
    }

    [Fact]
    public void TryRecover_RecoveryActionThrows_ReturnsFalse()
    {
        // Arrange
        var recovery = new MetalErrorRecovery(_logger);
        var exception = new MetalOperationException("test", "recoverable");
        var hints = recovery.AnalyzeException(exception);

        // Act
        var success = recovery.TryRecover(exception, hints, () =>
        {
            throw new Exception("Recovery failed");
        });

        // Assert
        Assert.False(success);
        _output.WriteLine("Exception in recovery action correctly handled");
    }

    [Fact]
    public void RecoveryHints_AllProperties_Work()
    {
        // Arrange & Act
        var hints = new RecoveryHints
        {
            ErrorCategory = ErrorCategory.Compilation,
            IsRecoverable = true,
            FallbackAvailable = true,
            RecoveryStrategy = RecoveryStrategy.UseCpuFallback,
            SuggestedActions = new List<string> { "Action 1", "Action 2" }
        };

        // Assert
        Assert.Equal(ErrorCategory.Compilation, hints.ErrorCategory);
        Assert.True(hints.IsRecoverable);
        Assert.True(hints.FallbackAvailable);
        Assert.Equal(RecoveryStrategy.UseCpuFallback, hints.RecoveryStrategy);
        Assert.Equal(2, hints.SuggestedActions.Count);
    }

    [Theory]
    [InlineData(ErrorCategory.Unknown)]
    [InlineData(ErrorCategory.Compilation)]
    [InlineData(ErrorCategory.Device)]
    [InlineData(ErrorCategory.Operation)]
    [InlineData(ErrorCategory.UnsupportedFeature)]
    public void ErrorCategory_AllValues_AreValid(ErrorCategory category)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(ErrorCategory), category));
        _output.WriteLine($"Error category {category} is valid");
    }

    [Theory]
    [InlineData(RecoveryStrategy.None)]
    [InlineData(RecoveryStrategy.RetryOperation)]
    [InlineData(RecoveryStrategy.UseCpuFallback)]
    [InlineData(RecoveryStrategy.ReinitializeDevice)]
    public void RecoveryStrategy_AllValues_AreValid(RecoveryStrategy strategy)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(RecoveryStrategy), strategy));
        _output.WriteLine($"Recovery strategy {strategy} is valid");
    }

    #endregion
}
